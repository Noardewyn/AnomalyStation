using Godot;

namespace Anomaly.Client.Render;

// DEVIATION(DV62): tg draws no air between the viewer and the floor. A first-person view sees a flashlight's beam from the
// side, and a beam is seen by the air it lights: the station's air is a thin volumetric fog that only the Godot lights
// (the shadowed lamps a little, the flashlights fully) light up. The grid's light is the materials' and lights no fog, so
// the fog is invisible where no Godot light shines.

/// <summary>The volumetric fog that shows the beams of light.</summary>
public static class BeamFog
{
    /// <summary>The air's density (Godot's units, per metre): thin enough that an unlit room shows nothing.</summary>
    public const float AirDensity = 0.015f;

    /// <summary>How strongly the fog scatters forward: a beam seen along itself is brighter than seen from the side.</summary>
    public const float Anisotropy = 0.3f;

    /// <summary>How far from the camera the fog is computed (metres).</summary>
    public const float Length = 32f;

    /// <summary>Applies fog level <paramref name="level"/> (0 off, 1 coarse, 2 fine) to <paramref name="environment"/>; <c>ANOMALY_FOG=0</c> turns it off for measurements and <c>ANOMALY_FOG_DENSITY</c> sets the air's density for tuning.</summary>
    public static void Apply(Godot.Environment environment, int level)
    {
        if (System.Environment.GetEnvironmentVariable("ANOMALY_FOG") == "0")
            level = 0;
        environment.VolumetricFogEnabled = level > 0;
        if (level <= 0)
            return;
        int size = level >= 2 ? 96 : 64;
        RenderingServer.EnvironmentSetVolumetricFogVolumeSize(size, size);
        environment.VolumetricFogDensity = EnvFloat("ANOMALY_FOG_DENSITY", AirDensity);
        environment.VolumetricFogAlbedo = new Color(1f, 1f, 1f);
        environment.VolumetricFogEmission = new Color(0f, 0f, 0f);
        environment.VolumetricFogAnisotropy = Anisotropy;
        environment.VolumetricFogLength = Length;
        environment.VolumetricFogDetailSpread = 2f;
        environment.VolumetricFogGIInject = 0f;
        environment.VolumetricFogAmbientInject = 0f;
        environment.VolumetricFogSkyAffect = 0f; // the starfield is seen through the windows as it is
        environment.VolumetricFogTemporalReprojectionEnabled = true;
        environment.VolumetricFogTemporalReprojectionAmount = 0.9f;
    }

    private static float EnvFloat(string name, float fallback) =>
        float.TryParse(System.Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;
}
