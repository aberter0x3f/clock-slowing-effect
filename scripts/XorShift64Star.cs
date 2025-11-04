/// <summary>
/// 一个使用 xorshift64* 算法的确定性伪随机数生成器．
/// 它的状态可以被保存和恢复，使其适用于可回溯的系统．
/// </summary>
public class XorShift64Star {
  private ulong _state;

  public ulong State {
    get => _state;
    set {
      // 种子必须为非零
      _state = (value == 0) ? 1 : value;
    }
  }

  public XorShift64Star(ulong seed) {
    State = seed;
  }

  /// <summary>
  /// 生成序列中的下一个伪随机数．
  /// </summary>
  public ulong Next() {
    _state ^= _state >> 12;
    _state ^= _state << 25;
    _state ^= _state >> 27;
    return _state * 0x2545F4914F6CDD1DUL;
  }

  /// <summary>
  /// 返回一个在 min 和 max (含) 之间的伪随机整数．
  /// </summary>
  public int RandiRange(int min, int max) {
    if (min > max) {
      (min, max) = (max, min);
    }
    // +1 因为范围是包含的
    ulong range = (ulong) (max - min + 1);
    if (range == 0) return min;
    // 模运算可能会在 range 不是 2 的幂时引入微小偏差，
    // 但对于本游戏的目的来说，这是可以接受的．
    return min + (int) (Next() % range);
  }
}
