namespace Anomaly.Protocol;

// Protocol version 13 (M4b audio slice 2: tg's sounds reach the clients; docs/ai/specs/protocol.md section 22). tg sends a
// /sound to one listener with SEND_SOUND (BYOND `mob << sound`); the server copies each one for the listener's session and
// sends the session's sounds of the tick in one Sounds message after the tick's Delta and Airflow. Private per session (D40).

/// <summary>What a <see cref="SoundEvent"/> does to its channel.</summary>
public enum SoundKind : byte
{
    /// <summary>A new sound on the channel (BYOND replaces what the channel played); channel 0 is BYOND's "any free channel".</summary>
    Play = 0,

    /// <summary>tg's <c>SOUND_UPDATE</c>: the channel's playing sound takes the new volume, position, mute and environment without restarting.</summary>
    Update = 1,

    /// <summary>A sound with no file: the channel stops (channel 0: every channel stops, BYOND's <c>sound(null)</c> on channel 0).</summary>
    Stop = 2,
}

/// <summary>
/// One sound for one listener (version 13): tg's <c>/sound</c> as it was when tg sent it (tg shares one datum across listeners
/// and rewrites it for each; the server copies it at the call). The numbers are the wire's: <see cref="Volume"/> in half units,
/// <see cref="Muffle"/> and <see cref="Occlusion"/> as 0..255 fractions, the source tile absolute.
/// </summary>
public readonly record struct SoundEvent
{
    public SoundKind Kind { get; init; }

    /// <summary>tg's channel (<c>SSsounds</c>' number), 1 to <see cref="Limits.MaxSoundChannel"/>; 0 only for a play on any free channel or a stop of every channel.</summary>
    public ushort Channel { get; init; }

    /// <summary>A play's file: an index (1 based) into the session's path table (<see cref="Sounds.NewPaths"/>); 0 for an update or a stop.</summary>
    public uint Path { get; init; }

    /// <summary>tg's final volume (after the falloff, the pressure factor and the preferences) in half units, 0 to <see cref="Limits.MaxSoundVolume"/>.</summary>
    public byte Volume { get; init; }

    /// <summary>D43, DV37: 1 minus tg's pressure factor without its "touching the source" floor (0 in full air, 255 in a vacuum), for the client's low-pass.</summary>
    public byte Muffle { get; init; }

    /// <summary>D43, DV39: how far the station's walls and doors stand between the source and the listener (0 an open line, up to 128 around corners, 255 sealed off).</summary>
    public byte Occlusion { get; init; }

    /// <summary>tg's <c>sound.environment</c> plus one: 0 none (<c>SOUND_ENVIRONMENT_NONE</c>, or a custom list), 1 to 26 BYOND's presets 0 to 25.</summary>
    public byte Environment { get; init; }

    /// <summary>tg's <c>repeat</c>.</summary>
    public bool Repeat { get; init; }

    /// <summary>tg's <c>status & SOUND_MUTE</c>: the channel plays on silently (a token out of range).</summary>
    public bool Mute { get; init; }

    /// <summary>
    /// Version 78 (silicons SI4): BYOND's <c>sound.wait</c> on a play: the sound waits in its channel's queue until the channel's sounds
    /// before it finish (tg's VOX announcement plays its words so).
    /// </summary>
    public bool Wait { get; init; }

    /// <summary>Whether the sound comes from a tile other than the listener's (tg wrote a non-zero x, y or z); otherwise it plays centred.</summary>
    public bool Positional { get; init; }

    /// <summary>The source tile when <see cref="Positional"/>: the listener's turf moved by tg's x (east), z (north) and y / 5 (levels).</summary>
    public byte X { get; init; }

    public byte Y { get; init; }

    public int Z { get; init; }

    /// <summary>tg's <c>frequency</c>, 0 for none: BYOND reads a magnitude up to 100 as a speed multiplier, more as a rate in Hz (tg's random frequency is 32000 to 55000).</summary>
    public float Frequency { get; init; }

    /// <summary>tg's <c>offset</c> (a token's place in its loop), 0 for none; tg's value as given.</summary>
    public float Offset { get; init; }

    /// <summary>Not on the wire: the play's file path the replica resolved from its table (null for an update or a stop, and in a decoded message).</summary>
    public string File { get; init; }

    /// <summary>tg's volume, 0 to 100.</summary>
    public float VolumeValue => Volume / 2f;
}

/// <summary>
/// Version 13, server to client on the State channel after the tick's Delta and Airflow (and after the MapGrid of a session
/// that joined in the tick), only when the session heard something: the sounds tg sent the session's mob or client in the tick,
/// in tg's order. The paths go into the session's path table, which only grows (a MapGrid, the start of every join, restarts it).
/// </summary>
public sealed class Sounds
{
    /// <summary>The table index of the first of <see cref="NewPaths"/> (the table's size plus one); 0 when there are none.</summary>
    public uint FirstPath { get; set; }

    /// <summary>The paths this message adds to the table (tg's resource paths, <c>sound/machines/airlock/airlock.ogg</c>), at most <see cref="Limits.MaxSoundPaths"/>.</summary>
    public List<string> NewPaths { get; } = new();

    /// <summary>The events, at most <see cref="Limits.MaxSoundEvents"/>.</summary>
    public List<SoundEvent> Events { get; } = new();
}
