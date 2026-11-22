// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/mulebot/mulebot_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/mulebot/mulebot_ai.dm:1
public partial class datum_ai_controller_basic_controller_bot_mulebot : datum_ai_controller_basic_controller_bot
{
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_ai.dm:15
    public override object get_able_to_run()
    {
        mob_living_basic_bot_mulebot bot_pawn = (mob_living_basic_bot_mulebot)pawn;
        if (!Dm.truthy(bot_pawn.has_power()))
            return AI_UNABLE_TO_RUN;
        return base.get_able_to_run();
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_ai.dm:21
    public override void setup_able_to_run()
    {
        base.setup_able_to_run();
        mob_living_basic_bot my_bot = (mob_living_basic_bot)pawn;
        DmList wire_signals = TgState.Current.datum_ai_controller_basic_controller_bot_mulebot__wire_signals ??= Dm.list(
            COMSIG_MEND_WIRE(WIRE_POWER1), //this framework is insane
            COMSIG_MEND_WIRE(WIRE_POWER2),
            COMSIG_CUT_WIRE(WIRE_POWER1),
            COMSIG_CUT_WIRE(WIRE_POWER2));
        if (my_bot.wires != null) // a runtime in tg: the wires are set after the controller possesses the pawn in the mob's Initialize
            RegisterSignals(my_bot.wires as datum, wire_signals, nameof(update_able_to_run));
        DmList content_signals = TgState.Current.datum_ai_controller_basic_controller_bot_mulebot__content_signals ??= Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_EXITED);
        RegisterSignals(my_bot, content_signals, nameof(update_able_to_run));
    }
}

/// Loads or unloads cargo at the delivery beacon held in target_key, then heads home or idles.
// tg: code/modules/mob/living/basic/bots/mulebot/mulebot_ai.dm:37
public partial class datum_bt_node_ai_behavior_handle_delivery : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_ai.dm:41
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        obj_machinery_navbeacon beacon = controller.blackboard[target_key] as obj_machinery_navbeacon;
        if (QDELETED(beacon))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        mob_living_basic_bot_mulebot bot_pawn = (mob_living_basic_bot_mulebot)controller.pawn;

        object load_direction = beacon.codes[NAVBEACON_DELIVERY_DIRECTION]; // this will be the load/unload dir
        if (!Dm.truthy(load_direction))
            load_direction = beacon.dir; // fallback

        load_direction = text2num(load_direction);

        if (bot_pawn.load != null)
        {
            if (Dm.truthy(Dm.bitand(bot_pawn.mulebot_delivery_flags, MULEBOT_REPORT_DELIVERY_MODE)))
            {
                bot_pawn.radio_channel = RADIO_CHANNEL_SUPPLY; //Supply channel
                bot_pawn.buzz(MULEBOT_MOOD_CHIME);
                bot_pawn.speak(Dm.text("Destination [] reached. Unloading [].", RUNECHAT_BOLD(beacon.location), bot_pawn.load), bot_pawn.radio_channel);
                Waiting.NoSleep(bot_pawn.unload(load_direction));
            }
        }
        else
        {
            if (Dm.truthy(Dm.bitand(bot_pawn.mulebot_delivery_flags, MULEBOT_AUTO_PICKUP_MODE))) // find a crate
            {
                atom_movable atom_to_pick_up = null;
                turf load_turf = get_step(bot_pawn.loc, Dm.num(load_direction)) as turf;
                if (Dm.truthy((bot_pawn.wires as datum_wires).is_cut(WIRE_LOADCHECK))) // if hacked, load first unanchored thing we find
                {
                    foreach (atom_movable target_atom in Dm.each<atom_movable>(load_turf))
                    {
                        if (!Dm.truthy(target_atom.anchored))
                        {
                            atom_to_pick_up = target_atom;
                            break;
                        }
                    }
                }
                else // otherwise, look for crates only
                    atom_to_pick_up = Dm.locate<obj_structure_closet_crate>(load_turf);
                if (atom_to_pick_up != null && Dm.truthy(Waiting.NoSleep(atom_to_pick_up.Adjacent(bot_pawn))))
                {
                    Waiting.NoSleep(bot_pawn.load__proc(atom_to_pick_up));
                    if (Dm.truthy(Dm.bitand(bot_pawn.mulebot_delivery_flags, MULEBOT_REPORT_DELIVERY_MODE)))
                        bot_pawn.speak(Dm.text("Now loading [] at [].", bot_pawn.load, RUNECHAT_BOLD(get_area_name(bot_pawn))), bot_pawn.radio_channel);
                }
            }
        }

        if (Dm.truthy(Dm.bitand(bot_pawn.mulebot_delivery_flags, MULEBOT_RETURN_MODE)) && Dm.truthy(controller.blackboard[BB_MULEBOT_HOME_BEACON]) && !Dm.eq(controller.blackboard[BB_MULEBOT_HOME_BEACON], beacon.location))
        {
            bot_pawn.update_bot_mode(new_mode: BOT_GO_HOME);
            controller.clear_blackboard_key(BB_MULEBOT_TRAVEL_TARGET);
        }
        else
            Waiting.NoSleep(bot_pawn.bot_reset()); // otherwise go idle

        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList datum_ai_controller_basic_controller_bot_mulebot__wire_signals;
    internal DmList datum_ai_controller_basic_controller_bot_mulebot__content_signals;
}
