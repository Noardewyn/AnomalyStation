// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/electrified_buckle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps AS1, D333: the electric chair, a shock kit on a chair): the component that shocks the mobs buckled to its
// parent, on a loop or when its item (the shock kit) is pulsed, from the live cable under it, its area's power or a fixed
// damage; its overlays drawn over or under the buckled mob, the "electrified" name, the screwdriver that takes the item out.
// The var block is generated (D111).

/**
 * # electrified_buckle component:
 * attach it to any atom/movable that can be buckled to in order to have it shock mobs buckled to it. by default it shocks mobs buckled to parent every shock_loop_time.
 * the parent is supposed to define its behavior with arguments in AddComponent
*/
// tg: code/datums/components/electrified_buckle.dm:13
public partial class datum_component_electrified_buckle : datum_component
{
    // tg: code/datums/components/electrified_buckle.dm:1-2  the file-local defines (#undef at :244-245, rules 11.4)
    private const float ELECTRIC_BUCKLE_SHOCK_STRENGTH_DIVISOR = 5000;
    private const float ELECTRIC_BUCKLE_MINUMUM_POWERNET_STRENGTH = 10;

    /**
     * Initialize args:
     *
     * * input_requirements - bitflag that defines how the component is supposed to act, see __DEFINES/electrified_buckle.dm for the options. sets usage_flags
     * * input_item - if set to an item and input_requirements has SHOCK_REQUIREMENT_ITEM, moves that item inside parent and the component will delete itself if input_item no longer exists/moves out of parent. sets required_object
     * * overlays_to_add - pass in a list of images and the component will add them to parent as well as remove them in UnregisterFromParent(). sets requested_overlays
     * * override_buckle - if TRUE, sets parent.can_buckle = TRUE and resets it on UnregisterFromParent(), usually objects that have need to be overridden will look janky on buckle
     * * damage_on_shock - if SHOCK_REQUIREMENT_LIVE_CABLE is not set in input_requirements, then this is how much damage each shock does. sets shock_damage
     * * shock_immediately - if TRUE we will shock the buckled mob the first time we process after buckling, otherwise we wait until the cooldown has passed
     * * print_message - if TRUE we will show a message every time we electrocute someone during the shock loop
     * * signal_to_register_from_parent - if set, the component registers to listen for this signal targeting parent to manually shock. sets requested_signal_parent_emits
     * * area_power_channel - if SHOCK_REQUIREMENT_AREA_POWER is set in input_requirements, it will check this power channel in the area
     * * shock_flags - flags passed into electrocute_act when we zap someone
    */
    // tg: code/datums/components/electrified_buckle.dm:55
    [DmParams("input_requirements", "input_item", "overlays_to_add", "override_buckle", "damage_on_shock", "shock_immediately", "print_message", "signal_to_register_from_parent", "loop_length", "area_power_channel", "shock_flags")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object input_requirements = Dm.arg(__extra, 0);
        obj_item input_item = Dm.arg<obj_item>(__extra, 1);
        DmList overlays_to_add = Dm.arg<DmList>(__extra, 2);
        object override_buckle = Dm.arg(__extra, 3) ?? FALSE;
        object damage_on_shock = Dm.arg(__extra, 4) ?? 50;
        object shock_immediately = Dm.arg(__extra, 5) ?? FALSE;
        object print_message = Dm.arg(__extra, 6) ?? TRUE;
        object signal_to_register_from_parent = Dm.arg(__extra, 7);
        object loop_length = Dm.arg(__extra, 8);
        object area_power_channel = Dm.arg(__extra, 9);
        object shock_flags = Dm.arg(__extra, 10) ?? NONE;

        atom_movable parent_as_movable = parent as atom_movable;
        if (!istype(parent_as_movable, typeof(atom_movable)))
            return COMPONENT_INCOMPATIBLE;

        usage_flags = input_requirements;
        this.shock_immediately = Dm.num(shock_immediately);
        this.print_message = Dm.num(print_message);
        // tg assigns the argument, null when it is not passed (the var's AREA_USAGE_EQUIP default is lost); null reads as 0 here
        this.area_power_channel = Dm.num(area_power_channel);
        this.shock_flags = shock_flags;

        if (!Dm.truthy(parent_as_movable.can_buckle) && !Dm.truthy(override_buckle))
            return COMPONENT_INCOMPATIBLE;
        else if (Dm.truthy(override_buckle))
            parent_as_movable.can_buckle = TRUE;

        if (Dm.truthy(Dm.bitand(usage_flags, SHOCK_REQUIREMENT_ITEM)) && QDELETED(input_item))
            return COMPONENT_INCOMPATIBLE;

        if (HAS_TRAIT(parent_as_movable, TRAIT_ELECTRIFIED_BUCKLE))
            return COMPONENT_INCOMPATIBLE;

        if (Dm.truthy(Dm.bitand(usage_flags, SHOCK_REQUIREMENT_ITEM)))
        {
            required_object = input_item;
            await required_object.Move(parent_as_movable);
            RegisterSignal(required_object, COMSIG_QDELETING, nameof(delete_self));
            RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER), nameof(move_required_object_from_contents));

            if (Dm.truthy(Dm.bitand(usage_flags, SHOCK_REQUIREMENT_ON_SIGNAL_RECEIVED)))
            {
                shock_on_loop = FALSE;
                RegisterSignal(required_object, COMSIG_ASSEMBLY_PULSED, nameof(do_electrocution));
            }
            else if (Dm.truthy(Dm.bitand(usage_flags, SHOCK_REQUIREMENT_SIGNAL_RECEIVED_TOGGLE)))
                RegisterSignal(required_object, COMSIG_ASSEMBLY_PULSED, nameof(toggle_shock_loop));
        }

        if (Dm.truthy(Dm.bitand(usage_flags, SHOCK_REQUIREMENT_PARENT_MOB_ISALIVE)) && ismob(parent))
            RegisterSignal(parent, COMSIG_LIVING_DEATH, nameof(delete_self));

        RegisterSignal(parent, COMSIG_MOVABLE_BUCKLE, nameof(on_buckle));
        RegisterSignal(parent, COMSIG_MOVABLE_UNBUCKLE, nameof(on_unbuckle));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));

        ADD_TRAIT(parent_as_movable, TRAIT_ELECTRIFIED_BUCKLE, INNATE_TRAIT);

        if (Dm.truthy(signal_to_register_from_parent))
        {
            RegisterSignal(parent, signal_to_register_from_parent, nameof(do_electrocution));
            requested_signal_parent_emits = signal_to_register_from_parent;
        }

        if (Dm.truthy(overlays_to_add))
        {
            requested_overlays = overlays_to_add;
            parent_as_movable.add_overlay(requested_overlays);
        }

        parent_as_movable.name = I18n.T("electrified []", Dm.initial(parent_as_movable, (atom p) => p.name));

        shock_damage = Dm.num(damage_on_shock);

        if (Dm.truthy(loop_length))
            shock_loop_time = Dm.num(loop_length);

        if (Dm.truthy(parent_as_movable.has_buckled_mobs()))
        {
            foreach (mob_living possible_guinea_pig in Dm.each_cast<mob_living>(parent_as_movable.buckled_mobs))
            {
                if (Dm.truthy(on_buckle(this, possible_guinea_pig)))
                    break;
            }
        }
        return null;
    }

    // tg: code/datums/components/electrified_buckle.dm:120
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        atom_movable parent_as_movable = parent as atom_movable;

        parent_as_movable.cut_overlay(requested_overlays);
        parent_as_movable.name = Dm.initial(parent_as_movable, (atom p) => p.name);
        parent_as_movable.can_buckle = Dm.initial(parent_as_movable, (atom_movable p) => p.can_buckle);

        if (parent != null)
        {
            REMOVE_TRAIT(parent_as_movable, TRAIT_ELECTRIFIED_BUCKLE, INNATE_TRAIT);
            UnregisterSignal(parent, Dm.list(COMSIG_MOVABLE_BUCKLE, COMSIG_MOVABLE_UNBUCKLE, COMSIG_ATOM_UPDATE_OVERLAYS, COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER)));
            if (Dm.truthy(requested_signal_parent_emits))
                UnregisterSignal(parent, requested_signal_parent_emits);
        }

        if (required_object != null)
        {
            UnregisterSignal(required_object, Dm.list(COMSIG_QDELETING, COMSIG_ASSEMBLY_PULSED));
            if (parent_as_movable != null && Dm.@in(required_object, parent_as_movable.contents))
                await required_object.Move(parent_as_movable.loc);
        }

        required_object = null;
        STOP_PROCESSING(SSprocessing, this);
    }

    // tg: code/datums/components/electrified_buckle.dm:141
    [SignalHandler]
    public virtual void delete_self(params object[] __extra)
    {
        qdel(this);
    }

    // tg: code/datums/components/electrified_buckle.dm:145
    [SignalHandler]
    public virtual void move_required_object_from_contents(datum source = null, mob_living user = null, obj_item tool = null, object tool_type = null)
    {
        atom_movable parent_as_movable = parent as atom_movable;
        if (!QDELETED(parent_as_movable))
        {
            tool.play_tool_sound(parent_as_movable);
            Waiting.NoSleep(required_object.Move(parent_as_movable.loc));
        }
        qdel(this);
    }

    // tg: code/datums/components/electrified_buckle.dm:153
    [SignalHandler]
    public virtual object on_buckle(datum source = null, mob_living mob_to_buckle = null, object _force = null)
    {
        if (!istype(mob_to_buckle, typeof(mob_living)))
            return FALSE;

        // tg's Initialize passes the component itself as the source (electrified_buckle.dm:117), whose update_appearance is undefined:
        // that call is a runtime there, as the cast is here
        if (Dm.truthy(requested_overlays))
            ((atom)source).update_appearance();
        if (Dm.truthy(shock_immediately))
            do_electrocution();

        // COOLDOWN_START(src, electric_buckle_cooldown, shock_loop_time) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        electric_buckle_cooldown = world.time + shock_loop_time;
        if (!Dm.truthy(Dm.bitand(usage_flags, SHOCK_REQUIREMENT_ON_SIGNAL_RECEIVED)) && Dm.truthy(shock_on_loop))
            START_PROCESSING(SSprocessing, this);
        return TRUE;
    }

    // tg: code/datums/components/electrified_buckle.dm:167
    [SignalHandler]
    public virtual object on_unbuckle(atom source = null, mob_living unbuckled_mob = null, object _force = null)
    {
        if (!istype(unbuckled_mob, typeof(mob_living)))
            return FALSE;

        if (Dm.truthy(requested_overlays))
            source.update_appearance();
        return null;
    }

    // tg: code/datums/components/electrified_buckle.dm:175
    [SignalHandler]
    public virtual void on_update_overlays(atom_movable source = null, DmList overlays = null)
    {
        float overlay_layer = Dm.truthy(length(source.buckled_mobs)) ? ABOVE_MOB_LAYER : OBJ_LAYER;
        foreach (mutable_appearance electrified_overlay__it in Dm.each_cast<mutable_appearance>(requested_overlays))
        {
            object electrified_overlay = electrified_overlay__it;
            ((mutable_appearance)electrified_overlay).layer = overlay_layer;
            electrified_overlay = source.color_atom_overlay((mutable_appearance)electrified_overlay);
            Dm.append(ref overlays, electrified_overlay);
        }
    }

    // tg: code/datums/components/electrified_buckle.dm:183
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        atom_movable parent_as_movable = parent as atom_movable;
        if (QDELETED(parent_as_movable) || !Dm.truthy(parent_as_movable.has_buckled_mobs()))
            return PROCESS_KILL;

        if (!Dm.truthy(shock_on_loop))
            return PROCESS_KILL;

        // COOLDOWN_FINISHED(src, electric_buckle_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(electric_buckle_cooldown <= world.time))
            return null;

        // COOLDOWN_START(src, electric_buckle_cooldown, shock_loop_time), inline
        electric_buckle_cooldown = world.time + shock_loop_time;
        do_electrocution();

        if (Dm.truthy(print_message))
            parent_as_movable.visible_message(span_danger(I18n.T("[] delivers a powerful shock!", parent_as_movable)), span_hear(I18n.T("You hear a deep sharp shock!")));
        return null;
    }

    // tg: code/datums/components/electrified_buckle.dm:200
    [SignalHandler]
    public virtual void do_electrocution(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(usage_flags, SHOCK_REQUIREMENT_ITEM)) && QDELETED(required_object))
            return;

        atom_movable parent_as_movable = parent as atom_movable;

        if (Dm.truthy(Dm.bitand(usage_flags, SHOCK_REQUIREMENT_LIVE_CABLE)))
        {
            turf our_turf = get_turf(parent_as_movable);
            obj_structure_cable live_cable = our_turf.get_cable_node() as obj_structure_cable;
            if (live_cable == null || live_cable.powernet == null || live_cable.powernet.avail < ELECTRIC_BUCKLE_MINUMUM_POWERNET_STRENGTH)
                return;

            foreach (mob_living guinea_pig in Dm.each_cast<mob_living>(parent_as_movable.buckled_mobs))
            {
                float shock_damage = round(live_cable.powernet.avail / ELECTRIC_BUCKLE_SHOCK_STRENGTH_DIVISOR);
                Waiting.NoSleep(guinea_pig.electrocute_act(shock_damage, parent_as_movable, flags: Dm.num_or_null(shock_flags)));
            }

            return;
        }

        if (Dm.truthy(Dm.bitand(usage_flags, SHOCK_REQUIREMENT_AREA_POWER)))
        {
            area our_area = get_area(parent) as area;
            if (!Dm.truthy(our_area.powered(area_power_channel)))
                return;

            foreach (mob_living guinea_pig in Dm.each_cast<mob_living>(parent_as_movable.buckled_mobs))
                Waiting.NoSleep(guinea_pig.electrocute_act(shock_damage, parent_as_movable, flags: Dm.num_or_null(shock_flags)));

            return;
        }

        foreach (mob_living guinea_pig in Dm.each_cast<mob_living>(parent_as_movable.buckled_mobs))
            Waiting.NoSleep(guinea_pig.electrocute_act(shock_damage, parent_as_movable, flags: Dm.num_or_null(shock_flags)));
    }

    // tg: code/datums/components/electrified_buckle.dm:230
    [SignalHandler]
    public virtual void toggle_shock_loop(params object[] __extra)
    {
        atom_movable parent_as_movable = parent as atom_movable;
        if (Dm.truthy(shock_on_loop))
        {
            shock_on_loop = FALSE;
            STOP_PROCESSING(SSprocessing, this);
            parent_as_movable.visible_message(span_notice(I18n.T("\\The [] emits a snap as its circuit opens, making it safe for now.", parent_as_movable)));
        }
        else
        {
            shock_on_loop = TRUE;
            START_PROCESSING(SSprocessing, this);
            parent_as_movable.visible_message(span_notice(I18n.T("You hear the sound of an electric circuit closing coming from \\the []!", parent_as_movable)));
        }
    }
}
