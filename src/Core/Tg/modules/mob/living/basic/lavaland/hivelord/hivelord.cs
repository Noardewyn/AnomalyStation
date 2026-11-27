// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/hivelord/hivelord.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Mob which retreats and spawns annoying sub-mobs to attack you
// tg: code/modules/mob/living/basic/lavaland/hivelord/hivelord.dm:2
public partial class mob_living_basic_mining_hivelord : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/lavaland/hivelord/hivelord.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_relay_attackers)));
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_item_organ_monster_core_regenerative_core)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_clickbox), Dm.named("icon_state", "hivelord"), Dm.named("max_scale", INFINITY), Dm.named("dead_state", "hivelord_dead"))); // They writhe so much.
        Waiting.NoSleep(AddComponent(typeof(datum_component_appearance_on_aggro), Dm.named("aggro_state", "hivelord_alert")));
        spawn_brood = Dm.@new<datum_action_cooldown_mob_cooldown_hivelord_spawn>(this);
        Waiting.NoSleep(spawn_brood.Grant(this));
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_TARGETED_ACTION, spawn_brood);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/hivelord/hivelord.dm:40
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref spawn_brood);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/hivelord/hivelord.dm:44
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        DmList safe_turfs = Dm.sub(RANGE_TURFS(1, this), get_turf(this)) as DmList;
        foreach (turf check_turf in Dm.each_cast<turf>(safe_turfs))
        {
            if (Dm.truthy(await check_turf.is_blocked_turf(exclude_mobs: TRUE)))
                Dm.remove(ref safe_turfs, check_turf);
        }

        turf our_turf = get_turf(this) as turf;
        for (float i = 1; i <= 3; i++)
        {
            if (!Dm.truthy(Dm.length(safe_turfs)))
                return dot;
            turf land_turf = pick_n_take(safe_turfs) as turf;
            obj_effect_temp_visual_hivebrood_spawn forecast = Dm.@new<obj_effect_temp_visual_hivebrood_spawn>(land_turf);
            await forecast.create_from(death_spawn_type, our_turf, Dm.@new<datum_callback>(this, nameof(complete_spawn), land_turf));
        }
        return dot;
    }

    /// Spawns a worm on the specified turf
    // tg: code/modules/mob/living/basic/lavaland/hivelord/hivelord.dm:60
    public virtual async DmTask complete_spawn(turf spawn_turf = null)
    {
        mob_living brood = Dm.@new(death_spawn_type, spawn_turf) as mob_living;
        // SET_FACTION_AND_ALLIES_FROM(brood, src) (__DEFINES/mobfactions.dm:119), inline
        brood.set_faction(get_faction());
        brood.set_allies(LAZYLISTDUPLICATE(allies));
        // tg passes the target as the key here, with no value: kept as tg has it
        (brood.ai_controller as datum_ai_controller)?.set_blackboard_key((ai_controller as datum_ai_controller).blackboard[BB_CURRENT_TARGET]);
        await brood.setDir(get_dir(this, spawn_turf));
    }

    // tg: code/modules/mob/living/basic/lavaland/hivelord/hivelord.dm:66
    public override async DmTask<object> RangedAttack(atom atom_target = null, object modifiers = null)
    {
        if (spawn_brood != null)
            await spawn_brood.Trigger(null, null, atom_target);
        return null;
    }
}

/// Attack worms spawned by the hivelord
// tg: code/modules/mob/living/basic/lavaland/hivelord/hivelord.dm:70
public partial class mob_living_basic_hivelord_brood : mob_living_basic
{
    // tg: code/modules/mob/living/basic/lavaland/hivelord/hivelord.dm:102
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_LAVA_IMMUNE, TRAIT_ASHSTORM_IMMUNE, TRAIT_PERMANENTLY_MORTAL), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_swarming)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_clickbox), Dm.named("icon_state", "hivelord"), Dm.named("max_scale", INFINITY)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(death)), 10 * SECONDS, file: "code/modules/mob/living/basic/lavaland/hivelord/hivelord.dm", line: 108);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/hivelord/hivelord.dm:110
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (!Dm.truthy(gibbed))
            Dm.@new<obj_effect_temp_visual_despawn_effect>(get_turf(this), /* copy_from = */ this);
        return await base.death(gibbed, __extra);
    }
}
