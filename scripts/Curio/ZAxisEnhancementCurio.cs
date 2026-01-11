using Godot;

namespace Curio;

[GlobalClass]
public partial class ZAxisEnhancementCurio : BaseCurio {
  public override CurioType Type => CurioType.ZAxisEnhancement;
  public override string Name => "Z-Axis Enhancement";
  public override string Description => "Active: Perform a 1-second jump, becoming briefly immune to enemy bullets on the ground plane.";
  public override bool HasActiveEffect => true;
  public override bool HasPassiveEffect => false;
  public override float Cooldown => 10f;

  public override void OnUsePressed(Player player) {
    if (CurrentCooldown > 0) {
      SoundManager.Instance.Play(SoundEffect.CurioWrong);
      return;
    }

    SoundManager.Instance.Play(SoundEffect.CurioUse);

    player.Velocity += new Vector3(0, 6.4f, 0);
    CurrentCooldown = Cooldown;
    if (GameManager.Instance != null) GameManager.Instance.UsedSkillThisLevel = true;
  }
}
