// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dance_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H3 (D333, the bar): the whole file: the jukebox (the Jukebox window: the track, the repeat, the volume; the song cooldown and
// its error beep; securing it with a wrench; the right click that starts and stops it) and the radiant dance machine (its spotlights
// that cycle their colours to the beat, the orbiting sparkles, the hierophant's floor flash, and the four dances it makes its
// listeners do). The syndicate campaign G2 translated /mob/proc/dance_flip first. The var blocks are generated (D111).

// tg: code/game/machinery/dance_machine.dm:1
public partial class obj_machinery_jukebox : obj_machinery
{
    // tg: code/game/machinery/dance_machine.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        music_player = Dm.@new<datum_jukebox>(this);
        register_context();
        Waiting.NoSleep(AddComponent(typeof(datum_component_leanable)));
        return dot;
    }

    // tg: code/game/machinery/dance_machine.dm:26
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(anchored) ? "Unsecure" : "Secure";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        context[SCREENTIP_CONTEXT_RMB] = "Toggle Playing";
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/machinery/dance_machine.dm:34
    public override object Destroy(float? force = null)
    {
        stop_music();
        QDEL_NULL(ref music_player);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/dance_machine.dm:39
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (music_player.active_song_sound != null)
            Dm.append(ref dot, I18n.T("Now playing: []", music_player.selection.song_name));
        return dot;
    }

    // tg: code/game/machinery/dance_machine.dm:44
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (music_player.active_song_sound != null)
            return NONE;

        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;

        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/machinery/dance_machine.dm:53
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, music_player?.active_song_sound != null ? "-active" : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/dance_machine.dm:57
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN) || !Dm.truthy(await allowed__proc(user)))
            return dot;
        await toggle_playing(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/dance_machine.dm:64
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (isobserver(user))
            return await base.ui_status(user, state);
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, "must be anchored!");
            return UI_CLOSE;
        }
        if (!Dm.truthy(await allowed__proc(user)))
        {
            balloon_alert(user, "access denied!");
            user.playsound_local(this, Dm.resource("sound/machines/compiler/compiler-failure.ogg"), 20, TRUE);
            return UI_CLOSE;
        }
        if (!Dm.truthy(length(music_player.songs)))
        {
            to_chat(user, span_warning(I18n.T("Error: No music tracks have been authorized for your station. Petition Central Command to resolve this issue.")));
            user.playsound_local(this, Dm.resource("sound/machines/compiler/compiler-failure.ogg"), 25, TRUE);
            return UI_CLOSE;
        }
        return await base.ui_status(user, state);
    }

    // tg: code/game/machinery/dance_machine.dm:80
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Jukebox", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/dance_machine.dm:86
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        return music_player.get_ui_data();
    }

    // tg: code/game/machinery/dance_machine.dm:89
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        var user = ui.user as mob;
        switch (Dm.str(action))
        {
            case "toggle":
                await toggle_playing(user);
                return TRUE;

            case "select_track":
            {
                if (music_player.active_song_sound != null)
                {
                    to_chat(user, span_warning(I18n.T("Error: You cannot change the song until the current one is over.")));
                    return TRUE;
                }

                var new_song = music_player.songs[@params["track"]] as datum_track;
                if (QDELETED(this) || new_song == null)
                    return TRUE;

                music_player.selection = new_song;
                return TRUE;
            }

            case "set_volume":
            {
                object new_volume = @params["volume"];
                if (Dm.eq(new_volume, "reset") || Dm.eq(new_volume, "max"))
                    music_player.set_volume_to_max();
                else if (Dm.eq(new_volume, "min"))
                    music_player.set_new_volume(0);
                else if (isnum(text2num(new_volume)))
                    music_player.set_new_volume(text2num(new_volume));
                return TRUE;
            }

            case "loop":
                music_player.sound_loops = Dm.truthy(@params["looping"]) ? TRUE : FALSE;
                return TRUE;
        }
        return null;
    }

    ///If a song is playing, cut it. If none is playing, and the cooldown is up, start the queued track.
    // tg: code/game/machinery/dance_machine.dm:127
    public virtual async DmTask toggle_playing(mob user = null)
    {
        if (music_player.active_song_sound != null)
        {
            stop_music();
            return;
        }
        if (jukebox_song_cd <= world.time) // COOLDOWN_FINISHED(src, jukebox_song_cd), inline (rules 11.1 (a))
        {
            await activate_music();
            return;
        }
        balloon_alert(user, Dm.text("on cooldown for []!", DisplayTimeText(jukebox_song_cd - world.time))); // COOLDOWN_TIMELEFT, inline
        if (jukebox_error_cd <= world.time) // COOLDOWN_FINISHED(src, jukebox_error_cd)
        {
            playsound(this, Dm.resource("sound/machines/compiler/compiler-failure.ogg"), 25, TRUE);
            jukebox_error_cd = world.time + 15 * SECONDS; // COOLDOWN_START(src, jukebox_error_cd, 15 SECONDS)
        }
    }

    // tg: code/game/machinery/dance_machine.dm:139
    public virtual async DmTask<object> activate_music()
    {
        if (music_player.active_song_sound != null)
            return FALSE;

        music_player.start_music();
        update_use_power(ACTIVE_POWER_USE);
        update_appearance(UPDATE_ICON_STATE);
        if (!Dm.truthy(music_player.sound_loops))
            song_timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(stop_music)), music_player.selection.song_length, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), TIMER_DELETE_ME),
                file: "code/game/machinery/dance_machine.dm", line: 147);
        return TRUE;
    }

    // tg: code/game/machinery/dance_machine.dm:150
    public virtual object stop_music()
    {
        if (song_timerid != null)
            deltimer(song_timerid);

        music_player.unlisten_all();

        if (!QDELING(this))
        {
            jukebox_song_cd = world.time + 10 * SECONDS; // COOLDOWN_START(src, jukebox_song_cd, 10 SECONDS)
            playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 50, TRUE);
            update_use_power(IDLE_POWER_USE);
            update_appearance(UPDATE_ICON_STATE);
        }
        return TRUE;
    }

    // tg: code/game/machinery/dance_machine.dm:163
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        await DmTask.CompletedTask;
        if (!Dm.truthy(is_operational))
            stop_music();
    }
}

// tg: code/game/machinery/dance_machine.dm:171
public partial class obj_machinery_jukebox_disco : obj_machinery_jukebox
{
    // tg: code/game/machinery/dance_machine.dm:190
    public override async DmTask<object> activate_music()
    {
        object dot = await base.activate_music();
        if (!Dm.truthy(dot))
            return dot;
        dance_setup();
        // DEVIATION(DV491): tg calls lights_spin() and waits for it, and its loop lasts as long as the song, so begin_processing()
        // (the dances) would come only after the music stopped; the loop runs detached here, so the floor dances to the song.
        Waiting.Detach(lights_spin());
        begin_processing();
        return dot;
    }

    // tg: code/game/machinery/dance_machine.dm:198
    public override object stop_music()
    {
        object dot = base.stop_music();
        if (!Dm.truthy(dot))
            return dot;
        QDEL_LIST(spotlights);
        QDEL_LIST(sparkles);
        end_processing();
        return dot;
    }

    // tg: code/game/machinery/dance_machine.dm:206
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        float dance_num = rand(1, 4); //all will do the same dance
        foreach (mob_living dancer in Dm.each<mob_living>(music_player.get_active_listeners()))
        {
            if (!Dm.truthy(Dm.bitand(dancer.mobility_flags, MOBILITY_MOVE)))
                continue;
            if (HAS_TRAIT(dancer, TRAIT_DISCO_DANCER))
                continue;
            await dance(dancer, dance_num);
        }
        return null;
    }

    // tg: code/game/machinery/dance_machine.dm:215
    public virtual void dance_setup()
    {
        var cen = get_turf(this);
        foreach (turf t in Dm.each<turf>(dview(3, get_turf(this), INVISIBILITY_LIGHTING))) // FOR_DVIEW
        {
            float dist = get_dist(this, t);
            if (t.x == cen.x && t.y > cen.y)
            {
                Dm.append(ref spotlights, Dm.@new<obj_item_flashlight_spotlight>(t, 1 + dist, 30 - (dist * 8), COLOR_SOFT_RED));
                continue;
            }
            if (t.x == cen.x && t.y < cen.y)
            {
                Dm.append(ref spotlights, Dm.@new<obj_item_flashlight_spotlight>(t, 1 + dist, 30 - (dist * 8), LIGHT_COLOR_PURPLE));
                continue;
            }
            if (t.x > cen.x && t.y == cen.y)
            {
                Dm.append(ref spotlights, Dm.@new<obj_item_flashlight_spotlight>(t, 1 + dist, 30 - (dist * 8), LIGHT_COLOR_DIM_YELLOW));
                continue;
            }
            if (t.x < cen.x && t.y == cen.y)
            {
                Dm.append(ref spotlights, Dm.@new<obj_item_flashlight_spotlight>(t, 1 + dist, 30 - (dist * 8), LIGHT_COLOR_GREEN));
                continue;
            }
            if ((t.x + 1 == cen.x && t.y + 1 == cen.y) || (t.x + 2 == cen.x && t.y + 2 == cen.y))
            {
                Dm.append(ref spotlights, Dm.@new<obj_item_flashlight_spotlight>(t, 1.4f + dist, 30 - (dist * 8), LIGHT_COLOR_ORANGE));
                continue;
            }
            if ((t.x - 1 == cen.x && t.y - 1 == cen.y) || (t.x - 2 == cen.x && t.y - 2 == cen.y))
            {
                Dm.append(ref spotlights, Dm.@new<obj_item_flashlight_spotlight>(t, 1.4f + dist, 30 - (dist * 8), LIGHT_COLOR_CYAN));
                continue;
            }
            if ((t.x - 1 == cen.x && t.y + 1 == cen.y) || (t.x - 2 == cen.x && t.y + 2 == cen.y))
            {
                Dm.append(ref spotlights, Dm.@new<obj_item_flashlight_spotlight>(t, 1.4f + dist, 30 - (dist * 8), LIGHT_COLOR_BLUEGREEN));
                continue;
            }
            if ((t.x + 1 == cen.x && t.y - 1 == cen.y) || (t.x + 2 == cen.x && t.y - 2 == cen.y))
            {
                Dm.append(ref spotlights, Dm.@new<obj_item_flashlight_spotlight>(t, 1.4f + dist, 30 - (dist * 8), LIGHT_COLOR_BLUE));
                continue;
            }
        }
    }

    // tg: code/game/machinery/dance_machine.dm:245
    public virtual async DmTask hierofunk()
    {
        for (int i = 1; i <= 10; i++)
        {
            spawn_atom_to_turf(typeof(obj_effect_temp_visual_hierophant_telegraph_edge), this, 1, FALSE);
            await sleep(0.5f * SECONDS);
        }
    }

    // tg: code/game/machinery/dance_machine.dm:249  #define DISCO_INFENO_RANGE (rand(85, 115)*0.01)
    private static float DISCO_INFENO_RANGE => rand(85, 115) * 0.01f;

    // tg: code/game/machinery/dance_machine.dm:252
    public virtual async DmTask lights_spin()
    {
        for (int i = 1; i <= 25; i++)
        {
            if (QDELETED(this) || music_player.active_song_sound == null)
                return;
            var S = Dm.@new<obj_effect_overlay_sparkles>(this);
            S.alpha = 0;
            Dm.append(ref sparkles, S);
            if (i >= 1 && i <= 8)
                await S.orbit(this, 30, TRUE, 60, 36, TRUE);
            else if (i >= 9 && i <= 16)
                await S.orbit(this, 62, TRUE, 60, 36, TRUE);
            else if (i >= 17 && i <= 24)
                await S.orbit(this, 95, TRUE, 60, 36, TRUE);
            else if (i == 25)
            {
                S.pixel_y = 7;
                await S.forceMove(get_turf(this));
            }
            await sleep(0.7f * SECONDS);
        }
        foreach (obj_effect_overlay_sparkles reveal in Dm.each_cast<obj_effect_overlay_sparkles>(sparkles))
            reveal.alpha = 255;
        while (music_player?.active_song_sound != null)
        {
            foreach (obj_item_flashlight_spotlight glow in Dm.each_cast<obj_item_flashlight_spotlight>(spotlights)) // The multiples reflects custom adjustments to each colors after dozens of tests
            {
                if (QDELETED(glow))
                {
                    stack_trace(Dm.text("[] found in [].[]", Dm.truthy(glow?.gc_destroyed) ? "Qdeleting glow" : "null entry", this, Dm.truthy(gc_destroyed) ? " Source qdeleting at the time." : ""),
                        file: "code/game/machinery/dance_machine.dm", line: 278);
                    return;
                }
                switch (glow.light_color)
                {
                    case COLOR_SOFT_RED:
                        if (Dm.truthy(glow.even_cycle))
                        {
                            await glow.set_light_on(FALSE);
                            glow.set_light_color(LIGHT_COLOR_BLUE);
                        }
                        else
                        {
                            glow.set_light_range_power_color(glow.base_light_range * DISCO_INFENO_RANGE, glow.light_power * 1.48f, LIGHT_COLOR_BLUE);
                            await glow.set_light_on(TRUE);
                        }
                        break;
                    case LIGHT_COLOR_BLUE:
                        if (Dm.truthy(glow.even_cycle))
                        {
                            glow.set_light_range_power_color(glow.base_light_range * DISCO_INFENO_RANGE, glow.light_power * 2, LIGHT_COLOR_GREEN);
                            await glow.set_light_on(TRUE);
                        }
                        else
                        {
                            await glow.set_light_on(FALSE);
                            glow.set_light_color(LIGHT_COLOR_GREEN);
                        }
                        break;
                    case LIGHT_COLOR_GREEN:
                        if (Dm.truthy(glow.even_cycle))
                        {
                            await glow.set_light_on(FALSE);
                            glow.set_light_color(LIGHT_COLOR_ORANGE);
                        }
                        else
                        {
                            glow.set_light_range_power_color(glow.base_light_range * DISCO_INFENO_RANGE, glow.light_power * 0.5f, LIGHT_COLOR_ORANGE);
                            await glow.set_light_on(TRUE);
                        }
                        break;
                    case LIGHT_COLOR_ORANGE:
                        if (Dm.truthy(glow.even_cycle))
                        {
                            glow.set_light_range_power_color(glow.base_light_range * DISCO_INFENO_RANGE, glow.light_power * 2.27f, LIGHT_COLOR_PURPLE);
                            await glow.set_light_on(TRUE);
                        }
                        else
                        {
                            await glow.set_light_on(FALSE);
                            glow.set_light_color(LIGHT_COLOR_PURPLE);
                        }
                        break;
                    case LIGHT_COLOR_PURPLE:
                        if (Dm.truthy(glow.even_cycle))
                        {
                            await glow.set_light_on(FALSE);
                            glow.set_light_color(LIGHT_COLOR_BLUEGREEN);
                        }
                        else
                        {
                            glow.set_light_range_power_color(glow.base_light_range * DISCO_INFENO_RANGE, glow.light_power * 0.44f, LIGHT_COLOR_BLUEGREEN);
                            await glow.set_light_on(TRUE);
                        }
                        break;
                    case LIGHT_COLOR_BLUEGREEN:
                        if (Dm.truthy(glow.even_cycle))
                        {
                            glow.set_light_range(glow.base_light_range * DISCO_INFENO_RANGE);
                            glow.set_light_color(LIGHT_COLOR_DIM_YELLOW);
                            await glow.set_light_on(TRUE);
                        }
                        else
                        {
                            await glow.set_light_on(FALSE);
                            glow.set_light_color(LIGHT_COLOR_DIM_YELLOW);
                        }
                        break;
                    case LIGHT_COLOR_DIM_YELLOW:
                        if (Dm.truthy(glow.even_cycle))
                        {
                            await glow.set_light_on(FALSE);
                            glow.set_light_color(LIGHT_COLOR_CYAN);
                        }
                        else
                        {
                            glow.set_light_range(glow.base_light_range * DISCO_INFENO_RANGE);
                            glow.set_light_color(LIGHT_COLOR_CYAN);
                            await glow.set_light_on(TRUE);
                        }
                        break;
                    case LIGHT_COLOR_CYAN:
                        if (Dm.truthy(glow.even_cycle))
                        {
                            glow.set_light_range_power_color(glow.base_light_range * DISCO_INFENO_RANGE, glow.light_power * 0.68f, COLOR_SOFT_RED);
                            await glow.set_light_on(TRUE);
                        }
                        else
                        {
                            await glow.set_light_on(FALSE);
                            glow.set_light_color(COLOR_SOFT_RED);
                        }
                        glow.even_cycle = Dm.truthy(glow.even_cycle) ? FALSE : TRUE;
                        break;
                }
            }
            if (prob(2)) // Unique effects for the dance floor that show up randomly to mix things up
                Waiting.Detach(hierofunk()); // INVOKE_ASYNC
            await sleep(Dm.truthy(music_player.selection.song_beat_deciseconds) ? music_player.selection.song_beat_deciseconds : 1 * SECONDS);
            if (QDELETED(this))
                return;
        }
    }

    // tg: code/game/machinery/dance_machine.dm:347
    public virtual async DmTask dance(mob_living dancer = null, object dance_num = null) //Show your moves
    {
        ADD_TRAIT(dancer, TRAIT_DISCO_DANCER, REF(this));
        switch (Dm.num(dance_num))
        {
            case 1:
                await dance1(dancer);
                break;
            case 2:
                dance2(dancer);
                break;
            case 3:
                start_dance3(dancer);
                break;
            case 4:
                await dance4(dancer);
                break;
        }
    }

    // tg: code/game/machinery/dance_machine.dm:363
    public virtual async DmTask dance1(mob_living dancer = null)
    {
        await DmTask.CompletedTask;
        _addtimer(TRAIT_CALLBACK_REMOVE(dancer, TRAIT_DISCO_DANCER, REF(this)), 6.5f * SECONDS, TIMER_CLIENT_TIME, file: "code/game/machinery/dance_machine.dm", line: 364);
        for (float i = 0; i <= 6 * SECONDS; i += 1.5f * SECONDS)
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.dance_rotate), dancer, Dm.@new<datum_callback>(dancer, nameof(mob.dance_flip))), i, TIMER_CLIENT_TIME,
                file: "code/game/machinery/dance_machine.dm", line: 366);
    }

    // tg: code/game/machinery/dance_machine.dm:368
    public virtual void dance2(mob_living dancer = null, float? dance_length = null)
    {
        dance_length ??= 2.5f * SECONDS;
        matrix initial_matrix = matrix(dancer.transform);
        DmList transforms = Dm.list(
            Dm.kv(Dm.text("[]", NORTH), matrix(dancer.transform).Translate(0, 3)),
            Dm.kv(Dm.text("[]", EAST), matrix(dancer.transform).Translate(3, 0)),
            Dm.kv(Dm.text("[]", SOUTH), matrix(dancer.transform).Translate(0, -3)),
            Dm.kv(Dm.text("[]", WEST), matrix(dancer.transform).Translate(-1, -1)));
        _addtimer(Dm.@new<datum_callback>(dancer, nameof(atom.transform__varset), initial_matrix), dance_length + 0.5f * SECONDS, TIMER_CLIENT_TIME,
            file: "code/game/machinery/dance_machine.dm", line: 376); // VARSET_CALLBACK(dancer, transform, initial_matrix)
        _addtimer(TRAIT_CALLBACK_REMOVE(dancer, TRAIT_DISCO_DANCER, REF(this)), dance_length + 0.5f * SECONDS, file: "code/game/machinery/dance_machine.dm", line: 377);
        for (float i = 1; i <= dance_length; i++)
            _addtimer(Dm.@new<datum_callback>(this, nameof(animate_dance2), dancer, transforms, initial_matrix), i, TIMER_CLIENT_TIME,
                file: "code/game/machinery/dance_machine.dm", line: 379);
    }

    // tg: code/game/machinery/dance_machine.dm:381
    public virtual void animate_dance2(mob_living dancer = null, DmList transforms = null, matrix initial_matrix = null)
    {
        Waiting.NoSleep(dancer.setDir(turn(dancer.dir, 90)));
        animate(dancer, Dm.named("transform", transforms[num2text(dancer.dir)]), Dm.named("time", 1), Dm.named("loop", 0));
        animate(Dm.named("transform", initial_matrix), Dm.named("time", 2), Dm.named("loop", 0));
    }

    // tg: code/game/machinery/dance_machine.dm:386
    public virtual void start_dance3(mob_living dancer = null, float? dance_length = null)
    {
        dance_length ??= 3 * SECONDS;
        float initially_resting = dancer.resting;
        int direction_index = 1; //this should allow everyone to dance in the same direction
        _addtimer(TRAIT_CALLBACK_REMOVE(dancer, TRAIT_DISCO_DANCER, REF(this)), dance_length + 0.2f * SECONDS, file: "code/game/machinery/dance_machine.dm", line: 389);
        _addtimer(Dm.@new<datum_callback>(dancer, nameof(mob_living.set_resting), initially_resting, TRUE, TRUE), dance_length + 0.2f * SECONDS, TIMER_CLIENT_TIME,
            file: "code/game/machinery/dance_machine.dm", line: 390);
        for (float i = 1; i <= dance_length; i += 2) // 1 = 0.1 seconds
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(dance3), dancer, GLOB.cardinals[direction_index]), i, TIMER_CLIENT_TIME,
                file: "code/game/machinery/dance_machine.dm", line: 392);
            direction_index++;
            if (direction_index > ((DmList)GLOB.cardinals).len)
                direction_index = 1;
        }
    }

    // tg: code/game/machinery/dance_machine.dm:397
    public virtual async DmTask dance3(mob_living dancer = null, object dir = null)
    {
        await dancer.setDir(dir);
        await dancer.set_resting(Dm.truthy(dancer.resting) ? FALSE : TRUE, silent: TRUE, instant: TRUE);
    }

    // tg: code/game/machinery/dance_machine.dm:401
    public virtual async DmTask dance4(mob_living dancer = null, float? dance_length = null)
    {
        dance_length ??= 1.5f * SECONDS;
        matrix initial_matrix = matrix(dancer.transform);
        animate(dancer, Dm.named("transform", matrix(dancer.transform).Turn(180)), Dm.named("time", 2), Dm.named("loop", 0));
        await dancer.emote("spin");
        _addtimer(Dm.@new<datum_callback>(this, nameof(dance4_revert), dancer, initial_matrix), dance_length, TIMER_CLIENT_TIME,
            file: "code/game/machinery/dance_machine.dm", line: 405);
    }

    // tg: code/game/machinery/dance_machine.dm:407
    public virtual void dance4_revert(mob_living dancer = null, matrix starting_matrix = null)
    {
        animate(dancer, Dm.named("transform", starting_matrix), Dm.named("time", 5), Dm.named("loop", 0));
        REMOVE_TRAIT(dancer, TRAIT_DISCO_DANCER, REF(this));
    }
}

public partial class mob : atom_movable
{
    // tg: code/game/machinery/dance_machine.dm:359
    public virtual async DmTask dance_flip()
    {
        if (Dm.eq(dir, WEST))
            await emote("flip");
    }
}

public partial class atom
{
    /// VARSET_CALLBACK(dancer, transform, initial_matrix)'s target (dance2 above): ___callbackvarset's datum branch for this var.
    public virtual void transform__varset(object var_value = null)
    {
        transform = var_value;
    }
}
