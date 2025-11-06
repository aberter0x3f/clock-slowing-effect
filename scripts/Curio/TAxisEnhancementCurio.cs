using Godot;
using Rewind;

namespace Curio;

[GlobalClass]
public partial class TAxisEnhancementCurio : BaseCurio {
  public override CurioType Type => CurioType.TAxisEnhancement;
  public override string Name => "T-Axis Enhancement";
  public override string Description => "Active: Press and hold to preview rewinding time. Release to commit.\nPassive: You do not lose health over time while rewinding.";
  public override bool HasActiveEffect => true;
  public override bool HasPassiveEffect => true;
  public override float Cooldown => 0f; // 无冷却

  public override void OnUsePressed(Player player) {
    SoundManager.Instance.PlaySoundEffect(SkillSound, cooldown: 0.1f);
    if (GameManager.Instance != null) GameManager.Instance.UsedSkillThisLevel = true;
    RewindManager.Instance.StartRewindPreview();
  }

  public override void OnUseReleased(Player player) {
    RewindManager.Instance.CommitRewind();
  }

  public override void OnUseCancelled(Player player) {
    // 如果在预览时切换，也提交回溯
    RewindManager.Instance.CommitRewind();
  }
}
