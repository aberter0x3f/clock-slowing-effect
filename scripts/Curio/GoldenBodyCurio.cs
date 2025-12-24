using Godot;

namespace Curio;

[GlobalClass]
public partial class GoldenBodyCurio : BaseCurio {
  private const float MAX_DURATION = 2.0f;
  private float _activeTimer = 0f;

  public override CurioType Type => CurioType.GoldenBody;
  public override string Name => "Golden Body";
  public override string Description => "Active: Press and hold to become invincible and immobile for up to 2 seconds. Releasing the key ends the effect early.";
  public override bool HasActiveEffect => true;
  public override bool HasPassiveEffect => false;
  public override float Cooldown => 20f;

  public override void OnUsePressed(Player player) {
    if (CurrentCooldown > 0) {
      SoundManager.Instance.Play(SoundEffect.CurioWrong);
      return;
    }

    SoundManager.Instance.Play(SoundEffect.CurioUse);

    player.IsGoldenBody = true;
    _activeTimer = MAX_DURATION;
    if (GameManager.Instance != null) GameManager.Instance.UsedSkillThisLevel = true;
  }

  public override void OnUseHeld(Player player, float scaledDelta) {
    if (!player.IsGoldenBody) return; // 效果可能已被取消

    _activeTimer -= scaledDelta;
    if (_activeTimer <= 0) {
      // 持续时间到，强制结束
      OnUseReleased(player);
    }
  }

  public override void OnUseReleased(Player player) {
    if (!player.IsGoldenBody) return; // 避免重复触发

    player.IsGoldenBody = false;
    _activeTimer = 0f;
    CurrentCooldown = Cooldown;
  }

  public override void OnUseCancelled(Player player) {
    OnUseReleased(player);
  }
}
