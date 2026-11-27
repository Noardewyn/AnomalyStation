// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/arcade/orion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: The Orion Trail (its turns, crew, spaceports and events, the violent behaviour reports, its Realism Mode emag)
// with its OrionGame window, the Kobayashi Maru, and the emagged game's explosive model ship. The achievements are not ported
// (Dm.Deferred). The var blocks are generated (D111).

// tg: code/game/machinery/computer/arcade/orion.dm:3
public partial class obj_machinery_computer_arcade_orion_trail : obj_machinery_computer_arcade
{
    // tg: code/game/machinery/computer/arcade/orion.dm:1
    private const float ORION_TRAIL_WINTURN = 9;

    ///List of all orion events, created on Initialize.
    // tg: code/game/machinery/computer/arcade/orion.dm:10
    public static ref DmList orion_events => ref TgState.Current.obj_machinery_computer_arcade_orion_trail__orion_events;

    // tg: code/game/machinery/computer/arcade/orion.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (orion_events == null)
        {
            DmList events = Dm.list();
            foreach (Type path in Dm.each<Type>(subtypesof(typeof(datum_orion_event))))
            {
                var new_event = (datum_orion_event)Dm.@new(path, this);
                events[new_event] = new_event.weight;
            }
            orion_events = events;
        }
        setup_events();
        return dot;
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:44
    public virtual void setup_events()
    {
        events = orion_events.Copy();
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:47
    public override object Destroy(float? force = null)
    {
        events = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:81
    public virtual void newgame(mob_living player = null)
    {
        // Set names of settlers in crew
        object player_crew_name = first_name(player.name);
        settlers = Dm.list();
        for (float i = 1; i <= ORION_STARTING_CREW_COUNT - 1; i++) //one reserved to be YOU
            add_crewmember(update: FALSE);
        add_crewmember(Dm.text("[]", player_crew_name));
        // Re-set items to defaults
        engine = 1;
        hull = 1;
        electronics = 1;
        food = 80;
        fuel = 60;
        alive = ORION_STARTING_CREW_COUNT;
        turns = 1;
        @event = null;
        gameStatus = ORION_STATUS_NORMAL;
        lings_aboard = 0;
        lings_suspected = FALSE;
        killed_crew = 0;

        //spaceport junk
        spaceport_raided = FALSE;
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:106
    public virtual async DmTask report_player(mob gamer = null)
    {
        if (Dm.eq(gamers[gamer], ORION_GAMER_GIVE_UP))
            return; // enough harassing them

        if (Dm.eq(gamers[gamer], ORION_GAMER_PAMPHLET))
        {
            await say("WARNING: Continued antisocial behavior detected: Dispensing self-help literature.");
            Dm.@new<obj_item_paper_pamphlet_violent_video_games>(drop_location());
            gamers[gamer] = Dm.num(gamers[gamer]) - 1;
            return;
        }

        if (!Dm.@in(gamer, gamers))
            gamers[gamer] = 0;

        gamers[gamer] = Dm.num(gamers[gamer]) + 1; // How many times the player has 'prestiged' (massacred their crew)

        if (Dm.num(gamers[gamer]) > ORION_GAMER_REPORT_THRESHOLD && prob(20 * Dm.num(gamers[gamer])))
        {
            await aas_config_announce(typeof(datum_aas_config_entry_orion_violent_behavior_alert), Dm.list(
                Dm.kv("PERSON", gamer.name),
                Dm.kv("LOCATION", get_area_name(this)),
                Dm.kv("SOURCE", name)), this, Dm.list(RADIO_CHANNEL_SECURITY), RADIO_CHANNEL_SECURITY);

            await aas_config_announce(typeof(datum_aas_config_entry_orion_violent_behavior_alert), Dm.list(
                Dm.kv("PERSON", gamer.name),
                Dm.kv("LOCATION", get_area_name(this)),
                Dm.kv("SOURCE", name)), this, Dm.list(RADIO_CHANNEL_MEDICAL), RADIO_CHANNEL_MEDICAL);

            gamers[gamer] = ORION_GAMER_PAMPHLET; //next report send a pamph

            Dm.Deferred("code/game/machinery/computer/arcade/orion.dm:132", "report_player: gamer.client.give_award(/datum/award/achievement/misc/gamer, gamer) (the achievements)");
            gamer.mind?.adjust_experience(typeof(datum_skill_gaming), 50); // cheevos make u better
        }
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:135
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "OrionGame", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:142
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/moods)): the asset transport is the client's (DV44); the OrionGame window
        // draws the settlers' mood faces from its own copy of tg's mood icons
        return Dm.list();
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:147
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["gamestatus"] = gameStatus;

        data["engine"] = engine;
        data["turns"] = turns;
        data["hull"] = hull;
        data["electronics"] = electronics;
        data["food"] = food;
        data["fuel"] = fuel;
        data["lings_suspected"] = lings_suspected;

        data["eventname"] = @event?.name;
        data["eventtext"] = @event?.text;
        data["buttons"] = @event?.event_responses;

        data["spaceport_raided"] = spaceport_raided;

        data["reason"] = reason;

        data["settlers"] = settlers;
        data["settlermoods"] = settlermoods;

        return data;
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:173
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList static_data = Dm.list();
        static_data["gamename"] = name;
        static_data["emagged"] = Dm.bitand(obj_flags, EMAGGED);

        return static_data;
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:180
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        var gamer = ui.user as mob_living;
        if (gamer == null)
            return dot;

        dot = TRUE;

        object gamer_skill_level = 0;
        object gamer_skill = 0;
        object gamer_skill_rands = 0;

        if (gamer?.mind != null)
        {
            gamer_skill_level = gamer.mind.get_skill_level(typeof(datum_skill_gaming));
            gamer_skill = gamer.mind.get_skill_modifier(typeof(datum_skill_gaming), SKILL_PROBS_MODIFIER);
            gamer_skill_rands = gamer.mind.get_skill_modifier(typeof(datum_skill_gaming), SKILL_RANDS_MODIFIER);
        }

        float xp_gained = 0;

        await gamer.played_game();

        if (@event != null)
        {
            await @event.response(this, action);
            if (!Dm.truthy(settlers.len) || Dm.num(food) <= 0 || Dm.num(fuel) <= 0)
            {
                await set_game_over(gamer);
                return dot;
            }
            new_settler_mood(); //events shake people up a bit and can also change food
            return TRUE;
        }
        switch (action)
        {
            case "start_game":
                if (gameStatus != ORION_STATUS_START)
                    return dot;
                newgame(gamer);
                break;
            case "instructions":
                if (gameStatus != ORION_STATUS_START)
                    return dot;
                gameStatus = ORION_STATUS_INSTRUCTIONS;
                break;
            case "back_to_menu": //back to the main menu
                gameStatus = ORION_STATUS_START;
                if (gameStatus == ORION_STATUS_GAMEOVER)
                {
                    @event = null;
                    reason = null;
                    food = 80;
                    fuel = 60;
                    settlers = Dm.list("Harry", "Larry", "Bob");
                }
                break;
            case "continue":
                if (gameStatus == ORION_STATUS_START)
                    return dot;
                if (turns >= ORION_TRAIL_WINTURN)
                {
                    await win(gamer);
                    xp_gained += 34;
                    return dot;
                }
                gamer.mind.adjust_experience(typeof(datum_skill_gaming), xp_gained + 1);
                food -= (alive + lings_aboard) * 2;
                fuel -= 5;
                turns += 1;
                //out of supplies, die
                if (Dm.num(food) <= 0 || Dm.num(fuel) <= 0)
                    await set_game_over(gamer);
                if (turns == 2 && prob(30 - Dm.num(gamer_skill))) //asteroids part of the trip
                {
                    await encounter_event(typeof(datum_orion_event_hull_part), gamer, gamer_skill, gamer_skill_level, gamer_skill_rands);
                    return dot;
                }
                if (turns == 4) //halfway mark
                {
                    await encounter_event(typeof(datum_orion_event_space_port_tau_ceti), gamer, gamer_skill, gamer_skill_level, gamer_skill_rands);
                    return dot;
                }
                if (turns == 7) //black hole part of the trip
                {
                    await encounter_event(typeof(datum_orion_event_black_hole), gamer, gamer_skill, gamer_skill_level, gamer_skill_rands);
                    return dot;
                }
                //an uneventful (get it) turn
                if (prob(25 + Dm.num(gamer_skill)))
                    return dot;
                await encounter_event(null, gamer, gamer_skill, gamer_skill_level);
                if (Dm.truthy(lings_aboard) && (istype(@event, typeof(datum_orion_event_changeling_infiltration)) || prob(45 + Dm.num(gamer_skill))))
                {
                    //upgrade infiltration/whatever else we got to attack right away
                    await encounter_event(typeof(datum_orion_event_changeling_attack), gamer, gamer_skill, gamer_skill_level, gamer_skill_rands);
                }
                break;
            case "random_kill":
                await execute_crewmember(gamer);
                break;
            case "target_kill":
                await execute_crewmember(gamer, @params["who"]);
                break;
            //Spaceport specific interactions
            case "buycrew": //buy a crewmember
                if (!Dm.truthy(spaceport_raided) && Dm.num(food) >= 10 && Dm.num(fuel) >= 10 && gameStatus == ORION_STATUS_MARKET)
                {
                    if (Dm.truthy(@params["odd"]))
                    {
                        //find some silly crewmember name
                        add_crewmember(Dm.str(pick(Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(GLOB.commando_names, GLOB.nightmare_names), GLOB.ai_names), GLOB.clown_names), GLOB.mime_names), GLOB.plasmaman_names), GLOB.ethereal_names), GLOB.carp_names))));
                    }
                    else
                        add_crewmember();
                    fuel -= ORION_BUY_CREW_PRICE;
                    food -= ORION_BUY_CREW_PRICE;
                    killed_crew--; // I mean not really but you know
                }
                break;
            case "sellcrew": //sell a crewmember
                if (!Dm.truthy(spaceport_raided) && settlers.len > 1 && gameStatus == ORION_STATUS_MARKET)
                {
                    remove_crewmember();
                    fuel += ORION_SELL_CREW_PRICE;
                    food += ORION_SELL_CREW_PRICE;
                }
                break;
            case "leave_spaceport":
                if (gameStatus != ORION_STATUS_MARKET) //Can't leave a place you aren't in
                    return dot;
                gameStatus = ORION_STATUS_NORMAL;
                spaceport_raided = FALSE;
                break;
            case "raid_spaceport":
                if (gameStatus != ORION_STATUS_MARKET)
                    return dot;
                spaceport_raided = TRUE;
                await encounter_event(typeof(datum_orion_event_space_port_raid), gamer, gamer_skill, gamer_skill_level, gamer_skill_rands);
                break;
            case "buyparts":
                if (!Dm.truthy(spaceport_raided) && Dm.num(fuel) > ORION_TRADE_RATE && gameStatus == ORION_STATUS_MARKET)
                {
                    object sw__1 = @params["part"];
                    if (Dm.eq(sw__1, ORION_BUY_ENGINE_PARTS))
                        engine++;
                    else if (Dm.eq(sw__1, ORION_BUY_ELECTRONICS))
                        hull++;
                    else if (Dm.eq(sw__1, ORION_BUY_HULL_PARTS))
                        electronics++;
                    fuel -= ORION_TRADE_RATE;
                }
                break;
            case "trade":
                if (!Dm.truthy(spaceport_raided) && gameStatus == ORION_STATUS_MARKET)
                {
                    object sw__2 = @params["what"];
                    if (Dm.eq(sw__2, ORION_I_WANT_FUEL))
                    {
                        if (Dm.num(fuel) > ORION_TRADE_RATE)
                        {
                            fuel -= ORION_TRADE_RATE;
                            food += ORION_TRADE_RATE;
                        }
                    }
                    else if (Dm.eq(sw__2, ORION_I_WANT_FOOD))
                    {
                        if (Dm.num(food) > ORION_TRADE_RATE)
                        {
                            fuel += ORION_TRADE_RATE;
                            food -= ORION_TRADE_RATE;
                        }
                    }
                }
                break;
        }
        return dot;
    }

    /**
     * pickweights a new event, sets event var as it. it then preps the event if it needs it
     *
     * giving a path argument will instead find that instanced datum instead of pickweighting. Used in events that follow from events.
     * Arguments:
     * * path: if we want a specific event, this is the path of the wanted one
     * * gamer: person using the arcade, used in emag effects
     * * gamer_skill: gaming skill of the player
     * * gamer_skill_level: gaming skill level of the player
     * * gamer_skill_rands: See above but for random chances, you can just look at gaming skill to see how it chalks that up
     */
    // tg: code/game/machinery/computer/arcade/orion.dm:322
    public virtual async DmTask encounter_event(object path = null, object gamer = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        if (!Dm.truthy(path))
            @event = pick_weight(events) as datum_orion_event;
        else
        {
            foreach (datum_orion_event instance in Dm.each<datum_orion_event>(events))
            {
                if (Dm.eq(instance.type, path))
                {
                    @event = instance;
                    break;
                }
            }
        }
        if (@event == null)
            throw CRASH(Dm.text("Woah, hey! we could not find the specified event \"[]\"! Add it to the events list, numb nuts!", path), "code/game/machinery/computer/arcade/orion.dm", 331);
        await @event.on_select(this, gamer_skill, gamer_skill_level, gamer_skill_rands);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            await @event.emag_effect(this, gamer as mob_living);
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:335
    public virtual async DmTask set_game_over(mob_living user = null, object given_reason = null)
    {
        user.lost_game();
        gameStatus = ORION_STATUS_GAMEOVER;
        @event = null;
        reason = Dm.truthy(given_reason) ? given_reason : await death_reason(user);
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:341
    public virtual async DmTask<object> death_reason(mob_living gamer = null)
    {
        object reason = null;
        if (!Dm.truthy(settlers.len))
            reason = I18n.T("Your entire crew died, and your ship joins the fleet of ghost-ships littering the galaxy.");
        else
        {
            if (Dm.num(food) <= 0)
            {
                reason = I18n.T("You ran out of food and starved.");
                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                {
                    await gamer.set_nutrition(0); //yeah you pretty hongry
                    to_chat(gamer, span_userdanger(I18n.T("Your body instantly contracts to that of one who has not eaten in months. Agonizing cramps seize you as you fall to the floor.")));
                }
            }
            if (Dm.num(fuel) <= 0)
            {
                reason = I18n.T("You ran out of fuel, and drift, slowly, into a star.");
                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                {
                    await gamer.adjust_fire_stacks(5);
                    gamer.ignite_mob(); //flew into a star, so you're on fire
                    to_chat(gamer, span_userdanger(I18n.T("You feel an immense wave of heat emanate from the arcade machine. Your skin bursts into flames.")));
                }
            }
        }

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            to_chat(gamer, span_userdanger(I18n.T("You're never going to make it to Orion...")));
            gamer.investigate_log("has been killed by an emagged Orion Trail game.", INVESTIGATE_DEATHS);
            await gamer.death();
            obj_flags = Dm.bitand(obj_flags, ~EMAGGED); //removes the emagged status after you lose
            gamer.log_message("lost a Realism Mode Orion Trail game, changing the machine back to normal.", LOG_GAME);
            gameStatus = ORION_STATUS_START;
            name = "The Orion Trail";
            desc = "Learn how our ancestors got to Orion, and have fun in the process!";
        }

        gamer?.mind?.adjust_experience(typeof(datum_skill_gaming), 10); //learning from your mistakes is the first rule of roguelikes
        return reason;
    }

    //Add Random/Specific crewmember
    // tg: code/game/machinery/computer/arcade/orion.dm:373
    public virtual object add_crewmember(string specific = null, float? update = null)
    {
        specific ??= "";
        update ??= TRUE;
        object newcrew = "";
        if (Dm.truthy(specific))
            newcrew = specific;
        else
        {
            if (prob(50))
                newcrew = pick(GLOB.first_names_male);
            else
                newcrew = pick(GLOB.first_names_female);
        }
        if (Dm.truthy(newcrew))
        {
            Dm.append(ref settlers, newcrew);
            alive++;
        }
        if (Dm.truthy(update))
            new_settler_mood(); //new faces!
        return newcrew;
    }

    //Remove Random/Specific crewmember
    // tg: code/game/machinery/computer/arcade/orion.dm:390
    public virtual object remove_crewmember(string specific = null, string dont_remove = null, float? update = null)
    {
        specific ??= "";
        dont_remove ??= "";
        update ??= TRUE;
        DmList safe_to_remove = settlers;
        object removed = "";
        if (Dm.truthy(dont_remove))
            Dm.remove(ref safe_to_remove, dont_remove);
        if (Dm.truthy(specific) && specific != dont_remove)
            safe_to_remove = Dm.list(specific);
        removed = pick(safe_to_remove);

        if (Dm.truthy(removed))
        {
            if (Dm.truthy(lings_aboard) && prob(40 * lings_aboard)) //if there are 2 lings you're twice as likely to get one, obviously
                lings_aboard = max(0, --lings_aboard);
            Dm.remove(ref settlers, removed);
            alive--;
        }
        if (Dm.truthy(update))
            new_settler_mood(); //bro, i...
        return removed;
    }

    /**
     * Crewmember executed code, targeted when there are no lings and untargeted when there are some
     * If there was no suspected lings (aka random shots) this is just murder and counts towards killed crew
     *
     * Arguments:
     * * gamer: carbon that may need emag effects applied
     */
    // tg: code/game/machinery/computer/arcade/orion.dm:417
    public virtual async DmTask execute_crewmember(mob_living gamer = null, object target = null)
    {
        object sheriff = remove_crewmember(target as string); //I shot the sheriff
        if (Dm.truthy(target))
            killed_crew += 1; //if there was no suspected lings, this is just plain murder
        playsound(loc as atom, Dm.resource("sound/items/weapons/gun/pistol/shot.ogg"), 100, TRUE);
        if (!Dm.truthy(settlers.len) || !Dm.truthy(alive))
        {
            await say(I18n.T("The last crewmember [], shot themselves, GAME OVER!", sheriff));
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            {
                gamer.investigate_log("has been killed by an emagged Orion Trail game.", INVESTIGATE_DEATHS);
                await gamer.death();
            }
            await set_game_over(gamer, I18n.T("Your last pioneer committed suicide."));
            if (killed_crew >= ORION_STARTING_CREW_COUNT)
            {
                gamer.mind?.adjust_experience(typeof(datum_skill_gaming), -15); //no cheating by spamming game overs
                await report_player(gamer);
            }
        }
        else if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            if (Dm.truthy(findtext(gamer.name, sheriff)))
            {
                await say(I18n.T("The crew of the ship chose to kill []!", gamer));
                gamer.investigate_log("has been killed by an emagged Orion Trail game.", INVESTIGATE_DEATHS);
                await gamer.death();
            }
        }
    }

    /**
     * Creates a new mood icon for each settler
     *
     * Things that effect mood:
     * * Pioneer count
     * * Low food
     * * Low parts
     * * Sometimes they're just a bit happier or sadder
     * Arguments:
     * * None!
     */
    // tg: code/game/machinery/computer/arcade/orion.dm:452
    public virtual void new_settler_mood()
    {
        settlermoods.Cut();
        for (float i = 1; i <= settlers.len; i++)
        {
            float food_mood = Dm.num(Dm.num(food) >= 15);
            float supply_mood = -2;
            if (Dm.truthy(hull))
                supply_mood++;
            if (Dm.truthy(electronics))
                supply_mood++;
            if (Dm.truthy(engine))
                supply_mood++;
            float changing_mood = 0;
            if (prob(60)) //sometimes they just feel better or worse
                changing_mood = rand(-1, 1);
            settlermoods[settlers[i]] = Dm.num(settlermoods[settlers[i]]) + max(settlers.len + food_mood + supply_mood + changing_mood, 1);
            if (Dm.truthy(lings_suspected)) //lings ruin any good mood
                settlermoods[settlers[i]] = min(Dm.num(settlermoods[settlers[i]]), 3);
        }
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:470
    public virtual async DmTask win(mob_living user = null)
    {
        user.won_game();
        gameStatus = ORION_STATUS_START;
        await say(I18n.T("Congratulations, you made it to Orion!"));
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            Dm.@new<obj_item_orion_ship>(loc);
            message_admins(Dm.text("[] made it to Orion on an emagged machine and got an explosive toy ship.", ADMIN_LOOKUPFLW(user)));
            user.log_message("made it to Orion on an emagged machine and got an explosive toy ship.", LOG_GAME);
        }
        else
            victory_tickets(2);
        obj_flags = Dm.bitand(obj_flags, ~EMAGGED);
        name = Dm.initial(this, (atom p) => p.name);
        desc = Dm.initial(this, (atom p) => p.desc);
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:485
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        name = I18n.T("The Orion Trail: Realism Edition");
        desc = I18n.T("Learn how our ancestors got to Orion, and try not to die in the process!");
        obj_flags = Dm.bitor(obj_flags, EMAGGED);

        if (user == null)
            return TRUE;

        user.log_message(Dm.text("emagged [], activating Realism Mode.", this), LOG_GAME);
        balloon_alert(user, I18n.T("realism mode enabled"));
        to_chat(user, span_notice(I18n.T("You override the cheat code menu and skip to Cheat #[]: Realism Mode.", rand(1, 50))));
        newgame(user as mob_living);
        return TRUE;
    }
}

// tg: code/game/machinery/computer/arcade/orion.dm:52
public partial class obj_machinery_computer_arcade_orion_trail_kobayashi : obj_machinery_computer_arcade_orion_trail
{
    // tg: code/game/machinery/computer/arcade/orion.dm:75
    public override void setup_events()
    {
        events = orion_events.Copy();
        foreach (datum_orion_event @event in Dm.each<datum_orion_event>(events))
        {
            if (!Dm.@in(@event.type, event_whitelist))
                events.Remove(@event);
        }
    }
}

///A minibomb achieved from winning at emagged Orion.
// tg: code/game/machinery/computer/arcade/orion.dm:503
public partial class obj_item_orion_ship : obj_item
{
    // tg: code/game/machinery/computer/arcade/orion.dm:512
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(in_range(user, this)))
            return dot;
        if (Dm.truthy(active))
        {
            Dm.append(ref dot, span_notice(I18n.T("There's a little switch on the bottom. It's flipped up.")));
            return dot;
        }
        Dm.append(ref dot, span_notice(I18n.T("There's a little switch on the bottom. It's flipped down.")));
        return dot;
    }

    // tg: code/game/machinery/computer/arcade/orion.dm:521
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(active))
            return null;

        log_bomber(user, "primed an explosive", this, "for detonation");
        to_chat(user, span_warning(I18n.T("You flip the switch on the underside of [].", this)));
        active = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(commit_explosion)), 1 * SECONDS, file: "code/game/machinery/computer/arcade/orion.dm", line: 528);
        return null;
    }

    ///After some dialogue (which doubles as the timer until explosion), causes a minibomb-level explosion.
    // tg: code/game/machinery/computer/arcade/orion.dm:531
    public virtual async DmTask commit_explosion(float? dialogue_level = null)
    {
        dialogue_level ??= 0;
        float time_for_next_level = 0;
        float sw__1 = Dm.num(dialogue_level);
        if (sw__1 == 0)
        {
            await say(I18n.T("This is ship ID #[] to Orion Port Authority. We're coming in for landing, over.", rand(1, 1000)));
            time_for_next_level = 2 * SECONDS;
        }
        else if (sw__1 == 1)
        {
            await say(I18n.T("Uh, Port? Having some issues with our reactor, could you check it out? Over."));
            time_for_next_level = 3 * SECONDS;
        }
        else if (sw__1 == 2)
        {
            await say(I18n.T("Oh, God! Code Eight! CODE EIGHT! IT'S GONNA BL-"));
            playsound(loc as atom, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 25, TRUE);
            time_for_next_level = 0.36f * SECONDS;
        }
        else if (sw__1 >= 3 && sw__1 <= INFINITY)
        {
            visible_message(span_userdanger(I18n.T("[] explodes!", this)));
            await explosion(this, devastation_range: 2, heavy_impact_range: 4, light_impact_range: 8, flame_range: 16);
            qdel(this);
            return;
        }

        if (Dm.truthy(time_for_next_level))
        {
            dialogue_level++;
            _addtimer(Dm.@new<datum_callback>(this, nameof(commit_explosion), dialogue_level), time_for_next_level, file: "code/game/machinery/computer/arcade/orion.dm", line: 552);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/computer/arcade/orion.dm:10
    internal DmList obj_machinery_computer_arcade_orion_trail__orion_events;
}
