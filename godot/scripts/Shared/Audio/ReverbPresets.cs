namespace Anomaly.Client.Shared.Audio;

/// <summary>Godot's reverb (AudioEffectReverb's fields) for one of BYOND's environments.</summary>
public readonly record struct ReverbSettings(float RoomSize, float Damping, float Wet, float PredelayMsec);

/// <summary>
/// D296: BYOND's 26 sound environments (tg's SOUND_ENVIRONMENT_*, __DEFINES/sound.dm:75-101, which BYOND hands to FMOD as its
/// I3DL2 presets) as Godot reverbs. tg writes the listener's area's environment on every sound from a turf
/// (<c>playsound_local</c>, sound.dm:157-169); the server sends it (protocol 22.1) and the client plays the voice through that
/// environment's reverb bus. Godot's reverb is a Freeverb, not FMOD's: the preset's decay time sets the room size, its high
/// frequency ratio and room HF the damping, its late reverb and early reflection levels the wet (times
/// <see cref="DefaultWetScale"/>, tunable in the dev menu until the owner's listening test, D145).
/// </summary>
public static class ReverbPresets
{
    public const float DefaultWetScale = 0.35f;

    // I3DL2: room, room HF (mB), decay (s), decay HF ratio, reflections (mB), reflections delay (s), reverb (mB), reverb delay (s)
    private static readonly (string Name, float Room, float RoomHf, float Decay, float HfRatio, float Reflections, float ReflectionsDelay, float Reverb, float ReverbDelay)[] Table =
    {
        ("generic", -1000, -100, 1.49f, 0.83f, -2602, 0.007f, 200, 0.011f),
        ("padded cell", -1000, -6000, 0.17f, 0.10f, -1204, 0.001f, 207, 0.002f),
        ("room", -1000, -454, 0.40f, 0.83f, -1646, 0.002f, 53, 0.003f),
        ("bathroom", -1000, -1200, 1.49f, 0.54f, -370, 0.007f, 1030, 0.011f),
        ("living room", -1000, -6000, 0.50f, 0.10f, -1376, 0.003f, -1104, 0.004f),
        ("stone room", -1000, -300, 2.31f, 0.64f, -711, 0.012f, 83, 0.017f),
        ("auditorium", -1000, -476, 4.32f, 0.59f, -789, 0.020f, -289, 0.030f),
        ("concert hall", -1000, -500, 3.92f, 0.70f, -1230, 0.020f, -2, 0.029f),
        ("cave", -1000, 0, 2.91f, 1.30f, -602, 0.015f, -302, 0.022f),
        ("arena", -1000, -698, 7.24f, 0.33f, -1166, 0.020f, 16, 0.030f),
        ("hangar", -1000, -1000, 10.05f, 0.23f, -602, 0.020f, 198, 0.030f),
        ("carpeted hallway", -1000, -4000, 0.30f, 0.10f, -1831, 0.002f, -1630, 0.030f),
        ("hallway", -1000, -300, 1.49f, 0.59f, -1219, 0.007f, 441, 0.011f),
        ("stone corridor", -1000, -237, 2.70f, 0.79f, -1214, 0.013f, 395, 0.020f),
        ("alley", -1000, -270, 1.49f, 0.86f, -1204, 0.007f, -4, 0.011f),
        ("forest", -1000, -3300, 1.49f, 0.54f, -2560, 0.162f, -613, 0.088f),
        ("city", -1000, -800, 1.49f, 0.67f, -2273, 0.007f, -2217, 0.011f),
        ("mountains", -1000, -2500, 1.49f, 0.21f, -2780, 0.300f, -2014, 0.100f),
        ("quarry", -1000, -1000, 1.49f, 0.83f, -10000, 0.061f, 500, 0.025f),
        ("plain", -1000, -2000, 1.49f, 0.50f, -2466, 0.179f, -2514, 0.100f),
        ("parking lot", -1000, 0, 1.65f, 1.50f, -1363, 0.008f, -1153, 0.012f),
        ("sewer pipe", -1000, -1000, 2.81f, 0.14f, 429, 0.014f, 648, 0.021f),
        ("underwater", -1000, -4000, 1.49f, 0.10f, -449, 0.007f, 1700, 0.011f),
        ("drugged", -1000, 0, 8.39f, 1.39f, -115, 0.002f, 985, 0.030f),
        ("dizzy", -1000, -400, 17.23f, 0.56f, -1713, 0.020f, -613, 0.030f),
        ("psychotic", -1000, -151, 7.56f, 0.91f, -626, 0.020f, 774, 0.030f),
    };

    public static int Count => Table.Length;

    /// <summary>The environment's name; <paramref name="environment"/> is the wire's (tg's + 1, 0 none).</summary>
    public static string Name(byte environment) => environment >= 1 && environment <= Table.Length ? Table[environment - 1].Name : "none";

    /// <summary>The reverb of the wire's environment (tg's + 1); null for none (0) or one past the table (a dry voice).</summary>
    public static ReverbSettings? For(byte environment, float wetScale = DefaultWetScale)
    {
        if (environment < 1 || environment > Table.Length)
            return null;
        var p = Table[environment - 1];
        float roomSize = Math.Clamp(p.Decay / (p.Decay + 2f), 0f, 0.98f);
        float damping = Math.Clamp(0.5f * (1f - Math.Min(p.HfRatio, 1.5f) / 1.5f) + 0.5f * Math.Min(1f, -p.RoomHf / 5000f), 0f, 1f);
        float late = MilliBel(p.Room + p.Reverb);
        float early = MilliBel(p.Room + p.Reflections);
        float wet = Math.Clamp((late + 0.5f * early) * wetScale, 0f, 0.8f);
        float predelay = Math.Clamp((p.ReflectionsDelay + p.ReverbDelay) * 1000f, 20f, 500f);
        return new ReverbSettings(roomSize, damping, wet, predelay);
    }

    private static float MilliBel(float mb) => MathF.Pow(10f, mb / 2000f);
}
