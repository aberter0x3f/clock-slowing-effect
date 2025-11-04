using System.Collections.Generic;

public static class ListExtensions {
  public static void Shuffle<T>(this IList<T> list, Godot.RandomNumberGenerator rng) {
    int n = list.Count;
    while (n > 1) {
      --n;
      int k = rng.RandiRange(0, n);
      (list[k], list[n]) = (list[n], list[k]);
    }
  }
  /// <summary>
  /// 使用可回溯的 PRNG 来打乱列表．
  /// </summary>
  public static void Shuffle<T>(this IList<T> list, XorShift64Star rng) {
    int n = list.Count;
    while (n > 1) {
      --n;
      int k = rng.RandiRange(0, n);
      (list[k], list[n]) = (list[n], list[k]);
    }
  }
}
