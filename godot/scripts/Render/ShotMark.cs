using Godot;

namespace Anomaly.Client.Render;

/// <summary>
/// The mark a shot leaves on what it hit (shot_mark.gdshader; the owner, 2026-11-11: where a shot landed was not seen): a dark spot on the
/// surface about the hit point that stays <see cref="StaySeconds"/> and fades out over <see cref="FadeSeconds"/>. The client's own, tg has none.
/// </summary>
public partial class ShotMark : MeshInstance3D
{
    public const float StaySeconds = 10f, FadeSeconds = 3f;

    private static ShaderMaterial _material;
    private static BoxMesh _box;
    private float _age;

    /// <summary>A mark of <paramref name="radius"/> metres at <paramref name="at"/> (world space) under <paramref name="parent"/>.</summary>
    public static void Spawn(Node parent, Vector3 at, float radius)
    {
        var mark = new ShotMark
        {
            Name = "ShotMark",
            Mesh = _box ??= new BoxMesh { Size = Vector3.One },
            MaterialOverride = _material ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/shot_mark.gdshader") },
            CastShadow = ShadowCastingSetting.Off,
            Scale = Vector3.One * (2f * radius),
        };
        parent.AddChild(mark);
        mark.GlobalPosition = at;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= StaySeconds + FadeSeconds)
            QueueFree();
        else if (_age > StaySeconds)
            SetInstanceShaderParameter("strength", 1f - (_age - StaySeconds) / FadeSeconds);
    }
}
