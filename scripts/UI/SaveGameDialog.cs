using Godot;

namespace UI;

public partial class SaveGameDialog : FileDialog {
  public void OnSelected(string path) {
    SaveManager.Instance.SaveGame(path);
  }
}
