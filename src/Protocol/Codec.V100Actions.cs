namespace Anomaly.Protocol;

// Version 100 also (MessagesV100Actions.cs, docs/ai/specs/protocol.md section 100; ui-overhaul.md section 15, DV698):
//
//   Actions (server to client, State): [u8 id = 36] [varuint count 0..64] then each button, in increasing button id:
//     [varuint button >= 1] [str(0..64) name, or none] [u8 flags: bit 0 active, bit 1 cooling down; bits 2-7 zero]
//     ([varuint cooldown end tick] [varuint cooldown ticks 1..72000] when cooling down)
//   then [varuint palette count 0..8] and each palette object [varuint id >= 1], in increasing id, none of them a button.
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes. Whether the
// screen objects exist is the client's concern: a button it has no screen entry for is not drawn.

public static partial class Codec
{
    private const byte ActionFlagActive = 1;
    private const byte ActionFlagCooldown = 2;

    /// <summary>Encodes an <see cref="ActionsMessage"/> for the State channel (at most <see cref="Limits.MaxActionsBytes"/>).</summary>
    public static byte[] Encode(ActionsMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return EncodeToArray(Limits.MaxActionsBytes, buffer =>
        {
            var w = new WireWriter(buffer);
            w.WriteMessageId(MessageId.Actions);
            IReadOnlyList<ActionButtonState> buttons = message.Buttons ?? throw Bad("an actions message without its buttons");
            IReadOnlyList<long> palette = message.Palette ?? throw Bad("an actions message without its palette");
            w.WriteCount(buttons.Count, Limits.MaxActionButtons);
            long last = 0;
            foreach (ActionButtonState b in buttons)
            {
                CheckActionButton(b.Button, b.CooldownEnd, b.CooldownTicks, ref last);
                w.WriteVarUInt((ulong)b.Button);
                w.WriteString(b.Name, Limits.MaxActionNameBytes);
                bool cooling = b.CooldownTicks > 0;
                w.WriteByte((byte)((b.Active ? ActionFlagActive : 0) | (cooling ? ActionFlagCooldown : 0)));
                if (cooling)
                {
                    w.WriteVarUInt((ulong)b.CooldownEnd);
                    w.WriteVarUInt((ulong)b.CooldownTicks);
                }
            }
            w.WriteCount(palette.Count, Limits.MaxActionPalette);
            long lastPalette = 0;
            foreach (long id in palette)
            {
                CheckPaletteId(id, buttons, ref lastPalette);
                w.WriteVarUInt((ulong)id);
            }
            return Finish(ref w, Limits.MaxActionsBytes);
        });
    }

    /// <summary>Decodes an <see cref="ActionsMessage"/>: at most 64 buttons in increasing id, names within the cap, known flags, a cooldown of 1 to 72000 ticks, and at most 8 palette objects in increasing id that are no button.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out ActionsMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Actions, Limits.MaxActionsBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            int count = r.ReadCount(Limits.MaxActionButtons, 3);
            var buttons = new ActionButtonState[count];
            long last = 0;
            for (int i = 0; i < count; i++)
            {
                long button = ReadNonNegativeLong(ref r, "an action button");
                string name = r.ReadString(Limits.MaxActionNameBytes);
                byte flags = r.ReadByte();
                if ((flags & ~(ActionFlagActive | ActionFlagCooldown)) != 0)
                    throw Bad("an action button with unknown flag bits");
                long end = 0;
                int ticks = 0;
                if ((flags & ActionFlagCooldown) != 0)
                {
                    end = ReadNonNegativeLong(ref r, "an action's cooldown end");
                    ulong length = r.ReadVarUInt();
                    if (length < 1 || length > Limits.MaxActionCooldownTicks)
                        throw Bad("an action's cooldown of " + length + " ticks");
                    ticks = (int)length;
                }
                CheckActionButton(button, end, ticks, ref last);
                buttons[i] = new ActionButtonState(button, name, (flags & ActionFlagActive) != 0, end, ticks);
            }
            int paletteCount = r.ReadCount(Limits.MaxActionPalette, 1);
            var palette = new long[paletteCount];
            long lastPalette = 0;
            for (int i = 0; i < paletteCount; i++)
            {
                palette[i] = ReadNonNegativeLong(ref r, "an action palette object");
                CheckPaletteId(palette[i], buttons, ref lastPalette);
            }
            r.ExpectEnd();
            value = new ActionsMessage { Buttons = buttons, Palette = palette };
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    // A button's id is positive and above the one before it; a cooldown has a length of 1 to the cap and an end that is not negative, or neither.
    private static void CheckActionButton(long button, long end, int ticks, ref long last)
    {
        if (button < 1 || button <= last)
            throw Bad("an action button " + button + " out of order");
        last = button;
        if (ticks < 0 || ticks > Limits.MaxActionCooldownTicks || end < 0 || (ticks == 0 && end != 0))
            throw Bad("an action's cooldown " + end + "/" + ticks);
    }

    private static void CheckPaletteId(long id, IReadOnlyList<ActionButtonState> buttons, ref long last)
    {
        if (id < 1 || id <= last)
            throw Bad("an action palette object " + id + " out of order");
        last = id;
        foreach (ActionButtonState b in buttons)
        {
            if (b.Button == id)
                throw Bad("an action palette object that is also a button");
        }
    }
}
