using Godot;

namespace Curio;

[GlobalClass]
public partial class XAxisEnhancementCurio : BaseCurio {
  private MapGenerator _mapGenerator;

  public override CurioType Type => CurioType.XAxisEnhancement;
  public override string Name => "X-Axis Enhancement";
  public override string Description => "Active: When at the left or right edge of the map, instantly teleport to the opposite edge.";
  public override bool HasActiveEffect => true;
  public override bool HasPassiveEffect => false;
  public override float Cooldown => 10f;

  public override void OnAcquired(Player player) {
    _mapGenerator = player.GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
  }

  public override void OnUsePressed(Player player) {
    if (CurrentCooldown > 0) {
      SoundManager.Instance.PlaySoundEffect(WrongSound, cooldown: 0.1f);
      return;
    }

    if (_mapGenerator == null) {
      SoundManager.Instance.PlaySoundEffect(WrongSound, cooldown: 0.1f);
      GD.PrintErr("XAxisEnhancementCurio: MapGenerator not found.");
      return;
    }

    // -1 是因为边界一格永远是墙面
    float halfWidth = (_mapGenerator.MapWidth / 2f - 1) * _mapGenerator.TileSize;
    float edgeThreshold = 10f; // 允许的边缘误差

    Vector2 currentPos = player.GlobalPosition;
    Vector2 newPos = currentPos;
    bool teleported = false;

    if (currentPos.X <= -halfWidth + edgeThreshold) {
      newPos.X = -currentPos.X;
      teleported = true;
    } else if (currentPos.X >= halfWidth - edgeThreshold) {
      newPos.X = -currentPos.X;
      teleported = true;
    }

    if (teleported) {
      SoundManager.Instance.PlaySoundEffect(SkillSound, cooldown: 0.1f);
      player.GlobalPosition = newPos;
      CurrentCooldown = Cooldown;
      if (GameManager.Instance != null) GameManager.Instance.UsedSkillThisLevel = true;
    } else {
      SoundManager.Instance.PlaySoundEffect(WrongSound, cooldown: 0.1f);
    }
  }
}
