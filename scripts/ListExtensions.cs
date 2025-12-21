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
}
