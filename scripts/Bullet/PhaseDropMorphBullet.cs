using Godot;
using Rewind;

namespace Bullet;

public class PhaseDropMorphBulletState : BaseBulletState {
  public float MorphDuration;
  public float ExpandSpeed;
  public float FireTimer;
  public float CurrentRadius;
  public float CurrentRotation;
}

public partial class PhaseDropMorphBullet : BaseBullet {
  // 变换参数
  public Vector3 PointA { get; set; }
  public Vector3 PointB { get; set; }
  public float Radius { get; set; }
  public float SParameter { get; set; } // 范围 [-1, 1]
  public float MorphDuration { get; set; }

  // 扩张阶段参数
  public float SpinSpeed { get; set; }
  public float ExpandAcceleration { get; set; }

  public Rect2 MapBounds { get; set; }

  [Export]
  public PackedScene ProjectileScene { get; set; }
  [Export]
  public float ProjectileInterval { get; set; } = 0.03f;
  [Export]
  public float ProjectileSpeed { get; set; } = 8.0f;

  private float _fireTimer = 0f;
  private float _currentRadius;
  private float _currentRotation;
  private float _expandSpeed = 0f;

  // 缓存的向量，避免每帧重复计算
  private Vector3 _midPoint;
  private Vector3 _u; // A -> B 的单位向量
  private Vector3 _v; // 垂直单位向量
  private float _halfLineLength;
  private bool _mathInitialized = false;

  public override void _Ready() {
    _currentRadius = Radius;
    InitializeMath();
    base._Ready();
  }

  private void InitializeMath() {
    if (_mathInitialized) return;

    // 在 XZ 平面计算
    Vector3 vecAB = PointB - PointA;
    // 忽略 Y 轴差异进行计算，确保是在同一平面
    Vector3 vecFlat = new Vector3(vecAB.X, 0, vecAB.Z);
    float dist = vecFlat.Length();

    if (dist < 0.001f) {
      _u = Vector3.Right;
    } else {
      _u = vecFlat / dist;
    }

    // 垂直向量 (-u.z, u.x) 对应逆时针旋转 90 度
    _v = new Vector3(-_u.Z, 0, _u.X);

    _midPoint = (PointA + PointB) / 2.0f;
    _halfLineLength = Mathf.Pi * Radius;
    _mathInitialized = true;
  }

  public override void UpdateBullet(float scaledDelta) {
    base.UpdateBullet(scaledDelta);
    if (!_mathInitialized) InitializeMath();

    float t = TimeAlive;

    if (t <= MorphDuration) {
      // 变换阶段：A (圆形) -> 中垂线 -> B (圆形)
      float normalizedTime = t / MorphDuration;

      Vector3 currentPos;

      if (normalizedTime <= 0.5f) {
        // 第一阶段：A -> 中垂线 (0 <= nt <= 0.5)
        // localT: 0 -> 1
        float localT = normalizedTime / 0.5f;
        // 使用平滑插值 easing
        float alpha = localT * localT;

        Vector3 posA = GetCircleAPosition();
        Vector3 posLine = GetLinePosition();

        currentPos = posA.Lerp(posLine, alpha);
      } else {
        // 第二阶段：中垂线 -> B (0.5 < nt <= 1.0)
        // localT: 0 -> 1
        float localT = (normalizedTime - 0.5f) / 0.5f;
        // 使用平滑插值 easing
        float alpha = 1.0f - (1.0f - localT) * (1.0f - localT);

        Vector3 posLine = GetLinePosition();
        Vector3 posB = GetCircleBPosition();

        currentPos = posLine.Lerp(posB, alpha);
      }

      GlobalPosition = currentPos;
    } else {
      // 扩张发射阶段
      // 此时 TimeAlive > MorphDuration

      _expandSpeed += ExpandAcceleration * scaledDelta;
      _currentRadius += _expandSpeed * scaledDelta;
      _currentRotation += SpinSpeed * scaledDelta;

      // 计算当前在 B 点圆周上的角度
      // 基础角度：参考 CircleB 的生成逻辑，S=0 对应 pi, S=1 对应 0
      float baseAngle = (1.0f - SParameter) * Mathf.Pi;

      // 为了让旋转生效，我们需要基于 _u, _v 局部坐标系旋转
      float finalAngle = baseAngle + _currentRotation;

      Vector3 offset = _currentRadius * (Mathf.Cos(finalAngle) * _u + Mathf.Sin(finalAngle) * _v);
      GlobalPosition = PointB + offset;

      // 旋转子弹自身朝向外
      Vector3 outwardDir = offset.Normalized();
      Vector3 up = Vector3.Up;

      // 发射子弹逻辑
      _fireTimer -= scaledDelta;
      if (_fireTimer <= 0) {
        _fireTimer = ProjectileInterval;
        FireProjectile(outwardDir);
      }

      // 检查是否超出地图
      float minX = Mathf.Min(Mathf.Abs(PointB.X + _currentRadius), Mathf.Abs(PointB.X - _currentRadius));
      float minZ = Mathf.Min(Mathf.Abs(PointB.Z + _currentRadius), Mathf.Abs(PointB.Z - _currentRadius));

      if (!MapBounds.HasPoint(new Vector2(minX, minZ))) {
        Destroy();
      }
    }
  }

  private void FireProjectile(Vector3 direction) {
    var proj = ProjectileScene.Instantiate<SimpleBullet>();
    Vector3 startPos = GlobalPosition;
    float speed = ProjectileSpeed;

    proj.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();
      s.position = startPos + direction * (speed * t + 0.1f);
      return s;
    };

    GameRootProvider.CurrentGameRoot.AddChild(proj);
  }

  private Vector3 GetCircleAPosition() {
    // A + r * (cos(s*pi)*u + sin(s*pi)*v)
    float angle = SParameter * Mathf.Pi;
    return PointA + Radius * (Mathf.Cos(angle) * _u + Mathf.Sin(angle) * _v);
  }

  private Vector3 GetLinePosition() {
    // M + (s * pi * r) * v
    // 中垂线长度对应半圆周长
    return _midPoint + (SParameter * _halfLineLength) * _v;
  }

  private Vector3 GetCircleBPosition() {
    // B + r * (cos((1-s)*pi)*u + sin((1-s)*pi)*v)
    float angle = (1.0f - SParameter) * Mathf.Pi;
    return PointB + Radius * (Mathf.Cos(angle) * _u + Mathf.Sin(angle) * _v);
  }

  public override RewindState CaptureState() {
    var bs = (BaseBulletState) base.CaptureState();
    return new PhaseDropMorphBulletState {
      GlobalPosition = bs.GlobalPosition,
      GlobalRotation = bs.GlobalRotation,
      WasGrazed = bs.WasGrazed,
      IsGrazing = bs.IsGrazing,
      Modulate = bs.Modulate,
      TimeAlive = bs.TimeAlive,
      MorphDuration = MorphDuration,
      ExpandSpeed = _expandSpeed,
      FireTimer = _fireTimer,
      CurrentRadius = _currentRadius,
      CurrentRotation = _currentRotation
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not PhaseDropMorphBulletState s) return;
    MorphDuration = s.MorphDuration;
    _expandSpeed = s.ExpandSpeed;
    _fireTimer = s.FireTimer;
    _currentRadius = s.CurrentRadius;
    _currentRotation = s.CurrentRotation;

    InitializeMath();
  }
}
