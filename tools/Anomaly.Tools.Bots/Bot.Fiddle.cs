using Anomaly.Protocol;

namespace Anomaly.Tools.Bots;

// Owner: M4a P6. The version-4 behaviours of a bot (docs/ai/specs/interaction.md, scope 2i "Bots v4"): Fiddle, which clicks, hovers
// and presses keybindings on what is near for load and soak, and the target forgeries of ForgedTarget and HoverFlood. Every
// message is built with the codec's encoder (a legal message); only its target is wrong in the forgeries, and the server's
// answer to it is a silent drop, never a strike. The format violations of BadV4 are AbusePayloads.BadV4Action's raw bytes.

public sealed partial class Bot
{
    /// <summary>Updates between two interactions of a fiddling bot (4 a second, under every action rate).</summary>
    public const int FiddlePeriodUpdates = 5;

    /// <summary>How far, in tiles, a fiddling bot looks for something to touch (tg's reach is one tile; the server's view range is 7).</summary>
    public const int FiddleReachTiles = 2;

    /// <summary>Interactions in a cycle of <see cref="Fiddle"/>.</summary>
    private const int FiddleCycle = 13;

    private int _fiddleStep;

    // One interaction every FiddlePeriodUpdates updates, in a fixed cycle (so a run is reproducible for its seed).
    private void Fiddle()
    {
        if (UpdateCount % FiddlePeriodUpdates != 0) return;
        if (!Replica.TryGet(Replica.OwnEntity, out ReplicaEntity own)) return;
        EntityState me = own.State;
        switch (_fiddleStep++ % FiddleCycle)
        {
            case 0: // look at what is near
                if (NearestEntity(me, FiddleReachTiles + 1, false, out long hovered)) SendFiddle(HoverOn(hovered));
                break;
            case 1: // pick up the nearest item (a left click takes it into the hand when it is in reach)
                if (NearestEntity(me, FiddleReachTiles, true, out long item)) SendFiddle(ClickOn(item, ClickButton.Left, ClickModifiers.None));
                break;
            case 2:
                SendFiddle(Key(KeybindingCode.ActivateInhand, false));
                break;
            case 3:
                SendFiddle(Key(KeybindingCode.SwapHands, false));
                break;
            case 4: // its own HUD: a hand, a slot or an open storage's cell (a screen target)
                if (RandomScreenEntry(out long screen)) SendFiddle(ClickScreen(screen));
                break;
            case 5:
                SendFiddle(Key(KeybindingCode.DropItem, false));
                break;
            case 6: // throw: throw mode on, a click on a cell, throw mode off
                SendFiddle(Key(KeybindingCode.ToggleThrowMode, false));
                break;
            case 7:
                SendFiddle(ClickTurf(me, ClickButton.Left));
                break;
            case 8:
                SendFiddle(Key(KeybindingCode.ToggleThrowMode, false));
                break;
            case 9: // examine (shift-click) the nearest thing
                if (NearestEntity(me, FiddleReachTiles + 1, false, out long examined)) SendFiddle(ClickOn(examined, ClickButton.Left, ClickModifiers.Shift));
                break;
            case 10:
                SendFiddle(Key(KeybindingCode.QuickEquip, false));
                break;
            case 11: // version 17: a drag of one of its HUD entries onto another (a slot onto a hand: what taking a backpack off is)
                if (RandomScreenEntry(out long dragged) && RandomScreenEntry(out long over)) SendFiddle(DropScreen(dragged, over));
                break;
            default: // the floor under its feet (a turf hover)
                SendFiddle(HoverTurf(me));
                break;
        }
    }

    private void SendFiddle(in ActionMessage action)
    {
        SendAction(action);
        Stats.FiddleActions++;
    }

    private ActionMessage HoverOn(long id)
        => new(_frameSeq, ActionKind.Hover, id, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Entity };

    private ActionMessage HoverTurf(in EntityState me)
        => new(_frameSeq, ActionKind.Hover, 0, ClickButton.None, ClickModifiers.None, null)
        {
            TargetKind = TargetKind.Turf,
            TurfX = me.X,
            TurfY = me.Y,
            TurfZ = me.Z,
        };

    private ActionMessage ClickOn(long id, ClickButton button, ClickModifiers modifiers)
        => new(_frameSeq, ActionKind.Click, id, button, modifiers, null)
        {
            TargetKind = TargetKind.Entity,
            IconX = (byte)_random.Next(1, Limits.MaxIconPixel),
            IconY = (byte)_random.Next(1, Limits.MaxIconPixel),
        };

    private ActionMessage ClickScreen(long id)
        => new(_frameSeq, ActionKind.Click, id, ClickButton.Left, ClickModifiers.None, null)
        {
            TargetKind = TargetKind.Screen,
            IconX = (byte)_random.Next(1, Limits.MaxIconPixel),
            IconY = (byte)_random.Next(1, Limits.MaxIconPixel),
        };

    // Version 17: a left drag of one screen entry released over another, a quick one (2 deciseconds).
    private ActionMessage DropScreen(long dragged, long over)
        => new(_frameSeq, ActionKind.Drop, dragged, ClickButton.Left, ClickModifiers.None, null)
        {
            TargetKind = TargetKind.Screen,
            OverKind = TargetKind.Screen,
            OverTarget = over,
            IconX = (byte)_random.Next(1, Limits.MaxIconPixel),
            IconY = (byte)_random.Next(1, Limits.MaxIconPixel),
            DragTime = 2,
        };

    // A cell within 4 tiles of the bot, inside the map: a throw target.
    private ActionMessage ClickTurf(in EntityState me, ClickButton button)
    {
        MapGrid map = Replica.Map;
        int width = map?.Width ?? Limits.MaxMapSize, height = map?.Height ?? Limits.MaxMapSize;
        return new ActionMessage(_frameSeq, ActionKind.Click, 0, button, ClickModifiers.None, null)
        {
            TargetKind = TargetKind.Turf,
            TurfX = Math.Clamp(me.X + _random.Next(-4, 4), 1, width),
            TurfY = Math.Clamp(me.Y + _random.Next(-4, 4), 1, height),
            TurfZ = me.Z,
            IconX = (byte)_random.Next(1, Limits.MaxIconPixel),
            IconY = (byte)_random.Next(1, Limits.MaxIconPixel),
        };
    }

    private ActionMessage Key(KeybindingCode code, bool up)
        => new(_frameSeq, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = code, KeyUp = up };

    // The nearest entity other than the bot's own mob within range tiles on its level (an item first, when asked: a /obj/item
    // type), by Chebyshev distance, lowest id on a tie.
    private bool NearestEntity(in EntityState me, int range, bool itemsOnly, out long id)
    {
        id = 0;
        int best = int.MaxValue;
        foreach (ReplicaEntity e in Replica.Entities)
        {
            EntityState s = e.State;
            if (s.Id == me.Id || s.Z != me.Z) continue;
            int distance = Math.Max(Math.Abs(s.X - me.X), Math.Abs(s.Y - me.Y));
            if (distance > range || distance >= best) continue;
            if (itemsOnly && !e.TypePath.StartsWith("/obj/item", StringComparison.Ordinal)) continue;
            best = distance;
            id = s.Id;
        }
        return id != 0;
    }

    private bool RandomScreenEntry(out long id)
    {
        id = 0;
        int count = Replica.Screen.Count;
        if (count == 0) return false;
        int pick = _random.Next(0, count - 1);
        foreach (long key in Replica.Screen.Keys)
        {
            if (pick-- == 0)
            {
                id = key;
                return true;
            }
        }
        return false;
    }

    // ---- the forgeries ----

    // A hover on the bot's own mob: a legal target, so that a hover flood tests the rate and nothing else.
    private void HoverSomething()
    {
        long own = Replica.OwnEntity;
        if (own != 0) SendEncodedAction(AbusePayloads.HoverOnEntity(_frameSeq, own), ActionKind.Hover);
    }

    // One legal-looking click or hover on a target that is not there, in turn (the server drops each silently and counts it).
    private void ForgeTarget()
    {
        byte[] message;
        ActionKind kind = ActionKind.Click;
        long own = Replica.OwnEntity;
        Replica.TryGet(own, out ReplicaEntity me);
        switch (_fiddleStep++ % 6)
        {
            case 0:
                message = AbusePayloads.MissingTargetClick(_frameSeq, _random);
                break;
            case 1:
                message = AbusePayloads.ForgedTurfClick(_frameSeq, _random);
                break;
            case 2:
                message = AbusePayloads.ForgedScreenClick(_frameSeq, _random);
                break;
            case 3:
                message = AbusePayloads.ForgedHover(_frameSeq, _random);
                kind = ActionKind.Hover;
                break;
            case 4:
                // An entity that exists but is out of the view range (or on another level): a real id, a forged target.
                message = me != null && FarEntity(me.State, out long far)
                    ? AbusePayloads.EntityClick(_frameSeq, far, ClickButton.Left)
                    : AbusePayloads.MissingTargetClick(_frameSeq, _random);
                break;
            default:
                message = AbusePayloads.ForgedTurfHover(_frameSeq, _random);
                kind = ActionKind.Hover;
                break;
        }

        SendEncodedAction(message, kind);
    }

    // The first entity farther than the server's view range from the bot (more than 8 tiles), or on another level.
    private bool FarEntity(in EntityState me, out long id)
    {
        foreach (ReplicaEntity e in Replica.Entities)
        {
            EntityState s = e.State;
            if (s.Id == me.Id) continue;
            if (s.Z != me.Z || Math.Max(Math.Abs(s.X - me.X), Math.Abs(s.Y - me.Y)) > 8)
            {
                id = s.Id;
                return true;
            }
        }
        id = 0;
        return false;
    }
}
