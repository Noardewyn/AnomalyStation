// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/bodybag.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J5 (D298): the unfolded body bags, their zip, tag and pinned paper; screentips are rows.

// tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:1
public partial class obj_structure_closet_body_bag : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:34
    public override object Destroy(float? force = null)
    {
        // If we have a stored bag, and it's in nullspace (not in someone's hand), delete it.
        if (foldedbag_instance != null && foldedbag_instance.loc == null)
        {
            qdel(foldedbag_instance);
            foldedbag_instance = null;
        }
        qdel(pinned);
        pinned = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:41
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
        tag_name = input;
        await update_icon();
        return Dm.truthy(tag_name) ? Dm.text($"{initial_name()} - {tag_name}") : initial_name();
    }

    private object initial_name() => Dm.initial(this, nameof(name));

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:47
    public override async DmTask rename_reset()
    {
        tag_name = null;
        await update_icon();
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:51
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
        => Dm.truthy(base.IsContainedAtomAccessible(contained, user)) ? TRUE : (contained == pinned ? TRUE : FALSE);

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:54
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(tag_name))
            dot.Add("bodybag_label");
        if (pinned != null)
        {
            dot.Add(pinned_overlay());
        }
        return dot;
    }

    // tg: bodybag.dm:58  paper_image: S3's paper reads is_empty and show_written_words (the inert paper is the unwritten picture)
    private object pinned_overlay()
    {
        mutable_appearance paper_image = mutable_appearance(icon, "paper") as mutable_appearance;
        paper_image.color = pinned.color;
        return paper_image;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:62
    public override async DmTask after_close(mob_living user = null, params object[] __extra)
    {
        await base.after_close(user, __extra);
        set_density(FALSE);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:66
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (!Dm.truthy(attempt_fold(user as mob_living_carbon_human)))
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        await perform_fold(user as mob_living_carbon_human);
        qdel(this);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    /**
     * Checks to see if we can fold. Return TRUE to actually perform the fold and delete.
     *
     * Arguments:
     * * the_folder - aka user
     */
    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:82
    public virtual object attempt_fold(mob_living_carbon_human the_folder = null)
    {
        if (the_folder == null)
            return FALSE;
        if (Dm.truthy(opened))
        {
            to_chat(the_folder, span_warning(I18n.T("You wrestle with [], but it won't fold while unzipped.", this)));
            return FALSE;
        }
        foreach (object content_thing in Dm.each(contents))
        {
            if (content_thing is mob || isobj(content_thing))
            {
                to_chat(the_folder, span_warning(I18n.T("There are too many things inside of [] to fold it up!", this)));
                return FALSE;
            }
        }
        // toto we made it!
        return TRUE;
    }

    /**
     * Performs the actual folding. Deleting is automatic, please do not include.
     *
     * Arguments:
     * * the_folder - aka user
     */
    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:102
    public virtual async DmTask perform_fold(mob_living_carbon_human the_folder = null)
    {
        visible_message(span_notice(I18n.T("[] folds up [].", the_folder, this)));
        await the_folder.put_in_hands(await undeploy_bodybag(the_folder.loc));
    }

    /// Makes the bag into an item, returns that item
    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:107
    public virtual async DmTask<obj_item_bodybag> undeploy_bodybag(atom fold_loc = null)
    {
        obj_item_bodybag folding_bodybag = foldedbag_instance ?? Dm.@new(foldedbag_path) as obj_item_bodybag;
        if (fold_loc != null)
            await folding_bodybag.forceMove(fold_loc);
        return folding_bodybag;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:113
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        // ideally we support this natively but i guess that's for a later time
        if (loc is not obj_machinery_disposal)
            return await base.container_resist_act(user, __extra);
        foreach (atom_movable thing in Dm.each<atom_movable>(contents.Copy()))
            await thing.forceMove(loc);
        await undeploy_bodybag(loc);
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:189
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        ADD_TRAIT(arrived, TRAIT_FLOORED, REF(this));
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:193
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        REMOVE_TRAIT(gone, TRAIT_FLOORED, REF(this));
        if (gone == pinned)
        {
            pinned = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:200
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(tag_name))
            dot.Add(span_info(I18n.T("The tag reads: []", tag_name)));
        if (pinned != null)
        {
            if (get_dist(user, this) <= 2 && user.client != null)
                await pinned.ui_interact(user);
            else
                dot.Add(span_smallnoticeital(I18n.T("There's a paper pinned to the bag, but you can't make out what it says.")));
        }
        if (Dm.truthy(can_scan_through))
            dot.Add(span_notice(I18n.T("The walls of the bag are thin enough to scan through via a <b>health analyzer</b>.")));
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:212
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if ((Dm.truthy(tag_name) && Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER)) || Dm.truthy(tool.get_sharpness()))
        {
            to_chat(user, span_notice(I18n.T("You cut the tag off [].", this)));
            playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
            tag_name = null;
            update_appearance();
            qdel(GetComponent(typeof(datum_component_rename)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (pinned == null && tool is obj_item_paper && Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
        {
            pinned = tool as obj_item_paper;
            await pinned.forceMove(this);
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(can_scan_through) && tool is obj_item_healthanalyzer)
        {
            foreach (mob_living inside in Dm.each<mob_living>(contents))
                return await tool.interact_with_atom(inside, user, modifiers);
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:233
    public override async DmTask<object> before_open(mob_living user = null, object force = null)
    {
        if (pinned != null)
        {
            if (Dm.truthy(force) || user == null || user.loc == this)
            {
                await pinned.forceMove(drop_location() as atom);
                return TRUE; // force open, no user, or we can't take the note out from inside
            }
            balloon_alert(user, I18n.T("paper removed"));
            if (!Dm.truthy(await user.put_in_inactive_hand(pinned)) && pinned.loc == this)
                await pinned.forceMove(drop_location() as atom);
            return FALSE; // blocked the open action
        }
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:244
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        if (pinned != null)
            await pinned.forceMove(drop_location() as atom);
        await base.handle_deconstruct(disassembled);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:123
public partial class obj_structure_closet_body_bag_bluespace : obj_structure_closet_body_bag
{
    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:132
    public override object attempt_fold(mob_living_carbon_human the_folder = null)
    {
        //copypaste zone, we do not want the content check so we don't want inheritance
        if (the_folder == null)
            return FALSE;
        if (Dm.truthy(opened))
        {
            to_chat(the_folder, span_warning(I18n.T("You wrestle with [], but it won't fold while unzipped.", this)));
            return FALSE;
        }
        //end copypaste zone
        if (contents.len >= Dm.num(mob_storage_capacity) / 2)
        {
            to_chat(the_folder, span_warning(I18n.T("There are too many things inside of [] to fold it up!", this)));
            return FALSE;
        }

        if (Dm.truthy(the_folder.in_contents_of(this)))
        {
            to_chat(the_folder, span_warning(I18n.T("You can't fold [] while you're inside of it!", this)));
            return FALSE;
        }

        foreach (obj_item_bodybag_bluespace B in Dm.each<obj_item_bodybag_bluespace>(contents))
        {
            to_chat(the_folder, span_warning(I18n.T("You can't recursively fold bluespace body bags!")));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:154
    public override async DmTask perform_fold(mob_living_carbon_human the_folder = null)
    {
        visible_message(span_notice(I18n.T("[] folds up [].", the_folder, this)));
        obj_item_bodybag folding_bodybag = await undeploy_bodybag(the_folder.loc);
        float max_weight_of_contents = Dm.num(Dm.initial(folding_bodybag, nameof(obj_item.w_class)));
        foreach (object am in Dm.each(contents.Copy()))
        {
            atom_movable content = am as atom_movable;
            await content.forceMove(folding_bodybag);
            if (isliving(content))
                to_chat(content, span_userdanger(I18n.T("You're suddenly forced into a tiny, compressed space!")));
            if (HAS_TRAIT(content, TRAIT_DWARF))
            {
                max_weight_of_contents = max(WEIGHT_CLASS_NORMAL, max_weight_of_contents);
                continue;
            }
            if (!isitem(content))
            {
                max_weight_of_contents = max(WEIGHT_CLASS_BULKY, max_weight_of_contents);
                continue;
            }
            obj_item A_is_item = content as obj_item;
            if (Dm.num(A_is_item.w_class) < max_weight_of_contents)
                continue;
            max_weight_of_contents = Dm.num(A_is_item.w_class);
        }
        folding_bodybag.update_weight_class(max_weight_of_contents);
        await the_folder.put_in_hands(folding_bodybag);
    }
}

/// Environmental bags. They protect against bad weather.

// tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:252
public partial class obj_structure_closet_body_bag_environmental : obj_structure_closet_body_bag
{
    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:266
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(weather_protection, INNATE_TRAIT);
        refresh_air();
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:271
    public override object Destroy(float? force = null)
    {
        if (air_contents != null)
        {
            qdel(air_contents);
            air_contents = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:276
    public override async DmTask<object> return_air()
    {
        refresh_air();
        return air_contents;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:280
    public override object remove_air(object amount = null)
    {
        refresh_air();
        return air_contents.remove(amount);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:284
    public override object return_analyzable_air()
    {
        refresh_air();
        return air_contents;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:288
    public override async DmTask<object> togglelock(mob_living user = null, object silent = null)
    {
        object dot = await base.togglelock(user, silent);
        foreach (mob_living target in Dm.each<mob_living>(contents))
            to_chat(target, span_warning(I18n.T("You hear a faint hiss, and a white mist fills your vision...")));
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:293
    public virtual void refresh_air()
    {
        air_contents = null;
        air_contents = Dm.@new<datum_gas_mixture>(50); //liters
        air_contents.temperature = T20C;

        air_contents.assert_gases(typeof(datum_gas_oxygen), typeof(datum_gas_nitrogen));
        air_contents.adjust_gas(typeof(datum_gas_oxygen), (ONE_ATMOSPHERE * 50) / (R_IDEAL_GAS_EQUATION * T20C) * O2STANDARD);
        air_contents.adjust_gas(typeof(datum_gas_nitrogen), (ONE_ATMOSPHERE * 50) / (R_IDEAL_GAS_EQUATION * T20C) * N2STANDARD);
    }
}

/// Securable enviro. bags

// tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:314
public partial class obj_structure_closet_body_bag_environmental_prisoner : obj_structure_closet_body_bag_environmental
{
    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:328
    public override object attempt_fold(mob_living_carbon_human the_folder = null)
    {
        if (Dm.truthy(cinched))
        {
            to_chat(the_folder, span_warning(I18n.T("You wrestle with [], but it won't fold while its straps are fastened.", this)));
            return FALSE;
        }
        return base.attempt_fold(the_folder);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:334
    public override async DmTask<object> before_open(mob_living user = null, object force = null)
    {
        object dot = await base.before_open(user, force);
        if (!Dm.truthy(dot))
            return FALSE;

        if (Dm.truthy(cinched) && !Dm.truthy(force))
        {
            to_chat(user, span_danger(I18n.T("The buckles on [] are cinched down, preventing it from opening.", this)));
            return FALSE;
        }

        cinched = FALSE; //in case it was forced open uncinch it
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:346
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        if (Dm.truthy(cinched))
            icon_state = Dm.text($"{Dm.initial(this, nameof(icon_state))}_cinched");
        else
            icon_state = Dm.initial(this, nameof(icon_state)) as string;
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:353
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        // copy-pasted with changes because flavor text as well as some other misc stuff
        if (Dm.truthy(opened) || ismovable(loc) || !Dm.truthy(cinched))
            return await base.container_resist_act(user, __extra);

        user.change_next_special_move(CLICK_CD_BREAKOUT);
        user.visible_message(span_warning(I18n.T("Someone in [] begins to wriggle!", this)),
            span_notice(I18n.T("You start wriggling, attempting to loosen []'s buckles... (this will take about [].)", this, DisplayTimeText(breakout_time))),
            span_hear(I18n.T("You hear straining cloth from [].", this)));
        if (Dm.truthy(await do_after(user, breakout_time, this)))
        {
            if (user == null || Dm.truthy(IS_UNCONSCIOUS_OR_CRIT(user)) || user.loc != this || Dm.truthy(opened) || !Dm.truthy(cinched))
                return null;
            //we check after a while whether there is a point of resisting anymore and whether the user is capable of resisting
            user.visible_message(span_danger(I18n.T("[] successfully broke out of []!", user, this)),
                span_notice(I18n.T("You successfully break out of []!", this)));
            if (loc is obj_machinery_disposal)
                return await base.container_resist_act(user, __extra);
            bust_open();
        }
        else
        {
            if (user.loc == this) //so we don't get the message if we resisted multiple times and succeeded.
                to_chat(user, span_warning(I18n.T("You fail to break out of []!", this)));
        }
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:376
    public override void bust_open()
    {
        cinched = FALSE;
        // We don't break the bag, because the buckles were backed out as opposed to fully broken.
        Waiting.Detach(open());
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:381
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(await user.can_perform_action(this)) || !isturf(loc))
            return null;
        await togglelock(user as mob_living);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:387
    public override async DmTask<object> togglelock(mob_living user = null, object silent = null)
    {
        if (Dm.truthy(opened))
        {
            to_chat(user, span_warning(I18n.T("You can't close the buckles while [] is unzipped!", this)));
            return null;
        }
        if (Dm.@in(user, contents))
        {
            to_chat(user, span_warning(I18n.T("You can't reach the buckles from here!")));
            return null;
        }
        if (iscarbon(user))
            add_fingerprint(user);
        if (!Dm.truthy(cinched))
        {
            foreach (mob_living target in Dm.each<mob_living>(contents))
                to_chat(target, span_userdanger(I18n.T("You feel the lining of [] tighten around you! Soon, you won't be able to escape!", this)));
            user.visible_message(span_notice(I18n.T("[] begins cinching down the buckles on [].", user, this)));
            if (!Dm.truthy(await do_after(user, cinch_time, this)))
                return null;
        }
        cinched = Dm.truthy(cinched) ? FALSE : TRUE;
        if (Dm.truthy(cinched))
            playsound(loc, cinch_sound, 15, TRUE, -2);
        user.visible_message(span_notice(I18n.T("[] []cinches [].", user, Dm.truthy(cinched) ? "" : "un", this)),
            span_notice(I18n.T("You []cinch [].", Dm.truthy(cinched) ? "" : "un", this)),
            span_hear(I18n.T("You hear stretching followed by metal clicking from [].", this)));
        user.log_message(Dm.text("[] secure environmental bag []", Dm.truthy(cinched) ? "cinched" : "uncinched", this), LOG_GAME);
        update_appearance();
        return null;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:423
public partial class obj_structure_closet_body_bag_environmental_prisoner_pressurized_syndicate : obj_structure_closet_body_bag_environmental_prisoner_pressurized
{
    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:423
    public override void refresh_air()
    {
        air_contents = null;
        air_contents = Dm.@new<datum_gas_mixture>(50); //liters
        air_contents.temperature = T20C;

        air_contents.assert_gases(typeof(datum_gas_oxygen), typeof(datum_gas_nitrous_oxide));
        air_contents.adjust_gas(typeof(datum_gas_oxygen), (ONE_ATMOSPHERE * 50) / (R_IDEAL_GAS_EQUATION * T20C) * O2STANDARD);
        air_contents.adjust_gas(typeof(datum_gas_nitrous_oxide), (ONE_ATMOSPHERE * 50) / (R_IDEAL_GAS_EQUATION * T20C) * N2STANDARD);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:432
public partial class obj_structure_closet_body_bag_environmental_hardlight : obj_structure_closet_body_bag_environmental
{
    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:441
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        if (Dm.@in(damage_type, Dm.list(BRUTE, BURN)))
            playsound(this, Dm.resource("sound/items/weapons/egloves.ogg"), 80, TRUE);
        return null;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:445
public partial class obj_structure_closet_body_bag_environmental_prisoner_hardlight : obj_structure_closet_body_bag_environmental_prisoner
{
    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:454
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        if (Dm.@in(damage_type, Dm.list(BRUTE, BURN)))
            playsound(this, Dm.resource("sound/items/weapons/egloves.ogg"), 80, TRUE);
        return null;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:458
public partial class obj_structure_closet_body_bag_environmental_stasis : obj_structure_closet_body_bag_environmental
{
    /// Base color filter applied to the bodybag, adjusted based on integrity
    private static DmList base_color_filter() => Dm.list(1, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0);

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:483
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_filter("stasis_color", 1, color_matrix_filter(base_color_filter()) as DmList);
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:487
    public override void on_update_integrity(object old_value = null, object new_value = null)
    {
        base.on_update_integrity(old_value, new_value);
        if (!(last_filter_update <= world.time))
            return;

        float damage_percent = 1 - Dm.num(get_integrity_percentage());
        DmList new_color_filter = base_color_filter();
        new_color_filter[1] = Dm.num(new_color_filter[1]) - (0.5f * damage_percent);
        new_color_filter[5] = Dm.num(new_color_filter[5]) - (0.5f * damage_percent);
        new_color_filter[9] = Dm.num(new_color_filter[9]) - (1.25f * damage_percent);

        if (Dm.eq(last_filter_update, -1))
            modify_filter("stasis_color", color_matrix_filter(new_color_filter) as DmList, TRUE);
        else
            transition_filter("stasis_color", color_matrix_filter(new_color_filter) as DmList, 1 * SECONDS);

        last_filter_update = world.time + (1 * SECONDS);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:505
    public override void refresh_air()
    {
        float mol_count = 50;
        float inner_temp = T0C - 60;
        air_contents = null;
        air_contents = Dm.@new<datum_gas_mixture>(mol_count);
        air_contents.set_temperature(inner_temp);

        air_contents.set_gas(typeof(datum_gas_oxygen), (ONE_ATMOSPHERE * mol_count) / (R_IDEAL_GAS_EQUATION * inner_temp) * O2STANDARD);
        air_contents.set_gas(typeof(datum_gas_nitrogen), (ONE_ATMOSPHERE * mol_count) / (R_IDEAL_GAS_EQUATION * inner_temp) * N2STANDARD);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:515
    public override object examine_status(mob user = null)
    {
        float percent = 100 * Dm.num(get_integrity_percentage());
        if (percent >= 50 && percent <= 75)
            return span_warning(I18n.T("It looks worn."));
        if (percent >= 25 && percent <= 50)
            return span_warning(I18n.T("It appears moderately worn."));
        if (percent >= 0 && percent <= 25)
            return span_boldwarning(I18n.T("It's falling apart!"));
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:524
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (isorgan(arrived))
        {
            obj_item_organ organ_arrived = arrived as obj_item_organ;
            organ_arrived.organ_flags = Dm.bitor(organ_arrived.organ_flags, ORGAN_FROZEN);
            return dot;
        }

        if (isbodypart(arrived))
        {
            foreach (obj_item_organ organ in Dm.each<obj_item_organ>(arrived.contents))
                organ.organ_flags = Dm.bitor(organ.organ_flags, ORGAN_FROZEN);
            return dot;
        }

        if (isliving(arrived))
        {
            if (!Dm.eq(seconds_freezing, -1))
                return dot;
            START_PROCESSING(SSobj, this);
        }
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:541
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (isorgan(gone))
        {
            obj_item_organ organ_gone = gone as obj_item_organ;
            organ_gone.organ_flags = Dm.bitand(organ_gone.organ_flags, Dm.bitnot(ORGAN_FROZEN));
            return dot;
        }

        if (isbodypart(gone))
        {
            foreach (obj_item_organ organ in Dm.each<obj_item_organ>(gone.contents))
                organ.organ_flags = Dm.bitand(organ.organ_flags, Dm.bitnot(ORGAN_FROZEN));
            return dot;
        }

        if (isliving(gone))
        {
            seconds_freezing = -1;
            STOP_PROCESSING(SSobj, this);
            if (HAS_TRAIT(gone, TRAIT_STASIS))
                await remove_stasis(gone as mob_living);
        }
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:559
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        mob_living freezing = Dm.locate<mob_living>(this);
        if (freezing == null)
            return PROCESS_KILL;

        seconds_freezing = Dm.num(seconds_freezing) + Dm.num(seconds_per_tick);

        if (Dm.num(seconds_freezing) > 3 && Dm.truthy(freezing.on_fire))
            freezing.extinguish_mob();

        if (Dm.num(seconds_freezing) > 5 && !HAS_TRAIT(freezing, TRAIT_STASIS))
            Waiting.Detach(apply_stasis(freezing));

        if (SPT_PROB(2 * (Dm.num(seconds_freezing) / 60), seconds_per_tick))
            Waiting.Detach(freezing.Unconscious(1 * SECONDS));

        datum_gas_mixture air = loc == null ? null : Waiting.NoSleep(loc.return_air()) as datum_gas_mixture;
        if (air != null && Dm.num(air.return_temperature()) > T0C)
        {
            // Bout two minutes of time
            Waiting.Detach(take_damage(Dm.num(max_integrity) * 0.004f * Dm.num(seconds_per_tick), sound_effect: FALSE));
        }
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:580
    public override async DmTask after_open(mob_living user = null, float? force = null)
    {
        await base.after_open(user, force);
        if ((freeze_sound_cd <= world.time) && Dm.locate<mob_living>(loc) != null)
            playsound(this, Dm.resource("sound/effects/spray.ogg"), 25, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE, frequency: 0.4f);
        freeze_sound_cd = world.time + (2 * SECONDS);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:586
    public virtual async DmTask apply_stasis(mob_living target = null)
    {
        await target.apply_status_effect(typeof(datum_status_effect_grouped_stasis), REF(this));
        if (!Dm.truthy(INCAPACITATED_IGNORING(target, INCAPABLE_STASIS)))
            to_chat(target, span_notice(I18n.T("You feel a cold, numbing sensation...")));
        RegisterSignal(target, COMSIG_LIVING_EARLY_UNARMED_ATTACK, nameof(skip_to_attack_hand));
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:592
    public override async DmTask after_close(mob_living user = null, params object[] __extra)
    {
        await base.after_close(user, __extra);
        if ((freeze_sound_cd <= world.time) && Dm.locate<mob_living>(this) != null)
            playsound(this, Dm.resource("sound/effects/spray.ogg"), 25, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE, frequency: 0.5f);
        freeze_sound_cd = world.time + (2 * SECONDS);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:598
    public virtual async DmTask remove_stasis(mob_living target = null)
    {
        target.remove_status_effect(typeof(datum_status_effect_grouped_stasis), REF(this));
        if (!Dm.truthy(INCAPACITATED_IGNORING(target, INCAPABLE_STASIS)))
            to_chat(target, span_notice(I18n.T("You can feel your fingers and toes again.")));
        UnregisterSignal(target, COMSIG_LIVING_EARLY_UNARMED_ATTACK);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:604
    public override async DmTask<obj_item_bodybag> undeploy_bodybag(atom fold_loc = null)
    {
        obj_item_bodybag folded = await base.undeploy_bodybag(fold_loc);
        folded.update_integrity(get_integrity());
        return folded;
    }

    /// Being in the bag applies hands_blocked which means you can't click the bag to open it
    /// Soooo we have to manually hook hearly unarmed attack (pre-hands-blocked check) to allow opening the bag via click
    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:611
    [SignalHandler]
    public virtual object skip_to_attack_hand(mob_living source = null, atom attack_target = null)
    {
        if (attack_target == this)
        {
            Waiting.Detach(container_resist_act(source));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        return NONE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:618
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        if (Dm.truthy(opened) || ismovable(loc))
            return await base.container_resist_act(user, __extra);
        if (!HAS_TRAIT(user, TRAIT_STASIS))
        {
            await open(user);
            return null;
        }

        user.change_next_special_move(6 * SECONDS);
        user.visible_message(
            span_warning(I18n.T("Something in [] begins to wriggle!", this)),
            span_notice(I18n.T("You start wriggling, attempting to climb out of []... (This will take about [].)", this, DisplayTimeText(breakout_time))),
            span_hear(I18n.T("You hear straining cloth from [].", this)));
        if (Dm.truthy(await do_after(user, breakout_time, this, timed_action_flags: IGNORE_TARGET_LOC_CHANGE, extra_checks: Dm.@new<datum_callback>(this, nameof(breakout_checks), user))))
        {
            user.visible_message(
                span_danger(I18n.T("[] climbs out of []!", user, this)),
                span_notice(I18n.T("You successfully climb out of []!", this)));
            await open(user, force: TRUE, special_effects: FALSE);
        }
        else if (!QDELETED(user) && user.loc == this)
            user.show_message(I18n.T("You fail to break out of []!", this), MSG_VISUAL);
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:641
    public virtual object breakout_checks(mob_living user = null)
    {
        if (QDELETED(user) || Dm.truthy(IS_UNCONSCIOUS_OR_CRIT(user)) || user.loc != this || Dm.truthy(opened))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:646
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        if (!Dm.truthy(Dm.bitand(obj_flags, NO_DEBRIS_AFTER_DECONSTRUCTION)))
        {
            Dm.@new<obj_effect_decal_cleanable_shreds>(loc, name);
            Dm.@new<obj_item_stack_sheet_cloth>(loc, 4);
        }
        loc.visible_message(span_warning(I18n.T("[] unwinds into threads!", this)), vision_distance: COMBAT_MESSAGE_RANGE);
        playsound(loc, Dm.resource("sound/items/duct_tape/duct_tape_rip.ogg"), 50, TRUE, frequency: 0.5f);
        foreach (mob_living left_behind in Dm.each<mob_living>(contents))
            await left_behind.Knockdown(3 * SECONDS);
        await base.handle_deconstruct(disassembled);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/bodybag.dm:656
    public override void get_remote_view_fullscreens(mob user = null)
    {
        if (Dm.eq(user.stat, DEAD) || !Dm.truthy(Dm.bitand(user.sight, Dm.bitor(SEEOBJS, SEEMOBS))))
            user.overlay_fullscreen("remote_view", typeof(atom_movable_screen_fullscreen_impaired), 2);
    }
}
