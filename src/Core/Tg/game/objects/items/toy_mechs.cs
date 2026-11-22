// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/toy_mechs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps TY1 (D333): the whole file. The Mini-Mecha action figures (a series of nineteen): played with in hand, they beep their battle
// cries; two of them knocked together, or a held one offered to another holder, fight a battle of spins, sparks and smashes that the
// controllers watch in chat until one falls apart, each toy's special move (heal, damage, utility or its own) charging below a third
// of its health; a battle cools both toys down for thirty seconds; and the suicide that no player wins. The var blocks are generated
// (D111).

/**
 * Mech prizes + MECHA COMBAT!!
 */
// tg: code/game/objects/items/toy_mechs.dm:15
public partial class obj_item_toy_mecha : obj_item_toy
{
    // tg: code/game/objects/items/toy_mechs.dm:5-12 (file-local, #undef at :651-655)
    /// Mech battle special attack types.
    internal const int SPECIAL_ATTACK_HEAL = 1;
    internal const int SPECIAL_ATTACK_DAMAGE = 2;
    internal const int SPECIAL_ATTACK_UTILITY = 3;
    internal const int SPECIAL_ATTACK_OTHER = 4;

    /// Max length of a mech battle
    internal const int MAX_BATTLE_LENGTH = 50;

    // tg: code/game/objects/items/toy_mechs.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_series), typeof(obj_item_toy_mecha), "Mini-Mecha action figures"));
        Waiting.NoSleep(AddElement(typeof(datum_element_toy_talk)));
        combat_health = max_combat_health;
        switch (special_attack_type)
        {
            case SPECIAL_ATTACK_DAMAGE:
                special_attack_type_message = "an aggressive move, which deals bonus damage.";
                break;
            case SPECIAL_ATTACK_HEAL:
                special_attack_type_message = "a defensive move, which grants bonus healing.";
                break;
            case SPECIAL_ATTACK_UTILITY:
                special_attack_type_message = "a utility move, which heals the user and damages the opponent.";
                break;
            case SPECIAL_ATTACK_OTHER:
                special_attack_type_message = Dm.text("a special move, which []", special_attack_type_message);
                break;
            default:
                special_attack_type_message = "a mystery move, even I don't know.";
                break;
        }
        return dot;
    }

    /**
     * this proc combines "sleep" while also checking for if the battle should continue
     *
     * this goes through some of the checks - the toys need to be next to each other to fight!
     * if it's player vs themself: They need to be able to "control" both mechs (either must be adjacent or using TK)
     * if it's player vs player: Both players need to be able to "control" their mechs (either must be adjacent or using TK)
     * if it's player vs mech (suicide): the mech needs to be in range of the player
     * if all the checks are TRUE, it does the sleeps, and returns TRUE. Otherwise, it returns FALSE.
     * Arguments:
     * * delay - the amount of time the sleep at the end of the check will sleep for
     * * attacker - the attacking toy in the battle.
     * * attacker_controller - the controller of the attacking toy. there should ALWAYS be an attacker_controller
     * * opponent - (optional) the defender controller in the battle, for PvP
     */
    // tg: code/game/objects/items/toy_mechs.dm:85
    public virtual async DmTask<object> combat_sleep(object delay = null, obj_item_toy_mecha attacker = null, mob_living_carbon attacker_controller = null, mob_living_carbon opponent = null)
    {
        if (attacker_controller == null)
            return FALSE;

        if (attacker == null) //if there's no attacker, then attacker_controller IS the attacker
        {
            if (!in_range(this, attacker_controller))
            {
                attacker_controller.visible_message(span_suicide(I18n.T("[] is running from []! The coward!", attacker_controller, this)));
                return FALSE;
            }
        }
        else // if there's an attacker, we can procede as normal
        {
            if (!in_range(this, attacker)) //and the two toys aren't next to each other, the battle ends
            {
                attacker_controller.visible_message(span_notice(I18n.T("[] and [] separate, ending the battle.", attacker, this)),
                    span_notice(I18n.T("[] and [] separate, ending the battle.", attacker, this)));
                return FALSE;
            }

            //dead men tell no tales, incapacitated men fight no fights
            if (Dm.truthy(attacker_controller.incapacitated))
                return FALSE;
            //if the attacker_controller isn't next to the attacking toy (and doesn't have telekinesis), the battle ends
            if (!in_range(attacker, attacker_controller) && !Dm.truthy(attacker_controller.dna.check_mutation(typeof(datum_mutation_telekinesis))))
            {
                attacker_controller.visible_message(span_notice(I18n.T("[] separates from [], ending the battle.", attacker_controller.name, attacker)),
                    span_notice(I18n.T("You separate from [], ending the battle.", attacker)));
                return FALSE;
            }

            //if it's PVP and the opponent is not next to the defending(src) toy (and doesn't have telekinesis), the battle ends
            if (opponent != null)
            {
                if (Dm.truthy(opponent.incapacitated))
                    return FALSE;
                if (!in_range(this, opponent) && !Dm.truthy(opponent.dna.check_mutation(typeof(datum_mutation_telekinesis))))
                {
                    opponent.visible_message(span_notice(I18n.T("[] separates from [], ending the battle.", opponent.name, this)),
                        span_notice(I18n.T("You separate from [], ending the battle.", this)));
                    return FALSE;
                }
            }
            //if it's not PVP and the attacker_controller isn't next to the defending toy (and doesn't have telekinesis), the battle ends
            else
            {
                if (!in_range(this, attacker_controller) && !Dm.truthy(attacker_controller.dna.check_mutation(typeof(datum_mutation_telekinesis))))
                {
                    attacker_controller.visible_message(span_notice(I18n.T("[] separates from [] and [], ending the battle.", attacker_controller.name, this, attacker)),
                        span_notice(I18n.T("You separate [] and [], ending the battle.", attacker, this)));
                    return FALSE;
                }
            }
        }

        //if all that is good, then we can sleep peacefully
        await sleep(Dm.num(delay));
        return TRUE;
    }

    //all credit to skasi for toy mech fun ideas
    // tg: code/game/objects/items/toy_mechs.dm:128
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = null;
        if (Dm.num(timer) < world.time)
        {
            to_chat(user, span_notice(I18n.T("You play with [].", this)));
            timer = world.time + cooldown;
            if (!Dm.truthy(quiet))
                playsound(user, Dm.resource("sound/vehicles/mecha/mechstep.ogg"), 20, TRUE);
        }
        else
        {
            dot = await base.attack_self(user, modifiers, __extra);
        }
        return dot;
    }

    // tg: code/game/objects/items/toy_mechs.dm:137
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (loc == user)
            await attack_self(user);
        return dot;
    }

    /**
     * If you attack a mech with a mech, initiate combat between them
     */
    // tg: code/game/objects/items/toy_mechs.dm:147
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_toy_mecha)
            return NONE;
        obj_item_toy_mecha challenger = tool as obj_item_toy_mecha;
        if (Dm.truthy(check_battle_start(user as mob_living_carbon, challenger)))
            await mecha_brawl(challenger, user as mob_living_carbon);
        return null;
    }

    /**
     * Attack is called from the user's toy, aimed at target(another human), checking for target's toy.
     */
    // tg: code/game/objects/items/toy_mechs.dm:157
    [DmParams("target", "user")]
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (target == user)
        {
            to_chat(user, span_notice(I18n.T("Target another toy mech if you want to start a battle with yourself.")));
            return null;
        }
        else if (!Dm.truthy(user.combat_mode))
        {
            if (Dm.truthy(wants_to_battle)) //prevent spamming someone with offers
            {
                to_chat(user, span_notice(I18n.T("You already are offering battle to someone!")));
                return null;
            }
            if (!Dm.truthy(check_battle_start(user as mob_living_carbon))) //if the user's mech isn't ready, don't bother checking
                return null;

            foreach (obj_item I in Dm.each<obj_item>(target.held_items))
            {
                if (I is obj_item_toy_mecha) //if you attack someone with a mech who's also holding a mech, offer to battle them
                {
                    obj_item_toy_mecha P = I as obj_item_toy_mecha;
                    if (!Dm.truthy(P.check_battle_start(target as mob_living_carbon, null, user as mob_living_carbon))) //check if the attacker mech is ready
                        break;

                    //slap them with the metaphorical white glove
                    if (Dm.truthy(P.wants_to_battle)) //if the target mech wants to battle, initiate the battle from their POV
                    {
                        await mecha_brawl(P, target as mob_living_carbon, user as mob_living_carbon); //P = defender's mech / SRC = attacker's mech / target = defender / user = attacker
                        P.wants_to_battle = FALSE;
                        return null;
                    }
                }
            }

            //extend the offer of battle to the other mech
            to_chat(user, span_notice(I18n.T("You offer battle to []!", target.name)));
            to_chat(target, span_notice(I18n.T("<b>[] wants to battle with [] []!</b> <i>Attack them with a toy mech to initiate combat.</i>", user.name, user.p_their(), name)));
            wants_to_battle = TRUE;
            _addtimer(Dm.@new<datum_callback>(this, nameof(withdraw_offer), user), 6 * SECONDS, file: "code/game/objects/items/toy_mechs.dm", line: 184);
            return null;
        }

        await base.attack__proc(target, user, modifiers, attack_modifiers, __extra);
        return null;
    }

    /**
     * Overrides attack_tk - Sorry, you have to be face to face to initiate a battle, it's good sportsmanship
     */
    // tg: code/game/objects/items/toy_mechs.dm:192
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (Dm.num(timer) < world.time)
        {
            to_chat(user, span_notice(I18n.T("You telekinetically play with [].", this)));
            timer = world.time + cooldown;
            if (!Dm.truthy(quiet))
                playsound(user, Dm.resource("sound/vehicles/mecha/mechstep.ogg"), 20, TRUE);
        }
        await DmTask.CompletedTask;
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /**
     * Resets the request for battle.
     *
     * For use in a timer, this proc resets the wants_to_battle variable after a short period.
     * Arguments:
     * * user - the user wanting to do battle
     */
    // tg: code/game/objects/items/toy_mechs.dm:208
    public virtual void withdraw_offer(mob_living_carbon user = null)
    {
        if (Dm.truthy(wants_to_battle))
        {
            wants_to_battle = FALSE;
            to_chat(user, span_notice(I18n.T("You get the feeling they don't want to battle.")));
        }
    }

    /**
     * Starts a battle, toy mech vs player. Player... doesn't win.
     */
    // tg: code/game/objects/items/toy_mechs.dm:215
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.truthy(in_combat))
        {
            to_chat(user, span_notice(I18n.T("[] is in battle, let it finish first.", this)));
            return null;
        }

        user.visible_message(span_suicide(I18n.T("[] begins a fight [] can't win with []! It looks like [] trying to commit suicide!", user, user.p_they(), this, user.p_theyre())));

        in_combat = TRUE;
        await sleep(1.5f * SECONDS);
        for (float i = 1, i__end = 4; i <= i__end; i++)
        {
            switch (i)
            {
                case 1 or 3:
                    SpinAnimation(5, 0);
                    playsound(this, Dm.resource("sound/vehicles/mecha/mechstep.ogg"), 30, TRUE);
                    await user.adjust_brute_loss(25);
                    await user.adjust_stamina_loss(50);
                    break;
                case 2:
                    user.SpinAnimation(5, 0);
                    playsound(user, Dm.resource("sound/items/weapons/smash.ogg"), 20, TRUE);
                    combat_health--; //we scratched it!
                    break;
                case 4:
                    await say(special_attack_cry + "!!");
                    await user.adjust_stamina_loss(25);
                    break;
            }

            if (!Dm.truthy(await combat_sleep(1 * SECONDS, null, user as mob_living_carbon)))
            {
                await say(I18n.T("PATHETIC."));
                combat_health = max_combat_health;
                in_combat = FALSE;
                return SHAME;
            }
        }

        await sleep(0.5f * SECONDS);
        await user.adjust_brute_loss(450);

        in_combat = FALSE;
        await say(I18n.T("AN EASY WIN. MY POWER INCREASES.")); // steal a soul, become swole
        await add_atom_colour(rgb(255, 115, 115), ADMIN_COLOUR_PRIORITY);
        max_combat_health = round(max_combat_health * 1.5f + 0.1f);
        combat_health = max_combat_health;
        wins++;
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/toy_mechs.dm:256
    [DmParams()]
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("This toy's special attack is [], []", special_attack_cry, special_attack_type_message)));
        if (Dm.truthy(in_combat))
        {
            Dm.append(ref dot, span_notice(I18n.T("This toy has a maximum health of []. Currently, it's [].", max_combat_health, combat_health)));
            Dm.append(ref dot, span_notice(I18n.T("Its special move light is []", Dm.truthy(special_attack_cooldown) ? I18n.T("flashing red.") : I18n.T("green and is ready!"))));
        }
        else
        {
            Dm.append(ref dot, span_notice(I18n.T("This toy has a maximum health of [].", max_combat_health)));
        }

        if (Dm.truthy(wins) || Dm.truthy(losses))
            Dm.append(ref dot, span_notice(I18n.T("This toy has [] wins, and [] losses.", wins, losses)));
        return dot;
    }

    // tg: code/game/objects/items/toy_mechs.dm:268
    public override object can_speak__proc(float? allow_mimes = null)
    {
        return (Dm.not(quiet) is var and__1 && !Dm.truthy(and__1) ? and__1 : base.can_speak__proc(allow_mimes));
    }

    /**
     * The 'master' proc of the mech battle. Processes the entire battle's events and makes sure it start and finishes correctly.
     *
     * src is the defending toy, and the battle proc is called on it to begin the battle.
     * After going through a few checks at the beginning to ensure the battle can start properly, the battle begins a loop that lasts
     * until either toy has no more health. During this loop, it also ensures the mechs stay in combat range of each other.
     * It will then randomly decide attacks for each toy, occasionally making one or the other use their special attack.
     * When either mech has no more health, the loop ends, and it displays the victor and the loser while updating their stats and resetting them.
     * Arguments:
     * * attacker - the attacking toy, the toy in the attacker_controller's hands
     * * attacker_controller - the user, the one who is holding the toys / controlling the fight
     * * opponent - optional arg used in Mech PvP battles: the other person who is taking part in the fight (controls src)
     */
    // tg: code/game/objects/items/toy_mechs.dm:284
    public virtual async DmTask mecha_brawl(obj_item_toy_mecha attacker = null, mob_living_carbon attacker_controller = null, mob_living_carbon opponent = null)
    {
        //A GOOD DAY FOR A SWELL BATTLE!
        attacker_controller.visible_message(span_danger(I18n.T("[] collides [] with []! Looks like they're preparing for a brawl!", attacker_controller.name, attacker, this)),
            span_danger(I18n.T("You collide [] into [], sparking a fierce battle!", attacker, this)),
            span_hear(I18n.T("You hear hard plastic smacking into hard plastic.")), COMBAT_MESSAGE_RANGE);

        /// Who's in control of the defender (src)?
        mob_living_carbon src_controller = (opponent != null) ? opponent : attacker_controller;
        /// How long has the battle been going?
        float battle_length = 0;

        in_combat = TRUE;
        attacker.in_combat = TRUE;

        //1.5 second cooldown * 20 = 30 second cooldown after a fight
        timer = world.time + cooldown * cooldown_multiplier;
        attacker.timer = world.time + attacker.cooldown * attacker.cooldown_multiplier;

        await sleep(1 * SECONDS);
        //--THE BATTLE BEGINS--
        while (combat_health > 0 && attacker.combat_health > 0 && battle_length < MAX_BATTLE_LENGTH)
        {
            if (!Dm.truthy(await combat_sleep(0.5f * SECONDS, attacker, attacker_controller, opponent))) //combat_sleep checks everything we need to have checked for combat to continue
                break;

            //before we do anything - deal with charged attacks
            if (Dm.truthy(special_attack_charged))
            {
                src_controller.visible_message(span_danger(I18n.T("[] unleashes its special attack!!", this)),
                    span_danger(I18n.T("You unleash []'s special attack!", this)));
                await special_attack_move(attacker);
            }
            else if (Dm.truthy(attacker.special_attack_charged))
            {
                attacker_controller.visible_message(span_danger(I18n.T("[] unleashes its special attack!!", attacker)),
                    span_danger(I18n.T("You unleash []'s special attack!", attacker)));
                await attacker.special_attack_move(this);
            }
            else
            {
                //process the cooldowns
                if (special_attack_cooldown > 0)
                    special_attack_cooldown--;
                if (attacker.special_attack_cooldown > 0)
                    attacker.special_attack_cooldown--;

                //combat commences
                switch (rand(1, 8))
                {
                    case >= 1 and <= 3: //attacker wins
                        if (attacker.special_attack_cooldown == 0 && attacker.combat_health <= round(attacker.max_combat_health / 3)) //if health is less than 1/3 and special off CD, use it
                        {
                            attacker.special_attack_charged = TRUE;
                            attacker_controller.visible_message(span_danger(I18n.T("[] begins charging its special attack!!", attacker)),
                                span_danger(I18n.T("You begin charging []'s special attack!", attacker)));
                        }
                        else //just attack
                        {
                            attacker.SpinAnimation(5, 0);
                            playsound(attacker, Dm.resource("sound/vehicles/mecha/mechstep.ogg"), 30, TRUE);
                            combat_health--;
                            attacker_controller.visible_message(span_danger(I18n.T("[] devastates []!", attacker, this)),
                                span_danger(I18n.T("You ram [] into []!", attacker, this)),
                                span_hear(I18n.T("You hear hard plastic smacking hard plastic.")), COMBAT_MESSAGE_RANGE);
                            if (prob(5))
                            {
                                combat_health--;
                                playsound(this, Dm.resource("sound/effects/meteorimpact.ogg"), 20, TRUE);
                                attacker_controller.visible_message(span_boldwarning(I18n.T("...and lands a CRIPPLING BLOW!")),
                                    span_boldwarning(I18n.T("...and you land a CRIPPLING blow on []!", this)), null, COMBAT_MESSAGE_RANGE);
                            }
                        }
                        break;

                    case 4: //both lose
                        attacker.SpinAnimation(5, 0);
                        SpinAnimation(5, 0);
                        combat_health--;
                        attacker.combat_health--;
                        do_sparks(2, FALSE, this);
                        do_sparks(2, FALSE, attacker);
                        if (prob(50))
                        {
                            attacker_controller.visible_message(span_danger(I18n.T("[] and [] clash dramatically, causing sparks to fly!", attacker, this)),
                                span_danger(I18n.T("[] and [] clash dramatically, causing sparks to fly!", attacker, this)),
                                span_hear(I18n.T("You hear hard plastic rubbing against hard plastic.")), COMBAT_MESSAGE_RANGE);
                        }
                        else
                        {
                            src_controller.visible_message(span_danger(I18n.T("[] and [] clash dramatically, causing sparks to fly!", this, attacker)),
                                span_danger(I18n.T("[] and [] clash dramatically, causing sparks to fly!", this, attacker)),
                                span_hear(I18n.T("You hear hard plastic rubbing against hard plastic.")), COMBAT_MESSAGE_RANGE);
                        }
                        break;
                    case 5: //both win
                        playsound(attacker, Dm.resource("sound/items/weapons/parry.ogg"), 20, TRUE);
                        if (prob(50))
                        {
                            attacker_controller.visible_message(span_danger(I18n.T("[]'s attack deflects off of [].", this, attacker)),
                                span_danger(I18n.T("[]'s attack deflects off of [].", this, attacker)),
                                span_hear(I18n.T("You hear hard plastic bouncing off hard plastic.")), COMBAT_MESSAGE_RANGE);
                        }
                        else
                        {
                            src_controller.visible_message(span_danger(I18n.T("[]'s attack deflects off of [].", attacker, this)),
                                span_danger(I18n.T("[]'s attack deflects off of [].", attacker, this)),
                                span_hear(I18n.T("You hear hard plastic bouncing off hard plastic.")), COMBAT_MESSAGE_RANGE);
                        }
                        break;

                    case >= 6 and <= 8: //defender wins
                        if (special_attack_cooldown == 0 && combat_health <= round(max_combat_health / 3)) //if health is less than 1/3 and special off CD, use it
                        {
                            special_attack_charged = TRUE;
                            src_controller.visible_message(span_danger(I18n.T("[] begins charging its special attack!!", this)),
                                span_danger(I18n.T("You begin charging []'s special attack!", this)));
                        }
                        else //just attack
                        {
                            SpinAnimation(5, 0);
                            playsound(this, Dm.resource("sound/vehicles/mecha/mechstep.ogg"), 30, TRUE);
                            attacker.combat_health--;
                            src_controller.visible_message(span_danger(I18n.T("[] smashes []!", this, attacker)),
                                span_danger(I18n.T("You smash [] into []!", this, attacker)),
                                span_hear(I18n.T("You hear hard plastic smashing hard plastic.")), COMBAT_MESSAGE_RANGE);
                            if (prob(5))
                            {
                                attacker.combat_health--;
                                playsound(attacker, Dm.resource("sound/effects/meteorimpact.ogg"), 20, TRUE);
                                src_controller.visible_message(span_boldwarning(I18n.T("...and lands a CRIPPLING BLOW!")),
                                    span_boldwarning(I18n.T("...and you land a CRIPPLING blow on []!", attacker)), null, COMBAT_MESSAGE_RANGE);
                            }
                        }
                        break;
                    default:
                        attacker_controller.visible_message(span_notice(I18n.T("[] and [] stand around awkwardly.", this, attacker)),
                            span_notice(I18n.T("You don't know what to do next.")));
                        break;
                }
            }

            battle_length++;
            await sleep(0.5f * SECONDS);
        }

        /// Lines chosen for the winning mech
        DmList winlines = Dm.list(I18n.T("YOU'RE NOTHING BUT SCRAP!"), I18n.T("I'LL YIELD TO NONE!"), I18n.T("GLORY IS MINE!"), I18n.T("AN EASY FIGHT."), I18n.T("YOU SHOULD HAVE NEVER FACED ME."), I18n.T("ROCKED AND SOCKED."));

        if (attacker.combat_health <= 0 && combat_health <= 0) //both lose
        {
            playsound(this, Dm.resource("sound/machines/warning-buzzer.ogg"), 20, TRUE);
            attacker_controller.visible_message(span_boldnotice(I18n.T("MUTUALLY ASSURED DESTRUCTION!! [] and [] both end up destroyed!", this, attacker)),
                span_boldnotice(I18n.T("Both [] and [] are destroyed!", this, attacker)));
        }
        else if (attacker.combat_health <= 0) //src wins
        {
            wins++;
            attacker.losses++;
            playsound(attacker, Dm.resource("sound/effects/light_flicker.ogg"), 20, TRUE);
            attacker_controller.visible_message(span_notice(I18n.T("[] falls apart!", attacker)),
                span_notice(I18n.T("[] falls apart!", attacker)), null, COMBAT_MESSAGE_RANGE);
            await say(Dm.text("[]", pick(winlines)));
            src_controller.visible_message(span_notice(I18n.T("[] destroys [] and walks away victorious!", this, attacker)),
                span_notice(I18n.T("You raise up [] victoriously over []!", this, attacker)));
        }
        else if (combat_health <= 0) //attacker wins
        {
            attacker.wins++;
            losses++;
            playsound(this, Dm.resource("sound/effects/light_flicker.ogg"), 20, TRUE);
            src_controller.visible_message(span_notice(I18n.T("[] collapses!", this)),
                span_notice(I18n.T("[] collapses!", this)), null, COMBAT_MESSAGE_RANGE);
            await attacker.say(Dm.text("[]", pick(winlines)));
            attacker_controller.visible_message(span_notice(I18n.T("[] demolishes [] and walks away victorious!", attacker, this)),
                I18n.T("[]!", span_notice(I18n.T("You raise up [] proudly over []", attacker, this))));
        }
        else //both win?
        {
            await say(I18n.T("NEXT TIME."));
            //don't want to make this a one sided conversation
            if (Dm.truthy(quiet))
                await attacker.say(I18n.T("I WENT EASY ON YOU."));
            else
                await attacker.say(I18n.T("OF COURSE."));
        }

        in_combat = FALSE;
        attacker.in_combat = FALSE;

        combat_health = max_combat_health;
        attacker.combat_health = attacker.max_combat_health;

        return;
    }

    /**
     * This proc checks if a battle can be initiated between src and attacker.
     *
     * Both SRC and attacker (if attacker is included) timers are checked if they're on cooldown, and
     * both SRC and attacker (if attacker is included) are checked if they are in combat already.
     * If any of the above are true, the proc returns FALSE and sends a message to user (and target, if included) otherwise, it returns TRUE
     * Arguments:
     * * user: the user who is initiating the battle
     * * attacker: optional arg for checking two mechs at once
     * * target: optional arg used in Mech PvP battles (if used, attacker is target's toy)
     */
    // tg: code/game/objects/items/toy_mechs.dm:444
    public virtual object check_battle_start(mob_living_carbon user = null, obj_item_toy_mecha attacker = null, mob_living_carbon target = null)
    {
        if (Dm.truthy(attacker?.in_combat))
        {
            to_chat(user, span_notice(I18n.T("[] [] is in combat.", target != null ? target.p_their() : I18n.T("Your"), attacker.name)));
            to_chat(target, span_notice(I18n.T("Your [] is in combat.", attacker.name)));
            return FALSE;
        }
        if (Dm.truthy(in_combat))
        {
            to_chat(user, span_notice(I18n.T("Your [] is in combat.", name)));
            to_chat(target, span_notice(I18n.T("[] [] is in combat.", user.p_their(), name)));
            return FALSE;
        }
        if (attacker != null && Dm.num(attacker.timer) > world.time)
        {
            to_chat(user, span_notice(I18n.T("[] [] isn't ready for battle.", target != null ? target.p_their() : I18n.T("Your"), attacker.name)));
            to_chat(target, span_notice(I18n.T("Your [] isn't ready for battle.", attacker.name)));
            return FALSE;
        }
        if (Dm.num(timer) > world.time)
        {
            to_chat(user, span_notice(I18n.T("Your [] isn't ready for battle.", name)));
            to_chat(target, span_notice(I18n.T("[] [] isn't ready for battle.", user.p_their(), name)));
            return FALSE;
        }

        return TRUE;
    }

    /**
     * Processes any special attack moves that happen in the battle (called in the mechaBattle proc).
     *
     * Makes the toy shout their special attack cry and updates its cooldown. Then, does the special attack.
     * Arguments:
     * * victim - the toy being hit by the special move
     */
    // tg: code/game/objects/items/toy_mechs.dm:471
    public virtual async DmTask special_attack_move(obj_item_toy_mecha victim = null)
    {
        await say(special_attack_cry + "!!");

        special_attack_charged = FALSE;
        special_attack_cooldown = 3;

        switch (special_attack_type)
        {
            case SPECIAL_ATTACK_DAMAGE: //+2 damage
                victim.combat_health -= 2;
                playsound(this, Dm.resource("sound/items/weapons/marauder.ogg"), 20, TRUE);
                break;
            case SPECIAL_ATTACK_HEAL: //+2 healing
                combat_health += 2;
                playsound(this, Dm.resource("sound/vehicles/mecha/mech_shield_raise.ogg"), 20, TRUE);
                break;
            case SPECIAL_ATTACK_UTILITY: //+1 heal, +1 damage
                victim.combat_health--;
                combat_health++;
                playsound(this, Dm.resource("sound/vehicles/mecha/mechmove01.ogg"), 30, TRUE);
                break;
            case SPECIAL_ATTACK_OTHER: //other
                await super_special_attack(victim);
                break;
            default:
                await say(I18n.T("I FORGOT MY SPECIAL ATTACK..."));
                break;
        }
    }

    /**
     * Base proc for 'other' special attack moves.
     *
     * This one is only for inheritance, each mech with an 'other' type move has their procs below.
     * Arguments:
     * * victim - the toy being hit by the super special move (doesn't necessarily need to be used)
     */
    // tg: code/game/objects/items/toy_mechs.dm:500
    public virtual async DmTask super_special_attack(obj_item_toy_mecha victim = null)
    {
        visible_message(span_notice(I18n.T("[] does a cool flip.", this)));
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/items/toy_mechs.dm:567
public partial class obj_item_toy_mecha_honk : obj_item_toy_mecha
{
    // tg: code/game/objects/items/toy_mechs.dm:574
    public override async DmTask super_special_attack(obj_item_toy_mecha victim = null)
    {
        playsound(this, Dm.resource("sound/mobs/non-humanoids/honkbot/honkbot_evil_laugh.ogg"), 20, TRUE);
        victim.special_attack_cooldown += 3; //Adds cooldown to the other mech and gives a minor self heal
        combat_health++;
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/items/toy_mechs.dm:601
public partial class obj_item_toy_mecha_deathripley : obj_item_toy_mecha
{
    // tg: code/game/objects/items/toy_mechs.dm:608
    public override async DmTask super_special_attack(obj_item_toy_mecha victim = null)
    {
        playsound(this, Dm.resource("sound/items/weapons/sonic_jackhammer.ogg"), 20, TRUE);
        if (victim.combat_health < combat_health) //Instantly kills the other mech if its health is below ours.
        {
            await say(I18n.T("EXECUTE!!"));
            victim.combat_health = 0;
        }
        else //Otherwise, just deal one damage.
        {
            victim.combat_health--;
        }
    }
}

// tg: code/game/objects/items/toy_mechs.dm:617
public partial class obj_item_toy_mecha_reticence : obj_item_toy_mecha
{
    // tg: code/game/objects/items/toy_mechs.dm:625
    public override async DmTask super_special_attack(obj_item_toy_mecha victim = null)
    {
        special_attack_cooldown--; //Has a lower cooldown...
        victim.special_attack_cooldown++; //and increases the opponent's cooldown by 1...
        victim.combat_health--; //and some free damage.
        await DmTask.CompletedTask;
    }
}
