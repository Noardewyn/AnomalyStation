namespace Anomaly.Core.DmShim;

/// <summary>
/// The BYOND <c>/sound</c> datum (BYOND ref <c>/sound/var</c>; presentation-seam.md section 4): a plain data object
/// that tg's <c>playsound</c> fills in and hands to <c>ISoundSink.Play</c> through <c>Dm.output_to</c>. Created by the
/// builtin <c>sound(...)</c> or <c>Dm.@new&lt;sound&gt;(...)</c>. A builtin value type: no world id, never
/// registered. Field defaults are BYOND's documented defaults.
/// </summary>
[DmPath("/sound")]
public class sound
{
    /// <summary>The file (or list of files) to play; null stops the channel.</summary>
    public object file;

    /// <summary>1 to play repeatedly. Default 0.</summary>
    public float repeat;

    /// <summary>1 to wait in the channel's queue, 0 to interrupt. Default 0.</summary>
    public float wait;

    /// <summary>0 for any channel, 1-1024 for a specific one. Default 0.</summary>
    public float channel;

    /// <summary>0 to 100. Default 100.</summary>
    public float volume = 100;

    /// <summary>Playback frequency or speed change. Default 0.</summary>
    public float frequency;

    /// <summary>Start time in seconds. Default 0.</summary>
    public float offset;

    /// <summary>Length in seconds (set by SoundQuery). Default 0.</summary>
    public float len;

    /// <summary>Stereo pan, -100 to 100. Default 0.</summary>
    public float pan;

    /// <summary>Extra parameters (a params text or list). Default null.</summary>
    public object @params;

    /// <summary>Pitch shift. Default 0.</summary>
    public float pitch;

    /// <summary>Channel priority. Default 0.</summary>
    public float priority;

    /// <summary>SOUND_* status flags. Default 0.</summary>
    public float status;

    /// <summary>Position relative to the listener. Default 0.</summary>
    public float x;

    /// <summary>Position relative to the listener. Default 0.</summary>
    public float y;

    /// <summary>Position relative to the listener. Default 0.</summary>
    public float z;

    /// <summary>Distance falloff. Default 1.</summary>
    public float falloff = 1;

    /// <summary>Environment preset number or list. Default -1 (none).</summary>
    public object environment = -1f;

    /// <summary>Echo parameters (a list). Default null.</summary>
    public object echo;

    /// <summary>The atom the sound is attached to. Default null.</summary>
    public object atom;

    /// <summary>PORT (not a BYOND var, DV37): set by playsound_local before each SEND_SOUND when the call's pressure_affected is
    /// FALSE, so the server's sound feed adds no vacuum muffle to a sound tg plays regardless of pressure. Per call, like volume
    /// and x/y/z, so a sound datum shared across listeners stays right.</summary>
    public bool port_pressure_unaffected;

    /// <summary>The 3D transform (a list). Default null.</summary>
    public object transform;
}
