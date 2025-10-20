using Godot;

namespace Bullet;

public partial class SimpleBullet : BaseBullet {
  [ExportGroup("Movement")]
  [Export]
  public float InitialSpeed { get; set; } = 400.0f;
  [Export]
  public float MaxSpeed { get; set; } = -1.0f; // 负数表示无限制
  [Export]
  public Vector2 Acceleration { get; set; } = Vector2.Zero;
  [Export(PropertyHint.Range, "0.0, 1.0")]
  public float Damping { get; set; } = 0.0f; // 线性阻尼，模拟摩擦力

  [ExportGroup("Rotation")]
  [Export]
  public float AngularVelocity { get; set; } = 0.0f; // 角速度 (弧度/秒)
  [Export]
  public float AngularAcceleration { get; set; } = 0.0f; // 角加速度
  [Export(PropertyHint.Range, "0.0, 1.0")]
  public float AngularDamping { get; set; } = 0.0f; // 角阻尼

  [ExportGroup("Lifetime")]
  [Export]
  public float MaxLifetime { get; set; } = 20.0f;

  public Vector2 Velocity { get; set; }
  private float _timeAlive = 0.0f;

  public override void _Ready() {
	base._Ready();
	// 基于初始方向和速度设置初始速度向量
	Velocity = Vector2.Right.Rotated(Rotation) * InitialSpeed;
  }

  public override void _Process(double delta) {
	var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

	// --- Lifetime Check ---
	_timeAlive += scaledDelta;
	if (_timeAlive > MaxLifetime) {
	  QueueFree();
	  return;
	}

	// --- Update Velocity & Position ---
	Velocity += Acceleration * scaledDelta;
	Velocity = Velocity.Lerp(Vector2.Zero, Damping * scaledDelta); // 应用阻尼
	if (MaxSpeed > 0) {
	  var length = Velocity.Length();
	  if (length > MaxSpeed) {
		Velocity = Velocity * (MaxSpeed / length);
	  }
	}
	Position += Velocity * scaledDelta;

	// --- Update Angular Velocity & Rotation ---
	AngularVelocity += AngularAcceleration * scaledDelta;
	AngularVelocity = Mathf.Lerp(AngularVelocity, 0, AngularDamping * scaledDelta); // 应用角阻尼
	Rotation += AngularVelocity * scaledDelta;
  }
}
