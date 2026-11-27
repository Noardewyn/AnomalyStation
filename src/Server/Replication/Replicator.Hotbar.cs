using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Version 99 also (DV689, ui-overhaul.md section 10, slice 3A; protocol.md section 97): the own human's ten hotbar cells and the names of the items
// in its hands, read after the tick (BeltHotbar.Peek: the cells as the next command phase will keep them) and sent when they change, when a press
// was refused, and in a join. A cell's picture is its item drawn alone, as a slot's vis_contents child (the same private row the HUD's slot uses).

public sealed partial class Replicator
{
    private sealed partial class SessionState
    {
        private HotbarMessage _hotbarSent;
        private BeltHotbar _hotbarOf; // the cells whose refusals _hotbarRefusals counts
        private int _hotbarRefusals;
        private long _hotbarLookedAt = long.MinValue; // the tick of this session's last look at the cells

        /// <summary>The Hotbar message of the last <see cref="Update"/>, or null when nothing changed.</summary>
        public HotbarMessage PendingHotbar { get; set; }

        private void UpdateHotbar(client player, long tick, bool full)
        {
            long lookedBefore = _hotbarLookedAt;
            _hotbarLookedAt = tick;
            HotbarMessage now = HotbarMessage.None;
            BeltHotbar kept = null;
            if (player?.mob is mob_living_carbon_human human && human.gc_destroyed == null)
            {
                kept = human.dm_hotbar;
                now = Read(human);
            }
            byte refused = 0;
            if (kept != _hotbarOf)
            {
                // cells made since the last look count their refusals from 0 (a press may have been refused in their first command phase); cells
                // that already existed (a body re-entered, a mind moved into a human, a join) start from their count, so an old refusal is not replayed
                _hotbarOf = kept;
                _hotbarRefusals = !full && kept != null && kept.CreatedTick > lookedBefore ? 0 : kept?.Refusals ?? 0;
            }
            if (kept != null && kept.Refusals != _hotbarRefusals)
            {
                _hotbarRefusals = kept.Refusals;
                refused = full ? (byte)0 : (byte)kept.RefusedCell;
            }
            if (!full && refused == 0 && now.Equals(_hotbarSent))
                return;
            _hotbarSent = now;
            PendingHotbar = refused == 0 ? now : new HotbarMessage
            {
                Bar = now.Bar, BeltWorn = now.BeltWorn, Cells = now.Cells, LeftHand = now.LeftHand, RightHand = now.RightHand, RefusedCell = refused,
            };
        }

        private HotbarMessage Read(mob_living_carbon_human human)
        {
            BeltHotbar bar = BeltHotbar.Peek(human);
            var cells = new HotbarCell[HotbarMessage.CellCount];
            for (int c = 1; c <= HotbarMessage.CellCount; c++)
            {
                HotbarState state = bar.StateOf(human, c);
                if (state == HotbarState.Empty)
                {
                    cells[c - 1] = HotbarCell.Empty;
                    continue;
                }
                uint look = Tables.InternVisChild(bar.ItemOf(c));
                cells[c - 1] = new HotbarCell(state == HotbarState.Holding ? HotbarCellState.Holding : HotbarCellState.Out, look == 0 ? TableRef.None : new TableRef(look, true));
            }
            return new HotbarMessage
            {
                Bar = true,
                BeltWorn = bar.Belt != null,
                Cells = cells,
                LeftHand = HandName(human, 1),
                RightHand = HandName(human, 2),
            };
        }

        // The name of the item in a hand (tg's held index: 1 left, 2 right), without tg's proper and improper markers; null for an empty hand.
        private static string HandName(mob_living_carbon_human human, int index)
        {
            if (human.get_item_for_held_index(index) is not obj_item item)
                return null;
            string name = Dm.jointext(Dm.list(item), ""); // the item's name as plain text (DmList.Join), no article and no markers
            return string.IsNullOrEmpty(name) ? null : ServerHost.Truncate(name, Limits.MaxHotbarNameBytes);
        }
    }

    /// <inheritdoc/>
    public HotbarMessage TakeHotbar(int session)
    {
        if (!_sessions.TryGetValue(session, out SessionState state) || state.PendingHotbar == null)
            return null;
        HotbarMessage taken = state.PendingHotbar;
        state.PendingHotbar = null;
        return taken;
    }
}
