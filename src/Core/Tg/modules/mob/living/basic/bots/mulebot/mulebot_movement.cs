// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/mulebot/mulebot_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:1
public partial class mob_living_basic_bot_mulebot : mob_living_basic_bot
{
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_movement.dm:1
    public override async DmTask<object> MobBump(mob M = null) // called when the bot bumps into a mob
    {
        mob bumped_mob = M;
        if (mind != null || !isliving(bumped_mob)) //if there's a sentience controlling the bot, they aren't allowed to harm folks.
            return await base.MobBump(bumped_mob);
        mob_living bumped_living = (mob_living)bumped_mob;
        if (Dm.truthy((wires as datum_wires).is_cut(WIRE_AVOIDANCE))) // usually just bumps, but if the avoidance wire is cut, knocks them over.
        {
            if (iscyborg(bumped_living))
                visible_message(span_danger(Dm.text("[] bumps into []!", this, bumped_living)));
            else if (Dm.truthy(await bumped_living.Knockdown(8 * SECONDS)))
            {
                log_combat(this, bumped_living, "knocked down");
                visible_message(span_danger(Dm.text("[] knocks over []!", this, bumped_living)));
            }
        }
        return await base.MobBump(bumped_mob);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_movement.dm:13
    public override async DmTask on_bot_movement(atom_movable source = null, atom oldloc = null, object dir = null, object forced = null)
    {
        if (cell != null)
            await cell.use(cell_move_power_usage);
        set_cell_hud();

        if (buckled == null && pulledby == null && throwing == null && Dm.truthy(has_gravity()))
        {
            foreach (mob_living_carbon_human future_pancake in Dm.each<mob_living_carbon_human>(loc))
            {
                if (Dm.eq(future_pancake.body_position, LYING_DOWN))
                    await run_over(future_pancake);
            }
        }

        await base.on_bot_movement(source, oldloc, dir, forced);
    }

    ///Checks if the bot is on or if it has charge
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_movement.dm:25
    [SignalHandler]
    public virtual object on_pre_move()
    {
        if (!Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)))
            return COMPONENT_MOB_BOT_BLOCK_PRE_STEP;

        if ((cell != null && Dm.num(cell.charge) < cell_move_power_usage) || !Dm.truthy(has_power()))
        {
            Waiting.NoSleep(turn_off());
            return COMPONENT_MOB_BOT_BLOCK_PRE_STEP;
        }
        return null;
    }

    // when mulebot is in the same loc
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_movement.dm:36
    public virtual async DmTask run_over(mob_living_carbon_human crushed = null)
    {
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)) && !Dm.truthy((wires as datum_wires).is_cut(WIRE_AVOIDANCE)))
        {
            if (!Dm.truthy(has_status_effect(typeof(datum_status_effect_careful_driving))))
                crushed.visible_message(span_notice(Dm.text("[] slows down to avoid crushing [].", this, crushed)));
            await apply_status_effect(typeof(datum_status_effect_careful_driving));
            return; // Player mules must be emagged before they can trample
        }

        log_combat(this, crushed, "run over", addition: Dm.text("(DAMTYPE: [])", uppertext(BRUTE)));
        crushed.visible_message(
            span_danger(Dm.text("[] drives over []!", this, crushed)),
            span_userdanger(Dm.text("[] drives over you!", this)));

        playsound(this, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);

        float damage = Dm.num(rand(5, 15));
        DmList zone_damages = TgState.Current.mob_living_basic_bot_mulebot__zone_damages ??= Dm.list(
            Dm.kv(BODY_ZONE_HEAD, 2),
            Dm.kv(BODY_ZONE_CHEST, 2),
            Dm.kv(BODY_ZONE_L_LEG, 0.5f),
            Dm.kv(BODY_ZONE_R_LEG, 0.5f),
            Dm.kv(BODY_ZONE_L_ARM, 0.5f),
            Dm.kv(BODY_ZONE_R_ARM, 0.5f));
        foreach (object body_zone in Dm.each<object>(zone_damages))
            await crushed.apply_damage(Dm.num(zone_damages[body_zone]) * damage, BRUTE, body_zone, Dm.num(run_armor_check(body_zone, MELEE)));

        await add_mob_blood(crushed);

        turf below_us = get_turf(this);
        await below_us.add_mob_blood(crushed);

        await AddComponent(typeof(datum_component_blood_walk),
            Dm.named("blood_type", typeof(obj_effect_decal_cleanable_blood_tracks)), Dm.named("target_dir_change", TRUE), Dm.named("transfer_blood_dna", TRUE), Dm.named("max_blood", 4));
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList mob_living_basic_bot_mulebot__zone_damages;
}
