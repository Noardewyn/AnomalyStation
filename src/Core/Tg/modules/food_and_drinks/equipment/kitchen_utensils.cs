// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/equipment/kitchen_utensils.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the forks, the plastic and the cyborg's kitchen knives, the rolling pins, the spoons and
// the ladle, the tongs. The var blocks are generated (D111).

/* Kitchen tools
 * Contains:
 * Fork
 * Kitchen knives
 * Rolling Pins
 * Plastic Utensils
 * Tongs
 */

// tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:12
public partial class obj_item_kitchen : obj_item
{
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:10  #define PLASTIC_BREAK_PROBABILITY 25 (file-local, #undef at :465)
    internal const float PLASTIC_BREAK_PROBABILITY = 25;

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_APC_SHOCKING, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:24
public partial class obj_item_kitchen_fork : obj_item_kitchen
{
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_eyestab)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:55
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] stabs \\the [] into [] chest! It looks like [] trying to take a bite out of []self!", user, this, user.p_their(), user.p_theyre(), user.p_them())));
        playsound(this, Dm.resource("sound/items/eatfood.ogg"), 50, TRUE);
        return BRUTELOSS;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:60
    public override async DmTask<object> attack__proc(mob_living M = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (M is not mob_living_carbon)
            return await base.attack__proc(M, user, modifiers, attack_modifiers, __extra);

        if (forkload != null)
        {
            if (M == user)
            {
                M.visible_message(span_notice(I18n.T("[] eats a delicious forkful of omelette!", user)));
                await ((datum_reagents)M.reagents).add_reagent(forkload.type, 1);
            }
            else
            {
                M.visible_message(span_notice(I18n.T("[] feeds [] a delicious forkful of omelette!", user, M)));
                await ((datum_reagents)M.reagents).add_reagent(forkload.type, 1);
            }
            icon_state = "fork";
            forkload = null;
        }
        else
            return await base.attack__proc(M, user, modifiers, attack_modifiers, __extra);
        return null;
    }
}

// tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:76
public partial class obj_item_kitchen_fork_plastic : obj_item_kitchen_fork
{
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:88
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_easily_fragmented), obj_item_kitchen.PLASTIC_BREAK_PROBABILITY));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:96
public partial class obj_item_knife_plastic : obj_item_knife
{
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:113
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_easily_fragmented), obj_item_kitchen.PLASTIC_BREAK_PROBABILITY));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:117
public partial class obj_item_knife_kitchen_silicon : obj_item_knife_kitchen
{
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:131
    public override object get_all_tool_behaviours()
    {
        return Dm.list(TOOL_ROLLINGPIN, TOOL_KNIFE);
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:134
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("It's fitted with a [] head.", tool_behaviour));
        return dot;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:138
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        playsound(get_turf(user), Dm.resource("sound/items/tools/change_drill.ogg"), 50, TRUE);
        if (!Dm.eq(tool_behaviour, TOOL_ROLLINGPIN))
        {
            tool_behaviour = TOOL_ROLLINGPIN;
            to_chat(user, span_notice(I18n.T("You attach the rolling pin bit to the [].", this)));
            icon_state = "sili_rolling_pin";
            force = 8;
            sharpness = NONE;
            hitsound = SFX_SWING_HIT;
            attack_verb_continuous = Dm.list("bashes", "batters", "bludgeons", "thrashes", "whacks");
            attack_verb_simple = Dm.list("bash", "batter", "bludgeon", "thrash", "whack");
        }
        else
        {
            tool_behaviour = TOOL_KNIFE;
            to_chat(user, span_notice(I18n.T("You attach the knife bit to the [].", this)));
            icon_state = "sili_knife";
            force = 0;
            sharpness = SHARP_EDGED;
            hitsound = Dm.resource("sound/items/weapons/bladeslice.ogg");
            attack_verb_continuous = Dm.list("prods", "whiffs", "scratches", "pokes");
            attack_verb_simple = Dm.list("prod", "whiff", "scratch", "poke");
        }
        return null;
    }
}

// tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:161
public partial class obj_item_kitchen_rollingpin : obj_item_kitchen
{
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:194
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins flattening [] head with \\the []! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        return BRUTELOSS;
    }
}

/* Trays  moved to /obj/item/storage/bag */

// tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:199
public partial class obj_item_kitchen_spoon : obj_item_kitchen
{
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:224
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(5, INJECTABLE | OPENCONTAINER | DUNKABLE);
        register_item_context();
        return dot;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:229
    public override void create_reagents(object max_vol = null, object flags = null)
    {
        base.create_reagents(max_vol, flags);
        RegisterSignal(reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_reagent_change));
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:233
    [SignalHandler]
    public virtual void on_reagent_change(datum_reagents reagents = null)
    {
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:238
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (Dm.truthy(target.is_open_container()))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Empty spoonful";
            context[SCREENTIP_CONTEXT_RMB] = "Grab spoonful";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (isliving(target))
        {
            context[SCREENTIP_CONTEXT_LMB] = target == user ? Dm.text("[] spoonful", spoon_sip_size >= ((datum_reagents)reagents).maximum_volume ? "Swallow" : "Taste") : "Give spoonful";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return NONE;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:248
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (((datum_reagents)reagents).total_volume <= 0)
            return dot;
        mutable_appearance filled_overlay = mutable_appearance(icon, Dm.text("[]_filled", base_icon_state)) as mutable_appearance;
        filled_overlay.color = mix_color_from_reagents(((datum_reagents)reagents).reagent_list);
        Dm.append(ref dot, filled_overlay);
        return dot;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:256
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (target_mob.reagents == null || ((datum_reagents)reagents).total_volume <= 0)
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        if (Dm.truthy(target_mob.is_mouth_covered(ITEM_SLOT_HEAD)) || Dm.truthy(target_mob.is_mouth_covered(ITEM_SLOT_MASK)))
        {
            if (target_mob == user)
                target_mob.balloon_alert(user, I18n.T("can't eat with mouth covered!"));
            else
                target_mob.balloon_alert(user, I18n.T("[] mouth is covered!", target_mob.p_their()));
            return TRUE;
        }

        if (target_mob == user)
        {
            user.visible_message(
                span_notice(I18n.T("[] scoops a spoonful into [] mouth.", user, user.p_their())),
                span_notice(I18n.T("You scoop a spoonful into your mouth."))
            );
        }
        else
        {
            to_chat(target_mob, span_userdanger(I18n.T("[] forces a spoon into your face!", Dm.truthy(target_mob.is_blind()) ? I18n.T("Someone") : Dm.text("[]", user))));
            target_mob.balloon_alert(user, I18n.T("feeding spoonful..."));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, target_mob)))
            {
                target_mob.balloon_alert(user, I18n.T("interrupted!"));
                return TRUE;
            }

            to_chat(target_mob, span_userdanger(I18n.T("[] swallow a spoonful of something!", Dm.truthy(target_mob.is_blind()) ? I18n.T("You are forced to") : I18n.T("[] forces you to", user))));
            user.visible_message(
                span_danger(I18n.T("[] scoops a spoonful into []'s mouth.", user, target_mob)),
                span_notice(I18n.T("You scoop a spoonful into []'s mouth.", target_mob))
            );
        }

        playsound(target_mob, Dm.resource("sound/items/drink.ogg"), rand(10, 50), vary: TRUE);
        await ((datum_reagents)reagents).trans_to(target_mob, spoon_sip_size, methods: INGEST);
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:290
    public override async DmTask<object> pre_attack(atom attacked_atom = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.pre_attack(attacked_atom, user, modifiers, attack_modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (isliving(attacked_atom))
            return dot;
        if (!Dm.truthy(attacked_atom.is_open_container()))
            return dot;
        datum_reagents reagents = (datum_reagents)this.reagents;
        if (reagents.total_volume <= 0)
            return dot;

        object amount_given = await reagents.trans_to(attacked_atom, reagents.maximum_volume);
        if (Dm.num(amount_given) >= reagents.total_volume)
            attacked_atom.balloon_alert(user, I18n.T("spoon emptied"));
        else if (Dm.num(amount_given) > 0)
            attacked_atom.balloon_alert(user, I18n.T("spoon partially emptied"));
        else
            attacked_atom.balloon_alert(user, I18n.T("it's full!"));
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:310
    public override async DmTask<object> pre_attack_secondary(atom attacked_atom = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.pre_attack_secondary(attacked_atom, user, modifiers, attack_modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (isliving(attacked_atom))
            return SECONDARY_ATTACK_CALL_NORMAL;
        if (!Dm.truthy(attacked_atom.is_open_container()))
            return SECONDARY_ATTACK_CALL_NORMAL;

        datum_reagents reagents = (datum_reagents)this.reagents;
        if (reagents.total_volume >= reagents.maximum_volume || ((datum_reagents)attacked_atom.reagents).total_volume <= 0)
            return SECONDARY_ATTACK_CALL_NORMAL;

        if (Dm.truthy(await ((datum_reagents)attacked_atom.reagents).trans_to(this, reagents.maximum_volume)))
            attacked_atom.balloon_alert(user, I18n.T("grabbed spoonful"));
        else
            attacked_atom.balloon_alert(user, I18n.T("spoon is full!"));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }
}

// tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:328
public partial class obj_item_kitchen_spoon_plastic : obj_item_kitchen_spoon
{
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:343
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_easily_fragmented), obj_item_kitchen.PLASTIC_BREAK_PROBABILITY));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:347
public partial class obj_item_kitchen_spoon_soup_ladle : obj_item_kitchen_spoon
{
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:357
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(SOUP_SERVING_SIZE + 5, INJECTABLE | OPENCONTAINER);
        return dot;
    }
}

/// Tongs, let you pick up and feed people food from further away.
// tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:362
public partial class obj_item_kitchen_tongs : obj_item_kitchen
{
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:382
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref tonged);
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:386
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (tonged != null)
            Dm.append(ref dot, span_notice(I18n.T("It is holding [].", tonged)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:391
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        await drop_tonged();
        return dot;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:395
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return TRUE;
        if (tonged != null)
        {
            await drop_tonged();
            return TRUE;
        }
        // COOLDOWN_FINISHED(src, clack_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(clack_cooldown <= world.time))
            return TRUE;
        user.visible_message(span_notice(I18n.T("[] clacks [] [] together like a crab. Click clack!", user, user.p_their(), name)));
        click_clack();
        return TRUE;
    }

    /// Release the food we are holding
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:409
    public virtual async DmTask drop_tonged()
    {
        if (tonged == null)
            return;
        visible_message(span_notice(I18n.T("[] falls to the ground!", tonged)));
        turf location = drop_location() as turf;
        await tonged.forceMove(location);
        tonged.do_drop_animation(location);
    }

    /// Play a clacking sound and appear closed, then open again
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:418
    public virtual void click_clack()
    {
        // COOLDOWN_START(src, clack_cooldown, clack_delay) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        clack_cooldown = world.time + clack_delay;
        playsound(this, clack_sound, vol: 100, vary: FALSE);
        icon_state = Dm.text("[]_closed", base_icon_state);
        float delay = min(0.5f * SECONDS, clack_delay / 2); // Just in case someone's been fucking with the cooldown
        _addtimer(Dm.@new<datum_callback>(this, nameof(clack)), delay, TIMER_DELETE_ME, file: "code/modules/food_and_drinks/equipment/kitchen_utensils.dm", line: 423);
    }

    /// Plays a clacking sound and appear open
    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:426
    public virtual void clack()
    {
        playsound(this, clack_sound, vol: 100, vary: FALSE);
        icon_state = base_icon_state as string;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:430
    public override async DmTask<object> Exited(atom_movable leaving = null, object direction = null)
    {
        object dot = await base.Exited(leaving, direction);
        if (leaving != tonged)
            return dot;
        tonged = null;
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:437
    public override async DmTask<object> pre_attack(atom attacked = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (tonged != null && tonged.force <= 0) // prevents tongs from giving food-weapons extra range
        {
            await attacked.attackby(tonged, user);
            return TRUE;
        }
        if (isliving(attacked))
        {
            // COOLDOWN_FINISHED(src, clack_cooldown), inline
            if (clack_cooldown <= world.time)
                click_clack();
            return await base.pre_attack(attacked, user, modifiers, attack_modifiers);
        }
        if (!IS_EDIBLE(attacked) || (attacked as obj_item).w_class > WEIGHT_CLASS_NORMAL || tonged != null)
            return await base.pre_attack(attacked, user, modifiers, attack_modifiers);
        obj_item attacked__item = (obj_item)attacked;
        tonged = attacked__item;
        attacked__item.do_pickup_animation(this);
        await attacked__item.forceMove(this);
        update_appearance(UPDATE_ICON);
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/equipment/kitchen_utensils.dm:453
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (tonged == null)
            return dot;
        mutable_appearance held_food = Dm.@new<mutable_appearance>(tonged.appearance);
        held_food.layer = layer;
        held_food.plane = plane;
        held_food.transform = ((matrix)held_food.transform).Scale(0.7f, 0.7f);
        held_food.pixel_w = 6;
        held_food.pixel_z = 6;
        Dm.append(ref dot, held_food);
        return dot;
    }
}
