// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/boss/thing/thing_objects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:1
public partial class obj_structure_thing_boss_spike : obj_structure
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        turf our_turf = get_turf(this);
        // #ifndef UNIT_TESTS: the dust is made outside tg's unit tests; this port has no such build, so it is always made
        Dm.@new<obj_effect_temp_visual_mook_dust>(loc);
        bool hit_someone = false;
        foreach (atom_movable potential_target in Dm.each_cast<atom_movable>(our_turf))
        {
            if (ismegafauna(potential_target) || potential_target == this)
                continue;
            if (potential_target is mob_living living_victim)
            {
                hit_someone = true;
                Waiting.NoSleep(living_victim.apply_damage(40, damagetype: BRUTE, sharpness: SHARP_POINTY, wound_bonus: -10));
            }
            else if (Dm.truthy(potential_target.uses_integrity) && !Dm.truthy(Dm.bitand(potential_target.resistance_flags, INDESTRUCTIBLE)) && Dm.truthy(Dm.initial(potential_target, "density")) && !HAS_TRAIT(potential_target, TRAIT_UNDERFLOOR))
                Waiting.NoSleep(potential_target.take_damage(100, BRUTE));
        }
        if (hit_someone)
        {
            expiry_time /= 2;
            playsound(this, Dm.resource("sound/items/weapons/slice.ogg"), vol: 50, vary: TRUE, pressure_affected: FALSE);
        }
        else
            playsound(this, Dm.resource("sound/misc/splort.ogg"), vol: 25, vary: TRUE, pressure_affected: FALSE);

        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), expiry_time > GC_FILTER_QUEUE ? WEAKREF(this) : this), expiry_time, file: "code/modules/mob/living/basic/boss/thing/thing_objects.dm", line: 33); // QDEL_IN(src, expiry_time)
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:35
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        if (Dm.truthy(damage_amount))
            playsound(this, Dm.resource("sound/effects/blob/blobattack.ogg"), 50, TRUE);
        else
            playsound(this, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
        return null;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:41
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (!istype(mover, typeof(mob_living_basic_boss_thing)))
            return FALSE;
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:46
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        if (!istype(pass_info.requester_ref?.resolve(), typeof(mob_living_basic_boss_thing)))
            return FALSE;
        return base.CanAStarPass(to_dir, pass_info);
    }
}

// tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:51
public partial class obj_effect_temp_visual_telegraphing_exclamation : obj_effect_temp_visual_telegraphing
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:56
    [DmParams("mapload", "duration")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        if (duration != null)
            this.duration = Dm.num(duration);
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:61
public partial class obj_effect_temp_visual_telegraphing_exclamation_following : obj_effect_temp_visual_telegraphing_exclamation
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:61
    [DmParams("mapload", "duration", "following")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom_movable following = Dm.arg(__extra, 1) as atom_movable;
        object dot = base.Initialize(mapload, __extra);
        if (following == null)
            return INITIALIZE_HINT_QDEL;
        glide_size = following.glide_size;
        RegisterSignal(following, COMSIG_MOVABLE_MOVED, nameof(follow));
        return dot;
    }

    ///called when the thing we're following moves
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:69
    [SignalHandler]
    public virtual void follow(datum source = null)
    {
        Waiting.NoSleep(forceMove(get_turf(source)));
    }
}

// tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:74
public partial class obj_effect_temp_visual_telegraphing_exclamation_animated : obj_effect_temp_visual_telegraphing_exclamation
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:77
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        transform = Dm.matrix().Multiply(2);
        animate(this, Dm.named("alpha", 255), Dm.named("transform", Dm.matrix()), Dm.named("time", duration / 3));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:90
public partial class obj_effect_temp_visual_incoming_thing_acid : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:101
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        animate(this, Dm.named("pixel_z", 0), Dm.named("time", duration));
        _addtimer(Dm.@new<datum_callback>(this, nameof(make_acid)), 0.85f * SECONDS, file: "code/modules/mob/living/basic/boss/thing/thing_objects.dm", line: 104);
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:106
    public virtual void make_acid()
    {
        foreach (turf_open open in Dm.each<turf_open>(RANGE_TURFS(1, loc)))
            Dm.@new<obj_effect_thing_acid>(open);
    }
}

// tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:110
public partial class obj_effect_thing_acid : obj_effect
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:122  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_effect_thing_acid__Initialize__loc_connections;

    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:120
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), duration_time > GC_FILTER_QUEUE ? WEAKREF(this) : this), duration_time, file: "code/modules/mob/living/basic/boss/thing/thing_objects.dm", line: 126); // QDEL_IN(src, duration_time)
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:128
    [SignalHandler]
    public virtual void on_entered(datum source = null, mob_living victim = null)
    {
        if (victim == null || ismegafauna(victim))
            return;
        foreach (object zone in Dm.each(Dm.list(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG)))
        {
            object blocked = victim.run_armor_check(zone, ACID);
            Waiting.NoSleep(victim.apply_damage(25, BURN, def_zone: zone, blocked: Dm.num(blocked)));
        }
        to_chat(victim, span_userdanger(I18n.T("You are burnt by the acid!")));
        playsound(victim, Dm.resource("sound/effects/wounds/sizzle1.ogg"), vol: 50, vary: TRUE);
        qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:122  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_effect_thing_acid__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_thing_acid.on_entered)));
}

