// Translated from tgstation (snapshot 1d99bd7f): code/modules/recycling/sortingmachinery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D325: the disposal system's mail): the wrapped package (its destination tag, which a flush hands to the holder for
// the mail sorting junctions; its gift wrap, note and export barcode; unwrapping it), the destination tagger (its DestinationTagger
// window) and the sales tagger.

// tg: code/modules/recycling/sortingmachinery.dm:1
public partial class obj_item_delivery : obj_item
{
    // tg: code/modules/recycling/sortingmachinery.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_MOVABLE_DISPOSING, nameof(disposal_handling));
        return dot;
    }

    /**
     * Initial check if manually unwrapping
     */
    // tg: code/modules/recycling/sortingmachinery.dm:17
    public virtual async DmTask<object> attempt_pre_unwrap_contents(mob user = null, object time = null)
    {
        time ??= 1.5f * SECONDS;
        to_chat(user, span_notice(I18n.T("You start to unwrap the package...")));
        return await do_after(user, time, target: user);
    }

    /**
     * Signals for unwrapping.
     */
    // tg: code/modules/recycling/sortingmachinery.dm:24
    public virtual void unwrap_contents()
    {
        if (sticker == null)
            return;
        foreach (atom_movable movable_content in Dm.each<atom_movable>(contents.Copy()))
            movable_content.SEND_SIGNAL(COMSIG_ITEM_UNWRAPPED, Dm.list(movable_content));
    }

    /**
     * Effects after completing unwrapping
     */
    // tg: code/modules/recycling/sortingmachinery.dm:33
    public virtual async DmTask post_unwrap_contents(mob user = null, float? rip_open = null)
    {
        rip_open ??= TRUE;
        turf turf_loc = get_turf((atom)user ?? this);
        if (Dm.truthy(rip_open))
        {
            playsound(loc, Dm.resource("sound/items/poster/poster_ripped.ogg"), 50, TRUE);
            Dm.@new<obj_effect_decal_cleanable_wrapping>(turf_loc);
        }
        else
        {
            playsound(loc, Dm.resource("sound/items/box_cut.ogg"), 50, TRUE);
            Dm.@new<obj_item_stack_package_wrap_one>(turf_loc);
        }
        foreach (atom_movable movable_content in Dm.each<atom_movable>(contents.Copy()))
            await movable_content.forceMove(turf_loc);

        qdel(this);
    }

    // tg: code/modules/recycling/sortingmachinery.dm:46
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Dm.combine(ref SSexplosions.high_mov_atom, contents);
                break;
            case EXPLODE_HEAVY:
                Dm.combine(ref SSexplosions.med_mov_atom, contents);
                break;
            case EXPLODE_LIGHT:
                Dm.combine(ref SSexplosions.low_mov_atom, contents);
                break;
        }
        await DmTask.CompletedTask;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:55
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        unwrap_contents();
        await post_unwrap_contents();
    }

    // tg: code/modules/recycling/sortingmachinery.dm:59
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (note != null)
        {
            if (!in_range(user, this))
                Dm.append(ref dot, span_info(I18n.T("There's a [] attached to it. You can't read it from here.", EXAMINE_HINT(note.name))));
            else
            {
                Dm.append(ref dot, span_info(I18n.T("There's a [] attached to it...", EXAMINE_HINT(note.name))));
                Dm.append(ref dot, await note.examine(user));
            }
        }
        if (sticker != null)
            Dm.append(ref dot, span_notice(I18n.T("There's a [] attached to the side. The package is marked for []", EXAMINE_HINT("barcode"), EXAMINE_HINT("export."))));
        if (Dm.truthy(sort_tag))
            Dm.append(ref dot, span_notice(I18n.T("There's a [] with the destination set to []", EXAMINE_HINT("sorting tag"), EXAMINE_HINT(Dm.text("[].", GLOB.TAGGERLOCATIONS[sort_tag])))));
        return dot;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:72
    public virtual void disposal_handling(object disposal_source = null, obj_structure_disposalholder disposal_holder = null, obj_machinery_disposal disposal_machine = null, object hasmob = null)
    {
        if (!Dm.truthy(hasmob))
            disposal_holder.destinationTag = sort_tag;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:77
    public override async DmTask<object> relay_container_resist_act(mob_living user = null, obj container = null)
    {
        if (ismovable(loc))
        {
            atom_movable movable_loc = loc as atom_movable; //can't unwrap the wrapped container if it's inside something.
            await movable_loc.relay_container_resist_act(user, container);
            return null;
        }
        to_chat(user, span_notice(I18n.T("You lean on the back of [] and start pushing to rip the wrapping around it.", container)));
        if (Dm.truthy(await do_after(user, 5 * SECONDS, target: container)))
        {
            if (user == null || IS_UNCONSCIOUS_OR_CRIT(user) || user.loc != container || container.loc != this)
                return null;
            to_chat(user, span_notice(I18n.T("You successfully removed []'s wrapping!", container)));
            await container.forceMove(loc);
            unwrap_contents();
            await post_unwrap_contents(user);
        }
        else
        {
            if (user.loc == this) //so we don't get the message if we resisted multiple times and succeeded.
                to_chat(user, span_warning(I18n.T("You fail to remove []'s wrapping!", container)));
        }
        return null;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:94
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(giftwrapped) ? Dm.text("gift[]", base_icon_state) : base_icon_state as string;
        return dot;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:98
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(sort_tag))
            Dm.append(ref dot, Dm.text("[]_sort", base_icon_state));
        if (note != null)
            Dm.append(ref dot, Dm.text("[]_note", base_icon_state));
        if (sticker != null)
            Dm.append(ref dot, Dm.text("[]_barcode", base_icon_state));
        return dot;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:107
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_dest_tagger)))
        {
            object relevant_tag = (tool as obj_item_dest_tagger).currTag;
            if (Dm.eq(sort_tag, relevant_tag))
                return ITEM_INTERACT_BLOCKING;
            string tag = uppertext(GLOB.TAGGERLOCATIONS[relevant_tag]) as string;
            to_chat(user, span_notice(Dm.text("*[]*", tag)));
            sort_tag = Dm.num(relevant_tag);
            playsound(loc, Dm.resource("sound/machines/beep/twobeep_high.ogg"), 100, TRUE);
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stack_wrapping_paper)))
        {
            if (Dm.truthy(giftwrapped))
                return ITEM_INTERACT_BLOCKING;
            obj_item_stack_wrapping_paper wrapping_paper = tool as obj_item_stack_wrapping_paper;
            if (!Dm.truthy(await wrapping_paper.use(3)))
            {
                to_chat(user, span_warning(I18n.T("You need more paper!")));
                return ITEM_INTERACT_BLOCKING;
            }
            user.visible_message(span_notice(I18n.T("[] wraps the package in festive paper!", user)));
            giftwrapped = TRUE;
            greyscale_config = text2path(Dm.text("/datum/greyscale_config/gift[]", icon_state));
            await set_greyscale(colors: wrapping_paper.greyscale_colors);
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_paper)))
        {
            if (note != null)
            {
                to_chat(user, span_warning(I18n.T("This package already has a note attached!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("For some reason, you can't attach []!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }
            user.visible_message(span_notice(I18n.T("[] attaches [] to [].", user, tool, this)), span_notice(I18n.T("You attach [] to [].", tool, this)));
            note = tool as obj_item_paper;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_universal_scanner)))
        {
            obj_item_universal_scanner sales_tagger = tool as obj_item_universal_scanner;
            if (!Dm.eq(sales_tagger.scanning_mode, SCAN_SALES_TAG))
                return ITEM_INTERACT_BLOCKING;
            if (sticker != null)
            {
                to_chat(user, span_warning(I18n.T("This package already has a barcode attached!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(sales_tagger.payments_acc))
            {
                to_chat(user, span_warning(I18n.T("Swipe an ID on [] first!", sales_tagger)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.num(sales_tagger.paper_count) <= 0)
            {
                to_chat(user, span_warning(I18n.T("[] is out of paper!", sales_tagger)));
                return ITEM_INTERACT_BLOCKING;
            }
            user.visible_message(span_notice(I18n.T("[] attaches a barcode to [].", user, this)), span_notice(I18n.T("You attach a barcode to [].", this)));
            sales_tagger.paper_count = Dm.num(sales_tagger.paper_count) - 1;
            sticker = Dm.@new<obj_item_barcode>(this);
            sticker.payments_acc = sales_tagger.payments_acc; //new tag gets the tagger's current account.
            sticker.cut_multiplier = sales_tagger.cut_multiplier; //same, but for the percentage taken.

            foreach (obj wrapped_item in Dm.each<obj>(get_all_contents()))
            {
                if (HAS_TRAIT(wrapped_item, TRAIT_NO_BARCODES))
                    continue;
                await wrapped_item.AddComponent(typeof(datum_component_pricetag), Dm.list(sticker.payments_acc), sales_tagger.cut_multiplier);
            }
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_barcode)))
        {
            obj_item_barcode stickerA = tool as obj_item_barcode;
            if (sticker != null)
            {
                to_chat(user, span_warning(I18n.T("This package already has a barcode attached!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(stickerA.payments_acc))
            {
                to_chat(user, span_warning(I18n.T("This barcode seems to be invalid. Guess it's trash now.")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("For some reason, you can't attach []!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }
            sticker = stickerA;
            foreach (obj wrapped_item in Dm.each<obj>(get_all_contents()))
            {
                if (HAS_TRAIT(wrapped_item, TRAIT_NO_BARCODES))
                    continue;
                await wrapped_item.AddComponent(typeof(datum_component_pricetag), Dm.list(sticker.payments_acc), sticker.cut_multiplier);
            }
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_boxcutter)))
        {
            obj_item_boxcutter boxcutter_item = tool as obj_item_boxcutter;
            if (!HAS_TRAIT(boxcutter_item, TRAIT_TRANSFORM_ACTIVE))
            {
                balloon_alert(user, I18n.T("prime the boxcutter!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await attempt_pre_unwrap_contents(user, time: 0.5f * SECONDS)))
                return ITEM_INTERACT_BLOCKING;
            unwrap_contents();
            await post_unwrap_contents(user, rip_open: FALSE);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:203
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
        await DmTask.CompletedTask;
        return Dm.text("[] ([])", name, input); // This just repeatedly adds new labels, but i think that's intentional?
    }
}

/**
 * # Wrapped up crates and lockers - too big to carry.
 */
// tg: code/modules/recycling/sortingmachinery.dm:210
public partial class obj_item_delivery_big : obj_item_delivery
{
    // tg: code/modules/recycling/sortingmachinery.dm:221
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(await attempt_pre_unwrap_contents(user)))
            return null;
        unwrap_contents();
        await post_unwrap_contents();
        return null;
    }
}

/**
 * # Wrapped up items small enough to carry.
 */
// tg: code/modules/recycling/sortingmachinery.dm:230
public partial class obj_item_delivery_small : obj_item_delivery
{
    // tg: code/modules/recycling/sortingmachinery.dm:235
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await attempt_pre_unwrap_contents(user)))
            return null;
        await user.temporarilyRemoveItemFromInventory(this, TRUE);
        unwrap_contents();
        foreach (atom_movable movable_content in Dm.each<atom_movable>(contents.Copy()))
            await user.put_in_hands(movable_content as obj_item);
        await post_unwrap_contents(user);
        return null;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:244
    public override async DmTask<object> attack_self_tk(mob user = null)
    {
        if (ismob(loc))
        {
            mob M = loc as mob;
            await M.temporarilyRemoveItemFromInventory(this, TRUE);
            foreach (atom_movable movable_content in Dm.each<atom_movable>(contents.Copy()))
                await M.put_in_hands(movable_content as obj_item);
        }
        else
        {
            foreach (atom_movable movable_content in Dm.each<atom_movable>(contents.Copy()))
                await movable_content.forceMove(loc);
        }

        unwrap_contents();
        await post_unwrap_contents(user);
        return ITEM_INTERACT_BLOCKING;
    }
}

// tg: code/modules/recycling/sortingmachinery.dm:258
public partial class obj_item_dest_tagger : obj_item
{
    // tg: code/modules/recycling/sortingmachinery.dm:281
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins tagging [] final destination! It looks like [] trying to commit suicide!", user, user.p_their(), user.p_theyre())));
        if (Dm.truthy(is_species(user, typeof(datum_species_lizard)))) // islizard (__DEFINES/is_helpers.dm:138)
            to_chat(user, span_notice(I18n.T("*HELL*"))); //lizard nerf
        else
            to_chat(user, span_notice(I18n.T("*HEAVEN*")));
        playsound(this, Dm.resource("sound/machines/beep/twobeep_high.ogg"), 100, TRUE);
        await DmTask.CompletedTask;
        return BRUTELOSS;
    }

    /** Standard TGUI actions */
    // tg: code/modules/recycling/sortingmachinery.dm:291
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        add_fingerprint(user);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "DestinationTagger", name);
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }

    /** If the user dropped the tagger */
    // tg: code/modules/recycling/sortingmachinery.dm:300
    public override object ui_state(mob user = null)
    {
        return GLOB.inventory_state;
    }

    /** User activates in hand */
    // tg: code/modules/recycling/sortingmachinery.dm:304
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(locked_destination))
        {
            await ui_interact(user);
            return null;
        }
        return null;
    }

    /** Data sent to TGUI window */
    // tg: code/modules/recycling/sortingmachinery.dm:310
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["locations"] = GLOB.TAGGERLOCATIONS;
        data["currentTag"] = currTag;
        await DmTask.CompletedTask;
        return data;
    }

    /** User clicks a button on the tagger */
    // tg: code/modules/recycling/sortingmachinery.dm:317
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action as string)
        {
            case "change":
                float new_tag = round(Dm.num(text2num(@params["index"])));
                if (new_tag == currTag || new_tag < 1 || new_tag > length(GLOB.TAGGERLOCATIONS))
                    return null;
                currTag = new_tag;
                break;
        }
        return TRUE;
    }
}

// tg: code/modules/recycling/sortingmachinery.dm:329
public partial class obj_item_sales_tagger : obj_item
{
    // tg: code/modules/recycling/sortingmachinery.dm:351
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("[] has []/[] available barcodes. Refill with paper.", this, paper_count, max_paper_count)));
        Dm.append(ref dot, span_notice(I18n.T("Profit split on sale is currently set to []%. <b>Alt-click</b> to change.", round(Dm.num(cut_multiplier) * 100))));
        if (Dm.truthy(payments_acc))
            Dm.append(ref dot, span_notice(I18n.T("<b>Ctrl-click</b> to clear the registered account.")));
        return dot;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:358
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (isidcard(tool))
        {
            obj_item_card_id potential_acc = tool as obj_item_card_id;
            if (potential_acc.registered_account == null)
            {
                to_chat(user, span_warning(I18n.T("This ID card has no account registered!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (payments_acc == potential_acc.registered_account)
            {
                to_chat(user, span_notice(I18n.T("ID card already registered.")));
                return ITEM_INTERACT_BLOCKING;
            }
            payments_acc = potential_acc.registered_account;
            playsound(this, Dm.resource("sound/machines/ping.ogg"), 40, TRUE);
            to_chat(user, span_notice(I18n.T("[] registers the ID card. Tag a wrapped item to create a barcode.", this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_paper)))
        {
            if (Dm.num(paper_count) >= Dm.num(max_paper_count))
            {
                to_chat(user, span_notice(I18n.T("[]'s paper supply is full.", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            paper_count = Dm.num(paper_count) + 10;
            qdel(tool);
            if (Dm.num(paper_count) >= Dm.num(max_paper_count))
            {
                paper_count = max_paper_count;
                to_chat(user, span_notice(I18n.T("[]'s paper supply is now full.", this)));
                return ITEM_INTERACT_SUCCESS;
            }
            to_chat(user, span_notice(I18n.T("You refill []'s paper supply, you have [] left.", this, paper_count)));
            return ITEM_INTERACT_SUCCESS;
        }

        await DmTask.CompletedTask;
        return NONE;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:387
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.num(paper_count) <= 0)
        {
            to_chat(user, span_warning(I18n.T("You're out of paper!'.")));
            return dot;
        }
        if (!Dm.truthy(payments_acc))
        {
            to_chat(user, span_warning(I18n.T("You need to swipe [] with an ID card first.", this)));
            return dot;
        }
        paper_count = Dm.num(paper_count) - 1;
        playsound(this, Dm.resource("sound/machines/click.ogg"), 40, TRUE);
        to_chat(user, span_notice(I18n.T("You print a new barcode.")));
        obj_item_barcode new_barcode = Dm.@new<obj_item_barcode>(this);
        new_barcode.payments_acc = payments_acc; // The sticker gets the scanner's registered account.
        new_barcode.cut_multiplier = cut_multiplier; // Also the registered percent cut.
        await user.put_in_hands(new_barcode);
        return dot;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:403
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        payments_acc = null;
        to_chat(user, span_notice(I18n.T("You clear the registered account.")));
        await DmTask.CompletedTask;
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/recycling/sortingmachinery.dm:408
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        // DEVIATION(DV340): BYOND's input() as num|null is tgui's number prompt
        object potential_cut = await tgui_input_number(user, "How much would you like to pay out to the registered card?", Dm.text("Percentage Profit ([]% - []%)", round(Dm.num(cut_min) * 100), round(Dm.num(cut_max) * 100)));
        if (!Dm.truthy(potential_cut))
            cut_multiplier = Dm.num(Dm.initial(this, nameof(cut_multiplier)));
        cut_multiplier = Dm.clamp(round(Dm.num(potential_cut) / 100, Dm.num(cut_min)), Dm.num(cut_min), Dm.num(cut_max));
        to_chat(user, span_notice(I18n.T("[]% profit will be received if a package with a barcode is sold.", round(Dm.num(cut_multiplier) * 100))));
        return CLICK_ACTION_SUCCESS;
    }
}
