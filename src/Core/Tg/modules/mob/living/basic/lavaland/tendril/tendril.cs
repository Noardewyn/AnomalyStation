// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/tendril/tendril.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:5  GLOBAL_LIST_INIT(tendrils, list())
    public DmList tendrils;

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:5  (GLOBAL_MANAGED(tendrils, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltendrils()
    {
        tendrils = Dm.list();
        Dm.append(ref gvars_datum_init_order, "tendrils");
    }
}

// tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:7
public partial class mob_living_basic_mining_tendril : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:1-3 (file-local #defines)
    private const float HEARTBEAT_NORMAL = 1.8f * SECONDS;
    private const float HEARTBEAT_FAST = 1 * SECONDS;
    private const float HEARTBEAT_FRANTIC = 0.4f * SECONDS;

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:62 (var/static/list/abilities)
    public static DmList tendril_abilities => TgState.Current.mob_living_basic_mining_tendril__abilities ??= Dm.list(
        Dm.kv(typeof(datum_action_cooldown_mob_cooldown_projectile_attack_tendril_lash), BB_TENDRIL_LASH),
        Dm.kv(typeof(datum_action_cooldown_mob_cooldown_tendril_chaser), BB_TENDRIL_CHASER),
        Dm.kv(typeof(datum_action_cooldown_mob_cooldown_tendril_cross_spikes), BB_TENDRIL_SPIKES));

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:49
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(GLOB.tendrils, this);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_structure_closet_crate_necropolis_tendril)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_target_timer)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_gps), "Eerie Signal"));
        Waiting.NoSleep(AddComponent(typeof(datum_component_basic_mob_attack_telegraph), Dm.named("display_telegraph_overlay", FALSE), Dm.named("telegraph_duration", 0.4f * SECONDS)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_regenerator), Dm.named("regeneration_delay", 30 * SECONDS), Dm.named("brute_per_second", 20), Dm.named("outline_colour", COLOR_CULT_RED)));
        add_traits(Dm.list(TRAIT_BACKSTAB_IMMUNE, TRAIT_IMMOBILIZED), INNATE_TRAIT);

        grant_actions_by_list(tendril_abilities);

        tendril_melee = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_tendril_melee>(this);
        Waiting.NoSleep(tendril_melee.Grant(this));
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        Waiting.NoSleep(AddComponent(typeof(datum_component_revenge_ability), tendril_melee, Dm.named("targeting", GET_TARGETING_STRATEGY(controller.blackboard[BB_TARGETING_STRATEGY])), Dm.named("max_range", 2), Dm.named("target_self", TRUE)));

        soundloop = Dm.@new<datum_looping_sound_heartbeat>(this, FALSE);
        soundloop.mid_length = HEARTBEAT_NORMAL;
        soundloop.pressure_affected = FALSE;
        soundloop.start();
        update_appearance(UPDATE_OVERLAYS);

        turf our_turf = get_turf(this);
        foreach (turf rock__it in Dm.each<turf>(range(4, this)))
        {
            turf rock = rock__it;
            // TURF-IDENTITY: tg's our_turf is the cell, so the floor that replaced it once its own turf turned
            float dist = Dm.num(get_dist_euclidean(rock, (turf)turf.dm_identity(our_turf)));
            if (dist > 4.5f)
                continue;

            if (ismineralturf(rock))
            {
                Waiting.NoSleep(rock.ScrapeAway(null, CHANGETURF_IGNORE_AIR));
                rock = (turf)turf.dm_identity(rock); // TURF-IDENTITY: tg's rock names the cell, the floor the rock left from here on
            }

            if (rock is turf_open_misc_asteroid && prob(100 / sqrt(max(1, dist))))
            {
                Type original_type = rock.type;
                turf infected = Waiting.NoSleep(rock.ChangeTurf(typeof(turf_open_indestructible_necropolis), null, CHANGETURF_IGNORE_AIR)) as turf;
                // TURF-IDENTITY: tg keys the cell's turf, so the list holds the necropolis floor that replaced the rock
                infected_turfs[infected ?? (turf)turf.dm_identity(rock)] = original_type;
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:89
    public override object Destroy(float? force = null)
    {
        Dm.remove(GLOB.tendrils, this);
        QDEL_NULL(ref soundloop);
        QDEL_NULL(ref tendril_melee);
        infected_turfs.Cut();

        // SSachievements.achievements_enabled: the subsystem is not ported (no achievements), so it reads FALSE and tg's own early
        // return is taken; the tendril score and the exterminator achievement of the killers in view (:98-104) wait for the
        // achievements (D332).
        object achievements_enabled__gap = FALSE;
        if (!Dm.truthy(achievements_enabled__gap) || Dm.truthy(Dm.bitand(flags_1, ADMIN_SPAWNED_1)))
            return base.Destroy(force);

        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:107
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        turf our_turf = get_turf(this);
        playsound(our_turf, Dm.resource("sound/effects/tendril_destroyed.ogg"), 200, FALSE, 50, TRUE, TRUE);
        // Change our infected turfs back into regular ones, but only if they haven't been already altered
        foreach (object key in Dm.each(infected_turfs))
        {
            // TURF-IDENTITY: tg's key is the cell, so a floor replaced since is not the necropolis floor any more
            if (turf.dm_identity(key as datum) is not turf_open_indestructible_necropolis infected_turf || infected_turf != key)
                continue;
            float dist = Dm.num(get_dist_euclidean(our_turf, infected_turf));
            if (dist > 4.5f) // We got moved?
                continue;
            _addtimer(Dm.@new<datum_callback>(infected_turf, nameof(turf.ChangeTurf), infected_turfs[infected_turf], null, CHANGETURF_IGNORE_AIR), round(5 - dist, 0.5f) * 1 * SECONDS, file: "code/modules/mob/living/basic/lavaland/tendril/tendril.dm", line: 115);
        }
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:118
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.Deferred("code/modules/mob/living/basic/lavaland/tendril/tendril.dm:120", "update_overlays: the tendril's emissive and bloom overlays (DEVIATION DV12)");
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:123
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.Life(seconds_per_tick, __extra);
        update_heartbeat(); // Just a single math op unless we need updating so its fine to put it here
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:127
    public override async DmTask updatehealth()
    {
        await base.updatehealth();
        update_heartbeat();
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:131
    public virtual void update_heartbeat()
    {
        if (soundloop == null)
            return;

        float beat_rate = HEARTBEAT_NORMAL;
        if (Dm.truthy((ai_controller as datum_ai_controller)?.blackboard[BB_CURRENT_TARGET]))
            beat_rate = round(HEARTBEAT_FRANTIC + Dm.num(health) / Dm.num(maxHealth) * (HEARTBEAT_FAST - HEARTBEAT_FRANTIC), 0.05f * SECONDS);

        if (!Dm.eq(beat_rate, soundloop.mid_length))
            soundloop.set_mid_length(beat_rate);
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:142
    public override async DmTask do_attack_animation(atom attacked_atom = null, object visual_effect_icon = null, obj_item used_item = null, object no_effect = null, float? fov_effect = null, object item_animation_override = null)
    {
        fov_effect ??= TRUE;
        if (!Dm.truthy(no_effect) && (Dm.truthy(visual_effect_icon) || used_item != null))
            do_item_attack_animation(attacked_atom, visual_effect_icon, used_item, animation_type: item_animation_override);

        // Stabby visuals
        obj_effect_temp_visual_spike_stab stab = Dm.@new<obj_effect_temp_visual_spike_stab>(get_turf(this));
        float target_dir = Dm.num(get_dir(this, attacked_atom));
        stab.transform = matrix().Turn(Dm.num(dir2angle(target_dir)) + rand(-7, 7));
        if (Dm.truthy(Dm.bitand(target_dir, NORTH)))
            stab.pixel_z = 24;
        else if (Dm.truthy(Dm.bitand(target_dir, SOUTH)))
            stab.pixel_z = -24;
        if (Dm.truthy(Dm.bitand(target_dir, EAST)))
            stab.pixel_w = 24;
        else if (Dm.truthy(Dm.bitand(target_dir, WEST)))
            stab.pixel_w = -24;
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:164
    public virtual async DmTask snatch_react()
    {
        if (tendril_melee != null && Dm.truthy(await tendril_melee.IsAvailable()))
            await tendril_melee.stab(null, FALSE);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/lavaland/tendril/tendril.dm:62 (var/static/list/abilities)
    internal DmList mob_living_basic_mining_tendril__abilities;
}
