// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/bookcase.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the bookcase: a mapped one takes the books on its tile and loads its shelf when SSlibrary
// initializes, books put in and taken out by hand with tg's list, its frame wrenched down, its wooden shelf added and pried out, pried
// apart, its icon by the number of books; the engineering and R&D manual bookcases). The var blocks are generated (D111).

// tg: code/modules/library/bookcase.dm:5
public partial class obj_structure_bookcase : obj_structure
{
    // tg: code/modules/library/bookcase.dm:1-3 (the file's #defines, #undef'd at its end)
    internal const float BOOKCASE_UNANCHORED = 0;
    internal const float BOOKCASE_ANCHORED = 1;
    internal const float BOOKCASE_FINISHED = 2;

    // tg: code/modules/library/bookcase.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_flags = Dm.bitor(obj_flags, Dm.bitor(UNIQUE_RENAME, RENAME_NO_DESC));
        if (!Dm.truthy(mapload) || QDELETED(this))
            return dot;
        // Only mapload from here on
        Waiting.NoSleep(set_anchored(TRUE));
        state = BOOKCASE_FINISHED;
        foreach (obj_item I in Dm.each<obj_item>(loc))
        {
            if (!isbook(I))
                continue;
            Waiting.NoSleep(I.forceMove(this));
        }
        update_appearance();

        if (Dm.truthy(SSlibrary.initialized))
            Waiting.Detach(load_shelf()); // INVOKE_ASYNC(src, PROC_REF(load_shelf))
        else
            Dm.append(ref SSlibrary.shelves_to_load, this);
        return dot;
    }

    ///proc for doing things after a bookcase is randomly populated
    // tg: code/modules/library/bookcase.dm:50
    public virtual void after_random_load()
    {
        return;
    }

    ///Loads the shelf, both by allowing it to generate random items, and by adding its contents to a list used by library machines
    // tg: code/modules/library/bookcase.dm:54
    public virtual async DmTask load_shelf()
    {
        //Loads a random selection of books in from the db, adds a copy of their info to a global list
        //To send to library consoles as a starting inventory
        if (Dm.truthy(load_random_books))
        {
            bool randomizing_categories = prob(category_prob) || random_category == BOOK_CATEGORY_RANDOM;
            // We only need to run this special logic if we're randomizing a non-adult bookshelf
            if (randomizing_categories && random_category != BOOK_CATEGORY_ADULT)
            {
                // Category is manually randomized rather than using BOOK_CATEGORY_RANDOM
                // So we can exclude adult books in non-adult bookshelves
                // And also weight the prime category more heavily
                DmList category_pool = Dm.list(
                    BOOK_CATEGORY_FICTION,
                    BOOK_CATEGORY_NONFICTION,
                    BOOK_CATEGORY_REFERENCE,
                    BOOK_CATEGORY_RELIGION);
                if (random_category != BOOK_CATEGORY_RANDOM)
                    Dm.append(ref category_pool, random_category);
                float sub_books_to_load = books_to_load;
                while (sub_books_to_load > 0 && length(category_pool) > 0)
                {
                    float cat_amount = Dm.num(min(rand(1, 2), sub_books_to_load));
                    sub_books_to_load -= cat_amount;
                    await create_random_books(amount: cat_amount, location: this, category: (string)pick_n_take(category_pool));
                }
            }
            // Otherwise we can just let the proc handle everything, it will even do randomization for us
            else
                await create_random_books(amount: books_to_load, location: this, category: randomizing_categories ? BOOK_CATEGORY_RANDOM : random_category);

            after_random_load();
            update_appearance(); //Make sure you look proper
        }

        area our_area = get_area(this) as area;
        Type area_type = our_area.type; //Save me from the dark

        if (!Dm.truthy(SSlibrary.books_by_area[area_type]))
            SSlibrary.books_by_area[area_type] = Dm.list();

        //Time to populate that list
        DmList books_in_area = SSlibrary.books_by_area[area_type] as DmList;
        foreach (obj_item_book book in Dm.each<obj_item_book>(contents))
        {
            datum_book_info info = book.book_data;
            // DEVIATION(DV27): an inert book (its own tg code not translated yet: the space law manual, the granters) skipped its
            // Initialize and has no book data; it is left out of the area's books until its type is live
            if (info == null)
                continue;
            Dm.append(ref books_in_area, info.return_copy());
        }
    }

    // tg: code/modules/library/bookcase.dm:96
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("The <i>bolts</i> on the bottom are unsecured.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("It's secured in place with <b>bolts</b>.")));
        if (state == BOOKCASE_UNANCHORED)
            Dm.append(ref dot, span_notice(I18n.T("There's a <b>small crack</b> visible on the back panel.")));
        else if (state == BOOKCASE_ANCHORED)
            Dm.append(ref dot, span_notice(I18n.T("There's space inside for a <i>wooden</i> shelf.")));
        else if (state == BOOKCASE_FINISHED)
            Dm.append(ref dot, span_notice(I18n.T("There's a <b>small crack</b> visible on the shelf.")));
        return dot;
    }

    // tg: code/modules/library/bookcase.dm:110
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        if (dot == null)
            return dot;
        state = Dm.num_or_null(anchorvalue);
        if (!Dm.truthy(anchorvalue)) //in case we were vareditted or uprooted by a hostile mob, ensure we drop all our books instead of having them disappear till we're rebuild.
        {
            atom Tsec = drop_location() as atom;
            foreach (obj I in Dm.each<obj>(contents))
            {
                if (!isbook(I))
                    continue;
                await I.forceMove(Tsec);
            }
        }
        update_appearance();
        return dot;
    }

    // tg: code/modules/library/bookcase.dm:123
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (state == BOOKCASE_ANCHORED)
        {
            if (!istype(tool, typeof(obj_item_stack_sheet_mineral_wood)))
                return NONE;
            obj_item_stack_sheet_mineral_wood planks = (obj_item_stack_sheet_mineral_wood)tool;
            if (Dm.lt(planks.get_amount(), 2))
            {
                balloon_alert(user, I18n.T("not enough wood"));
                return ITEM_INTERACT_BLOCKING;
            }

            await planks.use(2);
            balloon_alert(user, I18n.T("shelf added"));
            state = BOOKCASE_FINISHED;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (state != BOOKCASE_FINISHED)
            return NONE;

        if (isbook(tool))
        {
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return NONE;

            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        // tg tests the bookcase's own storage here (src's atom_storage, which a bookcase has not), so a bag is never emptied into
        // it: kept as tg's
        if (atom_storage != null)
        {
            bool found_anything = false;
            foreach (obj_item T in Dm.each<obj_item>(tool.contents))
            {
                if (istype(T, typeof(obj_item_book)) || istype(T, typeof(obj_item_spellbook)))
                {
                    atom_storage.attempt_remove(T, this);
                    found_anything = true;
                }
            }

            if (!found_anything)
                return ITEM_INTERACT_BLOCKING;

            balloon_alert(user, I18n.T("emptied into []", this));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/library/bookcase.dm:164
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (state == BOOKCASE_UNANCHORED)
        {
            if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
                return ITEM_INTERACT_BLOCKING;

            user.balloon_alert(user, I18n.T("pried apart"));
            await deconstruct(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }
        if (state == BOOKCASE_FINISHED)
        {
            if (Dm.truthy(length(contents)))
            {
                balloon_alert(user, I18n.T("remove the books first"));
                return ITEM_INTERACT_BLOCKING;
            }

            tool.play_tool_sound(this, 100);
            balloon_alert(user, I18n.T("pried the shelf out"));
            Dm.@new<obj_item_stack_sheet_mineral_wood>(drop_location(), 2);
            state = BOOKCASE_ANCHORED;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/library/bookcase.dm:188
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (state == BOOKCASE_UNANCHORED)
        {
            if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
                return ITEM_INTERACT_BLOCKING;

            balloon_alert(user, I18n.T("wrenched in place"));
            await set_anchored(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }
        if (state == BOOKCASE_ANCHORED)
        {
            tool.play_tool_sound(this, 100);
            balloon_alert(user, I18n.T("unwrenched the frame"));
            await set_anchored(FALSE);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/library/bookcase.dm:206
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (user is not mob_living living_user)
            return dot;
        if (!Dm.truthy(length(contents)))
            return dot;
        obj_item choice = await tgui_input_list(user, I18n.T("Book to remove from the shelf"), I18n.T("Remove Book"), sort_names(contents.Copy()) as DmList) as obj_item;
        if (choice == null)
            return dot;
        if (!Dm.truthy(Dm.bitand(living_user.mobility_flags, MOBILITY_USE)) || IS_UNCONSCIOUS_OR_CRIT(living_user) || HAS_TRAIT(living_user, TRAIT_HANDS_BLOCKED) || !in_range(loc, living_user))
            return dot;
        if (ishuman(living_user))
        {
            if (living_user.get_active_held_item() == null)
                await living_user.put_in_hands(choice);
        }
        else
            await choice.forceMove(drop_location() as atom);
        update_appearance();
        return dot;
    }

    // tg: code/modules/library/bookcase.dm:226
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        atom Tsec = drop_location() as atom;
        Dm.@new<obj_item_stack_sheet_mineral_wood>(Tsec, 4);
        foreach (obj_item I in Dm.each<obj_item>(contents))
        {
            if (!isbook(I)) //Wake me up inside
                continue;
            await I.forceMove(Tsec);
        }
    }

    // tg: code/modules/library/bookcase.dm:234
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (state == BOOKCASE_UNANCHORED || state == BOOKCASE_ANCHORED)
        {
            icon_state = "bookempty";
            return await base.update_icon_state(__extra);
        }
        float amount = length(contents);
        icon_state = Dm.text("book-[]", clamp(amount, 0, 5));
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/library/bookcase.dm:242
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        await DmTask.CompletedTask;
        return Dm.text("bookcase[]", Dm.truthy(input) ? Dm.text(" ([])", input) : null);
    }
}

// tg: code/modules/library/bookcase.dm:245
public partial class obj_structure_bookcase_manuals_engineering : obj_structure_bookcase_manuals
{
    // tg: code/modules/library/bookcase.dm:248
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_item_book_manual_wiki_engineering_construction>(this);
        Dm.@new<obj_item_book_manual_wiki_engineering_hacking>(this);
        Dm.@new<obj_item_book_manual_wiki_engineering_guide>(this);
        Dm.@new<obj_item_book_manual_wiki_robotics_cyborgs>(this);
        update_appearance();
        return dot;
    }
}

// tg: code/modules/library/bookcase.dm:256
public partial class obj_structure_bookcase_manuals_research_and_development : obj_structure_bookcase_manuals
{
    // tg: code/modules/library/bookcase.dm:259
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_item_book_manual_wiki_research_and_development>(this);
        update_appearance();
        return dot;
    }
}
