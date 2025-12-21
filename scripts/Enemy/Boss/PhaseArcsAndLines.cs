using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseArcsAndLinesState : BasePhaseState {
  public PhaseArcsAndLines.AttackState CurrentState;
  public float Timer;
  public int VolleyCounter;
  public bool IsCurrentPhaseInverted;
  public float Theta0;
  public float DefenseTimer;
}

public partial class PhaseArcsAndLines : BasePhase {
  public enum AttackState { MovingToPosition, BetweenVolleys, BetweenPhases }

  [ExportGroup("Movement")]
  [Export] public Vector3 TargetPosition { get; set; } = new(0, 0, -3.0f);
  [Export] public float MoveSpeed { get; set; } = 5.0f;

  [ExportGroup("Attack Pattern")]
  [Export] public int BulletCount { get; set; } = 20;
  [Export] public int VolleyCount { get; set; } = 8;
  [Export] public float VolleyInterval { get; set; } = 0.1f;
  [Export] public float PhaseInterval { get; set; } = 0.1f;

  [ExportGroup("Bullet Path")]
  [Export] public float PrimaryRadius { get; set; } = 3.0f;
  [Export] public float SecondaryRadius { get; set; } = 2.5f;
  [Export] public float QuarterArcDuration { get; set; } = 0.8f;
  [Export] public float SecondaryArcDuration { get; set; } = 1.2f;
  [Export] public float FinalLinearSpeed { get; set; } = 5.0f;

  [ExportGroup("Scene Reference")]
  [Export]
  public PackedScene BulletScene1 { get; set; }
  [Export]
  public PackedScene BulletScene2 { get; set; }

  [ExportGroup("Defense Mechanism")]
  [Export] public PackedScene DefenseBulletScene { get; set; }
  [Export] public float DefenseTriggerDistance { get; set; } = 4.0f;
  [Export] public float DefenseCooldown { get; set; } = 0.1f;
  [Export] public int DefenseBulletCount { get; set; } = 100;

  private AttackState _currentState;
  private float _timer;
  private int _volleyCounter;
  private bool _isCurrentPhaseInverted;
  private float _theta0;
  private float _defenseTimer;

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _currentState = AttackState.MovingToPosition;
    _defenseTimer = DefenseCooldown;

    float rankScale = (float) GameManager.Instance.EnemyRank / 5.0f;
    BulletCount = Mathf.RoundToInt(BulletCount * rankScale);
    SecondaryArcDuration /= rankScale;
    FinalLinearSpeed *= rankScale;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    switch (_currentState) {
      case AttackState.MovingToPosition:
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(TargetPosition, MoveSpeed * scaledDelta);
        if (ParentBoss.GlobalPosition.IsEqualApprox(TargetPosition)) StartAttackPattern();
        break;

      case AttackState.BetweenVolleys:
        _timer -= scaledDelta;
        HandleDefenseMechanism(scaledDelta);
        if (_timer <= 0) {
          if (_volleyCounter < VolleyCount) {
            FireVolley();
            ++_volleyCounter;
            _timer = VolleyInterval;
            SoundManager.Instance.Play(SoundEffect.FireSmall);
          } else {
            _currentState = AttackState.BetweenPhases;
            _timer = PhaseInterval;
          }
        }
        break;

      case AttackState.BetweenPhases:
        _timer -= scaledDelta;
        if (_timer <= 0) {
          _isCurrentPhaseInverted = !_isCurrentPhaseInverted;
          _volleyCounter = 0;
          _theta0 = GD.Randf() * Mathf.Tau;
          FireVolley();
          ++_volleyCounter;
          _currentState = AttackState.BetweenVolleys;
          _timer = VolleyInterval;
          SoundManager.Instance.Play(SoundEffect.FireBig);
        }
        break;
    }
  }

  private void FireVolley() {
    PackedScene sceneToUse = _isCurrentPhaseInverted ? BulletScene2 : BulletScene1;

    float R1 = PrimaryRadius;
    float R2 = SecondaryRadius;
    float TQ = QuarterArcDuration;
    float TS = SecondaryArcDuration;
    float LS = FinalLinearSpeed;
    bool inverted = _isCurrentPhaseInverted;
    Vector3 basePos = TargetPosition;

    for (int i = 0; i < BulletCount; ++i) {
      float angle = (Mathf.Tau / BulletCount * i) + _theta0;
      var bullet = sceneToUse.Instantiate<SimpleBullet>();

      Vector3 vRad = new Vector3(Mathf.Cos(angle), 0, -Mathf.Sin(angle));
      Vector3 vUp = Vector3.Up;
      Vector3 vTan = new Vector3(-Mathf.Sin(angle), 0, -Mathf.Cos(angle)) * (inverted ? -1 : 1);

      bullet.UpdateFunc = (t) => {
        SimpleBullet.UpdateState s = new();
        Vector2 p = Vector2.Zero; // 平面内的局部坐标 (径向距离, 高度或切向距离)
        bool useVertical = true;

        if (t <= TQ) {
          float prog = t / TQ;
          p = new Vector2(R1 * Mathf.Sin(prog * Mathf.Pi / 2), R1 - R1 * Mathf.Cos(prog * Mathf.Pi / 2));
        } else if (t <= 3 * TQ) {
          float prog = (t - TQ) / (2 * TQ);
          float ang = prog * Mathf.Pi;
          p = new Vector2(R1 / 2, R1) + new Vector2(R1 / 2 * Mathf.Cos(ang), R1 / 2 * Mathf.Sin(ang));
        } else if (t <= 4 * TQ) {
          float prog = (t - 3 * TQ) / TQ;
          float ang = Mathf.Pi + prog * Mathf.Pi / 2;
          p = new Vector2(R1, R1) + new Vector2(R1 * Mathf.Cos(ang), R1 * Mathf.Sin(ang));
        } else if (t <= 4 * TQ + TS) {
          useVertical = false;
          float prog = (t - 4 * TQ) / TS;
          float ang = -Mathf.Pi / 2 + prog * Mathf.Pi / 2;
          p = new Vector2(R1, R2) + new Vector2(R2 * Mathf.Cos(ang), R2 * Mathf.Sin(ang));
        } else {
          useVertical = false;
          float dt = t - (4 * TQ + TS);
          p = new Vector2(R1 + R2, R2 + LS * dt);
        }

        s.position = basePos + vRad * p.X + (useVertical ? vUp * p.Y : vTan * p.Y);

        return s;
      };

      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  private void HandleDefenseMechanism(float scaledDelta) {
    _defenseTimer -= scaledDelta;
    if (_defenseTimer > 0) return;

    if (IsInstanceValid(PlayerNode) && ParentBoss.GlobalPosition.DistanceTo(PlayerNode.GlobalPosition) < DefenseTriggerDistance) {
      FireDefensePattern();
      _defenseTimer = DefenseCooldown;
    }
  }

  private void FireDefensePattern() {
    if (DefenseBulletScene == null) return;
    SoundManager.Instance.Play(SoundEffect.FireBig);
    float step = Mathf.Tau / DefenseBulletCount;
    Vector3 bossPos = ParentBoss.GlobalPosition;

    for (int i = 0; i < DefenseBulletCount; ++i) {
      var bullet = DefenseBulletScene.Instantiate<SimpleBullet>();
      Vector3 dir = new Vector3(Mathf.Cos(i * step), 0, Mathf.Sin(i * step));
      bullet.UpdateFunc = (t) => {
        SimpleBullet.UpdateState s = new();
        s.position = bossPos + dir * (t * 6.0f);
        return s;
      };
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  private void StartAttackPattern() {
    _isCurrentPhaseInverted = false;
    _volleyCounter = 0;
    _theta0 = GD.Randf() * Mathf.Tau;
    FireVolley();
    ++_volleyCounter;
    _currentState = AttackState.BetweenVolleys;
    _timer = VolleyInterval;
    SoundManager.Instance.Play(SoundEffect.FireBig);
  }

  public override RewindState CaptureInternalState() {
    return new PhaseArcsAndLinesState {
      CurrentState = _currentState,
      Timer = _timer,
      VolleyCounter = _volleyCounter,
      IsCurrentPhaseInverted = _isCurrentPhaseInverted,
      Theta0 = _theta0,
      DefenseTimer = _defenseTimer
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseArcsAndLinesState pals) return;
    _currentState = pals.CurrentState; _timer = pals.Timer; _volleyCounter = pals.VolleyCounter;
    _isCurrentPhaseInverted = pals.IsCurrentPhaseInverted; _theta0 = pals.Theta0; _defenseTimer = pals.DefenseTimer;
  }
}
