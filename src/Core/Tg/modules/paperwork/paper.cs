// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/paper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298). Left out: the message in a bottle's queue (SSpersistence is not translated) and the paper shown
// through a camera, whose camera holder nothing sets (the AI and the camera's paper note are not ported); both are translated as
// tg wrote them where they only read state. The var blocks are generated, but datum_paper_field's field_index (text in tg).

/**
 * Paper
 * also scraps of paper
 *
 * lipstick wiping is in code/game/objects/items/weapons/cosmetics.dm!
 */

/**
 * Paper is now using markdown (like in github pull notes) for ALL rendering
 * so we do loose a bit of functionality but we gain in easy of use of
 * paper and getting rid of that crashing bug
 */
// tg: code/modules/paperwork/paper.dm:14
public partial class obj_item_paper : obj_item
{
    // tg: code/modules/paperwork/paper.dm:72
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = base_pixel_x + rand(-9, 9);
        pixel_y = base_pixel_y + rand(-8, 8);

        if (Dm.truthy(default_raw_text))
            add_raw_text(default_raw_text);

        update_appearance();

        if (Dm.truthy(can_become_message_in_bottle) && !Dm.truthy(mapload) && prob(MESSAGE_BOTTLE_CHANCE))
            Dm.Deferred("code/modules/paperwork/paper.dm:83", "Initialize: LAZYADD(SSpersistence.queued_message_bottles, src), the message in a bottle kept between rounds (SSpersistence is not translated)");

        Waiting.NoSleep(AddElement(typeof(datum_element_burn_on_item_ignition)));
        RegisterSignal(this, COMSIG_ATOM_IGNITED_BY_ITEM, nameof(close_paper_ui));
        return dot;
    }

    // tg: code/modules/paperwork/paper.dm:89
    public override object Destroy(float? force = null)
    {
        camera_holder = null;
        clear_paper();
        // LAZYREMOVE(SSpersistence.queued_message_bottles, src): never queued (Initialize)
        return base.Destroy(force);
    }

    // tg: code/modules/paperwork/paper.dm:95
    public override object custom_fire_overlay__proc()
    {
        if (!Dm.truthy(custom_fire_overlay))
            custom_fire_overlay = mutable_appearance(Dm.resource("icons/obj/service/bureaucracy.dmi"), "paper_onfire_overlay", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART));
        return custom_fire_overlay;
    }

    // tg: code/modules/paperwork/paper.dm:100
    public virtual void close_paper_ui()
    {
        SStgui.close_uis(this);
    }

    /// Determines whether this paper has been written or stamped to.
    // tg: code/modules/paperwork/paper.dm:105
    public virtual object is_empty()
    {
        return Dm.num(!(Dm.truthy(LAZYLEN(raw_text_inputs)) || Dm.truthy(LAZYLEN(raw_stamp_data))));
    }

    /// Returns a deep copy list of raw_text_inputs, or null if the list is empty or doesn't exist.
    // tg: code/modules/paperwork/paper.dm:109
    public virtual object copy_raw_text()
    {
        if (!Dm.truthy(LAZYLEN(raw_text_inputs)))
            return null;

        DmList copy_text = Dm.list();

        foreach (datum_paper_input existing_input in Dm.each<datum_paper_input>(raw_text_inputs))
            Dm.append(copy_text, existing_input.make_copy());

        return copy_text;
    }

    /// Returns a deep copy list of raw_field_input_data, or null if the list is empty or doesn't exist.
    // tg: code/modules/paperwork/paper.dm:121
    public virtual object copy_field_text()
    {
        if (!Dm.truthy(LAZYLEN(raw_field_input_data)))
            return null;

        DmList copy_text = Dm.list();

        foreach (datum_paper_field existing_input in Dm.each<datum_paper_field>(raw_field_input_data))
            Dm.append(copy_text, existing_input.make_copy());

        return copy_text;
    }

    /// Returns a deep copy list of raw_stamp_data, or null if the list is empty or doesn't exist. Does not copy overlays or stamp_cache, only the tgui rendered stamps.
    // tg: code/modules/paperwork/paper.dm:133
    public virtual object copy_raw_stamps()
    {
        if (!Dm.truthy(LAZYLEN(raw_stamp_data)))
            return null;

        DmList copy_stamps = Dm.list();

        foreach (datum_paper_stamp existing_input in Dm.each<datum_paper_stamp>(raw_stamp_data))
            Dm.append(copy_stamps, existing_input.make_copy());

        return copy_stamps;
    }

    /**
     * This proc copies this sheet of paper to a new
     * sheet. Used by carbon papers and the photocopier machine.
     *
     * Arguments
     * * paper_type - Type path of the new paper to create. Can copy anything to anything.
     * * location - Where to spawn in the new copied paper.
     * * colored - If true, the copied paper will be coloured and will inherit all colours.
     * * greyscale_override - If set to a colour string and coloured is false, it will override the default of COLOR_WEBSAFE_DARK_GRAY when copying.
     */
    // tg: code/modules/paperwork/paper.dm:155  (paper_type is object, Appendix A: a typepath or the paper instance the photocopier passes)
    public virtual object copy(object paper_type = null, atom location = null, float? colored = null, object greyscale_override = null)
    {
        paper_type ??= typeof(obj_item_paper);
        location ??= loc;
        colored ??= TRUE;
        obj_item_paper new_paper;
        if (paper_type is Type path && ispath(path, typeof(obj_item_paper)))
            new_paper = Dm.@new(path, location) as obj_item_paper;
        else if (paper_type is obj_item_paper instance)
            new_paper = instance;
        else
            throw Dm.CRASH(Dm.text("invalid paper_type [], paper type path or instance expected", paper_type), "code/modules/paperwork/paper.dm", 162);

        new_paper.raw_text_inputs = copy_raw_text() as DmList;
        new_paper.raw_field_input_data = copy_field_text() as DmList;

        if (Dm.truthy(colored))
            new_paper.color = color;
        else
        {
            object new_color = Dm.truthy(greyscale_override) ? greyscale_override : COLOR_WEBSAFE_DARK_GRAY;
            foreach (datum_paper_input text in Dm.each<datum_paper_input>(new_paper.raw_text_inputs))
                text.colour = new_color as string;

            foreach (datum_paper_field text in Dm.each<datum_paper_field>(new_paper.raw_field_input_data))
                text.field_data.colour = new_color as string;
        }

        new_paper.input_field_count = input_field_count;
        new_paper.raw_stamp_data = copy_raw_stamps() as DmList;
        new_paper.stamp_cache = stamp_cache?.Copy();
        Waiting.NoSleep(new_paper.update_icon_state());
        new_paper.copy_overlays(this);
        return new_paper;
    }

    /**
     * This simple helper adds the supplied raw text to the paper, appending to the end of any existing contents.
     *
     * This a God proc that does not care about paper max length and expects sanity checking beforehand if you want to respect it.
     *
     * The caller is expected to handle updating icons and appearance after adding text, to allow for more efficient batch adding loops.
     * * Arguments:
     * * text - The text to append to the paper.
     * * font - The font to use.
     * * color - The font color to use.
     * * bold - Whether this text should be rendered completely bold.
     * * advanced_html - Boolean that is true when the writer has R_FUN permission, which sanitizes less HTML (such as images) from the new paper_input
     */
    // tg: code/modules/paperwork/paper.dm:199
    public virtual void add_raw_text(object text = null, object font = null, object color = null, object bold = null, object advanced_html = null)
    {
        datum_paper_input new_input_datum = Dm.@new<datum_paper_input>(
            text,
            font,
            color,
            bold,
            advanced_html);

        input_field_count += Dm.num(get_input_field_count(text));

        LAZYADD(ref raw_text_inputs, new_input_datum);
    }

    /**
     * This simple helper adds the supplied input field data to the paper.
     *
     * It will not overwrite any existing input field data by default and will early return FALSE if this scenario happens unless overwrite is
     * set properly.
     *
     * Other than that, this is a God proc that does not care about max length or out-of-range IDs and expects sanity checking beforehand if
     * you want to respect it.
     *
     * * Arguments:
     * * field_id - The ID number of the field to which this data applies.
     * * text - The text to append to the paper.
     * * font - The font to use.
     * * color - The font color to use.
     * * bold - Whether this text should be rendered completely bold.
     * * overwrite - If TRUE, will overwrite existing field ID's data if it exists.
     */
    // tg: code/modules/paperwork/paper.dm:229
    public virtual object add_field_input(object field_id = null, object text = null, object font = null, object color = null, object bold = null, object signature_name = null, float? overwrite = null)
    {
        overwrite ??= FALSE;
        datum_paper_field field_data_datum = null;

        bool is_signature = Dm.eq(text, "%sign") || Dm.eq(text, "%s");
        bool is_date = Dm.eq(text, "%date") || Dm.eq(text, "%d");
        bool is_time = Dm.eq(text, "%time") || Dm.eq(text, "%t");

        object field_text = text;
        if (is_signature)
            field_text = signature_name;
        else if (is_date)
            field_text = Dm.text("[]", server_timestamp("DD/MM/YYYY", ic_time: TRUE));
        else if (is_time)
            field_text = round_timestamp();

        object field_font = is_signature ? SIGNATURE_FONT : font;

        foreach (datum_paper_field field_input in Dm.each<datum_paper_field>(raw_field_input_data))
        {
            if (Dm.eq(field_input.field_index, field_id))
            {
                if (!Dm.truthy(overwrite))
                    return FALSE;
                field_data_datum = field_input;
                break;
            }
        }

        if (field_data_datum == null)
        {
            datum_paper_field new_field_input_datum = Dm.@new<datum_paper_field>(
                field_id,
                field_text,
                field_font,
                color,
                bold,
                Dm.num(is_signature));
            LAZYADD(ref raw_field_input_data, new_field_input_datum);
            return TRUE;
        }

        datum_paper_input new_input_datum = Dm.@new<datum_paper_input>(
            field_text,
            field_font,
            color,
            bold);

        field_data_datum.field_data = new_input_datum;
        field_data_datum.is_signature = Dm.num(is_signature);

        return TRUE;
    }

    /**
     * This simple helper adds the supplied stamp to the paper, appending to the end of any existing stamps.
     *
     * This a God proc that does not care about stamp max count and expects sanity checking beforehand if you want to respect it.
     *
     * It does however respect the overlay limit and will not apply any overlays past the cap.
     *
     * The caller is expected to handle updating icons and appearance after adding text, to allow for more efficient batch adding loops.
     * * Arguments:
     * * stamp_class - Div class for the stamp.
     * * stamp_x - X coordinate to render the stamp in tgui.
     * * stamp_y - Y coordinate to render the stamp in tgui.
     * * rotation - Degrees of rotation for the stamp to be rendered with in tgui.
     * * stamp_icon_state - Icon state for the stamp as part of overlay rendering.
    * * stamp_icon_state - An alternate Icon file can be passed for the stamp as part of overlay rendering if desired
     */
    // tg: code/modules/paperwork/paper.dm:296
    public virtual void add_stamp(object stamp_class = null, object stamp_x = null, object stamp_y = null, object rotation = null, object stamp_icon_state = null, object stamp_icon = null)
    {
        stamp_icon ??= Dm.resource("icons/obj/service/bureaucracy.dmi");
        datum_paper_stamp new_stamp_datum = Dm.@new<datum_paper_stamp>(stamp_class, stamp_x, stamp_y, rotation);
        LAZYADD(ref raw_stamp_data, new_stamp_datum);

        if (LAZYLEN(stamp_cache) > MAX_PAPER_STAMPS_OVERLAYS)
            return;

        mutable_appearance stamp_overlay = mutable_appearance(stamp_icon, Dm.text("paper_[]", stamp_icon_state), appearance_flags: Dm.bitor(KEEP_APART, RESET_COLOR)) as mutable_appearance;
        stamp_overlay.pixel_w = rand(-2, 2);
        stamp_overlay.pixel_z = rand(-3, 2);
        add_overlay(stamp_overlay);
        LAZYADD(ref stamp_cache, stamp_icon_state);
    }

    /// Removes all input and all stamps from the paper, clearing it completely.
    // tg: code/modules/paperwork/paper.dm:310
    public virtual void clear_paper()
    {
        raw_text_inputs = null; // LAZYNULL
        raw_stamp_data = null;
        raw_field_input_data = null;
        stamp_cache = null;

        cut_overlays();
        update_appearance();
    }

    // tg: code/modules/paperwork/paper.dm:319
    public override async DmTask<object> pickup(mob user = null)
    {
        if (Dm.truthy(contact_poison) && ishuman(user))
        {
            mob_living_carbon_human H = (mob_living_carbon_human)user;
            obj_item_clothing_gloves G = H.gloves as obj_item_clothing_gloves;
            if (G == null || !Dm.truthy(Dm.bitand(G.body_parts_covered, HANDS)) || HAS_TRAIT(G, TRAIT_FINGERPRINT_PASSTHROUGH) || HAS_TRAIT(H, TRAIT_FINGERPRINT_PASSTHROUGH))
            {
                await ((datum_reagents)H.reagents).add_reagent(contact_poison, contact_poison_volume);
                contact_poison = null;
            }
        }
        return await base.pickup(user);
    }

    // tg: code/modules/paperwork/paper.dm:328
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(LAZYLEN(raw_text_inputs)) && Dm.truthy(show_written_words))
            icon_state = Dm.text("[]_words", Dm.initial(this, nameof(icon_state)));
        else
            icon_state = (string)Dm.initial(this, nameof(icon_state));
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/paperwork/paper.dm:335  GAME_VERB_SRC(/obj/item/paper, rename, usr, "Rename paper", null): the body
    public virtual async DmTask __gvb_rename(DmList structured_args = null)
    {
        // isAdminGhostAI is not declared (no admin ghosts yet, as is_helpers.cs)
        if (!Dm.truthy(usr.can_read(this)) || Dm.truthy(usr.is_blind()) || Dm.truthy(INCAPACITATED_IGNORING(usr, Dm.bitor(INCAPABLE_RESTRAINTS, INCAPABLE_GRAB))) || isobserver(usr))
            return;
        if (ishuman(usr))
        {
            mob_living_carbon_human H = (mob_living_carbon_human)usr;
            if (HAS_TRAIT(H, TRAIT_CLUMSY) && prob(25))
            {
                to_chat(H, span_warning(I18n.T("You cut yourself on the paper! Ahhhh! Ahhhhh!")));
                H.damageoverlaytemp = 9001;
                H.update_damage_hud();
                return;
            }
        }
        object n_name = await tgui_input_text(usr, I18n.T("Enter a paper label"), I18n.T("Paper Labelling"), max_length: MAX_NAME_LEN);
        if (n_name == null || Dm.eq(n_name, ""))
            return;
        if ((loc == usr || istype(loc, typeof(obj_item_clipboard))) && !IS_UNCONSCIOUS_OR_CRIT(usr))
            name = I18n.T("paper[]", Dm.truthy(n_name) ? I18n.T("- '[]'", n_name) : null);
        add_fingerprint(usr);
        await update_static_data(usr);
    }

    // tg: code/modules/paperwork/paper.dm:335  the verb proc
    public virtual void rename(params object[] __extra)
    {
        Waiting.Detach(__gvb_rename(Dm.list()));
    }

    // tg: code/modules/paperwork/paper.dm:351
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] scratches a grid on [] wrist with the paper! It looks like [] trying to commit sudoku...", user, user.p_their(), user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/modules/paperwork/paper.dm:355
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Alt-click [] to fold it into a paper plane.", this)));
        if (!in_range(user, this) && !isobserver(user))
        {
            Dm.append(ref dot, span_warning(I18n.T("You're too far away to read it!")));
            return dot;
        }

        if (Dm.truthy(user.is_blind()))
        {
            to_chat(user, span_warning(I18n.T("You are blind and can't read anything!")));
            return dot;
        }

        if (Dm.truthy(user.can_read(this)))
        {
            await ui_interact(user);
            return dot;
        }
        Dm.append(ref dot, span_warning(I18n.T("You cannot read it!")));
        return dot;
    }

    // tg: code/modules/paperwork/paper.dm:371
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        // Are we on fire?  Hard to read if so
        if (Dm.truthy(Dm.bitand(resistance_flags, ON_FIRE)))
            return UI_CLOSE;
        if ((camera_holder != null && Dm.truthy(can_show_to_mob_through_camera(user as mob_living))) || Dm.truthy(request_state))
            return UI_UPDATE;
        if (!in_range(user, this) && !isobserver(user))
            return UI_CLOSE;
        // isAdminGhostAI is not declared (no admin ghosts yet, as is_helpers.cs)
        if (Dm.truthy(INCAPACITATED_IGNORING(user, Dm.bitor(INCAPABLE_RESTRAINTS, INCAPABLE_GRAB))) || isobserver(user))
            return UI_UPDATE;
        // Even harder to read if your blind...braile? humm
        // .. or if you cannot read
        if (Dm.truthy(user.is_blind()))
        {
            to_chat(user, span_warning(I18n.T("You are blind and can't read anything!")));
            return UI_CLOSE;
        }
        if (!Dm.truthy(user.can_read(this)))
            return UI_CLOSE;
        if (ismovable(loc) && Dm.truthy(loc.IsContainedAtomAccessible(this, user)))
            return UI_INTERACTIVE;
        return await base.ui_status(user, state);
    }

    // tg: code/modules/paperwork/paper.dm:392
    public override async DmTask<object> can_interact(mob user = null, float? require_adjacent_turf = null)
    {
        if (ismovable(loc) && Dm.truthy(loc.IsContainedAtomAccessible(this, user)))
            return TRUE;
        return await base.can_interact(user, require_adjacent_turf);
    }

    // tg: code/modules/paperwork/paper.dm:397
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (HAS_TRAIT(user, TRAIT_PAPER_MASTER))
        {
            await make_plane(user as mob_living, typeof(obj_item_paperplane_syndicate));
            return CLICK_ACTION_SUCCESS;
        }
        await make_plane(user as mob_living, typeof(obj_item_paperplane));
        return CLICK_ACTION_SUCCESS;
    }

    /**
     * Paper plane folding
     * Makes a paperplane depending on args and returns it.
     *
     * Arguments:
     * * mob/living/user - who's folding
     * * plane_type - what it will be folded into (path)
     */
    // tg: code/modules/paperwork/paper.dm:413
    public virtual async DmTask<object> make_plane(mob_living user = null, Type plane_type = null)
    {
        plane_type ??= typeof(obj_item_paperplane);
        loc.balloon_alert(user, I18n.T("folded into a plane"));
        await user.temporarilyRemoveItemFromInventory(this);
        obj_item_paperplane new_plane = Dm.@new(plane_type, loc, this) as obj_item_paperplane;
        if (Dm.truthy(await user.Adjacent(new_plane)))
            await user.put_in_hands(new_plane);
        return new_plane;
    }

    // tg: code/modules/paperwork/paper.dm:421
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // Enable picking paper up by clicking on it with a clipboard, paper bin, or folder
        if (istype(tool, typeof(obj_item_clipboard)) || istype(tool, typeof(obj_item_paper_bin)) || istype(tool, typeof(obj_item_folder)))
        {
            await tool.item_interaction(user, this);
            return ITEM_INTERACT_SUCCESS;
        }

        // Handle writing items.
        DmList writing_stats = await tool.get_writing_implement_details() as DmList;

        if (writing_stats == null)
        {
            await ui_interact(user);
            return NONE;
        }

        if (Dm.eq(writing_stats["interaction_mode"], MODE_WRITING))
        {
            if (!Dm.truthy(await user.can_write(tool)))
                return ITEM_INTERACT_BLOCKING;
            if (Dm.num(get_total_length()) >= MAX_PAPER_LENGTH)
            {
                to_chat(user, span_warning(I18n.T("This sheet of paper is full!")));
                return ITEM_INTERACT_BLOCKING;
            }

            await ui_interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        // Handle stamping items.
        if (Dm.eq(writing_stats["interaction_mode"], MODE_STAMPING))
        {
            if (!Dm.truthy(user.can_read(this)) || Dm.truthy(user.is_blind()))
            {
                //The paper's stampable window area is assumed approx 300x400
                add_stamp(writing_stats["stamp_class"], rand(0, 300), rand(0, 400), rand(0, 360), writing_stats["stamp_icon_state"], stamp_icon: writing_stats["stamp_icon"]);
                user.visible_message(span_notice(I18n.T("[] blindly stamps [] with \\the []!", user, this, tool)));
                to_chat(user, span_notice(I18n.T("You stamp [] with \\the [] the best you can!", this, tool)));
                playsound(this, Dm.resource("sound/items/handling/standard_stamp.ogg"), 50, vary: TRUE);
            }
            else
            {
                to_chat(user, span_notice(I18n.T("You ready your stamp over the paper! ")));
                await ui_interact(user);
            }
            return ITEM_INTERACT_SUCCESS;
        }

        await ui_interact(user);
        return NONE;
    }

    /// Secondary right click interaction to quickly stamp things
    // tg: code/modules/paperwork/paper.dm:467
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        DmList writing_stats = await tool.get_writing_implement_details() as DmList;

        if (!Dm.truthy(length(writing_stats)))
            return NONE;
        if (!Dm.eq(writing_stats["interaction_mode"], MODE_STAMPING))
            return NONE;
        if (!Dm.truthy(user.can_read(this)) || Dm.truthy(user.is_blind())) // Just leftclick instead
            return NONE;

        add_stamp(writing_stats["stamp_class"], rand(1, 300), rand(1, 400), stamp_icon_state: writing_stats["stamp_icon_state"], stamp_icon: writing_stats["stamp_icon"]);
        user.visible_message(
            span_notice(I18n.T("[] quickly stamps [] with [] without looking.", user, this, tool)),
            span_notice(I18n.T("You quickly stamp [] with [] without looking.", this, tool)));
        playsound(this, Dm.resource("sound/items/handling/standard_stamp.ogg"), 50, vary: TRUE);

        return ITEM_INTERACT_BLOCKING; // Stop the UI from opening.
    }

    /**
     * Attempts to ui_interact the paper to the given user, with some sanity checking
     * to make sure the camera still exists via the weakref and that this paper is still
     * attached to it.
     */
    // tg: code/modules/paperwork/paper.dm:490
    public virtual async DmTask<object> show_through_camera(mob_living user = null)
    {
        if (!Dm.truthy(can_show_to_mob_through_camera(user)))
            return null;

        return await ui_interact(user);
    }

    // tg: code/modules/paperwork/paper.dm:496
    public virtual object can_show_to_mob_through_camera(mob_living user = null)
    {
        obj_machinery_camera held_to_camera = camera_holder.resolve() as obj_machinery_camera;

        if (held_to_camera == null)
            return FALSE;

        if (isAI(user))
        {
            mob_living_silicon_ai ai_user = (mob_living_silicon_ai)user;
            if (Dm.truthy(ai_user.control_disabled) || ai_user.stat == DEAD)
                return FALSE;

            return TRUE;
        }

        if (user.client?.eye != held_to_camera)
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/paperwork/paper.dm:516
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet/simple/stamps), get_asset_datum(/datum/asset/simple/logos)): the asset transport is
        // the client's (DV44); the client draws the stamps from its own copy of tg's icons (the PaperSheet window).
        return Dm.list();
    }

    // tg: code/modules/paperwork/paper.dm:522
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (user.client == null) //bro stop trying to open UI on AI man ur gonna drive me nuts man comeon man
            return null;
        if (Dm.truthy(Dm.bitand(resistance_flags, ON_FIRE)))
            return null;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            /**
             * these signals are for checking whether the ui viewer is holding a writing tool.
             * (whether they are holding a writing tool matters for the state of the ui)
             *
             * we have to do this rigamarole, rather than just checking on ui_data calls,
             * because if we set this ui to autoupdate, it causes weird rendering issues.
             * rather than figure out why those are happening, it was easier to just turn off autoupdate.
             */
            RegisterSignals(user, Dm.list(
                COMSIG_MOB_UNEQUIPPED_ITEM,
                COMSIG_MOB_EQUIPPED_ITEM,
                COMSIG_MOB_SWAP_HANDS,
                COMSIG_MOB_TRANSFORMING_ITEM), // specifically for pens
                nameof(viewer_writing_state_change));
            DmList writing_info = await get_viewer_writing_implement_details(user as mob_living) as DmList;
            if (writing_info != null)
                add_writer(user as mob_living, writing_info, update: FALSE);

            ui = Dm.@new<datum_tgui>(user, this, "PaperSheet", name);
            await ui.open();
            // please see the above comment if you want to re-enable autoupdate
            ui.set_autoupdate(FALSE);
        }
        return null;
    }

    // tg: code/modules/paperwork/paper.dm:549
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        if (Dm.truthy(LAZYACCESS(writers, REF(user))))
            remove_writer(user as mob_living, update: FALSE);
        UnregisterSignal(user, Dm.list(
            COMSIG_MOB_UNEQUIPPED_ITEM,
            COMSIG_MOB_EQUIPPED_ITEM,
            COMSIG_MOB_SWAP_HANDS,
            COMSIG_MOB_TRANSFORMING_ITEM));
    }

    /// Generically check if we are holding a writing tool to update our writer status
    // tg: code/modules/paperwork/paper.dm:561
    public virtual void viewer_writing_state_change(mob_living source = null)
    {
        DmList writing_info = Waiting.NoSleep(get_viewer_writing_implement_details(source)) as DmList;
        if (writing_info != null)
        {
            if (!Dm.truthy(LAZYACCESS(writers, REF(source))))
                add_writer(source, writing_info);
        }
        else
        {
            if (Dm.truthy(LAZYACCESS(writers, REF(source))))
                remove_writer(source);
        }
    }

    /// Add passed mob with passed writing info to the list of writers, then updates their ui
    // tg: code/modules/paperwork/paper.dm:575
    public virtual void add_writer(mob_living user = null, DmList writing_info = null, float? update = null)
    {
        Waiting.WaitforFalse(add_writer__body(user, writing_info, update));
    }

    // The body of the set waitfor = FALSE wrapper above (waiting-model.md 5.4).
    private async DmTask add_writer__body(mob_living user, DmList writing_info, float? update)
    {
        update ??= TRUE;
        LAZYSET(ref writers, REF(user), writing_info);
        if (Dm.truthy(update))
            await ui_interact(user);
    }

    /// Remove passed mob from the list of writers, then updates their ui
    // tg: code/modules/paperwork/paper.dm:585
    public virtual void remove_writer(mob_living user = null, float? update = null)
    {
        Waiting.WaitforFalse(remove_writer__body(user, update));
    }

    // The body of the set waitfor = FALSE wrapper above (waiting-model.md 5.4).
    private async DmTask remove_writer__body(mob_living user, float? update)
    {
        update ??= TRUE;
        LAZYREMOVE(ref writers, REF(user));
        if (Dm.truthy(update))
            await ui_interact(user);
    }

    // tg: code/modules/paperwork/paper.dm:593
    public virtual async DmTask<object> get_viewer_writing_implement_details(mob_living user = null)
    {
        object dot = null;
        if (istype(loc, typeof(obj_structure_noticeboard)))
        {
            obj_structure_noticeboard noticeboard = (obj_structure_noticeboard)loc;
            if (!Dm.truthy(await noticeboard.allowed__proc(user)))
                return null;
        }

        obj_item holding = user.get_active_held_item() as obj_item;
        dot = holding == null ? null : await holding.get_writing_implement_details();

        // Use a clipboard's pen, if applicable
        if (istype(loc, typeof(obj_item_clipboard)))
        {
            obj_item_clipboard clipboard = (obj_item_clipboard)loc;
            if (!Dm.truthy(dot))
                dot = clipboard.pen == null ? null : await clipboard.pen.get_writing_implement_details();
        }

        return dot;
    }

    // tg: code/modules/paperwork/paper.dm:609
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["held_item_details"] = LAZYACCESS(writers, REF(user));

        return data;
    }

    // tg: code/modules/paperwork/paper.dm:616
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList static_data = Dm.list();

        static_data["user_name"] = user.real_name;

        Dm.append(static_data, convert_to_data());

        static_data["max_length"] = MAX_PAPER_LENGTH;
        static_data["max_input_field_length"] = MAX_PAPER_INPUT_FIELD_LENGTH;

        static_data["default_pen_font"] = PEN_FONT;
        static_data["default_pen_color"] = COLOR_BLACK;
        static_data["signature_font"] = FOUNTAIN_PEN_FONT;

        return static_data;
    }

    // tg: code/modules/paperwork/paper.dm:632
    public virtual object convert_to_data()
    {
        DmList data = Dm.list();

        data[LIST_PAPER_RAW_TEXT_INPUT] = Dm.list();
        foreach (datum_paper_input text_input in Dm.each<datum_paper_input>(raw_text_inputs))
            Dm.append((DmList)data[LIST_PAPER_RAW_TEXT_INPUT], Dm.list(text_input.to_list()));

        data[LIST_PAPER_RAW_FIELD_INPUT] = Dm.list();
        foreach (datum_paper_field field_input in Dm.each<datum_paper_field>(raw_field_input_data))
            Dm.append((DmList)data[LIST_PAPER_RAW_FIELD_INPUT], Dm.list(field_input.to_list()));

        data[LIST_PAPER_RAW_STAMP_INPUT] = Dm.list();
        foreach (datum_paper_stamp stamp_input in Dm.each<datum_paper_stamp>(raw_stamp_data))
            Dm.append((DmList)data[LIST_PAPER_RAW_STAMP_INPUT], Dm.list(stamp_input.to_list()));

        data[LIST_PAPER_COLOR] = Dm.truthy(color) ? color : COLOR_WHITE;
        data[LIST_PAPER_NAME] = name;

        return data;
    }

    // tg: code/modules/paperwork/paper.dm:651
    public virtual async DmTask write_from_data(DmList data = null)
    {
        foreach (DmList input in Dm.each<DmList>(data[LIST_PAPER_RAW_TEXT_INPUT]))
            add_raw_text(input[LIST_PAPER_RAW_TEXT], input[LIST_PAPER_FONT], input[LIST_PAPER_FIELD_COLOR], input[LIST_PAPER_BOLD], input[LIST_PAPER_ADVANCED_HTML]);

        foreach (DmList field in Dm.each<DmList>(data[LIST_PAPER_RAW_FIELD_INPUT]))
        {
            DmList input = field[LIST_PAPER_FIELD_DATA] as DmList;
            add_field_input(field[LIST_PAPER_FIELD_INDEX], input[LIST_PAPER_RAW_TEXT], input[LIST_PAPER_FONT], input[LIST_PAPER_FIELD_COLOR], input[LIST_PAPER_BOLD], field[LIST_PAPER_IS_SIGNATURE]);
        }

        foreach (DmList stamp in Dm.each<DmList>(data[LIST_PAPER_RAW_STAMP_INPUT]))
            add_stamp(stamp[LIST_PAPER_CLASS], stamp[LIST_PAPER_STAMP_X], stamp[LIST_PAPER_STAMP_Y], stamp[LIST_PAPER_ROTATION]);

        object new_color = data[LIST_PAPER_COLOR];
        if (!Dm.eq(new_color, COLOR_WHITE))
            await add_atom_colour(new_color, FIXED_COLOUR_PRIORITY);

        name = data[LIST_PAPER_NAME] as string;
    }

    // tg: code/modules/paperwork/paper.dm:668
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user = ui.user;

        switch (action)
        {
            case "add_stamp":
            {
                obj_item holding = user.get_active_held_item() as obj_item;
                DmList stamp_info = holding == null ? null : await holding.get_writing_implement_details() as DmList;
                if (stamp_info == null || !Dm.eq(stamp_info["interaction_mode"], MODE_STAMPING))
                {
                    to_chat(this, span_warning(I18n.T("You can't stamp with the []!", holding))); // tg's own: told to the paper
                    return TRUE;
                }

                object stamp_class = stamp_info["stamp_class"];

                // If the paper is on an unwritable noticeboard, this usually shouldn't be possible.
                if (istype(loc, typeof(obj_structure_noticeboard)))
                {
                    obj_structure_noticeboard noticeboard = (obj_structure_noticeboard)loc;
                    if (!Dm.truthy(await noticeboard.allowed__proc(user)))
                    {
                        log_paper(Dm.text("[] tried to add stamp to [] when it was on an unwritable noticeboard: \"[]\"", key_name(user), name, stamp_class));
                        return TRUE;
                    }
                }

                float? stamp_x = text2num(@params["x"]);
                float? stamp_y = text2num(@params["y"]);
                float? stamp_rotation = text2num(@params["rotation"]);
                object stamp_icon_state = stamp_info["stamp_icon_state"];
                object stamp_icon = stamp_info["stamp_icon"];

                if (LAZYLEN(raw_stamp_data) >= MAX_PAPER_STAMPS)
                {
                    to_chat(usr, pick(I18n.T("You try to stamp but you miss!"), I18n.T("There is nowhere else you can stamp!")));
                    return TRUE;
                }

                add_stamp(stamp_class, stamp_x, stamp_y, stamp_rotation, stamp_icon_state, stamp_icon);
                user.visible_message(span_notice(I18n.T("[] stamps [] with \\the []!", user, this, holding.name)), span_notice(I18n.T("You stamp [] with \\the []!", this, holding.name)));
                playsound(this, Dm.resource("sound/items/handling/standard_stamp.ogg"), 50, vary: TRUE);

                update_appearance();
                ui.close();
                await update_static_data_for_all_viewers();
                return TRUE;
            }
            case "add_text":
            {
                object paper_input = @params["text"];
                float this_input_length = length_char(paper_input);

                if (this_input_length == 0)
                {
                    to_chat(user, pick(I18n.T("Writing block strikes again!"), I18n.T("You forgot to write anything!")));
                    return TRUE;
                }

                // If the paper is on an unwritable noticeboard, this usually shouldn't be possible.
                if (istype(loc, typeof(obj_structure_noticeboard)))
                {
                    obj_structure_noticeboard noticeboard = (obj_structure_noticeboard)loc;
                    if (!Dm.truthy(await noticeboard.allowed__proc(user)))
                    {
                        log_paper(Dm.text("[] tried to write to [] when it was on an unwritable noticeboard: \"[]\"", key_name(user), name, paper_input));
                        return TRUE;
                    }
                }

                obj_item holding = user.get_active_held_item() as obj_item;
                // Use a clipboard's pen, if applicable
                if (istype(loc, typeof(obj_item_clipboard)))
                {
                    obj_item_clipboard clipboard = (obj_item_clipboard)loc;
                    // This is just so you can still use a stamp if you're holding one. Otherwise, it'll
                    // use the clipboard's pen, if applicable.
                    if (!istype(holding, typeof(obj_item_stamp)) && clipboard.pen != null)
                        holding = clipboard.pen;
                }

                // As of the time of writing, can_write outputs a message to the user so we don't have to.
                if (!Dm.truthy(await user.can_write(holding)))
                    return TRUE;

                float current_length = Dm.num(get_total_length());
                float new_length = current_length + this_input_length;

                // tgui should prevent this outcome.
                if (new_length > MAX_PAPER_LENGTH)
                {
                    log_paper(Dm.text("[] tried to write to [] when it would exceed the length limit by [] characters: \"[]\"", key_name(user), name, new_length - MAX_PAPER_LENGTH, paper_input));
                    return TRUE;
                }

                // Safe to assume there are writing implement details as user.can_write(...) fails with an invalid writing implement.
                DmList writing_implement_data = await holding.get_writing_implement_details() as DmList;

                playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);

                add_raw_text(paper_input, writing_implement_data["font"], writing_implement_data["color"], writing_implement_data["use_bold"], check_rights_for(user?.client, R_FUN));

                log_paper(Dm.text("[] wrote to []: \"[]\"", key_name(user), name, paper_input));
                to_chat(user, I18n.T("You have added to your paper masterpiece!"));

                await update_static_data_for_all_viewers();
                update_appearance();
                return TRUE;
            }
            case "fill_input_field":
            {
                // If the paper is on an unwritable noticeboard, this usually shouldn't be possible.
                if (istype(loc, typeof(obj_structure_noticeboard)))
                {
                    obj_structure_noticeboard noticeboard = (obj_structure_noticeboard)loc;
                    if (!Dm.truthy(await noticeboard.allowed__proc(user)))
                    {
                        log_paper(Dm.text("[] tried to write to the input fields of [] when it was on an unwritable noticeboard!", key_name(user), name));
                        return TRUE;
                    }
                }

                obj_item holding = user.get_active_held_item() as obj_item;
                // Use a clipboard's pen, if applicable
                if (istype(loc, typeof(obj_item_clipboard)))
                {
                    obj_item_clipboard clipboard = (obj_item_clipboard)loc;
                    // This is just so you can still use a stamp if you're holding one. Otherwise, it'll
                    // use the clipboard's pen, if applicable.
                    if (!istype(holding, typeof(obj_item_stamp)) && clipboard.pen != null)
                        holding = clipboard.pen;
                }

                // As of the time of writing, can_write outputs a message to the user so we don't have to.
                if (!Dm.truthy(await user.can_write(holding)))
                    return TRUE;

                // Safe to assume there are writing implement details as user.can_write(...) fails with an invalid writing implement.
                DmList writing_implement_data = await holding.get_writing_implement_details() as DmList;
                DmList field_data = @params["field_data"] as DmList;

                foreach (object field_key in Dm.each(field_data))
                {
                    object field_text = field_data[field_key];
                    float text_length = length_char(field_text);
                    if (text_length > MAX_PAPER_INPUT_FIELD_LENGTH)
                    {
                        log_paper(Dm.text("[] tried to write to field [] with text over the max limit ([] out of []) with the following text: []", key_name(user), field_key, text_length, MAX_PAPER_INPUT_FIELD_LENGTH, field_text));
                        return TRUE;
                    }
                    if (Dm.num(text2num(field_key)) >= input_field_count)
                    {
                        log_paper(Dm.text("[] tried to write to invalid field [] (when the paper only has [] fields) with the following text: []", key_name(user), field_key, input_field_count, field_text));
                        return TRUE;
                    }

                    if (!Dm.truthy(add_field_input(field_key, field_text, writing_implement_data["font"], writing_implement_data["color"], writing_implement_data["use_bold"], user.real_name)))
                        log_paper(Dm.text("[] tried to write to field [] when it already has data, with the following text: []", key_name(user), field_key, field_text));
                }

                await update_static_data_for_all_viewers();
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/modules/paperwork/paper.dm:795  var/static/regex/field_regex = new(@"\[_+\]","g") (rules 10.6, made on the first call)
    private static ref regex get_input_field_count__field_regex => ref TgState.Current.obj_item_paper__get_input_field_count__field_regex;

    // tg: code/modules/paperwork/paper.dm:794
    public virtual object get_input_field_count(object raw_text = null)
    {
        regex field_regex = get_input_field_count__field_regex ??= Dm.@new<regex>(@"\[_+\]", "g");

        float counter = 0;
        while (Dm.truthy(field_regex.Find(raw_text)))
            counter++;

        return counter;
    }

    // tg: code/modules/paperwork/paper.dm:803
    public override object ui_host(mob user = null)
    {
        if (istype(loc, typeof(obj_structure_noticeboard)))
            return loc;
        return base.ui_host(user);
    }

    // tg: code/modules/paperwork/paper.dm:808
    public virtual object get_total_length()
    {
        float total_length = 0;
        foreach (datum_paper_input entry in Dm.each<datum_paper_input>(raw_text_inputs))
            total_length += length_char(entry.raw_text);

        return total_length;
    }

    /// Get a single string representing the text on a page
    // tg: code/modules/paperwork/paper.dm:815
    public virtual object get_raw_text()
    {
        string paper_contents = "";
        foreach (datum_paper_input line in Dm.each<datum_paper_input>(raw_text_inputs))
            paper_contents += line.raw_text + "/";
        return paper_contents;
    }
}

/// A single instance of a saved raw input onto paper.
// tg: code/modules/paperwork/paper.dm:820
public partial class datum_paper_input : datum
{
    // tg: code/modules/paperwork/paper.dm:832
    [DmParams("_raw_text", "_font", "_colour", "_bold", "_advanced_html")]
    public override async DmTask New(params object[] __extra)
    {
        raw_text = AsText(Dm.arg(__extra, 0));
        font = AsText(Dm.arg(__extra, 1));
        colour = AsText(Dm.arg(__extra, 2));
        bold = Dm.num(Dm.arg(__extra, 3));
        advanced_html = Dm.num(Dm.arg(__extra, 4));
    }

    // A DM var holds any value; the generated field is text (the inputs are text or null).
    internal static string AsText(object value) => value == null ? null : value as string ?? Dm.str(value);

    // tg: code/modules/paperwork/paper.dm:839
    public virtual object make_copy()
    {
        return Dm.@new<datum_paper_input>(raw_text, font, colour, bold, advanced_html);
    }

    // tg: code/modules/paperwork/paper.dm:842
    public virtual object to_list()
    {
        return Dm.list(
            Dm.kv(LIST_PAPER_RAW_TEXT, raw_text),
            Dm.kv(LIST_PAPER_FONT, font),
            Dm.kv(LIST_PAPER_FIELD_COLOR, colour),
            Dm.kv(LIST_PAPER_BOLD, bold),
            Dm.kv(LIST_PAPER_ADVANCED_HTML, advanced_html));
    }

    /// Returns the raw contents of the input as html, with **ZERO SANITIZATION**
    // tg: code/modules/paperwork/paper.dm:852
    public virtual object to_raw_html()
    {
        string final = raw_text;
        if (Dm.truthy(font) || Dm.truthy(colour))
            final = Dm.text("<font[][]>[]</font>", Dm.text(" color='[]'", colour), Dm.text(" face='[]'", font), final);
        if (Dm.truthy(bold))
            final = Dm.text("<b>[]</b>", final);
        return final;
    }
}

/// A single instance of a saved stamp on paper.
// tg: code/modules/paperwork/paper.dm:861
public partial class datum_paper_stamp : datum
{
    // tg: code/modules/paperwork/paper.dm:871
    [DmParams("_class", "_stamp_x", "_stamp_y", "_rotation")]
    public override async DmTask New(params object[] __extra)
    {
        @class = datum_paper_input.AsText(Dm.arg(__extra, 0));
        stamp_x = Dm.num(Dm.arg(__extra, 1));
        stamp_y = Dm.num(Dm.arg(__extra, 2));
        rotation = Dm.num(Dm.arg(__extra, 3));
    }

    // tg: code/modules/paperwork/paper.dm:877
    public virtual object make_copy()
    {
        return Dm.@new<datum_paper_stamp>(@class, stamp_x, stamp_y, rotation);
    }

    // tg: code/modules/paperwork/paper.dm:880
    public virtual object to_list()
    {
        return Dm.list(
            Dm.kv(LIST_PAPER_CLASS, @class),
            Dm.kv(LIST_PAPER_STAMP_X, stamp_x),
            Dm.kv(LIST_PAPER_STAMP_Y, stamp_y),
            Dm.kv(LIST_PAPER_ROTATION, rotation));
    }
}

/// A reference to some data that replaces a modifiable input field at some given index in paper raw input parsing.
// tg: code/modules/paperwork/paper.dm:889
public partial class datum_paper_field : datum
{
    /// When tgui parses the raw input, if it encounters a field_index matching the nth user input field, it will disable it and replace it with the field_data.
    // tg: code/modules/paperwork/paper.dm:891  (tg stores the window's text key, "0", and tgui compares it as text: the var holds any value)
    public object field_index = -1;

    // tg: code/modules/paperwork/paper.dm:897
    [DmParams("_field_index", "raw_text", "font", "colour", "bold", "_is_signature")]
    public override async DmTask New(params object[] __extra)
    {
        field_index = Dm.arg(__extra, 0);
        field_data = Dm.@new<datum_paper_input>(Dm.arg(__extra, 1), Dm.arg(__extra, 2), Dm.arg(__extra, 3), Dm.arg(__extra, 4));
        is_signature = Dm.num(Dm.arg(__extra, 5));
    }

    // tg: code/modules/paperwork/paper.dm:902
    public virtual object make_copy()
    {
        return Dm.@new<datum_paper_field>(field_index, field_data.raw_text, field_data.font, field_data.colour, field_data.bold, is_signature);
    }

    // tg: code/modules/paperwork/paper.dm:905
    public virtual object to_list()
    {
        return Dm.list(
            Dm.kv(LIST_PAPER_FIELD_INDEX, field_index),
            Dm.kv(LIST_PAPER_FIELD_DATA, field_data.to_list()),
            Dm.kv(LIST_PAPER_IS_SIGNATURE, is_signature));
    }
}

// tg: code/modules/paperwork/paper.dm:912
public partial class obj_item_paper_construction : obj_item_paper
{
    // tg: code/modules/paperwork/paper.dm:916
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon = Dm.resource("icons/obj/service/bureaucracy.dmi");
        color = pick(COLOR_RED, COLOR_LIME, COLOR_LIGHT_ORANGE, COLOR_DARK_PURPLE, COLOR_FADED_PINK, COLOR_BLUE_LIGHT) as string;
        update_appearance();
        return dot;
    }
}
