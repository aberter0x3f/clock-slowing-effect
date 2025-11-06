using Godot;

namespace Curio;

[GlobalClass]
public partial class ZAxisEnhancementCurio : BaseCurio {
  public override CurioType Type => CurioType.ZAxisEnhancement;
  public override string Name => "Z-Axis Enhancement";
  public override string Description => "Active: Perform a 1-second jump, becoming briefly immune to enemy bullets on the ground plane.";
  public override bool HasActiveEffect => true;
  public override bool HasPassiveEffect => false;
  public override float Cooldown => 20f;

  public override void OnUseReleased(Player player) {
    if (CurrentCooldown > 0 || player.IsJumping) {
      SoundManager.Instance.PlaySoundEffect(WrongSound, cooldown: 0.1f);
      return;
    }

    SoundManager.Instance.PlaySoundEffect(SkillSound, cooldown: 0.1f);

    player.Jump();
    CurrentCooldown = Cooldown;
    if (GameManager.Instance != null) GameManager.Instance.UsedSkillThisLevel = true;
  }
}
