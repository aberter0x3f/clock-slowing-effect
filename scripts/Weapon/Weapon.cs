using Enemy;
using Godot;
using Rewind;

namespace Weapon;

public class WeaponState : RewindState {
  public int CurrentAmmo;
  public bool IsReloading;
  public float TimeToReloaded;
  public float ShootTimer;
  public float OrbitAngle;
}

public abstract partial class Weapon : Node3D, IRewindable {
  public ulong InstanceId => GetInstanceId();
  public Texture2D Texture => _sprite.Texture;

  public abstract float BaseShootCooldown { get; }
  public abstract float BaseDamage { get; }
  public abstract float BaseMaxAmmo { get; }
  public abstract float BaseReloadTime { get; }

  // 散布设置 (弧度)
  public abstract float BaseSpreadNormal { get; }
  public abstract float BaseSpreadSlow { get; }

  [ExportGroup("Visuals")]
  [Export] public float OrbitRadius { get; set; } = 0.8f;
  [Export] public float OrbitSpeed { get; set; } = 2.0f;

  protected Sprite3D _sprite;
  protected Player _player;
  protected float _orbitAngle;
  protected RandomNumberGenerator _rnd = new();
  protected Node3D _currentTarget;

  public int CurrentAmmo { get; protected set; }
  public bool IsReloading { get; protected set; }
  public float TimeToReloaded { get; protected set; }
  public float ShootTimer { get; protected set; }

  // 计算后的属性 (结合玩家属性)
  public int MaxAmmoCalculated => Mathf.RoundToInt(BaseMaxAmmo * (1.0f + _player.Stats.MaxAmmoMultiplier));
  public float ReloadTimeCalculated => BaseReloadTime / (1.0f + _player.Stats.ReloadSpeed);

  // 某些武器可能需要限制玩家移动 (如 Guitar 的瞄准模式)
  public virtual bool IsImmobilizingPlayer => false;

  public virtual void Initialize(Player player) {
    _sprite = GetNode<Sprite3D>("Sprite3D");
    _player = player;

    CurrentAmmo = MaxAmmoCalculated;
    RewindManager.Instance.Register(this);
  }

  public virtual void ResetState() {
    CurrentAmmo = MaxAmmoCalculated;
    IsReloading = false;
    TimeToReloaded = 0;
    ShootTimer = 0;
    _orbitAngle = 0;
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) {
      return;
    }

    float scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    UpdateOrbit(scaledDelta);
    FindTarget();
    UpdateTimers(scaledDelta);

    if (!_player.IsPermanentlyDead && !_player.IsGoldenBody) {
      HandleInput(scaledDelta);
    }
  }

  protected virtual void UpdateOrbit(float delta) {
    _orbitAngle += OrbitSpeed * delta;
    if (_orbitAngle > Mathf.Tau) _orbitAngle -= Mathf.Tau;

    Position = new Vector3(Mathf.Cos(_orbitAngle), 0, Mathf.Sin(_orbitAngle)) * OrbitRadius;
  }

  protected void UpdateTimers(float delta) {
    if (ShootTimer > 0) ShootTimer -= delta;

    if (IsReloading) {
      TimeToReloaded -= delta * TimeManager.Instance.TimeScale;
      if (TimeToReloaded <= 0) FinishReload();
    }
  }

  protected void FindTarget() {
    _currentTarget = null;
    float closestDistSq = float.MaxValue;
    // 获取敌人组，寻找最近且未销毁的敌人
    var enemies = GetTree().GetNodesInGroup("enemies");

    foreach (Node node in enemies) {
      if (node is BaseEnemy enemy && !enemy.IsDestroyed) {
        float d = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
        if (d < closestDistSq) {
          closestDistSq = d;
          _currentTarget = enemy;
        }
      }
    }
  }

  /// <summary>
  /// 子类必须实现具体的输入处理逻辑．
  /// </summary>
  protected abstract void HandleInput(float scaledDelta);

  protected void StartReload() {
    if (IsReloading) return;
    IsReloading = true;
    TimeToReloaded = ReloadTimeCalculated;
  }

  protected void FinishReload() {
    SoundManager.Instance.Play(SoundEffect.PlayerReloadComplete);
    IsReloading = false;
    CurrentAmmo = (int) MaxAmmoCalculated;
    TimeToReloaded = 0;
  }

  /// <summary>
  /// 获取经过散布计算后的射击方向．
  /// </summary>
  protected Vector3 GetShootingDirection(float spread) {
    if (IsInstanceValid(_currentTarget)) {
      Vector3 dir = (_currentTarget.GlobalPosition - GlobalPosition).Normalized();
      float angle = (float) _rnd.Randfn(0, spread);
      return dir.Rotated(Vector3.Up, angle);
    }

    Vector3 defaultDir = GlobalTransform.Basis.X;
    return defaultDir.Rotated(Vector3.Up, (float) _rnd.Randfn(0, spread));
  }

  public virtual RewindState CaptureState() {
    return new WeaponState {
      CurrentAmmo = CurrentAmmo,
      IsReloading = IsReloading,
      TimeToReloaded = TimeToReloaded,
      ShootTimer = ShootTimer,
      OrbitAngle = _orbitAngle
    };
  }

  public virtual void RestoreState(RewindState state) {
    if (state is not WeaponState ws) return;
    CurrentAmmo = ws.CurrentAmmo;
    IsReloading = ws.IsReloading;
    TimeToReloaded = ws.TimeToReloaded;
    ShootTimer = ws.ShootTimer;
    _orbitAngle = ws.OrbitAngle;
    Position = new Vector3(Mathf.Cos(_orbitAngle), 0, Mathf.Sin(_orbitAngle)) * OrbitRadius;
  }

  public void Destroy() { QueueFree(); }
  public void Resurrect() { }

  public override void _ExitTree() {
    if (RewindManager.Instance != null) RewindManager.Instance.Unregister(this);
  }
}
