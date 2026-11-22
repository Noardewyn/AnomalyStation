// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/bot_hud.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/_bots.dm:11
public partial class mob_living_basic_bot : mob_living_basic
{
    // tg: code/modules/mob/living/basic/bots/bot_hud.dm:1
    public virtual void diag_hud_set_bothealth()
    {
        set_hud_image_state(DIAG_HUD, hud_state: Dm.text("huddiag[]", RoundDiagBar(Dm.num(health) / Dm.num(maxHealth))));
    }

    // tg: code/modules/mob/living/basic/bots/bot_hud.dm:4
    public virtual void diag_hud_set_botstat() //On (With wireless on or off), Off, EMP'ed
    {
        if (Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)))
        {
            set_hud_image_state(DIAG_STAT_HUD, hud_state: "hudstat");
            return;
        }

        if (IS_UNCONSCIOUS_OR_CRIT(this))
        {
            set_hud_image_state(DIAG_STAT_HUD, hud_state: "hudoffline");
            return;
        }

        set_hud_image_state(DIAG_STAT_HUD, hud_state: "huddead2");
    }

    // tg: code/modules/mob/living/basic/bots/bot_hud.dm:15
    public virtual void diag_hud_set_botmode() //Shows a bot's current operation
    {
        if (client != null) //If the bot is player controlled, it will not be following mode logic!
        {
            set_hud_image_state(DIAG_BOT_HUD, hud_state: "hudsentient");
            return;
        }

        switch (mode)
        {
            case BOT_SUMMON: case BOT_RESPONDING: //Responding to PDA or AI summons
                set_hud_image_state(DIAG_BOT_HUD, hud_state: "hudcalled");
                break;
            case BOT_CLEANING: case BOT_HEALING: //Cleanbot cleaning, Floorbot fixing, or Medibot Healing
                set_hud_image_state(DIAG_BOT_HUD, hud_state: "hudworking");
                break;
            case BOT_PATROL: case BOT_START_PATROL: //Patrol mode
                set_hud_image_state(DIAG_BOT_HUD, hud_state: "hudpatrol");
                break;
            case BOT_PREP_ARREST: case BOT_ARREST: case BOT_HUNT: //STOP RIGHT THERE, CRIMINAL SCUM!
                set_hud_image_state(DIAG_BOT_HUD, hud_state: "hudalert");
                break;
            case BOT_MOVING: case BOT_DELIVER: case BOT_GO_HOME: case BOT_NAV: //Moving to target for normal bots, moving to deliver or go home for MULES.
                set_hud_image_state(DIAG_BOT_HUD, hud_state: "hudmove");
                break;
            default:
                set_hud_image_state(DIAG_BOT_HUD, hud_state: "");
                break;
        }
    }

    ///proc that handles drawing and transforming the bot's path onto diagnostic huds
    // tg: code/modules/mob/living/basic/bots/bot_hud.dm:35
    [SignalHandler]
    public virtual void generate_bot_path(datum_move_loop_has_target_jps source = null, DmList path = null)
    {
        UnregisterSignal(this, COMSIG_MOVELOOP_JPS_FINISHED_PATHING);

        datum_ai_controller controller = ai_controller as datum_ai_controller;
        if (controller == null)
            return;

        if (!Dm.truthy(length(path)))
            return;

        atom move_target = path[path.len] as atom;
        if (move_target != get_turf(controller.blackboard[BB_BEACON_TARGET] as atom))
            return;

        //Removes path images and handles removing hud client images
        clear_path_hud();

        DmList path_huds_watching_me = Dm.list(GLOB.huds[DATA_HUD_DIAGNOSTIC], GLOB.huds[DATA_HUD_BOT_PATH]);

        DmList path_images = active_hud_list[DIAG_PATH_HUD] as DmList;
        LAZYCLEARLIST(path_images);

        DmList our_path = source.movement_path;
        if (!Dm.truthy(length(our_path)))
            return;

        for (int index = 1; index <= our_path.len; index++)
        {
            if (index == 1 || index == our_path.len)
                continue;
            turf current_turf = our_path[index] as turf;
            turf previous_turf = our_path[index - 1] as turf;

            turf next_turf = our_path[index + 1] as turf;
            float next_direction = Dm.num(get_dir(previous_turf, next_turf));
            float previous_direction = Dm.num(get_dir(current_turf, previous_turf));

            image path_display = image(icon: path_image_icon, loc: current_turf, icon_state: path_image_icon_state, layer: BOT_PATH_LAYER, dir: next_direction);

            SET_PLANE(path_display, GAME_PLANE, current_turf);

            if (Dm.truthy(ISDIAGONALDIR(next_direction)) && Dm.truthy(Dm.bitand(previous_direction, Dm.bitor(NORTH, SOUTH))))
            {
                float turn_value = (next_direction == SOUTHWEST || next_direction == NORTHEAST) ? 90 : -90;
                path_display.transform = ((matrix)path_display.transform).Turn(turn_value);
                path_display.transform = ((matrix)path_display.transform).Scale(1, -1);
            }

            path_display.color = path_image_color;
            Dm.append(ref path_images, path_display);
            current_pathed_turfs[current_turf] = path_display;
        }

        foreach (datum_atom_hud hud in Dm.each_cast<datum_atom_hud>(path_huds_watching_me))
            hud.add_atom_to_hud(this);
    }

    ///proc that handles moving along the bot's drawn path
    // tg: code/modules/mob/living/basic/bots/bot_hud.dm:91
    [SignalHandler]
    public virtual void handle_loop_movement(atom_movable source = null, atom oldloc = null, object dir = null, object forced = null)
    {
        handle_hud_path();
        Waiting.NoSleep(on_bot_movement(source, oldloc, dir, forced));
    }

    // tg: code/modules/mob/living/basic/bots/bot_hud.dm:97
    public virtual void handle_hud_path()
    {
        if (client != null || !Dm.truthy(length(current_pathed_turfs)) || ai_controller == null)
            return;

        turf our_turf = get_turf(this);
        image target_image = current_pathed_turfs[our_turf] as image;
        if (target_image != null)
            animate(target_image, Dm.named("alpha", 0), Dm.named("time", 0.3f * SECONDS));
        Dm.remove(ref current_pathed_turfs, our_turf);
    }

    ///proc that handles deleting the bot's drawn path when needed
    // tg: code/modules/mob/living/basic/bots/bot_hud.dm:108
    public virtual void clear_path_hud(float? remove_hud = null)
    {
        remove_hud ??= TRUE;
        foreach (turf index in Dm.each_cast<turf>(current_pathed_turfs?.Copy()))
        {
            image our_image = current_pathed_turfs[index] as image;
            animate(our_image, Dm.named("alpha", 0), Dm.named("time", 0.3f * SECONDS));
            Dm.remove(ref current_pathed_turfs, index);
        }

        if (!Dm.truthy(remove_hud))
            return;

        // Call hud remove handlers to ensure viewing user client images are removed
        DmList path_huds_watching_me = Dm.list(GLOB.huds[DATA_HUD_DIAGNOSTIC], GLOB.huds[DATA_HUD_BOT_PATH]);
        foreach (datum_atom_hud hud in Dm.each_cast<datum_atom_hud>(path_huds_watching_me))
            hud.remove_atom_from_hud(this);
    }
}
