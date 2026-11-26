using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// <summary>D295: tg's floor recipes on a ceiling, the held item used on it from below. Floor things (cables, pipes, items) are refused.</summary>
public sealed partial class CeilingSystem
{
    private const string Genhit = "sound/items/weapons/genhit.ogg", Deconstruct = "sound/items/deconstruct.ogg";
    private readonly HashSet<long> _builders = new();
    private readonly Dictionary<(int, int, int), long> _revisions = new();

    /// <summary>What one use of the held item does: the next state, the stack it uses, its wait, the refund and the messages.</summary>
    private readonly record struct Step(CeilingState Next, int Cost = 0, float Delay = 0, bool Tool = false, Type Refund = null,
        float RefundCount = 0, string Start = null, string Done = null, string Sound = null, int Volume = 50, bool Extend = false, bool Hit = false);

    public async DmTask Interact(mob_living user, turf target)
    {
        if (user == null || target == null || !_builders.Add(user.dm_id)) return;
        try
        {
            // tg: code/_onclick/click.dm:120  ClickOn's cooldown (a ceiling click comes here instead of ClickOn)
            if (Dm.gt(user.next_move, world.time) || !await Reachable(user, target)) return;
            int x = (int)target.x, y = (int)target.y, z = (int)target.z;
            // D325: under a level above the ceiling is that level's turf, built and broken on that level. From below only the tile under its
            // plating is worked on (DV507): a floor tile is laid there, as on plating, and pried off again
            bool under = HasLevelAbove(target);
            var key = (x, y, z);
            long revision = _revisions.GetValueOrDefault(key);
            obj_item item = user.get_active_held_item() as obj_item;
            if (Plan(user, item, x, y, z, under ? UnderLevel(x, y, z) : At(x, y, z), under) is not Step step) return;
            if (step.Hit) { await Hit(user, item, target); return; }
            if (step.Start != null) to_chat(user, span_notice(step.Start));
            if (step.Tool && !Dm.truthy(await item.use_tool(target, user, step.Delay, volume: 80))) return;
            if (!step.Tool && step.Delay > 0 && !Dm.truthy(await do_after(user, step.Delay, target: target))) return;
            if (QDELETED(item) || user.get_active_held_item() != item || !await Reachable(user, target)
                || _revisions.GetValueOrDefault(key) != revision || (step.Extend && !CanExtend(x, y, z))) return;
            if (step.Cost > 0 && !Dm.truthy(await ((obj_item_stack)item).use(step.Cost))) return;
            if (under) SetLaidUnder(x, y, z, step.Next.Turf == CeilingState.Plating ? default : step.Next);
            else Set(x, y, z, step.Next);
            if (step.Refund != null && step.RefundCount > 0) Dm.@new(step.Refund, target, step.RefundCount);
            if (step.Sound != null) playsound(target, Dm.resource(step.Sound), step.Volume, TRUE);
            if (step.Done != null) to_chat(user, span_notice(step.Done));
        }
        finally { _builders.Remove(user.dm_id); }
    }

    private Step? Plan(mob_living user, obj_item item, int x, int y, int z, CeilingState old, bool under)
    {
        if (item == null) return Refuse(user, under ? "Use a floor tile or a crowbar on this ceiling." : "Use rods, floor tiles or a tool on the ceiling.");
        obj_item_stack stack = item as obj_item_stack;
        // (under a level above: open space or glass over the cell, which is that level's to build; and its plating, which only takes a tile from here)
        if (under && !old.Sealed) return Refuse(user, "There is no ceiling here: the floor of the level above is built from there.");
        if (under && old.Turf == CeilingState.Plating && item is not obj_item_stack_tile)
            return Refuse(user, "This plating is the floor of the level above: from here only a floor tile is laid under it.");
        if (!old.Sealed)
        {
            // tg: code/game/turfs/open/_open.dm:625  build_with_rods
            if (item is obj_item_stack_rods)
            {
                if (old.Lattice == CeilingLattice.Catwalk) return Refuse(user, "There is already a catwalk here!");
                if (Dm.num(stack.get_amount()) < 1)
                    return Refuse(user, old.Lattice == CeilingLattice.Lattice ? "You need two rods to build a catwalk!" : "You need one rod to build a lattice.");
                if (old.Lattice == CeilingLattice.Lattice)
                    return new Step(CeilingState.Of(CeilingLattice.Catwalk), 1, Done: "You construct a catwalk.", Sound: Genhit); // a new catwalk, whole
                if (!CanExtend(x, y, z)) return Refuse(user, "The ceiling needs a connection to a wall or column.");
                return new Step(CeilingState.Of(CeilingLattice.Lattice), 1, Done: "You construct a lattice.", Sound: Genhit, Extend: true);
            }
            // tg: code/game/turfs/open/_open.dm:654  build_with_floor_tiles (has_valid_support: a floor on a cardinal neighbour)
            if (ismetaltile(item))
            {
                bool support = old.Lattice != CeilingLattice.None || (HasValidSupport(x, y, z) && CanExtend(x, y, z));
                if (!support) { user.balloon_alert(user, I18n.T("needs support, place rods!")); return null; }
                if (Dm.num(stack.get_amount()) < 1) { user.balloon_alert(user, I18n.T("need a floor tile to build!")); return null; }
                return new Step(CeilingState.Of(CeilingState.Plating), 1, Sound: Genhit, Extend: old.Lattice == CeilingLattice.None);
            }
            // tg: code/game/objects/structures/lattice.dm:95  wirecutter_act, then atom_deconstruct's rods (number_of_mats)
            if (old.Lattice != CeilingLattice.None && Dm.eq(item.tool_behaviour, TOOL_WIRECUTTER))
            {
                Type lattice = old.Lattice == CeilingLattice.Catwalk ? typeof(obj_structure_lattice_catwalk) : typeof(obj_structure_lattice);
                return new Step(default, Refund: typeof(obj_item_stack_rods), RefundCount: Dm.num(Dm.initial(lattice, "number_of_mats")),
                    Start: I18n.T("Slicing [] joints ...", Dm.initial(lattice, "name")));
            }
            // tg: code/_onclick/item_attack.dm:281  attack_atom: anything else that bludgeons hits the lattice (CAN_BE_HIT)
            if (old.Lattice != CeilingLattice.None && !Dm.truthy(Dm.bitand(item.item_flags, NOBLUDGEON)))
                return new Step(old, Hit: true);
            return Refuse(user, "That material or tool cannot be used on this ceiling.");
        }
        turf_open_floor floor = Proto(old.Turf) as turf_open_floor;
        if (floor is turf_open_floor_plating plating)
        {
            // tg: code/game/turfs/open/floor/plating.dm:45  item_interaction
            bool holes = Dm.truthy(plating.attachment_holes);
            if (item is obj_item_stack_rods && holes)
            {
                if (old.Broken != 0) return Refuse(user, "Repair the plating first! Use a welding tool to fix the damage.");
                if (Dm.num(stack.get_amount()) < 2) return Refuse(user, "You need two rods to make a reinforced floor!");
                return new Step(CeilingState.Of(DmTypes.PathOf(typeof(turf_open_floor_engine))), 2, 3 * SECONDS,
                    Start: "You begin reinforcing the floor...", Done: "You reinforce the floor.", Sound: Deconstruct, Volume: 80);
            }
            if (item is obj_item_stack_tile tile)
            {
                if (old.Broken != 0) { user.balloon_alert(user, I18n.T("too damaged, use a welding tool!")); return null; }
                // tg: code/game/objects/items/stacks/tiles/tile_types.dm:76  place_tile
                if (tile.turf_type == null || !DmTypes.IsSubtypeOrSame(tile.turf_type, typeof(turf_open_floor)))
                    return Refuse(user, "This tile cannot be used on a ceiling.");
                CeilingState laid = CeilingState.Of(DmTypes.PathOf(tile.turf_type));
                // (under a level above glass would take the place of the plating, which is that level's floor)
                if (under && laid.IsGlass) return Refuse(user, "Glass takes the place of the plating: lay it from the level above.");
                return new Step(laid, 1, Sound: Genhit);
            }
            // shortcut: plasteel's reinforced plating is left out (its deconstruction is a sequence of tool states); port it with the RCD's ceilings
            if (item is obj_item_stack_sheet_mineral_plastitanium && holes)
            {
                if (old.Broken != 0) return Refuse(user, "Repair the plating first! Use a welding tool to fix the damage.");
                if (Dm.num(stack.get_amount()) < 1) return Refuse(user, "You are literally holding nothing.");
                return new Step(CeilingState.Of(DmTypes.PathOf(typeof(turf_open_floor_engine_insulation))), 1, 1.5f * SECONDS,
                    Start: "insulating flooring...", Done: "You insulate the floor.", Sound: Deconstruct, Volume: 80);
            }
            // tg: code/game/turfs/open/floor/plating.dm:141  welder_act
            if (old.Broken != 0 && Dm.eq(item.tool_behaviour, TOOL_WELDER))
                return new Step(old with { Broken = 0 }, Tool: true, Done: "You fix some dents on the broken plating.");
            return Refuse(user, "That material or tool cannot be used on this ceiling.");
        }
        if (floor is turf_open_floor_engine engine)
        {
            // tg: code/game/turfs/open/floor/reinforced_floor.dm:44  wrench_act (its crowbar_act does nothing)
            if (Dm.eq(item.tool_behaviour, TOOL_WRENCH))
                return new Step(CeilingState.Of(CeilingState.Plating), Delay: 3 * SECONDS, Tool: true, Refund: engine.floor_tile,
                    RefundCount: engine.floor_tile_amount, Start: "You begin removing rods...");
            return Refuse(user, "That material or tool cannot be used on this ceiling.");
        }
        // tg: code/game/turfs/open/floor.dm:144  crowbar_act -> pry_tile -> remove_tile (a broken floor drops no tile)
        if (floor != null && Dm.truthy(floor.overfloor_placed) && Dm.eq(item.tool_behaviour, TOOL_CROWBAR))
            return new Step(CeilingState.Of(CeilingState.Plating), Tool: true, Refund: old.Broken != 0 ? null : floor.floor_tile, RefundCount: 1,
                Done: old.Broken != 0 ? "You remove the broken plating." : "You remove the floor tile.");
        return Refuse(user, "That material or tool cannot be used on this ceiling.");
    }

    // tg: code/_onclick/item_attack.dm:281 attack_atom, then code/game/objects/objs.dm:81 attacked_by, on the ceiling's lattice
    private async DmTask Hit(mob_living user, obj_item item, turf target)
    {
        int x = (int)target.x, y = (int)target.y, z = (int)target.z;
        user.changeNext_move(item.attack_speed);
        await user.do_attack_animation(target);
        if (BlockOf(At(x, y, z)) is not obj lattice || !Dm.truthy(item.force)) return;
        object demo = item.get_demolition_modifier(lattice);
        float damage = DamageLattice(x, y, z, CALCULATE_FORCE(item, null) * Dm.num(demo), item.damtype, MELEE, Dm.num(item.armour_penetration), true);
        float picked = Dm.num(rand(max(length(item.attack_verb_simple), length(item.attack_verb_continuous))));
        object continuous = "attacks", simple = "attack";
        if (picked != 0 && length(item.attack_verb_continuous) >= picked) continuous = item.attack_verb_continuous[picked];
        if (picked != 0 && length(item.attack_verb_simple) >= picked) simple = item.attack_verb_simple[picked];
        if (Dm.num(demo) < 1)
        {
            simple = Dm.add("ineffectively ", simple);
            continuous = Dm.add("ineffectively ", continuous);
        }
        user.visible_message(
            span_danger(I18n.T("[] [] [] with [][]", user, continuous, lattice, item, damage > 0 ? "." : Dm.text(", []!", lattice.no_damage_feedback))),
            span_danger(I18n.T("You [] [] with [][]", simple, lattice, item, damage > 0 ? "." : Dm.text(", []!", lattice.no_damage_feedback))),
            null, COMBAT_MESSAGE_RANGE);
    }

    private static Step? Refuse(mob user, string message)
    {
        to_chat(user, span_warning(I18n.T(message)));
        return null;
    }

    // tg: code/game/turfs/open/_open.dm:671  has_valid_support: a floor on a cardinal neighbour (here: a ceiling with a floor block)
    private bool HasValidSupport(int x, int y, int z) => Neighbors(x, y, z).Any(p => At(p.X, p.Y, p.Z).Sealed);

    private static async DmTask<bool> Reachable(mob_living user, turf target)
        => !QDELETED(user) && !QDELETED(target) && !Dm.truthy(user.incapacitated) && await InReach(user, target);

    /// <summary>The range a ceiling click enforces (the hover's prompt follows it, DV589): the cell over the user's tile or the next one, on its level.</summary>
    public static async DmTask<bool> InReach(mob_living user, turf target)
        => Dm.eq(user.z, target.z) && Dm.num(get_dist(user, target)) <= 1 && Dm.truthy(await user.Adjacent(target));
}
