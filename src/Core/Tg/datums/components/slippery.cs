// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/slippery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 3 (wet floors and slipping): the whole file. The var block is generated; the two static connection lists are
// TgState fields (rules 10.6).

/**
 * # Slip behaviour component
 *
 * Add this component to an object to make it a slippery object, slippery objects make mobs that cross them fall over.
 * Items with this component that get picked up may give their parent mob the slip behaviour.
 *
 * Here is a simple example of adding the component behaviour to an object.area
 *
 *     AddComponent(/datum/component/slippery, 80, (NO_SLIP_WHEN_WALKING | SLIDE))
 *
 * This adds slippery behaviour to the parent atom, with a 80 decisecond (~8 seconds) knockdown
 * The lube flags control how the slip behaves, in this case, the mob wont slip if it's in walking mode (NO_SLIP_WHEN_WALKING)
 * and if they do slip, they will slide a few tiles (SLIDE)
 *
 *
 * This component has configurable behaviours, see the [Initialize proc for the argument listing][/datum/component/slippery/proc/Initialize].
 */
// tg: code/datums/components/slippery.dm:18
public partial class datum_component_slippery : datum_component
{
    ///what we give to connect_loc by default, makes slippable mobs moving over us slip
    // tg: code/datums/components/slippery.dm:41  var/static/list/default_connections
    public static DmList default_connections => TgState.Current.datum_component_slippery__default_connections;

    ///what we give to connect_loc if we're an item and get equipped by a mob, or if we're a mob. makes slippable mobs moving over the mob slip
    // tg: code/datums/components/slippery.dm:46  var/static/list/mob_connections
    public static DmList mob_connections => TgState.Current.datum_component_slippery__mob_connections;

    /**
     * Initialize the slippery component behaviour
     *
     * When applied to any atom in the game this will apply slipping behaviours to that atom
     *
     * Arguments:
     * * knockdown - Length of time the knockdown applies (Deciseconds)
     * * lube_flags - Controls the slip behaviour, they are listed starting [here][SLIDE]
     * * datum/callback/on_slip_callback - Callback to define further custom controls on when slipping is applied
     * * paralyze - length of time to paralyze the crossing mob for (Deciseconds)
     * * daze - length of time to daze the crossing mob for (Deciseconds), default 3 seconds
     * * force_drop - should the crossing mob drop items in its hands or not
     * * slot_whitelist - flags controlling where on a mob this item can be equipped to make the parent mob slippery full list [here][ITEM_SLOT_OCLOTHING]
     * * datum/callback/on_slip_callback - Callback to add custom behaviours as the crossing mob is slipped
     * * slip_on_damage - whether the component should cause slipping when the parent attacks the target
     */
    // tg: code/datums/components/slippery.dm:66
    [DmParams("knockdown", "lube_flags", "on_slip_callback", "paralyze", "daze", "force_drop", "slot_whitelist", "can_slip_callback", "slip_on_damage")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object knockdown = Dm.arg(__extra, 0);
        object lube_flags = Dm.arg(__extra, 1) ?? NONE;
        datum_callback on_slip_callback = Dm.arg(__extra, 2) as datum_callback;
        object paralyze = Dm.arg(__extra, 3);
        object daze = Dm.arg(__extra, 4) ?? 3 * SECONDS;
        object force_drop = Dm.arg(__extra, 5) ?? FALSE;
        object slot_whitelist = Dm.arg(__extra, 6);
        datum_callback can_slip_callback = Dm.arg(__extra, 7) as datum_callback;
        object slip_on_damage = Dm.arg(__extra, 8) ?? FALSE;
        this.knockdown_time = max(Dm.num(knockdown), 0);
        this.paralyze_time = max(Dm.num(paralyze), 0);
        this.daze_time = max(Dm.num(daze), 0);
        this.force_drop_items = Dm.num(force_drop);
        this.lube_flags = lube_flags;
        this.can_slip_callback = can_slip_callback;
        this.on_slip_callback = on_slip_callback;
        this.slip_on_damage = Dm.num(slip_on_damage);
        if (Dm.truthy(slot_whitelist))
            this.slot_whitelist = Dm.num(slot_whitelist);

        await add_connect_loc_behalf_to_parent();
        if (!ismovable(parent))
            RegisterSignal(parent, COMSIG_ATOM_ENTERED, nameof(Slip));
        else if (isitem(parent))
        {
            this.lube_flags = Dm.bitor(this.lube_flags, SLIPPERY_WHEN_LYING_DOWN);
            RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
            RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_drop));
            RegisterSignal(parent, COMSIG_ITEM_APPLY_FANTASY_BONUSES, nameof(apply_fantasy_bonuses));
            RegisterSignal(parent, COMSIG_ITEM_REMOVE_FANTASY_BONUSES, nameof(remove_fantasy_bonuses));
            RegisterSignal(parent, COMSIG_ITEM_AFTERATTACK, nameof(slip_on_afterattack));
            RegisterSignal(parent, COMSIG_MOVABLE_IMPACT, nameof(slip_on_throw_impact));
        }
        return null;
    }

    // tg: code/datums/components/slippery.dm:100
    public override object Destroy(float? force = null)
    {
        can_slip_callback = null;
        on_slip_callback = null;
        holder = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/slippery.dm:106
    [SignalHandler]
    public virtual void apply_fantasy_bonuses(obj_item source = null, object bonus = null)
    {
        // SIGNAL_HANDLER
        knockdown_time = Dm.num(source.modify_fantasy_variable("knockdown_time", knockdown_time, bonus));
        if (Dm.num(bonus) >= 5)
        {
            paralyze_time = Dm.num(source.modify_fantasy_variable("paralyze_time", paralyze_time, bonus));
            LAZYSET(ref source.fantasy_modifications, "lube_flags", lube_flags);
            lube_flags = Dm.bitor(lube_flags, SLIDE);
        }
        if (Dm.num(bonus) >= 10)
            lube_flags = Dm.bitor(lube_flags, Dm.bitor(GALOSHES_DONT_HELP, SLIP_WHEN_CRAWLING));
    }

    // tg: code/datums/components/slippery.dm:116
    [SignalHandler]
    public virtual void remove_fantasy_bonuses(obj_item source = null, object bonus = null)
    {
        // SIGNAL_HANDLER
        knockdown_time = Dm.num(source.reset_fantasy_variable("knockdown_time", knockdown_time));
        paralyze_time = Dm.num(source.reset_fantasy_variable("paralyze_time", paralyze_time));
        object previous_lube_flags = LAZYACCESS(source.fantasy_modifications, "lube_flags");
        LAZYREMOVE(ref source.fantasy_modifications, "lube_flags");
        if (previous_lube_flags != null)
            lube_flags = previous_lube_flags;
    }

    // tg: code/datums/components/slippery.dm:125
    public virtual async DmTask add_connect_loc_behalf_to_parent()
    {
        DmList connections_to_use = null;
        if (isliving(parent))
            connections_to_use = mob_connections;
        else if (ismovable(parent))
            connections_to_use = default_connections;
        if (Dm.truthy(connections_to_use))
            await AddComponent(typeof(datum_component_connect_loc_behalf), parent, connections_to_use);
    }

    // tg: code/datums/components/slippery.dm:134
    [DmParams("component", "i_am_original", "knockdown", "lube_flags", "on_slip_callback", "paralyze", "daze", "force_drop", "slot_whitelist", "can_slip_callback", "slip_on_damage")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        datum_component_slippery component = C as datum_component_slippery;
        object knockdown = Dm.arg(__extra, 0);
        object lube_flags = Dm.arg(__extra, 1) ?? NONE;
        datum_callback on_slip_callback = Dm.arg(__extra, 2) as datum_callback;
        object paralyze = Dm.arg(__extra, 3);
        object daze = Dm.arg(__extra, 4);
        object force_drop = Dm.arg(__extra, 5) ?? FALSE;
        object slot_whitelist = Dm.arg(__extra, 6);
        datum_callback can_slip_callback = Dm.arg(__extra, 7) as datum_callback;
        object slip_on_damage = Dm.arg(__extra, 8) ?? FALSE;
        if (Dm.truthy(component))
        {
            knockdown = component.knockdown_time;
            lube_flags = component.lube_flags;
            on_slip_callback = component.on_slip_callback;
            can_slip_callback = component.can_slip_callback;
            paralyze = component.paralyze_time;
            daze = component.daze_time;
            force_drop = component.force_drop_items;
            slot_whitelist = component.slot_whitelist;
        }

        this.knockdown_time = max(Dm.num(knockdown), 0);
        this.paralyze_time = max(Dm.num(paralyze), 0);
        this.daze_time = max(Dm.num(daze), 0);
        this.force_drop_items = Dm.num(force_drop);
        this.lube_flags = lube_flags;
        this.on_slip_callback = on_slip_callback;
        this.can_slip_callback = can_slip_callback;
        this.slip_on_damage = Dm.num(slip_on_damage);
        if (Dm.truthy(slot_whitelist))
            this.slot_whitelist = Dm.num(slot_whitelist);
    }

    /**
     * The proc that does the sliping. Invokes the slip callback we have set.
     *
     * Arguments
     * * source - the source of the signal
     * * arrived - the atom/movable that is being slipped.
     */
    // tg: code/datums/components/slippery.dm:174
    [SignalHandler]
    public virtual void Slip(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        // SIGNAL_HANDLER
        if (!isliving(arrived))
            return;
        if (Dm.truthy(Dm.bitand(lube_flags, SLIPPERY_TURF)))
        {
            turf turf = get_turf(source) as turf;
            if (HAS_TRAIT(turf, TRAIT_TURF_IGNORE_SLIPPERY))
                return;
        }
        mob_living victim = (mob_living)arrived;
        if (Dm.truthy(Dm.bitand(victim.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)) && !Dm.truthy(Dm.bitand(lube_flags, SLIP_IN_NOGRAV)))
            return;
        if (Dm.truthy(can_slip_callback) && !Dm.truthy(Waiting.NoSleep(can_slip_callback.Invoke(holder, victim))))
            return;
        if (Dm.truthy(Waiting.NoSleep(victim.slip(knockdown_time, parent as atom, lube_flags, paralyze_time, daze_time, force_drop_items))))
        {
            if (on_slip_callback != null)
                Waiting.NoSleep(on_slip_callback.Invoke(victim));
        }
    }

    /**
     * Gets called when COMSIG_ITEM_EQUIPPED is sent to parent.
     * This proc register slip signals to the equipper.
     * If we have a slot whitelist, we only register the signals if the slot is valid (ex: clown PDA only slips in ID or belt slot).
     *
     * Arguments
     * * source - the source of the signal
     * * equipper - the mob we're equipping the slippery thing to
     * * slot - the slot we're equipping the slippery thing to on the equipper.
     */
    // tg: code/datums/components/slippery.dm:200
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob equipper = null, object slot = null)
    {
        // SIGNAL_HANDLER

        if ((!Dm.truthy(slot) || Dm.truthy(Dm.bitand(slot, slot_whitelist))) && isliving(equipper))
        {
            holder = (mob_living)equipper;
            qdel(GetComponent(typeof(datum_component_connect_loc_behalf)));
            Waiting.NoSleep(AddComponent(typeof(datum_component_connect_loc_behalf), holder, mob_connections));
            RegisterSignal(holder, COMSIG_QDELETING, nameof(holder_deleted));
        }
    }

    /**
     * Detects if the holder mob is deleted.
     * If our holder mob is the holder set in this component, we null it.
     *
     * Arguments:
     * * source - the source of the signal
     * * possible_holder - the mob being deleted.
     */
    // tg: code/datums/components/slippery.dm:217
    [SignalHandler]
    public virtual void holder_deleted(datum source = null, datum possible_holder = null)
    {
        // SIGNAL_HANDLER

        if (possible_holder == holder)
            holder = null;
    }

    /**
     * Gets called when COMSIG_ITEM_DROPPED is sent to parent.
     * Makes our holder mob un-slippery.
     *
     * Arguments:
     * * source - the source of the signal
     * * user - the mob that was formerly wearing our slippery item.
     */
    // tg: code/datums/components/slippery.dm:231
    [SignalHandler]
    public virtual void on_drop(datum source = null, mob user = null)
    {
        // SIGNAL_HANDLER

        UnregisterSignal(user, COMSIG_QDELETING);

        qdel(GetComponent(typeof(datum_component_connect_loc_behalf)));
        Waiting.NoSleep(add_connect_loc_behalf_to_parent());

        holder = null;
    }

    /**
     * The slip proc, but for equipped items.
     * Slips the person who crossed us if we're lying down and unbuckled.
     *
     * Arguments:
     * * source - the source of the signal
     * * arrived - the atom/movable that slipped on us.
     */
    // tg: code/datums/components/slippery.dm:249
    [SignalHandler]
    public virtual void slip_on_mob(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        // SIGNAL_HANDLER

        mob_living living = (holder ?? parent) as mob_living;
        if (!Dm.truthy(Dm.bitand(lube_flags, SLIPPERY_WHEN_LYING_DOWN)) || (living.body_position == LYING_DOWN && !Dm.truthy(living.buckled)))
            Slip(source, arrived);
    }

    // tg: code/datums/components/slippery.dm:256
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        qdel(GetComponent(typeof(datum_component_connect_loc_behalf)));
    }

    // tg: code/datums/components/slippery.dm:260
    [SignalHandler]
    public virtual void slip_on_afterattack(datum source = null, atom target = null, mob user = null, params object[] __extra)
    {
        // SIGNAL_HANDLER

        if (!Dm.truthy(slip_on_damage) || !isliving(target))
            return;

        Slip(source, (atom_movable)target);
    }

    // tg: code/datums/components/slippery.dm:268
    [SignalHandler]
    public virtual void slip_on_throw_impact(datum source = null, atom hit_atom = null, datum_thrownthing throwing_datum = null, object caught = null)
    {
        // SIGNAL_HANDLER

        if (!Dm.truthy(slip_on_damage) || !isliving(hit_atom) || Dm.truthy(caught))
            return;

        Slip(source, (atom_movable)hit_atom);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/slippery.dm:41  var/static/list/default_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(Slip))
    internal DmList datum_component_slippery__default_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_slippery.Slip)));

    // tg: code/datums/components/slippery.dm:46  var/static/list/mob_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(slip_on_mob))
    internal DmList datum_component_slippery__mob_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_slippery.slip_on_mob)));
}
