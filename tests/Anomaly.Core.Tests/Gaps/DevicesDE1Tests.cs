using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package DE1 (D333): the devices in a booted toy room with Bob as a player: the universal recorder (what is said near it
/// recorded with the tape's time stamps, played back through its own say at the recorded pace, the transcript printed on paper, the
/// tape ejected; its radial menu, its states and sounds), the tape (flipped, unspooled, wound back with a screwdriver, unspooled by a
/// fire), the N-spect scanner (the contraband scan with its cell's charge, the cell cover and the cell) and the gavel (on its block,
/// and the knee tap of the kneejerk element).
/// </summary>
public class DevicesDE1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
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

    // the use-in-hand key (Z, tg's activate_inhand: the held item's attack_self), pressed and released
    private static void UseInHand(AudioRoom r, mob who)
    {
        r.Step(Second / 2);
        r.Command(() => ClientSeam.Keybinding(who.client, 4, true));
        r.Step(2);
        r.Command(() => ClientSeam.Keybinding(who.client, 4, false));
        r.Step(2);
    }

    private static void PickRadial(AudioRoom r, mob who, string name)
    {
        r.Step(2);
        (long id, int index) = r.In(() =>
        {
            datum_radial_menu menu = Dm.each_cast<datum_radial_menu>(who.client.dm_radial_menus).SingleOrDefault();
            Assert.True(menu != null, "no radial menu");
            int i = menu.dm_elements.FindIndex(e => e.Name != null && e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            Assert.True(i >= 0, "no slice " + name + " in " + string.Join(", ", menu.dm_elements.Select(e => e.Name)));
            return (menu.dm_id, i + 1);
        });
        r.Command(() => ClientSeam.RadialPick(who.client, id, index));
        r.Step(6); // the radial's wait (a stoplag loop) resumes, then the pick runs
    }

    private static List<string> Slices(AudioRoom r, mob who) => r.In(() =>
        Dm.each_cast<datum_radial_menu>(who.client.dm_radial_menus).Single().dm_elements.Select(e => e.Name).ToList());

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    [Fact]
    public void TheRecorderRecordsWhatIsSaidPlaysItBackAtItsPaceAndPrintsItsTranscript()
    {
        var (r, feed, bob) = Room(8601);
        var recorder = Hold<obj_item_taperecorder>(r, bob);
        obj_item_tape tape = r.In(() =>
        {
            Assert.IsType<obj_item_tape_random>(recorder.mytape);
            Assert.Same(recorder, recorder.mytape.loc);
            Assert.StartsWith("tape (", recorder.mytape.name, StringComparison.Ordinal);
            Assert.Equal("taperecorder_idle", recorder.icon_state);
            return recorder.mytape;
        });

        // the radial of an idle recorder; Record: tg's click, the recording state and the hiss
        UseInHand(r, bob);
        Assert.Equal(new[] { "Record", "Play", "Eject" }, Slices(r, bob));
        PickRadial(r, bob, "Record");
        r.Step(5);
        r.In(() =>
        {
            Assert.True(Dm.truthy(recorder.recording));
            Assert.Equal("taperecorder_recording", recorder.icon_state);
        });
        Assert.True(Heard(r, "taperecorder/taperecorder_play"));
        Assert.True(feed.Any("balloon: started recording"));
        r.Step(2 * Second);
        Assert.True(Heard(r, "taperecorder_hiss"));

        // what is said is stored with the tape's time
        r.In(() => Waiting.NoSleep(bob.say("Hello there")));
        r.Step(3 * Second);
        r.In(() => Waiting.NoSleep(bob.say("General Kenobi")));
        r.Step(Second);
        r.In(() =>
        {
            List<string> lines = Dm.each(tape.storedinfo).Select(Dm.str).ToList();
            Assert.Equal(2, lines.Count);
            Assert.Matches(@"^\[00:\d\d\] .+: Hello there", lines[0]);
            Assert.Contains(": General Kenobi", lines[1], StringComparison.Ordinal);
            Assert.Equal(2, Dm.each(tape.timestamp).Count());
            Assert.True(Dm.num(tape.timestamp[2]) - Dm.num(tape.timestamp[1]) >= 2 * SECONDS);
            Assert.True(tape.used_capacity >= 5 * SECONDS);
        });

        // Stop
        UseInHand(r, bob);
        Assert.Equal(new[] { "Stop", "Eject" }, Slices(r, bob));
        PickRadial(r, bob, "Stop");
        r.In(() =>
        {
            Assert.False(Dm.truthy(recorder.recording));
            Assert.Equal("taperecorder_idle", recorder.icon_state);
        });
        Assert.True(Heard(r, "taperecorder/taperecorder_stop"));

        // the alt-click plays it back: the recorder says each line, the second after the recorded pause
        feed.Chat.Clear();
        Use(r, bob, recorder, "alt=1");
        r.Step(5);
        r.In(() =>
        {
            Assert.True(Dm.truthy(recorder.playing));
            Assert.Equal("taperecorder_playing", recorder.icon_state);
        });
        Assert.True(feed.Any("Hello there"));
        Assert.False(feed.Any("General Kenobi"));
        r.Step(5 * Second);
        Assert.True(feed.Any("General Kenobi"));
        r.Step(4 * Second);
        Assert.True(feed.Any("balloon: recording ended"));
        r.In(() =>
        {
            Assert.False(Dm.truthy(recorder.playing));
            Assert.Equal("taperecorder_idle", recorder.icon_state);
        });

        // the transcript, on paper in Bob's free hand; the printer then rests for thirty seconds
        UseInHand(r, bob);
        PickRadial(r, bob, "Print Transcript");
        r.Step(4);
        obj_item_paper paper = r.In(() => Assert.Single(Dm.each<obj_item_paper>(bob.held_items)));
        r.In(() =>
        {
            Assert.Equal(Dm.text("paper- '[] Transcript' page 1", tape.name), paper.name);
            string text = string.Join("", Dm.each_cast<datum_paper_input>(paper.raw_text_inputs).Select(i => Dm.str(i.raw_text)));
            Assert.Contains("Transcript:", text, StringComparison.Ordinal);
            Assert.Contains("Hello there", text, StringComparison.Ordinal);
            Assert.Contains("General Kenobi", text, StringComparison.Ordinal);
            Assert.False(Dm.truthy(recorder.canprint));
        });
        Assert.True(Heard(r, "taperecorder_print"));
        r.Step(31 * Second);
        r.In(() => Assert.True(Dm.truthy(recorder.canprint)));

        // the tape ejected into the free hand: an empty recorder
        r.In(() => qdel(paper));
        UseInHand(r, bob);
        PickRadial(r, bob, "Eject");
        r.In(() =>
        {
            Assert.Null(recorder.mytape);
            Assert.Contains(tape, Dm.each(bob.held_items));
            Assert.Equal("taperecorder_empty", recorder.icon_state);
        });
        Assert.True(Heard(r, "taperecorder_open"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTapeFlipsUnspoolsRefusesTheRecorderAndIsWoundBackWithAScrewdriver()
    {
        var (r, feed, bob) = Room(8602);
        var tape = Hold<obj_item_tape>(r, bob);
        string front = r.In(() =>
        {
            Assert.StartsWith("tape (", tape.name, StringComparison.Ordinal);
            Dm.append(ref tape.storedinfo, "[00:01] Bob: side A");
            Dm.append(ref tape.timestamp, 10);
            tape.used_capacity = 20;
            return tape.icon_state;
        });
        string other = front.EndsWith("_reverse", StringComparison.Ordinal) ? front[..^"_reverse".Length] : front + "_reverse";

        // Flip: the other side, with tg's flip sound and the other state
        UseInHand(r, bob);
        Assert.Equal(new[] { "Unwind tape", "Flip tape" }, Slices(r, bob));
        PickRadial(r, bob, "Flip tape");
        r.In(() =>
        {
            Assert.Empty(Dm.each(tape.storedinfo));
            Assert.Equal(0f, tape.used_capacity);
            Assert.Equal("[00:01] Bob: side A", Dm.str(tape.storedinfo_otherside[1]));
            Assert.Equal(20f, tape.used_capacity_otherside);
            Assert.Equal(other, tape.icon_state);
        });
        Assert.True(Heard(r, "tape_flip"));

        // Unwind: the ribbon hangs out
        UseInHand(r, bob);
        PickRadial(r, bob, "Unwind tape");
        r.In(() => Assert.True(Dm.truthy(tape.unspooled)));
        Assert.Contains("ribbonoverlay", Overlays(r, tape));
        string examined = r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(tape.examine(bob))).Select(Dm.str)));
        Assert.Contains("unspooled", examined, StringComparison.Ordinal);

        // an unspooled tape goes into an empty recorder (tg's click) but does not record
        var recorder = Place<obj_item_taperecorder_empty>(r, 5, 4);
        r.In(() => Assert.Equal("taperecorder_empty", recorder.icon_state));
        Use(r, bob, recorder);
        r.In(() =>
        {
            Assert.Same(tape, recorder.mytape);
            Assert.Equal("taperecorder_idle", recorder.icon_state);
        });
        Assert.True(Heard(r, "taperecorder_close"));
        Use(r, bob, recorder); // picked up with the empty hand
        r.In(() => Assert.Same(recorder, bob.get_active_held_item()));
        UseInHand(r, bob);
        PickRadial(r, bob, "Record");
        r.Step(5);
        r.In(() => Assert.False(Dm.truthy(recorder.recording)));
        Assert.True(feed.Any("balloon: no spooled tape!"));

        // the other hand takes the tape out of the held recorder; on the floor, a screwdriver winds it back in twelve seconds
        r.In(() => bob.swap_hand());
        Use(r, bob, recorder);
        r.In(() =>
        {
            Assert.Null(recorder.mytape);
            Assert.Same(tape, bob.get_active_held_item());
        });
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(tape)));
        Hold<obj_item_screwdriver>(r, bob);
        Use(r, bob, tape);
        Assert.True(feed.Any("balloon: respooling tape..."));
        r.Step(12 * Second + 5);
        r.In(() => Assert.False(Dm.truthy(tape.unspooled)));
        Assert.DoesNotContain("ribbonoverlay", Overlays(r, tape));
        Assert.True(feed.Any("balloon: tape respooled"));

        // a fire unspools the tape in a recorder
        var loaded = r.In(() => Dm.@new<obj_item_taperecorder>(locate(6, 6, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(loaded.fire_act(1000, 100)));
        r.In(() => Assert.True(Dm.truthy(loaded.mytape.unspooled)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRandomTapesPickAColourAndTheDyedTapeIsTinted()
    {
        var (r, feed, bob) = Room(8603);
        var states = new HashSet<string>();
        for (int i = 0; i < 6; i++)
        {
            var random = r.In(() => Dm.@new<obj_item_tape_random>(locate(5 + i, 5, 1)));
            r.In(() =>
            {
                Assert.Matches("^tape_(white|blue|red|yellow|purple|greyscale)(_reverse)?$", random.icon_state);
                Assert.Matches(@"^tape \([0-9a-fA-F]{3}\)$", random.name);
                states.Add(random.icon_state);
            });
        }
        Assert.True(states.Count > 1, "six random tapes of one colour");
        var dyed = r.In(() => Dm.@new<obj_item_tape_dyed>(locate(5, 6, 1)));
        r.In(() =>
        {
            string colour = dyed.name.Substring("tape (".Length, 3);
            Assert.Equal(("#" + colour).ToLowerInvariant(), Dm.str(dyed.color).ToLowerInvariant());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheInspectorScansForContrabandWithItsCellAndItsCoverAndCellAreWorkedByHand()
    {
        var (r, feed, bob) = Room(8604);
        var inspector = Hold<obj_item_inspector>(r, bob);
        var cell = r.In(() => Assert.IsType<obj_item_stock_parts_power_store_cell_crap>(inspector.cell));
        float charge = r.In(() => Dm.num(cell.charge));
        var wrench = Place<obj_item_wrench>(r, 5, 4);
        var bomb = Place<obj_item_screwdriver>(r, 5, 5);
        r.In(() => ADD_TRAIT(bomb, TRAIT_CONTRABAND, INNATE_TRAIT));

        // a clean item: the ping and "clear"; the scan costs a thousandth of a standard cell
        Use(r, bob, wrench);
        Assert.True(feed.Any("balloon: clear"));
        Assert.True(Heard(r, "machines/ping"));
        r.In(() => Assert.Equal(charge - 0.001f * STANDARD_CELL_CHARGE, Dm.num(cell.charge), 3));

        // contraband: the uplink's error
        Use(r, bob, bomb);
        Assert.True(feed.Any("balloon: contraband detected!"));
        Assert.True(Heard(r, "uplink/uplinkerror"));

        // a scanner that scans wrongly finds the clean item and clears the contraband
        r.In(() => inspector.scans_correctly = FALSE);
        feed.Chat.Clear();
        Use(r, bob, wrench);
        Assert.True(feed.Any("balloon: contraband detected!"));
        r.In(() => inspector.scans_correctly = TRUE);

        // the cover pried open: no scan; the cell taken out by a ctrl-click with an empty hand, put back by hand
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(inspector)));
        r.In(() => Waiting.NoSleep(inspector.forceMove(locate(4, 5, 1))));
        var crowbar = Hold<obj_item_crowbar>(r, bob);
        Use(r, bob, inspector);
        r.In(() => Assert.True(Dm.truthy(inspector.cell_cover_open)));
        Assert.True(feed.Any("balloon: opened cell cover"));
        Drop(r, bob);
        Use(r, bob, inspector, "ctrl=1");
        r.In(() =>
        {
            Assert.Null(inspector.cell);
            Assert.Same(cell, bob.get_active_held_item());
        });
        Use(r, bob, inspector);
        r.In(() => Assert.Same(cell, inspector.cell));
        Assert.True(feed.Any("You successfully install"));

        // scanning with the cover open, then with no cell
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(inspector)));
        Use(r, bob, wrench);
        Assert.True(feed.Any("balloon: close cover first!"));
        r.In(() =>
        {
            inspector.cell_cover_open = FALSE;
            qdel(cell);
        });
        r.Step(2);
        r.In(() => Assert.Null(inspector.cell));
        Use(r, bob, wrench);
        Assert.True(feed.Any("balloon: check cell!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGavelBangsOnItsBlockAndTapsABuckledKnee()
    {
        var (r, feed, bob) = Room(8605);
        var block = Place<obj_item_gavelblock>(r, 5, 4);
        var gavel = Hold<obj_item_gavelhammer>(r, bob);
        Use(r, bob, block);
        Assert.True(Heard(r, "items/gavel"));
        Assert.True(feed.Any("strikes the sound block with the gavel"));

        // the knee tap: a right click on a seated man's leg
        var dummy = r.Dummy(4, 5);
        r.In(() =>
        {
            var chair = Dm.@new<obj_structure_chair>(locate(4, 5, 1));
            Waiting.NoSleep(chair.buckle_mob(dummy, force: TRUE));
            Assert.NotNull(dummy.buckled);
            bob.zone_selected = BODY_ZONE_L_LEG;
        });
        float brute = r.In(() => Dm.num(dummy.get_brute_loss()));
        r.Step(Second);
        r.Command(() => ClientSeam.Click(bob.client, dummy, "icon-x=16;icon-y=16;right=1;button=right"));
        r.Step(4);
        Assert.True(feed.Any("gently taps"));
        Assert.True(feed.Any("leg kicks out sharply"));
        Assert.True(Heard(r, "weapons/punchmiss"));
        r.In(() => Assert.Equal(brute, Dm.num(dummy.get_brute_loss())));
        r.AssertNoRuntimes();
    }
}
