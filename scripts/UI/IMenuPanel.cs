namespace UI;

/// <summary>
/// 代表一个可以在「关卡间菜单」中切换的独立面板．
/// </summary>
public interface IMenuPanel {
  /// <summary>
  /// 当此面板变为可见时，由父容器调用以设置初始焦点．
  /// </summary>
  void GrabInitialFocus();
}
