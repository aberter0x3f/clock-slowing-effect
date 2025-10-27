/// <summary>
/// 代表一个玩家可以与之交互的对象的接口．
/// </summary>
public interface IInteractable {
  /// <summary>
  /// 当此对象成为或不再是玩家的主要交互目标时调用．
  /// 用于视觉反馈，例如高亮显示．
  /// </summary>
  /// <param name="highlighted">如果应高亮显示，则为 true．</param>
  void SetHighlight(bool highlighted);

  /// <summary>
  /// 当玩家按下交互键时，对此对象执行交互操作．
  /// </summary>
  void Interact();
}
