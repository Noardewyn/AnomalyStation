namespace Anomaly.Protocol;

public static partial class Codec
{
    /// <summary>Encodes a <see cref="TtsMessage"/> for the Control channel.</summary>
    public static int Encode(in TtsMessage message, Span<byte> destination)
    {
        if (message.Speaker < 0)
            throw Bad("a TTS line's speaker below 0");
        if (string.IsNullOrEmpty(message.Text) || string.IsNullOrEmpty(message.Voice))
            throw Bad("a TTS line without text or voice");
        if (message.Pitch is < -Limits.MaxTtsPitch or > Limits.MaxTtsPitch || message.Volume is < 0 or > Limits.MaxTtsVolume)
            throw Bad("a TTS line's pitch or volume out of range");
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Tts);
        w.WriteVarUInt((ulong)message.Speaker);
        w.WriteString(message.Text, Limits.MaxTtsTextBytes);
        w.WriteString(message.Voice, Limits.MaxTtsVoiceBytes);
        w.WriteVarInt(message.Pitch);
        w.WriteVarUInt((ulong)message.Volume);
        w.WriteString(message.Filter, Limits.MaxTtsFilterBytes); // version 64
        if ((message.Flags & ~TtsFlags.All) != 0)
            throw Bad("a TTS line's unknown flags");
        w.WriteByte((byte)message.Flags);
        return Finish(ref w, Limits.MaxTtsMessageBytes);
    }

    /// <summary>The encoded <see cref="TtsMessage"/>.</summary>
    public static byte[] Encode(in TtsMessage message)
    {
        TtsMessage copy = message;
        return EncodeToArray(Limits.MaxTtsMessageBytes, (Span<byte> buffer) => Encode(copy, buffer));
    }

    /// <summary>Decodes a <see cref="TtsMessage"/>: a text and a voice, the pitch and volume in range.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out TtsMessage value, out ProtocolError error)
    {
        value = default;
        if (!Begin(message, MessageId.Tts, Limits.MaxTtsMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            long speaker = ReadNonNegativeLong(ref r, "a TTS line's speaker");
            string text = r.ReadString(Limits.MaxTtsTextBytes);
            string voice = r.ReadString(Limits.MaxTtsVoiceBytes);
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(voice))
                throw Bad("a TTS line without text or voice");
            long pitch = r.ReadVarInt();
            ulong volume = r.ReadVarUInt();
            string filter = r.ReadString(Limits.MaxTtsFilterBytes);
            var flags = (TtsFlags)r.ReadByte();
            if ((flags & ~TtsFlags.All) != 0)
                throw Bad("a TTS line's unknown flags");
            if (pitch is < -Limits.MaxTtsPitch or > Limits.MaxTtsPitch || volume > Limits.MaxTtsVolume)
                throw Bad("a TTS line's pitch or volume out of range");
            r.ExpectEnd();
            value = new TtsMessage(speaker, text, voice, (int)pitch, (int)volume, filter, flags);
            return true;
        }
        catch (ProtocolException e)
        {
            value = default;
            error = e.Error;
            return false;
        }
    }
}
