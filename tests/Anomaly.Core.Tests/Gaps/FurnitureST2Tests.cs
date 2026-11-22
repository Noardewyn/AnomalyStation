using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.M4aAccess.P5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package ST2 (D333): the station's furniture and odd structures in a booted toy room with Bob as a player: the curtains (drawn
/// by hand, the cloth one blocking the view, wrenched and cut apart; the theatre's mechanical curtains and their button), the bookcase
/// (built, filled, emptied by its list, pried apart; the mapped manual shelves and the library subsystem), the display cases (opened
/// by access, a showpiece put in and taken out, the burglar alarm of a broken case, the glass fix), the trophy case and its window,
/// the vend-a-tray and its window (registered, stocked and bought from), the showcase's deconstruction, the mannequin's clothes, the
/// tank holder, the broken flooring, the basketball hoop and the plaques (engraved, unwrenched and fastened to a wall).
/// </summary>
public class FurnitureST2Tests
{
    private const string Drag = "left=1;button=left;drag=left";

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test");
            (get_area(locate(4, 4, 1)) as area).requires_power = FALSE; // the toy room has no APC
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, params object[] args) where T : obj_item
    {
        T item = r.In(() => (T)Dm.@new(typeof(T), new object[] { who.loc }.Concat(args).ToArray()));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static void Drop(AudioRoom r, mob_living_carbon_human who) => r.In(() =>
    {
        if (who.get_active_held_item() is obj_item held)
            qdel(held);
    });

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        Click(r, who, target, modifiers);
    }

    /// <summary>An atom as the map loader makes it: the preloader's var edits, then Initialize with mapload (as P4bButtonTests' Mapped).</summary>
    private static T Mapped<T>(AudioRoom r, int x, int y, DmList edits = null) where T : atom => r.In(() =>
    {
        float previous = SSatoms.initialized;
        SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
        if (edits != null)
            world.preloader_setup(edits, typeof(T));
        T made = Dm.@new<T>(locate(x, y, 1));
        SSatoms.initialized = previous;
        return made;
    });

    // the answer to tg's input window of that interface (TextInputModal, ListInputWindow) the player has open
    private static void Answer(AudioRoom r, mob who, string iface, object entry)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/submit", Dm.list(Dm.kv("entry", entry))));
        r.Step(4);
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static float Amount<T>(AudioRoom r, int x, int y) where T : obj_item_stack =>
        r.In(() => Dm.each<T>(locate(x, y, 1).contents).Sum(s => Dm.num(s.amount)));

    // the window of that interface the player just opened: its id and its data
    private static string Window(AudioRoom r, mob who, string iface, out JsonElement data) => Window(r, who, iface, out data, out _);

    private static string Window(AudioRoom r, mob who, string iface, out JsonElement data, out JsonElement staticData)
    {
        JsonElement payload = LastUpdate(Take(who), out string window);
        Assert.Equal(iface, InterfaceOf(payload));
        data = payload.GetProperty("data");
        staticData = payload.TryGetProperty("static_data", out JsonElement found) ? found : default;
        return window;
    }

    [Fact]
    public void TheCurtainsDrawByHandTheClothOneBlocksTheViewAndTheShowerOneIsCutApart()
    {
        var (r, feed, bob) = Room(8201);
        var curtain = Place<obj_structure_curtain>(r, 5, 4);
        r.In(() => Assert.Equal("bathroom-open", curtain.icon_state));

        // a hand draws it shut: the curtain's sound, the wall layer, still see-through
        Use(r, bob, curtain);
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("curtain") == true);
        r.In(() =>
        {
            Assert.False(Dm.truthy(curtain.open));
            Assert.Equal("bathroom-closed", curtain.icon_state);
            Assert.Equal(WALL_OBJ_LAYER, curtain.layer);
            Assert.False(Dm.truthy(curtain.opacity));
        });
        Use(r, bob, curtain);
        r.In(() => Assert.Equal("bathroom-open", curtain.icon_state));

        // the cloth curtain is opaque when shut
        var cloth = Place<obj_structure_curtain_cloth>(r, 4, 5);
        Use(r, bob, cloth);
        r.In(() => Assert.True(Dm.truthy(cloth.opacity)));
        Use(r, bob, cloth);
        r.In(() => Assert.False(Dm.truthy(cloth.opacity)));

        // the wirecutters do nothing to a fastened curtain; wrenched loose (five seconds), it is cut apart (five more)
        Hold<obj_item_wirecutters>(r, bob);
        Use(r, bob, curtain);
        r.Step(6 * Second);
        r.In(() => Assert.False(QDELETED(curtain)));
        Drop(r, bob);
        Hold<obj_item_wrench>(r, bob);
        Use(r, bob, curtain);
        r.Step(5 * Second + 5);
        r.In(() => Assert.False(Dm.truthy(curtain.anchored)));
        Drop(r, bob);
        Hold<obj_item_wirecutters>(r, bob);
        Use(r, bob, curtain);
        Assert.True(feed.Any("You start to cut apart"));
        r.Step(5 * Second + 5);
        r.In(() => Assert.True(QDELETED(curtain)));
        Assert.Equal(2f, Amount<obj_item_stack_sheet_cloth>(r, 5, 4));
        Assert.Equal(2f, Amount<obj_item_stack_sheet_plastic>(r, 5, 4));
        Assert.Equal(1f, Amount<obj_item_stack_rods>(r, 5, 4));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCurtainButtonOpensAndShutsTheTheatresMechanicalCurtains()
    {
        var (r, feed, bob) = Room(8202);
        var left = Place<obj_structure_curtain_cloth_fancy_mechanical_start_closed>(r, 6, 6);
        var right = Place<obj_structure_curtain_cloth_fancy_mechanical_start_closed>(r, 7, 6);
        var other = Place<obj_structure_curtain_cloth_fancy_mechanical_start_closed>(r, 8, 6);
        r.In(() =>
        {
            left.id = "st2_stage";
            right.id = "st2_stage";
            other.id = "elsewhere";
            Assert.Contains(left, Dm.each(GLOB.curtains));
            Assert.Equal("cur_fancy-closed", left.icon_state);
            Assert.True(Dm.truthy(left.density));
            Assert.True(Dm.truthy(left.opacity));
        });
        var button = Mapped<obj_machinery_button_curtain>(r, 5, 4, Dm.list(Dm.kv("id", "st2_stage")));
        r.Step(Second);
        r.In(() =>
        {
            var device = Assert.IsType<obj_item_assembly_control_curtain>(button.device_instance);
            Assert.Equal("st2_stage", device.id);
        });

        // a hand on a mechanical curtain does nothing
        Use(r, bob, left);
        r.In(() => Assert.True(Dm.truthy(left.density)));

        // the button opens both of its curtains and leaves the other
        Use(r, bob, button);
        r.Step(5);
        r.In(() =>
        {
            foreach (var c in new[] { left, right })
            {
                Assert.Equal("cur_fancy-open", c.icon_state);
                Assert.False(Dm.truthy(c.density));
                Assert.False(Dm.truthy(c.opacity));
                Assert.Equal(SIGN_LAYER, c.layer);
            }
            Assert.True(Dm.truthy(other.density));
        });

        // the next press shuts them again
        r.Step(Second);
        Use(r, bob, button);
        r.Step(5);
        r.In(() =>
        {
            Assert.Equal("cur_fancy-closed", left.icon_state);
            Assert.True(Dm.truthy(left.density));
            Assert.True(Dm.truthy(right.opacity));
        });
        r.In(() => qdel(left));
        r.In(() => Assert.DoesNotContain(left, Dm.each(GLOB.curtains)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBookcaseIsBuiltFilledEmptiedByItsListAndPriedApart()
    {
        var (r, feed, bob) = Room(8203);
        var bookcase = Place<obj_structure_bookcase>(r, 5, 4);
        r.In(() =>
        {
            Assert.Equal("bookempty", bookcase.icon_state);
            Assert.False(Dm.truthy(bookcase.anchored));
        });

        // wrenched down (two seconds), a shelf of two planks added
        Hold<obj_item_wrench>(r, bob);
        Use(r, bob, bookcase);
        r.Step(2 * Second + 5);
        r.In(() => Assert.True(Dm.truthy(bookcase.anchored)));
        r.In(() => Assert.Equal(1f, bookcase.state));
        Drop(r, bob);
        var planks = Hold<obj_item_stack_sheet_mineral_wood>(r, bob, 5);
        Use(r, bob, bookcase);
        r.In(() =>
        {
            Assert.Equal(2f, bookcase.state);
            Assert.Equal("book-0", bookcase.icon_state);
            Assert.Equal(3f, Dm.num(planks.amount));
        });
        Assert.True(feed.Any("shelf added"));
        Drop(r, bob);

        // a book goes on the shelf, the list takes it back into the hand
        var book = Hold<obj_item_book_manual_wiki_engineering_guide>(r, bob);
        Use(r, bob, bookcase);
        r.In(() =>
        {
            Assert.Same(bookcase, book.loc);
            Assert.Equal("book-1", bookcase.icon_state);
        });
        Take(bob);
        Use(r, bob, bookcase);
        r.Step(2);
        Window(r, bob, "ListInputWindow", out _, out JsonElement list);
        string entry = list.GetProperty("items")[0].GetString();
        Answer(r, bob, "ListInputWindow", entry);
        r.In(() =>
        {
            Assert.Same(book, bob.get_active_held_item());
            Assert.Equal("book-0", bookcase.icon_state);
        });
        Drop(r, bob);

        // the crowbar pries the shelf out (two planks), the wrench frees the frame, the crowbar takes it apart (four planks)
        Hold<obj_item_crowbar>(r, bob);
        Use(r, bob, bookcase);
        r.In(() => Assert.Equal(1f, bookcase.state));
        Assert.Equal(2f, Amount<obj_item_stack_sheet_mineral_wood>(r, 5, 4));
        Drop(r, bob);
        Hold<obj_item_wrench>(r, bob);
        Use(r, bob, bookcase);
        r.In(() => Assert.Equal(0f, bookcase.state));
        Drop(r, bob);
        Hold<obj_item_crowbar>(r, bob);
        Use(r, bob, bookcase);
        r.Step(2 * Second + 5);
        r.In(() => Assert.True(QDELETED(bookcase)));
        Assert.Equal(6f, Amount<obj_item_stack_sheet_mineral_wood>(r, 5, 4));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMappedManualShelfStandsFinishedWithItsBooksAndTheLibraryCountsThem()
    {
        var (r, _, bob) = Room(8204);
        r.In(() => Assert.True(Dm.truthy(SSlibrary.initialized)));
        var shelf = Mapped<obj_structure_bookcase_manuals_engineering>(r, 5, 4);
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(shelf.anchored));
            Assert.Equal(2f, shelf.state);
            Assert.Equal(4, Dm.each<obj_item_book>(shelf.contents).Count());
            Assert.Equal("book-4", shelf.icon_state);
            // a shelf mapped after the library is up loads at once (tg's INVOKE_ASYNC in the parent's Initialize), before the manual
            // shelf's own Initialize has put its manuals in: as at the round start's load (SSlibrary's Initialize), its books are
            // listed by its area once it loads with them
            Waiting.NoSleep(shelf.load_shelf());
            var books = SSlibrary.books_by_area[(get_area(shelf) as area).type] as DmList;
            Assert.NotNull(books);
            Assert.True(books.len >= 4, "the library counted " + books.len + " books");
        });

        // a mapped bookcase takes the books lying on its tile, the random shelves boot
        var loose = r.In(() => Dm.@new<obj_item_book_manual_wiki_engineering_hacking>(locate(6, 4, 1)));
        var plain = Mapped<obj_structure_bookcase>(r, 6, 4);
        Mapped<obj_structure_bookcase_random_reference>(r, 7, 4);
        Mapped<obj_structure_bookcase_random_fiction>(r, 8, 4);
        r.Step(Second);
        r.In(() => Assert.Same(plain, loose.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCaptainsCaseOpensOnlyByAccessRingsItsAlarmWhenBrokenAndIsMendedWithGlass()
    {
        var (r, feed, bob) = Room(8205);
        var display = Place<obj_structure_displaycase_captain>(r, 5, 4);
        r.In(() => Assert.IsType<obj_item_gun_energy_laser_captain>(display.showpiece));
        Assert.Contains("glassbox_closed", Overlays(r, display));

        // an assistant's card is refused, the case's access opens it
        r.In(() => IdCards.Hold(bob, "maint_tunnels"));
        Use(r, bob, display);
        Assert.True(feed.Any("Access denied."));
        r.In(() => IdCards.Hold(bob, "cent_specops"));
        Use(r, bob, display);
        r.In(() => Assert.True(Dm.truthy(display.open)));
        Assert.DoesNotContain("glassbox_closed", Overlays(r, display));

        // an empty hand takes the laser out, it goes back in
        r.In(() => bob.swap_hand());
        Use(r, bob, display);
        Assert.True(feed.Any("You deactivate the hover field built into the case."));
        var laser = r.In(() => Dm.each<obj_item_gun_energy_laser_captain>(locate(5, 4, 1).contents).Single());
        r.In(() => Assert.Null(display.showpiece));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(laser)));
        Use(r, bob, display);
        r.In(() => Assert.Same(laser, display.showpiece));
        Assert.True(feed.Any("on display."));
        r.In(() => bob.swap_hand());
        Use(r, bob, display);
        r.In(() => Assert.False(Dm.truthy(display.open)));

        // broken: a shard, the alarm and its sound, the broken glass; the hover field drops the laser
        r.In(() => Waiting.NoSleep(display.take_damage(160))); // below its integrity_failure (a quarter), not destroyed
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(display.broken));
            Assert.False(Dm.truthy(display.density));
            Assert.Single(Dm.each<obj_item_shard>(locate(5, 4, 1).contents));
            Assert.True(Dm.truthy((get_area(display) as area).fire)); // the burglar alert lights the area
        });
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("alert") == true);
        Assert.Contains("glassbox_broken", Overlays(r, display));
        r.In(() => bob.swap_hand());
        Use(r, bob, display);
        r.In(() => Assert.Same(locate(5, 4, 1), laser.loc));

        // two glass sheets and two seconds mend it
        r.In(() => bob.swap_hand());
        r.In(() => qdel(bob.get_active_held_item()));
        r.In(() => bob.swap_hand());
        var glass = Hold<obj_item_stack_sheet_glass>(r, bob, 5);
        Use(r, bob, display);
        r.Step(2 * Second + 5);
        r.In(() =>
        {
            Assert.False(Dm.truthy(display.broken));
            Assert.Equal(display.max_integrity, Dm.num(display.atom_integrity));
            Assert.Equal(3f, Dm.num(glass.amount));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTrophyCaseTakesATrophyAndTheCuratorsKeyRecordsItsDescriptionInItsWindow()
    {
        var (r, feed, bob) = Room(8206);
        var trophy = Place<obj_structure_displaycase_trophy>(r, 5, 4);
        r.In(() => Assert.Contains(trophy, Dm.each(GLOB.trophy_cases)));

        // the library's access opens it, a toolbox goes in, the case closes again
        r.In(() => IdCards.Hold(bob, "library"));
        Use(r, bob, trophy);
        r.In(() => Assert.True(Dm.truthy(trophy.open)));
        r.In(() => bob.swap_hand());
        var toolbox = Hold<obj_item_storage_toolbox_mechanical>(r, bob);
        Use(r, bob, trophy);
        r.In(() =>
        {
            Assert.Same(toolbox, trophy.showpiece);
            Assert.Equal(bob.ckey, trophy.placer_key);
        });
        r.In(() => bob.swap_hand());
        Use(r, bob, trophy);
        r.In(() => Assert.False(Dm.truthy(trophy.open)));

        // an empty hand opens its window: the trophy's name and picture, no description yet
        r.In(() => bob.swap_hand());
        Take(bob);
        Use(r, bob, trophy);
        string window = Window(r, bob, "Trophycase", out JsonElement data, out JsonElement trophyStatic);
        Assert.Equal(1, data.GetProperty("has_showpiece").GetInt32());
        Assert.Equal(0, data.GetProperty("historian_mode").GetInt32());
        Assert.Contains("toolbox", data.GetProperty("showpiece_name").GetString(), StringComparison.OrdinalIgnoreCase);
        JsonElement icon = trophyStatic.GetProperty("showpiece_icon");
        Assert.Equal(r.In(() => toolbox.icon_state), icon.GetProperty("icon_state").GetString());
        Assert.Contains("toolbox", icon.GetProperty("icon").GetString());

        // the curator's key in the hand: historian mode, then the description
        Hold<obj_item_key_displaycase>(r, bob);
        Act(r, bob, window, "insert_key");
        r.In(() => Assert.True(Dm.truthy(trophy.historian_mode)));
        Assert.Equal(1, Data(r, bob, out _).GetProperty("historian_mode").GetInt32());
        Act(r, bob, window, "change_message");
        Answer(r, bob, "TextInputModal", "Won at the toolbox derby");
        r.In(() => Assert.Equal("Won at the toolbox derby", trophy.trophy_message));
        Act(r, bob, window, "lock");
        r.In(() => Assert.False(Dm.truthy(trophy.historian_mode)));
        Assert.Contains("Won at the toolbox derby", Data(r, bob, out _).GetProperty("showpiece_description").GetString());
        r.In(() => qdel(trophy));
        r.In(() => Assert.DoesNotContain(trophy, Dm.each(GLOB.trophy_cases)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheVendATrayIsRegisteredStockedAndSoldFromThroughItsWindow()
    {
        var (r, feed, bob) = Room(8207);
        // no payday inside the sale (an assistant's paycheck landed in it once gaps MS1's SSmood moved the MC's ticks)
        r.In(() => SSeconomy.can_fire = FALSE);
        datum_bank_account bobs = VendingTests.Funds(r, bob, 500);
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        r.Step(Second);
        datum_bank_account alices = VendingTests.Funds(r, alice, 100);
        var tray = Place<obj_structure_displaycase_forsale>(r, 5, 4);
        r.In(() => Assert.Equal("laserbox_empty", tray.icon_state));
        Assert.Contains("laserbox_overlay", Overlays(r, tray));

        // Bob opens its window and registers it to his account
        Take(bob);
        Use(r, bob, tray);
        string window = Window(r, bob, "Vendatray", out JsonElement data);
        Assert.Equal(0, data.GetProperty("registered").GetInt32());
        Act(r, bob, window, "Register");
        r.In(() => Assert.Same(bobs, tray.payments_acc));
        JsonElement registered = Data(r, bob, out _);
        Assert.Equal(1, registered.GetProperty("registered").GetInt32());
        Assert.Equal(r.In(() => bobs.account_holder), registered.GetProperty("owner_name").GetString());

        // opened by its owner, a donut goes on it (DV501), closed again
        Act(r, bob, window, "Open");
        r.In(() =>
        {
            Assert.True(Dm.truthy(tray.open));
            Assert.Equal("laserbox_open", tray.icon_state);
        });
        var donut = Hold<obj_item_food_donut_plain>(r, bob);
        Use(r, bob, tray);
        r.In(() => Assert.Same(donut, tray.showpiece));
        JsonElement stocked = Data(r, bob, out _);
        Assert.Contains("donut", stocked.GetProperty("product_name").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(20, stocked.GetProperty("product_cost").GetInt32());
        Act(r, bob, window, "Open");
        r.In(() =>
        {
            Assert.False(Dm.truthy(tray.open));
            Assert.Equal("laserbox", tray.icon_state);
        });

        // Alice buys it: twenty credits from her account to Bob's, the donut in her hand
        Take(alice);
        Use(r, alice, tray);
        string alicesWindow = Window(r, alice, "Vendatray", out JsonElement forSale);
        Assert.Equal(1, forSale.GetProperty("registered").GetInt32());
        Act(r, alice, alicesWindow, "Buy");
        r.In(() =>
        {
            Assert.Same(donut, alice.get_active_held_item());
            Assert.Null(tray.showpiece);
            Assert.Equal("laserbox_empty", tray.icon_state);
            Assert.Equal(80f, alices.account_balance);
            Assert.Equal(520f, bobs.account_balance);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("cashregister") == true);

        // an emag resets its owner
        r.In(() => Waiting.NoSleep(tray.emag_act(bob, null)));
        r.In(() => Assert.Null(tray.payments_acc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheShowcaseIsUnwrenchedUnscrewedAndCrowbarredApartAndTheFakeConsolesWearTheirOverlays()
    {
        var (r, feed, bob) = Room(8208);
        var showcase = Place<obj_structure_showcase_cyborg_old>(r, 5, 4);
        Hold<obj_item_screwdriver>(r, bob);
        Use(r, bob, showcase); // fastened: the screws stay
        r.In(() => Assert.Equal(1f, showcase.deconstruction_state));
        Drop(r, bob);
        Hold<obj_item_wrench>(r, bob);
        Use(r, bob, showcase);
        r.Step(2 * Second + 5);
        r.In(() => Assert.False(Dm.truthy(showcase.anchored)));
        Drop(r, bob);
        Hold<obj_item_screwdriver>(r, bob);
        Use(r, bob, showcase);
        r.In(() => Assert.Equal(2f, showcase.deconstruction_state));
        Assert.True(feed.Any("You unscrew the screws."));
        Drop(r, bob);
        Hold<obj_item_crowbar>(r, bob);
        Use(r, bob, showcase);
        r.Step(2 * Second + 5);
        r.In(() => Assert.True(QDELETED(showcase)));
        Assert.Equal(4f, Amount<obj_item_stack_sheet_iron>(r, 5, 4));

        var fakeid = Place<obj_structure_showcase_fakeid>(r, 6, 6);
        var fakesec = Place<obj_structure_showcase_fakesec>(r, 7, 6);
        Assert.Contains("id_key", Overlays(r, fakeid));
        r.In(() => fakesec.update_appearance()); // tg's Initialize draws no overlays: its first appearance update does
        Assert.Contains("security_key", Overlays(r, fakesec));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSkeletonModelWearsItsClothesWhichAreStrippedAndChanged()
    {
        var (r, feed, bob) = Room(8209);
        var skeleton = Place<obj_structure_mannequin_skeleton>(r, 5, 4);
        string eyes = Dm.str(ITEM_SLOT_EYES), suit = Dm.str(ITEM_SLOT_OCLOTHING), head = Dm.str(ITEM_SLOT_HEAD);
        var eyepatch = r.In(() => Assert.IsType<obj_item_clothing_glasses_eyepatch>(skeleton.worn_items[eyes]));
        r.In(() =>
        {
            Assert.IsType<obj_item_clothing_suit_costume_hawaiian>(skeleton.worn_items[suit]);
            Assert.StartsWith("mannequin_skeleton_", skeleton.icon_state);
        });
        List<string> overlays = Overlays(r, skeleton);
        Assert.Contains("pedestal_skeleton", overlays);
        Assert.True(overlays.Count >= 3, "the worn clothes are not drawn: " + string.Join(", ", overlays));

        // dragged onto Bob, it opens its strip menu
        r.Command(() => ClientSeam.Drop(bob.client, skeleton, bob, Drag, Drag, 5));
        r.Step(Second);
        r.In(() =>
        {
            var element = (datum_element_strippable)SSdcs.GetElement(Dm.list(typeof(datum_element_strippable), GLOB.strippable_mannequin_items));
            Assert.NotNull(element.strip_menus?[skeleton]);
        });

        // the eyepatch comes off into Bob's hand; a cap fits its head and not its eyes
        r.In(() =>
        {
            var eyeSlot = (datum_strippable_item)GLOB.strippable_mannequin_items["eyes"];
            Assert.Same(eyepatch, eyeSlot.get_item(skeleton));
            Waiting.NoSleep(eyeSlot.finish_unequip(skeleton, bob));
            Assert.True(Dm.each(bob.held_items).Contains(eyepatch), "the eyepatch is in " + eyepatch.loc + ", Bob holds " + string.Join(", ", Dm.each(bob.held_items).Select(Dm.str)));
            Assert.Null(skeleton.worn_items[eyes]);
        });
        Drop(r, bob);
        var cap = Hold<obj_item_clothing_head_soft>(r, bob);
        r.In(() =>
        {
            var eyeSlot = (datum_strippable_item)GLOB.strippable_mannequin_items["eyes"];
            Assert.False(Dm.truthy(Waiting.NoSleep(eyeSlot.try_equip(skeleton, cap, bob))));
            var headSlot = (datum_strippable_item)GLOB.strippable_mannequin_items["head"];
            Assert.True(Dm.truthy(Waiting.NoSleep(headSlot.try_equip(skeleton, cap, bob))));
            headSlot.finish_equip(skeleton, cap, bob);
            Assert.Same(cap, skeleton.worn_items[head]);
            Assert.Same(skeleton, cap.loc);
        });
        Assert.True(feed.Any("won't fit!"));

        // its clothes fall when it is destroyed
        r.In(() => Waiting.NoSleep(skeleton.take_damage(1000)));
        r.Step(2);
        r.In(() => Assert.Same(locate(5, 4, 1), cap.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTankHolderGivesUpItsExtinguisherTakesItBackAndIsUnscrewedWhenEmpty()
    {
        var (r, feed, bob) = Room(8210);
        var holder = Place<obj_structure_tank_holder_extinguisher>(r, 5, 4);
        var extinguisher = r.In(() => Assert.IsAssignableFrom<obj_item_extinguisher>(holder.tank));
        string full = r.In(() => holder.icon_state);
        r.In(() =>
        {
            Assert.Same(holder, extinguisher.loc);
            Assert.NotEqual("holder", full);
            Assert.True(Dm.truthy(holder.density));
        });

        // a hand takes it: the empty holder
        Use(r, bob, holder);
        r.In(() =>
        {
            Assert.Same(extinguisher, bob.get_active_held_item());
            Assert.Null(holder.tank);
            Assert.Equal("holder", holder.icon_state);
            Assert.False(Dm.truthy(holder.density));
        });

        // put back
        Use(r, bob, holder);
        r.In(() =>
        {
            Assert.Same(extinguisher, holder.tank);
            Assert.Equal(full, holder.icon_state);
        });

        // the screwdriver leaves a full holder alone and takes an empty one apart into two rods
        Hold<obj_item_screwdriver>(r, bob);
        Use(r, bob, holder);
        r.In(() => Assert.False(QDELETED(holder)));
        r.In(() => bob.swap_hand());
        Use(r, bob, holder);
        r.In(() => Assert.Same(extinguisher, bob.get_active_held_item()));
        r.In(() => bob.swap_hand());
        Use(r, bob, holder);
        r.In(() => Assert.True(QDELETED(holder)));
        Assert.Equal(2f, Amount<obj_item_stack_rods>(r, 5, 4));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BrokenFlooringLiesAsTrashOnAFloorAndTheCrowbarReclaimsItsTile()
    {
        var (r, feed, bob) = Room(8211);
        var broken = Place<obj_structure_broken_flooring>(r, 5, 4);
        r.In(() =>
        {
            Assert.Equal(GAME_PLANE, broken.plane);
            Assert.Equal(LOW_OBJ_LAYER, broken.layer);
        });
        Hold<obj_item_crowbar>(r, bob);
        Use(r, bob, broken);
        r.In(() => Assert.True(QDELETED(broken)));
        Assert.Equal(1f, Amount<obj_item_stack_tile_iron>(r, 5, 4));
        Assert.True(feed.Any("tile reclaimed"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADunkScoresTwoOnTheHoopsBoardAndACtrlClickResetsIt()
    {
        var (r, feed, bob) = Room(8212);
        var hoop = Place<obj_structure_hoop>(r, 5, 4);
        Assert.Contains("basketball_scorecard", Overlays(r, hoop));
        var ball = Hold<obj_item_toy_basketball>(r, bob);
        Use(r, bob, hoop);
        r.In(() =>
        {
            Assert.Equal(2f, hoop.total_score);
            Assert.Same(locate(5, 4, 1), ball.loc);
            Assert.Contains(bob.ckey, Dm.each(ball.pickup_restriction_ckeys).Select(Dm.str));
        });
        Assert.True(feed.Any("dunks"));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("scanbuzz") == true);
        r.In(() =>
        {
            // the board's digits: the ones and the tens over the scorecard
            var made = (DmList)Waiting.NoSleep(hoop.update_overlays());
            var board = Dm.each(made).OfType<mutable_appearance>().Single(o => o.icon_state == "basketball_scorecard");
            var digits = Dm.each(board.overlays).Select(o => o is IDmAppearance a ? a.icon_state : Dm.str(o)).ToList();
            Assert.Contains("days_2", digits);
            Assert.Contains("days_0", digits);
        });

        // ctrl-click: five seconds to reset the score
        Use(r, bob, hoop, "ctrl=1");
        r.Step(5 * Second + 5);
        r.In(() => Assert.Equal(0f, hoop.total_score));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APlaqueIsEngravedWithAFountainPenUnwrenchedAndFastenedToAWall()
    {
        var (r, feed, bob) = Room(8213);
        var plaque = Place<obj_structure_plaque>(r, 5, 4);

        // a plain pen is not fancy enough
        Hold<obj_item_pen>(r, bob);
        Use(r, bob, plaque);
        Assert.True(feed.Any("Your pen isn't fancy enough to engrave this!"));
        Drop(r, bob);

        // the fountain pen: a title, a text and four seconds
        Hold<obj_item_pen_fountain>(r, bob);
        Use(r, bob, plaque);
        Answer(r, bob, "TextInputModal", "Best HoP Award");
        Answer(r, bob, "TextInputModal", "For the paperwork");
        r.Step(4 * Second + 5);
        r.In(() =>
        {
            Assert.True(Dm.truthy(plaque.engraved));
            Assert.Contains("Best HoP Award", plaque.name);
            Assert.Equal("The plaque reads: 'For the paperwork'", plaque.desc);
        });
        Use(r, bob, plaque);
        Assert.True(feed.Any("This plaque has already been engraved."));
        Drop(r, bob);

        // the wrench takes it down as an item that keeps the engraving
        Hold<obj_item_wrench>(r, bob);
        Use(r, bob, plaque);
        r.Step(4 * Second + 5);
        r.In(() => Assert.True(QDELETED(plaque)));
        var item = r.In(() => Dm.each<obj_item_plaque>(locate(4, 4, 1).contents).Single());
        r.In(() =>
        {
            Assert.True(Dm.truthy(item.engraved));
            Assert.Contains("Best HoP Award", item.name);
        });
        Drop(r, bob);

        // fastened to the wall north of Bob: on his tile, shifted onto the wall
        r.In(() => Waiting.NoSleep((locate(4, 5, 1) as turf).ChangeTurf(typeof(turf_closed_wall))));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(item)));
        Use(r, bob, r.In(() => locate(4, 5, 1) as atom));
        r.In(() =>
        {
            Assert.True(QDELETED(item));
            var placed = Dm.each<obj_structure_plaque>(locate(4, 4, 1).contents).Single();
            Assert.Equal(32f, placed.pixel_y);
            Assert.Equal(NORTH, placed.dir);
            Assert.Contains("Best HoP Award", placed.name);
            Assert.Equal("The plaque reads: 'For the paperwork'", placed.desc);
        });

        // a static plaque in the open lies on the floor
        var golden = Place<obj_structure_plaque_static_plaque_golden>(r, 7, 4);
        r.In(() => Assert.Equal(FLOOR_PLANE, golden.plane));
        r.AssertNoRuntimes();
    }
}
