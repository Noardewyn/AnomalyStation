// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/squeak.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice F (bananium's squeak, reached by a dimensional anomaly's clown theme): the whole file. The var block is generated; the
// two static lists are TgState fields (rules 10.6).

// tg: code/datums/components/squeak.dm:1
public partial class datum_component_squeak : datum_component
{
    // tg: code/datums/components/squeak.dm:2  var/static/list/default_squeak_sounds
    public static DmList default_squeak_sounds => TgState.Current.datum_component_squeak__default_squeak_sounds;

    ///what we set connect_loc to if parent is an item
    // tg: code/datums/components/squeak.dm:25  var/static/list/item_connections
    public static DmList item_connections => TgState.Current.datum_component_squeak__item_connections;

    // tg: code/datums/components/squeak.dm:30
    [DmParams("custom_sounds", "volume_override", "chance_override", "step_delay_override", "use_delay_override", "extrarange", "falloff_exponent", "fallof_distance")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList custom_sounds = Dm.arg(__extra, 0) as DmList;
        object volume_override = Dm.arg(__extra, 1);
        object chance_override = Dm.arg(__extra, 2);
        object step_delay_override = Dm.arg(__extra, 3);
        object use_delay_override = Dm.arg(__extra, 4);
        object extrarange = Dm.arg(__extra, 5);
        object falloff_exponent = Dm.arg(__extra, 6);
        object fallof_distance = Dm.arg(__extra, 7);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        RegisterSignals(parent, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_BLOB_ACT, COMSIG_ATOM_HULK_ATTACK, COMSIG_ATOM_ATTACKBY), nameof(play_squeak));
        if (ismovable(parent))
        {
            RegisterSignals(parent, Dm.list(COMSIG_MOVABLE_BUMP, COMSIG_MOVABLE_IMPACT, COMSIG_PROJECTILE_BEFORE_FIRE), nameof(play_squeak));

            await AddComponent(typeof(datum_component_connect_loc_behalf), parent, item_connections);
            RegisterSignal(parent, COMSIG_MOVABLE_DISPOSING, nameof(disposing_react));
            if (isitem(parent))
            {
                RegisterSignals(parent, Dm.list(COMSIG_ITEM_ATTACK, COMSIG_ITEM_ATTACK_ATOM, COMSIG_ITEM_HIT_REACT), nameof(play_squeak));
                RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(use_squeak));
                RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
                RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_drop));
                if (isclothing(parent))
                    RegisterSignal(parent, COMSIG_SHOES_STEP_ACTION, nameof(step_squeak));
            }
            else if (isstructure(parent))
                RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(use_squeak));
        }

        if (istype(parent, typeof(obj_item_organ_liver)))
            // Liver squeaking is depending on them functioning like a clown's liver
            RegisterSignal(parent, SIGNAL_REMOVETRAIT(TRAIT_COMEDY_METABOLISM), nameof(on_comedy_metabolism_removal));

        override_squeak_sounds = custom_sounds;
        if (Dm.truthy(chance_override))
            squeak_chance = Dm.num(chance_override);
        if (Dm.truthy(volume_override))
            volume = Dm.num(volume_override);
        if (Dm.isnum(step_delay_override))
            step_delay = Dm.num(step_delay_override);
        if (Dm.isnum(use_delay_override))
            use_delay = Dm.num(use_delay_override);
        if (Dm.isnum(extrarange))
            sound_extra_range = Dm.num(extrarange);
        if (Dm.isnum(falloff_exponent))
            sound_falloff_exponent = Dm.num(falloff_exponent);
        if (Dm.isnum(fallof_distance))
            sound_falloff_distance = Dm.num(fallof_distance);
        return null;
    }

    // tg: code/datums/components/squeak.dm:69
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        qdel(GetComponent(typeof(datum_component_connect_loc_behalf)));
    }

    // tg: code/datums/components/squeak.dm:73
    [SignalHandler]
    public virtual void play_squeak()
    {
        if (prob(squeak_chance))
        {
            if (!Dm.truthy(override_squeak_sounds))
                playsound(parent as atom, pick_weight(default_squeak_sounds), volume, TRUE, sound_extra_range, sound_falloff_exponent, falloff_distance: sound_falloff_distance);
            else
                playsound(parent as atom, pick_weight(override_squeak_sounds), volume, TRUE, sound_extra_range, sound_falloff_exponent, falloff_distance: sound_falloff_distance);
        }
    }

    // tg: code/datums/components/squeak.dm:82
    [SignalHandler]
    public virtual void step_squeak(obj_item_clothing source = null)
    {
        mob_living_carbon_human owner = source.loc as mob_living_carbon_human;
        if (Dm.truthy(CHECK_MOVE_LOOP_FLAGS(owner, MOVEMENT_LOOP_OUTSIDE_CONTROL)))
            return;
        if (Dm.truthy(owner.buckled) || Dm.truthy(owner.throwing) || Dm.truthy(Dm.bitand(owner.movement_type, Dm.bitor(VENTCRAWLING, FLYING))) || HAS_TRAIT(owner, TRAIT_IMMOBILIZED))
            return;
        if (steps > step_delay)
        {
            play_squeak();
            steps = 0;
        }
        else
            steps++;
    }

    // tg: code/datums/components/squeak.dm:96
    [SignalHandler]
    public virtual void play_squeak_crossed(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (isitem(arrived))
        {
            obj_item I = (obj_item)arrived;
            if (Dm.truthy(Dm.bitand(I.item_flags, ABSTRACT)))
                return;
        }
        if (Dm.truthy(Dm.bitand(arrived.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)) || !Dm.truthy(arrived.has_gravity()))
            return;
        if (ismob(arrived) && !Dm.truthy(arrived.density)) // Prevents 10 overlapping mice from making an unholy sound while moving
            return;
        if (isliving(arrived))
        {
            mob_living living_arrived = (mob_living)arrived;
            if (Dm.lt(living_arrived.mob_size, MOB_SIZE_HUMAN))
                return;
        }
        atom current_parent = parent as atom;
        if (isturf(current_parent?.loc))
            play_squeak();
    }

    // tg: code/datums/components/squeak.dm:115
    [SignalHandler]
    public virtual void use_squeak()
    {
        // COOLDOWN_FINISHED(src, spam_cooldown) and COOLDOWN_START(src, spam_cooldown, use_delay) (__DEFINES/cooldowns.dm), inline (rules 11.1 (a))
        if (spam_cooldown <= world.time)
        {
            spam_cooldown = world.time + use_delay;
            play_squeak();
        }
    }

    // tg: code/datums/components/squeak.dm:122
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob equipper = null, object slot = null)
    {
        holder = equipper;
        RegisterSignal(holder, COMSIG_MOVABLE_DISPOSING, nameof(disposing_react), @override: TRUE);
        RegisterSignal(holder, COMSIG_QDELETING, nameof(holder_deleted), @override: TRUE);
        //override for the preqdeleted is necessary because putting parent in hands sends the signal that this proc is registered towards,
        //so putting an object in hands and then equipping the item on a clothing slot (without dropping it first)
        //will always runtime without override = TRUE
    }

    // tg: code/datums/components/squeak.dm:131
    [SignalHandler]
    public virtual void on_drop(datum source = null, mob user = null)
    {
        UnregisterSignal(user, COMSIG_MOVABLE_DISPOSING);
        UnregisterSignal(user, COMSIG_QDELETING);
        holder = null;
    }

    ///just gets rid of the reference to holder in the case that they're qdeleted
    // tg: code/datums/components/squeak.dm:138
    [SignalHandler]
    public virtual void holder_deleted(datum source = null, datum possible_holder = null)
    {
        if (possible_holder == holder)
            holder = null;
    }

    // Disposal pipes related shits
    // tg: code/datums/components/squeak.dm:144
    [SignalHandler]
    public virtual void disposing_react(datum source = null, obj_structure_disposalholder disposal_holder = null, obj_machinery_disposal disposal_source = null)
    {
        //We don't need to worry about unregistering this signal as it will happen for us automatically when the holder is qdeleted
        RegisterSignal(disposal_holder, COMSIG_ATOM_DIR_CHANGE, nameof(holder_dir_change));
    }

    // tg: code/datums/components/squeak.dm:150
    [SignalHandler]
    public virtual void holder_dir_change(datum source = null, object old_dir = null, object new_dir = null)
    {
        //If the dir changes it means we're going through a bend in the pipes, let's pretend we bumped the wall
        if (!Dm.eq(old_dir, new_dir))
            play_squeak();
    }

    // tg: code/datums/components/squeak.dm:157
    [SignalHandler]
    public virtual void on_comedy_metabolism_removal(datum source = null, object trait = null)
    {
        qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/squeak.dm:2  var/static/list/default_squeak_sounds = list('sound/items/toy_squeak/toysqueak1.ogg'=1, ...)
    internal DmList datum_component_squeak__default_squeak_sounds = Dm.list(
        Dm.kv(Dm.resource("sound/items/toy_squeak/toysqueak1.ogg"), 1),
        Dm.kv(Dm.resource("sound/items/toy_squeak/toysqueak2.ogg"), 1),
        Dm.kv(Dm.resource("sound/items/toy_squeak/toysqueak3.ogg"), 1));

    // tg: code/datums/components/squeak.dm:25  var/static/list/item_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(play_squeak_crossed))
    internal DmList datum_component_squeak__item_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_squeak.play_squeak_crossed)));
}
