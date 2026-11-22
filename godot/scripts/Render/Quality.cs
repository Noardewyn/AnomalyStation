using Godot;
using Anomaly.Client.Light;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Render;

/// <summary>
/// Applies a graphics preset (scope 2i, D114, D288): the lamps (P7's <see cref="LightLayer.SetPreset"/>: how far they are drawn, how many cast
/// shadows, their shadow mode), the positional shadow atlas (sized for the count, <see cref="ShadowAtlasPlan"/>) and the shadows' softness, the
/// sun's shadow (<see cref="SunShadowPlan"/>), MSAA, the 3D render scale (FSR below 100 per cent), the view distance, the ambient occlusion
/// (<see cref="AmbientOcclusionPass"/>), SSIL, the bloom and the volumetric fog (DV62). Occlusion culling is on unless <c>--occlusion=off</c> (<see cref="Occlusion"/>).
/// </summary>
public static class Quality
{
    /// <summary>Whether the viewport culls by the occluders (on; <c>--occlusion=off</c> turns it off for measurements). Set once by the game scene before the preset is applied.</summary>
    public static bool Occlusion { get; set; } = true;

    /// <summary>The world's environment, whose fog, SSAO, SSIL and bloom the preset sets; null leaves them alone. Set once by the game scene.</summary>
    public static Godot.Environment Environment { get; set; }

    /// <summary>The entity layer, whose mobs cast shadows by the preset (D288); null leaves them alone. Set once by the game scene.</summary>
    public static EntityLayer Entities { get; set; }

    /// <summary>The ambient occlusion pass under the camera, whose level is the preset's <see cref="RenderPreset.Ssao"/>; null leaves it alone. Set once by the game scene.</summary>
    public static AmbientOcclusionPass AmbientOcclusion { get; set; }

    /// <summary>Applies <paramref name="preset"/> to the viewport, the camera and the light layer (any of which may be null).</summary>
    public static void Apply(RenderPreset preset, Viewport viewport, Camera3D camera, LightLayer light)
    {
        if (viewport != null)
        {
            viewport.Msaa3D = preset.Msaa switch
            {
                0 => Viewport.Msaa.Disabled,
                2 => Viewport.Msaa.Msaa2X,
                4 => Viewport.Msaa.Msaa4X,
                _ => Viewport.Msaa.Msaa8X,
            };
            viewport.Scaling3DMode = preset.Fsr ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
            viewport.Scaling3DScale = preset.RenderScale;
            ShadowAtlasPlan atlas = ShadowAtlasPlan.For(preset.ShadowedLights, preset.ShadowAtlas);
            viewport.PositionalShadowAtlasSize = atlas.Size;
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                viewport.SetPositionalShadowAtlasQuadrantSubdiv(quadrant, atlas.Quadrants[quadrant] switch
                {
                    4 => Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv4,
                    16 => Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv16,
                    64 => Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv64,
                    _ => Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv256,
                });
            }
            viewport.UseOcclusionCulling = Occlusion;
        }
        var filter = (RenderingServer.ShadowQuality)Math.Clamp(preset.ShadowFilter, 0, 5);
        RenderingServer.PositionalSoftShadowFilterSetQuality(filter);
        RenderingServer.DirectionalSoftShadowFilterSetQuality(filter);
        SunShadowPlan sun = SunShadowPlan.For(preset.SunShadow);
        RenderingServer.DirectionalShadowAtlasSetSize(sun.Size, true);
        if (camera != null)
            camera.Far = preset.ViewDistance;
        light?.SetPreset(preset);
        if (GodotObject.IsInstanceValid(AmbientOcclusion)) // a scene before this one may have set it
            AmbientOcclusion.Configure(preset.Ssao);
        if (Entities != null)
            Entities.MobShadows = preset.CharacterShadows;
        if (Environment != null)
        {
            BeamFog.Apply(Environment, preset.Fog);
            Environment.SsaoEnabled = false; // it misses the light grid's emission; AmbientOcclusionPass darkens the whole lit view instead
            Environment.SsilEnabled = preset.Ssil;
            Environment.SsrEnabled = preset.Reflections; // it reflects what is on screen, so the light grid's emission is in it
            Environment.GlowEnabled = preset.Glow;
        }
    }
}
