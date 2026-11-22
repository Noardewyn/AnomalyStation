// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/janitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H4 (D333, the janitor): the whole file: the mop bucket (filled from a container, wetting a mop with a right click) and the
// janitorial cart (its mop, broom, trash bag, spray, light replacer and up to four wet floor signs, taken out from a radial menu,
// the mop with a right click; things put in its bag with a right click; its bucket dumped with a crowbar). The var blocks are
// generated (D111). The examine lines' chat icons (icon2html) are left out: the 3D client's chat is text.

// tg: code/game/objects/structures/janitor.dm:3
public partial class obj_structure_mop_bucket : obj_structure
{
    // tg: code/game/objects/structures/janitor.dm:1  #define CART_HAS_MINIMUM_REAGENT_VOLUME !(reagents.total_volume < 1)
    protected bool CART_HAS_MINIMUM_REAGENT_VOLUME => !(((datum_reagents)reagents).total_volume < 1);

    // tg: code/game/objects/structures/janitor.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(100, OPENCONTAINER);
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/janitor.dm:18
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (held_item is obj_item_mop)
        {
            context[SCREENTIP_CONTEXT_RMB] = Dm.text("Wet []", held_item);
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item is obj_item_reagent_containers)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Fill mop bucket";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return dot;
    }

    // tg: code/game/objects/structures/janitor.dm:31
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (tool is obj_item_mop)
        {
            var tool_holder = (datum_reagents)tool.reagents;
            if (tool_holder.total_volume >= tool_holder.maximum_volume)
            {
                balloon_alert(user, "already soaked!");
                return ITEM_INTERACT_BLOCKING;
            }

            if (!CART_HAS_MINIMUM_REAGENT_VOLUME)
            {
                balloon_alert(user, "empty!");
                return ITEM_INTERACT_BLOCKING;
            }

            await ((datum_reagents)reagents).trans_to(tool, tool_holder.maximum_volume, transferred_by: user);
            balloon_alert(user, "doused mop");
            playsound(this, Dm.resource("sound/effects/slosh.ogg"), 25, vary: TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/janitor.dm:48
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (((datum_reagents)reagents)?.total_volume > 0)
            Dm.append(ref dot, water_icon);
        return dot;
    }
}

// tg: code/game/objects/structures/janitor.dm:53
public partial class obj_structure_mop_bucket_janitorialcart : obj_structure_mop_bucket
{
    // tg: code/game/objects/structures/janitor.dm:66
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ((datum_reagents)reagents).maximum_volume *= 2.5f;
        Dm.append(ref GLOB.janitor_devices, this);
        return dot;
    }

    // tg: code/game/objects/structures/janitor.dm:71
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.janitor_devices, this);
        QDEL_NULL(ref myreplacer);
        QDEL_NULL(ref myspray);
        QDEL_NULL(ref mybroom);
        QDEL_NULL(ref mymop);
        QDEL_NULL(ref mybag);
        QDEL_LIST(held_signs);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/janitor.dm:81
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (arrived is obj_item_storage_bag_trash bag)
            mybag = bag;
        else if (arrived is obj_item_mop mop)
            mymop = mop;
        else if (arrived is obj_item_pushbroom broom)
            mybroom = broom;
        else if (arrived is obj_item_reagent_containers_spray_cleaner spray)
            myspray = spray;
        else if (arrived is obj_item_lightreplacer replacer)
            myreplacer = replacer;
        else if (arrived is obj_item_clothing_suit_caution)
            Dm.append(ref held_signs, arrived);
        update_appearance(UPDATE_OVERLAYS);
        return await base.Entered(arrived, old_loc, old_locs);
    }

    // tg: code/game/objects/structures/janitor.dm:97
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == mybag)
            mybag = null;
        else if (gone == mymop)
            mymop = null;
        else if (gone == mybroom)
            mybroom = null;
        else if (gone == myspray)
            myspray = null;
        else if (gone == myreplacer)
            myreplacer = null;
        else if (Dm.@in(gone, held_signs))
            Dm.remove(ref held_signs, gone);
        if (!QDELING(this))
            update_appearance(UPDATE_OVERLAYS);
        return await base.Exited(gone, direction);
    }

    // tg: code/game/objects/structures/janitor.dm:114
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(contents.len))
        {
            Dm.append(ref dot, span_bold(span_info(I18n.T("\nIt is carrying:"))));
            foreach (object thing in Dm.each<object>(sort_names(contents)))
            {
                if (Dm.@in(thing, held_signs))
                    continue; //we'll do this after.
                // tg: "\t[icon2html(thing, user)] \a [thing]": the chat icon is left out (the 3D client's chat is text)
                Dm.append(ref dot, Dm.text("\t\\a []", thing));
            }
            if (Dm.truthy(held_signs.len))
            {
                var sign_obj = held_signs[1] as obj_item_clothing_suit_caution;
                if (held_signs.len > 1)
                    Dm.append(ref dot, Dm.text("\t[] []\\s", convert_integer_to_words(length(held_signs)), sign_obj.name));
                else
                    Dm.append(ref dot, Dm.text("\t\\a []", sign_obj));
            }
            Dm.append(ref dot, span_notice(contents.len > 1
                ? I18n.T("\n<b>Left-click</b> to search [].", this)
                : I18n.T("\n<b>Left-click</b> to remove [].", contents[1])));
            if (mybag != null)
                Dm.append(ref dot, span_notice(I18n.T("<b>Right-click</b> with a <b>[] item</b> to put it in [].", weight_class_to_text(mybag.atom_storage.max_specific_storage), mybag)));
            if (mymop != null)
                Dm.append(ref dot, span_notice(I18n.T("<b>Right-click</b> to quickly remove [].", mymop)));
        }
        if (CART_HAS_MINIMUM_REAGENT_VOLUME)
        {
            Dm.append(ref dot, span_notice(I18n.T("<b>Right-click</b> with a <b>mop</b> to wet it.")));
            Dm.append(ref dot, span_info(I18n.T("<b>Crowbar</b> it to dump its mop bucket onto [].", get_turf(this))));
        }
        return dot;
    }

    // tg: code/game/objects/structures/janitor.dm:137
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (held_item == null && Dm.truthy(contents.len))
        {
            if (mymop != null)
            {
                context[SCREENTIP_CONTEXT_RMB] = Dm.text("Remove []", mymop);
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
            if (contents.len == 1)
            {
                context[SCREENTIP_CONTEXT_LMB] = Dm.text("Remove []", contents[1]);
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
            context[SCREENTIP_CONTEXT_LMB] = "Search cart";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item is obj_item_mop && mymop == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("Insert []", held_item);
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item is obj_item_pushbroom && mybroom == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("Insert []", held_item);
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item is obj_item_storage_bag_trash && mybag == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("Insert []", held_item);
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item is obj_item_reagent_containers_spray_cleaner && myspray == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("Insert []", held_item);
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item is obj_item_clothing_suit_caution && held_signs.len < max_signs)
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("Insert []", held_item);
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item is obj_item_lightreplacer && myreplacer == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("Insert []", held_item);
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item != null && Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR) && CART_HAS_MINIMUM_REAGENT_VOLUME)
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("Dump []'s mop bucket on []", this, get_turf(this));
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item != null && mybag?.atom_storage != null && mybag.atom_storage.max_specific_storage >= Dm.num(held_item.w_class))
        {
            context[SCREENTIP_CONTEXT_RMB] = Dm.text("Insert [] into []", held_item, mybag);
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        return Dm.truthy(dot) ? dot : NONE;
    }

    // tg: code/game/objects/structures/janitor.dm:177
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_mop)
        {
            if (mymop != null)
            {
                balloon_alert(user, Dm.text("already has \\a []!", mymop));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            balloon_alert(user, Dm.text("placed []", tool));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_pushbroom)
        {
            if (mybroom != null)
            {
                balloon_alert(user, Dm.text("already has \\a []!", mybroom));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            balloon_alert(user, Dm.text("placed []", tool));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_storage_bag_trash insert)
        {
            if (mybag != null)
            {
                balloon_alert(user, Dm.text("already has \\a []!", mybag));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(insert.insertable))
            {
                balloon_alert(user, "cannot be inserted!");
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            balloon_alert(user, Dm.text("attached []", tool));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_reagent_containers_spray_cleaner)
        {
            if (myspray != null)
            {
                balloon_alert(user, Dm.text("already has \\a []!", myspray));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            balloon_alert(user, Dm.text("placed []", tool));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_lightreplacer)
        {
            if (myreplacer != null)
            {
                balloon_alert(user, Dm.text("already has \\a []!", myreplacer));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            balloon_alert(user, Dm.text("placed []", tool));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_clothing_suit_caution)
        {
            if (held_signs.len >= max_signs)
            {
                balloon_alert(user, "sign rack is full!");
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            balloon_alert(user, Dm.text("placed []", tool));
            return ITEM_INTERACT_SUCCESS;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/objects/structures/janitor.dm:251
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!CART_HAS_MINIMUM_REAGENT_VOLUME)
        {
            balloon_alert(user, "mop bucket is empty!");
            return ITEM_INTERACT_SUCCESS;
        }
        user.balloon_alert_to_viewers(Dm.text("starts dumping []...", this), Dm.text("started dumping []...", this));
        user.visible_message(span_notice(I18n.T("[] begins to dumping the contents of []'s mop bucket.", user, this)), span_notice(I18n.T("You begin to dump the contents of []'s mop bucket...", this)));
        if (Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS, volume: 50)))
        {
            balloon_alert(user, Dm.text("dumped []", this));
            to_chat(user, span_notice(I18n.T("You dumped the contents of []'s mop bucket onto the floor.", this)));
            var holder = (datum_reagents)reagents;
            await holder.expose(loc);
            await holder.clear_reagents();
            update_appearance(UPDATE_OVERLAYS);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/janitor.dm:265
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = await base.item_interaction_secondary(user, tool, modifiers);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (mybag?.atom_storage == null || !Dm.truthy(mybag.atom_storage.attempt_insert(tool, user)))
            return dot;

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/janitor.dm:275
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        DmList items = Dm.list();
        if (mybag != null)
            Dm.append(ref items, Dm.list(Dm.kv("Trash bag", image(icon: mybag.icon, icon_state: mybag.icon_state))));
        if (mymop != null)
            Dm.append(ref items, Dm.list(Dm.kv("Mop", image(icon: mymop.icon, icon_state: mymop.icon_state))));
        if (mybroom != null)
            Dm.append(ref items, Dm.list(Dm.kv("Broom", image(icon: mybroom.icon, icon_state: mybroom.icon_state))));
        if (myspray != null)
            Dm.append(ref items, Dm.list(Dm.kv("Spray bottle", image(icon: myspray.icon, icon_state: myspray.icon_state))));
        if (myreplacer != null)
            Dm.append(ref items, Dm.list(Dm.kv("Light replacer", image(icon: myreplacer.icon, icon_state: myreplacer.icon_state))));
        if (Dm.truthy(held_signs.len))
        {
            var sign = held_signs[1] as obj_item_clothing_suit_caution;
            Dm.append(ref items, Dm.list(Dm.kv("Sign", image(icon: sign.icon, icon_state: sign.icon_state))));
        }

        if (!Dm.truthy(length(items)))
            return dot;

        object pick = items[1];
        if (length(items) > 1)
        {
            items = sort_list(items) as DmList;
            pick = await show_radial_menu(user, this, items, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 38, require_near: TRUE);
        }

        if (!Dm.truthy(pick))
            return dot;
        switch (Dm.str(pick))
        {
            case "Trash bag":
                if (mybag == null)
                    return dot;
                balloon_alert(user, Dm.text("detached []", mybag));
                await user.put_in_hands(mybag);
                break;
            case "Mop":
                if (mymop == null)
                    return dot;
                balloon_alert(user, Dm.text("removed []", mymop));
                await user.put_in_hands(mymop);
                break;
            case "Broom":
                if (mybroom == null)
                    return dot;
                balloon_alert(user, Dm.text("removed []", mybroom));
                await user.put_in_hands(mybroom);
                break;
            case "Spray bottle":
                if (myspray == null)
                    return dot;
                balloon_alert(user, Dm.text("removed []", myspray));
                await user.put_in_hands(myspray);
                break;
            case "Light replacer":
                if (myreplacer == null)
                    return dot;
                balloon_alert(user, Dm.text("removed []", myreplacer));
                await user.put_in_hands(myreplacer);
                break;
            case "Sign":
            {
                if (!Dm.truthy(held_signs.len))
                    return dot;
                var removed_sign = held_signs[1] as obj_item_clothing_suit_caution;
                if (length(held_signs) > 1)
                    balloon_alert(user, Dm.text("removed \\a []", removed_sign));
                else
                    balloon_alert(user, Dm.text("removed []", removed_sign));
                await user.put_in_hands(removed_sign);
                break;
            }
            default:
                return dot;
        }
        return dot;
    }

    // tg: code/game/objects/structures/janitor.dm:343
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (mymop == null)
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        balloon_alert(user, Dm.text("removed []", mymop));
        await user.put_in_hands(mymop);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    /**
     * check_menu: Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The mob interacting with a menu
     */
    // tg: code/game/objects/structures/janitor.dm:356
    public virtual object check_menu(mob_living user = null)
    {
        return user != null && !Dm.truthy(user.incapacitated) ? TRUE : FALSE;
    }

    // tg: code/game/objects/structures/janitor.dm:359
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (mybag != null)
            Dm.append(ref dot, mybag is obj_item_storage_bag_trash_bluespace ? "cart_bluespace_garbage" : "cart_garbage");
        if (mymop != null)
            Dm.append(ref dot, "cart_mop");
        if (mybroom != null)
            Dm.append(ref dot, "cart_broom");
        if (myspray != null)
            Dm.append(ref dot, "cart_spray");
        if (myreplacer != null)
            Dm.append(ref dot, "cart_replacer");
        if (Dm.truthy(held_signs.len))
            Dm.append(ref dot, Dm.text("cart_sign[]", min(held_signs.len, 4)));
        return dot;
    }
}
