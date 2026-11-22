namespace Anomaly.Protocol;

// Version 13 (MessagesV13.cs, docs/ai/specs/protocol.md section 22):
//
//   Sounds (server to client, State): [u8 id = 14]
//     [count <= 32 new paths] [varuint first: 0 with no new paths, else the table index of the first] { str(1..256) }*
//     [count <= 64 events] { event }*
//   event = [u8 head]  bits 0-1 kind (0 play, 1 update, 2 stop), bit 2 repeat, bit 3 mute, bit 4 positional, bit 5 frequency,
//                      bit 6 offset, bit 7 wait (version 78: a play only; a stop sets none of bits 2-7)
//           [u16 channel 0..1024]   (0: a play on any free channel or a stop of every channel; an update names its channel)
//           play:        [varuint path index 1..65536]
//           play/update: [u8 volume, half units 0..200] [u8 muffle] [u8 occlusion] [u8 environment + 1, 0 none .. 26]
//                        positional: [u8 x 1..255] [u8 y 1..255] [varuint z 1..16]
//                        frequency:  [f32, non-zero, magnitude <= 10^6]     offset: [f32, 0 < offset <= 36000]
//
// Every rule of the earlier codecs holds: TryDecode never throws, every count is capped before anything is allocated, and a
// message that decodes encodes back to the same bytes. The path table (an index past its end) and the source tile (inside
// the map) are the replica's checks (ReplicaWorld.Apply(Sounds)).

public static partial class Codec
{
    private const int SoundHeadKindMask = 0x03;
    private const int SoundHeadRepeat = 0x04;
    private const int SoundHeadMute = 0x08;
    private const int SoundHeadPositional = 0x10;
    private const int SoundHeadFrequency = 0x20;
    private const int SoundHeadOffset = 0x40;
    private const int SoundHeadWait = 0x80; // version 78 (silicons SI4): BYOND's sound.wait, a play's only
    private const int MinSoundPathBytes = 2;
    private const int MinSoundEventBytes = 3;

    /// <summary>Encodes a <see cref="Sounds"/> message for the State channel (at most <see cref="Limits.MaxSoundsBytes"/>).</summary>
    public static int Encode(Sounds message, Span<byte> destination)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Sounds);
        w.WriteCount(message.NewPaths.Count, Limits.MaxSoundPaths);
        CheckSoundPathRange(message.FirstPath, message.NewPaths.Count);
        w.WriteVarUInt(message.FirstPath);
        foreach (string path in message.NewPaths)
        {
            if (string.IsNullOrEmpty(path))
                throw new ProtocolException(ProtocolError.BadString, "a sound path that is null or empty");
            w.WriteString(path, Limits.MaxSoundPathBytes);
        }
        w.WriteCount(message.Events.Count, Limits.MaxSoundEvents);
        foreach (SoundEvent e in message.Events)
            WriteSoundEvent(ref w, e);
        return Finish(ref w, Limits.MaxSoundsBytes);
    }

    /// <summary>The encoded <see cref="Sounds"/>.</summary>
    public static byte[] Encode(Sounds message) =>
        EncodeToArray(Limits.MaxSoundsBytes, (Span<byte> buffer) => Encode(message, buffer));

    /// <summary>Decodes a <see cref="Sounds"/> message: every cap, kind, channel, range and flag rule of section 22.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out Sounds value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Sounds, Limits.MaxSoundsBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            var result = new Sounds();
            int paths = r.ReadCount(Limits.MaxSoundPaths, MinSoundPathBytes);
            ulong first = r.ReadVarUInt();
            if (first > Limits.MaxSoundTableEntries)
                throw Bad("a sound path table index over " + Limits.MaxSoundTableEntries);
            CheckSoundPathRange((uint)first, paths);
            result.FirstPath = (uint)first;
            for (int i = 0; i < paths; i++)
            {
                string path = r.ReadString(Limits.MaxSoundPathBytes);
                if (string.IsNullOrEmpty(path))
                    throw new ProtocolException(ProtocolError.BadString, "a sound path that is null or empty");
                result.NewPaths.Add(path);
            }
            int events = r.ReadCount(Limits.MaxSoundEvents, MinSoundEventBytes);
            for (int i = 0; i < events; i++)
                result.Events.Add(ReadSoundEvent(ref r));

            r.ExpectEnd();
            value = result;
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    private static void CheckSoundPathRange(uint first, int count)
    {
        if (count == 0 ? first != 0 : first < 1 || (long)first + count - 1 > Limits.MaxSoundTableEntries)
            throw Bad("a sound path table start " + first + " for " + count + " new paths");
    }

    private static void WriteSoundEvent(ref WireWriter w, in SoundEvent e)
    {
        int head = CheckSoundEvent(e);
        w.WriteByte((byte)head);
        w.WriteUInt16(e.Channel);
        if (e.Kind == SoundKind.Stop)
            return;
        if (e.Kind == SoundKind.Play)
            w.WriteVarUInt(e.Path);
        w.WriteByte(e.Volume);
        w.WriteByte(e.Muffle);
        w.WriteByte(e.Occlusion);
        w.WriteByte(e.Environment);
        if (e.Positional)
        {
            w.WriteByte(e.X);
            w.WriteByte(e.Y);
            w.WriteVarUInt((ulong)e.Z);
        }
        if (e.Frequency != 0)
            w.WriteSingle(e.Frequency);
        if (e.Offset != 0)
            w.WriteSingle(e.Offset);
    }

    // The event's head byte, after the encoder's checks (the decoder's rules, so a sender bug throws here).
    private static int CheckSoundEvent(in SoundEvent e)
    {
        if (e.Kind is not (SoundKind.Play or SoundKind.Update or SoundKind.Stop))
            throw Bad("a sound event of kind " + (int)e.Kind);
        CheckSoundChannel(e.Kind, e.Channel);
        if (e.Kind == SoundKind.Stop)
        {
            if (e.Path != 0 || e.Repeat || e.Mute || e.Positional || e.Frequency != 0 || e.Offset != 0 || e.Volume != 0
                || e.Muffle != 0 || e.Occlusion != 0 || e.Environment != 0 || e.Wait)
                throw Bad("a sound stop that carries more than its channel");
            return (int)SoundKind.Stop;
        }
        if (e.Kind == SoundKind.Play ? e.Path < 1 || e.Path > Limits.MaxSoundTableEntries : e.Path != 0)
            throw Bad("a sound event's path index " + e.Path + " for a " + e.Kind);
        if (e.Volume > Limits.MaxSoundVolume)
            throw Bad("a sound volume of " + e.Volume + " half units");
        if (e.Environment > Limits.MaxSoundEnvironment + 1)
            throw Bad("a sound environment byte of " + e.Environment);
        if (e.Positional)
            CheckSoundTile(e.X, e.Y, e.Z);
        else if (e.X != 0 || e.Y != 0 || e.Z != 0)
            throw Bad("a sound that is not positional with a source tile");
        if (e.Frequency != 0)
            CheckSoundFrequency(e.Frequency);
        if (e.Offset != 0)
            CheckSoundOffset(e.Offset);
        if (e.Wait && e.Kind != SoundKind.Play)
            throw Bad("a wait on a sound that is not a play");
        return (int)e.Kind | (e.Repeat ? SoundHeadRepeat : 0) | (e.Mute ? SoundHeadMute : 0) | (e.Positional ? SoundHeadPositional : 0)
            | (e.Frequency != 0 ? SoundHeadFrequency : 0) | (e.Offset != 0 ? SoundHeadOffset : 0) | (e.Wait ? SoundHeadWait : 0);
    }

    private static SoundEvent ReadSoundEvent(ref WireReader r)
    {
        int head = r.ReadByte();
        var kind = (SoundKind)(head & SoundHeadKindMask);
        if ((head & SoundHeadWait) != 0 && kind != SoundKind.Play)
            throw Bad("a wait on a sound event that is not a play");
        if (kind is not (SoundKind.Play or SoundKind.Update or SoundKind.Stop))
            throw Bad("a sound event of kind " + (int)kind);
        ushort channel = r.ReadUInt16();
        CheckSoundChannel(kind, channel);
        if (kind == SoundKind.Stop)
        {
            if ((head & ~SoundHeadKindMask) != 0)
                throw Bad("a sound stop with flags");
            return new SoundEvent { Kind = SoundKind.Stop, Channel = channel };
        }
        uint path = 0;
        if (kind == SoundKind.Play)
        {
            ulong index = r.ReadVarUInt();
            if (index < 1 || index > Limits.MaxSoundTableEntries)
                throw Bad("a sound play's path index " + index);
            path = (uint)index;
        }
        byte volume = r.ReadByte();
        if (volume > Limits.MaxSoundVolume)
            throw Bad("a sound volume of " + volume + " half units");
        byte muffle = r.ReadByte();
        byte occlusion = r.ReadByte();
        byte environment = r.ReadByte();
        if (environment > Limits.MaxSoundEnvironment + 1)
            throw Bad("a sound environment byte of " + environment);
        bool positional = (head & SoundHeadPositional) != 0;
        byte x = 0, y = 0;
        int z = 0;
        if (positional)
        {
            x = r.ReadByte();
            y = r.ReadByte();
            ulong level = r.ReadVarUInt();
            z = level > int.MaxValue ? int.MaxValue : (int)level;
            CheckSoundTile(x, y, z);
        }
        float frequency = 0, offset = 0;
        if ((head & SoundHeadFrequency) != 0)
        {
            frequency = r.ReadSingle();
            CheckSoundFrequency(frequency);
        }
        if ((head & SoundHeadOffset) != 0)
        {
            offset = r.ReadSingle();
            CheckSoundOffset(offset);
        }
        return new SoundEvent
        {
            Kind = kind,
            Channel = channel,
            Path = path,
            Volume = volume,
            Muffle = muffle,
            Occlusion = occlusion,
            Environment = environment,
            Repeat = (head & SoundHeadRepeat) != 0,
            Mute = (head & SoundHeadMute) != 0,
            Wait = (head & SoundHeadWait) != 0,
            Positional = positional,
            X = x,
            Y = y,
            Z = z,
            Frequency = frequency,
            Offset = offset,
        };
    }

    private static void CheckSoundChannel(SoundKind kind, int channel)
    {
        if (channel > Limits.MaxSoundChannel)
            throw Bad("a sound channel " + channel + " over " + Limits.MaxSoundChannel);
        if (kind == SoundKind.Update && channel == 0)
            throw Bad("a sound update on channel 0");
    }

    private static void CheckSoundTile(int x, int y, int z)
    {
        if (x < 1 || x > Limits.MaxMapSize || y < 1 || y > Limits.MaxMapSize || z < 1 || z > Limits.MaxMapLevels)
            throw Bad("a sound source tile outside the map's range");
    }

    private static void CheckSoundFrequency(float frequency)
    {
        if (!float.IsFinite(frequency) || frequency == 0 || MathF.Abs(frequency) > Limits.MaxSoundFrequency)
            throw Bad("a sound frequency of " + frequency);
    }

    private static void CheckSoundOffset(float offset)
    {
        if (!float.IsFinite(offset) || offset <= 0 || offset > Limits.MaxSoundOffset)
            throw Bad("a sound offset of " + offset);
    }
}
