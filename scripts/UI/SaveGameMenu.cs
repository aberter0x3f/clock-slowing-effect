using Godot;

namespace UI;

public partial class SaveGameMenu : Control, IMenuPanel {
  private Button _saveButton;
  private FileDialog _saveGameDialog;

  public override void _Ready() {
    _saveButton = GetNode<Button>("SaveButton");
    _saveButton.Pressed += OnSavePressed;

    _saveGameDialog = GetNode<FileDialog>("SaveGameDialog");
  }

  public void GrabInitialFocus() {
    _saveButton.GrabFocus();
  }

  private void OnSavePressed() {
    _saveGameDialog.PopupCentered();
  }

  private void OnFileSelected(string path) {
    SaveManager.Instance.SaveGame(path);
  }
}
