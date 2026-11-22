namespace Anomaly.Protocol;

// Protocol version 63 (D307, docs/ai/specs/protocol.md section 63; the codec: Codec.V63.cs).
// - TtsMessage (server to client, Control): one line this player's client voices itself (tg's SStts.play_tts plays a TTS server's
//   audio; the port's clients synthesize, D16, DV262): the text as this player understands it, the speaker's voice and pitch, tg's
//   volume, and the entity it comes from (0: the player's own preview, not placed in the world).

/// <summary>Version 63: a line for the client's text to speech.</summary>
/// <param name="Speaker">The entity the voice comes from; 0 plays it to the player only (tg's local preview).</param>
/// <param name="Text">The line as this player understands it (tg's tts_speech_filter applied).</param>
/// <param name="Voice">One of tg's <c>SStts.available_speakers</c> (the port's ten voices).</param>
/// <param name="Pitch">tg's pitch preference, -12 to 12 semitones.</param>
/// <param name="Volume">tg's volume, 0 to <see cref="Limits.MaxTtsVolume"/> (85 for others, 60 for the speaker, plus the line's offset).</param>
/// <param name="Filter">Version 64 (D308): tg's ffmpeg filter chain of the line (the speaker's voice_filter and the line's tts_filter), or null.</param>
/// <param name="Flags">Version 64 (D308): the radio and tg's special filters.</param>
public readonly record struct TtsMessage(long Speaker, string Text, string Voice, int Pitch, int Volume, string Filter = null, TtsFlags Flags = TtsFlags.None);

/// <summary>Version 64 (D308): what kind of line a <see cref="TtsMessage"/> is.</summary>
[Flags]
public enum TtsFlags : byte
{
    None = 0,

    /// <summary>A radio broadcast, from the radio that receives it (tg's queued_radio_messages): the radio's clicks and the radio's options.</summary>
    Radio = 1,

    /// <summary>A radio line on the common frequency (tg's departmental-only option leaves it out).</summary>
    Common = 2,

    /// <summary>The listener's own line (tg's hear-self-radio option).</summary>
    Own = 4,

    /// <summary>tg's "silicon" special filter (a silicon or a robotic voice).</summary>
    Silicon = 8,

    /// <summary>tg's "radio" special filter on a spoken line (a mask's radio beeps).</summary>
    Clicks = 16,

    /// <summary>Version 65 (D312): a whispered line: a breathy voice, quieter and short-ranged.</summary>
    Whisper = 32,

    /// <summary>Version 65 (D312): a shouted line (tg's yell, "!!"): a strained voice, louder and far-reaching.</summary>
    Yell = 64,

    /// <summary>Every defined bit.</summary>
    All = Radio | Common | Own | Silicon | Clicks | Whisper | Yell,
}
