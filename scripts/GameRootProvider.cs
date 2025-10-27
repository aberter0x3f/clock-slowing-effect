using Godot;

/// <summary>
/// 一个静态辅助类，用于持有对当前活动的游戏根节点（例如 Combat 或 Title 场景的根节点）的引用．
/// 这确保了所有动态生成的对象（子弹、敌人等）都可以被正确地添加到游戏场景中，
/// 从而在场景切换时能够被正确清理．
/// </summary>
public static class GameRootProvider {
  /// <summary>
  /// 获取或设置当前的游戏根节点．
  /// </summary>
  public static Node CurrentGameRoot { get; set; }
}
