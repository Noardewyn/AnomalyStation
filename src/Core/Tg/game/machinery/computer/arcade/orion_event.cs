// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/arcade/orion_event.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: The Orion Trail's events, their responses and their Realism Mode (emag) effects. The var blocks are generated (D111).

//yep you guessed it back at it again with another datum singleton.
// tg: code/game/machinery/computer/arcade/orion_event.dm:2
public partial class datum_orion_event : datum
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:58-59, :82, :113, :160-161, :184-187, :232, :325-326, :376, :391
    protected const string BUTTON_FIX_ENGINE = "Fix Engine";
    protected const string BUTTON_WAIT = "Wait";
    protected const string BUTTON_REPAIR_ELECTRONICS = "Repair Electronics";
    protected const string BUTTON_RESTORE_HULL = "Restore Hull";
    protected const string BUTTON_EXPLORE_SHIP = "Explore Ship";
    protected const string BUTTON_LEAVE_THE_DERELICT = "Leave the Derelict";
    protected const string BUTTON_WELCOME_ABOARD = "Welcome aboard.";
    protected const string BUTTON_WHERE_DID_YOU_GO = "Where did you go?!";
    protected const string BUTTON_A_GOOD_FIND = "A good find.";
    protected const string BUTTON_CONTINUE_TRAVELS = "Continue travels.";
    protected const string BUTTON_CONTINUE = "Continue";
    protected const string BUTTON_KEEP_SPEED = "Keep Speed";
    protected const string BUTTON_SLOW_DOWN = "Slow Down";
    protected const string BUTTON_SPEED_PAST = "Speed Past";
    protected const string BUTTON_GO_AROUND = "Go Around";
    protected const string BUTTON_OH = "Oh...";
    protected const string BUTTON_DOCK = "Dock";

    /**
     * What happens when this event is selected to trigger, sets vars. also can set some event pre-encounter randomization
     *
     * Arguments:
     * * game: arcade machine calling this
     * * gamer_skill: gaming skill of the player
     * * gamer_skill_level: gaming skill level of the player
     * * gamer_skill_rands: See above but it's another metric, you can just look at gaming skill to see how it chalks that up
     */
    // tg: code/game/machinery/computer/arcade/orion_event.dm:29
    public virtual async DmTask on_select(obj_machinery_computer_arcade_orion_trail game = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        this.gamer_skill_level = Dm.num(gamer_skill_level);
        this.gamer_skill = Dm.num(gamer_skill);
        this.gamer_skill_rands = Dm.num(gamer_skill_rands);
    }

    /**
     * What happens when you respond to this event by choosing one of the buttons
     *
     * Arguments:
     * * game: arcade machine calling this
     * * choice: name of the button you pressed.
     */
    // tg: code/game/machinery/computer/arcade/orion_event.dm:42
    public virtual async DmTask response(obj_machinery_computer_arcade_orion_trail game = null, object choice = null)
    {
        game.@event = null;
    }

    /**
     * Some effect that happens to the carbon when this event triggers on an emagged arcade machine.
     *
     * By default, it just sends an audible message and a sound, both vars on the orion datum
     * Arguments:
     * * game: arcade machine calling this
     * * gamer: victim to apply effects to
     * * gamer_skill: skill of the gamer, because gamers gods can lessen the effects of the emagged machine
     * * gamer_skill_level: skill level of the gamer, another way to measure emag downside avoidance
     */
    // tg: code/game/machinery/computer/arcade/orion_event.dm:54
    public virtual async DmTask emag_effect(obj_machinery_computer_arcade_orion_trail game = null, mob_living gamer = null)
    {
        if (Dm.truthy(emag_message))
            game.audible_message(emag_message);
        if (Dm.truthy(emag_sound))
            playsound(game, emag_sound, 100, TRUE);
    }
}

///Engine Breakdown - spend one engine part or wait 3 days (harmless emag effect)
// tg: code/game/machinery/computer/arcade/orion_event.dm:62
public partial class datum_orion_event_engine_part : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:72
    public override async DmTask on_select(obj_machinery_computer_arcade_orion_trail game = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        await base.on_select(game, gamer_skill, gamer_skill_level, gamer_skill_rands);
        if (Dm.num(game.engine) >= 1)
            Dm.append(ref event_responses, BUTTON_FIX_ENGINE);
        Dm.append(ref event_responses, BUTTON_WAIT);
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:78
    public override async DmTask response(obj_machinery_computer_arcade_orion_trail game = null, object choice = null)
    {
        if (Dm.eq(choice, BUTTON_FIX_ENGINE))
        {
            game.engine = Dm.num(game.engine) - 1;
            game.engine = max(0, Dm.num(game.engine));
        }
        else
            game.food = Dm.num(game.food) - (game.alive + game.lings_aboard) * ORION_LONG_DELAY;
        event_responses.Cut();
        await base.response(game, choice);
    }
}

///Malfunction - spend one engine part or wait 3 days (emag effect randomizes some stats)
// tg: code/game/machinery/computer/arcade/orion_event.dm:90
public partial class datum_orion_event_electronic_part : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:99
    public override async DmTask on_select(obj_machinery_computer_arcade_orion_trail game = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        await base.on_select(game, gamer_skill, gamer_skill_level, gamer_skill_rands);
        if (Dm.num(game.electronics) >= 1)
            Dm.append(ref event_responses, BUTTON_REPAIR_ELECTRONICS);
        Dm.append(ref event_responses, BUTTON_WAIT);
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:105
    public override async DmTask response(obj_machinery_computer_arcade_orion_trail game = null, object choice = null)
    {
        if (Dm.eq(choice, BUTTON_REPAIR_ELECTRONICS))
            game.electronics = max(0, Dm.num(game.electronics) - 1);
        else
            game.food = Dm.num(game.food) - (game.alive + game.lings_aboard) * ORION_LONG_DELAY;
        event_responses.Cut();
        await base.response(game, choice);
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:113
    public override async DmTask emag_effect(obj_machinery_computer_arcade_orion_trail game = null, mob_living gamer = null)
    {
        playsound(game, Dm.resource("sound/effects/empulse.ogg"), 50, TRUE);
        game.visible_message(span_danger(I18n.T("[] malfunctions, randomizing in-game stats!", game)));
        float? oldfood = game.food;
        float? oldfuel = game.fuel;
        game.food = rand(10, 80) / rand(1, 2);
        game.fuel = rand(10, 60) / rand(1, 2);
        if (Dm.truthy(game.electronics))
            _addtimer(Dm.@new<datum_callback>(this, nameof(revert_random), game, oldfood, oldfuel), 1 * SECONDS, file: "code/game/machinery/computer/arcade/orion_event.dm", line: 121);
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:123
    public virtual void revert_random(obj_machinery_computer_arcade_orion_trail game = null, object oldfood = null, object oldfuel = null)
    {
        if (Dm.num(oldfuel) > Dm.num(game.fuel) && Dm.num(oldfood) > Dm.num(game.food))
            game.audible_message(span_danger(I18n.T("[] lets out a somehow reassuring chime.", game)));
        else if (Dm.num(oldfuel) < Dm.num(game.fuel) || Dm.num(oldfood) < Dm.num(game.food))
            game.audible_message(span_danger(I18n.T("[] lets out a somehow ominous chime.", game)));
        game.food = Dm.num_or_null(oldfood);
        game.fuel = Dm.num_or_null(oldfuel);
        playsound(game, Dm.resource("sound/machines/chime.ogg"), 50, TRUE);
    }
}

///Collision - spend one engine part or wait 3 days (has a nasty emag effect)
// tg: code/game/machinery/computer/arcade/orion_event.dm:135
public partial class datum_orion_event_hull_part : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:143
    public override async DmTask on_select(obj_machinery_computer_arcade_orion_trail game = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        await base.on_select(game, gamer_skill, gamer_skill_level, gamer_skill_rands);
        if (game.hull >= 1)
            Dm.append(ref event_responses, BUTTON_RESTORE_HULL);
        Dm.append(ref event_responses, BUTTON_WAIT);
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:149
    public override async DmTask response(obj_machinery_computer_arcade_orion_trail game = null, object choice = null)
    {
        if (Dm.eq(choice, BUTTON_RESTORE_HULL))
            game.hull = max(0, game.hull - 1);
        else
            game.food = Dm.num(game.food) - (game.alive + game.lings_aboard) * ORION_LONG_DELAY;
        event_responses.Cut();
        await base.response(game, choice);
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:157
    public override async DmTask emag_effect(obj_machinery_computer_arcade_orion_trail game = null, mob_living gamer = null)
    {
        if (prob(10 + gamer_skill))
        {
            await game.say(I18n.T("Something slams into the floor around [] - luckily, it didn't get through!", game));
            playsound(game, Dm.resource("sound/effects/bang.ogg"), 50, TRUE);
            return;
        }
        playsound(game, Dm.resource("sound/effects/bang.ogg"), 100, TRUE);
        foreach (turf_open_floor smashed in Dm.each<turf_open_floor>(orange(1, game)))
            await smashed.ScrapeAway();
        await game.say(I18n.T("Something slams into the floor around [], exposing it to space!", game));
        if (Dm.truthy(game.hull))
            _addtimer(Dm.@new<datum_callback>(this, nameof(fix_floor), game), 1 * SECONDS, file: "code/game/machinery/computer/arcade/orion_event.dm", line: 168);
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:170
    public virtual async DmTask fix_floor(obj_machinery_computer_arcade_orion_trail game = null)
    {
        await game.say(I18n.T("A new floor suddenly appears around []. What the hell?", game));
        playsound(game, Dm.resource("sound/items/weapons/genhit.ogg"), 100, TRUE);
        foreach (turf_open_space @fixed in Dm.each<turf_open_space>(orange(1, game)))
            await @fixed.place_on_top(typeof(turf_open_floor_plating));
    }
}

// tg: code/game/machinery/computer/arcade/orion_event.dm:179
public partial class datum_orion_event_old_ship : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:186
    public override async DmTask response(obj_machinery_computer_arcade_orion_trail game = null, object choice = null)
    {
        if (Dm.eq(choice, BUTTON_LEAVE_THE_DERELICT))
        {
            await base.response(game, choice);
            return;
        }
        await game.encounter_event(typeof(datum_orion_event_exploring_derelict));
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:191
    public override async DmTask emag_effect(obj_machinery_computer_arcade_orion_trail game = null, mob_living gamer = null)
    {
        return; //do nothing because this leads into an event where we actually will do something.
    }
}

// tg: code/game/machinery/computer/arcade/orion_event.dm:199
public partial class datum_orion_event_exploring_derelict : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:205
    public override async DmTask on_select(obj_machinery_computer_arcade_orion_trail game = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        await base.on_select(game, gamer_skill, gamer_skill_level, gamer_skill_rands);
        float sw__1 = rand(100);
        if (sw__1 >= 0 && sw__1 <= 14)
        {
            object rescued = game.add_crewmember();
            float oldfood = rand(1, 7);
            float oldfuel = rand(4, 10);
            game.food += oldfood;
            game.fuel += oldfuel;
            text = I18n.T("As you look through it you find some supplies and a living person! [] was rescued from the abandoned ship! You also found [] Food and [] Fuel.", rescued, oldfood, oldfuel);
            Dm.append(ref event_responses, BUTTON_WELCOME_ABOARD);
        }
        else if (sw__1 >= 15 && sw__1 <= 35)
        {
            float lostfuel = rand(4, 7);
            object deadname = game.remove_crewmember();
            game.fuel -= lostfuel;
            text = I18n.T("[] was lost deep in the wreckage, and your own vessel lost [] Fuel maneuvering to the abandoned ship.", deadname, lostfuel);
            Dm.append(ref event_responses, BUTTON_WHERE_DID_YOU_GO);
        }
        else if (sw__1 >= 36 && sw__1 <= 65)
        {
            float oldfood = rand(5, 11);
            game.food += oldfood;
            game.engine++;
            text = I18n.T("You found [] Food and some parts amongst the wreck.", oldfood);
            Dm.append(ref event_responses, BUTTON_A_GOOD_FIND);
        }
        else
        {
            text = I18n.T("As you look through the wreck you cannot find much of use.");
            Dm.append(ref event_responses, BUTTON_CONTINUE_TRAVELS);
        }
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:232
    public override async DmTask response(obj_machinery_computer_arcade_orion_trail game = null, object choice = null)
    {
        event_responses.Cut(); //so they don't pile up between games
        await base.response(game, choice);
    }
}

// tg: code/game/machinery/computer/arcade/orion_event.dm:238
public partial class datum_orion_event_raiders : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:243
    public override async DmTask on_select(obj_machinery_computer_arcade_orion_trail game = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        await base.on_select(game, gamer_skill, gamer_skill_level, gamer_skill_rands);
        text = I18n.T("Raiders have come aboard your ship! ");
        if (prob(50))
        {
            float sfood = rand(1, 10);
            float sfuel = rand(1, 10);
            game.food -= sfood;
            game.fuel -= sfuel;
            text += I18n.T("They have stolen [] Food and [] Fuel.", sfood, sfuel);
        }
        else if (prob(10))
        {
            object deadname = game.remove_crewmember();
            text += I18n.T("[] tried to fight back, but was killed.", deadname);
        }
        else
            text += I18n.T("Fortunately, you fended them off without any trouble.");
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:258
    public override async DmTask emag_effect(obj_machinery_computer_arcade_orion_trail game = null, mob_living gamer = null)
    {
        if (prob(50 - gamer_skill))
        {
            to_chat(usr, span_userdanger(I18n.T("You hear battle shouts. The tramping of boots on cold metal. Screams of agony. The rush of venting air. Are you going insane?")));
            Waiting.NoSleep(gamer.adjust_hallucinations(60 * SECONDS));
        }
        else
        {
            to_chat(usr, span_userdanger(I18n.T("Something strikes you from behind! It hurts like hell and feel like a blunt weapon, but nothing is there...")));
            await gamer.take_bodypart_damage(30);
            playsound(game, Dm.resource("sound/items/weapons/genhit2.ogg"), 100, TRUE);
        }
    }
}

// tg: code/game/machinery/computer/arcade/orion_event.dm:267
public partial class datum_orion_event_illness : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:273
    public override async DmTask on_select(obj_machinery_computer_arcade_orion_trail game = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        await base.on_select(game, gamer_skill, gamer_skill_level, gamer_skill_rands);
        object deadname = game.remove_crewmember();
        text = I18n.T("A deadly illness has been contracted! [] was killed by the disease.", deadname);
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:278
    public override async DmTask emag_effect(obj_machinery_computer_arcade_orion_trail game = null, mob_living gamer = null)
    {
        float maxSeverity = 3;
        if (gamer_skill_level >= SKILL_LEVEL_EXPERT)
            maxSeverity = 2; //part of gitting gud is rng mitigation
        float severity = rand(1, maxSeverity); //pray to RNGesus. PRAY, PIGS
        if (severity == 1)
        {
            to_chat(gamer, span_userdanger(I18n.T("You suddenly feel slightly nauseated.")));
            gamer.adjust_disgust(50);
        }
        if (severity == 2)
        {
            to_chat(usr, span_userdanger(I18n.T("You suddenly feel extremely nauseated and hunch over until it passes.")));
            gamer.adjust_disgust(110);
            await gamer.Stun(60);
        }
        if (severity >= 3) //you didn't pray hard enough
        {
            to_chat(gamer, span_warning(I18n.T("An overpowering wave of nausea consumes over you. You hunch over, your stomach's contents preparing for a spectacular exit.")));
            gamer.adjust_disgust(150); //max this bitch out so they barf a lot
            await gamer.Stun(100);
        }
    }
}

// tg: code/game/machinery/computer/arcade/orion_event.dm:300
public partial class datum_orion_event_flux : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:307
    public override async DmTask response(obj_machinery_computer_arcade_orion_trail game = null, object choice = null)
    {
        if (Dm.eq(choice, BUTTON_KEEP_SPEED))
        {
            if (prob(25))
            {
                await base.response(game, choice);
                return;
            }
            await game.encounter_event(typeof(datum_orion_event_engine_part));
        }
        else //Slow Down response
        {
            game.food = Dm.num(game.food) - (game.alive + game.lings_aboard) * ORION_SHORT_DELAY;
            game.fuel -= 5;
            await base.response(game, choice);
        }
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:317
    public override async DmTask emag_effect(obj_machinery_computer_arcade_orion_trail game = null, mob_living gamer = null)
    {
        if (prob(25 + gamer_skill)) //withstand the wind with your GAMER SKILL
        {
            to_chat(gamer, span_userdanger(I18n.T("A violent gale blows past you, and you barely manage to stay standing!")));
            return;
        }
        await gamer.Paralyze(60);
        await game.say(I18n.T("A sudden gust of powerful wind slams [] into the floor!", gamer));
        await gamer.take_bodypart_damage(25);
        playsound(game, Dm.resource("sound/items/weapons/genhit.ogg"), 100, TRUE);
    }
}

// tg: code/game/machinery/computer/arcade/orion_event.dm:326
public partial class datum_orion_event_changeling_infiltration : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:331
    public override async DmTask on_select(obj_machinery_computer_arcade_orion_trail game = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        await base.on_select(game, gamer_skill, gamer_skill_level, gamer_skill_rands);
        text = I18n.T("Strange reports warn of changelings infiltrating crews on trips to Orion...");
        if (game.settlers.len <= 2)
        {
            text += I18n.T(" But your crew's chance of reaching Orion is so slim the changelings likely avoided your ship...");
            if (prob(10)) // "likely", I didn't say it was guaranteed! //worst outcome too because changeling controls don't even enable
                game.lings_aboard = min(++game.lings_aboard, 2);
        }
        else
        {
            if (Dm.truthy(game.lings_aboard)) //less likely to stack lings
            {
                if (prob(20))
                    game.lings_aboard = min(++game.lings_aboard, 2);
            }
            else if (prob(70))
                game.lings_aboard = min(++game.lings_aboard, 2);

            text += I18n.T(" You have reasonable suspicion there may be a changeling. Some menu options have been changed.");
            game.lings_suspected = TRUE;
        }
    }
}

// tg: code/game/machinery/computer/arcade/orion_event.dm:350
public partial class datum_orion_event_changeling_attack : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:354
    public override async DmTask on_select(obj_machinery_computer_arcade_orion_trail game = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        await base.on_select(game, gamer_skill, gamer_skill_level, gamer_skill_rands);
        text = "";
        if (game.lings_aboard <= 0) //shouldn't trigger, but hey.
        {
            text = I18n.T("Haha, fooled you, there are no changelings on board! (You should report this to a coder :S )");
            return;
        }
        object ling1 = game.remove_crewmember();
        object ling2 = "";
        if (game.lings_aboard >= 2)
            ling2 = game.remove_crewmember();
        if (Dm.truthy(ling2))
            text = I18n.T(" [] and []'s arms twist and contort into grotesque blades!", ling1, ling2);
        else
            text = I18n.T(" []'s arm twists and contorts into a grotesque blade!", ling1);

        float chance2attack = game.alive * 20;
        if (!prob(chance2attack))
        {
            text += I18n.T(" [], the changeling[] vanish[] into space through the airlocks! You're safe... for now.",
                pick(I18n.T("Sensing unfavorable odds"), I18n.T("After a failed attack"), I18n.T("Suddenly breaking nerve")),
                Dm.truthy(ling2) ? "s" : "", Dm.truthy(ling2) ? "" : "es");
            if (Dm.truthy(ling2))
                game.lings_aboard = max(0, game.lings_aboard - 2);
            else
                game.lings_aboard = max(0, game.lings_aboard - 2);
            return;
        }

        float chancetokill = 30 * game.lings_aboard - (5 * game.alive); //eg: 30*2-(10) = 50%, 2 lings, 2 crew is 50% chance
        if (prob(chancetokill))
        {
            object deadguy = game.remove_crewmember();
            object murder_text = pick(
                I18n.T("The changeling[] bring[] down [] and disembowel[] them in a spray of gore!", Dm.truthy(ling2) ? "s" : "", Dm.truthy(ling2) ? "" : "s", deadguy, Dm.truthy(ling2) ? "" : "s"),
                I18n.T("[] corners [] and impales them through the stomach!", Dm.truthy(ling2) ? pick(ling1, ling2) : ling1, deadguy),
                I18n.T("[] decapitates [] in a single cleaving arc!", Dm.truthy(ling2) ? pick(ling1, ling2) : ling1, deadguy));
            text += Dm.text(" []", murder_text);
        }
        else
        {
            text += I18n.T(" You valiantly fight off the changeling[]!", Dm.truthy(ling2) ? "s" : "");
            if (Dm.truthy(ling2))
            {
                game.food += 30;
                game.lings_aboard = max(0, game.lings_aboard - 2);
            }
            else
            {
                game.food += 15;
                game.lings_aboard = max(0, game.lings_aboard - 1);
            }
            text += I18n.T(" Well, it's perfectly good food... You cut the changeling[] into meat, gaining [] Food!", Dm.truthy(ling2) ? "s" : "", Dm.truthy(ling2) ? "30" : "15");
        }
    }
}

///Black Hole - final  (emag can spawn singulo, see death event)
// tg: code/game/machinery/computer/arcade/orion_event.dm:404
public partial class datum_orion_event_black_hole : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:412
    public override async DmTask response(obj_machinery_computer_arcade_orion_trail game = null, object choice = null)
    {
        if (Dm.eq(choice, BUTTON_GO_AROUND))
        {
            game.food = Dm.num(game.food) - (game.alive + game.lings_aboard) * ORION_LONG_DELAY;
            game.fuel -= 15;
            game.turns += 1;
            await base.response(game, choice);
            return;
        }
        if (prob(75 - gamer_skill))
        {
            await game.encounter_event(typeof(datum_orion_event_black_hole_death), usr);
            return;
        }
        game.turns += 1;
        await base.response(game, choice);
    }
}

///You died to a black hole, have some fluff text
// tg: code/game/machinery/computer/arcade/orion_event.dm:427
public partial class datum_orion_event_black_hole_death : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:436
    public override async DmTask response(obj_machinery_computer_arcade_orion_trail game = null, object choice = null)
    {
        await game.set_game_over(usr as mob_living, I18n.T("You were swept away into the black hole."));
        await base.response(game, choice);
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:440
    public override async DmTask emag_effect(obj_machinery_computer_arcade_orion_trail game = null, mob_living gamer = null)
    {
        if (Dm.truthy(Dm.bitand(game.obj_flags, EMAGGED)))
        {
            playsound(game.loc as atom, Dm.resource("sound/effects/supermatter.ogg"), 100, TRUE);
            await game.say(I18n.T("A miniature black hole suddenly appears in front of [], devouring [] alive!", game, gamer));
            await gamer.Stun(200, ignore_canstun: TRUE); //you can't run :^)
            object black_hole = Dm.@new<obj_singularity_orion>(gamer.loc);
            _addtimer(Dm.@new<datum_callback>(game, nameof(atom_movable.say), I18n.T("[] winks out, just as suddenly as it appeared.", black_hole)), 5 * SECONDS, file: "code/game/machinery/computer/arcade/orion_event.dm", line: 447);
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 5 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(black_hole as datum) : black_hole), 5 * SECONDS, file: "code/game/machinery/computer/arcade/orion_event.dm", line: 448); // QDEL_IN(black_hole, 5 SECONDS), inline (rules 11.1 (f))
        }
    }
}

///You found a space port!
// tg: code/game/machinery/computer/arcade/orion_event.dm:455
public partial class datum_orion_event_space_port : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:463
    public override async DmTask on_select(obj_machinery_computer_arcade_orion_trail game = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        await base.on_select(game, gamer_skill, gamer_skill_level, gamer_skill_rands);
        //If your crew is pathetic you can get freebies (provided you haven't already gotten one from this port)
        if (Dm.num(game.fuel) > 20 && Dm.num(game.food) > 20) //but you don't need one
        {
            text = normal_arrival;
            return;
        }
        text = I18n.T("The workers at the Port are shocked at the state of your pioneers. With supplies that low, they take pity on you and your crew, generously giving you some free supplies! ");

        float pity_fuel = 10;
        float pity_food = 10;
        bool freecrew = prob(10);
        if (prob(30))
        {
            pity_fuel = 25;
            pity_fuel = 25;
        }
        if (freecrew)
            game.add_crewmember();
        text += I18n.T("(+[] fuel, +[] food)", pity_food, pity_fuel);
        if (freecrew)
            text += I18n.T(" A worker at the Port is inspired by your determination, and joins your crew!");
        game.fuel += pity_fuel;
        game.food += pity_food;
    }

    // tg: code/game/machinery/computer/arcade/orion_event.dm:490
    public override async DmTask response(obj_machinery_computer_arcade_orion_trail game = null, object choice = null)
    {
        game.gameStatus = ORION_STATUS_MARKET;
        await base.response(game, choice);
    }
}

///You raided a space port!
// tg: code/game/machinery/computer/arcade/orion_event.dm:505
public partial class datum_orion_event_space_port_raid : datum_orion_event
{
    // tg: code/game/machinery/computer/arcade/orion_event.dm:510
    public override async DmTask on_select(obj_machinery_computer_arcade_orion_trail game = null, object gamer_skill = null, object gamer_skill_level = null, object gamer_skill_rands = null)
    {
        await base.on_select(game, gamer_skill, gamer_skill_level, gamer_skill_rands);
        float success = min(15 * game.alive + Dm.num(gamer_skill), 100); //default crew (4) have a 60% chance
        game.spaceport_raided = TRUE;

        float fuel = 0;
        float food = 0;
        if (prob(success))
        {
            fuel = rand(5 + Dm.num(gamer_skill_rands), 15 + Dm.num(gamer_skill_rands));
            food = rand(5 + Dm.num(gamer_skill_rands), 15 + Dm.num(gamer_skill_rands));
            text = I18n.T("You successfully raided the spaceport! You gained [] Fuel and [] Food! (+[] fuel, +[] food)", fuel, food, fuel, food);
            usr?.mind?.adjust_experience(typeof(datum_skill_gaming), 10);
        }
        else
        {
            fuel = rand(-5, -15);
            food = rand(-5, -15);
            text = I18n.T("You failed to raid the spaceport! You lost [] Fuel and [] Food in your scramble to escape! ([] fuel, [] food)", fuel * -1, food * -1, fuel, food);

            //your chance of lose a crewmember is 1/2 your chance of success
            //this makes higher % failures hurt more, don't get cocky space cowboy!
            if (prob(success * 5))
            {
                object lost_crew = game.remove_crewmember();
                if (!Dm.truthy(game.settlers.len))
                {
                    await game.set_game_over(usr as mob_living, I18n.T("You were gunned down by space port security."));
                    await base.on_select(game, gamer_skill, gamer_skill_level, gamer_skill_rands);
                    return;
                }
                text = I18n.T("You failed to raid the spaceport! You lost [] Fuel and [] Food, AND [] in your scramble to escape! ([]fuel, [] food, -Crew)", fuel * -1, food * -1, lost_crew, fuel, food);
                if (Dm.truthy(Dm.bitand(game.obj_flags, EMAGGED)))
                {
                    await game.say(I18n.T("WEEWOO! WEEWOO! Spaceport security en route!"));
                    playsound(game, Dm.resource("sound/items/weeoo1.ogg"), 100, FALSE);
                    for (float i = 1; i <= 3; i++)
                    {
                        var spaceport_security = Dm.@new<mob_living_basic_trooper_syndicate_ranged_smg_orion>(get_turf(game));
                        (spaceport_security.ai_controller as datum_ai_controller).set_blackboard_key(BB_CURRENT_TARGET, usr);
                    }
                }
            }
        }
        game.fuel += fuel;
        game.food += food;
    }
}
