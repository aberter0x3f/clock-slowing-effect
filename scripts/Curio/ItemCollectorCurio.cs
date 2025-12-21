using Godot;

namespace Curio;

[GlobalClass]
public partial class ItemCollectorCurio : BaseCurio {
  public override CurioType Type => CurioType.ItemCollector;
  public override string Name => "Item Collector";
  public override string Description => "Active: Instantly collect all Time Shards on the screen.";
  public override bool HasActiveEffect => true;
  public override bool HasPassiveEffect => false;
  public override float Cooldown => 5f;

  public override void OnUsePressed(Player player) {
    if (CurrentCooldown > 0) {
      SoundManager.Instance.Play(SoundEffect.CurioWrong);
      return;
    }

    SoundManager.Instance.Play(SoundEffect.CurioUse);

    var pickups = player.GetTree().GetNodesInGroup("pickups");
    foreach (var pickupNode in pickups) {
      if (pickupNode is TimeShard shard && !shard.IsDestroyed && shard.CurrentState != TimeShard.State.Collected) {
        shard.CollectByPlayer(player);
      }
    }

    CurrentCooldown = Cooldown;
    if (GameManager.Instance != null) GameManager.Instance.UsedSkillThisLevel = true;
  }
}
