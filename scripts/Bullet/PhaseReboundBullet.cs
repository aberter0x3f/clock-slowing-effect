using System.Collections.Generic;
using Godot;

namespace Bullet;

// 用于存储预计算轨迹的单个线段
public struct TrajectorySegment {
  public Vector3 StartPoint;
  public Vector3 Direction;
  public float Duration;
  public float Speed;
}

[GlobalClass]
public partial class PhaseReboundBullet : BaseBullet {
  private List<TrajectorySegment> _trajectorySegments = new();
  private Vector3 _finalDirection;
  private Vector3 _finalSegmentStartPoint;
  private float _totalSegmentDuration = 0f;
  private bool _trajectoryInitialized = false;
  private float _positionY = 0f;

  public override void UpdateBullet(float scaledDelta) {
    base.UpdateBullet(scaledDelta);

    if (!_trajectoryInitialized) return;

    float timeOnPath = 0f;
    Vector3 currentPosition = Vector3.Zero;
    bool inFinalSegment = true;

    // 遍历所有有限时长的轨迹段
    foreach (var segment in _trajectorySegments) {
      if (TimeAlive < timeOnPath + segment.Duration) {
        // 子弹当前位于此段内
        float timeInSegment = TimeAlive - timeOnPath;
        currentPosition = segment.StartPoint + segment.Direction * (segment.Speed * timeInSegment);
        inFinalSegment = false;
        break;
      }
      timeOnPath += segment.Duration;
    }

    // 如果子弹已经完成了所有反弹
    if (inFinalSegment) {
      float timeInFinalSegment = TimeAlive - _totalSegmentDuration;
      currentPosition = _finalSegmentStartPoint + _finalDirection * (_trajectorySegments[^1].Speed * timeInFinalSegment);
    }

    GlobalPosition = currentPosition with { Y = _positionY * Mathf.Max((5f - TimeAlive) / 5f, 0) };
  }

  /// <summary>
  /// 根据初始参数，预计算子弹的完整反弹轨迹．
  /// </summary>
  public void InitializeTrajectory(Vector3 startPos, Vector3 initialDirection, float speed, Rect2 bounds, int maxRebounds) {
    _trajectorySegments.Clear();
    _totalSegmentDuration = 0f;
    _positionY = startPos.Y;

    Vector3 currentPos = startPos with { Y = 0 };
    Vector3 currentDir = initialDirection with { Y = 0 };

    for (int i = 0; i < maxRebounds; ++i) {
      // 计算到四个边界的碰撞时间
      float tX = float.MaxValue, tZ = float.MaxValue;
      if (!Mathf.IsZeroApprox(currentDir.X)) {
        float boundaryX = currentDir.X > 0 ? bounds.End.X : bounds.Position.X;
        tX = (boundaryX - currentPos.X) / (currentDir.X * speed);
      }
      if (!Mathf.IsZeroApprox(currentDir.Z)) {
        float boundaryZ = currentDir.Z > 0 ? bounds.End.Y : bounds.Position.Y;
        tZ = (boundaryZ - currentPos.Z) / (currentDir.Z * speed);
      }

      // 找到最近的碰撞点
      float hitTime = Mathf.Min(tX, tZ);

      if (hitTime <= 0.001f) {
        // 如果时间过小或为负（可能已在边界外），则停止计算
        break;
      }

      // 添加当前线段
      var segment = new TrajectorySegment {
        StartPoint = currentPos,
        Direction = currentDir,
        Duration = hitTime,
        Speed = speed
      };
      _trajectorySegments.Add(segment);
      _totalSegmentDuration += hitTime;

      // 更新位置和方向以进行下一次反弹计算
      currentPos += currentDir * speed * hitTime;

      // 反转相应的速度分量
      if (Mathf.IsEqualApprox(tX, hitTime)) {
        currentDir.X *= -1;
      } else {
        currentDir.Z *= -1;
      }
    }

    // 存储最后一次反弹后的信息
    _finalSegmentStartPoint = currentPos;
    _finalDirection = currentDir;

    _trajectoryInitialized = true;
  }
}
