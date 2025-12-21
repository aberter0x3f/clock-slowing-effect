using Godot;

namespace Curio;

[GlobalClass]
public partial class TotemOfUndyingCurio : BaseCurio {
  public override CurioType Type => CurioType.TotemOfUndying;
  public override string Name => "Totem of Undying";
  public override string Description => "Passive: When your health drops to 1s or less, it is instantly set to 50% of your maximum health. This curio is consumed upon activation.";
  public override bool HasActiveEffect => false;
  public override bool HasPassiveEffect => true;
  public override float Cooldown => 0f;

  public override void OnUpdate(Player player, float scaledDelta) {
    if (player.Health <= 1.0f) {
      GD.Print("Totem of Undying triggered!");
      SoundManager.Instance.Play(SoundEffect.CurioUse);
      player.Health = player.Stats.MaxHealth * 0.5f;
      GameManager.Instance.RemoveCurio(this, player);
    }
  }
}
