using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI4 (D326): the AI. A player made an AI gets its core, its eye, its laws and its command verbs; the camera net leaves static
/// where no camera sees and none where one does; the AI's clicks reach a machine its cameras see (ctrl-click bolts an airlock); the
/// holopad projects the AI's hologram and the hologram follows the eye within its range; an intelliCard carries the AI out of its core
/// and into the integrity restorer, which brings it back to full health; a VOX word plays on the VOX channel and waits its turn.
/// </summary>
public class AiTests
{
    private const int Second = 20;

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.In(() =>
        {
            // the toy room has no APC: free power
            (get_area(locate(5, 5, 1)) as area).requires_power = FALSE;
            // tg's shipped config allows the AI job (game_options.txt ALLOW_AI); the toy room's config is tg's defaults, so it is set
            // and the jobs are set up again with it
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_flag_allow_ai), TRUE));
            SSjob.setup_occupations();
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    // tg's admin Make AI, here: the player becomes an AI where they stand (AIize(client, move = FALSE))
    private static mob_living_silicon_ai Aiize(AudioRoom r, mob_living_carbon_human who)
    {
        r.In(() => Waiting.Detach(who.AIize(who.client, FALSE)));
        r.Step(Second);
        return r.In(() => Dm.each<mob_living_silicon_ai>(GLOB.ai_list).Single());
    }

    private static obj_machinery_camera Camera(AudioRoom r, int x, int y)
    {
        var camera = r.In(() => Dm.@new<obj_machinery_camera>(locate(x, y, 1)));
        r.Step(Second); // SScameras updates the chunks around a new camera
        return camera;
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(mob_living_silicon_ai), typeof(mob_eye_camera_ai), typeof(mob_eye_camera_ai_pic_in_pic), typeof(datum_camerachunk), typeof(datum_trackable),
            typeof(obj_machinery_holopad), typeof(obj_machinery_holopad_secure), typeof(obj_effect_overlay_holo_pad_hologram), typeof(obj_effect_overlay_holoray),
            typeof(datum_holocall), typeof(obj_item_disk_holodisk), typeof(datum_wires_holopad), typeof(obj_structure_ai_core), typeof(obj_structure_ai_core_latejoin_inactive),
            typeof(obj_item_circuitboard_aicore), typeof(obj_item_aicard), typeof(obj_machinery_computer_aifixer), typeof(datum_computer_file_program_ai_restorer),
            typeof(datum_job_ai), typeof(obj_effect_landmark_start_ai), typeof(datum_robot_control), typeof(datum_ai_core_display_picker), typeof(datum_ai_status_display_picker),
            typeof(datum_emote_ai_emotion_display), typeof(obj_machinery_ai_slipper), typeof(obj_machinery_camera_autoname), typeof(obj_machinery_camera_motion),
            typeof(obj_machinery_camera_emp_proof), typeof(obj_machinery_camera_xray), typeof(obj_item_wallframe_camera), typeof(atom_movable_screen_movable_pic_in_pic_ai),
            typeof(datum_hud_ai), typeof(atom_movable_screen_ai_aicore), typeof(datum_round_event_camera_failure), typeof(datum_saymode_holopad),
            typeof(datum_preference_choiced_ai_core_display), typeof(datum_preference_choiced_ai_hologram_display),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void APlayerMadeAnAiGetsItsEyeLawsAndCommands()
    {
        var (r, feed, bob) = Room(9501);
        var ai = Aiize(r, bob);
        r.In(() =>
        {
            Assert.NotNull(ai.client);
            Assert.NotNull(ai.eyeobj);
            Assert.Same(ai, ai.eyeobj.ai);
            Assert.Same(ai.eyeobj, ai.client.eye);
            Assert.NotNull(ai.laws);
            Assert.NotNull(ai.hud_used as datum_hud_ai);
        });
        Assert.True(feed.Any("Obey these laws"), "the AI is told its laws");
        // tg's stat panel tab: the AI's command verbs
        IReadOnlyList<CommandVerb> verbs = CommandVerbs.For(ai);
        Assert.Contains(verbs, v => v.Category == "AI Commands" && v.Name == "Show Laws");
        Assert.Contains(verbs, v => v.Category == "AI Commands" && v.Name == "Toggle Floor Bolts");
        // running a verb: the floor bolts come off
        CommandVerb bolts = verbs.First(v => v.Name == "Toggle Floor Bolts");
        float anchoredBefore = r.In(() => ai.is_anchored);
        r.Command(() => ClientSeam.RunVerb(ai.client, bolts.Path));
        r.Step(Second);
        Assert.NotEqual(anchoredBefore, r.In(() => ai.is_anchored));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCameraNetLeavesStaticWhereNoCameraSees()
    {
        var (r, _, bob) = Room(9502);
        var ai = Aiize(r, bob);
        // the core's built-in camera (tg's builtInCamera) sees the room; switched off, no camera is left
        r.In(() => ai.builtInCamera.toggle_cam(null, 0));
        r.Step(Second);
        // no camera: every tile of the chunks around the eye is obscured
        r.In(() =>
        {
            Assert.True(Dm.length(ai.eyeobj.visibleCameraChunks) > 0, "the eye looks into the chunks around it");
            Assert.False(SScameras.is_visible_by_cameras(locate(5, 5, 1)));
        });
        int obscuredBefore = r.In(() => Dm.each<datum_camerachunk>(ai.eyeobj.visibleCameraChunks).Sum(c => (int)Dm.length(c.obscuredTurfs)));
        Assert.True(obscuredBefore > 0);
        // a camera sees the room: its tiles lose their static
        Camera(r, 2, 2);
        r.In(() => ai.eyeobj.setLoc(locate(6, 5, 1)));
        r.Step(Second);
        r.In(() => Assert.True(SScameras.is_visible_by_cameras(locate(5, 5, 1)), "the camera sees the middle of the room"));
        int obscuredAfter = r.In(() => Dm.each<datum_camerachunk>(ai.eyeobj.visibleCameraChunks).Sum(c => (int)Dm.length(c.obscuredTurfs)));
        Assert.True(obscuredAfter < obscuredBefore, $"the camera clears static ({obscuredBefore} -> {obscuredAfter})");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEyeFliesFreelyAtTheSlowerSpeedAndTheMesonSwitchDropsSeeTurfs()
    {
        // DV423 (the owner, 2026-11-14): a held key flies the eye continuously at the ghost's 7 tiles a second (tg stepped it a tile a tick)
        var (r, _, bob) = Room(9511);
        Anomaly.Core.Motion.MotionState Eye(mob_living_silicon_ai a) => r.In(() =>
        {
            Assert.True(LocomotionSeam.TryGetState(World.Current, a.eyeobj.dm_id, out Anomaly.Core.Motion.MotionState s, out _), "the eye has a capsule");
            return s;
        });
        r.In(() => LocomotionSeam.Install(r.World));
        var ai = Aiize(r, bob);
        r.In(() => LocomotionSeam.Register(ai));
        int startX = r.In(() => (int)ai.eyeobj.x);
        bool between = false;
        for (uint seq = 1; seq <= 10; seq++)
        {
            r.In(() => LocomotionSeam.SetFrame(ai, new Anomaly.Core.Motion.MotionInput(seq, Anomaly.Core.Motion.MotionInput.MaxMove, 0, 0, Anomaly.Core.Motion.MotionInputFlags.None)));
            r.Step(1);
            between |= Eye(ai).X % Anomaly.Core.Motion.MotionMath.SubunitsPerTile != 0; // off a tile's centre: continuous
        }
        int moved = r.In(() => (int)ai.eyeobj.x) - startX;
        Assert.True(between, "the eye flies between tile centres");
        Assert.InRange(moved, 2, 5); // 0.35 tiles a tick for 10 ticks; tg's AIMove would have stepped 10
        Assert.Same(r.In(() => ai.loc), r.In(() => get_turf(ai))); // the core stays

        // DV424: the meson vision switch: tg's SEE_TURFS off and back on
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(ai.sight, SEE_TURFS))), "tg's AI sees turfs");
        r.Command(() => Waiting.Detach(ai.dm_meson_action.Trigger(ai)));
        r.Step(1);
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(ai.sight, SEE_TURFS))), "the meson vision is off");
        r.Command(() => Waiting.Detach(ai.dm_meson_action.Trigger(ai)));
        r.Step(1);
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(ai.sight, SEE_TURFS))), "the meson vision is back");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAisCtrlClickBoltsAnAirlockItsCamerasSee()
    {
        var (r, _, bob) = Room(9503);
        var door = r.In(() => Dm.@new<obj_machinery_door_airlock>(locate(7, 5, 1)));
        Camera(r, 2, 2);
        var ai = Aiize(r, bob);
        r.In(() => ai.eyeobj.setLoc(locate(6, 5, 1)));
        r.Step(Second);
        Assert.Equal(0f, r.In(() => door.locked));
        r.Command(() => Waiting.Detach(ai.ClickOn(door, ClientSeam.ClickParams("left", ctrl: true))));
        r.Step(Second);
        Assert.Equal(1f, r.In(() => door.locked));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHolopadProjectsTheAisHologramAndItFollowsTheEye()
    {
        var (r, _, bob) = Room(9504);
        var pad = r.In(() => Dm.@new<obj_machinery_holopad>(locate(6, 5, 1)));
        Camera(r, 2, 2);
        var ai = Aiize(r, bob);
        r.In(() => ai.eyeobj.setLoc(locate(6, 5, 1)));
        r.Step(Second);
        r.Command(() => Waiting.Detach(pad.attack_ai_secondary(ai)));
        r.Step(Second);
        var holo = r.In(() => pad.masters?[ai] as obj_effect_overlay_holo_pad_hologram);
        Assert.NotNull(holo);
        r.In(() =>
        {
            Assert.Same(pad, ai.current);
            Assert.Same(locate(6, 5, 1), holo.loc);
            Assert.NotNull(pad.holorays?[ai]);
        });
        // the eye moves: the hologram follows it within the pad's range
        r.In(() => ai.eyeobj.setLoc(locate(8, 5, 1)));
        r.Step();
        r.In(() => Assert.Same(locate(8, 5, 1), holo.loc));
        // a right click again ends the projection
        r.Command(() => Waiting.Detach(pad.attack_ai_secondary(ai)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(holo));
            Assert.Null(ai.current);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHologramGlidesWithTheFlyingEye()
    {
        // DV429 (the owner, 2026-11-14): the hologram stands where the free eye (DV423) is and glides with it, not from tile middle to tile middle
        var (r, _, bob) = Room(9512);
        var pad = r.In(() => Dm.@new<obj_machinery_holopad>(locate(6, 5, 1)));
        Camera(r, 2, 2);
        r.In(() => LocomotionSeam.Install(r.World));
        var ai = Aiize(r, bob);
        r.In(() => LocomotionSeam.Register(ai));
        r.In(() => ai.eyeobj.setLoc(locate(6, 5, 1)));
        r.Step(Second);
        r.Command(() => Waiting.Detach(pad.attack_ai_secondary(ai)));
        r.Step(Second);
        var holo = r.In(() => pad.masters?[ai] as obj_effect_overlay_holo_pad_hologram);
        Assert.NotNull(holo);
        bool glided = false;
        for (uint seq = 1; seq <= 8; seq++)
        {
            r.In(() => LocomotionSeam.SetFrame(ai, new Anomaly.Core.Motion.MotionInput(seq, Anomaly.Core.Motion.MotionInput.MaxMove, 0, 0, Anomaly.Core.Motion.MotionInputFlags.None)));
            r.Step(1);
            r.In(() =>
            {
                Assert.True(LocomotionSeam.TryGetState(World.Current, ai.eyeobj.dm_id, out Anomaly.Core.Motion.MotionState eye, out _));
                Anomaly.Core.Motion.FlightArc arc = holo.dm_flight;
                Assert.False(arc.IsNone, "the hologram is drawn at its eye's point");
                (double x, double y, _) = arc.At(World.Current.Tick);
                Assert.Equal(eye.X / (double)Anomaly.Core.Motion.MotionMath.SubunitsPerTile, x, 3);
                Assert.Equal(eye.Y / (double)Anomaly.Core.Motion.MotionMath.SubunitsPerTile, y, 3);
                Assert.Same(get_turf(ai.eyeobj), holo.loc); // tg's move_hologram still moves it tile by tile
                glided |= arc.IsMoving;
            });
        }
        Assert.True(glided, "the hologram glides while the eye flies");
        Assert.True(r.In(() => holo.x) > 6, "the eye took it east");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHolocallsEyeFliesFreelyAndItsHologramGlidesWithIt()
    {
        // DV429 (the owner, 2026-11-14): a caller's eye at the answering pad flies as the AI's eye does (tg's relaymove stepped it a tile a
        // keypress), and the caller's hologram glides with it
        var (r, _, bob) = Room(9513);
        r.In(() => LocomotionSeam.Install(r.World));
        r.In(() => LocomotionSeam.Register(bob));
        var calling = r.In(() => Dm.@new<obj_machinery_holopad>(locate(3, 5, 1)));
        var answering = r.In(() => Dm.@new<obj_machinery_holopad>(locate(7, 5, 1)));
        r.Step(Second);
        var call = r.In(() => Dm.@new<datum_holocall>(bob, calling, Dm.list(answering)));
        r.Command(() => Waiting.Detach(call.Answer(answering)));
        r.Step(Second);
        var eye = r.In(() => bob.remote_control as mob_eye_camera_remote_holo);
        Assert.NotNull(eye);
        var holo = r.In(() => call.hologram);
        Assert.NotNull(holo);
        int startX = r.In(() => (int)eye.x);
        bool between = false, glided = false;
        for (uint seq = 1; seq <= 10; seq++)
        {
            r.In(() => LocomotionSeam.SetFrame(bob, new Anomaly.Core.Motion.MotionInput(seq, Anomaly.Core.Motion.MotionInput.MaxMove, 0, 0, Anomaly.Core.Motion.MotionInputFlags.None)));
            r.Step(1);
            r.In(() =>
            {
                Assert.True(LocomotionSeam.TryGetState(World.Current, eye.dm_id, out Anomaly.Core.Motion.MotionState s, out _), "the call's eye has a capsule");
                between |= s.X % Anomaly.Core.Motion.MotionMath.SubunitsPerTile != 0;
                (double x, _, _) = holo.dm_flight.At(World.Current.Tick);
                Assert.Equal(s.X / (double)Anomaly.Core.Motion.MotionMath.SubunitsPerTile, x, 3);
                glided |= holo.dm_flight.IsMoving;
            });
        }
        Assert.True(between, "the eye flies between tile centres");
        Assert.True(glided, "the hologram glides with it");
        Assert.InRange(r.In(() => (int)eye.x) - startX, 2, 5);
        r.In(() => Assert.Same(get_turf(eye), holo.loc));
        r.In(() => Assert.Same(locate(3, 5, 1), bob.loc)); // the caller stays at the pad
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnIntellicardCarriesTheAiToTheRestorerWhichMendsIt()
    {
        var (r, _, bob) = Room(9505);
        var card = r.In(() => Dm.@new<obj_item_aicard>(locate(4, 5, 1)));
        var fixer = r.In(() => Dm.@new<obj_machinery_computer_aifixer>(locate(4, 6, 1)));
        mob_living_carbon_human carol = r.Join(4, 4, "Carol");
        var ai = Aiize(r, bob);
        r.In(() => Waiting.NoSleep(carol.put_in_hands(card)));
        // the card takes the AI out of its core
        r.Command(() => Waiting.Detach(card.interact_with_atom(ai, carol)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(ai, card.AI);
            Assert.Same(card, ai.loc);
        });
        // into the restorer, damaged
        r.Command(() => Waiting.Detach(card.interact_with_atom(fixer, carol)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(ai, fixer.occupier);
            Assert.Null(card.AI);
            Waiting.NoSleep(ai.adjust_brute_loss(60));
            Waiting.NoSleep(ai.updatehealth());
        });
        Assert.True(r.In(() => Dm.num(ai.health)) < 50);
        r.In(() => Waiting.NoSleep(fixer.ui_act("PRG_beginReconstruction", Dm.list(), Dm.@new<datum_tgui>(carol, fixer, "AiRestorer"), null)));
        Assert.Equal(1f, r.In(() => fixer.restoring));
        r.Step(Second * 30);
        Assert.Equal(100f, r.In(() => Dm.num(ai.health)), 0.5f);
        Assert.Equal(0f, r.In(() => fixer.restoring));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AVoxWordPlaysOnTheVoxChannelAndWaitsItsTurn()
    {
        var (r, _, bob) = Room(9506);
        r.In(() => Assert.True(Dm.truthy(GLOB.vox_sounds["alert"])));
        r.Command(() => Waiting.NoSleep(play_vox_word("alert", get_turf(bob), bob)));
        r.Step();
        HeardSound word = r.Sounds.Heard.Single(h => ReferenceEquals(h.Target, bob) && h.File != null);
        Assert.EndsWith("alert.ogg", word.File);
        Assert.Equal(CHANNEL_VOX, word.Channel);
        Assert.Equal(1f, word.Wait);
        r.AssertNoRuntimes();
    }

    // silicons SI5: the round-end report's laws (roundend.dm:459)
    [Fact]
    public void TheRoundEndReportListsTheAisLaws()
    {
        var (r, _, bob) = Room(9507);
        var ai = Aiize(r, bob);
        r.In(() => ai.laws.add_inherent_law("You may not injure a human being.")); // the toy room has no law rack to give the round's lawset
        string report = r.In(() => SSticker.law_report());
        Assert.Contains("(Played by: <b>Bob</b>)'s laws at the end of the round were:", report);
        Assert.True(report.Contains("You may not injure a human being", StringComparison.Ordinal), report);
        Assert.Contains("Total law changes: 0", report);
        r.AssertNoRuntimes();
    }
}
