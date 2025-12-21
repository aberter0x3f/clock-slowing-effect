using Godot;

[GlobalClass]
public partial class SoundResource : Resource {
  [Export] public AudioStream Stream;
  [Export] public float VolumeDb = 0f;
  [Export] public float Pitch = 1f;
  [Export] public float Cooldown = 0.05f;
}
