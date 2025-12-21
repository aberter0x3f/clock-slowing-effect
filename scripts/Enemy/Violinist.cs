using Bullet;
using Godot;
using Rewind;

namespace Enemy;

public class ViolinistState : SimpleEnemyState {
  public Violinist.AttackState CurrentAttackState;
  public float StaffCreationDist;
  public int NotesFiredCount;
  public Vector3 AttackDirection;
  public Vector3 AttackPerpendicularDir;
  public Vector3 AttackStartPosition;
  public float AttackLineLength;
}

public partial class Violinist : SimpleEnemy {
  public enum AttackState { CreatingStaff, FiringNotes }

  private AttackState _attackState = AttackState.CreatingStaff;
  private float _staffCreationDist;
  private int _notesFiredCount;
  private Vector3 _attackDirection;
  private Vector3 _attackPerpendicularDir;
  private Vector3 _attackStartPosition;
  private float _attackLineLength;

  [ExportGroup("Attack Configuration")]
  [Export] public PackedScene StaffLineBulletScene { get; set; }
  [Export] public PackedScene NoteBulletScene { get; set; }

  [Export] public float StaffLineCreationInterval { get; set; } = 0.05f;
  [Export] public float NoteCreationInterval { get; set; } = 0.02f;
  [Export] public float StaffLineSpacing { get; set; } = 0.15f;
  [Export] public float StaffBulletSpacing { get; set; } = 0.20f;
  [Export] public int NoteBulletCount { get; set; } = 50;
  [Export] public float NoteBulletAcceleration { get; set; } = 1.0f;
  [Export] public float NoteBulletMaxSpeed { get; set; } = 3.0f;

  private readonly RandomNumberGenerator _rnd = new();

  public override (float nextDelay, bool canWalk) Shoot() {
    var target = PlayerNode;
    if (target == null || !IsInstanceValid(target)) return (0.5f, true);

    if (_staffCreationDist == 0 && _notesFiredCount == 0) {
      _attackStartPosition = GlobalPosition;
      _attackDirection = (target.GlobalPosition - _attackStartPosition).Normalized() with { Y = 0 };
      _attackPerpendicularDir = _attackDirection.Rotated(Vector3.Up, Mathf.Pi / 2.0f);

      float distToTarget = _attackStartPosition.DistanceTo(target.GlobalPosition);
      _attackLineLength = Mathf.Max(5.0f, distToTarget * 1.2f);

      _attackState = AttackState.CreatingStaff;
      SoundManager.Instance.Play(SoundEffect.FireBig);
    }

    if (_attackState == AttackState.CreatingStaff) {
      if (_staffCreationDist < _attackLineLength) {
        SpawnStaffSegment();
        SoundManager.Instance.Play(SoundEffect.FireSmall);
        _staffCreationDist += StaffBulletSpacing;
        return (StaffLineCreationInterval, false);
      } else {
        _attackState = AttackState.FiringNotes;
        SoundManager.Instance.Play(SoundEffect.FireBig);
      }
    }

    if (_attackState == AttackState.FiringNotes) {
      if (_notesFiredCount < NoteBulletCount) {
        SpawnNote();
        SoundManager.Instance.Play(SoundEffect.FireSmall);
        ++_notesFiredCount;
        return (NoteCreationInterval, false);
      }
    }

    _staffCreationDist = 0;
    _notesFiredCount = 0;
    return (FireWarmUpTime, true);
  }

  private void SpawnStaffSegment() {
    if (StaffLineBulletScene == null) return;

    // 绘制 5 条平行的静止线段
    for (int i = -2; i <= 2; ++i) {
      var segment = StaffLineBulletScene.Instantiate<BaseBullet>();
      Vector3 offset = _attackPerpendicularDir * i * StaffLineSpacing;
      Vector3 spawnPos = _attackStartPosition + (_attackDirection * _staffCreationDist) + offset;

      segment.Position = spawnPos;
      segment.Rotation = Basis.LookingAt(_attackDirection).GetEuler();
      GameRootProvider.CurrentGameRoot.AddChild(segment);
    }
  }

  private void SpawnNote() {
    if (NoteBulletScene == null) return;

    // 随机选择一条线和一个位置
    int lineIndex = _rnd.RandiRange(-2, 2);
    float distOnLine = _rnd.Randf() * _attackLineLength;
    Vector3 spawnPos = _attackStartPosition + (_attackDirection * distOnLine) + (_attackPerpendicularDir * lineIndex * StaffLineSpacing);

    // 音符飞行方向：垂直于五线谱（左右随机）
    bool flip = _rnd.Randf() > 0.5f;
    Vector3 flyDir = _attackPerpendicularDir.Rotated(Vector3.Up, (float) GD.RandRange(-Mathf.Pi * 0.1, Mathf.Pi * 0.1)) * (flip ? 1 : -1);

    var note = NoteBulletScene.Instantiate<SimpleBullet>();
    var tMax = NoteBulletMaxSpeed / NoteBulletAcceleration;
    var xMax = 0.5f * NoteBulletAcceleration * tMax * tMax;
    note.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();
      s.position = spawnPos + flyDir * (t < tMax ? 0.5f * NoteBulletAcceleration * t * t : xMax + (t - tMax) * NoteBulletMaxSpeed);
      return s;
    };
    GameRootProvider.CurrentGameRoot.AddChild(note);
  }

  public override RewindState CaptureState() {
    var bs = (SimpleEnemyState) base.CaptureState();
    return new ViolinistState {
      GlobalPosition = bs.GlobalPosition,
      Velocity = bs.Velocity,
      Health = bs.Health,
      HitTimerLeft = bs.HitTimerLeft,
      IsInHitState = bs.IsInHitState,
      ShootTimer = bs.ShootTimer,
      CanWalk = bs.CanWalk,
      CurrentAttackState = _attackState,
      StaffCreationDist = _staffCreationDist,
      NotesFiredCount = _notesFiredCount,
      AttackDirection = _attackDirection,
      AttackPerpendicularDir = _attackPerpendicularDir,
      AttackStartPosition = _attackStartPosition,
      AttackLineLength = _attackLineLength
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not ViolinistState vs) return;
    _attackState = vs.CurrentAttackState;
    _staffCreationDist = vs.StaffCreationDist;
    _notesFiredCount = vs.NotesFiredCount;
    _attackDirection = vs.AttackDirection;
    _attackPerpendicularDir = vs.AttackPerpendicularDir;
    _attackStartPosition = vs.AttackStartPosition;
    _attackLineLength = vs.AttackLineLength;
  }
}
