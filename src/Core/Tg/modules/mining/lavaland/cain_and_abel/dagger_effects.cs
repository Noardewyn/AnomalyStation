// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

//effect when we're swinging wildly around
// tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:2
public partial class obj_effect_temp_visual_dagger_slash : obj_effect_temp_visual
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        animate(this, Dm.named("alpha", 0), Dm.named("time", 1.75f * SECONDS));
        return dot;
    }
}

//blade we hurl
// tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:32
public partial class obj_projectile_dagger : obj_projectile
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:47
    public virtual atom dagger_effects(atom target = null)
    {
        if (QDELETED(target))
            return null;
        turf target_turf = get_turf(target);
        if (isgroundlessturf(target_turf))
            return null;
        obj_effect_temp_visual_dagger_engraved engraved = Dm.@new(effect_left, target_turf) as obj_effect_temp_visual_dagger_engraved;
        ((atom)firer).Beam(engraved, icon_state: "chain", icon: Dm.resource("icons/obj/mining_zones/artefacts.dmi"), maxdistance: 9, layer: BELOW_MOB_LAYER);
        return engraved;
    }
}

// tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:60
public partial class obj_projectile_dagger_launch : obj_projectile_dagger
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:63
    public override atom dagger_effects(atom target = null)
    {
        atom dot = base.dagger_effects(target);
        if (dot == null)
            return dot;
        obj_effect_temp_visual_dagger_engraved_launch launching_dagger = dot as obj_effect_temp_visual_dagger_engraved_launch;
        Waiting.NoSleep(launching_dagger.launch(firer as mob_living));
        return dot;
    }
}

//the dagger thatll launch us toward it
// tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:89
public partial class obj_effect_temp_visual_dagger_engraved_launch : obj_effect_temp_visual_dagger_engraved
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:89
    public virtual async DmTask launch(mob_living firer = null)
    {
        await firer.throw_at(target: this, range: 9, speed: 1, spin: FALSE, gentle: TRUE, throw_datum_typepath: typeof(datum_thrownthing_dagger_launch));
    }
}

//throw datum the cain and abel applies
// tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:93
public partial class datum_thrownthing_dagger_launch : datum_thrownthing
{
    ///traits we apply to the user when being launched
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:97 (var/static/list/traits_on_launch)
    public static DmList traits_on_launch => TgState.Current.datum_thrownthing_dagger_launch__traits_on_launch ??= Dm.list(
        TRAIT_IMMOBILIZED,
        TRAIT_MOVE_FLOATING);

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:100
    [DmParams("thrownthing", "target", "init_dir", "maxrange", "speed", "thrower", "diagonals_first", "force", "gentle", "callback", "target_zone")]
    public override async DmTask New(params object[] __extra)
    {
        object thrownthing = Dm.arg(__extra, 0);
        await base.New(__extra);
        if (thrownthing == null)
            return;
        atom thrown_atom = thrownthing as atom;
        thrown_atom.add_traits(traits_on_launch, REF(this));
        Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(thrownthing as atom));
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:108
    public override void finalize(float? hit = null, object target = null)
    {
        if (thrownthing != null)
            Dm.@new<obj_effect_temp_visual_mook_dust>(get_turf(thrownthing));
        base.finalize(hit, target);
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:113
    public override object Destroy(float? force = null)
    {
        if (thrownthing != null)
            thrownthing.remove_traits(traits_on_launch, REF(this));
        obj_effect_temp_visual_dagger_engraved_launch target_dagger = initial_target?.resolve() as obj_effect_temp_visual_dagger_engraved_launch;
        if (target_dagger is obj_effect_temp_visual_dagger_engraved_launch)
            qdel(target_dagger);
        return base.Destroy(force);
    }
}

//dagger thatll spring up crystals
// tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:122
public partial class obj_effect_temp_visual_dagger_engraved_crystals : obj_effect_temp_visual_dagger_engraved
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:126
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        for (float index = 0, index__end = 2; index <= index__end; index++)
            _addtimer(Dm.@new<datum_callback>(this, nameof(generate_crystals), index), index * 0.5f * SECONDS, file: "code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm", line: 130);
        return dot;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:131
    public virtual void generate_crystals(object range = null)
    {
        if (Dm.num(range) == 0)
        {
            Dm.@new<obj_effect_temp_visual_dagger_crystal>(get_turf(this));
            return;
        }

        playsound(this, Dm.resource("sound/items/weapons/crystal_dagger_sound.ogg"), 60, vary: TRUE, pressure_affected: FALSE);
        DmList turfs_to_crystalize = border_diamond_range_turfs(this, range);
        foreach (object turf_to_crystalize in Dm.each(turfs_to_crystalize))
            Dm.@new<obj_effect_temp_visual_dagger_crystal>(turf_to_crystalize);
    }
}

//effect when our whisps hit something
// tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:142
public partial class obj_effect_temp_visual_wisp_explosion : obj_effect_temp_visual
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:151
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        playsound(get_turf(this), Dm.resource("sound/items/weapons/effects/blood_wisp_explode.ogg"), 60, vary: TRUE, pressure_affected: FALSE);
        return dot;
    }
}

//painful crystals to step on
// tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:156
public partial class obj_effect_temp_visual_dagger_crystal : obj_effect_temp_visual
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:166
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList loc_connections = TgState.Current.obj_effect_temp_visual_dagger_crystal__Initialize__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));

        foreach (mob_living victim in Dm.each<mob_living>(get_turf(this)))
        {
            if (Dm.num(victim.mob_size) < MOB_SIZE_LARGE)
                continue;
            Waiting.NoSleep(apply_crystal_effects(victim));
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(dissappear_gracefully)), Dm.num(duration) - 1 * SECONDS, file: "code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm", line: 180);
        return dot;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:179
    [SignalHandler]
    public virtual void on_entered(datum source = null, mob_living entered_living = null)
    {
        if (entered_living is mob_living)
            Waiting.NoSleep(apply_crystal_effects(entered_living));
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:184
    public virtual async DmTask apply_crystal_effects(mob_living victim = null)
    {
        await victim.apply_status_effect(typeof(datum_status_effect_dagger_stun));
        playsound(victim, Dm.resource("sound/items/weapons/bladeslice.ogg"), 50, FALSE);
        await victim.apply_damage(Dm.num(victim.mob_size) >= MOB_SIZE_LARGE ? applied_damage : applied_damage / 10, BRUTE);
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:189
    public virtual void dissappear_gracefully()
    {
        animate(this, Dm.named("alpha", 0), Dm.named("time", 0.9f * SECONDS));
    }
}

//wisp we hurl at monsters
// tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:193
public partial class obj_projectile_dagger_wisp : obj_projectile
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:203
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        transform = (transform as matrix).Scale(1, -1);
        return dot;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:207
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        Dm.@new<obj_effect_temp_visual_wisp_explosion>(get_turf(target));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:97 (var/static/list/traits_on_launch)
    internal DmList datum_thrownthing_dagger_launch__traits_on_launch;
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_effects.dm:171 (var/static/list/loc_connections)
    internal DmList obj_effect_temp_visual_dagger_crystal__Initialize__loc_connections;
}
