namespace Anomaly.Protocol;

// Version 21 (MessagesV21.cs, docs/ai/specs/protocol.md section 30):
//
//   CameraShake (server to client, State): [u8 id = 18] [u8 kind 0..1 (version 34: CameraShakeKind)] [varuint count(1..256)]
//     count x ( [varint pixel_x(-4096..4096)] [varint pixel_y(-4096..4096)] [varuint time(1..600)] )
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes a <see cref="CameraShakeMessage"/> for the State channel (at most <see cref="Limits.MaxCameraShakeBytes"/>).</summary>
    public static int Encode(CameraShakeMessage message, Span<byte> destination)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        if (message.Steps.Count < 1 || message.Steps.Count > Limits.MaxCameraShakeSteps)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a camera shake of " + message.Steps.Count + " steps");
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.CameraShake);
        if (message.Kind > CameraShakeKind.Recoil)
            throw Bad("a camera shake of an unknown kind");
        w.WriteByte((byte)message.Kind);
        w.WriteVarUInt((ulong)message.Steps.Count);
        foreach (CameraShakeStep step in message.Steps)
        {
            CheckStep(step);
            w.WriteVarInt(step.PixelX);
            w.WriteVarInt(step.PixelY);
            w.WriteVarUInt((ulong)step.Time);
        }
        return Finish(ref w, Limits.MaxCameraShakeBytes);
    }

    /// <summary>The encoded <see cref="CameraShakeMessage"/>.</summary>
    public static byte[] Encode(CameraShakeMessage message) =>
        EncodeToArray(Limits.MaxCameraShakeBytes, (Span<byte> buffer) => Encode(message, buffer));

    /// <summary>Decodes a <see cref="CameraShakeMessage"/>: 1 to 256 steps, each in range.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out CameraShakeMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.CameraShake, Limits.MaxCameraShakeBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            byte kind = r.ReadByte();
            if (kind > (byte)CameraShakeKind.Recoil)
                throw Bad("a camera shake of an unknown kind");
            int count = r.ReadCount(Limits.MaxCameraShakeSteps, 3);
            if (count < 1)
                throw new ProtocolException(ProtocolError.BadValue, "a camera shake without steps");
            var result = new CameraShakeMessage { Kind = (CameraShakeKind)kind };
            for (int i = 0; i < count; i++)
            {
                long x = r.ReadVarInt(), y = r.ReadVarInt();
                ulong time = r.ReadVarUInt();
                if (x < -Limits.MaxCameraShakePixels || x > Limits.MaxCameraShakePixels || y < -Limits.MaxCameraShakePixels || y > Limits.MaxCameraShakePixels || time < 1 || time > Limits.MaxCameraShakeStepTime)
                    throw Bad("a camera shake step out of range");
                result.Steps.Add(new CameraShakeStep((int)x, (int)y, (int)time));
            }
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

    private static void CheckStep(CameraShakeStep step)
    {
        if (Math.Abs(step.PixelX) > Limits.MaxCameraShakePixels || Math.Abs(step.PixelY) > Limits.MaxCameraShakePixels || step.Time < 1 || step.Time > Limits.MaxCameraShakeStepTime)
            throw Bad("a camera shake step out of range");
    }
}
