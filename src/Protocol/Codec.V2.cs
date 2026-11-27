using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Owner: M2 P5 (the bodies, the byte layout: docs/ai/specs/protocol.md); the signatures are the M2 contracts step's (frozen,
// docs/ai/specs/m2-ownership.md). Codec.cs holds the layouts of the existing messages (MapGrid, Snapshot, the Delta body, the
// entity and change records) with their version-2 fields; this file holds what is new in version 2: the client's
// MotionInput and Action messages, the private block, the appearance, resource and string tables, and the cell changes.
// Every rule of the version-1 codec holds: TryDecode never throws, every count and size is capped by Limits before anything
// is allocated, and a message that decodes encodes back to the same bytes (so every optional part is canonical: a field is
// present on the wire exactly when it differs from its default).

public static partial class Codec
{
    /// <summary>The bytes of a private block that holds nothing (no motion, no tables, no screen entries): flags, the input buffer depth, and (version 43) no actions done and no access.</summary>
    public const int EmptyOwnBlockBytes = 4;

    /// <summary>The largest per-connection Delta header of version 2 (tick, lastInputSeq, own entity) before the shared body. The version-1 <c>EncodeDelta</c> (no private block) appends an empty one, so it counts here too.</summary>
    public const int MaxDeltaHeaderBytesV2 = MaxDeltaHeaderBytes;

    // The least bytes of a list item, for WireReader.ReadCount.
    private const int MinResourceBytes = 1;
    private const int MinAppearanceBytes = 1;
    private const int MinCellChangeBytes = 4;
    private const int MinScreenEntryBytes = 5;

    // ---- client to server: MotionInput (Input channel) ----

    // [u8 id = 9] [u8 count 1..4] [varuint seq of the first frame] { [i8 moveX] [i8 moveY] [u16 yaw] [u8 flags] }*
    // The frames are consecutive: frame i has seq first + i.
    private const int MotionFrameBytes = 1 + 1 + 2 + 1;

    /// <summary>The largest encoded <see cref="MotionInputMessage"/>.</summary>
    public const int MaxMotionInputBytes = 1 + 1 + 5 + Limits.MaxInputFramesPerMessage * MotionFrameBytes;

    /// <summary>Encodes a <see cref="MotionInputMessage"/> (1 to <see cref="Limits.MaxInputFramesPerMessage"/> frames of consecutive seq) for the Input channel; returns the length.</summary>
    public static int Encode(MotionInputMessage message, Span<byte> destination)
    {
        List<MotionInput> frames = message.Frames;
        if (frames.Count < 1 || frames.Count > Limits.MaxInputFramesPerMessage)
            throw Bad("a MotionInput message of " + frames.Count + " frames (1 to " + Limits.MaxInputFramesPerMessage + ")");
        if (frames[0].Seq == 0)
            throw Bad("a MotionInput frame with seq 0 (the first is 1)");
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.MotionInput);
        w.WriteByte((byte)frames.Count);
        w.WriteVarUInt(frames[0].Seq);
        for (int i = 0; i < frames.Count; i++)
        {
            MotionInput f = frames[i];
            if (f.Seq != unchecked(frames[0].Seq + (uint)i) || (i > 0 && f.Seq <= frames[i - 1].Seq))
                throw Bad("the frames of a MotionInput message must have consecutive seqs");
            if (f.MoveX < -MotionInput.MaxMove || f.MoveY < -MotionInput.MaxMove)
                throw Bad("a MotionInput move of -128 (the range is -127 to 127)");
            if (f.Flags != MotionInputFlags.None)
                throw Bad("a MotionInput frame sets flag bits that are not defined");
            w.WriteByte((byte)f.MoveX);
            w.WriteByte((byte)f.MoveY);
            w.WriteUInt16(f.Yaw);
            w.WriteByte((byte)f.Flags);
        }

        return Finish(ref w, Limits.MaxMotionInputMessageBytes);
    }

    /// <summary>The encoded <see cref="MotionInputMessage"/>.</summary>
    public static byte[] Encode(MotionInputMessage message)
    {
        Span<byte> buffer = stackalloc byte[MaxMotionInputBytes];
        return buffer.Slice(0, Encode(message, buffer)).ToArray();
    }

    /// <summary>Decodes a <see cref="MotionInputMessage"/>: at most <see cref="Limits.MaxMotionInputMessageBytes"/> bytes, 1 to 4 frames, consecutive seqs, moves within -127..127, no unknown flag bits.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out MotionInputMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.MotionInput, Limits.MaxMotionInputMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            int count = r.ReadByte();
            if (count < 1 || count > Limits.MaxInputFramesPerMessage)
                throw Bad("a MotionInput message of " + count + " frames");
            ulong first = r.ReadVarUInt();
            if (first == 0 || first + (ulong)count - 1 > uint.MaxValue)
                throw Bad("a MotionInput first seq of 0 or one that overflows 32 bits");
            var result = new MotionInputMessage();
            for (int i = 0; i < count; i++)
            {
                sbyte moveX = (sbyte)r.ReadByte();
                sbyte moveY = (sbyte)r.ReadByte();
                if (moveX < -MotionInput.MaxMove || moveY < -MotionInput.MaxMove)
                    throw Bad("a MotionInput move of -128");
                ushort yaw = r.ReadUInt16();
                byte flags = r.ReadByte();
                if (flags != 0)
                    throw Bad("a MotionInput frame sets flag bits that are not defined");
                result.Frames.Add(new MotionInput((uint)(first + (ulong)i), moveX, moveY, yaw, (MotionInputFlags)flags));
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

    // ---- client to server: Action (Control channel) ----

    // [u8 id = 10] [varuint frameSeq] [u8 kind] then by kind:
    //   Click (4):      [u8 target kind 1..3] [target] [u8 button] [u8 modifiers] [u8 icon-x 0..32] [u8 icon-y 0..32]
    //   Hover (7):      [u8 target kind 1..3] [target] [version 99: u8 aim zone 1..9 on an entity, absent for none (Codec.V99.cs)]
    //                   target: Entity and Screen [varuint id >= 1]; Turf [u8 x 1..255] [u8 y 1..255] [varuint z 1..16]
    //   DevGasProbe (8, version 5): [target: a turf]
    //   Unhover (9, version 14): nothing
    //   RadialPick (10, version 15): [varuint menu id >= 1] [varuint element 0..16]
    //   UiAct (11, version 16): [varuint window 1..200] [str(1..48) action] [value params: an object or null] (Codec.V16.cs)
    //   UiClose (12, version 16): [varuint window 1..200]
    //   Drop (13, version 17): [dragged: target] [over: target] [u8 button 1..3] [u8 modifiers: shift 1, ctrl 2, alt 4]
    //                          [u8 icon-x 0..32] [u8 icon-y 0..32] [u8 drag time in deciseconds]
    //   MouseDown (15), MouseDrag (16), MouseUp (17) (version 34), MouseWheel (21, version 70: the button 1 up, 2 down):
    //                   [u8 target kind 0..3] [target unless 0] [u8 button 1..3]
    //                   [u8 modifiers: shift 1, ctrl 2, alt 4, bit 7 aim] [u8 icon-x 0..32] [u8 icon-y 0..32] [aim if bit 7]
    //   a Click's modifiers byte bits 4-6 are its aim zone (DV30); version 99: bit 3 is the zone's high bit (Codec.V99.cs)
    //   version 34: a Click's modifiers byte bit 7 says an aim follows its icon-y (Codec.V34.cs:
    //                   [varint east] [varint north] [u16 up mm] [u16 yaw] [i16 pitch -16384..16384])
    //   Keybinding (6): [u8 code 1..13] [u8 key up 0|1]
    //   Emote (19, version 39): [str(32) emote key: a-z, 0-9, _]
    //   DevCommand (5): [str(200) text]
    //   HotbarPress (28, version 99): [u8 cell 1..10]; InventoryMove (29, version 99): Codec.V99Storages.cs
    //   others:         nothing
    // (docs/ai/specs/protocol.md section 14.1). A version-3 click (target kind None, a target above 0) is written as an
    // entity click: ActionMessage.EffectiveTargetKind.

    /// <summary>Encodes an <see cref="ActionMessage"/> for the Control channel.</summary>
    public static int Encode(in ActionMessage message, Span<byte> destination)
    {
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Action);
        w.WriteVarUInt(message.FrameSeq);
        if (message.Kind < ActionKind.Rest || message.Kind > ActionKinds.Last)
            throw Bad("an unknown action kind " + (byte)message.Kind);
        w.WriteByte((byte)message.Kind);
        bool targeted = message.IsTargeted;
        TargetKind targetKind = message.EffectiveTargetKind;
        if (!targeted && (message.Target != 0 || message.TargetKind != TargetKind.None || message.TurfX != 0 || message.TurfY != 0 || message.TurfZ != 0))
            throw Bad("a target on an action that is not a click or a hover");
        if (message.Kind is not (ActionKind.Click or ActionKind.Throw or ActionKind.PutDown or ActionKind.Drop or ActionKind.DblClick) && !message.IsMouse && (message.Button != ClickButton.None || message.Modifiers != ClickModifiers.None || message.IconX != 0 || message.IconY != 0))
            throw Bad("a button, modifiers or an icon pixel on an action that is not a click, a drop or a mouse action");
        if (message.Kind is not (ActionKind.Click or ActionKind.Throw or ActionKind.PutDown) && !message.IsMouse && message.Aim.HasValue)
            throw Bad("an aim on an action that is not a click or a mouse action");
        if (message.Kind is not (ActionKind.Click or ActionKind.Throw or ActionKind.PutDown or ActionKind.Hover) && message.AimZone != AimZone.None)
            throw Bad("an aim zone on an action that is not a click or a hover");
        if (message.Kind != ActionKind.Drop && (message.OverKind != TargetKind.None || message.OverTarget != 0 || message.OverX != 0 || message.OverY != 0 || message.OverZ != 0 || message.DragTime != 0))
            throw Bad("a drop's fields on an action that is not a drop");
        if (message.Kind != ActionKind.Keybinding && (message.Keybinding != KeybindingCode.None || message.KeyUp))
            throw Bad("a keybinding or a key release on an action that is not a keybinding");
        if (message.Kind is not (ActionKind.DevCommand or ActionKind.Topic or ActionKind.Say or ActionKind.Emote or ActionKind.Verb or ActionKind.PopupClose or ActionKind.ContextPick) && message.Text != null)
            throw Bad("a text on an action that is not a dev command, a chat link, a say, an emote, a verb or a popup close");
        if (message.Kind != ActionKind.Say && (message.SayType != null || message.SayChannel != null || message.SayVisible))
            throw Bad("a say's fields on an action that is not a say");
        if (message.Kind != ActionKind.RadialPick && (message.RadialMenu != 0 || message.RadialElement != 0))
            throw Bad("a radial menu or element on an action that is not a radial pick");
        if (message.Kind != ActionKind.UiAct && message.Kind != ActionKind.UiClose && (message.UiWindow != 0 || message.UiAction != null || message.UiParams != null))
            throw Bad("a UI window, action or params on an action that is not a UI act or close");
        if (message.Kind != ActionKind.HotbarPress && message.HotbarCell != 0)
            throw Bad("a hotbar cell on an action that is not a hotbar press");
        if (message.Kind != ActionKind.InventoryMove && (message.MoveItem != 0 || message.MoveTo != InventoryTarget.None || message.MoveStorage != 0 || message.MoveIndex != 0))
            throw Bad("an inventory move's fields on an action that is not an inventory move");
        switch (message.Kind)
        {
            case ActionKind.UiAct:
            case ActionKind.UiClose:
                WriteUiAction(ref w, message);
                break;
            case ActionKind.InventoryMove:
                WriteInventoryMove(ref w, message); // version 99 also (DV692, Codec.V99Storages.cs)
                break;
            case ActionKind.HotbarPress:
                // version 99 also (DV689): the cell, 1 to 10
                if (message.HotbarCell < 1 || message.HotbarCell > HotbarMessage.CellCount)
                    throw Bad("a hotbar press of cell " + message.HotbarCell);
                w.WriteByte((byte)message.HotbarCell);
                break;
            case ActionKind.RadialPick:
                if (message.RadialMenu < 1)
                    throw Bad("a radial pick without a menu");
                if (message.RadialElement < 0 || message.RadialElement > Limits.MaxRadialElements)
                    throw Bad("a radial pick of element " + message.RadialElement);
                w.WriteVarUInt((ulong)message.RadialMenu);
                w.WriteVarUInt((ulong)message.RadialElement);
                break;
            case ActionKind.DevGasProbe:
                if (targetKind != TargetKind.Turf)
                    throw Bad("a dev gas probe whose target is not a turf");
                WriteTarget(ref w, targetKind, message.Target, message.TurfX, message.TurfY, message.TurfZ, "a dev gas probe");
                break;
            case ActionKind.Drop:
                // version 17: the dragged thing and what it was released over, BYOND's src_object and over_object
                WriteTarget(ref w, targetKind, message.Target, message.TurfX, message.TurfY, message.TurfZ, "a drop's dragged thing");
                WriteTarget(ref w, message.OverKind, message.OverTarget, message.OverX, message.OverY, message.OverZ, "a drop's target");
                if (message.Button < ClickButton.Left || message.Button > ClickButton.Middle)
                    throw Bad("a drop with an unknown button");
                if ((message.Modifiers & ~ClickModifiers.All) != 0)
                    throw Bad("a drop with unknown modifier bits");
                if (message.IconX > Limits.MaxIconPixel || message.IconY > Limits.MaxIconPixel)
                    throw Bad("a drop with an icon pixel over " + Limits.MaxIconPixel);
                w.WriteByte((byte)message.Button);
                w.WriteByte((byte)message.Modifiers);
                w.WriteByte(message.IconX);
                w.WriteByte(message.IconY);
                w.WriteByte(message.DragTime);
                break;
            case ActionKind.Click:
            case ActionKind.Throw: // version 81: a click's fields
            case ActionKind.PutDown: // version 82: a click's fields
            case ActionKind.Hover:
                WriteTarget(ref w, targetKind, message.Target, message.TurfX, message.TurfY, message.TurfZ, "a click or hover");
                if (message.Kind == ActionKind.Hover)
                {
                    WriteHoverZone(ref w, message.AimZone, targetKind); // version 99 (slice T4b)
                    break;
                }
                if (message.Button < ClickButton.Left || message.Button > ClickButton.Middle)
                    throw Bad("a click with an unknown button");
                if (message.Kind is ActionKind.Throw or ActionKind.PutDown && (message.Button != ClickButton.Left || message.Modifiers != ClickModifiers.None))
                    throw Bad("a throw or a put-down that is not a plain left click");
                if ((message.Modifiers & ~ClickModifiers.All) != 0)
                    throw Bad("a click with unknown modifier bits");
                if (message.IconX > Limits.MaxIconPixel || message.IconY > Limits.MaxIconPixel)
                    throw Bad("a click with an icon pixel over " + Limits.MaxIconPixel);
                if (message.AimZone > AimZone.Last)
                    throw Bad("a click with an unknown aim zone");
                w.WriteByte((byte)message.Button);
                w.WriteByte((byte)((byte)message.Modifiers | ZoneBits(message.AimZone) | (message.Aim.HasValue ? ModifierAimBit : 0))); // version 99: bit 3 the zone's high bit
                w.WriteByte(message.IconX);
                w.WriteByte(message.IconY);
                if (message.Aim.HasValue)
                    WriteAim(ref w, message.Aim.Value);
                break;
            case ActionKind.MouseDown:
            case ActionKind.MouseDrag:
            case ActionKind.MouseUp:
            case ActionKind.MouseWheel: // version 70
                WriteMouseAction(ref w, message, targetKind);
                break;
            case ActionKind.DblClick:
                // version 51: a mouse action's fields on a target
                if (targetKind == TargetKind.None)
                    throw Bad("a double click on nothing");
                if (message.Aim.HasValue)
                    throw Bad("an aim on a double click");
                WriteMouseAction(ref w, message, targetKind);
                break;
            case ActionKind.Keybinding:
                if (message.Keybinding < KeybindingCode.SwapHands || message.Keybinding > KeybindingCode.Last)
                    throw Bad("a keybinding action with an unknown code " + (byte)message.Keybinding);
                w.WriteByte((byte)message.Keybinding);
                w.WriteByte(message.KeyUp ? (byte)1 : (byte)0);
                break;
            case ActionKind.DevCommand:
                if (message.Text == null)
                    throw Bad("a dev command without text");
                w.WriteString(message.Text, Limits.MaxDevCommandBytes);
                break;
            case ActionKind.Topic:
                // version 24: a chat link's href (D154)
                if (string.IsNullOrEmpty(message.Text))
                    throw Bad("a chat link without an href");
                w.WriteString(message.Text, Limits.MaxTopicHrefBytes);
                break;
            case ActionKind.Say:
                WriteSay(ref w, message); // version 38
                break;
            case ActionKind.Emote:
                // version 39: the emote key
                if (!EmoteKeys.IsValid(message.Text))
                    throw Bad("an emote action without a valid emote key");
                w.WriteString(message.Text, Limits.MaxEmoteKeyBytes);
                break;
            case ActionKind.Verb:
                // version 78: the verb's path
                if (string.IsNullOrEmpty(message.Text))
                    throw Bad("a verb action without a path");
                w.WriteString(message.Text, Limits.MaxVerbPathBytes);
                break;
            case ActionKind.PopupClose:
                // version 78 (DV387): the popup window's name
                if (string.IsNullOrEmpty(message.Text))
                    throw Bad("a popup close without a window name");
                w.WriteString(message.Text, Limits.MaxPopupNameBytes);
                break;
            case ActionKind.ContextPick:
                // version 86 (DV503): the target and the entry's label
                if (targetKind == TargetKind.None)
                    throw Bad("a context pick on nothing");
                if (string.IsNullOrEmpty(message.Text))
                    throw Bad("a context pick without a label");
                WriteTarget(ref w, targetKind, message.Target, message.TurfX, message.TurfY, message.TurfZ, "a context pick");
                w.WriteString(message.Text, Limits.MaxContextLabelBytes);
                break;
        }

        return Finish(ref w, message.Kind == ActionKind.Say ? Limits.MaxSayActionBytes : Limits.MaxActionMessageBytes);
    }

    /// <summary>The encoded <see cref="ActionMessage"/>.</summary>
    public static byte[] Encode(in ActionMessage message)
    {
        Span<byte> buffer = stackalloc byte[Limits.MaxSayActionBytes + 8];
        return buffer.Slice(0, Encode(message, buffer)).ToArray();
    }

    /// <summary>
    /// Decodes an <see cref="ActionMessage"/> (version 4): a known kind, a click or hover target of a known kind inside the
    /// caps (an entity or screen id above 0, a turf inside the map), a known button and modifier bits and icon pixels within
    /// 32 for a click, a keybinding code in range for a keybinding, text only for a dev command and within
    /// <see cref="Limits.MaxDevCommandBytes"/>. Whether the target exists is the host's check (D40).
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out ActionMessage value, out ProtocolError error)
    {
        value = default;
        if (!Begin(message, MessageId.Action, Limits.MaxSayActionBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            uint frameSeq = ReadUInt32Varint(ref r, "an action's frame seq");
            byte kindByte = r.ReadByte();
            if (kindByte < (byte)ActionKind.Rest || kindByte > (byte)ActionKinds.Last)
                throw Bad("an unknown action kind " + kindByte);
            var kind = (ActionKind)kindByte;
            var targetKind = TargetKind.None;
            long target = 0;
            int turfX = 0, turfY = 0, turfZ = 0;
            var button = ClickButton.None;
            var modifiers = ClickModifiers.None;
            byte iconX = 0, iconY = 0;
            var aimZone = AimZone.None;
            var keybinding = KeybindingCode.None;
            bool keyUp = false;
            string text = null;
            long radialMenu = 0;
            int radialElement = 0;
            var overKind = TargetKind.None;
            long overTarget = 0;
            int overX = 0, overY = 0, overZ = 0;
            byte dragTime = 0;
            ShotAim? aim = null;
            string sayType = null, sayChannel = null;
            bool sayVisible = false;
            int uiWindow = 0;
            string uiAction = null;
            UiValue uiParams = null;
            int hotbarCell = 0;
            long moveItem = 0, moveStorage = 0;
            InventoryTarget moveTo = InventoryTarget.None;
            int moveIndex = 0;
            switch (kind)
            {
                case ActionKind.UiAct:
                case ActionKind.UiClose:
                    ReadUiAction(ref r, kind, out uiWindow, out uiAction, out uiParams);
                    break;
                case ActionKind.InventoryMove:
                    ReadInventoryMove(ref r, out moveItem, out moveTo, out moveStorage, out moveIndex); // version 99 also (DV692)
                    break;
                case ActionKind.HotbarPress:
                    hotbarCell = r.ReadByte(); // version 99 also (DV689)
                    if (hotbarCell < 1 || hotbarCell > HotbarMessage.CellCount)
                        throw Bad("a hotbar press of cell " + hotbarCell);
                    break;
                case ActionKind.Drop:
                    ReadTarget(ref r, out targetKind, out target, out turfX, out turfY, out turfZ, "a drop's dragged thing");
                    ReadTarget(ref r, out overKind, out overTarget, out overX, out overY, out overZ, "a drop's target");
                    byte dropButton = r.ReadByte();
                    if (dropButton < (byte)ClickButton.Left || dropButton > (byte)ClickButton.Middle)
                        throw Bad("a drop with an unknown button");
                    button = (ClickButton)dropButton;
                    byte dropMods = r.ReadByte();
                    if ((dropMods & ~(byte)ClickModifiers.All) != 0)
                        throw Bad("a drop with unknown modifier bits");
                    modifiers = (ClickModifiers)dropMods;
                    iconX = r.ReadByte();
                    iconY = r.ReadByte();
                    if (iconX > Limits.MaxIconPixel || iconY > Limits.MaxIconPixel)
                        throw Bad("a drop with an icon pixel over " + Limits.MaxIconPixel);
                    dragTime = r.ReadByte();
                    break;
                case ActionKind.RadialPick:
                    radialMenu = ReadNonNegativeLong(ref r, "a radial pick's menu");
                    if (radialMenu < 1)
                        throw Bad("a radial pick without a menu");
                    ulong element = r.ReadVarUInt();
                    if (element > Limits.MaxRadialElements)
                        throw Bad("a radial pick of element " + element);
                    radialElement = (int)element;
                    break;
                case ActionKind.DevGasProbe:
                    ReadTarget(ref r, out targetKind, out target, out turfX, out turfY, out turfZ, "a dev gas probe");
                    if (targetKind != TargetKind.Turf)
                        throw Bad("a dev gas probe whose target is not a turf");
                    break;
                case ActionKind.Click:
                case ActionKind.Throw: // version 81: a click's fields
                case ActionKind.PutDown: // version 82: a click's fields
                case ActionKind.Hover:
                    ReadTarget(ref r, out targetKind, out target, out turfX, out turfY, out turfZ, "a click or hover");
                    if (kind == ActionKind.Hover)
                    {
                        aimZone = ReadHoverZone(ref r, targetKind); // version 99 (slice T4b)
                        break;
                    }
                    byte buttonByte = r.ReadByte();
                    if (buttonByte < (byte)ClickButton.Left || buttonByte > (byte)ClickButton.Middle)
                        throw Bad("a click with an unknown button");
                    button = (ClickButton)buttonByte;
                    byte mods = r.ReadByte();
                    // version 99: bit 3 is the zone's high bit (Codec.V99.cs), no longer an unknown modifier
                    int zone = ZoneOfModifiers(mods);
                    if (zone > (byte)AimZone.Last)
                        throw Bad("a click with an unknown aim zone");
                    modifiers = (ClickModifiers)(mods & (byte)ClickModifiers.All);
                    aimZone = (AimZone)zone;
                    iconX = r.ReadByte();
                    iconY = r.ReadByte();
                    if (iconX > Limits.MaxIconPixel || iconY > Limits.MaxIconPixel)
                        throw Bad("a click with an icon pixel over " + Limits.MaxIconPixel);
                    if ((mods & ModifierAimBit) != 0)
                        aim = ReadAim(ref r);
                    if (kind is ActionKind.Throw or ActionKind.PutDown && (button != ClickButton.Left || modifiers != ClickModifiers.None))
                        throw Bad("a throw or a put-down that is not a plain left click");
                    break;
                case ActionKind.MouseDown:
                case ActionKind.MouseDrag:
                case ActionKind.MouseUp:
                case ActionKind.MouseWheel: // version 70
                    ReadMouseAction(ref r, out targetKind, out target, out turfX, out turfY, out turfZ, out button, out modifiers, out iconX, out iconY, out aim);
                    break;
                case ActionKind.DblClick:
                    ReadMouseAction(ref r, out targetKind, out target, out turfX, out turfY, out turfZ, out button, out modifiers, out iconX, out iconY, out aim);
                    if (targetKind == TargetKind.None)
                        throw Bad("a double click on nothing");
                    if (aim.HasValue)
                        throw Bad("an aim on a double click");
                    break;
                case ActionKind.Keybinding:
                    byte code = r.ReadByte();
                    if (code < (byte)KeybindingCode.SwapHands || code > (byte)KeybindingCode.Last)
                        throw Bad("a keybinding action with an unknown code " + code);
                    keybinding = (KeybindingCode)code;
                    byte up = r.ReadByte();
                    if (up > 1)
                        throw Bad("a keybinding action whose key-up flag is not 0 or 1");
                    keyUp = up == 1;
                    break;
                case ActionKind.DevCommand:
                    text = r.ReadString(Limits.MaxDevCommandBytes);
                    if (text == null)
                        throw Bad("a dev command without text");
                    break;
                case ActionKind.Topic:
                    text = r.ReadString(Limits.MaxTopicHrefBytes);
                    if (string.IsNullOrEmpty(text))
                        throw Bad("a chat link without an href");
                    break;
                case ActionKind.Say:
                    ReadSay(ref r, out sayType, out sayChannel, out text, out sayVisible); // version 38
                    break;
                case ActionKind.Emote:
                    text = r.ReadString(Limits.MaxEmoteKeyBytes); // version 39
                    if (!EmoteKeys.IsValid(text))
                        throw Bad("an emote action without a valid emote key");
                    break;
                case ActionKind.Verb:
                    text = r.ReadString(Limits.MaxVerbPathBytes); // version 78
                    if (string.IsNullOrEmpty(text))
                        throw Bad("a verb action without a path");
                    break;
                case ActionKind.PopupClose:
                    text = r.ReadString(Limits.MaxPopupNameBytes); // version 78
                    if (string.IsNullOrEmpty(text))
                        throw Bad("a popup close without a window name");
                    break;
                case ActionKind.ContextPick:
                    ReadTarget(ref r, out targetKind, out target, out turfX, out turfY, out turfZ, "a context pick"); // version 86
                    text = r.ReadString(Limits.MaxContextLabelBytes);
                    if (string.IsNullOrEmpty(text))
                        throw Bad("a context pick without a label");
                    break;
            }

            r.ExpectEnd();
            if (kind != ActionKind.Say && message.Length > Limits.MaxActionMessageBytes)
                throw new ProtocolException(ProtocolError.TooLong, "an action over " + Limits.MaxActionMessageBytes + " bytes");
            value = new ActionMessage(frameSeq, kind, target, button, modifiers, text)
            {
                TargetKind = targetKind,
                TurfX = turfX,
                TurfY = turfY,
                TurfZ = turfZ,
                IconX = iconX,
                IconY = iconY,
                AimZone = aimZone,
                Keybinding = keybinding,
                KeyUp = keyUp,
                RadialMenu = radialMenu,
                RadialElement = radialElement,
                OverKind = overKind,
                OverTarget = overTarget,
                OverX = overX,
                OverY = overY,
                OverZ = overZ,
                DragTime = dragTime,
                Aim = aim,
                UiWindow = uiWindow,
                UiAction = uiAction,
                UiParams = uiParams,
                SayType = sayType,
                SayChannel = sayChannel,
                SayVisible = sayVisible,
                HotbarCell = hotbarCell,
                MoveItem = moveItem,
                MoveTo = moveTo,
                MoveStorage = moveStorage,
                MoveIndex = moveIndex,
            };
            return true;
        }
        catch (ProtocolException e)
        {
            value = default;
            error = e.Error;
            return false;
        }
    }

    // ---- server to client: the private block ----

    // [u8 flags: bit 0 motion, bit 1 private tables, bit 2 screen entries, bit 3 private images (version 4); the rest zero]
    // if motion:   [u16 x] [u16 y] [varuint z] [u16 yaw]
    //              [u8 packed: bit 0 lying, bits 1-2 driver, bit 3 can move, bit 4 slide, the rest zero]
    //              [varint pressTimer] ([u16 slideX] [u16 slideY] [varuint slideTicks] if the slide bit)
    //              [f32 tilesPerTick] [f32 radius] [varuint moveDelayTicks]
    // [u8 inputBufferDepth]
    // if tables:   [varuint firstString] [count] { str(256) }*   [varuint firstResource] [count] { str(512) }*
    //              [varuint firstAppearance] [count] { appearance }*
    // if screen:   [count added] { [varint id diff] [tableref appearance] [str(128) screenLoc] [u8 group 0..6] }*   (then the same for changed)
    //              [count removed] { [varint id diff] }*      (the ids of each list strictly increase)
    // if images:   [count added] { [varint id diff] [tableref appearance] [varuint attachedTo] }*   (then the same for changed)
    //              [count removed] { [varint id diff] }*      (version 4, at most 64 each; Codec.V4.cs)

    private const int OwnFlagMotion = 1;
    private const int OwnFlagTables = 2;
    private const int OwnFlagScreen = 4;
    private const int OwnFlagImages = 8; // version 4: the private images
    private const int OwnFlagsAll = 255; // version 22: OwnFlagLayer (16), Codec.V22.cs; version 23: OwnFlagBody (32), Codec.V23.cs; version 29: OwnFlagGamePlane (64), Codec.V29.cs; version 31: OwnFlagVision (128), Codec.V31.cs

    private const int MaxMotionBytes = 2 + 2 + 5 + 2 + 1 + 2 + 5 + 5 + 4 + 4 + 5;
    private const int MaxAppearanceFixedBytes = 5 + 5 + 5 + 1 + 4 + 80 + 1 + 4 + 5 + 10 + 10 + 24 + 5 + 1 + 5 + 5 + 5 + 5 + 5 + 5 + 5;
    private const int MaxScreenEntryFixedBytes = 10 + 5 + 5 + 1;

    /// <summary>The most bytes <see cref="EncodeDelta(long, uint, long, ReadOnlySpan{byte}, OwnBlock, Span{byte})"/> writes for <paramref name="own"/> after the body.</summary>
    public static long MaxOwnBlockSize(OwnBlock own)
    {
        long size = 1 + MaxMotionBytes + 1 + 6;
        if (own == null)
            return size;
        size += MaxOwnV43Bytes(own);
        size += 3 * (5 + 5);
        foreach (string s in own.NewPrivateStrings)
            size += 5 + MaxUtf8(s, Limits.MaxTableStringBytes);
        foreach (string s in own.NewPrivateResources)
            size += 5 + MaxUtf8(s, Limits.MaxResourceBytes);
        foreach (AppearanceEntry a in own.NewPrivateAppearances)
            size += MaxAppearanceSize(a);
        size += 3 * 5;
        foreach (ScreenEntry e in own.ScreenAdded)
            size += MaxScreenEntryFixedBytes + MaxUtf8(e.ScreenLoc, Limits.MaxScreenLocBytes) + MaxScreenLookBytes(e);
        foreach (ScreenEntry e in own.ScreenChanged)
            size += MaxScreenEntryFixedBytes + MaxUtf8(e.ScreenLoc, Limits.MaxScreenLocBytes) + MaxScreenLookBytes(e);
        size += own.ScreenRemoved.Count * 10L;
        size += MaxImagesSize(own);
        size += MaxLayerSize(own);
        size += MaxBodyBytes;
        size += MaxGamePlaneBytes;
        size += MaxVisionBytes;
        return size;
    }

    // Version 26: a filter is at most 1 + 3 (kind, fields) + 8 + 4 + 4 + 80 + 1 + 3 x 5 + 4 x 9 (offset to falloff) + 1 + 24 + 1 bytes;
    // the count and the render target 5 each.
    private const long MaxFilterBytes = 178;

    private static long MaxAppearanceSize(AppearanceEntry a)
        => MaxAppearanceFixedBytes + 5L * (a.OverlayCount + a.UnderlayCount) + 10 + MaxFilterBytes * (a.Filters?.Count ?? 0);

    /// <summary>
    /// Encodes a private block on its own into <paramref name="destination"/> (the replicator builds it once per session and
    /// the host appends it with <see cref="EncodeDelta(long, uint, long, ReadOnlySpan{byte}, ReadOnlySpan{byte}, Span{byte})"/>);
    /// returns the length.
    /// </summary>
    public static int EncodeOwnBlock(OwnBlock own, Span<byte> destination)
    {
        var w = new WireWriter(destination);
        WriteOwnBlock(ref w, own, own?.LayerTick ?? 0);
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    /// <summary>
    /// Version 2: one connection's Delta: its header, the tick's shared <paramref name="body"/> (<see cref="EncodeDeltaBody(Delta, ref byte[])"/>)
    /// and its private block <paramref name="own"/> after the body (D40: only this connection's bytes hold it). Needs
    /// <see cref="MaxDeltaHeaderBytesV2"/> plus the body's length plus <see cref="MaxOwnBlockSize"/>.
    /// </summary>
    public static int EncodeDelta(long tick, uint lastInputSeq, long ownEntity, ReadOnlySpan<byte> body, OwnBlock own, Span<byte> destination)
    {
        var w = new WireWriter(destination);
        WriteDeltaHeader(ref w, tick, lastInputSeq, ownEntity);
        w.WriteBytes(body);
        WriteOwnBlock(ref w, own, tick);
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    /// <summary>
    /// Version 2: one connection's Delta from the encoded parts: the header, the shared <paramref name="body"/> and the
    /// connection's encoded private block <paramref name="ownBlock"/> (<see cref="EncodeOwnBlock"/>).
    /// </summary>
    public static int EncodeDelta(long tick, uint lastInputSeq, long ownEntity, ReadOnlySpan<byte> body, ReadOnlySpan<byte> ownBlock, Span<byte> destination)
    {
        var w = new WireWriter(destination);
        WriteDeltaHeader(ref w, tick, lastInputSeq, ownEntity);
        w.WriteBytes(body);
        w.WriteBytes(ownBlock);
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    private static void WriteOwnBlock(ref WireWriter w, OwnBlock own, long tick)
    {
        if (own == null)
            own = EmptyOwn;
        bool tables = own.NewPrivateStrings.Count > 0 || own.NewPrivateResources.Count > 0 || own.NewPrivateAppearances.Count > 0;
        bool screen = own.ScreenAdded.Count > 0 || own.ScreenChanged.Count > 0 || own.ScreenRemoved.Count > 0;
        bool images = own.ImagesAdded.Count > 0 || own.ImagesChanged.Count > 0 || own.ImagesRemoved.Count > 0;
        bool layer = HasLayer(own);
        w.WriteByte((byte)((own.HasMotion ? OwnFlagMotion : 0) | (tables ? OwnFlagTables : 0) | (screen ? OwnFlagScreen : 0) | (images ? OwnFlagImages : 0) | (layer ? OwnFlagLayer : 0)
            | (own.BodyChanged ? OwnFlagBody : 0) | (own.GamePlaneChanged ? OwnFlagGamePlane : 0) | (own.VisionChanged ? OwnFlagVision : 0)));
        if (own.HasMotion)
            WriteMotion(ref w, own.Motion, own.Params);
        w.WriteByte(own.InputBufferDepth);
        WriteOwnV43(ref w, own);
        if (tables)
        {
            WriteStringTable(ref w, own.FirstNewPrivateString, own.NewPrivateStrings, Limits.MaxStringTableEntries, Limits.MaxTableStringBytes);
            WriteStringTable(ref w, own.FirstNewPrivateResource, own.NewPrivateResources, Limits.MaxResourceTableEntries, Limits.MaxResourceBytes);
            WriteAppearanceTable(ref w, own.FirstNewPrivateAppearance, own.NewPrivateAppearances, true);
            if ((long)own.NewPrivateStrings.Count + own.NewPrivateResources.Count + own.NewPrivateAppearances.Count > Limits.MaxNewTableEntriesPerMessage)
                throw new ProtocolException(ProtocolError.CountTooLarge, "a private block that adds more than " + Limits.MaxNewTableEntriesPerMessage + " table entries");
        }

        if (screen)
        {
            WriteScreenEntries(ref w, own.ScreenAdded);
            WriteScreenEntries(ref w, own.ScreenChanged);
            w.WriteCount(own.ScreenRemoved.Count, Limits.MaxScreenEntries);
            long previous = 0;
            foreach (long id in own.ScreenRemoved)
            {
                WriteId(ref w, id, previous, true);
                previous = id;
            }
        }

        if (images)
            WriteImages(ref w, own);
        if (layer)
            WriteLayer(ref w, own, tick);
        if (own.BodyChanged)
            WriteBody(ref w, own);
        if (own.GamePlaneChanged)
            WriteGamePlane(ref w, own);
        if (own.VisionChanged)
            WriteVision(ref w, own);
    }

    private static readonly OwnBlock EmptyOwn = new();

    private static void ReadOwnBlock(ref WireReader r, OwnBlock own, long tick)
    {
        own.LayerTick = tick;
        byte flags = r.ReadByte();
        if ((flags & ~OwnFlagsAll) != 0)
            throw Bad("a private block sets flag bits that are not defined");
        if ((flags & OwnFlagMotion) != 0)
        {
            own.HasMotion = true;
            ReadMotion(ref r, out MotionState state, out MotionParams parameters);
            own.Motion = state;
            own.Params = parameters;
        }

        own.InputBufferDepth = r.ReadByte();
        ReadOwnV43(ref r, own);
        if ((flags & OwnFlagTables) != 0)
        {
            own.FirstNewPrivateString = ReadStringTable(ref r, own.NewPrivateStrings, Limits.MaxStringTableEntries, Limits.MaxTableStringBytes, out int strings);
            own.FirstNewPrivateResource = ReadStringTable(ref r, own.NewPrivateResources, Limits.MaxResourceTableEntries, Limits.MaxResourceBytes, out int resources);
            own.FirstNewPrivateAppearance = ReadAppearanceTable(ref r, own.NewPrivateAppearances, true, out int appearances);
            if (strings + resources + appearances == 0)
                throw Bad("a private block flags tables and adds none");
            if ((long)strings + resources + appearances > Limits.MaxNewTableEntriesPerMessage)
                throw new ProtocolException(ProtocolError.CountTooLarge, "a private block that adds more than " + Limits.MaxNewTableEntriesPerMessage + " table entries");
        }

        if ((flags & OwnFlagScreen) != 0)
        {
            ReadScreenEntries(ref r, own.ScreenAdded);
            ReadScreenEntries(ref r, own.ScreenChanged);
            int removed = r.ReadCount(Limits.MaxScreenEntries, 1);
            own.ScreenRemoved.Capacity = removed;
            long previous = 0;
            for (int i = 0; i < removed; i++)
            {
                long id = ReadId(ref r, previous, true);
                previous = id;
                own.ScreenRemoved.Add(id);
            }

            if (own.ScreenAdded.Count + own.ScreenChanged.Count + removed == 0)
                throw Bad("a private block flags screen entries and has none");
        }

        if ((flags & OwnFlagImages) != 0)
            ReadImages(ref r, own);
        if ((flags & OwnFlagLayer) != 0)
            ReadLayer(ref r, own, tick);
        if ((flags & OwnFlagBody) != 0)
            ReadBody(ref r, own);
        if ((flags & OwnFlagGamePlane) != 0)
            ReadGamePlane(ref r, own);
        if ((flags & OwnFlagVision) != 0)
            ReadVision(ref r, own);
    }

    private static void WriteMotion(ref WireWriter w, in MotionState state, in MotionParams parameters)
    {
        WritePos(ref w, state.X, state.Y);
        w.WriteVarUInt(CheckCoordinate(state.Z, Limits.MaxMapLevels, "z"));
        w.WriteUInt16(state.Yaw);
        if (state.Posture > Posture.Lying)
            throw Bad("an unknown posture " + (byte)state.Posture);
        if (state.Driver > MotionDriver.ZeroG)
            throw Bad("an unknown motion driver " + (byte)state.Driver);
        bool slide = state.Slide.Active;
        bool drift = parameters.DriftX != 0 || parameters.DriftY != 0;
        w.WriteByte((byte)((byte)state.Posture | ((byte)state.Driver << 1) | (parameters.CanMove ? 8 : 0) | (slide ? 16 : 0) | (parameters.NoClip ? 32 : 0) | (drift ? 64 : 0) | (parameters.OnClimbable ? 128 : 0))); // 128: version 37, D239
        w.WriteVarInt(state.PressTimer);
        w.WriteVarInt(CheckVelocity(state.VelX)); // version 66 (DV263): the walking capsule's velocity
        w.WriteVarInt(CheckVelocity(state.VelY));
        if (slide)
        {
            WritePos(ref w, state.Slide.TargetX, state.Slide.TargetY);
            w.WriteVarUInt(state.Slide.TicksLeft);
        }
        else if (state.Slide.TargetX != 0 || state.Slide.TargetY != 0)
        {
            throw Bad("a slide that is not active carries a target");
        }

        CheckParams(parameters);
        w.WriteSingle(parameters.TilesPerTick);
        w.WriteSingle(parameters.Radius);
        w.WriteVarUInt((ulong)parameters.MoveDelayTicks);
        if (drift)
        {
            // version 28 (DV69): a weightless capsule's drift, subunits a tick
            w.WriteVarInt(parameters.DriftX);
            w.WriteVarInt(parameters.DriftY);
        }
        // version 48: the tiles around where the mob would be weightless, and its speed on the other kind
        w.WriteVarUInt(parameters.GravityMask);
        if (parameters.GravityMask != 0)
        {
            w.WriteVarUInt(CheckCoordinate(parameters.GravityX, Limits.MaxMapSize, "gravity x"));
            w.WriteVarUInt(CheckCoordinate(parameters.GravityY, Limits.MaxMapSize, "gravity y"));
            w.WriteSingle(parameters.OtherTilesPerTick);
            // version 50: whether it steers on a weightless tile with nothing to push off, and a drift's speed from rest
            w.WriteByte(parameters.OtherCanMove ? (byte)1 : (byte)0);
            w.WriteSingle(parameters.InertiaTilesPerTick);
        }
        else if (parameters.OtherCanMove || parameters.InertiaTilesPerTick != 0f)
        {
            throw Bad("the space step's parameters without a gravity mask");
        }
        // version 49: the speed and the move delay after a walk/run toggle
        w.WriteSingle(parameters.IntentTilesPerTick);
        w.WriteVarUInt((ulong)parameters.IntentMoveDelayTicks);
        w.WriteVarUInt((ulong)parameters.UnblockTicks); // version 50
        w.WriteVarUInt((ulong)parameters.ImpulseSpeed); // version 51 (DV177): the drift a push from rest would start, subunits a tick
        // version 66 (DV263): the speed ramp and the slower directions
        w.WriteVarUInt((ulong)parameters.Feel.AccelTicks);
        w.WriteVarUInt((ulong)parameters.Feel.DecelTicks);
        w.WriteSingle(parameters.Feel.BackSlow);
        w.WriteSingle(parameters.Feel.StrafeSlow);
        // version 69 (DV347): the running belts around, their speed first (0: none)
        WriteField(ref w, parameters.Belt, "belt");
        // version 72 (DV357): the wind around, as the belts; then a gravity pull's speed (0: none) and its tile
        WriteField(ref w, parameters.Wind, "wind");
        w.WriteVarUInt((ulong)parameters.PullSpeed);
        if (parameters.PullSpeed > 0)
        {
            w.WriteVarUInt(CheckCoordinate(parameters.PullX, Limits.MaxMapSize, "pull x"));
            w.WriteVarUInt(CheckCoordinate(parameters.PullY, Limits.MaxMapSize, "pull y"));
        }
        w.WriteByte(parameters.PassesFlaps ? (byte)1 : (byte)0); // version 73 (DV360)
        w.WriteByte((byte)((parameters.HaltsAtHandhold ? 1 : 0) | (parameters.SteersByHandhold ? 2 : 0))); // version 89 (DV534): the handhold flags
    }

    private static void WriteField(ref WireWriter w, BeltField field, string what)
    {
        w.WriteVarUInt((ulong)field.Speed);
        if (field.Active)
        {
            w.WriteVarUInt(CheckCoordinate(field.X, Limits.MaxMapSize, what + " x"));
            w.WriteVarUInt(CheckCoordinate(field.Y, Limits.MaxMapSize, what + " y"));
            w.WriteVarUInt(field.W0);
            w.WriteVarUInt(field.W1);
            w.WriteVarUInt(field.W2);
            w.WriteVarUInt(field.W3);
        }
    }

    private static void ReadMotion(ref WireReader r, out MotionState state, out MotionParams parameters)
    {
        state = default;
        ReadPos(ref r, out state.X, out state.Y);
        state.Z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "the own z");
        state.Yaw = r.ReadUInt16();
        byte packed = r.ReadByte();
        state.Posture = (Posture)(packed & 1);
        state.Driver = (MotionDriver)((packed >> 1) & 3);
        bool canMove = (packed & 8) != 0;
        bool noClip = (packed & 32) != 0;
        bool onClimbable = (packed & 128) != 0; // version 37, D239
        long pressTimer = r.ReadVarInt();
        if (pressTimer < short.MinValue || pressTimer > short.MaxValue)
            throw Bad("the own press timer is outside a 16-bit range");
        state.PressTimer = (short)pressTimer;
        state.VelX = ReadVelocity(ref r); // version 66
        state.VelY = ReadVelocity(ref r);
        if ((packed & 16) != 0)
        {
            ReadPos(ref r, out int targetX, out int targetY);
            ulong ticks = r.ReadVarUInt();
            if (ticks < 1 || ticks > ushort.MaxValue)
                throw Bad("an active slide with ticks left outside 1 to 65535");
            state.Slide = new Slide(targetX, targetY, (ushort)ticks);
        }

        float tilesPerTick = r.ReadSingle();
        float radius = r.ReadSingle();
        ulong delay = r.ReadVarUInt();
        if (delay > (ulong)short.MaxValue)
            throw Bad("the own move delay is outside a 16-bit range");
        short driftX = 0, driftY = 0;
        if ((packed & 64) != 0)
        {
            driftX = ReadDrift(ref r);
            driftY = ReadDrift(ref r);
            if (driftX == 0 && driftY == 0)
                throw Bad("the own motion flags a drift of zero");
        }
        ulong gravityMask = r.ReadVarUInt();
        int gravityX = 0, gravityY = 0;
        float otherTilesPerTick = 0f;
        bool otherCanMove = false;
        float inertiaTilesPerTick = 0f;
        if (gravityMask != 0)
        {
            if (gravityMask > MotionParams.GravityMaskAll)
                throw Bad("a gravity mask of more than 7 x 7 tiles");
            gravityX = ReadRanged(ref r, 1, Limits.MaxMapSize, "the gravity mask's x");
            gravityY = ReadRanged(ref r, 1, Limits.MaxMapSize, "the gravity mask's y");
            otherTilesPerTick = r.ReadSingle();
            byte otherCanMoveByte = r.ReadByte(); // version 50
            if (otherCanMoveByte > 1)
                throw Bad("the gravity mask's steering flag is not 0 or 1");
            otherCanMove = otherCanMoveByte == 1;
            inertiaTilesPerTick = r.ReadSingle();
        }
        float intentTilesPerTick = r.ReadSingle(); // version 49
        ulong intentDelay = r.ReadVarUInt();
        if (intentDelay > (ulong)short.MaxValue)
            throw Bad("the own move delay after a walk/run toggle is outside a 16-bit range");
        parameters = new MotionParams(tilesPerTick, radius, canMove, (short)delay, noClip, driftX, driftY, onClimbable, gravityMask, gravityX, gravityY, otherTilesPerTick,
            intentTilesPerTick, (short)intentDelay, otherCanMove, inertiaTilesPerTick, ReadUnblockTicks(ref r), ReadImpulse(ref r), ReadFeel(ref r), ReadBelt(ref r));
        BeltField wind = ReadBelt(ref r); // version 72 (DV357)
        ulong pullSpeed = r.ReadVarUInt();
        if (pullSpeed > (ulong)short.MaxValue)
            throw Bad("a pull speed outside a 16-bit range");
        int pullX = pullSpeed == 0 ? 0 : ReadRanged(ref r, 1, Limits.MaxMapSize, "the pull's x");
        int pullY = pullSpeed == 0 ? 0 : ReadRanged(ref r, 1, Limits.MaxMapSize, "the pull's y");
        byte passesFlaps = r.ReadByte(); // version 73 (DV360)
        if (passesFlaps > 1)
            throw Bad("the own flaps flag is not 0 or 1");
        byte handhold = r.ReadByte(); // version 89 (DV534)
        if (handhold > 3)
            throw Bad("the own handhold flags have bits other than 1 and 2");
        parameters = parameters with
        {
            Wind = wind, PullX = pullX, PullY = pullY, PullSpeed = (short)pullSpeed, PassesFlaps = passesFlaps == 1,
            HaltsAtHandhold = (handhold & 1) != 0, SteersByHandhold = (handhold & 2) != 0,
        };
        CheckParams(parameters);
    }

    // Version 69 (DV347): the running belts around; CheckParams checks the speed and the field's bits.
    private static BeltField ReadBelt(ref WireReader r)
    {
        ulong speed = r.ReadVarUInt();
        if (speed == 0)
            return default;
        if (speed > (ulong)short.MaxValue)
            throw Bad("a belt speed outside a 16-bit range");
        int x = ReadRanged(ref r, 1, Limits.MaxMapSize, "the belt field's x");
        int y = ReadRanged(ref r, 1, Limits.MaxMapSize, "the belt field's y");
        return new BeltField(r.ReadVarUInt(), r.ReadVarUInt(), r.ReadVarUInt(), r.ReadVarUInt(), x, y, (short)speed);
    }

    // Version 65 (DV263): a velocity component, 1/65536 tile a tick, at most the fastest speed either way.
    private static int CheckVelocity(int v)
    {
        long max = (long)(Limits.MaxTilesPerTick * MotionMath.VelocityPerTile);
        if (v < -max || v > max)
            throw Bad("a velocity outside " + Limits.MaxTilesPerTick + " tiles per tick");
        return v;
    }

    private static int ReadVelocity(ref WireReader r)
    {
        long v = r.ReadVarInt();
        long max = (long)(Limits.MaxTilesPerTick * MotionMath.VelocityPerTile);
        if (v < -max || v > max)
            throw Bad("the own velocity is outside " + Limits.MaxTilesPerTick + " tiles per tick");
        return (int)v;
    }

    // Version 65 (DV263): the movement feel; CheckParams checks its ranges.
    private static MoveFeel ReadFeel(ref WireReader r)
    {
        ulong accel = r.ReadVarUInt();
        ulong decel = r.ReadVarUInt();
        if (accel > (ulong)MoveFeel.MaxTicks || decel > (ulong)MoveFeel.MaxTicks)
            throw Bad("a speed ramp over " + MoveFeel.MaxTicks + " ticks");
        return new MoveFeel((short)accel, (short)decel, r.ReadSingle(), r.ReadSingle());
    }

    // Version 50: the ticks a drift's input block still holds.
    private static short ReadUnblockTicks(ref WireReader r)
    {
        ulong ticks = r.ReadVarUInt();
        if (ticks > (ulong)short.MaxValue)
            throw Bad("the own unblock ticks are outside a 16-bit range");
        return (short)ticks;
    }

    // Version 51: the push speed, subunits a tick: at most the fastest speed (Limits.MaxTilesPerTick).
    private static short ReadImpulse(ref WireReader r)
    {
        ulong v = r.ReadVarUInt();
        if (v > (ulong)(Limits.MaxTilesPerTick * 256))
            throw Bad("a push speed over " + Limits.MaxTilesPerTick + " tiles per tick");
        return (short)v;
    }

    // A drift component, subunits a tick: at most the fastest speed (Limits.MaxTilesPerTick) either way.
    private static short ReadDrift(ref WireReader r)
    {
        long v = r.ReadVarInt();
        long max = (long)(Limits.MaxTilesPerTick * 256);
        if (v < -max || v > max)
            throw Bad("a drift outside " + Limits.MaxTilesPerTick + " tiles per tick");
        return (short)v;
    }

    private static void CheckParams(in MotionParams p)
    {
        if (!(p.TilesPerTick >= 0f && p.TilesPerTick <= Limits.MaxTilesPerTick) || !(p.OtherTilesPerTick >= 0f && p.OtherTilesPerTick <= Limits.MaxTilesPerTick)
            || !(p.IntentTilesPerTick >= 0f && p.IntentTilesPerTick <= Limits.MaxTilesPerTick) || !(p.InertiaTilesPerTick >= 0f && p.InertiaTilesPerTick <= Limits.MaxTilesPerTick))
            throw Bad("a speed outside 0 to " + Limits.MaxTilesPerTick + " tiles per tick");
        if (!(p.Radius >= 0f && p.Radius <= Limits.MaxCapsuleRadius))
            throw Bad("a capsule radius outside 0 to " + Limits.MaxCapsuleRadius + " tiles");
        if (p.MoveDelayTicks < 0 || p.IntentMoveDelayTicks < 0 || p.UnblockTicks < 0)
            throw Bad("a negative move delay");
        long maxDrift = (long)(Limits.MaxTilesPerTick * 256);
        if (Math.Abs((long)p.DriftX) > maxDrift || Math.Abs((long)p.DriftY) > maxDrift)
            throw Bad("a drift outside " + Limits.MaxTilesPerTick + " tiles per tick");
        if (p.ImpulseSpeed < 0 || p.ImpulseSpeed > maxDrift)
            throw Bad("a push speed outside 0 to " + Limits.MaxTilesPerTick + " tiles per tick");
        if (!p.Feel.IsValid)
            throw Bad("a movement feel outside its ranges");
        if (p.Belt.Speed < 0 || p.Belt.Speed > maxDrift)
            throw Bad("a belt speed outside 0 to " + Limits.MaxTilesPerTick + " tiles per tick");
        if (p.Belt.Speed == 0 ? p.Belt != default : !BeltCodes(p.Belt))
            throw Bad("a belt field with bits outside 9 x 9 cardinal directions, or without a speed");
        if (p.Wind.Speed < 0 || p.Wind.Speed > maxDrift || p.PullSpeed < 0 || p.PullSpeed > maxDrift)
            throw Bad("a wind or pull speed outside 0 to " + Limits.MaxTilesPerTick + " tiles per tick");
        if (p.Wind.Speed == 0 ? p.Wind != default : !BeltCodes(p.Wind))
            throw Bad("a wind field with bits outside 9 x 9 cardinal directions, or without a speed");
        if (p.PullSpeed == 0 && (p.PullX != 0 || p.PullY != 0))
            throw Bad("a pull's tile without a speed");
    }

    // Version 69: every tile of a belt field is none or one cardinal direction (codes 0 to 4), and no bit lies past the last tile.
    private static bool BeltCodes(BeltField belt)
    {
        ulong pad = 1UL << 63; // a word's 64th bit, past its 21 tiles
        if ((belt.W0 & pad) != 0 || (belt.W1 & pad) != 0 || (belt.W2 & pad) != 0 || (belt.W3 & ~BeltField.LastWordMask) != 0)
            return false;
        for (int i = 0; i < BeltField.Side * BeltField.Side; i++)
        {
            if (belt.CodeAt(i) > 4)
                return false;
        }
        return true;
    }

    // A table update is [count] then, when the count is not 0, [varuint first index] and the entries: with no new entries
    // there is no first index (it decodes as 0).
    private static void WriteStringTable(ref WireWriter w, uint first, List<string> list, int maxEntries, int maxBytes)
    {
        CheckTableRange(first, list.Count, maxEntries, "a string or resource table");
        w.WriteCount(list.Count, maxEntries);
        if (list.Count == 0)
            return;
        w.WriteVarUInt(first);
        foreach (string s in list)
        {
            if (s == null)
                throw Bad("a null table string");
            w.WriteString(s, maxBytes);
        }
    }

    private static uint ReadStringTable(ref WireReader r, List<string> into, int maxEntries, int maxBytes, out int count)
    {
        count = r.ReadCount(maxEntries, MinResourceBytes);
        if (count == 0)
            return 0;
        uint first = ReadTableFirst(ref r, maxEntries);
        CheckTableRange(first, count, maxEntries, "a string or resource table");
        into.Capacity = count;
        for (int i = 0; i < count; i++)
        {
            string s = r.ReadString(maxBytes);
            if (s == null)
                throw new ProtocolException(ProtocolError.BadString, "a null table string");
            into.Add(s);
        }

        return first;
    }

    private static void WriteAppearanceTable(ref WireWriter w, uint first, List<AppearanceEntry> list, bool isPrivate)
    {
        CheckTableRange(first, list.Count, Limits.MaxAppearanceTableEntries, "an appearance table");
        w.WriteCount(list.Count, Limits.MaxAppearanceTableEntries);
        if (list.Count == 0)
            return;
        w.WriteVarUInt(first);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null)
                throw Bad("a null appearance entry");
            WriteAppearance(ref w, list[i], isPrivate, first + (uint)i);
        }
    }

    private static uint ReadAppearanceTable(ref WireReader r, List<AppearanceEntry> into, bool isPrivate, out int count)
    {
        count = r.ReadCount(Limits.MaxAppearanceTableEntries, MinAppearanceBytes);
        if (count == 0)
            return 0;
        uint first = ReadTableFirst(ref r, Limits.MaxAppearanceTableEntries);
        CheckTableRange(first, count, Limits.MaxAppearanceTableEntries, "an appearance table");
        into.Capacity = count;
        for (int i = 0; i < count; i++)
            into.Add(ReadAppearance(ref r, isPrivate, first + (uint)i));
        return first;
    }

    // A table's "first new index" is from 1 (0 is "none").
    private static uint ReadTableFirst(ref WireReader r, int maxEntries)
    {
        ulong first = r.ReadVarUInt();
        if (first < 1 || first > (ulong)maxEntries)
            throw Bad("a table's first new index is outside 1 to its cap");
        return (uint)first;
    }

    private static void CheckTableRange(uint first, int count, int maxEntries, string what)
    {
        if (count > 0 && first == 0)
            throw Bad(what + "'s first new index is 0 (they count from 1)");
        if (count > 0 && (long)first + count - 1 > maxEntries)
            throw new ProtocolException(ProtocolError.CountTooLarge, what + " would pass " + maxEntries + " entries");
    }

    // ---- table references ----

    // A TableRef is a varuint (index << 1) | private. In a public table everything is public; a private table may refer to
    // either (the codec cannot know a table's length: the replica checks the indices against it).
    private static void WritePlainRef(ref WireWriter w, TableRef reference, bool allowPrivate, int maxIndex, string what)
    {
        if (reference.Index > (uint)maxIndex || (reference.Index == 0 && reference.Private))
            throw Bad(what + " names an entry outside its table");
        if (reference.Private && !allowPrivate)
            throw Bad(what + " names a private entry where only public ones may be named (D40)");
        w.WriteVarUInt(((ulong)reference.Index << 1) | (reference.Private ? 1UL : 0UL));
    }

    // ---- appearance entries ----

    private const uint PresIcon = 1u << 0;
    private const uint PresIconState = 1u << 1;
    private const uint PresDir = 1u << 2;
    private const uint PresRgba = 1u << 3;
    private const uint PresMatrix = 1u << 4;
    private const uint PresAlpha = 1u << 5;
    private const uint PresLayer = 1u << 6;
    private const uint PresPlane = 1u << 7;
    private const uint PresPixelXY = 1u << 8;
    private const uint PresPixelWZ = 1u << 9;
    private const uint PresTransform = 1u << 10;
    private const uint PresFlags = 1u << 11;
    private const uint PresBlend = 1u << 12;
    private const uint PresOverlays = 1u << 13;
    private const uint PresUnderlays = 1u << 14;
    private const uint PresMaptext = 1u << 15; // version 18
    private const uint PresFilters = 1u << 16; // version 25
    private const uint PresRenderTarget = 1u << 17; // version 25
    private const uint PresAll = (1u << 18) - 1;

    private const uint DefaultRgba = 0xFFFFFFFF;

    // [varuint presence] then the present fields in bit order:
    //   icon, iconState: tableref; dir: u8; rgba: u32; matrix: 20 x f32; alpha: u8; layer: f32; plane: varint;
    //   pixelX, pixelY: varint; pixelW, pixelZ: varint; transform: 6 x f32; flags: varuint; blend: u8;
    //   overlays, underlays: [count 1..256] { tableref }*
    //   maptext (version 18; version 74: in the public tables too, the world's maptext): tableref (a string), width, height: varuint; x, y: varint
    //   filters (version 25): [count 1..16] { [u8 kind 1..4] [u8 fields: bit 0 x y, 1 size, 2 rgba, 3 matrix, 4 flags, 5 icon,
    //     6 icon state, 7 render source] x, y: f32; size: f32; rgba: u32; matrix: 20 x f32; flags: u8; icon, state, source: tableref }*
    //   renderTarget (version 25): tableref (a string)
    // A field is present exactly when it is not its default (none, 0, white, 255, +0.0).
    private static void WriteAppearance(ref WireWriter w, AppearanceEntry e, bool isPrivate, uint ownIndex)
    {
        uint presence = 0;
        if (!e.Icon.IsNone) presence |= PresIcon;
        if (!e.IconState.IsNone) presence |= PresIconState;
        if (e.Dir != 0) presence |= PresDir;
        if (e.Rgba != DefaultRgba) presence |= PresRgba;
        if (e.ColorMatrix != null) presence |= PresMatrix;
        if (e.Alpha != 255) presence |= PresAlpha;
        if (BitConverter.SingleToInt32Bits(e.Layer) != 0) presence |= PresLayer;
        if (e.Plane != 0) presence |= PresPlane;
        if (e.PixelX != 0 || e.PixelY != 0) presence |= PresPixelXY;
        if (e.PixelW != 0 || e.PixelZ != 0) presence |= PresPixelWZ;
        if (e.Transform != null) presence |= PresTransform;
        if (e.AppearanceFlags != 0) presence |= PresFlags;
        if (e.BlendMode != 0) presence |= PresBlend;
        if (e.OverlayCount > 0) presence |= PresOverlays;
        if (e.UnderlayCount > 0) presence |= PresUnderlays;
        if (!e.Maptext.IsNone) presence |= PresMaptext;
        if (e.Filters is { Count: > 0 }) presence |= PresFilters;
        if (!e.RenderTarget.IsNone) presence |= PresRenderTarget;
        w.WriteVarUInt(presence);
        const string what = "an appearance entry";
        if ((presence & PresIcon) != 0)
            WritePlainRef(ref w, e.Icon, isPrivate, Limits.MaxResourceTableEntries, what + "'s icon");
        if ((presence & PresIconState) != 0)
            WritePlainRef(ref w, e.IconState, isPrivate, Limits.MaxStringTableEntries, what + "'s icon state");
        if ((presence & PresDir) != 0)
        {
            CheckDir(e.Dir);
            w.WriteByte(e.Dir);
        }

        if ((presence & PresRgba) != 0)
            w.WriteUInt32(e.Rgba);
        if ((presence & PresMatrix) != 0)
        {
            if (e.ColorMatrix.Length != 20)
                throw Bad("a colour matrix of " + e.ColorMatrix.Length + " numbers (it has 20)");
            foreach (float f in e.ColorMatrix)
                w.WriteSingle(f);
        }

        if ((presence & PresAlpha) != 0)
            w.WriteByte(e.Alpha);
        if ((presence & PresLayer) != 0)
            w.WriteSingle(e.Layer);
        if ((presence & PresPlane) != 0)
            w.WriteVarInt(e.Plane);
        if ((presence & PresPixelXY) != 0)
        {
            w.WriteVarInt(e.PixelX);
            w.WriteVarInt(e.PixelY);
        }

        if ((presence & PresPixelWZ) != 0)
        {
            w.WriteVarInt(e.PixelW);
            w.WriteVarInt(e.PixelZ);
        }

        if ((presence & PresTransform) != 0)
        {
            if (e.Transform.Length != 6)
                throw Bad("a transform of " + e.Transform.Length + " numbers (it has 6)");
            foreach (float f in e.Transform)
                w.WriteSingle(f);
        }

        if ((presence & PresFlags) != 0)
            w.WriteVarUInt(e.AppearanceFlags);
        if ((presence & PresBlend) != 0)
            w.WriteByte(e.BlendMode);
        if ((presence & PresOverlays) != 0)
            WriteOverlayRefs(ref w, e.Overlays, isPrivate, ownIndex, "an overlay");
        if ((presence & PresUnderlays) != 0)
            WriteOverlayRefs(ref w, e.Underlays, isPrivate, ownIndex, "an underlay");
        if ((presence & PresMaptext) != 0)
        {
            WritePlainRef(ref w, e.Maptext, isPrivate, Limits.MaxStringTableEntries, what + "'s maptext");
            if (e.MaptextWidth < 0 || e.MaptextHeight < 0)
                throw Bad(what + "'s maptext box is negative");
            w.WriteVarUInt((ulong)e.MaptextWidth);
            w.WriteVarUInt((ulong)e.MaptextHeight);
            w.WriteVarInt(e.MaptextX);
            w.WriteVarInt(e.MaptextY);
        }

        if ((presence & PresFilters) != 0)
        {
            w.WriteCount(e.Filters.Count, Limits.MaxAppearanceFilters);
            foreach (FilterEntry f in e.Filters)
                WriteFilter(ref w, f, isPrivate);
        }

        if ((presence & PresRenderTarget) != 0)
            WritePlainRef(ref w, e.RenderTarget, isPrivate, Limits.MaxStringTableEntries, what + "'s render target");
    }

    // Version 26: the fields of a filter, a varuint mask (protocol.md section 35.1).
    private const uint FilterX = 1, FilterSize = 2, FilterRgba = 4, FilterMatrix = 8, FilterFlagsField = 16, FilterIcon = 32, FilterState = 64,
        FilterSource = 128, FilterOffset = 1u << 8, FilterAlpha = 1u << 9, FilterThreshold = 1u << 10, FilterThresholdRgba = 1u << 11,
        FilterDensity = 1u << 12, FilterFactor = 1u << 13, FilterRepeat = 1u << 14, FilterRadius = 1u << 15, FilterFalloff = 1u << 16,
        FilterSpace = 1u << 17, FilterTransform = 1u << 18, FilterBlend = 1u << 19, FilterFields = (1u << 20) - 1;

    private static void WriteFilter(ref WireWriter w, FilterEntry f, bool isPrivate)
    {
        if (f.Kind is < FilterKind.Alpha or > FilterKind.Greyscale)
            throw Bad("a filter of an undefined kind " + (byte)f.Kind);
        uint fields = 0;
        if (f.X != 0f || f.Y != 0f) fields |= FilterX;
        if (f.Size != 1f) fields |= FilterSize;
        if (f.Rgba != 0x000000FF) fields |= FilterRgba;
        if (f.ColorMatrix != null) fields |= FilterMatrix;
        if (f.Flags != 0) fields |= FilterFlagsField;
        if (!f.Icon.IsNone) fields |= FilterIcon;
        if (!f.IconState.IsNone) fields |= FilterState;
        if (!f.RenderSource.IsNone) fields |= FilterSource;
        if (f.Offset != 0f) fields |= FilterOffset;
        if (f.Alpha != 255f) fields |= FilterAlpha;
        if (f.Threshold != 0f) fields |= FilterThreshold;
        if (f.ThresholdRgba != 0x000000FF) fields |= FilterThresholdRgba;
        if (f.Density != 0f) fields |= FilterDensity;
        if (f.Factor != 0f) fields |= FilterFactor;
        if (f.Repeat != 0f) fields |= FilterRepeat;
        if (f.Radius != 0f) fields |= FilterRadius;
        if (f.Falloff != 0f) fields |= FilterFalloff;
        if (f.Space != 0) fields |= FilterSpace;
        if (f.Transform != null) fields |= FilterTransform;
        if (f.BlendMode != 0) fields |= FilterBlend;
        w.WriteByte((byte)f.Kind);
        w.WriteVarUInt(fields);
        if ((fields & FilterX) != 0)
        {
            w.WriteSingle(f.X);
            w.WriteSingle(f.Y);
        }
        if ((fields & FilterSize) != 0) w.WriteSingle(f.Size);
        if ((fields & FilterRgba) != 0) w.WriteUInt32(f.Rgba);
        if ((fields & FilterMatrix) != 0)
        {
            if (f.ColorMatrix.Length != 20)
                throw Bad("a filter's colour matrix of " + f.ColorMatrix.Length + " numbers (it has 20)");
            foreach (float n in f.ColorMatrix)
                w.WriteSingle(n);
        }
        if ((fields & FilterFlagsField) != 0) w.WriteByte(f.Flags);
        if ((fields & FilterIcon) != 0) WritePlainRef(ref w, f.Icon, isPrivate, Limits.MaxResourceTableEntries, "a filter's icon");
        if ((fields & FilterState) != 0) WritePlainRef(ref w, f.IconState, isPrivate, Limits.MaxStringTableEntries, "a filter's icon state");
        if ((fields & FilterSource) != 0) WritePlainRef(ref w, f.RenderSource, isPrivate, Limits.MaxStringTableEntries, "a filter's render source");
        if ((fields & FilterOffset) != 0) w.WriteSingle(f.Offset);
        if ((fields & FilterAlpha) != 0) w.WriteSingle(f.Alpha);
        if ((fields & FilterThreshold) != 0) w.WriteSingle(f.Threshold);
        if ((fields & FilterThresholdRgba) != 0) w.WriteUInt32(f.ThresholdRgba);
        if ((fields & FilterDensity) != 0) w.WriteSingle(f.Density);
        if ((fields & FilterFactor) != 0) w.WriteSingle(f.Factor);
        if ((fields & FilterRepeat) != 0) w.WriteSingle(f.Repeat);
        if ((fields & FilterRadius) != 0) w.WriteSingle(f.Radius);
        if ((fields & FilterFalloff) != 0) w.WriteSingle(f.Falloff);
        if ((fields & FilterSpace) != 0)
        {
            if (f.Space > 3) throw Bad("a filter's colour space " + f.Space);
            w.WriteByte(f.Space);
        }
        if ((fields & FilterTransform) != 0)
        {
            if (f.Transform.Length != 6)
                throw Bad("a filter's transform of " + f.Transform.Length + " numbers (it has 6)");
            foreach (float n in f.Transform)
                w.WriteSingle(n);
        }
        if ((fields & FilterBlend) != 0)
        {
            if (f.BlendMode > 5) throw Bad("a filter's blend mode " + f.BlendMode);
            w.WriteByte(f.BlendMode);
        }
    }

    private static FilterEntry ReadFilter(ref WireReader r, bool isPrivate)
    {
        byte kind = r.ReadByte();
        if (kind is < (byte)FilterKind.Alpha or > (byte)FilterKind.Greyscale)
            throw Bad("a filter of an undefined kind " + kind);
        ulong wide = r.ReadVarUInt();
        if ((wide & ~(ulong)FilterFields) != 0)
            throw Bad("a filter's fields have undefined bits");
        uint fields = (uint)wide;
        var f = new FilterEntry { Kind = (FilterKind)kind };
        if ((fields & FilterX) != 0)
        {
            f.X = ReadFinite(ref r, "a filter's x");
            f.Y = ReadFinite(ref r, "a filter's y");
        }
        if ((fields & FilterSize) != 0) f.Size = ReadFinite(ref r, "a filter's size");
        if ((fields & FilterRgba) != 0) f.Rgba = r.ReadUInt32();
        if ((fields & FilterMatrix) != 0)
        {
            f.ColorMatrix = new float[20];
            for (int i = 0; i < 20; i++)
                f.ColorMatrix[i] = ReadFinite(ref r, "a filter's colour matrix");
        }
        if ((fields & FilterFlagsField) != 0) f.Flags = r.ReadByte();
        if ((fields & FilterIcon) != 0) f.Icon = ReadPlainRef(ref r, isPrivate, Limits.MaxResourceTableEntries, "a filter's icon");
        if ((fields & FilterState) != 0) f.IconState = ReadPlainRef(ref r, isPrivate, Limits.MaxStringTableEntries, "a filter's icon state");
        if ((fields & FilterSource) != 0) f.RenderSource = ReadPlainRef(ref r, isPrivate, Limits.MaxStringTableEntries, "a filter's render source");
        if ((fields & FilterOffset) != 0) f.Offset = ReadFinite(ref r, "a filter's offset");
        if ((fields & FilterAlpha) != 0) f.Alpha = ReadFinite(ref r, "a filter's alpha");
        if ((fields & FilterThreshold) != 0) f.Threshold = ReadFinite(ref r, "a filter's threshold");
        if ((fields & FilterThresholdRgba) != 0) f.ThresholdRgba = r.ReadUInt32();
        if ((fields & FilterDensity) != 0) f.Density = ReadFinite(ref r, "a filter's density");
        if ((fields & FilterFactor) != 0) f.Factor = ReadFinite(ref r, "a filter's factor");
        if ((fields & FilterRepeat) != 0) f.Repeat = ReadFinite(ref r, "a filter's repeat");
        if ((fields & FilterRadius) != 0) f.Radius = ReadFinite(ref r, "a filter's radius");
        if ((fields & FilterFalloff) != 0) f.Falloff = ReadFinite(ref r, "a filter's falloff");
        if ((fields & FilterSpace) != 0)
        {
            f.Space = r.ReadByte();
            if (f.Space > 3) throw Bad("a filter's colour space " + f.Space);
        }
        if ((fields & FilterTransform) != 0)
        {
            f.Transform = new float[6];
            for (int i = 0; i < 6; i++)
                f.Transform[i] = ReadFinite(ref r, "a filter's transform");
        }
        if ((fields & FilterBlend) != 0)
        {
            f.BlendMode = r.ReadByte();
            if (f.BlendMode > 5) throw Bad("a filter's blend mode " + f.BlendMode);
        }
        return f;
    }

    private static float ReadFinite(ref WireReader r, string what)
    {
        float value = r.ReadSingle();
        if (!float.IsFinite(value))
            throw Bad(what + " is not a finite number");
        return value;
    }

    private static void WriteOverlayRefs(ref WireWriter w, List<TableRef> list, bool isPrivate, uint ownIndex, string what)
    {
        w.WriteCount(list.Count, Limits.MaxAppearanceOverlays);
        foreach (TableRef reference in list)
        {
            if (reference.Index > (uint)Limits.MaxAppearanceTableEntries || (reference.Index == 0 && reference.Private))
                throw Bad(what + " names an entry outside its table");
            if (reference.Private && !isPrivate)
                throw Bad(what + " names a private entry in a public appearance (D40)");
            // An overlay names an entry of the same table that comes before the one that holds it (no cycles, one pass to build).
            if (reference.Private == isPrivate && reference.Index >= ownIndex)
                throw Bad(what + " names an entry that does not precede the one that holds it");
            if (reference.Index == 0)
                throw Bad(what + " names no entry");
            w.WriteVarUInt(((ulong)reference.Index << 1) | (reference.Private ? 1UL : 0UL));
        }
    }

    private static AppearanceEntry ReadAppearance(ref WireReader r, bool isPrivate, uint ownIndex)
    {
        ulong bits = r.ReadVarUInt();
        if (bits > PresAll)
            throw Bad("an appearance entry sets presence bits that are not defined");
        uint presence = (uint)bits;
        var e = new AppearanceEntry();
        const string what = "an appearance entry";
        if ((presence & PresIcon) != 0)
        {
            e.Icon = ReadPlainRef(ref r, isPrivate, Limits.MaxResourceTableEntries, what + "'s icon");
            if (e.Icon.IsNone)
                throw Bad(what + "'s icon is present and names none");
        }

        if ((presence & PresIconState) != 0)
        {
            e.IconState = ReadPlainRef(ref r, isPrivate, Limits.MaxStringTableEntries, what + "'s icon state");
            if (e.IconState.IsNone)
                throw Bad(what + "'s icon state is present and names none");
        }

        if ((presence & PresDir) != 0)
        {
            e.Dir = r.ReadByte();
            CheckDir(e.Dir);
            if (e.Dir == 0)
                throw Bad(what + "'s dir is present and 0");
        }

        if ((presence & PresRgba) != 0)
        {
            e.Rgba = r.ReadUInt32();
            if (e.Rgba == DefaultRgba)
                throw Bad(what + "'s colour is present and white");
        }

        if ((presence & PresMatrix) != 0)
        {
            var matrix = new float[20];
            for (int i = 0; i < matrix.Length; i++)
                matrix[i] = r.ReadSingle();
            e.ColorMatrix = matrix;
        }

        if ((presence & PresAlpha) != 0)
        {
            e.Alpha = r.ReadByte();
            if (e.Alpha == 255)
                throw Bad(what + "'s alpha is present and 255");
        }

        if ((presence & PresLayer) != 0)
        {
            e.Layer = r.ReadSingle();
            if (BitConverter.SingleToInt32Bits(e.Layer) == 0)
                throw Bad(what + "'s layer is present and +0");
        }

        if ((presence & PresPlane) != 0)
        {
            e.Plane = ReadShort(ref r, what + "'s plane");
            if (e.Plane == 0)
                throw Bad(what + "'s plane is present and 0");
        }

        if ((presence & PresPixelXY) != 0)
        {
            e.PixelX = ReadShort(ref r, what + "'s pixel x");
            e.PixelY = ReadShort(ref r, what + "'s pixel y");
            if (e.PixelX == 0 && e.PixelY == 0)
                throw Bad(what + "'s pixel offset is present and 0");
        }

        if ((presence & PresPixelWZ) != 0)
        {
            e.PixelW = ReadShort(ref r, what + "'s pixel w");
            e.PixelZ = ReadShort(ref r, what + "'s pixel z");
            if (e.PixelW == 0 && e.PixelZ == 0)
                throw Bad(what + "'s pixel w and z are present and 0");
        }

        if ((presence & PresTransform) != 0)
        {
            var transform = new float[6];
            for (int i = 0; i < transform.Length; i++)
                transform[i] = r.ReadSingle();
            e.Transform = transform;
        }

        if ((presence & PresFlags) != 0)
        {
            ulong flags = r.ReadVarUInt();
            if (flags == 0 || flags > ushort.MaxValue)
                throw Bad(what + "'s appearance flags are present and 0, or over 16 bits");
            e.AppearanceFlags = (ushort)flags;
        }

        if ((presence & PresBlend) != 0)
        {
            e.BlendMode = r.ReadByte();
            if (e.BlendMode == 0)
                throw Bad(what + "'s blend mode is present and 0");
        }

        if ((presence & PresOverlays) != 0)
            ReadOverlayRefs(ref r, e.Overlays, isPrivate, ownIndex, "an overlay");
        if ((presence & PresUnderlays) != 0)
            ReadOverlayRefs(ref r, e.Underlays, isPrivate, ownIndex, "an underlay");
        if ((presence & PresMaptext) != 0)
        {
            e.Maptext = ReadPlainRef(ref r, isPrivate, Limits.MaxStringTableEntries, what + "'s maptext");
            if (e.Maptext.IsNone)
                throw Bad(what + "'s maptext is present and names none");
            ulong width = r.ReadVarUInt(), height = r.ReadVarUInt();
            if (width > (ulong)short.MaxValue || height > (ulong)short.MaxValue)
                throw Bad(what + "'s maptext box is outside a 16-bit range");
            e.MaptextWidth = (short)width;
            e.MaptextHeight = (short)height;
            e.MaptextX = ReadShort(ref r, what + "'s maptext x");
            e.MaptextY = ReadShort(ref r, what + "'s maptext y");
        }

        if ((presence & PresFilters) != 0)
        {
            int count = r.ReadCount(Limits.MaxAppearanceFilters, 1);
            e.Filters = new List<FilterEntry>(count);
            for (int i = 0; i < count; i++)
                e.Filters.Add(ReadFilter(ref r, isPrivate));
        }

        if ((presence & PresRenderTarget) != 0)
        {
            e.RenderTarget = ReadPlainRef(ref r, isPrivate, Limits.MaxStringTableEntries, what + "'s render target");
            if (e.RenderTarget.IsNone)
                throw Bad(what + "'s render target is present and names none");
        }

        return e;
    }

    // An icon or an icon state: a private table's entry may name a public entry too (for what is not secret).
    private static TableRef ReadPlainRef(ref WireReader r, bool allowPrivate, int maxIndex, string what)
    {
        ulong value = r.ReadVarUInt();
        bool isPrivate = (value & 1) != 0;
        ulong index = value >> 1;
        if (index > (ulong)maxIndex || (index == 0 && isPrivate))
            throw Bad(what + " names an entry outside its table");
        if (isPrivate && !allowPrivate)
            throw Bad(what + " names a private entry where only public ones may be named (D40)");
        return new TableRef((uint)index, isPrivate);
    }

    private static void ReadOverlayRefs(ref WireReader r, List<TableRef> into, bool isPrivate, uint ownIndex, string what)
    {
        int count = r.ReadCount(Limits.MaxAppearanceOverlays, 1);
        if (count == 0)
            throw Bad("an appearance entry flags " + what + "s and has none");
        into.Capacity = count;
        for (int i = 0; i < count; i++)
        {
            ulong value = r.ReadVarUInt();
            bool refPrivate = (value & 1) != 0;
            ulong index = value >> 1;
            if (index == 0 || index > (ulong)Limits.MaxAppearanceTableEntries)
                throw Bad(what + " names an entry outside its table");
            if (refPrivate && !isPrivate)
                throw Bad(what + " names a private entry in a public appearance (D40)");
            if (refPrivate == isPrivate && index >= ownIndex)
                throw Bad(what + " names an entry that does not precede the one that holds it");
            into.Add(new TableRef((uint)index, refPrivate));
        }
    }

    private static short ReadShort(ref WireReader r, string what)
    {
        long value = r.ReadVarInt();
        if (value < short.MinValue || value > short.MaxValue)
            throw Bad(what + " is outside a 16-bit range");
        return (short)value;
    }

    // ---- positions: u16 pairs in 1/256 tile, the tile inside the protocol's map range ----

    private static void WritePos(ref WireWriter w, int x, int y)
    {
        CheckPos(x, y);
        w.WriteUInt16((ushort)x);
        w.WriteUInt16((ushort)y);
    }

    private static void ReadPos(ref WireReader r, out int x, out int y)
    {
        x = r.ReadUInt16();
        y = r.ReadUInt16();
        CheckPos(x, y);
    }

    private static void CheckPos(int x, int y)
    {
        if (x < 0 || x > ushort.MaxValue || y < 0 || y > ushort.MaxValue
            || MotionMath.TileOf(x) < 1 || MotionMath.TileOf(x) > Limits.MaxMapSize || MotionMath.TileOf(y) < 1 || MotionMath.TileOf(y) > Limits.MaxMapSize)
            throw Bad("a position of " + x + "," + y + " (1/256 tile) is outside the map's range");
    }

    // ---- screen entries ----

    private static void WriteScreenEntries(ref WireWriter w, List<ScreenEntry> list)
    {
        w.WriteCount(list.Count, Limits.MaxScreenEntries);
        long previous = 0;
        foreach (ScreenEntry e in list)
        {
            WriteId(ref w, e.Id, previous, true);
            previous = e.Id;
            if (e.Appearance.Index > (uint)Limits.MaxAppearanceTableEntries || (e.Appearance.Index == 0 && e.Appearance.Private))
                throw Bad("a screen entry's appearance is outside its table");
            w.WriteVarUInt(((ulong)e.Appearance.Index << 1) | (e.Appearance.Private ? 1UL : 0UL));
            w.WriteString(e.ScreenLoc, Limits.MaxScreenLocBytes);
            if (e.Group > Limits.MaxHudGroup)
                throw Bad("a screen entry's HUD group " + e.Group + " is over " + Limits.MaxHudGroup);
            w.WriteByte(e.Group);
            WriteScreenLook(ref w, e);
        }
    }

    private static void ReadScreenEntries(ref WireReader r, List<ScreenEntry> into)
    {
        int count = r.ReadCount(Limits.MaxScreenEntries, MinScreenEntryBytes);
        into.Capacity = count;
        long previous = 0;
        for (int i = 0; i < count; i++)
        {
            long id = ReadId(ref r, previous, true);
            previous = id;
            TableRef appearance = ReadPlainRef(ref r, true, Limits.MaxAppearanceTableEntries, "a screen entry's appearance");
            string loc = r.ReadString(Limits.MaxScreenLocBytes);
            byte group = r.ReadByte();
            if (group > Limits.MaxHudGroup)
                throw Bad("a screen entry's HUD group " + group + " is over " + Limits.MaxHudGroup);
            ReadScreenLook(ref r, out TableRef flick, out long flickTick, out EntityAnimation animation, out ScreenChildren children, out long actionTarget,
                out ScreenInventory inventory);
            into.Add(new ScreenEntry(id, appearance, loc) { Group = group, FlickAppearance = flick, FlickTick = flickTick, Animation = animation, VisChildren = children,
                ActionTarget = actionTarget, SlotId = inventory.SlotId, HandIndex = inventory.HandIndex });
        }
    }

    // ---- cell changes (M3: doors, ChangeTurf; kept current by the version-2 replicator) ----

    // [count] { [varuint x] [varuint y] [varuint z] [u8 solid 0|1] }*   and   [count] { [varuint x] [varuint y] [varuint z] [varuint appearance] }*
    // Each list is in the Delta body only when it is not empty (the Delta's parts byte says which).

    private static void WriteSolidChanges(ref WireWriter w, List<CellSolidChange> list, int max)
    {
        w.WriteCount(list.Count, max);
        foreach (CellSolidChange c in list)
        {
            WriteCell(ref w, c.X, c.Y, c.Z);
            w.WriteByte(c.Solid ? (byte)1 : (byte)0);
        }
    }

    private static void WriteTurfChanges(ref WireWriter w, List<CellAppearanceChange> list, int max)
    {
        w.WriteCount(list.Count, max);
        foreach (CellAppearanceChange c in list)
        {
            WriteCell(ref w, c.X, c.Y, c.Z);
            if (c.Appearance.Private || c.Appearance.Index > (uint)Limits.MaxAppearanceTableEntries)
                throw Bad("a turf appearance change names a private or out-of-table entry");
            w.WriteVarUInt(c.Appearance.Index);
        }
    }

    private static void ReadSolidChanges(ref WireReader r, List<CellSolidChange> into, int max)
    {
        int count = r.ReadCount(max, MinCellChangeBytes);
        into.Capacity = count;
        for (int i = 0; i < count; i++)
        {
            ReadCell(ref r, out int x, out int y, out int z);
            byte flag = r.ReadByte();
            if (flag > 1)
                throw Bad("a solidity flag other than 0 or 1");
            into.Add(new CellSolidChange(x, y, z, flag == 1));
        }
    }

    private static void ReadTurfChanges(ref WireReader r, List<CellAppearanceChange> into, int max)
    {
        int count = r.ReadCount(max, MinCellChangeBytes);
        into.Capacity = count;
        for (int i = 0; i < count; i++)
        {
            ReadCell(ref r, out int x, out int y, out int z);
            ulong index = r.ReadVarUInt();
            if (index > (ulong)Limits.MaxAppearanceTableEntries)
                throw Bad("a turf appearance change names an entry outside its table");
            into.Add(new CellAppearanceChange(x, y, z, new TableRef((uint)index, false)));
        }
    }

    private static void WriteCell(ref WireWriter w, int x, int y, int z)
    {
        w.WriteVarUInt(CheckCoordinate(x, Limits.MaxMapSize, "x"));
        w.WriteVarUInt(CheckCoordinate(y, Limits.MaxMapSize, "y"));
        w.WriteVarUInt(CheckCoordinate(z, Limits.MaxMapLevels, "z"));
    }

    private static void ReadCell(ref WireReader r, out int x, out int y, out int z)
    {
        x = ReadRanged(ref r, 1, Limits.MaxMapSize, "a cell x");
        y = ReadRanged(ref r, 1, Limits.MaxMapSize, "a cell y");
        z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "a cell z");
    }
}
