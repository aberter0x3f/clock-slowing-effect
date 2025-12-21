using Godot;

namespace Curio;

[GlobalClass]
public partial class DecoyTargetCurio : BaseCurio {
  private const float DECOY_DURATION = 5.0f;
  private Node _decoyInstance;

  public override CurioType Type => CurioType.DecoyTarget;
  public override string Name => "Decoy Target";
  public override string Description => "Active: Deploy a decoy at your current position for 5 seconds. Most enemies will target the decoy instead of you.";
  public override bool HasActiveEffect => true;
  public override bool HasPassiveEffect => false;
  public override float Cooldown => 20f;

  public override void OnUsePressed(Player player) {
    if (CurrentCooldown > 0 || IsInstanceValid(_decoyInstance)) {
      SoundManager.Instance.Play(SoundEffect.CurioWrong);
      return;
    }

    SoundManager.Instance.Play(SoundEffect.CurioUse);
    player.CreateDecoyTarget(DECOY_DURATION);
    CurrentCooldown = Cooldown;
    if (GameManager.Instance != null) GameManager.Instance.UsedSkillThisLevel = true;
  }

  public override void OnLost(Player player) {
    player.RemoveDecoyTarget();
  }
}
