// Translated from tgstation (snapshot 1d99bd7f): code/datums/proximity_monitor/fields/timestop.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole, for the chrono suit's MOD timestopper (modules_timeline.dm): a chronofield that freezes everything
// that moves into its radius (the living are muted, stopped and held in stasis, projectiles and throws hang in the air, machines and
// structures only look frozen), drawn colour-inverted, while its immune caster is not; the timestopper's channelled kind lasts until
// the caster moves. The wizard's time stop (/obj/effect/timestop/magic, held off by antimagic) comes with it; its spell is the
// wizard's (D333). The var blocks are generated (D111).

// tg: code/datums/proximity_monitor/fields/timestop.dm:2
public partial class obj_effect_timestop : obj_effect
{
    // tg: code/datums/proximity_monitor/fields/timestop.dm:25
    [DmParams("mapload", "radius", "time", "immune_atoms", "start", "silent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object radius = Dm.arg(__extra, 0);
        object time = Dm.arg(__extra, 1);
        DmList immune_atoms = Dm.arg(__extra, 2) as DmList;
        object start = Dm.arg(__extra, 3) ?? TRUE;
        object silent = Dm.arg(__extra, 4) ?? FALSE;
        object dot = base.Initialize(mapload, __extra);
        if (time != null)
            duration = Dm.num(time);
        if (radius != null)
            freezerange = Dm.num(radius);
        if (Dm.truthy(silent))
        {
            hidden = TRUE;
            alpha = 0;
        }
        foreach (object innately_immune in Dm.each(immune_atoms))
            immune[innately_immune] = TRUE;
        if (Dm.truthy(start))
            Waiting.Detach(timestop());
        return dot;
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:39
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref chronofield);
        if (!Dm.truthy(hidden))
            playsound(this, Dm.resource("sound/effects/magic/timeparadox2.ogg"), 75, TRUE, frequency: -1); //reverse!
        return base.Destroy(force);
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:45
    public virtual async DmTask timestop()
    {
        target = get_turf(this);
        if (!Dm.truthy(hidden))
            playsound(this, Dm.resource("sound/effects/magic/timeparadox2.ogg"), 75, TRUE, -1);
        chronofield = await Dm.new_async<datum_proximity_monitor_advanced_timestop>(this, freezerange, TRUE, immune, antimagic_flags, channelled);
        if (!Dm.truthy(channelled))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), Dm.num(duration) > GC_FILTER_QUEUE ? WEAKREF(this) : this), duration, file: "code/datums/proximity_monitor/fields/timestop.dm", line: 51);
    }
}

// tg: code/datums/proximity_monitor/fields/timestop.dm:61
public partial class datum_proximity_monitor_advanced_timestop : datum_proximity_monitor_advanced
{
    // tg: code/datums/proximity_monitor/fields/timestop.dm:72
    public static DmList global_frozen_atoms => TgState.Current.datum_proximity_monitor_advanced_timestop__global_frozen_atoms ??= Dm.list();

    // tg: code/datums/proximity_monitor/fields/timestop.dm:74
    [DmParams("_host", "range", "_ignore_if_not_on_turf", "immune", "antimagic_flags", "channelled")]
    public override async DmTask New(params object[] __extra)
    {
        DmList immune = Dm.arg(__extra, 3) as DmList;
        object antimagic_flags = Dm.arg(__extra, 4);
        object channelled = Dm.arg(__extra, 5);
        await base.New(__extra);
        this.immune = immune;
        this.antimagic_flags = Dm.num(antimagic_flags);
        this.channelled = Dm.num(channelled);
        recalculate_field(full_recalc: TRUE);
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:81
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(unfreeze_all());
        if (Dm.truthy(channelled))
        {
            foreach (object atom in Dm.each(immune))
                UnregisterSignal(atom as datum, COMSIG_MOVABLE_MOVED);
        }
        return base.Destroy(force);
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:88
    public override void field_turf_crossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        Waiting.NoSleep(freeze_atom(movable));
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:91
    public virtual async DmTask<object> freeze_atom(atom_movable A = null)
    {
        if (Dm.truthy(global_frozen_atoms[A]) || !istype(A, typeof(atom_movable)))
            return FALSE;

        if (HAS_TRAIT(A, TRAIT_TIME_STOP_IMMUNE) || Dm.truthy((A as mob)?.can_block_magic(antimagic_flags)))
            immune[A] = TRUE;

        if (Dm.truthy(immune[A]))
        {
            if (Dm.truthy(channelled))
                RegisterSignal(A, COMSIG_MOVABLE_MOVED, nameof(atom_broke_channel), @override: TRUE);
            return FALSE;
        }

        bool frozen = true;
        if (isliving(A))
            await freeze_mob(A as mob_living);
        else if (isprojectile(A))
            freeze_projectile(A as obj_projectile);
        else if (ismecha(A))
            freeze_mecha(A as obj_vehicle_sealed_mecha);
        else if ((ismachinery(A) && !istype(A, typeof(obj_machinery_light))) || isstructure(A)) //Special exception for light fixtures since recoloring causes them to change light
            await freeze_structure(A as obj);
        else
            frozen = false;
        if (A.throwing != null)
        {
            freeze_throwing(A);
            frozen = true;
        }
        if (!frozen)
            return null;

        frozen_things[A] = A.move_resist;
        A.move_resist = INFINITY;
        global_frozen_atoms[A] = this;
        await into_the_negative_zone(A);
        RegisterSignal(A, COMSIG_MOVABLE_PRE_MOVE, nameof(unfreeze_atom));
        RegisterSignal(A, COMSIG_ITEM_PICKUP, nameof(unfreeze_atom));

        A.SEND_SIGNAL(COMSIG_ATOM_TIMESTOP_FREEZE, Dm.list(A, this));

        return TRUE;
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:131
    public virtual async DmTask unfreeze_all()
    {
        foreach (object i in Dm.each(frozen_things))
            unfreeze_atom(i as atom_movable);
        foreach (object T in Dm.each(frozen_turfs))
            await unfreeze_turf(T as turf);
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:137
    [SignalHandler]
    public virtual void unfreeze_atom(atom_movable A = null)
    {
        if (A.throwing != null)
            unfreeze_throwing(A);
        if (isliving(A))
            Waiting.NoSleep(unfreeze_mob(A as mob_living));
        else if (isprojectile(A))
            unfreeze_projectile(A as obj_projectile);
        else if (ismecha(A))
            unfreeze_mecha(A as obj_vehicle_sealed_mecha);

        UnregisterSignal(A, COMSIG_MOVABLE_PRE_MOVE);
        UnregisterSignal(A, COMSIG_ITEM_PICKUP);

        A.SEND_SIGNAL(COMSIG_ATOM_TIMESTOP_UNFREEZE, Dm.list(A, this));

        Waiting.NoSleep(escape_the_negative_zone(A));
        A.move_resist = Dm.num(frozen_things[A]);
        Dm.remove(ref frozen_things, A);
        Dm.remove(global_frozen_atoms, A);
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:159
    public virtual void freeze_mecha(obj_vehicle_sealed_mecha M = null)
    {
        M.completely_disabled = TRUE;
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:162
    public virtual void unfreeze_mecha(obj_vehicle_sealed_mecha M = null)
    {
        M.completely_disabled = FALSE;
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:165
    public virtual void freeze_throwing(atom_movable AM = null)
    {
        datum_thrownthing T = AM.throwing as datum_thrownthing;
        T.paused = TRUE;
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:169
    public virtual void unfreeze_throwing(atom_movable AM = null)
    {
        datum_thrownthing T = AM.throwing as datum_thrownthing;
        if (T != null)
            T.paused = FALSE;
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:174
    public virtual async DmTask freeze_turf(turf T = null)
    {
        await into_the_negative_zone(T);
        Dm.append(ref frozen_turfs, T);
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:178
    public virtual async DmTask unfreeze_turf(turf T = null)
    {
        await escape_the_negative_zone(T);
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:181
    public virtual async DmTask freeze_structure(obj O = null)
    {
        await into_the_negative_zone(O);
        Dm.append(ref frozen_structures, O);
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:185
    public virtual async DmTask unfreeze_structure(obj O = null)
    {
        await escape_the_negative_zone(O);
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:188
    public override void setup_field_turf(turf target = null)
    {
        base.setup_field_turf(target);
        foreach (object i in Dm.each(target.contents))
            Waiting.NoSleep(freeze_atom(i as atom_movable));
        Waiting.NoSleep(freeze_turf(target));
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:194
    public virtual void freeze_projectile(obj_projectile proj = null)
    {
        proj.paused = TRUE;
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:197
    public virtual void unfreeze_projectile(obj_projectile proj = null)
    {
        proj.paused = FALSE;
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:200
    public virtual async DmTask freeze_mob(mob_living victim = null)
    {
        Dm.append(ref frozen_mobs, victim);
        victim.add_traits(Dm.list(TRAIT_MUTE, TRAIT_EMOTEMUTE, TRAIT_INCAPACITATED, TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED, TRAIT_STASIS), TIMESTOP_TRAIT);
        GLOB.move_manager.stop_looping(victim); //stops them mid pathing even if they're stunimmune //This is really dumb
        if (isanimal(victim))
        {
            mob_living_simple_animal animal_victim = victim as mob_living_simple_animal;
            animal_victim.toggle_ai(AI_OFF);
            if (ishostile(victim))
            {
                mob_living_simple_animal_hostile hostile_victim = victim as mob_living_simple_animal_hostile;
                hostile_victim.LoseTarget();
            }
        }
        else if (isbasicmob(victim))
        {
            mob_living_basic basic_victim = victim as mob_living_basic;
            if (basic_victim.ai_controller is datum_ai_controller controller)
                await controller.force_ai_off();
        }
    }

    // tg: code/datums/proximity_monitor/fields/timestop.dm:214
    public virtual async DmTask unfreeze_mob(mob_living victim = null)
    {
        victim.remove_traits(Dm.list(TRAIT_MUTE, TRAIT_EMOTEMUTE, TRAIT_INCAPACITATED, TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED, TRAIT_STASIS), TIMESTOP_TRAIT);
        Dm.remove(ref frozen_mobs, victim);
        if (isanimal(victim))
        {
            mob_living_simple_animal animal_victim = victim as mob_living_simple_animal;
            animal_victim.toggle_ai(Dm.initial(animal_victim, (mob_living_simple_animal p) => p.AIStatus));
        }
        else if (isbasicmob(victim))
        {
            mob_living_basic basic_victim = victim as mob_living_basic;
            if (basic_victim.ai_controller is datum_ai_controller controller)
                await controller.clear_forced_off();
        }
    }

    //you don't look quite right, is something the matter?
    // tg: code/datums/proximity_monitor/fields/timestop.dm:225
    public virtual async DmTask into_the_negative_zone(atom A = null)
    {
        await A.add_atom_colour(COLOR_MATRIX_INVERT(), TEMPORARY_COLOUR_PRIORITY);
    }

    //let's put some colour back into your cheeks
    // tg: code/datums/proximity_monitor/fields/timestop.dm:229
    public virtual async DmTask escape_the_negative_zone(atom A = null)
    {
        await A.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY);
    }

    //signal fired when an immune atom moves in the time freeze zone
    // tg: code/datums/proximity_monitor/fields/timestop.dm:233
    [SignalHandler]
    public virtual void atom_broke_channel(datum source = null)
    {
        qdel(host);
    }
}

public sealed partial class TgState
{
    internal DmList datum_proximity_monitor_advanced_timestop__global_frozen_atoms;
}
