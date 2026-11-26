using Anomaly.Client.Shared;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Protocol;

namespace Anomaly.Server.Tests.Client.Interaction;

/// <summary>
/// Protocol version 17 (drag and drop, docs/ai/specs/interaction.md section 8): the client's engine-free drag model. A cursor-mode press that
/// never moves past 4 pixels is the click it always was; past them it is a drag, and its release over a thing is a Drop with the press's
/// button and modifiers and the drag's length in deciseconds; let go over nothing it is nothing. The HUD under the pointer wins over the
/// world under it; the dragged icon is a screen entry's, a slot item's or an entity's appearance. The --keys script's drag step.
/// </summary>
public class DragModelTests
{
    [Fact]
    public void APressWithoutAMoveIsTheClickAndAMovePastTheThresholdIsADrop()
    {
        var model = new DragModel();
        var slot = new ClickTarget(TargetKind.Screen, 41, 0, 0, 0);
        var hand = new ClickTarget(TargetKind.Screen, 42, 0, 0, 0, 3, 4);

        Assert.True(model.Press(slot, ClickButton.Left, ClickModifiers.None, 100, 100, 1.0));
        Assert.False(model.Move(103, 101)); // within 4 pixels: still a click
        Assert.Equal(DragOutcome.Click, model.Release(hand, 1.2, out DragRelease click));
        Assert.Equal((slot, slot), (click.Source, click.Over));
        Assert.False(model.Pressed);

        Assert.True(model.Press(slot, ClickButton.Left, ClickModifiers.Ctrl, 100, 100, 2.0));
        Assert.True(model.Move(110, 100)); // the drag starts
        Assert.False(model.Move(150, 120)); // only once
        Assert.True(model.Dragging);
        Assert.Equal(DragOutcome.Drop, model.Release(hand, 2.37, out DragRelease drop));
        Assert.Equal((slot, hand, ClickButton.Left, ClickModifiers.Ctrl, (byte)3), (drop.Source, drop.Over, drop.Button, drop.Modifiers, drop.DragTime));

        // let go over nothing: BYOND's MouseDrop without an over_object, which tg ignores; no press on nothing
        Assert.True(model.Press(slot, ClickButton.Left, ClickModifiers.None, 0, 0, 3.0));
        model.Move(50, 0);
        Assert.Equal(DragOutcome.None, model.Release(ClickTarget.None, 3.5, out _));
        Assert.False(model.Press(ClickTarget.None, ClickButton.Left, ClickModifiers.None, 0, 0, 4.0));
        Assert.Equal(DragOutcome.None, model.Release(hand, 4.1, out _));
    }

    [Fact]
    public void APressOnAThingThatIsNotDraggableStaysItsClickWhereverThePointerGoes()
    {
        var model = new DragModel();
        var intercom = new ClickTarget(TargetKind.Entity, 7, 0, 0, 0);

        Assert.True(model.Press(intercom, ClickButton.Left, ClickModifiers.None, 100, 100, 1.0, draggable: false));
        Assert.False(model.Move(160, 140));
        Assert.False(model.Dragging);
        Assert.Equal(DragOutcome.Click, model.Release(default, 1.2, out DragRelease click));
        Assert.Equal(intercom, click.Source);

        Assert.True(model.Press(intercom, ClickButton.Left, ClickModifiers.None, 100, 100, 2.0));
        Assert.True(model.Move(160, 140)); // the next press is draggable again
    }

    [Fact]
    public void TheDropLandsOnTheHudOverTheWorldAndTheTimeIsDeciseconds()
    {
        var hud = new ClickTarget(TargetKind.Screen, 42, 0, 0, 0);
        var table = new ClickTarget(TargetKind.Entity, 77, 0, 0, 0);
        Assert.Equal(hud, DragModel.ChooseOver(hud, table));
        Assert.Equal(table, DragModel.ChooseOver(ClickTarget.None, table));
        Assert.False(DragModel.ChooseOver(ClickTarget.None, ClickTarget.None).IsSet);
        Assert.Equal((byte)0, DragModel.DragDeciseconds(0.05));
        Assert.Equal((byte)1, DragModel.DragDeciseconds(0.1));
        Assert.Equal((byte)25, DragModel.DragDeciseconds(2.55));
        Assert.Equal((byte)255, DragModel.DragDeciseconds(60));
        Assert.Equal((byte)0, DragModel.DragDeciseconds(-1));
    }

    [Fact]
    public void TheDraggedIconIsTheScreenEntrysTheSlotItemsOrTheEntitys()
    {
        var replica = new ReplicaWorld();
        replica.Apply(Msg.FlatMap(8, 8));
        Snapshot snapshot = Msg.Snapshot(10, 1, 0, new[] { "/mob", "/obj/item" }, Msg.State(1, 0, 3, 3), Msg.State(9, 1, 2, 2) with { Appearance = TableRef.Public(0) });
        snapshot.Resources.Add("icons/obj/storage/backpack.dmi");
        snapshot.Strings.Add("backpack");
        snapshot.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(0), IconState = TableRef.Public(0) });
        replica.Apply(snapshot);
        Assert.Equal(TableRef.Public(0), DragModel.IconOf(replica, new ClickTarget(TargetKind.Entity, 9, 0, 0, 0)));
        Assert.True(DragModel.IconOf(replica, new ClickTarget(TargetKind.Turf, 0, 2, 2, 1)).IsNone);
        Assert.True(DragModel.IconOf(replica, new ClickTarget(TargetKind.Screen, 123, 0, 0, 0)).IsNone);
        Assert.True(DragModel.IconOf(null, new ClickTarget(TargetKind.Entity, 9, 0, 0, 0)).IsNone);
    }

    [Fact]
    public void ADragScriptStepIsAPressAMoveAndARelease()
    {
        Assert.True(InputScript.TryParse("2:TAB+,2.1:TAB-,3:drag=left@100x200>640x700", out List<ScriptedInput> steps, out string error), error);
        ScriptedInput drag = steps.Single(s => s.Action == ScriptAction.Drag);
        Assert.Equal((3.0, "LEFT"), (drag.AtSeconds, drag.Key));
        Assert.Equal((100f, 200f), drag.At);
        Assert.Equal((640f, 700f), drag.To);
        Assert.False(InputScript.TryParse("1:drag=left@100x200", out _, out _));
        Assert.False(InputScript.TryParse("1:drag=left@1x2>3", out _, out _));
        // the map editor's camera (docs/ai/specs/map-editor.md 5.6): the right and the middle button drag too, Alt is held, the wheel turns a notch at a point
        Assert.True(InputScript.TryParse("1:ALT+,1.2:drag=middle@1x2>3x4,1.6:ALT-,2:drag=right@5x6>7x8,3:click=wheelup@9x10,3.1:click=wheeldown@9x10", out steps, out error), error);
        Assert.Equal(new[] { "MIDDLE", "RIGHT" }, steps.Where(s => s.Action == ScriptAction.Drag).Select(s => s.Key));
        Assert.Equal(new[] { "WHEELUP", "WHEELDOWN" }, steps.Where(s => s.Action == ScriptAction.Click).Select(s => s.Key));
        Assert.Equal((9f, 10f), steps.First(s => s.Action == ScriptAction.Click).At);
        Assert.False(InputScript.TryParse("1:drag=wheelup@1x2>3x4", out _, out _));
        Assert.False(InputScript.TryParse("1:click=side@1x2", out _, out _));
    }

    [Fact]
    public void ADropOnTheHealthDollIsADropOnTheOwnMob()
    {
        // the owner (2026-10-15): the HUD's health doll stands for the own body as a drop target
        var doll = new ClickTarget(TargetKind.Screen, 40, 0, 0, 0, 10, 10);
        var slot = new ClickTarget(TargetKind.Screen, 41, 0, 0, 0, 10, 10);
        var world = new ClickTarget(TargetKind.Entity, 7, 0, 0, 0, 3, 3);
        bool IsDoll(long id) => id == 40;
        Assert.Equal(new ClickTarget(TargetKind.Entity, 99, 0, 0, 0, 16, 16), DragModel.SelfOnDoll(doll, IsDoll, 99));
        Assert.Equal(slot, DragModel.SelfOnDoll(slot, IsDoll, 99));
        Assert.Equal(world, DragModel.SelfOnDoll(world, IsDoll, 99));
        Assert.Equal(doll, DragModel.SelfOnDoll(doll, IsDoll, 0)); // no own mob yet
    }

    [Fact]
    public void ADragFromTheHealthDollOntoTheWorldDragsTheOwnMob()
    {
        // DV417 (gaps L1): the own body is never under the pointer; a drag from the doll onto a world thing is tg's drag of oneself
        var doll = new ClickTarget(TargetKind.Screen, 40, 0, 0, 0, 10, 10);
        var slot = new ClickTarget(TargetKind.Screen, 41, 0, 0, 0, 10, 10);
        var wall = new ClickTarget(TargetKind.Turf, 0, 4, 5, 1);
        bool IsDoll(long id) => id == 40;
        DragRelease onWall = DragModel.SelfFromDoll(new DragRelease(doll, wall, ClickButton.Left, ClickModifiers.None, 3), IsDoll, 99);
        Assert.Equal(new ClickTarget(TargetKind.Entity, 99, 0, 0, 0, 16, 16), onWall.Source);
        Assert.Equal(wall, onWall.Over);
        var onHud = new DragRelease(doll, slot, ClickButton.Left, ClickModifiers.None, 3);
        Assert.Equal(onHud, DragModel.SelfFromDoll(onHud, IsDoll, 99)); // a drop back on the HUD keeps the doll
        var fromSlot = new DragRelease(slot, wall, ClickButton.Left, ClickModifiers.None, 3);
        Assert.Equal(fromSlot, DragModel.SelfFromDoll(fromSlot, IsDoll, 99));
    }

    [Fact]
    public void ACtrlClickOnTheHealthDollIsACtrlClickOnTheOwnMob()
    {
        // DV72: tg's ctrl-click on oneself (the self grasp) through the doll; slice T5c: aimed at the doll's part clicked (DV30), so that limb is
        // grasped; off the doll's parts no zone: the zone selector's
        var doll = new ClickTarget(TargetKind.Screen, 40, 0, 0, 0, 10, 10);
        var slot = new ClickTarget(TargetKind.Screen, 41, 0, 0, 0, 10, 10);
        bool IsDoll(long id) => id == 40;
        Assert.Equal(new ClickTarget(TargetKind.Entity, 99, 0, 0, 0, 0, 0, AimZone.LeftArm), DragModel.SelfCtrlClickOnDoll(doll, ClickModifiers.Ctrl, IsDoll, 99, AimZone.LeftArm));
        Assert.Equal(new ClickTarget(TargetKind.Entity, 99, 0, 0, 0, 0, 0, AimZone.RightLeg), DragModel.SelfCtrlClickOnDoll(doll, ClickModifiers.Ctrl | ClickModifiers.Shift, IsDoll, 99, AimZone.RightLeg));
        Assert.Equal(new ClickTarget(TargetKind.Entity, 99, 0, 0, 0), DragModel.SelfCtrlClickOnDoll(doll, ClickModifiers.Ctrl, IsDoll, 99, AimZone.None));
        Assert.Equal(doll, DragModel.SelfCtrlClickOnDoll(doll, ClickModifiers.None, IsDoll, 99, AimZone.LeftArm)); // a plain click is tg's self-check
        Assert.Equal(doll, DragModel.SelfCtrlClickOnDoll(doll, ClickModifiers.Alt, IsDoll, 99, AimZone.LeftArm));
        Assert.Equal(slot, DragModel.SelfCtrlClickOnDoll(slot, ClickModifiers.Ctrl, IsDoll, 99, AimZone.LeftArm));
        Assert.Equal(doll, DragModel.SelfCtrlClickOnDoll(doll, ClickModifiers.Ctrl, IsDoll, 0, AimZone.LeftArm)); // no own mob yet
    }

    [Fact]
    public void TheHealthDollsHeadHoldsTgsEyesAndMouthPixelByPixel()
    {
        // Slice T5c: tg's zone selector's get_zone_at on the 32 x 32 doll: eyes rows 25 to 27 columns 15 to 17, plus 14 and 18 on row 26; mouth
        // rows 23 and 24 columns 15 to 17; the rest of the head is the head
        var eyes = new HashSet<(int, int)> { (14, 26), (18, 26) };
        var mouth = new HashSet<(int, int)>();
        for (int x = 15; x <= 17; x++)
        {
            for (int y = 25; y <= 27; y++)
                eyes.Add((x, y));
            mouth.Add((x, 23));
            mouth.Add((x, 24));
        }
        for (int x = 1; x <= 32; x++)
            for (int y = 1; y <= 32; y++)
            {
                Assert.Equal(eyes.Contains((x, y)) ? AimZone.Eyes : mouth.Contains((x, y)) ? AimZone.Mouth : AimZone.Head, BodyZoneAim.DollFaceAt(AimZone.Head, x, y));
                // only the head is refined: any other limb (or none) under those pixels stays itself
                Assert.Equal(AimZone.Chest, BodyZoneAim.DollFaceAt(AimZone.Chest, x, y));
                Assert.Equal(AimZone.None, BodyZoneAim.DollFaceAt(AimZone.None, x, y));
            }
        Assert.Equal(11, eyes.Count);
        Assert.Equal(6, mouth.Count);
    }

    [Fact]
    public void AnItemOnTheDollsEyesOrMouthUsesThemAndACtrlClickThereGraspsTheHead()
    {
        // Slice T5c: an item click on the doll carries the eyes or the mouth; the ctrl-click grasp carries the limb (the head), since tg's
        // get_bodypart has no eyes or mouth to grasp
        var doll = new ClickTarget(TargetKind.Screen, 40, 0, 0, 0, 16, 26);
        bool IsDoll(long id) => id == 40;
        Assert.Equal(new ClickTarget(TargetKind.Entity, 99, 0, 0, 0, 0, 0, AimZone.Eyes), DragModel.SelfItemClickOnDoll(doll, ClickModifiers.None, true, IsDoll, 99, BodyZoneAim.DollFaceAt(AimZone.Head, 16, 26)));
        Assert.Equal(new ClickTarget(TargetKind.Entity, 99, 0, 0, 0, 0, 0, AimZone.Mouth), DragModel.SelfItemClickOnDoll(doll, ClickModifiers.None, true, IsDoll, 99, BodyZoneAim.DollFaceAt(AimZone.Head, 16, 23)));
        Assert.Equal(new ClickTarget(TargetKind.Entity, 99, 0, 0, 0, 0, 0, AimZone.Head), DragModel.SelfItemClickOnDoll(doll, ClickModifiers.None, true, IsDoll, 99, BodyZoneAim.DollFaceAt(AimZone.Head, 13, 28)));
        Assert.Equal(new ClickTarget(TargetKind.Entity, 99, 0, 0, 0, 0, 0, AimZone.Head), DragModel.SelfCtrlClickOnDoll(doll, ClickModifiers.Ctrl, IsDoll, 99, AimZone.Head));
    }

    [Fact]
    public void AClickOnADollLimbWithAnItemInHandIsAClickOnTheOwnMobAimedAtThatLimb()
    {
        // D183: first aid on oneself in first person: a plain click with an item in the active hand on the doll's limb is the own mob's click
        // aimed at that limb (the server selects it, DV30); an empty hand keeps tg's self-check, a modifier key and a click off a limb keep the click
        var doll = new ClickTarget(TargetKind.Screen, 40, 0, 0, 0, 16, 20);
        var slot = new ClickTarget(TargetKind.Screen, 41, 0, 0, 0, 16, 20);
        bool IsDoll(long id) => id == 40;
        Assert.Equal(new ClickTarget(TargetKind.Entity, 99, 0, 0, 0, 0, 0, AimZone.Chest), DragModel.SelfItemClickOnDoll(doll, ClickModifiers.None, true, IsDoll, 99, AimZone.Chest));
        Assert.Equal(new ClickTarget(TargetKind.Entity, 99, 0, 0, 0, 0, 0, AimZone.LeftArm), DragModel.SelfItemClickOnDoll(doll, ClickModifiers.None, true, IsDoll, 99, AimZone.LeftArm));
        Assert.Equal(doll, DragModel.SelfItemClickOnDoll(doll, ClickModifiers.None, false, IsDoll, 99, AimZone.Chest)); // empty hand: tg's self-check
        Assert.Equal(doll, DragModel.SelfItemClickOnDoll(doll, ClickModifiers.Shift, true, IsDoll, 99, AimZone.Chest)); // examine
        Assert.Equal(doll, DragModel.SelfItemClickOnDoll(doll, ClickModifiers.None, true, IsDoll, 99, AimZone.None)); // off the limbs
        Assert.Equal(slot, DragModel.SelfItemClickOnDoll(slot, ClickModifiers.None, true, IsDoll, 99, AimZone.Chest));
        Assert.Equal(doll, DragModel.SelfItemClickOnDoll(doll, ClickModifiers.None, true, IsDoll, 0, AimZone.Chest)); // no own mob yet
    }

    [Theory]
    [InlineData("head0", AimZone.Head)]
    [InlineData("chest3", AimZone.Chest)]
    [InlineData("l_arm6", AimZone.LeftArm)]
    [InlineData("r_arm7", AimZone.RightArm)]
    [InlineData("l_legDEAD", AimZone.LeftLeg)]
    [InlineData("r_leg5", AimZone.RightLeg)]
    [InlineData("healthdoll_OVERLAY", AimZone.None)]
    [InlineData("human_l_arm", AimZone.None)]
    public void TheDollsLimbStatesNameTheirZones(string state, AimZone zone) => Assert.Equal(zone, BodyZoneAim.DollZoneOfState(state));

    [Fact]
    public void ACursorModeClickOnTheWorldIsSentOnlyWithCtrlForAPull()
    {
        // pulling (2026-10-15): in cursor mode a HUD entry's click is sent as before; a click on a world thing or cell under the pointer
        // only with Ctrl held (tg's ctrl-click pulls it), never a plain one
        var model = new DragModel();
        var crate = new ClickTarget(TargetKind.Entity, 7, 0, 0, 0, 12, 20);
        var slot = new ClickTarget(TargetKind.Screen, 41, 0, 0, 0);
        DragRelease Click(ClickTarget source, ClickModifiers modifiers)
        {
            Assert.True(model.Press(source, ClickButton.Left, modifiers, 100, 100, 1.0));
            Assert.Equal(DragOutcome.Click, model.Release(source, 1.1, out DragRelease release));
            return release;
        }

        Assert.True(DragModel.ClickIsSent(Click(crate, ClickModifiers.Ctrl)));
        Assert.True(DragModel.ClickIsSent(Click(crate, ClickModifiers.Ctrl | ClickModifiers.Shift)));
        Assert.False(DragModel.ClickIsSent(Click(crate, ClickModifiers.None)));
        Assert.True(DragModel.ClickIsSent(Click(crate, ClickModifiers.None), worldClicks: true)); // through a camera eye (the AI's)
        Assert.False(DragModel.ClickIsSent(Click(crate, ClickModifiers.Alt)));
        Assert.True(DragModel.ClickIsSent(Click(slot, ClickModifiers.None)));
        Assert.True(DragModel.ClickIsSent(Click(new ClickTarget(TargetKind.Turf, 0, 5, 6, 1, 16, 16), ClickModifiers.Ctrl)));
    }
}
