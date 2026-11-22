// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/toys.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the toy base (floor placeable toys), the balloons (gaps J2, D298; the syndicate balloon: syndicate G3b) and gaps TY1
// (D333) for the rest: the water balloon, the Captain's Aid, the toy singularities and their suicide, the cap gun and its caps, the toy
// sword (its blade's colour, the rainbow hack, two joined into the double-bladed toy), the windup toolbox's rumble, the replica katana,
// the snap pops (and the phoenix's ash that makes another), the talking toys and their pull-string lines (the owl's and griffin's
// chatter), the toy nuke, the mini-meteor, the big red button, the snowball, the steampunk watch, the xenomorph figure, the action
// figures, the ventriloquist dummy, the seashells, the broken radio, the squeaky brain, the toy Codex Cicatrix, the foam finger, the
// Intento, the extendo-hand and the banhammer. The var blocks are generated (D111). Inline chat icons (icon2html) are not drawn: the
// port's chat is text (as ion.cs). The Intento's award is an achievement (D332).

// tg: code/game/objects/items/toys.dm:32
public partial class obj_item_toy : obj_item
{
    // tg: code/game/objects/items/toys.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(floor_placeable))
            Waiting.NoSleep(AddElement(typeof(datum_element_floor_placeable)));
        return dot;
    }
}

/*
 * Balloons
 */
// tg: code/game/objects/items/toys.dm:51
public partial class obj_item_toy_waterballoon : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        create_reagents(10);
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:63
    [DmParams("M", "user")]
    public override async DmTask<object> attack__proc(mob_living M = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/items/toys.dm:66
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is not obj_structure_reagent_dispensers)
            return NONE;
        obj_structure_reagent_dispensers RD = interacting_with as obj_structure_reagent_dispensers;
        if (((datum_reagents)RD.reagents).total_volume <= 0)
        {
            to_chat(user, span_warning(I18n.T("[] is empty.", RD)));
        }
        else if (((datum_reagents)reagents).total_volume >= 10)
        {
            to_chat(user, span_warning(I18n.T("[] is full.", this)));
        }
        else
        {
            await ((datum_reagents)interacting_with.reagents).trans_to(this, 10, transferred_by: user);
            to_chat(user, span_notice(I18n.T("You fill the balloon with the contents of [].", interacting_with)));
            desc = I18n.T("A translucent balloon with some form of liquid sloshing around in it.");
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/items/toys.dm:82
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_reagent_containers_cup && tool.reagents != null)
        {
            if (((datum_reagents)tool.reagents).total_volume <= 0)
            {
                to_chat(user, span_warning(I18n.T("[] is empty.", tool)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (((datum_reagents)reagents).total_volume >= 10)
            {
                to_chat(user, span_warning(I18n.T("[] is full.", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            desc = I18n.T("A translucent balloon with some form of liquid sloshing around in it.");
            to_chat(user, span_notice(I18n.T("You fill the balloon with the contents of [].", tool)));
            await ((datum_reagents)tool.reagents).trans_to(this, 10, transferred_by: user);
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(tool.get_sharpness()))
        {
            await balloon_burst();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/toys.dm:104  (set waitfor = FALSE, inherited from /atom/movable/throw_impact: the wrapper of
    // waiting-model.md 5.4; balloon_burst may sleep)
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(throw_impact__body(hit_atom, throwingdatum, __extra), null); // `.` is never assigned: no box (5.4)
    }

    private async DmTask<object> throw_impact__body(atom hit_atom, datum_thrownthing throwingdatum, object[] __extra)
    {
        if (!Dm.truthy(base.throw_impact(hit_atom, throwingdatum, __extra))) //was it caught by a mob?
            await balloon_burst(hit_atom);
        return null;
    }

    // tg: code/game/objects/items/toys.dm:108
    public virtual async DmTask balloon_burst(atom AT = null)
    {
        if (((datum_reagents)reagents).total_volume >= 1)
        {
            turf T = null;
            if (AT != null)
                T = get_turf(AT);
            else
                T = get_turf(this);
            T.visible_message(span_danger(I18n.T("[] bursts!", this)), span_hear(I18n.T("You hear a pop and a splash.")));
            await ((datum_reagents)reagents).expose(T);
            foreach (atom A in Dm.each<atom>(T))
                await ((datum_reagents)reagents).expose(A);
            icon_state = "burst";
            qdel(this);
        }
    }

    // tg: code/game/objects/items/toys.dm:122
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (((datum_reagents)reagents).total_volume >= 1)
        {
            icon_state = "waterballoon";
            inhand_icon_state = "balloon";
        }
        else
        {
            icon_state = "balloon_red-e";
            inhand_icon_state = "balloon-empty";
        }
        return await base.update_icon_state(__extra);
    }
}

// tg: code/game/objects/items/toys.dm:134
public partial class obj_item_toy_balloon : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:201
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_ammo_casing_foam_dart || !HAS_TRAIT(user, TRAIT_SIMIAN))
            return NONE;
        pop_balloon(monkey_pop: TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/toys.dm:207
    public override async DmTask<object> hitby(atom_movable AM = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        mob thrower = throwingdatum?.get_thrower() as mob;
        if (HAS_TRAIT(thrower, TRAIT_SIMIAN) && AM is obj_item_ammo_casing_foam_dart)
        {
            pop_balloon(monkey_pop: TRUE);
            return null;
        }
        return await base.hitby(AM, skipcatch, hitpush, blocked, throwingdatum);
    }

    // tg: code/game/objects/items/toys.dm:214
    public override async DmTask<object> bullet_act(obj_projectile proj = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        if ((proj is obj_projectile_bullet_p50 || proj is obj_projectile_bullet_foam_dart) && HAS_TRAIT(proj.firer as datum, TRAIT_SIMIAN))
        {
            pop_balloon(monkey_pop: TRUE);
            return BULLET_ACT_HIT;
        }
        return await base.bullet_act(proj, def_zone, piercing_hit, blocked);
    }

    // tg: code/game/objects/items/toys.dm:220
    public virtual void pop_balloon(float? monkey_pop = null)
    {
        playsound(this, Dm.resource("sound/effects/cartoon_sfx/cartoon_pop.ogg"), 50, vary: TRUE);
        if (Dm.truthy(monkey_pop)) // Monkeys make money from popping bloons
            Dm.@new<obj_item_coin_iron>(get_turf(this));
        qdel(this);
    }

    // tg: code/game/objects/items/toys.dm:226
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        if (!Dm.truthy(random_color))
            return dot;
        current_color = pick("red", "blue", "green", "yellow", "orange", "purple"); // BALLOON_COLORS
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:234
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = Dm.text("[][]", Dm.truthy(current_color) ? Dm.text("[] ", current_color) : null, Dm.initial(this, "name"));
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:238
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(current_color)))
            update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:243
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        string new_icon = Dm.text("[][]", Dm.initial(this, "icon_state"), Dm.truthy(current_color) ? Dm.text("_[]", current_color) : null);
        inhand_icon_state = new_icon;
        icon_state = Dm.text("[][]", new_icon, isturf(loc) ? null : "_storage");
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:249
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/items/toys.dm:152
public partial class obj_item_toy_balloon_long : obj_item_toy_balloon
{
    // tg: code/game/objects/items/toys.dm:177
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_toy_balloon_long || !HAS_TRAIT(user, TRAIT_BALLOON_SUTRA))
            return await base.item_interaction(user, tool, modifiers, __extra);

        obj_item_toy_balloon_long hit_by = tool as obj_item_toy_balloon_long;
        if (Dm.eq(hit_by.current_color, current_color))
        {
            to_chat(user, span_warning(I18n.T("You must use balloons of different colours to do that!")));
            return ITEM_INTERACT_BLOCKING;
        }
        user.visible_message(
            span_notice(I18n.T("[] starts contorting up a balloon animal!", user.name)),
            span_notice(I18n.T("You start twisting together a balloon animal!")),
            span_hear(I18n.T("You hear balloons being contorted.")),
            vision_distance: 3,
            ignored_mobs: user);
        foreach (DmList pair_of_colors in Dm.each<DmList>(balloon_combos))
        {
            if ((Dm.eq(hit_by.current_color, pair_of_colors[1]) && Dm.eq(current_color, pair_of_colors[2])) || (Dm.eq(current_color, pair_of_colors[1]) && Dm.eq(hit_by.current_color, pair_of_colors[2])))
            {
                Type path_to_spawn = balloon_combos[pair_of_colors] as Type;
                await user.put_in_hands(Dm.@new(path_to_spawn) as obj_item);
                break;
            }
        }
        qdel(hit_by);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/toys.dm:268
public partial class obj_item_toy_balloon_syndicate : obj_item_toy_balloon
{
    // tg: code/game/objects/items/toys.dm:275
    public override async DmTask<object> pickup(mob user = null)
    {
        object dot = await base.pickup(user);
        if (user != null && user.mind != null && Dm.truthy(user.mind.has_antag_datum(typeof(datum_antagonist), TRUE)))
            await ((mob_living)user).add_mood_event("badass_antag", typeof(datum_mood_event_badass_antag));
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:280
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        if (user != null)
            ((mob_living)user).clear_mood_event("badass_antag");
        return await base.dropped(user, silent, __extra);
    }

    // tg: code/game/objects/items/toys.dm:285
    public override object Destroy(float? force = null)
    {
        if (ismob(loc))
        {
            mob_living M = loc as mob_living;
            M?.clear_mood_event("badass_antag");
        }
        return base.Destroy(force);
    }
}

/*
* Captain's Aid
*/
// tg: code/game/objects/items/toys.dm:399
public partial class obj_item_toy_captainsaid : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:396  #define CAPTAINSAID_MODE_OFF 1 (file-local, #undef at :431)
    private const int CAPTAINSAID_MODE_OFF = 1;

    // tg: code/game/objects/items/toys.dm:416
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        Dm.append(ref dot, span_notice(I18n.T("You could swear you've been hearing advertisments for the 'soon upcoming' release of a tablet version for the better part of 3 years...")));
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:420
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        current_mode++;
        playsound(this, Dm.resource("sound/items/tools/screwdriver2.ogg"), 50, vary: TRUE);
        if (current_mode <= modes.len)
        {
            balloon_alert(user, I18n.T("set to []", current_mode));
        }
        else
        {
            balloon_alert(user, I18n.T("turned off"));
            current_mode = CAPTAINSAID_MODE_OFF;
        }
        icon_state = Dm.text("captainsaid_[]", modes[current_mode]);
        update_appearance(UPDATE_ICON);
        await DmTask.CompletedTask;
        return null;
    }
}

/*
 * Fake singularity
 */
// tg: code/game/objects/items/toys.dm:441
public partial class obj_item_toy_spinningtoy : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:447
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (!iscarbon(user))
        {
            user.visible_message(span_suicide(I18n.T("[] consumes []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
            user.spin(8 * SECONDS, 1);
            return BRUTELOSS;
        }
        obj_item_bodypart_head myhead = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (myhead == null)
        {
            user.visible_message(span_suicide(I18n.T("[] tries consuming []... but [] [] no mouth!", user, this, user.p_they(), user.p_have()))); // and i must scream
            return SHAME;
        }
        user.visible_message(span_suicide(I18n.T("[] consumes []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        playsound(user, Dm.resource("sound/items/eatfood.ogg"), 50, TRUE);
        await user.adjust_nutrition(50); // mmmm delicious
        _addtimer(Dm.@new<datum_callback>(this, nameof(manual_suicide), user), (3 * SECONDS), file: "code/game/objects/items/toys.dm", line: 459);
        return MANUAL_SUICIDE;
    }

    /**
     * Internal function used in the toy singularity suicide
     *
     * Cavity implants the toy singularity into the body of the user (arg1), and kills the user.
     * Makes the user vomit and receive 120 suffocation damage if there already is a cavity implant in the user.
     * Throwing the singularity away will cause the user to start choking themself to death.
     * Arguments:
     * * user - Whoever is doing the suiciding
     */
    // tg: code/game/objects/items/toys.dm:471
    public virtual async DmTask manual_suicide(mob_living_carbon_human user = null)
    {
        if (user == null)
            return;
        if (!Dm.truthy(user.is_holding(this))) // Half digestion? Start choking to death
        {
            user.visible_message(span_suicide(I18n.T("[] panics and starts choking []self to death!", user, user.p_them())));
            await user.adjust_oxy_loss(200);
            await user.death(FALSE); // unfortunately you have to handle the suiciding yourself with a manual suicide
            user.ghostize(FALSE); // get the fuck out of our body
            return;
        }
        obj_item_bodypart_chest CH = user.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
        if (CH.cavity_item != null) // if he's (un)bright enough to have a round and full belly...
        {
            user.visible_message(span_danger(I18n.T("[] regurgitates []!", user, this))); // I swear i dont have a fetish
            await user.vomit(VOMIT_CATEGORY_BLOOD, lost_nutrition: 100, distance: 0);
            await user.adjust_oxy_loss(120);
            await user.dropItemToGround(this); // incase the crit state doesn't drop the singulo to the floor
            user.set_suicide(FALSE);
            return;
        }
        await user.transferItemToLoc(this, user, TRUE);
        CH.cavity_item = this; // The mother came inside and found Andy, dead with a HUGE belly full of toys
        await user.adjust_oxy_loss(200); // You know how most small toys in the EU have that 3+ onion head icon and a warning that says "Unsuitable for children under 3 years of age due to small parts - choking hazard"? This is why.
        await user.death(FALSE);
        user.ghostize(FALSE);
    }
}

/*
 * Toy gun: Why isn't this an /obj/item/gun?
 */
// tg: code/game/objects/items/toys.dm:510
public partial class obj_item_toy_gun : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:526
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("There [] [] cap\\s left.", bullets == 1 ? I18n.T("is") : I18n.T("are"), bullets));
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:530
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_toy_ammo_gun)
            return NONE;
        obj_item_toy_ammo_gun ammunition = tool as obj_item_toy_ammo_gun;
        if (bullets >= 7)
        {
            to_chat(user, span_warning(I18n.T("It's already fully loaded!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (ammunition.amount_left <= 0)
        {
            to_chat(user, span_warning(I18n.T("There are no more caps!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (ammunition.amount_left < (7 - bullets))
        {
            bullets += ammunition.amount_left;
            to_chat(user, span_notice(I18n.T("You reload [] cap\\s.", ammunition.amount_left)));
            ammunition.amount_left = 0;
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You reload [] cap\\s.", 7 - bullets)));
            ammunition.amount_left -= 7 - bullets;
            bullets = 7;
        }
        tool.update_appearance();
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/toys.dm:552
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return ITEM_INTERACT_BLOCKING;
        }
        add_fingerprint(user);
        if (bullets < 1)
        {
            user.show_message(span_warning(I18n.T("*click*")), MSG_AUDIBLE);
            playsound(this, Dm.resource("sound/items/weapons/gun/revolver/dry_fire.ogg"), 30, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }
        playsound(user, Dm.resource("sound/items/weapons/gun/revolver/shot.ogg"), 100, TRUE);
        bullets--;
        user.visible_message(span_danger(I18n.T("[] fires [] at []!", user, this, interacting_with)),
            span_danger(I18n.T("You fire [] at []!", this, interacting_with)),
            span_hear(I18n.T("You hear a gunshot!")));
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/toys.dm:572
public partial class obj_item_toy_ammo_gun : obj_item_toy_ammo
{
    // tg: code/game/objects/items/toys.dm:580
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("357OLD-[]", amount_left);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/toys.dm:584
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("There [] [] cap\\s left.", amount_left == 1 ? I18n.T("is") : I18n.T("are"), amount_left));
        return dot;
    }
}

/*
 * Toy swords
 */
// tg: code/game/objects/items/toys.dm:592
public partial class obj_item_toy_sword : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:621  var/static/list/tool_behaviors (a proc-level static, rules 10.6)
    private static ref DmList Initialize__tool_behaviors => ref TgState.Current.obj_item_toy_sword__Initialize__tool_behaviors;

    // tg: code/game/objects/items/toys.dm:609
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_transforming),
            Dm.named("throw_speed_on", throw_speed),
            Dm.named("hitsound_on", hitsound),
            Dm.named("clumsy_check", FALSE),
            Dm.named("inhand_icon_change", FALSE)
        ));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));

        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_tools), Initialize__tool_behaviors));
        return dot;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Updates our icon to have the correct color, and give some feedback.
     */
    // tg: code/game/objects/items/toys.dm:633
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (user != null)
            balloon_alert(user, I18n.T("[] []", Dm.truthy(active) ? I18n.T("flicked out") : I18n.T("pushed in"), this));

        playsound(this, Dm.truthy(active) ? Dm.resource("sound/items/weapons/saberon.ogg") : Dm.resource("sound/items/weapons/saberoff.ogg"), 20, TRUE);
        update_appearance(UPDATE_ICON);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/game/objects/items/toys.dm:643
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It has a lighting dial currently set to [] which looks like it can be turned with a <b>screwdriver</b>.", saber_color)));
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:647
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        switch (saber_color)
        {
            case "red":
                saber_color = "blue";
                break;
            case "blue":
                saber_color = "green";
                break;
            case "green":
                saber_color = "purple";
                break;
            case "purple":
                saber_color = "red";
                break;
            default:
                return ITEM_INTERACT_SUCCESS;
        }
        balloon_alert(user, I18n.T("changed to []", saber_color));
        update_appearance(UPDATE_ICON);
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/toys.dm:663
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(saber_color)))
            update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:668
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        string last_part = HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE) ? Dm.text("_on[]", Dm.truthy(saber_color) ? Dm.text("_[]", saber_color) : null) : null;
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_toy_sword p) => p.icon_state), last_part);
        inhand_icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_toy_sword p) => p.inhand_icon_state), last_part);
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:674
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(hacked))
        {
            to_chat(user, span_warning(I18n.T("It's already fabulous!")));
            return null;
        }
        hacked = TRUE;
        saber_color = "rainbow";
        to_chat(user, span_warning(I18n.T("RNBW_ENGAGE")));
        update_appearance(UPDATE_ICON);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/items/toys.dm:683
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_toy_sword)
            return NONE;
        obj_item_toy_sword attatched_sword = tool as obj_item_toy_sword;
        if (HAS_TRAIT(tool, TRAIT_NODROP))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to your hand, you can't attach it to []!", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (HAS_TRAIT(this, TRAIT_NODROP))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to your hand, you can't attach it to []!", this, tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You attach the ends of the two plastic swords, making a single double-bladed toy! You're fake-cool.")));
        obj_item_dualsaber_toy new_saber = Dm.@new<obj_item_dualsaber_toy>(user.loc);
        if (Dm.truthy(attatched_sword.hacked) || Dm.truthy(hacked))
        {
            new_saber.hacked = TRUE;
            new_saber.saber_color = "rainbow";
        }
        qdel(tool);
        qdel(this);
        await user.put_in_hands(new_saber);
        return ITEM_INTERACT_SUCCESS;
    }
}

/*
 * Foam armblade
 */
// tg: code/game/objects/items/toys.dm:708  (var-only)

// tg: code/game/objects/items/toys.dm:723
public partial class obj_item_toy_windup_toolbox : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:760  var/static/list/transforms (a proc-level static, rules 10.6)
    private static ref DmList Rumble__transforms => ref TgState.Current.obj_item_toy_windup_toolbox__Rumble__transforms;

    // tg: code/game/objects/items/toys.dm:737
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:741
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(active))
            Dm.append(ref dot, "single_latch_open");
        else
            Dm.append(ref dot, "single_latch");
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:748
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(active))
        {
            to_chat(user, span_notice(I18n.T("You wind up [], it begins to rumble.", this)));
            active = TRUE;
            update_appearance();
            playsound(this, Dm.resource("sound/effects/pope_entry.ogg"), 100);
            Rumble();
            _addtimer(Dm.@new<datum_callback>(this, nameof(stopRumble)), 60 * SECONDS, file: "code/game/objects/items/toys.dm", line: 755);
        }
        else
        {
            to_chat(user, span_warning(I18n.T("[] is already active!", this)));
        }
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/items/toys.dm:759
    public virtual void Rumble()
    {
        if (!Dm.truthy(Rumble__transforms))
        {
            matrix M1 = Dm.matrix();
            matrix M2 = Dm.matrix();
            matrix M3 = Dm.matrix();
            matrix M4 = Dm.matrix();
            M1.Translate(-1, 0);
            M2.Translate(0, 1);
            M3.Translate(1, 0);
            M4.Translate(0, -1);
            Rumble__transforms = Dm.list(M1, M2, M3, M4);
        }
        animate(this, Dm.named("transform", Rumble__transforms[1]), Dm.named("time", 0.2f), Dm.named("loop", -1));
        animate(Dm.named("transform", Rumble__transforms[2]), Dm.named("time", 0.1f));
        animate(Dm.named("transform", Rumble__transforms[3]), Dm.named("time", 0.2f));
        animate(Dm.named("transform", Rumble__transforms[4]), Dm.named("time", 0.3f));
    }

    // tg: code/game/objects/items/toys.dm:776
    public virtual void stopRumble()
    {
        active = FALSE;
        update_appearance();
        visible_message(span_warning(I18n.T("[] slowly stops rattling and falls still, its latch snapping shut.", this))); //subtle difference
        playsound(loc, Dm.resource("sound/items/weapons/batonextend.ogg"), 100, TRUE);
        animate(this, Dm.named("transform", Dm.matrix()));
    }
}

/*
 * Subtype of Double-Bladed Energy Swords
 */
// tg: code/game/objects/items/toys.dm:787
public partial class obj_item_dualsaber_toy : obj_item_dualsaber
{
    // tg: code/game/objects/items/toys.dm:797
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        await DmTask.CompletedTask;
        return 0;
    }

    // tg: code/game/objects/items/toys.dm:800
    [DmParams()]
    public override async DmTask<object> IsReflect(object def_zone = null) //Stops Toy Dualsabers from reflecting energy projectiles
    {
        await DmTask.CompletedTask;
        return 0;
    }

    // tg: code/game/objects/items/toys.dm:803
    public override async DmTask impale(mob_living user = null) //Stops Toy Dualsabers from injuring clowns
    {
        to_chat(user, span_warning(I18n.T("You twirl around a bit before losing your balance and impaling yourself on [].", this)));
        await user.adjust_stamina_loss(25);
    }
}

// tg: code/game/objects/items/toys.dm:808
public partial class obj_item_toy_katana : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:827
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple));
        return dot;
    }
}

/*
 * Snap pops
 */
// tg: code/game/objects/items/toys.dm:837
public partial class obj_item_toy_snappop : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:861  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_item_toy_snappop__Initialize__loc_connections;

    // tg: code/game/objects/items/toys.dm:844
    public virtual async DmTask pop_burst(float? n = null, float? c = null)
    {
        n ??= 3;
        c ??= TRUE;
        do_sparks(n, c, this);
        await Dm.new_async(ash_type, loc);
        visible_message(span_warning(I18n.T("[] explodes!", this)),
            span_hear(I18n.T("You hear a snap!")));
        playsound(this, Dm.resource("sound/effects/snap.ogg"), 50, TRUE);
        qdel(this);
    }

    // tg: code/game/objects/items/toys.dm:852
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        await pop_burst();
        return null;
    }

    // tg: code/game/objects/items/toys.dm:855  (set waitfor = FALSE, inherited from /atom/movable/throw_impact: the wrapper of
    // waiting-model.md 5.4; pop_burst may sleep)
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(throw_impact__body(hit_atom, throwingdatum, __extra), null); // `.` is never assigned: no box (5.4)
    }

    private async DmTask<object> throw_impact__body(atom hit_atom, datum_thrownthing throwingdatum, object[] __extra)
    {
        if (!Dm.truthy(base.throw_impact(hit_atom, throwingdatum, __extra)))
            await pop_burst();
        return null;
    }

    // tg: code/game/objects/items/toys.dm:859
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:866
    [SignalHandler]
    public virtual void on_entered(datum source = null, object H = null)
    {
        if (ishuman(H) || issilicon(H)) //i guess carp and shit shouldn't set them off
        {
            // tg types M as a carbon but keeps the silicon in it (rules 3.4: wider stored values): the cast is at the carbon's read
            object M = H;
            if (issilicon(H) || ((mob_living)M).move_intent == MOVE_INTENT_RUN)
            {
                to_chat(M, span_danger(I18n.T("You step on the snap pop!")));
                Waiting.NoSleep(pop_burst(2, 0));
            }
        }
    }
}

// tg: code/game/objects/items/toys.dm:880
public partial class obj_effect_decal_cleanable_ash_snappop_phoenix : obj_effect_decal_cleanable_ash
{
    // tg: code/game/objects/items/toys.dm:882
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(this, nameof(respawn)), respawn_time, file: "code/game/objects/items/toys.dm", line: 884);
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:886
    public virtual void respawn()
    {
        Dm.@new<obj_item_toy_snappop_phoenix>(get_turf(this));
        qdel(this);
    }
}

// tg: code/game/objects/items/toys.dm:891
public partial class obj_item_toy_talking : obj_item_toy
{
    // Talking toys are language universal, and thus all species can use them
    // tg: code/game/objects/items/toys.dm:906
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/items/toys.dm:909
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(cooldown))
        {
            activation_message(user);
            playsound(loc, Dm.resource("sound/machines/click.ogg"), 20, TRUE);

            Waiting.Detach(do_toy_talk(user));

            cooldown = TRUE;
            // VARSET_CALLBACK(src, cooldown, FALSE): ___callbackvarset is not declared (vars is not translated, rules 10.7), so the
            // timer calls cooldown__varset (as doorcontrol.cs)
            _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown__varset), FALSE), recharge_time, file: "code/game/objects/items/toys.dm", line: 917);
            return null;
        }
        await base.attack_self(user, modifiers, __extra);
        return null;
    }

    // tg: code/__HELPERS/varset_callback.dm:3  VARSET_CALLBACK(src, cooldown, value)
    public virtual void cooldown__varset(object var_value = null) => cooldown = Dm.num(var_value);

    // tg: code/game/objects/items/toys.dm:921
    public virtual void activation_message(mob user = null)
    {
        user.visible_message(
            span_notice(I18n.T("[] pulls the string on \\the [].", user, this)),
            span_notice(I18n.T("You pull the string on \\the [].", this)),
            span_notice(I18n.T("You hear a string being pulled.")));
    }

    // tg: code/game/objects/items/toys.dm:927
    public virtual object generate_messages()
    {
        return Dm.list(pick(messages));
    }

    // tg: code/game/objects/items/toys.dm:930
    public virtual async DmTask do_toy_talk(mob user = null)
    {
        foreach (object message in Dm.each(generate_messages()))
        {
            await toy_talk(user, message);
            await sleep(1 * SECONDS);
        }
    }

    // tg: code/game/objects/items/toys.dm:935
    public virtual async DmTask toy_talk(mob user = null, object message = null)
    {
        await say(message, spans: Dm.list(span));
        if (Dm.truthy(chattering))
            chatter(message, phomeme, user);
    }
}

/*
 * AI core prizes
 */
// tg: code/game/objects/items/toys.dm:944
public partial class obj_item_toy_talking_ai : obj_item_toy_talking
{
    // tg: code/game/objects/items/toys.dm:949
    public override object generate_messages()
    {
        return Dm.list(generate_ion_law());
    }
}

// tg: code/game/objects/items/toys.dm:953
public partial class obj_item_toy_talking_codex_gigas : obj_item_toy_talking
{
    // tg: code/game/objects/items/toys.dm:964
    public override void activation_message(mob user = null)
    {
        user.visible_message(
            span_notice(I18n.T("[] presses the button on \\the [].", user, this)),
            span_notice(I18n.T("You press the button on \\the [].", this)),
            span_notice(I18n.T("You hear a soft click.")));
    }
}

/*
 * Fake nuke
 */
// tg: code/game/objects/items/toys.dm:992
public partial class obj_item_toy_nuke : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1000
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && cooldown < world.time)
        {
            cooldown = world.time + 600;
            user.audible_message(span_hear(I18n.T("You hear the click of a button.")), self_message: span_notice(I18n.T("You activate [], it plays a loud noise!", this)));
            await sleep(0.5f * SECONDS);
            playsound(this, Dm.resource("sound/announcer/alarm/nuke_alarm.ogg"), 20, FALSE);
            await sleep(14 * SECONDS);
            user.visible_message(span_alert(I18n.T("[] violently explodes!", this)));
            await explosion(this, light_impact_range: 1);
            qdel(this);
        }
        else if (cooldown < world.time)
        {
            cooldown = world.time + 600; //1 minute
            user.visible_message(span_warning(I18n.T("[] presses a button on [].", user, this)), span_notice(I18n.T("You activate [], it plays a loud noise!", this)), span_hear(I18n.T("You hear the click of a button.")));
            await sleep(0.5f * SECONDS);
            icon_state = "nuketoy";
            playsound(this, Dm.resource("sound/announcer/alarm/nuke_alarm.ogg"), 20, FALSE);
            await sleep(13.5f * SECONDS);
            icon_state = "nuketoycool";
            await sleep(cooldown - world.time);
            icon_state = "nuketoyidle";
        }
        else
        {
            float timeleft = (cooldown - world.time);
            to_chat(user, span_alert(I18n.T("Nothing happens, and '</span>[]<span class='alert'>' appears on the small display.", round(timeleft / 10))));
            await sleep(0.5f * SECONDS);
        }
        return null;
    }

    // tg: code/game/objects/items/toys.dm:1025
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        balloon_alert(user, I18n.T("explosive simulation enabled"));
        Dm.combine(ref obj_flags, EMAGGED);
        await DmTask.CompletedTask;
        return TRUE;
    }
}

/*
 * Fake meteor
 */
// tg: code/game/objects/items/toys.dm:1036
public partial class obj_item_toy_minimeteor : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1044
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        to_chat(user, span_warning(I18n.T("You short circuit whatever electronics exist inside. The \"meteor\" suddenly feels a lot heavier...?")));
        // not adding a balloon alert here since its hard to actually describe what this emag does in the balloon
        Dm.combine(ref obj_flags, EMAGGED);
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/game/objects/items/toys.dm:1052  (set waitfor = FALSE, inherited from /atom/movable/throw_impact: the wrapper of
    // waiting-model.md 5.4; the explosion may sleep)
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(throw_impact__body(hit_atom, throwingdatum, __extra), null); // `.` is never assigned: no box (5.4)
    }

    private async DmTask<object> throw_impact__body(atom hit_atom, datum_thrownthing throwingdatum, object[] __extra)
    {
        playsound(this, Dm.resource("sound/effects/meteorimpact.ogg"), 40, TRUE);
        foreach (mob M in Dm.each<mob>(urange(10, this)))
        {
            if (!IS_UNCONSCIOUS_OR_CRIT(M) && !isAI(M))
                shake_camera(M, 3, 1);
        }
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            await explosion(this, devastation_range: -1, heavy_impact_range: -1, light_impact_range: 1);
        return null;
    }
}

/*
 * Toy big red button
 */
// tg: code/game/objects/items/toys.dm:1064
public partial class obj_item_toy_redbutton : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1072
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (cooldown < world.time)
        {
            cooldown = (world.time + 300); // Sets cooldown at 30 seconds
            user.visible_message(span_warning(I18n.T("[] presses the big red button.", user)), span_notice(I18n.T("You press the button, it plays a loud noise!")), span_hear(I18n.T("The button clicks loudly.")));
            playsound(this, Dm.resource("sound/effects/explosion/explosionfar.ogg"), 50, FALSE);
            foreach (mob M in Dm.each<mob>(urange(10, this))) // Checks range
            {
                if (!IS_UNCONSCIOUS_OR_CRIT(M) && !isAI(M)) // Checks to make sure whoever's getting shaken is alive/not the AI
                {
                    // Short delay to match up with the explosion sound
                    // Shakes player camera 2 squares for 1 second.
                    _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.shake_camera), M, 2, 1), 0.8f * SECONDS, file: "code/game/objects/items/toys.dm", line: 1081);
                }
            }
        }
        else
        {
            to_chat(user, span_alert(I18n.T("Nothing happens.")));
        }
        await DmTask.CompletedTask;
        return null;
    }
}

/*
 * Snowballs
 */
// tg: code/game/objects/items/toys.dm:1090
public partial class obj_item_toy_snowball : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1098
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        await user.throw_item(interacting_with);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/toys.dm:1102
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        if (!Dm.truthy(base.throw_impact(hit_atom, throwingdatum, __extra)))
        {
            playsound(this, Dm.resource("sound/effects/pop.ogg"), 20, TRUE);
            qdel(this);
        }
        return null;
    }
}

/*
 * Beach ball
 */
// tg: code/game/objects/items/toys.dm:1111  (var-only, with the branded beach ball and the baseball)

/*
 * Clockwork Watch
 */
// tg: code/game/objects/items/toys.dm:1136
public partial class obj_item_toy_clockwork_watch : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1145
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (cooldown < world.time)
        {
            cooldown = world.time + 1800; //3 minutes
            user.visible_message(span_warning(I18n.T("[] rotates a cogwheel on [].", user, this)), span_notice(I18n.T("You rotate a cogwheel on [], it plays a loud noise!", this)), span_hear(I18n.T("You hear cogwheels turning.")));
            playsound(this, Dm.resource("sound/effects/magic/clockwork/ark_activation.ogg"), 50, FALSE);
        }
        else
        {
            to_chat(user, span_alert(I18n.T("The cogwheels are already turning!")));
        }
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/items/toys.dm:1153
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("Station Time: []", server_timestamp(ic_time: TRUE, twelve_hour_clock: user.client?.prefs?.read_preference(typeof(datum_preference_toggle_twelve_hour))))));
        if (Dm.truthy(user.is_literate()))
            Dm.append(ref dot, span_info(I18n.T("That means it is currently [] into the shift.", round_timestamp())));
        return dot;
    }
}

/*
 * Toy Dagger
 */
// tg: code/game/objects/items/toys.dm:1163  (var-only)

/*
 * Xenomorph action figure
 */
// tg: code/game/objects/items/toys.dm:1177
public partial class obj_item_toy_toy_xeno : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1185
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (cooldown <= world.time)
        {
            cooldown = (world.time + 50); //5 second cooldown
            user.visible_message(span_notice(I18n.T("[] pulls back the string on [].", user, this)));
            icon_state = Dm.text("[]_used", Dm.initial(this, (obj_item_toy_toy_xeno p) => p.icon_state));
            await sleep(0.5f * SECONDS);
            // tg: "[icon2html(src, viewers(src))] Hiss!": the figure's picture in the chat line (the port's chat shows no inline icons)
            audible_message(span_danger(I18n.T("Hiss!")));
            DmList possible_sounds = Dm.list(Dm.resource("sound/mobs/non-humanoids/hiss/hiss1.ogg"), Dm.resource("sound/mobs/non-humanoids/hiss/hiss2.ogg"), Dm.resource("sound/mobs/non-humanoids/hiss/hiss3.ogg"), Dm.resource("sound/mobs/non-humanoids/hiss/hiss4.ogg"));
            object chosen_sound = pick(possible_sounds);
            playsound(get_turf(this), chosen_sound, 50, TRUE);
            // VARSET_CALLBACK(src, icon_state, "[initial(icon_state)]"): ___callbackvarset is not declared (vars is not translated,
            // rules 10.7), so the timer calls icon_state__varset (as urinal.cs)
            _addtimer(Dm.@new<datum_callback>(this, nameof(icon_state__varset), Dm.text("[]", Dm.initial(this, (obj_item_toy_toy_xeno p) => p.icon_state))), 4.5f * SECONDS, file: "code/game/objects/items/toys.dm", line: 1195);
        }
        else
        {
            to_chat(user, span_warning(I18n.T("The string on [] hasn't rewound all the way!", this)));
            return null;
        }
        return null;
    }

    // tg: code/__HELPERS/varset_callback.dm:3  VARSET_CALLBACK(src, icon_state, value)
    public virtual void icon_state__varset(object var_value = null) => icon_state = var_value as string;
}

// TOY MOUSEYS :3 :3 :3
// tg: code/game/objects/items/toys.dm:1202  (var-only)

/*
 * Action Figures
 */
// tg: code/game/objects/items/toys.dm:1215
public partial class obj_item_toy_figure : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1224
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        desc = I18n.T("A \"Space Life\" brand [].", this);
        Waiting.NoSleep(AddElement(typeof(datum_element_toy_talk)));
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:1229
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (cooldown <= world.time)
        {
            cooldown = world.time + 50;
            to_chat(user, span_notice(I18n.T("[] says \"[]\"", this, toysay)));
            playsound(user, toysound, 20, TRUE);
        }
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/game/objects/items/toys.dm:1450
public partial class obj_item_toy_dummy : obj_item_toy
{
    //Add changing looks when i feel suicidal about making 20 inhands for these.
    // tg: code/game/objects/items/toys.dm:1459
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object new_name = await tgui_input_text(usr, I18n.T("What would you like to name the dummy?"), I18n.T("Doll Name"), doll_name, max_length: MAX_NAME_LEN);
        if (!Dm.truthy(new_name) || !Dm.truthy(user.is_holding(this)))
            return null;
        doll_name = (string)new_name;
        to_chat(user, span_notice(I18n.T("You name the dummy as \"[]\".", doll_name)));
        name = I18n.T("[] - []", Dm.initial(this, (obj_item_toy_dummy p) => p.name), doll_name);
        return null;
    }

    // tg: code/game/objects/items/toys.dm:1467
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_toy_talk)));
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:1471
    public override object get_voice(float? add_id_name = null)
    {
        return doll_name;
    }
}

// tg: code/game/objects/items/toys.dm:1475
public partial class obj_item_toy_seashell : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1480  var/static/list/possible_colors (a type-level static, rules 10.6)
    public static ref DmList possible_colors => ref TgState.Current.obj_item_toy_seashell__possible_colors;

    // tg: code/game/objects/items/toys.dm:1482
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = rand(-5, 5);
        pixel_y = rand(-5, 5);
        icon_state = Dm.text("shell[]", rand(1, 3));
        color = pick_weight(possible_colors);
        Waiting.NoSleep(setDir(pick(GLOB.cardinals)));
        return dot;
    }
}

// tg: code/game/objects/items/toys.dm:1491
public partial class obj_item_toy_brokenradio : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1498
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (cooldown <= world.time)
        {
            cooldown = (world.time + 300);
            user.visible_message(span_notice(I18n.T("[] adjusts the dial on [].", user, this)));
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), this, Dm.resource("sound/items/radiostatic.ogg"), 50, FALSE), 0.5f * SECONDS, file: "code/game/objects/items/toys.dm", line: 1502);
        }
        else
        {
            to_chat(user, span_warning(I18n.T("The dial on [] jams up", this)));
            return null;
        }
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/game/objects/items/toys.dm:1508
public partial class obj_item_toy_braintoy : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1514
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (cooldown <= world.time)
        {
            cooldown = (world.time + 10);
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), this, Dm.resource("sound/effects/blob/blobattack.ogg"), 50, FALSE), 0.5f * SECONDS, file: "code/game/objects/items/toys.dm", line: 1517);
        }
        await DmTask.CompletedTask;
        return null;
    }
}

/*
 * Eldritch Toys
 */
// tg: code/game/objects/items/toys.dm:1523
public partial class obj_item_toy_eldritch_book : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1537
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(book_open))
            close_animation();
        else
            open_animation();
        return dot;
    }

    /// Plays a little animation that shows the book opening and closing.
    // tg: code/game/objects/items/toys.dm:1548
    public virtual void open_animation()
    {
        icon_state = Dm.text("[]_open", base_icon_state);
        flick(Dm.text("[]_opening", base_icon_state), this);
        book_open = TRUE;

        timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(close_animation)), 5 * SECONDS, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_STOPPABLE), file: "code/game/objects/items/toys.dm", line: 1553);
    }

    /// Plays a closing animation and resets the icon state.
    // tg: code/game/objects/items/toys.dm:1556
    public virtual void close_animation()
    {
        icon_state = (string)base_icon_state;
        flick(Dm.text("[]_closing", base_icon_state), this);
        book_open = FALSE;

        deltimer(timer_id);
    }
}

/*
 * Fake tear
 */
// tg: code/game/objects/items/toys.dm:1567  (var-only)

// tg: code/game/objects/items/toys.dm:1574
public partial class obj_item_toy_foamfinger : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1584
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!(foamfinger_cooldown <= world.time)) // COOLDOWN_FINISHED
            return null;
        foamfinger_cooldown = world.time + (5 * SECONDS); // COOLDOWN_START
        user.manual_emote(I18n.T("waves around the foam finger."));
        float direction = prob(50) ? -1 : 1;
        if (Dm.truthy(NSCOMPONENT(user.dir)))
        {
            animate(user, Dm.named("pixel_w", (1 * direction)), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
            animate(Dm.named("pixel_w", (-2 * direction)), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_w", (2 * direction)), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_w", (-2 * direction)), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_w", (1 * direction)), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
        }
        else
        {
            animate(user, Dm.named("pixel_z", (1 * direction)), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
            animate(Dm.named("pixel_z", (-2 * direction)), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_z", (2 * direction)), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_z", (-2 * direction)), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_z", (1 * direction)), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
        }
        user.changeNext_move(CLICK_CD_MELEE);
        await DmTask.CompletedTask;
        return null;
    }
}

///All people who have used an Intento this round along with their high scores.
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/objects/items/toys.dm:1605
    public DmList intento_players;

    // tg: code/game/objects/items/toys.dm:1605  (GLOBAL_LIST_EMPTY(intento_players), __DEFINES/__globals.dm)
    public virtual void InitGlobalintento_players()
    {
        intento_players = Dm.list();
        Dm.append(ref gvars_datum_init_order, "intento_players");
    }
}

// tg: code/game/objects/items/toys.dm:1629
public partial class obj_item_toy_intento : obj_item_toy
{
    // tg: code/game/objects/items/toys.dm:1607-1626 (file-local, #undef at :1830-1843)
    private const string HELP = "help";
    private const string DISARM = "disarm";
    private const string GRAB = "grab";
    private const string HARM = "harm";
    private const float ICON_SPLIT = (float)ICON_SIZE_ALL / 2;

    // These states do not have any associated processing.
    private const string STATE_AWAITING_PLAYER_INPUT = "awaiting_player_input";
    private const string STATE_OFF = "off";

    // When the Intento is in one of these four states, it has an accompanying
    // set of code that runs in processing()
    private const string STATE_STARTING = "starting";
    private const string STATE_DEMO = "demo";
    private const string STATE_END_OF_GAME = "end_of_game";
    private const string STATE_RETALIATION = "retaliation";

    private const float TIME_TO_BEGIN = (1.6f * SECONDS);
    private const float TIME_PER_DEMO_STEP = (0.6f * SECONDS);
    private const float TIME_TO_RESET_ICON = (0.5f * SECONDS);

    /// Associated list of intents to their sounds
    // tg: code/game/objects/items/toys.dm:1642  var/static/list/sound_by_intent (a type-level static, rules 10.6)
    public static ref DmList sound_by_intent => ref TgState.Current.obj_item_toy_intento__sound_by_intent;

    // tg: code/game/objects/items/toys.dm:1658
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra) //added params to attack_self, the alternative is registering a signal on clickon but i was advised not to
    {
        await base.attack_self(user, modifiers, __extra);
        if (state == STATE_OFF)
        {
            await boot();
            return null;
        }

        if (!Dm.truthy(modifiers))
            return null;

        if (state != STATE_AWAITING_PLAYER_INPUT)
            return null;

        object input = null;
        float? icon_x = text2num(((DmList)modifiers)[ICON_X]);
        float? icon_y = text2num(((DmList)modifiers)[ICON_Y]);
        if (Dm.num(icon_x) > ICON_SPLIT && Dm.num(icon_y) > ICON_SPLIT)
            input = DISARM;
        if (Dm.num(icon_x) < ICON_SPLIT && Dm.num(icon_y) > ICON_SPLIT)
            input = HELP;
        if (Dm.num(icon_x) > ICON_SPLIT && Dm.num(icon_y) < ICON_SPLIT)
            input = GRAB;
        if (Dm.num(icon_x) < ICON_SPLIT && Dm.num(icon_y) < ICON_SPLIT)
            input = HARM;

        player_input(user, input);
        return null;
    }

    // tg: code/game/objects/items/toys.dm:1684
    public virtual async DmTask boot()
    {
        await say(I18n.T("Game starting!"));
        playsound(this, Dm.resource("sound/machines/synth/synth_yes.ogg"), 50, FALSE);

        state = STATE_STARTING;
        next_process = world.time + (TIME_TO_BEGIN); // COOLDOWN_START
        START_PROCESSING(SSfastprocess, this);
    }

    // tg: code/game/objects/items/toys.dm:1692
    public virtual void player_input(mob player = null, object intent = null)
    {
        // All branches of this proc lead to us wanting to process
        START_PROCESSING(SSfastprocess, this);

        render(intent);

        Dm.append(ref player_sequence, intent);
        for (float i = 1, i__end = player_sequence.len; i <= i__end; i++)
        {
            if (!Dm.eq(player_sequence[i], current_sequence[i]))
            {
                state = STATE_END_OF_GAME;
                next_process = world.time + (TIME_TO_RESET_ICON); // COOLDOWN_START
                return;
            }
        }

        if (player_sequence.len == current_sequence.len)
        {
            score++;

            state = STATE_STARTING;
            next_process = world.time + (TIME_TO_BEGIN); // COOLDOWN_START
        }
    }

    // tg: code/game/objects/items/toys.dm:1711  (set waitfor = FALSE, inherited from /datum/process: the wrapper of waiting-model.md
    // 5.4; process_end and process_retaliation may sleep)
    [DmParams()]
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null); // `.` is never assigned: no box (5.4)
    }

    private async DmTask<object> process__body()
    {
        if (Dm.truthy(next_icon_reset) && next_icon_reset <= world.time)
        {
            icon_state = Dm.initial(this, (obj_item_toy_intento p) => p.icon_state) as string;
            next_icon_reset = 0; // COOLDOWN_RESET
        }

        if (Dm.truthy(next_process) && next_process > world.time)
            return null;

        switch (state)
        {
            case STATE_STARTING:
                process_start();
                break;

            case STATE_DEMO:
                process_demo();
                break;

            case STATE_END_OF_GAME:
                await process_end(isliving(loc) ? loc as mob : null);
                break;

            case STATE_RETALIATION:
                await process_retaliation();
                break;
        }

        if (!Dm.truthy(next_process) && !Dm.truthy(next_icon_reset))
            return PROCESS_KILL;
        return null;
    }

    // tg: code/game/objects/items/toys.dm:1735
    public virtual void process_start()
    {
        player_sequence.Cut();

        Dm.append(ref current_sequence, pick(Dm.list(HELP, DISARM, GRAB, HARM)));

        state = STATE_DEMO;
        next_process = world.time;
        index = 1;
    }

    // tg: code/game/objects/items/toys.dm:1744
    public virtual void process_demo()
    {
        if (Dm.num(index) > length(current_sequence))
        {
            state = STATE_AWAITING_PLAYER_INPUT;
            next_process = 0; // COOLDOWN_RESET
            return;
        }

        object intent = current_sequence[index];
        render(intent);

        Dm.append(ref index, 1); // index += 1 (rules 4.3: an object target)
        next_process = world.time + (TIME_PER_DEMO_STEP); // COOLDOWN_START
    }

    // tg: code/game/objects/items/toys.dm:1756
    public virtual async DmTask process_end(mob user = null)
    {
        if (user != null && Dm.lt(GLOB.intento_players[user.ckey], score))
        {
            GLOB.intento_players[user.ckey] = score;
            // var/award_status = user.client.get_award_status(/datum/award/score/intento_score) and the give_award of the score's rise
            Dm.Deferred("code/game/objects/items/toys.dm:1759", "process_end: user.client.get_award_status and give_award(/datum/award/score/intento_score): the achievements (D332)");
        }

        await say(I18n.T("GAME OVER. Your score was []!", score));
        playsound(this, Dm.resource("sound/machines/synth/synth_no.ogg"), 50, FALSE);

        if (user != null && loc == user && Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            ADD_TRAIT(this, TRAIT_NODROP, type);
            to_chat(user, span_userdanger(I18n.T("Bad mistake.")));

            state = STATE_RETALIATION;
            next_process = world.time;
            index = 1;
        }
        else
        {
            cleanup();
        }
    }

    // tg: code/game/objects/items/toys.dm:1777
    public virtual async DmTask process_retaliation()
    {
        mob_living victim = loc as mob_living;
        if (!isliving(victim) || Dm.num(index) > length(current_sequence))
        {
            cleanup();
            return;
        }

        object intent = current_sequence[index];
        render(intent);
        object sw__1 = intent;
        if (Dm.eq(sw__1, HELP))
        {
            to_chat(victim, span_danger(I18n.T("[] hugs you to make you feel better!", this)));
            await victim.add_mood_event("hug", typeof(datum_mood_event_hug));
        }
        else if (Dm.eq(sw__1, DISARM))
        {
            to_chat(victim, span_danger(I18n.T("You're knocked down from a shove by []!", this)));
            await victim.Knockdown(2 * SECONDS);
        }
        else if (Dm.eq(sw__1, GRAB))
        {
            to_chat(victim, span_danger(I18n.T("[] grabs you aggressively!", this)));
            await victim.Stun(2 * SECONDS);
        }
        else if (Dm.eq(sw__1, HARM))
        {
            to_chat(victim, span_danger(I18n.T("You're punched by []!", this)));
            await victim.apply_damage(rand(20, 30), BRUTE);
        }

        Dm.append(ref index, 1); // index += 1 (rules 4.3: an object target)
        next_process = world.time + (TIME_PER_DEMO_STEP); // COOLDOWN_START
    }

    // tg: code/game/objects/items/toys.dm:1802
    public virtual void cleanup()
    {
        score = 0;
        index = 1;
        player_sequence.Cut();
        current_sequence.Cut();

        state = STATE_OFF;
        next_process = 0; // COOLDOWN_RESET
        REMOVE_TRAIT(this, TRAIT_NODROP, type);
    }

    // tg: code/game/objects/items/toys.dm:1812
    public virtual void render(object input = null)
    {
        icon_state = input as string;
        playsound(this, sound_by_intent[input], 50, FALSE);

        START_PROCESSING(SSfastprocess, this);
        next_icon_reset = world.time + (TIME_TO_RESET_ICON); // COOLDOWN_START
    }

    // tg: code/game/objects/items/toys.dm:1819
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        Dm.combine(ref obj_flags, EMAGGED);
        balloon_alert(user, I18n.T("negative feedback loop enabled"));
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/game/objects/items/toys.dm:1826
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }
}

/*
 * Runic Scepter
 */
// tg: code/game/objects/items/toys.dm:1849  (var-only)

// tg: code/game/objects/items/toys.dm:1864
public partial class obj_item_extendohand : obj_item
{
    // tg: code/game/objects/items/toys.dm:1881
    [DmParams("M", "user", "modifiers", "attack_modifiers")]
    public override async DmTask<object> attack__proc(mob_living M = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        float dist = get_dist(M, user);
        if (dist < min_reach)
        {
            to_chat(user, span_warning(I18n.T("[] is too close to use [] on.", M, this)));
            return null;
        }
        await M.attack_hand(user, modifiers);
        return null;
    }
}

// tg: code/game/objects/items/toys.dm:1889
public partial class obj_item_banhammer : obj_item
{
    // tg: code/game/objects/items/toys.dm:1910
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_kneejerk)));
        return dot;
    }

    // tg: code/game/objects/items/toys.dm:1914
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is hitting []self with []! It looks like [] trying to ban []self from life.", user, user.p_them(), this, user.p_theyre(), user.p_them())));
        await DmTask.CompletedTask;
        return Dm.bitor(Dm.bitor(Dm.bitor(BRUTELOSS, FIRELOSS), TOXLOSS), OXYLOSS);
    }
    /*
    oranges says: This is a meme relating to the english translation of the ss13 russian wiki page on lurkmore.
    mrdoombringer sez: and remember kids, if you try and PR a fix for this item's grammar, you are admitting that you are, indeed, a newfriend.
    for further reading, please see: https://github.com/tgstation/tgstation/pull/30173 and https://translate.google.com/translate?sl=auto&tl=en&js=y&prev=_t&hl=en&ie=UTF-8&u=%2F%2Flurkmore.to%2FSS13&edit-text=&act=url
    */
    // tg: code/game/objects/items/toys.dm:1922
    [DmParams("M", "user")]
    public override async DmTask<object> attack__proc(mob_living M = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (user.zone_selected == BODY_ZONE_HEAD)
            M.visible_message(span_danger(I18n.T("[] are stroking the head of [] with a bangammer.", user, M)), span_userdanger(I18n.T("[] are stroking your head with a bangammer.", user)), span_hear(I18n.T("You hear a bangammer stroking a head."))); // see above comment
        else
            M.visible_message(span_danger(I18n.T("[] has been banned FOR NO REISIN by []!", M, user)), span_userdanger(I18n.T("You have been banned FOR NO REISIN by []!", user)), span_hear(I18n.T("You hear a banhammer banning someone.")));
        playsound(loc, Dm.resource("sound/effects/adminhelp.ogg"), 15); //keep it at 15% volume so people don't jump out of their skin too much
        if (Dm.truthy(user.combat_mode))
            return await base.attack__proc(M, user);
        return null;
    }
}

// The static lists of the toys (rules 10.6: type-level and proc-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/game/objects/items/toys.dm:621  var/static/list/tool_behaviors = list(TOOL_SCREWDRIVER = list(SCREENTIP_CONTEXT_LMB = "Change blade color"))
    internal DmList obj_item_toy_sword__Initialize__tool_behaviors = Dm.list(
        Dm.kv(TOOL_SCREWDRIVER, Dm.list(Dm.kv(SCREENTIP_CONTEXT_LMB, "Change blade color"))));

    // tg: code/game/objects/items/toys.dm:760  var/static/list/transforms (made at the first rumble)
    internal DmList obj_item_toy_windup_toolbox__Rumble__transforms;

    // tg: code/game/objects/items/toys.dm:861  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_item_toy_snappop__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_item_toy_snappop.on_entered)));

    // tg: code/game/objects/items/toys.dm:1480  var/static/list/possible_colors
    internal DmList obj_item_toy_seashell__possible_colors = Dm.list(
        Dm.kv("", 2), Dm.kv(COLOR_PURPLE_GRAY, 1), Dm.kv(COLOR_OLIVE, 1), Dm.kv(COLOR_PALE_BLUE_GRAY, 1), Dm.kv(COLOR_RED_GRAY, 1));

    // tg: code/game/objects/items/toys.dm:1642  var/static/list/sound_by_intent
    internal DmList obj_item_toy_intento__sound_by_intent = Dm.list(
        Dm.kv("help", Dm.resource("sound/items/intents/Help.ogg")),
        Dm.kv("disarm", Dm.resource("sound/items/intents/Disarm.ogg")),
        Dm.kv("grab", Dm.resource("sound/items/intents/Grab.ogg")),
        Dm.kv("harm", Dm.resource("sound/items/intents/Harm.ogg")));
}
