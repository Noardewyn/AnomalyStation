using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package S3 (D298): the paperwork in a booted toy room. A pen writes on paper through tg's PaperSheet window (the text, the
/// written-on icon state, the pen's scratch), a stamp stamps it (the window's stamp, the paper's overlay and sound), a paper burns
/// away, a folded paper plane flies, a folder and a clipboard hold papers, the hand labeler labels, the paper bin hands out paper, the
/// photocopier copies a written paper, a fax reaches another fax, and every paperwork type spawns and goes with no runtimes.
/// </summary>
public class PaperworkTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test");
            (get_area(locate(4, 4, 1)) as area).requires_power = FALSE; // the toy room has no APC
            Dm.@new<obj_machinery_light_floor>(locate(5, 5, 1)); // writing needs light
        });
        r.Step(Second);
        return (r, bob);
    }

    private static T Hold<T>(AudioRoom r, mob who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.Step(2);
        return item;
    }

    private static string WriteOn(AudioRoom r, mob_living_carbon_human bob, obj_item_paper paper, string text)
    {
        Take(bob);
        Click(r, bob, paper);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("PaperSheet", InterfaceOf(payload));
        Assert.Equal(MODE_WRITING, payload.GetProperty("data").GetProperty("held_item_details").GetProperty("interaction_mode").GetInt32());
        Act(r, bob, window, "add_text", ("text", text));
        return window;
    }

    [Fact]
    public void APenWritesThroughThePaperSheetWindowAndThePaperShowsItsWords()
    {
        var (r, bob) = Room(9901);
        var feed = Listen(r);
        obj_item_paper paper = Place<obj_item_paper>(r, 5, 4);
        Hold<obj_item_pen_blue>(r, bob);
        Assert.Equal("paper", r.In(() => paper.icon_state));

        WriteOn(r, bob, paper, "Hello **station**! [____]");
        r.In(() =>
        {
            datum_paper_input input = paper.raw_text_inputs[1] as datum_paper_input;
            Assert.Equal(("Hello **station**! [____]", PEN_FONT, COLOR_BLUE), (input.raw_text, input.font, input.colour));
            Assert.Equal(1f, paper.input_field_count);
        });
        Assert.Equal("paper_words", r.In(() => paper.icon_state));
        Assert.True(feed.Any("You have added to your paper masterpiece!"));
        Assert.True(r.Sounds.Heard.Any(h => h.File != null && h.File.Contains("writing_pen", StringComparison.Ordinal)), r.Sounds.Dump());

        // the window's static data carries the text (update_static_data_for_all_viewers); the field is filled in through fill_input_field
        JsonElement full = Take(bob).Where(o => o.Kind == TguiOutputKind.Message).Select(o => JsonDocument.Parse(o.Json).RootElement)
            .Last(e => e.GetProperty("type").GetString() == "update" && e.GetProperty("payload").TryGetProperty("static_data", out _)).GetProperty("payload");
        Assert.Equal("Hello **station**! [____]", full.GetProperty("static_data").GetProperty("raw_text_input")[0].GetProperty("raw_text").GetString());
        r.In(() => Waiting.NoSleep(paper.ui_act("fill_input_field", Dm.list(Dm.kv("field_data", Dm.list(Dm.kv("0", "%sign")))), SStgui.get_open_ui(bob, paper) as datum_tgui)));
        r.In(() => Assert.Equal(bob.real_name, Dm.str((paper.raw_field_input_data[1] as datum_paper_field).field_data.raw_text))); // %sign: the writer's name
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AStampStampsThePaperWithItsOverlayAndSound()
    {
        var (r, bob) = Room(9902);
        obj_item_paper paper = Place<obj_item_paper>(r, 5, 4);
        Hold<obj_item_stamp_head_captain>(r, bob);
        Take(bob);
        Click(r, bob, paper);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("PaperSheet", InterfaceOf(payload));
        Assert.Equal("stamp-cap", payload.GetProperty("data").GetProperty("held_item_details").GetProperty("stamp_class").GetString());
        Act(r, bob, window, "add_stamp", ("x", 120), ("y", 80), ("rotation", 15));
        r.In(() =>
        {
            datum_paper_stamp stamp = paper.raw_stamp_data[1] as datum_paper_stamp;
            Assert.Equal(("stamp-cap", 120f, 80f, 15f), (stamp.@class, stamp.stamp_x, stamp.stamp_y, stamp.rotation));
            Assert.Equal("stamp-cap", paper.stamp_cache[1]);
            Assert.Contains(Dm.each_cast<object>(paper.overlays), o => o is IDmAppearance a && a.icon_state == "paper_stamp-cap");
        });
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/handling/standard_stamp.ogg");

        // the right click stamps at once, without the window
        obj_item_paper second = Place<obj_item_paper>(r, 4, 5);
        Click(r, bob, second, "right=1;button=right");
        Assert.Equal(1f, r.In(() => LAZYLEN(second.raw_stamp_data)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APaperBurnsAwayAndAFoldedPlaneFlies()
    {
        var (r, bob) = Room(9903);
        obj_item_paper paper = Place<obj_item_paper>(r, 7, 4);
        r.In(() => Waiting.NoSleep(paper.fire_act(1000, 100)));
        r.Step(30 * Second);
        Assert.True(r.In(() => QDELETED(paper)), "the paper burned away");

        obj_item_paper sheet = Hold<obj_item_paper>(r, bob);
        r.In(() => Waiting.NoSleep(sheet.click_alt(bob)));
        r.Step(2);
        obj_item_paperplane plane = r.In(() => bob.get_active_held_item() as obj_item_paperplane);
        Assert.NotNull(plane);
        Assert.Same(sheet, r.In(() => plane.internal_paper));
        r.In(() => Waiting.NoSleep(bob.throw_item(locate(11, 4, 1))));
        r.Step(3 * Second);
        Assert.True(r.In(() => plane.x) >= 9, "the plane flew: " + r.In(() => plane.x));
        r.In(() => Waiting.NoSleep(bob.forceMove(plane.loc)));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(plane)));
        r.In(() => Waiting.NoSleep(plane.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(plane)), "unfolded");
        Assert.Same(sheet, r.In(() => bob.get_active_held_item()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFolderAndAClipboardHoldPapersAndShowThem()
    {
        var (r, bob) = Room(9904);
        obj_item_folder_blue folder = Place<obj_item_folder_blue>(r, 5, 4);
        obj_item_paper paper = Hold<obj_item_paper>(r, bob);
        Click(r, bob, folder);
        Assert.Same(folder, r.In(() => paper.loc));
        r.In(() => Assert.Contains(Dm.each_cast<object>(folder.overlays), o => o is IDmAppearance a && a.icon_state == "folder_paper"));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(folder)));
        Take(bob);
        r.In(() => Waiting.NoSleep(folder.attack_self(bob)));
        r.Step(2);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("Folder", InterfaceOf(payload));
        Assert.Equal("#355e9f", payload.GetProperty("data").GetProperty("bg_color").GetString());
        Assert.Equal("The paper", payload.GetProperty("data").GetProperty("contents")[0].GetString()); // BYOND puts "the" before an embedded atom
        Act(r, bob, window, "remove", ("ref", payload.GetProperty("data").GetProperty("contents_ref")[0].GetString()));
        Assert.Same(bob, r.In(() => paper.loc));

        obj_item_clipboard clipboard = Place<obj_item_clipboard>(r, 5, 5);
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(folder));
            Waiting.NoSleep(bob.put_in_active_hand(paper));
        });
        Click(r, bob, clipboard);
        Assert.Same(paper, r.In(() => clipboard.top_paper));
        obj_item_pen pen = Hold<obj_item_pen>(r, bob);
        r.Step(Second);
        Click(r, bob, clipboard);
        r.In(() =>
        {
            Assert.Same(pen, clipboard.pen);
            var states = Dm.each_cast<object>(clipboard.overlays).OfType<IDmAppearance>().Select(a => a.icon_state).ToList();
            Assert.Contains("clipboard_pen", states);
            Assert.Contains("clipboard_over", states);
            Assert.Contains("paper", states);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHandLabelerLabelsAndThePaperBinHandsOutPaper()
    {
        var (r, bob) = Room(9905);
        obj_item_hand_labeler labeler = Hold<obj_item_hand_labeler>(r, bob);
        r.In(() => { labeler.mode = 1; labeler.label = "MINE"; labeler.icon_state = "labeler1"; });
        obj_item_toner toner = Place<obj_item_toner>(r, 5, 4);
        Click(r, bob, toner);
        Assert.Equal("toner cartridge (MINE)", r.In(() => toner.name));
        Assert.Equal(29f, r.In(() => labeler.labels_left));
        Assert.True(r.In(() => HAS_TRAIT(toner, TRAIT_HAS_LABEL)));

        obj_item_paper_bin bin = Place<obj_item_paper_bin>(r, 4, 5);
        r.In(() => Waiting.NoSleep(bob.drop_all_held_items()));
        Assert.Equal("paper_bin0", r.In(() => bin.icon_state)); // tg's bin: the papers are its overlays
        r.In(() => Assert.Contains(Dm.each_cast<object>(bin.overlays), o => o is IDmAppearance a && a.icon_state == "paper"));
        r.Step(Second);
        Click(r, bob, bin);
        Assert.IsType<obj_item_paper>(r.In(() => bob.get_active_held_item()));
        Assert.Equal(29f, r.In(() => bin.total_paper));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePhotocopierCopiesAWrittenPaper()
    {
        var (r, bob) = Room(9906);
        obj_machinery_photocopier copier = Place<obj_machinery_photocopier_prebuilt>(r, 5, 4);
        obj_item_paper original = Hold<obj_item_paper>(r, bob);
        r.In(() => { original.add_raw_text("ORDERS"); original.update_appearance(); });
        Click(r, bob, copier);
        Assert.Same(copier, r.In(() => original.loc));
        r.Step(Second);
        Take(bob);
        Click(r, bob, copier);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("Photocopier", InterfaceOf(payload));
        Assert.Equal(1, payload.GetProperty("data").GetProperty("has_item").GetInt32());
        // a copy costs the copier's usage cost (gaps P4a: the payment component, payment.dm): Bob pays with a holochip he holds
        obj_item_holochip chip = r.In(() =>
        {
            var held = Dm.@new<obj_item_holochip>(bob.loc, 20);
            Waiting.NoSleep(bob.put_in_inactive_hand(held));
            return held;
        });
        Act(r, bob, window, "make_copy");
        r.Step(5 * Second);
        obj_item_paper copy = r.In(() => Dm.each<obj_item_paper>(copier.loc).FirstOrDefault(p => p != original));
        Assert.NotNull(copy);
        r.In(() =>
        {
            datum_paper_input input = copy.raw_text_inputs[1] as datum_paper_input;
            Assert.Equal(("ORDERS", COLOR_FULL_TONER_BLACK), (input.raw_text, input.colour));
            Assert.Equal(29f, Dm.num(copier.paper_stack[typeof(obj_item_paper)]));
            Assert.True(copier.toner_cartridge.charges < 5);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/printer.ogg");
        Assert.True(r.In(() => QDELETED(chip))); // the chip is spent and the change, 15 credits, comes back as a new holochip
        Assert.Equal(15f, r.In(() => Dm.each<obj_item_holochip>(bob.get_all_contents() as DmList).Sum(c => Dm.num(c.credits))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFaxReachesAnotherFax()
    {
        var (r, bob) = Room(9907);
        obj_machinery_fax sender = Place<obj_machinery_fax>(r, 5, 4);
        obj_machinery_fax receiver = Place<obj_machinery_fax>(r, 10, 6);
        obj_item_paper letter = Hold<obj_item_paper>(r, bob);
        Click(r, bob, sender);
        Assert.Same(sender, r.In(() => letter.loc));
        r.Step(Second);
        Take(bob);
        Click(r, bob, sender);
        var outputs = Take(bob);
        Assert.True(outputs.Count > 0, r.In(() => $"stat {sender.machine_stat}, held {bob.get_active_held_item()}, runtimes: " + string.Join(" | ", r.Room.RuntimeText)));
        JsonElement payload = LastUpdate(outputs, out string window);
        Assert.Equal("Fax", InterfaceOf(payload));
        JsonElement other = payload.GetProperty("data").GetProperty("faxes")[0];
        Assert.Equal(r.In(() => Dm.str(receiver.fax_id)), other.GetProperty("fax_id").GetString());
        Act(r, bob, window, "send", ("id", other.GetProperty("fax_id").GetString()), ("name", other.GetProperty("fax_name").GetString()));
        Assert.Equal("fax_receive", r.In(() => receiver.icon_state));
        r.Step(3 * Second);
        Assert.Same(r.In(() => receiver.loc), r.In(() => letter.loc));
        Assert.Equal("fax", r.In(() => receiver.icon_state));
        Assert.Equal("Receive", r.In(() => Dm.str((receiver.fax_history[1] as DmList)["history_type"])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryPaperworkTypeSpawnsLiveAndGoes()
    {
        var (r, _) = Room(9908);
        string[] roots = { "/obj/item/paper", "/obj/item/pen", "/obj/item/folder", "/obj/item/clipboard", "/obj/item/stamp", "/obj/item/paper_bin", "/obj/item/hand_labeler",
            "/obj/item/hand_labeler_refill", "/obj/item/label", "/obj/item/papercutter", "/obj/item/paperplane", "/obj/item/paperwork", "/obj/item/ticket_machine_ticket", "/obj/item/toner",
            "/obj/item/documents", "/obj/machinery/photocopier", "/obj/machinery/fax", "/obj/machinery/ticket_machine", "/obj/machinery/button/ticket_machine", "/obj/structure/filingcabinet",
            "/obj/structure/noticeboard", "/obj/effect/temp_visual/security_holosign" };
        var types = r.In(() => roots.SelectMany(p => Dm.each_cast<Type>(typesof(text2path(p)))).Where(t => !InertSeam.IsInertType(t)).Distinct().ToList());
        Assert.True(types.Count > 400, "live paperwork types: " + types.Count);
        foreach (Type type in types)
        {
            atom thing = r.In(() => Dm.@new(type, locate(2, 2, 1)) as atom);
            r.Step(1);
            r.In(() => { if (!QDELETED(thing)) qdel(thing); });
        }
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }
}
