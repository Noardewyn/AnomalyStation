// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/book.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the book, for the bible and the granters: reading (the MarkdownViewer window), vandalizing and carving. The context tips are rows.
// Gaps P3b (D327): the tgui book (book.dm:264-296: its window and page sound) and the manual's book state, for the DNA infusion book; the
// DSM and IDC manuals (:299-end) wait for the library row.

// tg: code/modules/library/book.dm:1
public partial class obj_item_book : obj_item
{
    // tg: code/modules/library/book.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        book_data = Dm.@new<datum_book_info>(starting_title, starting_author, starting_content);

        Waiting.NoSleep(AddElement(typeof(datum_element_falling_hazard), Dm.named("damage", 5), Dm.named("wound_bonus", 0), Dm.named("hardhat_safety", TRUE), Dm.named("crushes", FALSE), Dm.named("impact_sound", drop_sound)));
        Waiting.NoSleep(AddElement(typeof(datum_element_burn_on_item_ignition)));
        register_context();
        return dot;
    }

    // tg: code/modules/library/book.dm:43
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(carved))
            Dm.append(ref dot, span_notice(I18n.T("[] has been hollowed out.", this)));
        return dot;
    }

    /// Gets the context to add for clicking the book inhand. Returns null if none.
    // tg: code/modules/library/book.dm:71
    public virtual object get_attack_self_context(mob_living user = null)
    {
        return "Read";
    }

    // tg: code/modules/library/book.dm:74
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["author"] = book_data.get_author();
        data["title"] = book_data.get_title();
        data["content"] = book_data.get_content();
        return data;
    }

    // tg: code/modules/library/book.dm:81
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "MarkdownViewer", name);
            await ui.open();
        }
        return null;
    }

    /// Proc that handles sending the book information to the user, as well as some housekeeping stuff.
    // tg: code/modules/library/book.dm:88
    public virtual async DmTask display_content(mob_living user = null)
    {
        await ui_interact(user);
    }

    /// Proc that checks if the user is capable of reading the book, for UI interactions and otherwise. Returns TRUE if they can, FALSE if they can't.
    // tg: code/modules/library/book.dm:92
    public virtual object can_read_book(mob_living user = null)
    {
        if (Dm.truthy(user.is_blind()))
        {
            to_chat(user, span_warning(I18n.T("You are blind and can't read anything!")));
            return FALSE;
        }

        if (!Dm.truthy(user.can_read(this)))
            return FALSE;

        if (Dm.truthy(carved))
        {
            balloon_alert(user, I18n.T("book is carved out!"));
            return FALSE;
        }

        if (!Dm.truthy(length(book_data.get_content())))
        {
            balloon_alert(user, I18n.T("book is blank!"));
            return FALSE;
        }

        return TRUE;
    }

    /// Proc that adds the book to a list on the user's mind so we know what works of art they've been catching up on.
    // tg: code/modules/library/book.dm:111
    public virtual async DmTask credit_book_to_reader(mob_living user = null)
    {
        if (user == null || user.mind == null)
            return;

        user.mind.book_titles_read ??= Dm.list();
        if (Dm.@in(starting_title, user.mind.book_titles_read))
            return;

        await user.add_mood_event("book_nerd", typeof(datum_mood_event_book_nerd));
        user.mind.book_titles_read[starting_title] = TRUE;
    }

    // tg: code/modules/library/book.dm:122
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(can_read_book(user as mob_living)))
            return null;

        playsound(user, Dm.resource("sound/items/handling/paper_pickup.ogg"), 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        user.visible_message(span_notice(I18n.T("[] opens a book titled \"[]\" and begins reading intently.", user, book_data.title)));
        await credit_book_to_reader(user as mob_living);
        await display_content(user as mob_living);
        return null;
    }

    // tg: code/modules/library/book.dm:131
    public virtual object is_carving_tool(obj_item tool = null)
    {
        if (Dm.truthy(Dm.bitand(tool.get_sharpness(), SHARP_EDGED)))
            return TRUE;
        if (Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER))
            return TRUE;
        return FALSE;
    }

    /// Checks for whether we can vandalize this book, to ensure we still can after each input.
    /// Uses to_chat over balloon alerts to give more detailed information as to why.
    // tg: code/modules/library/book.dm:141
    public virtual async DmTask<object> can_vandalize(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(await user.can_perform_action(this)) || !Dm.truthy(await user.can_write(tool, TRUE)))
            return FALSE;
        if (Dm.truthy(user.is_blind()))
        {
            to_chat(user, span_warning(I18n.T("As you are trying to write on the book, you suddenly feel very stupid!")));
            return FALSE;
        }
        if (Dm.truthy(unique))
        {
            to_chat(user, span_warning(I18n.T("These pages don't seem to take the ink well! Looks like you can't modify it.")));
            return FALSE;
        }
        if (Dm.truthy(carved))
        {
            to_chat(user, span_warning(I18n.T("The book has been carved out! There is nothing to be vandalized.")));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/library/book.dm:155
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // Items can both be carving tools and writing utensils.
        // Because of this, we flip interaction priority on secondary.
        // This means pure writing utensils have writing as their primary action,
        // pure carving tools have carving as their primary action,
        // but items with both have primary write secondary carve.
        if (Dm.eq(tool == null ? null : (await tool.get_writing_implement_details() as DmList)?["interaction_mode"], MODE_WRITING))
            return await writing_utensil_act(user, tool);
        if (Dm.truthy(is_carving_tool(tool)))
            return await carving_act(user, tool);
        return NONE;
    }

    // tg: code/modules/library/book.dm:167
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.truthy(is_carving_tool(tool)))
            return await carving_act(user, tool);
        if (Dm.eq(tool == null ? null : (await tool.get_writing_implement_details() as DmList)?["interaction_mode"], MODE_WRITING))
            return await writing_utensil_act(user, tool);
        return NONE;
    }

    /// Called when user clicks on the book with a writing utensil. Attempts to vandalize the book.
    // tg: code/modules/library/book.dm:175
    public virtual async DmTask<object> writing_utensil_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(await can_vandalize(user, tool)))
            return ITEM_INTERACT_BLOCKING;

        object choice = await tgui_input_list(usr, I18n.T("What would you like to change?"), I18n.T("Book Alteration"), Dm.list("Title", "Contents", "Author", "Cancel"));
        if (choice == null)
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await can_vandalize(user, tool)))
            return ITEM_INTERACT_BLOCKING;

        switch (Dm.str(choice))
        {
            case "Title":
                return await vandalize_title(user, tool);
            case "Contents":
                return await vandalize_contents(user, tool);
            case "Author":
                return await vandalize_author(user, tool);
        }

        return NONE;
    }

    // tg: code/modules/library/book.dm:195
    public virtual async DmTask<object> vandalize_title(mob_living user = null, obj_item tool = null)
    {
        object newtitle = reject_bad_text(await tgui_input_text(user, I18n.T("Write a new title"), I18n.T("Book Title"), max_length: 30));
        if (!Dm.truthy(newtitle))
        {
            balloon_alert(user, I18n.T("invalid input!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.num(length_char(newtitle)) > 30)
        {
            balloon_alert(user, I18n.T("too long!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await can_vandalize(user, tool)))
            return ITEM_INTERACT_BLOCKING;

        name = Dm.str(newtitle);
        book_data.set_title(html_decode(newtitle)); //Don't want to double encode here
        playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/library/book.dm:211
    public virtual async DmTask<object> vandalize_contents(mob_living user = null, obj_item tool = null)
    {
        object content = await tgui_input_text(user, I18n.T("Write your book's contents (HTML NOT allowed)"), I18n.T("Book Contents"), max_length: MAX_PAPER_LENGTH, multiline: TRUE);
        if (!Dm.truthy(content))
        {
            balloon_alert(user, I18n.T("invalid input!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await can_vandalize(user, tool)))
            return ITEM_INTERACT_BLOCKING;

        book_data.set_content(html_decode(content));
        playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/library/book.dm:223
    public virtual async DmTask<object> vandalize_author(mob_living user = null, obj_item tool = null)
    {
        object author = await tgui_input_text(user, I18n.T("Write the author's name"), I18n.T("Author Name"), max_length: MAX_NAME_LEN);
        if (!Dm.truthy(author))
        {
            balloon_alert(user, I18n.T("invalid input!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await can_vandalize(user, tool)))
            return ITEM_INTERACT_BLOCKING;

        book_data.set_author(html_decode(author)); //Setting this encodes, don't want to double up
        playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Called when user clicks on the book with a carving utensil. Attempts to carve the book.
    // tg: code/modules/library/book.dm:236
    public virtual async DmTask<object> carving_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(carved))
        {
            balloon_alert(user, I18n.T("already carved!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("carving out..."));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("carved out"));
        playsound(this, Dm.resource("sound/effects/cloth_rip.ogg"), vol: 75, vary: TRUE);
        await carve_out();
        return ITEM_INTERACT_SUCCESS;
    }

    /// Handles setting everything a carved book needs.
    // tg: code/modules/library/book.dm:252
    public virtual async DmTask carve_out()
    {
        carved = TRUE;
        await create_storage(storage_type: carved_storage_type);
    }

    /// Generates a random icon state for the book
    // tg: code/modules/library/book.dm:257
    public virtual void gen_random_icon_state()
    {
        icon_state = Dm.text("book[]", rand(1, maximum_book_state));
    }
}

/// Base type for a book that opens a bespoke TUGI
/// Does not inherit from /obj/item/book because the only similarities between the two are the concept of "being a book"
/// When designing a UI book you can send a "play_flip_sound" act to play the page turn sound
// tg: code/modules/library/book.dm:264
public partial class obj_item_tgui_book : obj_item
{
    // tg: code/modules/library/book.dm:280
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, Dm.str(ui_name));
            await ui.open();
            playsound(this, SFX_PAGE_TURN, 30, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        }
        return null;
    }

    // tg: code/modules/library/book.dm:287
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "play_flip_sound"))
            playsound(this, SFX_PAGE_TURN, 30, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        return dot;
    }
}

// For guides
// tg: code/modules/library/book.dm:299
public partial class obj_item_tgui_book_manual : obj_item_tgui_book
{
    // tg: code/modules/library/book.dm:294
    public override object ui_state(mob user = null)
    {
        return GLOB.book_state;
    }
}
