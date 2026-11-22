// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/pizzabox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the pizza box (opening, stacking, the tag, the pizza inside) and the pizza bomb in it
// (armed by hand with its timer, beeping when opened, defused or set off by its wires). The screentips are a live-partial
// row; the infinite pizza box came with gaps BR1 (a bitrunning disk's reward). The var block is hand-written: the generator types `pizza`
// by its name as a Type, and tg's var holds the subtype's typepath until Initialize puts the pizza itself there.

// tg: code/modules/food_and_drinks/pizzabox.dm:9
public partial class obj_item_pizzabox : obj_item
{
    // tg: code/modules/food_and_drinks/pizzabox.dm:28  (a typepath on the subtypes, the pizza itself after Initialize)
    public object pizza;

    // tg: code/modules/food_and_drinks/pizzabox.dm:10
    public obj_item_pizzabox()
    {
        name = "pizza box";
        desc = "A box suited for pizzas.";
        icon = Dm.resource("icons/obj/food/containers.dmi");
        icon_state = "pizzabox";
        base_icon_state = "pizzabox";
        inhand_icon_state = "pizzabox";
        lefthand_file = Dm.resource("icons/mob/inhands/items/food_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/items/food_righthand.dmi");
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:18
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_cardboard), SHEET_MATERIAL_AMOUNT));
        boxes = Dm.list();
    }

    private protected obj_item_food_pizza pizza_item => pizza as obj_item_food_pizza;

    // tg: code/modules/food_and_drinks/pizzabox.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (pizza != null)
        {
            pizza = Dm.@new((Type)pizza, this);
            pizza_item.slice();
        }
        update_appearance();
        register_context();
        return dot;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:48
    public virtual void register_bomb(object new_bomb = null)
    {
        bomb = new_bomb as obj_item_bombcore_miniature_pizza;
        if (istype(bomb, typeof(obj_item_bombcore_miniature_pizza)))
            RegisterSignal(bomb, COMSIG_QDELETING, nameof(clear_bomb));
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:53
    [SignalHandler]
    public virtual void clear_bomb(datum source = null)
    {
        if (bomb == null)
            return;
        UnregisterSignal(bomb, COMSIG_QDELETING);
        bomb = null;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:60
    public override object Destroy(float? force = null)
    {
        unprocess();
        clear_bomb();
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:65
    public override object update_desc(float? updates = null)
    {
        desc = Dm.initial(this, nameof(desc)) as string;
        object dot = base.update_desc(updates);
        if (pizza_item != null && Dm.truthy(pizza_item.boxtag) && !Dm.truthy(boxtag_set))
        {
            boxtag = pizza_item.boxtag;
            boxtag_set = TRUE;
        }
        if (Dm.truthy(open))
        {
            if (pizza_item != null)
                desc = I18n.T("[] It appears to have \\a [] inside[]. Use your other hand to take it out.", desc, pizza_item, Dm.truthy(pizza_item.sliced) ? I18n.T(". It is sliced") : "");
            if (bomb != null)
            {
                desc = I18n.T("[] Wait, what?! It has \\a [] inside!", desc, bomb);
                if (Dm.truthy(bomb_defused))
                    desc = I18n.T("[] The bomb seems inert. Use your other hand to activate it.", desc);
                if (Dm.truthy(bomb_active))
                    desc = I18n.T("[] It looks like it's about to go off!", desc);
            }
        }
        else
        {
            obj_item_pizzabox box = Dm.truthy(Dm.length(boxes)) ? boxes[Dm.length(boxes)] as obj_item_pizzabox : this;
            if (Dm.truthy(Dm.length(boxes)))
                desc = I18n.T("A pile of boxes suited for pizzas. There appear to be [] boxes in the pile.", Dm.length(boxes) + 1);
            if (box.boxtag != "")
                desc = I18n.T("[] The []'s tag reads: [].", desc, Dm.truthy(Dm.length(boxes)) ? I18n.T("top box") : I18n.T("box"), box.boxtag);
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:87
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(open))
        {
            icon_state = Dm.text("[]", base_icon_state);
            return await base.update_icon_state(__extra);
        }

        icon_state = pizza != null ? Dm.text("[]_messy", base_icon_state) : Dm.text("[]_open", base_icon_state);
        if (bomb != null)
            bomb.icon_state = Dm.text("pizzabomb_[]", Dm.truthy(bomb_active) ? "active" : "inactive");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:96
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(open))
        {
            if (pizza_item != null)
            {
                mutable_appearance pizza_overlay = mutable_appearance(pizza_item.icon, pizza_item.icon_state) as mutable_appearance;
                if (!Dm.eq(pizza_item.slices_left, Dm.initial(pizza_item, nameof(obj_item_food_pizza.slices_left))))
                    pizza_overlay.add_filter("pizzaslices", 1, pizza_item.get_slices_filter() as DmList);
                pizza_overlay.pixel_z = -2;
                Dm.append(ref dot, pizza_overlay);
            }
            if (bomb != null)
            {
                mutable_appearance bomb_overlay = mutable_appearance(bomb.icon, bomb.icon_state, layer: layer + 0.01f) as mutable_appearance;
                bomb_overlay.pixel_z = 8;
                Dm.append(ref dot, bomb_overlay);
            }
            return dot;
        }

        float box_offset = 0;
        foreach (object stacked_box in Dm.each(boxes))
        {
            box_offset += 3;
            obj_item_pizzabox box__1 = stacked_box as obj_item_pizzabox;
            mutable_appearance box_overlay = mutable_appearance(box__1.icon, box__1.icon_state, layer: layer + box_offset * 0.01f) as mutable_appearance;
            box_overlay.pixel_z = box_offset;
            Dm.append(ref dot, box_overlay);
        }

        obj_item_pizzabox box = Dm.truthy(LAZYLEN(Dm.length(boxes))) ? boxes[Dm.length(boxes)] as obj_item_pizzabox : this;
        if (box.boxtag != "")
        {
            mutable_appearance tag_overlay = mutable_appearance(icon, "pizzabox_tag", layer: layer + box_offset * 0.02f) as mutable_appearance;
            tag_overlay.pixel_z = box_offset;
            Dm.append(ref dot, tag_overlay);
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:125
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        DmList dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        float current_offset = 2;
        if (!Dm.truthy(isinhands))
            return dot;

        foreach (object V in Dm.each(boxes)) //add EXTRA BOX per box
        {
            mutable_appearance M = mutable_appearance(icon_file, inhand_icon_state as string) as mutable_appearance;
            M.pixel_z = current_offset;
            current_offset += 2;
            Dm.append(ref dot, M);
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:137
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.length(boxes) > 0)
            return null;
        open = Dm.truthy(open) ? FALSE : TRUE;
        if (Dm.truthy(open) && !Dm.truthy(bomb_defused))
        {
            // [icon2html(src, hearers(src))]: the box's picture in the chat line (chat icons, not-translated section 76)
            audible_message(span_warning(I18n.T("*beep*")));
            bomb_active = TRUE;
            START_PROCESSING(SSobj, this);
        }
        update_appearance();
        return null;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:147
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        if (Dm.length(boxes) > 0)
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (pizza != null || bomb != null || !Dm.truthy(foldable))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        obj_item_stack_sheet_cardboard cardboard = Dm.@new<obj_item_stack_sheet_cardboard>(user.drop_location());
        await user.put_in_active_hand(cardboard);
        qdel(this);
        return SECONDARY_ATTACK_CONTINUE_CHAIN;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/food_and_drinks/pizzabox.dm:158
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (user.get_inactive_held_item() != this)
        {
            if (Dm.truthy(open) && pizza_item != null && Dm.truthy(pizza_item.sliced) && !isobj(loc))
            {
                await pizza_item.produce_slice(user);
                update_appearance();
                return null;
            }
            return await base.attack_hand(user, modifiers, __extra);
        }
        if (Dm.truthy(open))
        {
            if (pizza != null)
            {
                await user.put_in_hands(pizza as obj_item);
                pizza = null;
                update_appearance();
            }
            else if (bomb != null)
            {
                if (Dm.truthy(wires.is_all_cut()) && Dm.truthy(bomb_defused))
                {
                    await user.put_in_hands(bomb);
                    balloon_alert(user, I18n.T("removed bomb"));
                    clear_bomb();
                    update_appearance();
                    return null;
                }
                else
                {
                    bomb_timer = Dm.num(await tgui_input_number(user, I18n.T("Set the bomb timer"), I18n.T("Pizza Bomb"), bomb_timer, bomb_timer_max, bomb_timer_min));
                    if (!Dm.truthy(bomb_timer) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await usr.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
                        return null;
                    bomb_defused = FALSE;
                    log_bomber(user, "has trapped a", this, Dm.text("with [] set to [] seconds", bomb, bomb_timer));
                    bomb.adminlog = I18n.T("\\The [] in [] that [] activated has detonated!", bomb, name, key_name(user));
                    balloon_alert(user, I18n.T("bomb set"));
                    update_appearance();
                }
            }
        }
        else if (Dm.truthy(Dm.length(boxes)))
        {
            obj_item_pizzabox topbox = boxes[Dm.length(boxes)] as obj_item_pizzabox;
            boxes.Remove(topbox);
            await user.put_in_hands(topbox);
            topbox.update_appearance();
            update_appearance();
            await user.regenerate_icons();
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:194
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item used_item = tool;
        object dot = NONE;
        if (istype(used_item, typeof(obj_item_pizzabox)))
        {
            obj_item_pizzabox newbox = (obj_item_pizzabox)used_item;
            if (!Dm.truthy(open) && !Dm.truthy(newbox.open))
            {
                DmList add = Dm.list();
                add.Add(newbox);
                add.Add(newbox.boxes);
                if (!Dm.truthy(await user.transferItemToLoc(newbox, this)))
                    return ITEM_INTERACT_FAILURE;
                boxes.Add(add);
                newbox.boxes.Cut();
                newbox.update_appearance();
                update_appearance();
                await user.regenerate_icons();
                if (Dm.length(boxes) >= 5)
                {
                    if (prob(10 * Dm.length(boxes)))
                    {
                        user.balloon_alert_to_viewers(I18n.T("oops!"));
                        await disperse_pizzas();
                    }
                    else
                    {
                        balloon_alert(user, I18n.T("looks unstable..."));
                    }
                }
                return ITEM_INTERACT_SUCCESS;
            }
            else
            {
                balloon_alert(user, I18n.T("close it first!"));
                return ITEM_INTERACT_FAILURE;
            }
        }
        else if (istype(used_item, typeof(obj_item_food_pizza)))
        {
            if (Dm.truthy(open))
            {
                if (pizza != null)
                {
                    balloon_alert(user, I18n.T("it's full!"));
                    return ITEM_INTERACT_FAILURE;
                }
                if (!Dm.truthy(await user.transferItemToLoc(used_item, this)))
                    return ITEM_INTERACT_FAILURE;
                pizza = used_item;
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
            }
        }
        else if (istype(used_item, typeof(obj_item_bombcore_miniature_pizza)))
        {
            if (Dm.truthy(open) && bomb == null)
            {
                if (!Dm.truthy(await user.transferItemToLoc(used_item, this)))
                    return ITEM_INTERACT_FAILURE;
                set_wires(Dm.@new<datum_wires_explosive_pizza>(this));
                register_bomb(used_item);
                balloon_alert(user, I18n.T("bomb placed"));
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
            }
            else if (bomb != null)
            {
                balloon_alert(user, I18n.T("already rigged!"));
                return ITEM_INTERACT_FAILURE;
            }
        }
        else if (IS_WRITING_UTENSIL(used_item))
        {
            if (Dm.truthy(open))
                return ITEM_INTERACT_FAILURE;
            if (!Dm.truthy(await user.can_write(used_item)))
                return ITEM_INTERACT_FAILURE;
            obj_item_pizzabox box = Dm.truthy(Dm.length(boxes)) ? boxes[Dm.length(boxes)] as obj_item_pizzabox : this;
            box.boxtag += Dm.text("[]", await tgui_input_text(user, I18n.T("Write on []'s tag:", box), box, max_length: 30));
            if (!Dm.truthy(await user.can_perform_action(this)))
                return ITEM_INTERACT_FAILURE;
            balloon_alert(user, I18n.T("writing box tag..."));
            playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
            boxtag_set = TRUE;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }
        else if (Dm.truthy(is_wire_tool(used_item)) && wires != null && bomb != null)
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        else if (istype(used_item, typeof(obj_item_knife)) && pizza_item != null && Dm.truthy(open) && !Dm.truthy(pizza_item.sliced))
        {
            pizza_item.slice(user, used_item);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:263
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // set waitfor = FALSE, inherited from /datum/process (waiting-model.md 5.4)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        if (Dm.truthy(bomb_active) && !Dm.truthy(bomb_defused) && bomb_timer > 0)
        {
            playsound(loc, Dm.resource("sound/items/timer.ogg"), 50, FALSE);
            bomb_timer -= Dm.num(seconds_per_tick);
        }
        if (Dm.truthy(bomb_active) && !Dm.truthy(bomb_defused) && bomb_timer <= 0)
        {
            if (Dm.@in(bomb, this))
            {
                await bomb.detonate();
                unprocess();
                qdel(this);
            }
        }
        if (!Dm.truthy(bomb_active) || Dm.truthy(bomb_defused))
        {
            if (Dm.truthy(bomb_defused) && Dm.@in(bomb, this))
            {
                bomb.defuse();
                bomb_active = FALSE;
                unprocess();
            }
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:279
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (Dm.length(boxes) >= 3 && prob(25 * Dm.length(boxes)))
            await disperse_pizzas();
        return dot;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:284
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        if (Dm.length(boxes) >= 2 && prob(20 * Dm.length(boxes)))
            Waiting.NoSleep(disperse_pizzas());
        return null;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:288
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (isobserver(user))
        {
            if (bomb != null)
                dot.Add(span_deadsay(I18n.T("This pizza box contains [].", Dm.truthy(bomb_defused) ? I18n.T("an unarmed bomb") : I18n.T("an armed bomb"))));
            if (pizza != null && istype(pizza, typeof(obj_item_food_pizza_margherita_robo)))
                dot.Add(span_deadsay(I18n.T("The pizza in this pizza box contains nanomachines.")));
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:296
    public virtual async DmTask disperse_pizzas()
    {
        visible_message(span_warning(I18n.T("The pizzas fall everywhere!")));
        foreach (object V in Dm.each(boxes.Copy()))
        {
            obj_item_pizzabox P = V as obj_item_pizzabox;
            object fall_dir = pick(GLOB.alldirs);
            await step(P, Dm.num(fall_dir));
            if (P.pizza_item != null && Dm.truthy(P.can_open_on_fall) && prob(50)) //rip pizza
            {
                P.open = TRUE;
                await P.pizza_item.forceMove(get_turf(P));
                fall_dir = pick(GLOB.alldirs);
                await step(P.pizza_item, Dm.num(fall_dir));
                P.pizza = null;
                P.update_appearance();
            }
            boxes.Remove(P);
        }
        update_appearance();
        if (isliving(loc))
        {
            mob_living L = (mob_living)loc;
            await L.regenerate_icons();
        }
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:315
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone != pizza)
            return dot;
        pizza = null;
        update_appearance();
        return dot;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:322
    public virtual void unprocess()
    {
        STOP_PROCESSING(SSobj, this);
        qdel(wires);
        set_wires(null);
        update_appearance();
    }
}

// tg: code/modules/food_and_drinks/pizzabox.dm:328
public partial class obj_item_pizzabox_bomb : obj_item_pizzabox
{
    // tg: code/modules/food_and_drinks/pizzabox.dm:328
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (pizza == null)
        {
            DmList pizzas = subtypesof(typeof(obj_item_food_pizza));
            pizzas.Remove(typeof(obj_item_food_pizza_flatbread)); //also disincludes another base type
            object randompizza = pick(pizzas);
            pizza = Dm.@new((Type)randompizza, this);
            update_appearance();
        }
        register_bomb(Dm.@new<obj_item_bombcore_miniature_pizza>(this));
        set_wires(Dm.@new<datum_wires_explosive_pizza>(this));
        return dot;
    }
}

//An anomalous pizza box that, when opened, produces the opener's favorite kind of pizza.
// Gaps BR1 (D339): the infinite pizza box, a bitrunning item disk's reward
// tg: code/modules/food_and_drinks/pizzabox.dm:363
public partial class obj_item_pizzabox_infinite : obj_item_pizzabox
{
    ///List of ckeys and their favourite pizzas. e.g. pizza_preferences[ckey] = /obj/item/food/pizza/meat
    // tg: code/modules/food_and_drinks/pizzabox.dm:380  var/static/list/pizza_preferences
    private static DmList pizza_preferences
    {
        get => TgState.Current.obj_item_pizzabox_infinite__pizza_preferences;
        set => TgState.Current.obj_item_pizzabox_infinite__pizza_preferences = value;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:382
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (pizza_preferences == null)
            pizza_preferences = Dm.list();
        return dot;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:387
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(open) && ishuman(user))
            attune_pizza(user as mob_living_carbon_human); //pizza tag changes based on examiner
        object dot = await base.examine(user, __extra);
        if (isobserver(user))
            Dm.append(ref dot, span_deadsay(I18n.T("This pizza box is anomalous, and will produce infinite pizza.")));
        return dot;
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:394
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (ishuman(user))
        {
            attune_pizza(user as mob_living_carbon_human);
            to_chat(user, span_notice(I18n.T("Another pizza immediately appears in the box, what the hell?")));
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/food_and_drinks/pizzabox.dm:400
    public virtual void attune_pizza(mob_living_carbon_human nommer = null) //tonight on "proc names I never thought I'd type"
    {
        if (!Dm.truthy(nommer.ckey))
            return;

        //list our ckey and assign it a favourite pizza
        if (!Dm.truthy(pizza_preferences[nommer.ckey]))
        {
            if (Dm.truthy(nommer.has_quirk(typeof(datum_quirk_pineapple_liker))))
                pizza_preferences[nommer.ckey] = typeof(obj_item_food_pizza_pineapple);
            else if (Dm.truthy(nommer.has_quirk(typeof(datum_quirk_pineapple_hater))))
            {
                DmList pineapple_pizza_liker = pizza_types.Copy();
                Dm.remove(ref pineapple_pizza_liker, typeof(obj_item_food_pizza_pineapple));
                pizza_preferences[nommer.ckey] = pick_weight(pineapple_pizza_liker);
            }
            else if (nommer.mind?.assigned_role is datum_job_botanist)
                pizza_preferences[nommer.ckey] = typeof(obj_item_food_pizza_dank);
            else
                pizza_preferences[nommer.ckey] = pick_weight(pizza_types);
        }
        if (pizza != null)
        {
            //if the pizza isn't our favourite, delete it
            if (!Dm.eq((pizza as datum)?.type, pizza_preferences[nommer.ckey]))
                QDEL_NULL(ref pizza);
            else
            {
                pizza_item.foodtypes = nommer.get_liked_foodtypes(); //make sure it's our favourite
                return;
            }
        }

        Type favourite_pizza_type = pizza_preferences[nommer.ckey] as Type;
        pizza = Dm.@new(favourite_pizza_type, this);
        pizza_item.slice();
        boxtag_set = FALSE;
        update_appearance(); //update our boxtag to match our new pizza
        pizza_item.foodtypes = nommer.get_liked_foodtypes(); //it's our favorite!
    }
}

public sealed partial class TgState
{
    internal DmList obj_item_pizzabox_infinite__pizza_preferences;
}
