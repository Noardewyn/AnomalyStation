using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>D285: the own held things' poses and motions in first person (Hud/ViewModel).</summary>
public class HeldPoseTests
{
    [Fact]
    public void AThingComesUpFromUnderTheViewAndGoesBackDown()
    {
        Assert.Equal(1f, HeldPose.Raised(0));
        Assert.True(HeldPose.Raised(HeldPose.RaiseSeconds / 2) < 0.2f, "it is mostly up halfway through");
        Assert.Equal(0f, HeldPose.Raised(HeldPose.RaiseSeconds));
        Assert.Equal(0f, HeldPose.Lowered(0));
        Assert.Equal(1f, HeldPose.Lowered(HeldPose.LowerSeconds));
        Assert.Equal(1f, HeldPose.Lowered(5));
    }

    [Fact]
    public void TheLeftHandMirrorsTheRight()
    {
        foreach (HeldKind kind in Enum.GetValues<HeldKind>())
        {
            HeldOffset right = HeldPose.Rest(kind, left: false, wielded: false), left = HeldPose.Rest(kind, left: true, wielded: false);
            Assert.True(right.X > 0f && left.X == -right.X && left.Yaw == -right.Yaw && left.Y == right.Y, kind.ToString());
            Assert.True(right.Z < 0f, "in front of the eye");
        }
        Assert.True(HeldPose.Rest(HeldKind.Long, false, wielded: true).X < HeldPose.Rest(HeldKind.Long, false, wielded: false).X, "a wielded thing is nearer the middle");
    }

    [Fact]
    public void MotionsEndWhereTheyBegan()
    {
        Assert.Equal(default, HeldPose.Swing(0));
        Assert.Equal(default, HeldPose.Swing(HeldPose.SwingSeconds));
        HeldOffset up = HeldPose.Swing(HeldPose.SwingSeconds * HeldPose.SwingWindUp);
        Assert.True(up.Pitch > 20f && up.Y > 0.05f, "the swing first goes up and back");
        HeldOffset chop = HeldPose.Swing(HeldPose.SwingSeconds * HeldPose.SwingChop);
        Assert.True(chop.Pitch < -60f && chop.Y < -0.05f && chop.Z < -0.1f, "then chops down and forward");
        Assert.True(Math.Abs(up.Z) < Math.Abs(chop.Z) / 3, "it goes away from the eye far more than toward it");
        Assert.Equal(default, HeldPose.Reload(HeldPose.ReloadSeconds));
        Assert.True(HeldPose.Reload(HeldPose.ReloadSeconds / 2).Y < -0.05f);
        Assert.Equal(default, HeldPose.Rack(HeldPose.RackSeconds));
        Assert.True(HeldPose.Rack(HeldPose.RackSeconds / 4).Z > 0.05f, "a rack jerks the gun back");
        Assert.True(HeldPose.Kick(0.03).Z > 0.05f, "a kick is back toward the eye");
        Assert.Equal(0f, HeldPose.Kick(1).Z);
        Assert.Equal(1.5f * HeldPose.Kick(0.03).Z, HeldPose.Kick(0.03, new HeldKick(2f, 0f, 1f)).Z, 4); // DV96: as big as the view's kick
        Assert.Equal(HeldPose.Kick(0.03, new HeldKick(8f, 0f, 1f)).Z, HeldPose.Kick(0.03, new HeldKick(15f, 0f, 1f)).Z, 4);
        // a heavy kick is still going out when a light one is at its peak, lasts longer and rolls to its side
        Assert.True(HeldPose.Kick(0.03, new HeldKick(10f, 1f, 1f)).Z < HeldPose.Kick(0.08, new HeldKick(10f, 1f, 1f)).Z);
        Assert.Equal(0f, HeldPose.Kick(0.3, new HeldKick(1f, 0f, 1f)).Z);
        Assert.True(HeldPose.Kick(0.3, new HeldKick(10f, 1f, 1f)).Z > 0f);
        Assert.Equal(-HeldPose.Kick(0.08, new HeldKick(10f, 1f, 1f)).Roll, HeldPose.Kick(0.08, new HeldKick(10f, 1f, -1f)).Roll, 4);
    }

    [Fact]
    public void ATrembleIsSteadyWithinATickAndBoundedByItsSize()
    {
        Assert.Equal(HeldPose.Tremble(1.001, 0.01f, 3), HeldPose.Tremble(1.04, 0.01f, 3));
        Assert.NotEqual(HeldPose.Tremble(1.001, 0.01f, 3), HeldPose.Tremble(1.06, 0.01f, 3));
        for (int i = 0; i < 200; i++)
        {
            HeldOffset t = HeldPose.Tremble(i * 0.05, 0.01f, 7);
            Assert.InRange(t.X, -0.01f, 0.01f);
            Assert.InRange(t.Y, -0.01f, 0.01f);
        }
        Assert.Equal(default, HeldPose.Tremble(3, 0f, 1));
        Assert.Equal(default, HeldPose.Bob(1, 0f));
        Assert.Equal(default, HeldPose.Float(1, 0f));
    }

    [Theory]
    [InlineData("guns/ballistic.dmi", "pistol", "guns/ballistic.dmi", "pistol-e", true)]
    [InlineData("tools.dmi", "welder", "tools.dmi", "welder1", true)]
    [InlineData("tools.dmi", "welder", "tools.dmi", "crowbar", false)]
    [InlineData("a.dmi", "x", "b.dmi", "x", false)]
    [InlineData("a.dmi", null, "a.dmi", "x", false)]
    public void ALookChangeOfTheSameThingIsNotANewThing(string iconA, string stateA, string iconB, string stateB, bool same)
    {
        Assert.Equal(same, HeldHands.SameThing(iconA, stateA, iconB, stateB));
    }

    [Fact]
    public void GunsAreThingsOfTgsGunIcons()
    {
        Assert.True(HeldHands.IsGunIcon("icons/obj/weapons/guns/ballistic.dmi"));
        Assert.False(HeldHands.IsGunIcon("icons/obj/tools.dmi"));
        Assert.False(HeldHands.IsGunIcon(null));
    }

    [Fact]
    public void TheTurnsConfigFindsAThingsTurnAndKeepsWhatItSaves()
    {
        HeldTurns turns = HeldTurns.Parse("{ \"icons/obj/tools.dmi:welder\": { \"yaw\": 180 }, \"bad\": 3 }");
        Assert.Equal(new HeldTurn(180f, 0f, 0f), turns.Find("icons/obj/tools.dmi", "welder"));
        Assert.Equal(new HeldTurn(180f, 0f, 0f), turns.Find("icons/obj/tools.dmi", "welder1")); // the same thing lit
        Assert.True(turns.Find("icons/obj/tools.dmi", "crowbar").IsNone);
        turns.Set("icons/obj/tools.dmi", "crowbar", new HeldTurn(0f, 10f, -90f));
        HeldTurns again = HeldTurns.Parse(turns.ToJson());
        Assert.Equal(new HeldTurn(0f, 10f, -90f), again.Find("icons/obj/tools.dmi", "crowbar"));
        Assert.Equal(1f, again.Find("icons/obj/tools.dmi", "crowbar").Size); // no scale is as drawn
        again.Set("icons/obj/guns/ballistic.dmi", "pistol", new HeldTurn(0f, 0f, 0f, 0.6f));
        Assert.Equal(0.6f, HeldTurns.Parse(again.ToJson()).Find("icons/obj/guns/ballistic.dmi", "pistol-e").Size);
        again.Set("icons/obj/tools.dmi", "wrench", new HeldTurn(0f, 0f, 0f, X: -0.05f, Z: 0.1f));
        Assert.Equal(new HeldTurn(0f, 0f, 0f, X: -0.05f, Z: 0.1f), HeldTurns.Parse(again.ToJson()).Find("icons/obj/tools.dmi", "wrench"));
        again.Set("icons/obj/tools.dmi", "crowbar", default);
        Assert.True(again.Find("icons/obj/tools.dmi", "crowbar").IsNone);
        Assert.Equal(0, HeldTurns.Parse("not json").Count);
        // a look without its own tuning takes the nearest look's, whatever order the config lists them in
        HeldTurns swords = HeldTurns.Parse("{ \"s.dmi:sword\": { \"yaw\": 1 }, \"s.dmi:swordon\": { \"yaw\": 2 } }");
        Assert.Equal(2f, swords.Find("s.dmi", "swordon1").Yaw);
        Assert.Equal(1f, swords.Find("s.dmi", "sword-e").Yaw);
        Assert.True(swords.Find("s.dmi", "").IsNone); // a thing with no state takes nobody's tuning
    }

    [Fact]
    public void TheTurnsConfigKeepsWhatWasSavedForResetAndEveryThingsTuning()
    {
        HeldTurns turns = HeldTurns.Parse("{ \"_all\": { \"fov\": 70, \"scale\": 1.2, \"x\": -0.05 }, \"t.dmi:knife\": { \"scale\": 0.7 } }");
        Assert.Equal(new HeldGlobal(70f, 1.2f, -0.05f), turns.All);
        Assert.Equal(1, turns.Count); // "_all" is no thing's
        Assert.False(turns.Unsaved);
        turns.Set("t.dmi", "knife", new HeldTurn(0f, 0f, 0f, 0.9f));
        turns.Set("t.dmi", "leg", new HeldTurn(10f, 0f, 0f));
        Assert.True(turns.Unsaved);
        Assert.Equal(0.7f, turns.Saved("t.dmi", "knife").Size); // Reset goes back to the file's
        Assert.True(turns.Saved("t.dmi", "leg").IsNone); // and to as drawn for a thing the file lacks
        turns.All = turns.All with { Fov = 80f };
        HeldTurns again = HeldTurns.Parse(turns.ToJson());
        Assert.Equal(80f, again.All.Fov);
        Assert.Equal(0.9f, again.Find("t.dmi", "knife").Size);
        turns.MarkSaved();
        Assert.False(turns.Unsaved);
        Assert.Equal(new HeldGlobal(), HeldTurns.Parse("{}").All);
        Assert.DoesNotContain("_all", HeldTurns.Parse("{}").ToJson());
    }

    [Fact]
    public void TheHandBoxesAndTheHintStripShowTheHandTheViewModelHolds()
    {
        // ui-overhaul.md section 6, slice C: the live HUD reads the active hand off the hand boxes by hand index (LiveHud), the view model off their
        // icon states (HeldHands): through pick-ups and swaps they agree, and the active hand holds an item (the hint strip shows) exactly when the view
        // model holds one there
        var feed = new FakeFeed("Tester", seed: 1, size: 32, bots: 0, inventory: true);
        var rig = new PlayRig(feed);
        rig.Run(0.5);
        Anomaly.Protocol.ReplicaWorld world = feed.Replica;
        FakeServer server = feed.Server;
        Anomaly.Protocol.ScreenEntry Hand(int index) => world.Screen.Values.Single(e => e.HandIndex == index);
        void Agree(bool leftActive, long heldId)
        {
            HeldHandsView view = HeldHands.Find(world);
            Assert.Equal(leftActive, LiveHud.IsActiveHand(world, Hand(1)));
            Assert.Equal(!leftActive, LiveHud.IsActiveHand(world, Hand(2)));
            Assert.Equal(leftActive, view.LeftActive);
            Anomaly.Protocol.ScreenChild[] shown = Hand(leftActive ? 1 : 2).VisChildren?.Items;
            Assert.Equal(heldId, shown?.Single().Id ?? 0);
            Assert.Equal(heldId != 0, !(leftActive ? view.Left : view.Right).IsNone);
        }
        Agree(true, 0);
        void Pick(long id)
        {
            Assert.True(rig.Session.Click(new ClickTarget(Anomaly.Protocol.TargetKind.Entity, id, 0, 0, 0, 16, 16), Anomaly.Protocol.ClickButton.Left, Anomaly.Protocol.ClickModifiers.None));
            rig.Run(0.4);
        }
        Pick(server.ToolboxId);
        Agree(true, server.ToolboxId);
        rig.Session.Keybinding(Anomaly.Protocol.KeybindingCode.SwapHands);
        rig.Run(0.3);
        Agree(false, 0); // the right hand, empty: no hint strip
        Pick(server.WelderId);
        Agree(false, server.WelderId);
        rig.Session.Keybinding(Anomaly.Protocol.KeybindingCode.SwapHands);
        rig.Run(0.3);
        Agree(true, server.ToolboxId);
    }
}
