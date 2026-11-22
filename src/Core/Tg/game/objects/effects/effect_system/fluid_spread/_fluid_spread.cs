// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/////////////////////////////////////////////
//// SMOKE SYSTEMS
/////////////////////////////////////////////

/**
 * A group of fluid objects.
 */
// tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:8
public partial class datum_fluid_group : datum
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:16
    [DmParams("target_size")]
    public override async DmTask New(params object[] __extra)
    {
        object target_size = Dm.arg<object>(__extra, 0);
        target_size ??= 0f;
        await base.New(__extra);
        this.nodes = Dm.list();
        this.target_size = target_size;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:21
    public override object Destroy(float? force = null)
    {
        QDEL_LAZYLIST(ref nodes);
        return base.Destroy(force);
    }

    /**
     * Adds a fluid node to this fluid group.
     *
     * Is a noop if the node is already in the group.
     * Removes the node from any other fluid groups it is in.
     * Syncs the group of the node with the group it is being added to (this one).
     * Increments the total size of the fluid group.
     *
     * Arguments:
     * - [node][/obj/effect/particle_effect/fluid]: The fluid node that is going to be added to this group.
     *
     * Returns:
     * - [TRUE]: If the node to be added is in this group by the end of the proc.
     * - [FALSE]: Otherwise.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:40
    public virtual object add_node(obj_effect_particle_effect_fluid node = null)
    {
        if (!istype(node, typeof(obj_effect_particle_effect_fluid)))
            throw Dm.CRASH(Dm.text("Attempted to add non-fluid node [] to a fluid group.", node == null ? "NULL" : node));
        if (QDELING(node))
            throw Dm.CRASH("Attempted to add qdeling node to a fluid group");

        if (node.group != null)
        {
            if (node.group == this)
                return TRUE;
            if (!Dm.truthy(node.group.remove_node(node)))
                return FALSE;
        }

        nodes.Add(node);
        node.group = this;
        total_size++;
        return TRUE;
    }

    /**
     * Removes a fluid node from this fluid group.
     *
     * Is a noop if the node is not in this group.
     * Nulls the nodes fluid group ref to sync it with its new state.
     * DOES NOT decrement the total size of the fluid group.
     *
     * Arguments:
     * - [node][/obj/effect/particle_effect/fluid]: The fluid node that is going to be removed from this group.
     *
     * Returns:
     * - [TRUE]: If the node to be removed is not in the group by the end of the proc.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:71
    public virtual object remove_node(obj_effect_particle_effect_fluid node = null)
    {
        if (node.group != this)
            return TRUE;

        nodes?.Remove(node);
        node.group = null;
        return TRUE; // Note: does not decrement total size since we don't want the group to expand again when it begins to dissipate or it will never stop.
    }
}

/**
 * A particle effect that belongs to a fluid group.
 */
// tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:83
public partial class obj_effect_particle_effect_fluid : obj_effect_particle_effect
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:92
    [DmParams("mapload", "group", "source")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_fluid_group group = Dm.arg<datum_fluid_group>(__extra, 0);
        obj_effect_particle_effect_fluid source = Dm.arg<obj_effect_particle_effect_fluid>(__extra, 1);
        // We don't pass on explosions. Don't wanna set off a chain reaction in our reagents
        flags_1 = Dm.num(Dm.bitor(flags_1, PREVENT_CONTENTS_EXPLOSION_1));
        object dot = base.Initialize(mapload, __extra);
        if (group == null)
            group = source?.group ?? Dm.@new<datum_fluid_group>();
        group.add_node(this);
        source?.transfer_fingerprints_to(this);
        return dot;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:101
    public override object Destroy(float? force = null)
    {
        group?.remove_node(this);
        return base.Destroy(force);
    }

    /**
     * Attempts to spread this fluid node to wherever it can spread.
     *
     * Exact results vary by subtype implementation.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:110
    public virtual async DmTask<object> spread(params object[] __extra)
    {
        throw Dm.CRASH("The base fluid spread proc is not implemented and should not be called. You called it.");
    }
}

/**
 * A factory which produces fluid groups.
 */
// tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:117
public partial class datum_effect_system_fluid_spread : datum_effect_system
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:123
    [DmParams("location", "range", "amount", "holder")]
    public override async DmTask New(params object[] __extra)
    {
        float? range = Dm.arg<float?>(__extra, 1);
        float? amount = Dm.arg<float?>(__extra, 2);
        atom holder = Dm.arg<atom>(__extra, 3);
        range ??= 1;
        await base.New(__extra);
        attach(holder);
        if (amount == null)
            amount = DIAMOND_AREA(Dm.num(range));
        this.amount = amount;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:130
    [DmParams("log")]
    public override async DmTask<object> start(params object[] __extra)
    {
        object log = Dm.arg<object>(__extra, 0) ?? FALSE;
        object location = (object)this.location ?? get_turf(holder as atom);
        obj_effect_particle_effect_fluid flood = Dm.@new(effect_type, location, Dm.@new<datum_fluid_group>(amount)) as obj_effect_particle_effect_fluid;
        if (Dm.truthy(log)) // Smoke is used as an aesthetic effect in a tonne of places and we don't want, say, a broken secway spamming admin chat.
            await help_out_the_admins(flood, holder as atom, location as atom);
        await flood.spread();
        return null;
    }

    /**
     * Handles logging the beginning of a fluid flood.
     *
     * Arguments:
     * - [flood][/obj/effect/particle_effect/fluid]: The first cell of the fluid flood.
     * - [holder][/atom]: What the flood originated from.
     * - [location][/atom]: Where the flood originated.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/_fluid_spread.dm:145
    public virtual async DmTask help_out_the_admins(obj_effect_particle_effect_fluid flood = null, atom holder = null, atom location = null)
    {
        string source_msg;
        string blame_msg = null;
        if (holder != null)
        {
            holder.transfer_fingerprints_to(flood); // This is important. If this doesn't exist thermobarics are annoying to adjudicate.
            source_msg = Dm.text("from inside of []", ismob(holder) ? ADMIN_LOOKUPFLW(holder) : ADMIN_VERBOSEJMP(holder));
            object lastkey = holder.fingerprintslast;
            if (Dm.truthy(lastkey))
            {
                var scapegoat = get_mob_by_key(lastkey) as mob;
                blame_msg = Dm.text(" last touched by []", ADMIN_LOOKUPFLW(scapegoat));
            }
            else
                blame_msg = " with no known fingerprints";
        }
        else
            source_msg = "with no known source";
        var fluid_area = get_area(location) as area;
        if (!(holder is obj_machinery_plumbing) && !Dm.truthy(Dm.bitand(fluid_area?.area_flags, QUIET_LOGS))) //excludes standard plumbing equipment as well as deathmatch from spamming admins with this shit
            message_admins(Dm.text("\\A [] flood started at [] [][].", flood, ADMIN_VERBOSEJMP(location), source_msg, blame_msg));
        log_game(Dm.text("\\A [] flood started at [] [][].", flood, AREACOORD(location), source_msg, blame_msg));
    }
}
