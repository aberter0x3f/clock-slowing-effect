using Godot;

namespace UI;

public partial class LoadGameDialog : FileDialog {
  public void OnSelected(string path) {
    SaveManager.Instance.LoadGame(path);
  }
}
