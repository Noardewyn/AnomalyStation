using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8y;

/// <summary>
/// M8y slice A: tg's buckling (game/objects/buckling.dm, living.dm set_buckled and resist_buckle, carbon.dm's cuffed resist, the
/// buckled alert) and the types that buckle (chair.dm, pew.dm, sofa.dm, bed.dm, the operating table) in a booted toy room with a
/// joined player, Bob, who drags: tg's unit test unit_tests/buckle.dm (a mob dragged onto a chair, oneself or another), the 2 s
/// do_after of a mob dragged from the next tile, the chair's messages and layer, the bed's lying angle by its headrest, the
/// emergency bed deployed, wheeled with its patient and folded, the stools' sitter offsets, unbuckling by hand and by the alert,
/// and the cuffed breakout.
/// </summary>
public class BucklingTests
{
    private const int Second = 20;
    private const string Drag = "left=1;button=left;drag=left";

    private sealed class Feed : IMessageSink
    {
        public List<(object Target, string Html)> Chat { get; } = new();

        void IMessageSink.Chat(object target, DmList message) => Chat.Add((target, Dm.str(message?["html"])));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) => Chat.Add((viewer, "balloon: " + text));

        public bool Any(string text) => Chat.Any(c => c.Html != null && c.Html.Contains(text, StringComparison.Ordinal));
    }

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, Feed Feed);

    private static Rig Build(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        r.Step(Second);
        return new Rig(r, bob, feed);
    }

    private static T Place<T>(AudioRoom r, int x, int y, float dir = 0) where T : atom_movable => r.In(() =>
    {
        T thing = Dm.@new<T>(locate(x, y, 1));
        if (dir != 0)
            Waiting.NoSleep(thing.setDir(dir));
        return thing;
    });

    // BYOND's MouseDrop through the client seam (protocol version 17): `who` drags `dragged` onto `over`.
    private static void Drop(AudioRoom r, mob who, atom dragged, atom over)
    {
        r.Command(() => ClientSeam.Drop(who.client, dragged, over, Drag, Drag, 5));
        r.Step(2);
    }

    private static void HandClick(AudioRoom r, mob who, atom target)
    {
        r.Command(() => ClientSeam.Click(who.client, target, "left=1;button=left;icon-x=16;icon-y=16"));
        r.Step(2);
    }

    // tg: code/modules/unit_tests/buckle.dm:4 (buckling_self): the dummy drags himself onto a chair on his tile.
    [Fact]
    public void BucklingSelf()
    {
        var g = Build(8801);
        var chair = Place<obj_structure_chair>(g.R, 4, 4);
        Drop(g.R, g.Bob, g.Bob, chair);
        Assert.Same(chair, g.R.In(() => g.Bob.buckled));
        Assert.True(g.Feed.Any("You sit down on"));
        g.R.AssertNoRuntimes();
    }

    // tg: code/modules/unit_tests/buckle.dm:22 (buckling_others): the dummy drags the victim onto a chair on their tile.
    [Fact]
    public void BucklingOthers()
    {
        var g = Build(8802);
        var victim = g.R.Dummy(4, 4);
        var chair = Place<obj_structure_chair>(g.R, 4, 4);
        Drop(g.R, g.Bob, victim, chair);
        Assert.Same(chair, g.R.In(() => victim.buckled));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AFriendDraggedFromTheNextTileIsBuckledAfterTwoSecondsFacesTheChairAndIsHeldThere()
    {
        var g = Build(8803);
        AudioRoom r = g.R;
        var friend = r.Dummy(5, 4);
        var chair = Place<obj_structure_chair>(r, 5, 5, NORTH);
        Drop(r, g.Bob, friend, chair);
        // the friend stands next to the chair: tg's do_after of buckle_delay (2 s); its warning is a viewers' message (visible_message
        // to others is M9's hearing), so the delay is what is checked
        Assert.Null(r.In(() => friend.buckled));
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Same(chair, friend.buckled);
            Assert.Same(chair.loc, friend.loc); // moved onto the chair (buckle_mob's Move)
            Assert.Equal(NORTH, Dm.num(friend.dir)); // BUCKLE_MATCH_DIR
            Assert.Equal(ABOVE_MOB_LAYER, chair.layer); // handle_layer: a chair facing north covers its sitter
            Assert.True(HAS_TRAIT(friend, TRAIT_IMMOBILIZED));
            Assert.Equal(STANDING_UP, Dm.num(friend.body_position)); // buckle_lying 0
            Assert.True(Dm.truthy(friend.alerts?[ALERT_BUCKLED]));
            Assert.True(Dm.@in(friend, chair.buckled_mobs));
        });
        // the chair turned turns its sitter (handle_rotation)
        r.In(() => Waiting.NoSleep(chair.setDir(EAST)));
        Assert.Equal(EAST, r.In(() => Dm.num(friend.dir)));
        Assert.Equal(OBJ_LAYER, r.In(() => chair.layer));
        // Bob's empty hand on the chair unbuckles him (attack_hand, user_unbuckle_mob)
        HandClick(r, g.Bob, chair);
        r.In(() =>
        {
            Assert.Null(friend.buckled);
            Assert.False(HAS_TRAIT(friend, TRAIT_IMMOBILIZED));
            Assert.False(Dm.truthy(friend.alerts?[ALERT_BUCKLED]));
            Assert.Equal(0f, LAZYLEN(chair.buckled_mobs));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBuckledAlertUnbucklesItsOwnerAndAFlooredSitterDropsWhenHeStandsUp()
    {
        var g = Build(8804);
        AudioRoom r = g.R;
        var stool = Place<obj_structure_chair_stool_bar>(r, 4, 4);
        Drop(r, g.Bob, g.Bob, stool);
        r.In(() =>
        {
            Assert.Same(stool, g.Bob.buckled);
            // the bar stool's sitter offsets (chair.dm:335, :374): the bar's z 7 replaces the stool's 4 under the same source
            Assert.Equal(7f, Dm.num(((DmList)g.Bob.offsets[PIXEL_Z_OFFSET])[stool.type]));
        });
        // resting on a chair keeps him up; standing up from it, he drops (set_buckled: the chair's buckle_lying 0)
        r.In(() => Waiting.NoSleep(g.Bob.set_resting(TRUE, silent: TRUE)));
        Assert.Equal(STANDING_UP, r.In(() => Dm.num(g.Bob.body_position)));
        var alert = r.In(() => (atom_movable_screen_alert)g.Bob.alerts[ALERT_BUCKLED]);
        r.Command(() => ClientSeam.Click(g.Bob.client, alert, "left=1;button=left;icon-x=16;icon-y=16"));
        r.Step(2);
        r.In(() =>
        {
            Assert.Null(g.Bob.buckled);
            Assert.Equal(LYING_DOWN, Dm.num(g.Bob.body_position));
            Assert.Null(((DmList)g.Bob.offsets?[PIXEL_Z_OFFSET])?[stool.type]);
        });
        Assert.True(g.Feed.Any("You stand up from"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACuffedSitterResistsOutAfterTheCuffsBreakoutTime()
    {
        var g = Build(8805);
        AudioRoom r = g.R;
        var chair = Place<obj_structure_chair>(r, 4, 4);
        Drop(r, g.Bob, g.Bob, chair);
        Assert.Same(chair, r.In(() => g.Bob.buckled));
        obj_item_restraints_handcuffs cuffs = r.In(() => Dm.@new<obj_item_restraints_handcuffs>(locate(4, 4, 1)));
        r.In(() => cuffs.breakouttime = 5 * SECONDS);
        r.In(() => Waiting.NoSleep(g.Bob.equip_to_slot(cuffs, ITEM_SLOT_HANDCUFFED)));
        r.Step();
        // resisting while buckled is the buckle's first (execute_resist): carbon.dm:152's do_after of the cuffs' breakouttime
        r.Command(() => Waiting.Detach(g.Bob.execute_resist()));
        r.Step(2);
        Assert.True(g.Feed.Any("You attempt to unbuckle yourself... (This will take around 5 seconds and you must stay still.)"));
        r.Step(4 * Second);
        Assert.Same(chair, r.In(() => g.Bob.buckled));
        r.Step(2 * Second);
        Assert.Null(r.In(() => g.Bob.buckled));
        Assert.Same(cuffs, r.In(() => g.Bob.handcuffed)); // still cuffed: only the buckle came off
        Assert.True(g.Feed.Any("You unbuckle yourself from"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARollerBedIsDeployedTakesAPatientLyingRollsWithHimAndFoldsOnlyEmpty()
    {
        var g = Build(8806);
        AudioRoom r = g.R;
        obj_item_emergency_bed folded = r.In(() => Dm.@new<obj_item_emergency_bed>(locate(4, 4, 1)));
        r.In(() => Waiting.NoSleep(g.Bob.put_in_active_hand(folded)));
        // used on the floor next to him: deploy_bed (bed.dm:280)
        r.Command(() => ClientSeam.Click(g.Bob.client, locate(5, 4, 1), "left=1;button=left;icon-x=16;icon-y=16"));
        r.Step(2);
        var bed = Assert.Single(r.In(() => Dm.each_cast<atom>(locate(5, 4, 1).contents).OfType<obj_structure_bed_medical_emergency>().ToList()));
        Assert.True(r.In(() => QDELETED(folded)));
        Assert.Equal("emerg_down", r.In(() => bed.icon_state));

        var patient = r.Dummy(5, 5);
        Drop(r, g.Bob, patient, bed);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Same(bed, patient.buckled);
            Assert.Equal(LYING_DOWN, Dm.num(patient.body_position));
            Assert.Equal(270f, patient.lying_angle); // buckle_lying 270 facing south (the medical bed's left headrest dirs are SOUTHWEST)
            Assert.Equal(SOUTH, Dm.num(patient.dir)); // the bed's buckle_dir
            Assert.True(Dm.truthy(bed.density));
            Assert.Equal("emerg_up", bed.icon_state);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File?.StartsWith("sound/machines/buckle/buckle", StringComparison.Ordinal) == true); // SFX_SEATBELT_BUCKLE

        // a folded bed with a patient is refused (attack_hand_secondary)
        r.Step(Second); // the click cooldown
        r.Command(() => ClientSeam.Click(g.Bob.client, bed, "right=1;button=right;icon-x=16;icon-y=16"));
        r.Step(2);
        Assert.False(r.In(() => QDELETED(bed)));

        // Bob pulls the bed: the patient rolls with it (handle_buckled_mob_movement)
        r.In(() => Waiting.NoSleep(g.Bob.start_pulling(bed)));
        r.In(() => Waiting.NoSleep(g.Bob.Move(locate(3, 4, 1), WEST)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(locate(4, 4, 1), bed.loc);
            Assert.Same(bed.loc, patient.loc);
            Assert.Same(bed, patient.buckled);
        });
        r.In(() => Waiting.NoSleep(g.Bob.stop_pulling()));

        // unbuckled by hand: he gets up from it; then the empty bed folds into Bob's hand
        HandClick(r, g.Bob, bed);
        Assert.Null(r.In(() => patient.buckled));
        Assert.False(r.In(() => Dm.truthy(bed.density)));
        r.Step(Second); // the click cooldown
        r.Command(() => ClientSeam.Click(g.Bob.client, bed, "right=1;button=right;icon-x=16;icon-y=16"));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(bed)));
        Assert.IsType<obj_item_emergency_bed>(r.In(() => g.Bob.get_active_held_item()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABedLaysItsPatientByItsHeadrestAndOperatingTableLiftsHimAndFreesSurgery()
    {
        var g = Build(8807);
        AudioRoom r = g.R;
        var bed = Place<obj_structure_bed>(r, 6, 4);
        var east = Place<obj_structure_bed>(r, 6, 6, EAST);
        r.In(() =>
        {
            Assert.Equal(90f, bed.buckle_lying); // the bed's left_headrest_dirs are NORTHEAST
            Assert.Equal(270f, east.buckle_lying);
            Assert.True(HAS_TRAIT(locate(6, 4, 1), TRAIT_ELEVATED_TURF)); // the bed's elevation 8
        });
        var patient = r.Dummy(6, 4);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bed.buckle_mob(patient)))));
        r.In(() =>
        {
            Assert.Equal(90f, patient.lying_angle);
            Assert.Equal(SOUTH, Dm.num(patient.dir));
        });

        var table = Place<obj_structure_table_optable>(r, 8, 4);
        var other = r.Dummy(8, 4);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(table.buckle_mob(other)))));
        r.In(() =>
        {
            Assert.Equal(90f, other.lying_angle);
            Assert.Equal(6f, Dm.num(((DmList)other.offsets[PIXEL_Z_OFFSET])[table.type]));
            Assert.True(Dm.truthy(other.GetComponent(typeof(datum_component_free_operation))));
            Assert.Same(other, table.patient); // lying on its tile: the table's patient
            Waiting.NoSleep(table.unbuckle_mob(other));
            Assert.False(Dm.truthy(other.GetComponent(typeof(datum_component_free_operation))));
            Assert.Null(((DmList)other.offsets?[PIXEL_Z_OFFSET])?[table.type]);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PewsSofasAndAShuttleSeatBuckleAsChairs()
    {
        var g = Build(8808);
        AudioRoom r = g.R;
        var pew = Place<obj_structure_chair_pew_left>(r, 4, 4);
        Drop(r, g.Bob, g.Bob, pew);
        Assert.Same(pew, r.In(() => g.Bob.buckled));
        HandClick(r, g.Bob, pew);
        Assert.Null(r.In(() => g.Bob.buckled));

        var dummy = r.Dummy(6, 4);
        var sofa = Place<obj_structure_chair_sofa_corp_corner>(r, 6, 4, NORTH);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(sofa.buckle_mob(dummy)))));
        Assert.Equal(OBJ_LAYER, r.In(() => sofa.layer)); // the corner's handle_layer does nothing

        var seat = Place<obj_structure_chair_comfy_shuttle>(r, 8, 4);
        var rider = r.Dummy(8, 4);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(seat.buckle_mob(rider)))));
        r.AssertNoRuntimes();
    }
}
