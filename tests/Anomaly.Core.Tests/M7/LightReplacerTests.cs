using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 power helpers slice: tg's light replacer (game/objects/items/devices/lightreplacer.dm) and the light items it stores
/// (modules/power/lighting/light_items.dm). A glass sheet makes 5 lights and four shards one; a click on a broken, burned out or
/// missing fixture fits a new light (one use, the old one becomes a shard), with tg's click sound; the replacer takes working
/// lights, broken ones (as shards) and the lights, glass and shards of a box; it examines with its count, charges, gets
/// short-circuited by an emag, finishes a floodlight frame and, in its bluespace form, replaces from afar and marks the broken
/// fixtures around it. tg's own unit test (<c>light_replacer.dm</c>) is in <c>Tg/UnitTests/light_replacer.cs</c>.
/// </summary>
public class LightReplacerTests
{
    private const int Second = 20;
    private const int LampX = 5, LampY = 2, Lamp2X = 7;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_light Lamp, obj_machinery_light Lamp2);

    private static Rig Build(ulong seed)
    {
        obj_machinery_light lamp = null, lamp2 = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            lamp = Dm.@new<obj_machinery_light_directional_south>(locate(LampX, LampY, 1));
            lamp2 = Dm.@new<obj_machinery_light_directional_south>(locate(Lamp2X, LampY, 1));
            lamp.allow_break_on_init = FALSE;
            lamp2.allow_break_on_init = FALSE;
        });
        mob_living_carbon_human bob = r.Join(LampX, LampY + 1, "Bob");
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test");
            foreach (obj_machinery_light l in new[] { lamp, lamp2 })
                if (!Dm.eq(l.status, LIGHT_OK))
                    Waiting.NoSleep(l.fix());
        });
        r.Step(Second);
        return new Rig(r, bob, lamp, lamp2);
    }

    private static void Click(AudioRoom r, mob who, atom target, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, Left));
        r.Step(Math.Max(2, (int)(seconds * Second)));
    }

    private static T Spawn<T>(AudioRoom r, int x, int y, float? amount = null) where T : obj_item => r.In(() =>
        amount == null ? Dm.@new<T>(locate(x, y, 1)) : (T)Dm.@new(typeof(T), locate(x, y, 1), amount.Value));

    private static void Break(AudioRoom r, obj_machinery_light lamp) => r.In(() => Waiting.NoSleep(lamp.break_light_tube(TRUE)));

    private static float Uses(AudioRoom r, obj_item_lightreplacer lr) => r.In(() => lr.uses);

    private static List<HeardSound> Heard(AudioRoom r, mob who, string file) => r.Sounds.Of(who, file);

    [Fact]
    public void AGlassSheetMakesFiveLightsUpToTheMaximumAndAFullReplacerRefusesIt()
    {
        var g = Build(7801);
        var lr = WallTests.Hold<obj_item_lightreplacer>(g.R, g.Bob);
        var glass = Spawn<obj_item_stack_sheet_glass>(g.R, LampX, LampY + 1, 3);
        g.R.In(() =>
        {
            Assert.Equal((10f, 20f, 0f), (lr.uses, lr.max_uses, lr.bulb_shards));
            lr.uses = 3;
        });
        // the replacer in the hand clicks the sheets on the floor: glass.dm:53 hands the sheet to the replacer
        Click(g.R, g.Bob, glass);
        Assert.Equal(8f, Uses(g.R, lr));
        Assert.Equal(2f, g.R.In(() => Dm.num(glass.amount)));
        Click(g.R, g.Bob, glass);
        Click(g.R, g.Bob, glass);
        // 3 + 5 + 5 = 13 + 5 = 18: three sheets used, none left
        Assert.Equal(18f, Uses(g.R, lr));
        Assert.True(g.R.In(() => QDELETED(glass)), "the last sheet is used up");
        // 18 + 5 is cut at the maximum of 20, and a full one says "already full!" and keeps the sheet
        var more = Spawn<obj_item_stack_sheet_glass>(g.R, LampX, LampY + 1, 2);
        Click(g.R, g.Bob, more);
        Assert.Equal(20f, Uses(g.R, lr));
        Assert.Equal(1f, g.R.In(() => Dm.num(more.amount)));
        Click(g.R, g.Bob, more);
        Assert.Equal(20f, Uses(g.R, lr));
        Assert.Equal(1f, g.R.In(() => Dm.num(more.amount)));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void FourShardsMakeABulbWithTgsDingAndAPlasmaShardIsTooImpure()
    {
        var g = Build(7802);
        var lr = WallTests.Hold<obj_item_lightreplacer>(g.R, g.Bob);
        g.R.In(() => lr.uses = 4);
        var shards = Enumerable.Range(0, 4).Select(_ => Spawn<obj_item_shard>(g.R, LampX, LampY + 1)).ToList();
        for (int i = 0; i < 3; i++)
        {
            Click(g.R, g.Bob, shards[i]);
            Assert.True(g.R.In(() => QDELETED(shards[i])), "the shard goes into the replacer");
            Assert.Equal(i + 1f, g.R.In(() => lr.bulb_shards));
            Assert.Equal(4f, Uses(g.R, lr));
        }
        Assert.Empty(Heard(g.R, g.Bob, "sound/machines/ding.ogg"));
        Click(g.R, g.Bob, shards[3]);
        g.R.In(() =>
        {
            Assert.Equal(0f, lr.bulb_shards);
            Assert.Equal(5f, lr.uses); // add_shard (:273): four fragments make a bulb
        });
        Assert.NotEmpty(Heard(g.R, g.Bob, "sound/machines/ding.ogg"));
        // a plasma shard is not glass: "too impure!", it stays
        var plasma = Spawn<obj_item_shard_plasma>(g.R, LampX, LampY + 1);
        Click(g.R, g.Bob, plasma);
        g.R.In(() =>
        {
            Assert.False(QDELETED(plasma));
            Assert.Equal(0f, lr.bulb_shards);
            Assert.Equal(5f, lr.uses);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AClickOnABrokenBurnedOrMissingFixtureFitsALightAtOneUseAndAShardEach()
    {
        var g = Build(7803);
        var lr = WallTests.Hold<obj_item_lightreplacer>(g.R, g.Bob);
        // an intact light: "light already installed!", nothing used
        Click(g.R, g.Bob, g.Lamp);
        Assert.Equal(10f, Uses(g.R, lr));
        Assert.Empty(Heard(g.R, g.Bob, "sound/machines/click.ogg"));
        // broken: replaced; the old light is a shard in the replacer, the click is heard, the fixture is lit again
        Break(g.R, g.Lamp);
        g.R.In(() => Assert.True(Dm.eq(g.Lamp.status, LIGHT_BROKEN)));
        Click(g.R, g.Bob, g.Lamp);
        g.R.In(() =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(g.Lamp.status));
            Assert.Equal(9f, lr.uses);
            Assert.Equal(1f, lr.bulb_shards);
            Assert.True(Dm.truthy(g.Lamp.on));
            Assert.Equal("tube", g.Lamp.icon_state);
            Assert.Equal(1f, g.Lamp.switchcount); // the new light's 0, then update() counts the switch on (light.dm:324)
            Assert.Equal(8f, g.Lamp.brightness);
        });
        Assert.Single(Heard(g.R, g.Bob, "sound/machines/click.ogg"));
        // burned out: the same
        g.R.In(() => Waiting.NoSleep(g.Lamp.burn_out()));
        g.R.In(() => Assert.Equal(LIGHT_BURNED, Dm.num(g.Lamp.status)));
        Click(g.R, g.Bob, g.Lamp);
        g.R.In(() =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(g.Lamp.status));
            Assert.Equal((8f, 2f), (lr.uses, lr.bulb_shards));
        });
        // missing: a light goes in, no shard comes out
        g.R.In(() => Waiting.NoSleep(g.Lamp.drop_light_tube()));
        g.R.In(() => Assert.Equal(LIGHT_EMPTY, Dm.num(g.Lamp.status)));
        Click(g.R, g.Bob, g.Lamp);
        g.R.In(() =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(g.Lamp.status));
            Assert.Equal((7f, 2f), (lr.uses, lr.bulb_shards));
        });
        // a bulb fixture takes its own type
        var bulb = g.R.In(() => Dm.@new<obj_machinery_light_small_directional_south>(locate(LampX + 1, LampY, 1)));
        g.R.In(() => bulb.allow_break_on_init = FALSE);
        g.R.In(() => { if (!Dm.eq(bulb.status, LIGHT_OK)) Waiting.NoSleep(bulb.fix()); });
        Break(g.R, bulb);
        g.R.In(() => Waiting.NoSleep(g.Bob.forceMove(locate(LampX + 1, LampY + 1, 1))));
        Click(g.R, g.Bob, bulb);
        g.R.In(() =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(bulb.status));
            Assert.Equal(4f, bulb.brightness); // light_type /obj/item/light/bulb: brightness 4
            Assert.Equal((6f, 3f), (lr.uses, lr.bulb_shards));
        });
        // an empty replacer: "light replacer empty!"
        g.R.In(() => lr.uses = 0);
        Break(g.R, bulb);
        Click(g.R, g.Bob, bulb);
        g.R.In(() =>
        {
            Assert.Equal(LIGHT_BROKEN, Dm.num(bulb.status));
            Assert.Equal(3f, lr.bulb_shards);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void UsedInHandItFixesTheFixturesOnTheUsersTileAndTellsItsCountOtherwise()
    {
        var g = Build(7804);
        var lr = WallTests.Hold<obj_item_lightreplacer>(g.R, g.Bob);
        // a fixture on the player's own tile (a floor lamp's place): attack_self replaces every light there
        var under = g.R.In(() => Dm.@new<obj_machinery_light_directional_south>(g.Bob.loc as turf));
        g.R.In(() => { under.allow_break_on_init = FALSE; if (!Dm.eq(under.status, LIGHT_OK)) Waiting.NoSleep(under.fix()); });
        Break(g.R, under);
        g.R.Command(() => ClientSeam.Keybinding(g.Bob.client, 4, true));
        g.R.Command(() => ClientSeam.Keybinding(g.Bob.client, 4, false));
        g.R.Step(2);
        g.R.In(() =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(under.status));
            Assert.Equal(9f, lr.uses);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ItExaminesWithItsCountChargesAndAnEmagShortCircuitsIt()
    {
        var g = Build(7805);
        var lr = WallTests.Hold<obj_item_lightreplacer>(g.R, g.Bob);
        g.R.In(() =>
        {
            var text = Dm.each(Waiting.NoSleep(lr.examine(g.Bob)) as DmList).Select(Dm.str).ToList();
            Assert.Contains("It has 10 lights remaining (plus 0/4 fragments).", text);
            lr.uses = 1;
            lr.bulb_shards = 1;
            // \s follows the nearest [] (the 4 fragments needed), so the fragments are plural at 1/4 too
            Assert.Equal("It has 1 light remaining (plus 1/4 fragments).", Dm.str(lr.status_string()));
            // Charge (:284): 30000 per light, the rest kept
            lr.uses = 5;
            lr.charge = 1;
            lr.Charge(g.Bob, 70000);
            Assert.Equal((7f, 10001f), (lr.uses, lr.charge));
            lr.Charge(g.Bob, 20000); // 10001 + 20000 = 30001 is over 30000: one more light, 1 left
            Assert.Equal((8f, 1f), (lr.uses, lr.charge));
            lr.Charge(g.Bob, 20000);
            Assert.Equal((8f, 20001f), (lr.uses, lr.charge));
            // the emag short-circuits it: a new name and picture, and the sparks
            Assert.Equal("light replacer", Dm.str(lr.name));
            Assert.Equal("lightreplacer", lr.icon_state);
            Assert.False(Dm.truthy(Waiting.NoSleep(lr.emag_act(g.Bob, null))));
            Assert.Equal("shortcircuited light replacer", Dm.str(lr.name));
            Assert.Equal("lightreplacer-emagged", lr.icon_state);
            Assert.True(Dm.truthy(Dm.bitand(lr.obj_flags, EMAGGED)));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ItTakesWorkingAndBrokenLightsAndEverythingUsableFromABox()
    {
        var g = Build(7806);
        var lr = WallTests.Hold<obj_item_lightreplacer>(g.R, g.Bob);
        g.R.In(() => lr.uses = 2);
        // a working tube on the floor: the light item hands the replacer to itself (light_items.dm:35), one use more
        var tube = Spawn<obj_item_light_tube>(g.R, LampX, LampY + 1);
        g.R.In(() =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(tube.status));
            Assert.Equal("ltube", tube.icon_state);
            Assert.Equal("A replacement light tube.", Dm.str(tube.desc));
        });
        Click(g.R, g.Bob, tube);
        Assert.Equal(3f, Uses(g.R, lr));
        Assert.True(g.R.In(() => QDELETED(tube)));
        // a broken bulb: a shard, no use
        var bulb = Spawn<obj_item_light_bulb_broken>(g.R, LampX, LampY + 1);
        g.R.In(() =>
        {
            Assert.Equal(LIGHT_BROKEN, Dm.num(bulb.status));
            Assert.Equal("lbulb-broken", bulb.icon_state);
            Assert.Equal("A replacement light bulb.", Dm.str(bulb.desc)); // Initialize (:20) updates only the icon state, as tg's
        });
        Click(g.R, g.Bob, bulb);
        g.R.In(() => Assert.Equal((3f, 1f), (lr.uses, lr.bulb_shards)));
        // a box in the other hand: a tube, a broken tube, a glass sheet, a shard and a crowbar; the box on the replacer
        var box = g.R.In(() => Dm.@new<obj_item_storage_box>(locate(LampX, LampY + 1, 1)));
        var goodTube = g.R.In(() => Dm.@new<obj_item_light_tube>(box));
        var brokenTube = g.R.In(() => Dm.@new<obj_item_light_tube_broken>(box));
        var sheet = g.R.In(() => (obj_item_stack_sheet_glass)Dm.@new(typeof(obj_item_stack_sheet_glass), box, 2));
        var shard = g.R.In(() => Dm.@new<obj_item_shard>(box));
        var crowbar = g.R.In(() => Dm.@new<obj_item_crowbar>(box));
        g.R.In(() => g.Bob.swap_hand());
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.put_in_active_hand(box)))));
        Click(g.R, g.Bob, lr);
        g.R.In(() =>
        {
            // 3 + 1 (the tube) + 5 (the first glass sheet of the stack: use(1) makes 5 lights) = 9; the broken tube and the shard
            // are two more fragments: 1 + 2 = 3
            Assert.Equal(9f, lr.uses);
            Assert.Equal(3f, lr.bulb_shards);
            Assert.True(QDELETED(goodTube) && QDELETED(brokenTube) && QDELETED(shard));
            Assert.False(QDELETED(crowbar));
            Assert.Same(box, crowbar.loc);
            Assert.True(QDELETED(sheet)); // tg deletes the whole stack after using one sheet of it (lightreplacer.dm:145)
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AReplacerFinishesAFloodlightFrameForOneUse()
    {
        var g = Build(7807);
        var lr = WallTests.Hold<obj_item_lightreplacer>(g.R, g.Bob);
        var frame = g.R.In(() => Dm.@new<obj_structure_floodlight_frame>(locate(LampX, LampY + 1, 1)));
        g.R.In(() => frame.state = FLOODLIGHT_NEEDS_LIGHTS);
        // a click on the frame does not get to the tool: the frame is an inert type (floodlight.dm is not translated, so the
        // interaction guard stops the click, D132); the replacer's branch is called as the click would
        Assert.True(g.R.In(() => Dm.truthy(Waiting.NoSleep(lr.do_action(frame, g.Bob)))));
        g.R.In(() =>
        {
            Assert.True(QDELETED(frame));
            Assert.Equal(9f, lr.uses);
            Assert.NotNull(Dm.locate<obj_machinery_power_floodlight>(locate(LampX, LampY + 1, 1)));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheBluespaceReplacerFixesFixturesFromAfarAndItsScanMarksTheBrokenOnes()
    {
        var g = Build(7808);
        var lr = WallTests.Hold<obj_item_lightreplacer_blue>(g.R, g.Bob);
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(lr.bluespace_toggle));
            Assert.Equal("lightreplacer_blue", lr.icon_state);
        });
        // from afar: the second fixture, three tiles away
        Break(g.R, g.Lamp2);
        Click(g.R, g.Bob, g.Lamp2);
        g.R.In(() =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(g.Lamp2.status));
            Assert.Equal(9f, lr.uses);
        });
        Assert.NotEmpty(Heard(g.R, g.Bob, "sound/items/pshoom/pshoom.ogg"));
        // the scan (the action button): a firefly on each broken fixture within 7 tiles, none on the sound ones; a second press
        // within the cooldown is refused
        Break(g.R, g.Lamp);
        g.R.In(() => Waiting.NoSleep(lr.ui_action_click(g.Bob, null)));
        g.R.Step(2);
        int Flies() => g.R.In(() => Dm.each<obj_effect_temp_visual_blue_firefly>(Dm.world).Count());
        Assert.Equal(1, Flies());
        g.R.In(() => Assert.Same(g.Lamp.loc, Dm.each<obj_effect_temp_visual_blue_firefly>(Dm.world).First().loc));
        Break(g.R, g.Lamp2);
        g.R.In(() => Waiting.NoSleep(lr.ui_action_click(g.Bob, null)));
        g.R.Step(2);
        Assert.Equal(1, Flies());
        // they live 5 seconds; after the cooldown (6 s) the next scan marks both
        g.R.Step(5 * Second);
        Assert.Equal(0, Flies());
        g.R.Step(2 * Second);
        g.R.In(() => Waiting.NoSleep(lr.ui_action_click(g.Bob, null)));
        g.R.Step(2);
        Assert.Equal(2, Flies());
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AStepOnAWorkingLightShattersItWithTgsGlassSoundAndItsPictureFollows()
    {
        var g = Build(7809);
        var tube = Spawn<obj_item_light_tube>(g.R, LampX, LampY + 1);
        // the player stands on it (spawned under him): Entered shatters a light on a walker
        var other = g.R.Dummy(LampX + 2, LampY + 1);
        g.R.In(() => Waiting.NoSleep(other.forceMove(tube.loc as atom)));
        g.R.Step(2);
        g.R.In(() =>
        {
            Assert.Equal(LIGHT_BROKEN, Dm.num(tube.status));
            Assert.Equal("ltube-broken", tube.icon_state);
            Assert.Equal("A broken light tube.", Dm.str(tube.desc));
            Assert.Equal(5f, tube.force);
        });
        Assert.NotEmpty(Heard(g.R, g.Bob, "sound/effects/footstep/glass_step.ogg"));
        Assert.NotEmpty(Heard(g.R, g.Bob, "sound/effects/glass/glasshit.ogg"));
        // a burnt-out one shows its own picture
        var burnt = Spawn<obj_item_light_bulb>(g.R, LampX + 1, LampY + 1);
        g.R.In(() => { burnt.status = LIGHT_BURNED; burnt.update_appearance(UPDATE_DESC | UPDATE_ICON); });
        g.R.In(() =>
        {
            Assert.Equal("lbulb-burned", burnt.icon_state);
            Assert.Equal("A burnt-out light bulb.", Dm.str(burnt.desc));
        });
        g.R.AssertNoRuntimes();
    }
}
