using System.Collections.Generic;
using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseSnowState : BasePhaseState {
  public PhaseSnow.AttackState CurrentState;
  public float Timer;
  public int SeedBulletCounter;
  public int JumperSeedCounter;
  public Vector3 AttackPlaneNormal;
  public Vector3 AttackPlaneRight;
  public Vector3 TargetPosition;
  public int WaveCounter;
}

[GlobalClass]
public partial class PhaseSnow : BasePhase {
  public enum AttackState {
    MovingToPosition,
    Attack1_FiringSeeds,
    Attack1_WaitingForCompletion,
    Attack2_FiringSeeds,
    Attack2_Waiting,
  }

  private AttackState _currentState;
  private float _timer;
  private int _seedBulletCounter;
  private int _jumperSeedCounter;
  private Vector3 _attackPlaneNormal;
  private Vector3 _attackPlaneRight;
  private Vector3 _targetPosition;
  private int _waveCounter = 0;

  [ExportGroup("Scene References")]
  [Export] public PackedScene SeedBulletScene { get; set; }
  [Export] public PackedScene FeederSnowflakeBulletScene { get; set; }
  [Export] public PackedScene FeederHomingBulletScene { get; set; }
  [Export] public PackedScene JumperSnowflakeBulletScene { get; set; }

  [ExportGroup("Movement")]
  [Export] public float MoveSpeed { get; set; } = 5.0f;

  [ExportGroup("Attack 1: Graze Feeder")]
  [Export] public int FeederSnowFlakeCount { get; set; } = 6;
  [Export] public float FeederSeedFireInterval { get; set; } = 0.1f;
  [Export] public float FeederSeedLifetime { get; set; } = 0.8f;
  [Export] public float FeederSeedSpeed { get; set; } = 5.0f;
  [Export] public float FeederSnowflakeExpandDuration { get; set; } = 1f;
  [Export] public float FeederSnowflakeSize { get; set; } = 2f;
  [Export] public int FeederSnowflakeBulletsPerArm { get; set; } = 5;
  [Export] public int FeederSnowflakeBulletsPerBranch { get; set; } = 2;
  [Export] public float FeederHomingSpeed { get; set; } = 6.0f;
  [Export] public float FeederWaitTime { get; set; } = 3.0f;

  [ExportGroup("Attack 2: Jump Teacher")]
  [Export] public float JumperSeedFireInterval { get; set; } = 0.03f;
  [Export] public float JumperSeedLifetime { get; set; } = 0.3f;
  [Export] public float JumperSeedSpeed { get; set; } = 5.0f;
  [Export] public int JumperSnowflakeCount { get; set; } = 8;
  [Export] public float JumperSnowflakeSize { get; set; } = 3.0f;
  [Export] public float JumperSnowflakeExpandDuration { get; set; } = 1.0f;
  [Export] public int JumperSnowflakeBulletsPerMainArm { get; set; } = 15;
  [Export] public int JumperSnowflakeBulletsPerBranch { get; set; } = 6;
  [Export] public float JumperSnowflakeRotationSpeed { get; set; } = 0.7f; // rad/s
  [Export] public float JumperSnowflakeOutwardSpeed { get; set; } = 2f;
  [Export] public float JumperSnowflakeFormationRotationSpeed { get; set; } = 0.3f; // rad/s
  [Export] public float JumperWaitTime { get; set; } = 3.0f;


  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _targetPosition = new Vector3(0, 0, -3.0f);
    _currentState = AttackState.MovingToPosition;
    _waveCounter = 0;

    var rank = GameManager.Instance.EnemyRank;
    FeederHomingSpeed *= (rank + 3) / 8f;
    JumperSnowflakeRotationSpeed *= (rank + 3) / 8f;
    JumperSnowflakeOutwardSpeed *= (rank + 3) / 8f;
    JumperSnowflakeFormationRotationSpeed *= (rank + 3) / 8f;
    JumperWaitTime /= (rank + 3) / 8f;
    FeederWaitTime /= (rank + 3) / 8f;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    _timer -= scaledDelta;

    switch (_currentState) {
      case AttackState.MovingToPosition:
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(_targetPosition, MoveSpeed * scaledDelta);
        if (ParentBoss.GlobalPosition.IsEqualApprox(_targetPosition)) {
          // 移动到位后，总是开始攻击模式 1
          StartAttack1();
        }
        break;

      case AttackState.Attack1_FiringSeeds:
        if (_timer <= 0 && _seedBulletCounter < FeederSnowFlakeCount) {
          FireFeederSeedBullet();
          ++_seedBulletCounter;
          _timer = FeederSeedFireInterval;
        } else if (_seedBulletCounter >= FeederSnowFlakeCount) {
          // 所有种子已发射，进入等待阶段
          _currentState = AttackState.Attack1_WaitingForCompletion;
          _timer = FeederWaitTime;
        }
        break;

      case AttackState.Attack1_WaitingForCompletion:
        if (_timer <= 0) {
          // 攻击 1 结束，开始攻击 2
          StartAttack2();
        }
        break;

      case AttackState.Attack2_FiringSeeds:
        if (_timer <= 0 && _jumperSeedCounter < JumperSnowflakeCount) {
          FireJumperSeedBullet(_jumperSeedCounter);
          ++_jumperSeedCounter;
          _timer = JumperSeedFireInterval;
        } else if (_jumperSeedCounter >= JumperSnowflakeCount) {
          _currentState = AttackState.Attack2_Waiting;
          _timer = JumperWaitTime;
        }
        break;

      case AttackState.Attack2_Waiting:
        if (_timer <= 0) {
          // 攻击 2 结束，随机移动到新位置，准备下一轮循环
          ++_waveCounter;
          float newX = (float) GD.RandRange(-3.0f, 3.0f);
          _targetPosition = new Vector3(newX, 0, -3.0f);
          _currentState = AttackState.MovingToPosition;
        }
        break;
    }
  }

  private void StartAttack1() {
    _currentState = AttackState.Attack1_FiringSeeds;
    _seedBulletCounter = 0;
    _timer = 0;
    _attackPlaneNormal = ((ParentBoss.GlobalPosition - PlayerNode.GlobalPosition) with { Y = 0 }).Normalized();
    _attackPlaneRight = _attackPlaneNormal.Cross(Vector3.Up);
  }

  private void FireFeederSeedBullet() {
    if (SeedBulletScene == null) return;

    SoundManager.Instance.Play(SoundEffect.FireSmall);

    bool isReversed = _waveCounter % 2 != 0;
    float angleCounter = isReversed ? (FeederSnowFlakeCount - 1 - _seedBulletCounter) : _seedBulletCounter;
    float angle = (Mathf.Pi / (FeederSnowFlakeCount - 1)) * angleCounter;
    Vector3 direction = (_attackPlaneRight * Mathf.Cos(angle) + Vector3.Up * Mathf.Sin(angle));
    Vector3 startPos = ParentBoss.GlobalPosition;

    var seed = SeedBulletScene.Instantiate<SimpleBullet>();

    float lifetime = FeederSeedLifetime;
    float speed = FeederSeedSpeed;
    PhaseSnow self = this;

    seed.UpdateFunc = t => {
      var s = new SimpleBullet.UpdateState();
      s.position = startPos + direction * speed * t;

      if (t >= lifetime) {
        s.destroy = true;
        self.SpawnStaticSnowflake(s.position);
      }
      return s;
    };
    GameRootProvider.CurrentGameRoot.AddChild(seed);
  }

  public void SpawnStaticSnowflake(Vector3 center) {
    // 1. 计算出雪花所有组成部分的最终相对位置
    var partOffsets = new List<Vector3>();
    for (int i = 0; i < 6; ++i) {
      float armAngle = Mathf.Tau / 6 * i;
      Vector3 armDir = _attackPlaneRight * Mathf.Cos(armAngle) + Vector3.Up * Mathf.Sin(armAngle);
      // Main arm parts
      for (int j = 1; j <= FeederSnowflakeBulletsPerArm; ++j) {
        partOffsets.Add(armDir * FeederSnowflakeSize * ((float) j / FeederSnowflakeBulletsPerArm));
      }
      // Branch parts
      Vector3 branchBaseOffset = armDir * FeederSnowflakeSize * 0.5f;
      Vector3 branchDir1 = armDir.Rotated(_attackPlaneNormal, Mathf.Pi / 3);
      Vector3 branchDir2 = armDir.Rotated(_attackPlaneNormal, -Mathf.Pi / 3);
      for (int k = 1; k <= FeederSnowflakeBulletsPerBranch; ++k) {
        float branchProgress = (float) k / FeederSnowflakeBulletsPerBranch;
        partOffsets.Add(branchBaseOffset + branchDir1 * FeederSnowflakeSize * 0.4f * branchProgress);
        partOffsets.Add(branchBaseOffset + branchDir2 * FeederSnowflakeSize * 0.4f * branchProgress);
      }
    }

    // 2. 为每个部分生成一个子弹
    foreach (var offset in partOffsets) {
      // 创建视觉子弹 (雪花部分)
      var bullet = FeederSnowflakeBulletScene.Instantiate<SimpleBullet>();
      bullet.Position = center;

      float expandDuration = FeederSnowflakeExpandDuration;
      Vector3 targetPos = center + offset;

      bullet.UpdateFunc = t => {
        var s = new SimpleBullet.UpdateState();
        if (t < expandDuration) {
          s.position = center.Lerp(targetPos, t / expandDuration);
        } else {
          s.destroy = true;
          FireHomingBullet(targetPos);
        }
        return s;
      };
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  private void FireHomingBullet(Vector3 targetPos) {
    var homing = FeederHomingBulletScene.Instantiate<SimpleBullet>();
    var direction = (PlayerNode.GlobalPosition - targetPos).Normalized();
    var homingSpeed = FeederHomingSpeed;
    homing.UpdateFunc = t => new SimpleBullet.UpdateState {
      position = targetPos + direction * homingSpeed * t,
    };
    GameRootProvider.CurrentGameRoot.AddChild(homing);
  }

  private void StartAttack2() {
    _currentState = AttackState.Attack2_FiringSeeds;
    _jumperSeedCounter = 0;
    _timer = 0;
    SoundManager.Instance.Play(SoundEffect.FireBig);
  }

  private void FireJumperSeedBullet(int index) {
    if (SeedBulletScene == null) return;

    SoundManager.Instance.Play(SoundEffect.FireSmall);

    float initialAngle = Mathf.Tau / JumperSnowflakeCount * index;

    Vector3 startPos = ParentBoss.GlobalPosition;
    Vector3 direction = Vector3.Right.Rotated(Vector3.Up, initialAngle);

    var seed = SeedBulletScene.Instantiate<SimpleBullet>();

    float lifetime = JumperSeedLifetime;
    float speed = JumperSeedSpeed;
    PhaseSnow self = this;

    seed.UpdateFunc = t => {
      var s = new SimpleBullet.UpdateState();
      s.position = startPos + direction * speed * t;

      if (t >= lifetime) {
        s.destroy = true;
        self.SpawnJumperSnowflakeAt(index, s.position);
      }
      return s;
    };
    GameRootProvider.CurrentGameRoot.AddChild(seed);
  }

  private void SpawnJumperSnowflakeAt(int index, Vector3 expandStartCenter) {
    float initialAngle = Mathf.Tau / JumperSnowflakeCount * index;
    var snowflakeComponentPositions = new List<Vector3>();

    // 为每个雪花生成所有组成部分的相对位置
    for (int i = 0; i < 6; ++i) {
      float armAngle = Mathf.Tau / 6 * i;
      Vector3 armDir = Vector3.Right.Rotated(Vector3.Up, armAngle);

      // 主臂
      for (int j = 1; j <= JumperSnowflakeBulletsPerMainArm; ++j) {
        float progress = (float) j / JumperSnowflakeBulletsPerMainArm;
        snowflakeComponentPositions.Add(armDir * JumperSnowflakeSize * progress);
      }

      // 分支
      Vector3 branchBase = armDir * JumperSnowflakeSize * 0.75f;
      Vector3 branchDir1 = armDir.Rotated(Vector3.Up, Mathf.Pi / 3);
      Vector3 branchDir2 = armDir.Rotated(Vector3.Up, -Mathf.Pi / 3);
      for (int k = 1; k <= JumperSnowflakeBulletsPerBranch; ++k) {
        float progress = (float) k / JumperSnowflakeBulletsPerBranch;
        snowflakeComponentPositions.Add(branchBase + branchDir1 * JumperSnowflakeSize * 0.4f * progress);
        snowflakeComponentPositions.Add(branchBase + branchDir2 * JumperSnowflakeSize * 0.4f * progress);
      }
    }

    bool isReversed = _waveCounter % 2 != 0;

    foreach (var relPos in snowflakeComponentPositions) {
      var b = JumperSnowflakeBulletScene.Instantiate<SimpleBullet>();

      float expandDuration = JumperSnowflakeExpandDuration;
      float formRotSpeed = JumperSnowflakeFormationRotationSpeed * (isReversed ? -1 : 1);
      float outwardSpeed = JumperSnowflakeOutwardSpeed;
      float selfRotSpeed = JumperSnowflakeRotationSpeed * (isReversed ? -1 : 1);

      b.UpdateFunc = t => {
        var s = new SimpleBullet.UpdateState();
        if (t < expandDuration) {
          float progress = t / expandDuration;
          s.position = expandStartCenter.Lerp(expandStartCenter + relPos, progress);
        } else {
          float tMove = t - expandDuration;
          float formRot = formRotSpeed * tMove;
          Vector3 outwardDir = Vector3.Right.Rotated(Vector3.Up, initialAngle + formRot);
          Vector3 currentCenter = expandStartCenter + outwardDir * (outwardSpeed * tMove);
          float selfRot = selfRotSpeed * tMove;
          s.position = currentCenter + relPos.Rotated(Vector3.Up, selfRot);
        }
        return s;
      };
      GameRootProvider.CurrentGameRoot.AddChild(b);
    }
  }

  public override RewindState CaptureInternalState() => new PhaseSnowState {
    CurrentState = _currentState,
    Timer = _timer,
    SeedBulletCounter = _seedBulletCounter,
    JumperSeedCounter = _jumperSeedCounter,
    AttackPlaneNormal = _attackPlaneNormal,
    AttackPlaneRight = _attackPlaneRight,
    TargetPosition = _targetPosition,
    WaveCounter = _waveCounter,
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseSnowState pss) return;
    _currentState = pss.CurrentState;
    _timer = pss.Timer;
    _seedBulletCounter = pss.SeedBulletCounter;
    _jumperSeedCounter = pss.JumperSeedCounter;
    _attackPlaneNormal = pss.AttackPlaneNormal;
    _attackPlaneRight = pss.AttackPlaneRight;
    _targetPosition = pss.TargetPosition;
    _waveCounter = pss.WaveCounter;
  }
}
