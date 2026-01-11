using System.Collections.Generic;
using System.Linq;
using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseTreeState : BasePhaseState {
  public PhaseTree.State CurrentState;
  public float Timer;
  public int SeedsFiredCount;
  public ulong RngState;
}

public partial class PhaseTree : BasePhase {
  public enum State {
    Waiting,
    FiringSeeds,
  }

  [ExportGroup("Seed Configuration")]
  [Export] public int TreeCount { get; set; } = 4; // 小树的数量
  [Export] public float SeedFireInterval { get; set; } = 0.5f;
  [Export] public float SeedFireIntervalLarge { get; set; } = 2f;
  [Export] public float SeedTravelDuration { get; set; } = 1.5f; // 所有种子飞行时间相同
  [Export] public float SeedSpawnRadiusScale { get; set; } = 0.8f;
  [Export] public PackedScene SmallSeedBulletScene { get; set; }
  [Export] public PackedScene LargeSeedBulletScene { get; set; }

  [ExportGroup("Tree Generation")]
  [Export] public PackedScene ExplosionBulletScene { get; set; }
  [Export] public PackedScene TreeBulletSceneGreen { get; set; }
  [Export] public PackedScene TreeBulletSceneRed { get; set; }
  [Export] public int SmallTreeLength { get; set; } = 80;
  [Export] public int LargeTreeMainLength { get; set; } = 160;
  [Export] public int LargeTreeBranches { get; set; } = 2;
  [Export] public float TreeGrowthInterval { get; set; } = 0.03f; // 每个树节点出现的时间间隔
  [Export] public int RedBulletInterval { get; set; } = 3; // 每 N 个子弹生成一个红色特殊子弹

  [ExportGroup("Tree Physics (Generator)")]
  [Export] public float MainHeight { get; set; } = 3.0f;
  [Export] public int NumBranches { get; set; } = 2;
  [Export] public float SpiralRadius { get; set; } = 0.3f;
  [Export] public float TurnsTotal { get; set; } = 12.0f;
  [Export] public float Wildness { get; set; } = 5.0f;
  [Export] public float DipChance { get; set; } = 1.0f;

  [ExportGroup("Explosion (Seed Arrival)")]
  [Export] public int ExplosionBulletCount { get; set; } = 4;
  [Export] public float ExplosionSpeed { get; set; } = 3.0f;

  [ExportGroup("Green Bullet (Scatter)")]
  [Export] public float GreenGravity { get; set; } = 6.0f;
  [Export] public float GreenSpeedVMean { get; set; } = 4.0f;
  [Export] public float GreenSpeedVSigma { get; set; } = 1.0f;
  [Export] public float GreenSpeedHMean { get; set; } = 3.0f;
  [Export] public float GreenSpeedHSigma { get; set; } = 1.0f;

  [ExportGroup("Red Bullet (Sniper)")]
  [Export] public PackedScene SniperBulletScene { get; set; }
  [Export] public float SniperSpeed { get; set; } = 4.0f;

  private State _currentState = State.Waiting;
  private float _timer;
  private int _seedsFiredCount;

  private MapGenerator _mapGenerator;
  private float _spawnRadius;
  private RandomNumberGenerator _rng = new();

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);

    // 初始化 RNG 和地图引用
    _rng.Seed = ((ulong) GD.Randi() << 32) | (ulong) GD.Randi();
    _mapGenerator = GetTree().Root.GetNode<MapGenerator>("GameRoot/MapGenerator");

    float w = (_mapGenerator.MapWidth / 2f - 1) * _mapGenerator.TileSize;
    float h = (_mapGenerator.MapHeight / 2f - 1) * _mapGenerator.TileSize;
    _spawnRadius = Mathf.Min(w, h) * SeedSpawnRadiusScale;

    var rank = GameManager.Instance.EnemyRank;
    float scale = rank / 5.0f;

    TreeCount = Mathf.RoundToInt(TreeCount * (scale + 1f) / 2f);
    SeedFireInterval /= scale;
    SeedFireIntervalLarge /= scale;
    TreeGrowthInterval /= scale;
    SmallTreeLength = Mathf.RoundToInt(SmallTreeLength * (scale + 1f) / 2f);
    LargeTreeMainLength = Mathf.RoundToInt(LargeTreeMainLength * (scale + 1f) / 2f);

    _currentState = State.FiringSeeds;
    _seedsFiredCount = 0;
    _timer = 0; // 立即发射第一个
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    if (_currentState == State.FiringSeeds) {
      _timer -= scaledDelta;
      if (_timer <= 0) {
        FireNextSeed();
        _timer += SeedFireInterval;
      }
    }
  }

  private void FireNextSeed() {
    // 总共发射 (TreeCount + 1) 颗种子
    // 前 TreeCount 颗是小树，最后一颗是大树
    bool isLastSeed = _seedsFiredCount >= TreeCount;
    Vector3 targetPos;

    if (isLastSeed) {
      // 最后一个种子必定瞄准玩家当前位置（投影到地面）
      targetPos = PlayerNode.GlobalPosition;
      targetPos.Y = 0;
    } else {
      // 随机选择圆内的一个点
      float angle = _rng.Randf() * Mathf.Tau;
      float r = Mathf.Sqrt(_rng.Randf()) * _spawnRadius; // 开方以保证均匀分布
      targetPos = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * r;
    }

    // 确定此树的配置
    var bulletCount = isLastSeed ? LargeTreeMainLength : SmallTreeLength;
    var branches = isLastSeed ? NumBranches : 0;
    var seedScene = isLastSeed ? LargeSeedBulletScene : SmallSeedBulletScene;

    // 发射种子
    var seed = seedScene.Instantiate<SimpleBullet>();
    Vector3 startPos = ParentBoss.GlobalPosition;
    float duration = SeedTravelDuration;

    // 使用闭包捕获种子到达后所需的所有参数
    seed.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();
      if (t < duration) {
        // 简单的线性插值加上抛物线高度
        s.position = startPos.Lerp(targetPos, t / duration);
        s.position.Y += Mathf.Sin((t / duration) * Mathf.Pi) * 2.0f;
      } else {
        s.destroy = true;
        // 种子到达，触发落地效果
        CallDeferred(nameof(OnSeedArrived), targetPos, bulletCount, branches);
      }
      return s;
    };

    GameRootProvider.CurrentGameRoot.AddChild(seed);
    SoundManager.Instance.Play(SoundEffect.FireSmall);

    ++_seedsFiredCount;
    if (isLastSeed) {
      _seedsFiredCount = 0;
      _timer += SeedFireIntervalLarge;
    }
  }

  /// <summary>
  /// 当种子到达目标点时调用．生成爆炸圈并构建树．
  /// </summary>
  private void OnSeedArrived(Vector3 rootPos, int mainLength, int branches) {
    SoundManager.Instance.Play(SoundEffect.FireBig);

    SpawnExplosionRing(rootPos);

    List<TreePoint> treeData = null;
    const int MAX_RETRIES = 100;

    for (int i = 0; i < MAX_RETRIES; ++i) {
      var candidateData = GenerateTreeData(mainLength, branches);
      // 检查是否有任何点掉到了地表以下
      if (candidateData.All(p => (rootPos.Y + p.RelativePos.Y) >= -0.01f)) {
        treeData = candidateData;
        break;
      }
    }

    if (treeData == null) {
      treeData = GenerateTreeData(mainLength, branches);
    }

    // 生成构成树的所有子弹
    // 计算树完全长成所需的时间
    float totalBuildTime = mainLength * TreeGrowthInterval;
    // 激活时间（散开/狙击）设定在长成后一小段时间
    float activationTime = totalBuildTime + 0.2f;

    for (int i = 0; i < treeData.Count; ++i) {
      var node = treeData[i];
      SpawnTreeBullet(rootPos, node.RelativePos, node.Index, activationTime);
    }
  }

  private void SpawnExplosionRing(Vector3 center) {
    var targetDir = PlayerNode.GlobalPosition - center;
    if (targetDir.IsZeroApprox()) targetDir = Vector3.Right;
    var baseAng = Mathf.Atan2(targetDir.Z, targetDir.X);

    for (int i = 0; i < ExplosionBulletCount; ++i) {
      var b = ExplosionBulletScene.Instantiate<SimpleBullet>();
      float angle = Mathf.Tau / ExplosionBulletCount * i + baseAng;
      Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

      b.UpdateFunc = (t) => new SimpleBullet.UpdateState {
        position = center + dir * (ExplosionSpeed * t)
      };
      GameRootProvider.CurrentGameRoot.AddChild(b);
    }
  }

  // 用于存储生成结果的数据结构
  private struct TreePoint {
    public int Index;
    public Vector3 RelativePos;
  }

  private double BinomialCoefficient(int n, int k) {
    if (k < 0 || k > n) return 0;
    if (k == 0 || k == n) return 1;
    if (k > n / 2) k = n - k;
    double res = 1;
    for (int i = 1; i <= k; ++i) {
      res = res * (n - i + 1) / i;
    }
    return res;
  }

  private Vector3 GetBezierPoint(float t, Vector3[] cp) {
    int n = cp.Length - 1;
    Vector3 point = Vector3.Zero;
    for (int i = 0; i <= n; ++i) {
      double b = BinomialCoefficient(n, i) * Mathf.Pow(t, i) * Mathf.Pow(1 - t, n - i);
      point += cp[i] * (float) b;
    }
    return point;
  }

  private List<TreePoint> GenerateSpiralGeometry(Vector3[] cp, int nBullets, float turns, float radius, int typeId) {
    var bullets = new List<TreePoint>();
    if (nBullets < 1) return bullets;

    for (int i = 0; i < nBullets; ++i) {
      float t = (float) i / (nBullets - 1);
      Vector3 p = GetBezierPoint(t, cp);

      // 计算切线以建立局部坐标系
      float dt = 0.01f;
      Vector3 pNext = GetBezierPoint(Mathf.Min(t + dt, 1.0f), cp);
      Vector3 tangent = (pNext - p);
      if (tangent.LengthSquared() < 1e-8) {
        tangent = Vector3.Up;
      } else {
        tangent = tangent.Normalized();
      }

      // 建立坐标基
      Vector3 reference = Vector3.Forward;
      if (Mathf.Abs(tangent.Dot(reference)) > 0.9f) {
        reference = Vector3.Right;
      }
      Vector3 normal = tangent.Cross(reference).Normalized();
      Vector3 binormal = tangent.Cross(normal);

      // 螺旋偏移逻辑
      float angle = Mathf.Tau * turns * t;
      Vector3 offset = (Mathf.Cos(angle) * normal + Mathf.Sin(angle) * binormal) * radius;

      bullets.Add(new TreePoint {
        Index = i,
        RelativePos = p + offset
      });
    }
    return bullets;
  }

  private List<TreePoint> GenerateTreeData(int mainLen, int numBranches) {
    var allData = new List<TreePoint>();

    // 1. 准备分叉索引
    var validRange = Enumerable.Range((int) (mainLen * 0.2f), (int) (mainLen * 0.6f)).ToList();
    var branchIndices = new List<int>();
    for (int i = 0; i < numBranches; ++i) {
      if (validRange.Count == 0) break;
      int idxIdx = _rng.RandiRange(0, validRange.Count - 1);
      branchIndices.Add(validRange[idxIdx]);
      validRange.RemoveAt(idxIdx);
    }
    branchIndices.Sort();

    // 2. 生成主干骨架控制点
    const int numCp = 5;
    var mainCp = new Vector3[numCp];
    mainCp[0] = Vector3.Zero;
    float hStep = MainHeight / (numCp - 1);

    for (int i = 1; i < numCp; ++i) {
      mainCp[i] = mainCp[i - 1] + new Vector3(
        _rng.RandfRange(-Wildness, Wildness),
        _rng.RandfRange(-DipChance, hStep * 2.5f),
        _rng.RandfRange(-Wildness, Wildness)
      );
      if (i == numCp - 1) {
        mainCp[i].Y = Mathf.Max(mainCp[i].Y, MainHeight);
      }
    }

    var mainBullets = GenerateSpiralGeometry(mainCp, mainLen, TurnsTotal, SpiralRadius, 0);
    allData.AddRange(mainBullets);

    // 3. 生成分支
    foreach (int idx in branchIndices) {
      int remainingLen = mainLen - idx;
      int branchLen = remainingLen / 2;
      if (branchLen < 10) continue;

      // 分支起点为主干当前点
      Vector3 startPos = mainBullets[idx].RelativePos;

      // 分支高度缩放
      float branchHFactor = (float) branchLen / mainLen;
      float targetBrH = MainHeight * branchHFactor;

      // 分支控制点
      Vector3 endP = startPos + new Vector3(
        _rng.RandfRange(-Wildness, Wildness) * 0.7f,
        targetBrH,
        _rng.RandfRange(-Wildness, Wildness) * 0.7f
      );

      Vector3 midP = (startPos + endP) / 2.0f + new Vector3(
        _rng.RandfRange(-3f, 3f),
        _rng.RandfRange(-3f, 3f),
        _rng.RandfRange(-3f, 3f)
      );

      var branchCp = new Vector3[] { startPos, midP, endP };
      float branchTurns = TurnsTotal * ((float) branchLen / mainLen);

      var branchBullets = GenerateSpiralGeometry(branchCp, branchLen, branchTurns, SpiralRadius, 1);
      allData.AddRange(branchBullets);
    }

    return allData;
  }

  /// <summary>
  /// 生成树的一个组成子弹．
  /// </summary>
  private void SpawnTreeBullet(Vector3 rootPos, Vector3 relPos, int orderIndex, float activationTime) {
    // 决定颜色：每隔 RedBulletInterval 个绿色生成一个红色
    bool isRed = (orderIndex % RedBulletInterval == 0);

    // 子弹应该出现的时间
    float appearTime = orderIndex * TreeGrowthInterval;

    // 预计算绿色子弹激活时的上抛速度向量
    Vector3 greenTossVel = Vector3.Zero;
    if (!isRed) {
      float angle = _rng.Randf() * Mathf.Tau;
      float speedV = (float) _rng.Randfn(GreenSpeedVMean, GreenSpeedVSigma);
      float speedH = (float) _rng.Randfn(GreenSpeedHMean, GreenSpeedHSigma);
      greenTossVel = new Vector3(Mathf.Cos(angle) * speedH, speedV, Mathf.Sin(angle) * speedH);
    }

    var bulletScene = isRed ? TreeBulletSceneRed : TreeBulletSceneGreen;
    var bullet = bulletScene.Instantiate<SimpleBullet>();

    // 捕获状态供 lambda 使用
    Vector3 finalStaticPos = rootPos + relPos;
    finalStaticPos.Y = Mathf.Abs(finalStaticPos.Y);

    float w = (_mapGenerator.MapWidth / 2f) * _mapGenerator.TileSize * 1.5f;
    float h = (_mapGenerator.MapHeight / 2f) * _mapGenerator.TileSize * 1.5f;

    bullet.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();

      if (t < appearTime) {
        // 尚未出生：隐藏在根部或设为不可见
        s.position = rootPos;
        s.modulate.A = 0;
      } else if (t < activationTime) {
        // 静止阶段（生长完成）
        s.position = finalStaticPos;
        s.modulate.A = 1;
      } else {
        // 激活阶段
        float actT = t - activationTime;

        if (!isRed) {
          // 绿色：受重力影响的上抛运动
          // p = p0 + v*t + 0.5*g*t^2
          Vector3 displacement = greenTossVel * actT + 0.5f * Vector3.Down * GreenGravity * actT * actT;
          s.position = finalStaticPos + displacement;
          if (s.position.Y <= 0) {
            s.position.Y = 0;
            if (Mathf.Abs(s.position.X) > w || Mathf.Abs(s.position.Z) > h) s.destroy = true;
          }
        } else {
          // 红色：销毁自身并生成自机狙
          s.destroy = true;
          CallDeferred(nameof(SpawnSniperStream), finalStaticPos);
        }
      }
      return s;
    };

    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  /// <summary>
  /// 在指定位置生成一连串自机狙子弹．
  /// </summary>
  private void SpawnSniperStream(Vector3 origin) {
    if (!IsInstanceValid(PlayerNode)) return;

    // 在生成流的瞬间确定目标方向
    Vector3 targetPos = PlayerNode.GlobalPosition;
    Vector3 dir = (targetPos - origin).Normalized();
    float speed = SniperSpeed;

    var b = SniperBulletScene.Instantiate<SimpleBullet>();

    b.UpdateFunc = (t) => new SimpleBullet.UpdateState {
      position = origin + dir * (speed * t),
    };

    GameRootProvider.CurrentGameRoot.AddChild(b);
  }

  public override RewindState CaptureInternalState() => new PhaseTreeState {
    CurrentState = _currentState,
    Timer = _timer,
    SeedsFiredCount = _seedsFiredCount,
    RngState = _rng.State
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseTreeState s) return;
    _currentState = s.CurrentState;
    _timer = s.Timer;
    _seedsFiredCount = s.SeedsFiredCount;
    _rng.State = s.RngState;
  }
}
