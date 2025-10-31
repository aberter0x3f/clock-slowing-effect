using Godot;

namespace Event;

/// <summary>
/// 代表事件中的一个可选项．
/// </summary>
public partial class EventOption : RefCounted {
  public string Title { get; set; }
  public string Description { get; set; }
  public bool IsEnabled { get; set; } = true;

  public EventOption(string title, string description) {
    Title = title;
    Description = description;
  }
}
