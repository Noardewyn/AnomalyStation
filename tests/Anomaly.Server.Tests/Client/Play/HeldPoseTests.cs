using Anomaly.Client.Shared;
using System.IO;
using System.Numerics;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Models;

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

    // the clips keyframed in Blender (held_swing.glb): short enough for tg's melee click cooldown, at rest at their ends, the blow landing 0.12 to
    // 0.2 s in (the hit is dealt on the click) after a short wind-up; the one-handed chop comes down in front, the wielded one moves the hands
    // across, both with the thing turned well over; a stab drives them forward; the left hand's is the mirror image
    [Theory]
    [InlineData(HeldClip.MeleeSwing, 0.17)]
    [InlineData(HeldClip.WieldChop, 0.2)]
    [InlineData(HeldClip.Stab, 0.15)]
    [InlineData(HeldClip.WieldStab, 0.17)]
    public void AClipWindsUpStrikesSoonAfterTheClickAndComesBackToRest(string name, double blow)
    {
        HeldClip clip = HeldClip.Of(GlbFile.Read(File.ReadAllBytes(ModelsFile(HeldClip.File))), name);
        Assert.InRange(clip.Length, 0.4f, 0.8f);
        Assert.Equal((default(HeldOffset), Quaternion.Identity), clip.At(0, left: false));
        Assert.Equal((default(HeldOffset), Quaternion.Identity), clip.At(clip.Length, left: false));
        bool stab = name is HeldClip.Stab or HeldClip.WieldStab;
        (HeldOffset move, Quaternion turn) = clip.At(blow, left: false);
        if (stab)
            Assert.True(move.Z < -0.1f, $"the hands drive forward: {move}");
        else if (name == HeldClip.MeleeSwing)
            Assert.True(move.Z < -0.1f && Math.Abs(move.X) < 0.08f, $"the hand comes down in front, not across: {move}");
        else
            Assert.True(move.X < -0.1f, $"the hands move across to the left: {move}");
        Assert.True(2 * MathF.Acos(Math.Min(1f, Math.Abs(turn.W))) > MathF.PI / 6, $"the thing is turned at the blow: {turn}");
        HeldOffset windUp = clip.At(0.06, left: false).Move;
        Assert.True(windUp.Z > 0f, $"it winds up first: the hands draw back: {windUp}");
        (HeldOffset leftMove, Quaternion leftTurn) = clip.At(blow, left: true);
        Assert.Equal(-move.X, leftMove.X, 4);
        Assert.Equal(move.Y, leftMove.Y, 4);
        Assert.Equal(move.Z, leftMove.Z, 4);
        Assert.Equal(-Vector3.Transform(Vector3.UnitY, turn).X, Vector3.Transform(Vector3.UnitY, leftTurn).X, 4);
    }

    [Fact]
    public void APointedThingStabsAndAWieldedThingStrikesTwoHanded()
    {
        Assert.Equal(HeldClip.MeleeSwing, HeldClip.For(pointy: false, wielded: false));
        Assert.Equal(HeldClip.WieldChop, HeldClip.For(pointy: false, wielded: true));
        Assert.Equal(HeldClip.Stab, HeldClip.For(pointy: true, wielded: false));
        Assert.Equal(HeldClip.WieldChop, HeldClip.For(pointy: true, wielded: true)); // a wielded spear swings like an axe
    }

    // held_items.json's attack clips: a thing's own over the default (every thing's; every wielded melee thing's while wielded) over tg's rule,
    // kept through a save and a load; an unknown name is none
    [Fact]
    public void AThingsAttackClipOverridesTheDefaultWhichOverridesTgsRule()
    {
        HeldTurns turns = HeldTurns.Parse("""
            { "_all": { "attack": "stab" }, "_wield": { "attack": "wield_stab" },
              "icons/a.dmi:knife": { "attack": "melee_swing", "wield_attack": "wield_chop" }, "icons/a.dmi:pen": { "attack": "kick" } }
            """);
        Assert.Equal(HeldClip.MeleeSwing, turns.ClipOf("icons/a.dmi", "knife", wielded: false, pointy: true));
        Assert.Equal(HeldClip.WieldChop, turns.ClipOf("icons/a.dmi", "knife", wielded: true, pointy: true));
        Assert.Equal(HeldClip.Stab, turns.ClipOf("icons/a.dmi", "pen", wielded: false, pointy: false));
        Assert.Equal(HeldClip.WieldStab, turns.ClipOf("icons/a.dmi", "axe", wielded: true, pointy: false));
        Assert.Equal(HeldClip.Stab, HeldTurns.Parse("{}").ClipOf("icons/a.dmi", "pen", wielded: false, pointy: true));
        Assert.Equal(HeldClip.WieldChop, HeldTurns.Parse("{}").ClipOf("icons/a.dmi", "axe", wielded: true, pointy: false));
        HeldTurns again = HeldTurns.Parse(turns.ToJson());
        Assert.Equal(turns.All, again.All);
        Assert.Equal(turns.WieldAttack, again.WieldAttack);
        Assert.Equal(turns.Find("icons/a.dmi", "knife"), again.Find("icons/a.dmi", "knife"));
        Assert.False(turns.Unsaved);
        turns.WieldAttack = null;
        Assert.True(turns.Unsaved);
    }

    private static string ModelsFile(string relative)
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "godot", "assets", "models", relative);
            if (File.Exists(candidate))
                return candidate;
        }
        throw new InvalidOperationException($"godot/assets/models/{relative} not found above the test's folder");
    }

    // held_items.json's _wield and _wield_gun: the defaults until set, kept through a save and a load, and Unsaved while changed
    [Fact]
    public void TheWieldMovesAreTunedAndSaved()
    {
        HeldTurns turns = HeldTurns.Parse("{}");
        Assert.Equal(HeldPose.WieldMelee, turns.WieldOf(HeldKind.Long));
        Assert.Equal(HeldPose.WieldGun, turns.WieldOf(HeldKind.Gun));
        Assert.DoesNotContain(HeldTurns.WieldKey, turns.ToJson());
        turns.Wield = new HeldOffset(-0.25f, 0.05f, 0.01f, Pitch: 5f, Yaw: 10f, Roll: -20f);
        Assert.True(turns.Unsaved);
        HeldTurns loaded = HeldTurns.Parse(turns.ToJson());
        Assert.Equal(turns.Wield, loaded.Wield);
        Assert.Equal(HeldPose.WieldGun, loaded.WieldGun);
        Assert.False(loaded.Unsaved);
        Assert.Equal(HeldPose.Rest(HeldKind.Gun, true, wielded: true), HeldPose.Rest(HeldKind.Gun, true, HeldPose.WieldGun));
    }

    // a thing's own flips and wielded move: kept through a save and a load; its wielded move replaces the common one only for it
    [Fact]
    public void AThingsFlipsAndWieldedMoveAreSaved()
    {
        HeldTurns turns = HeldTurns.Parse("{}");
        var move = new HeldOffset(-0.1f, 0.2f, 0f, Pitch: 40f);
        turns.Set("icons/obj/weapons/fireaxe.dmi", "fireaxe1", new HeldTurn(0f, 0f, 0f, FlipY: true, Wield: move));
        HeldTurns loaded = HeldTurns.Parse(turns.ToJson());
        HeldTurn t = loaded.Find("icons/obj/weapons/fireaxe.dmi", "fireaxe1");
        Assert.True(t.FlipY);
        Assert.False(t.FlipX);
        Assert.Equal(move, t.Wield);
        Assert.Equal(move, loaded.WieldOf("icons/obj/weapons/fireaxe.dmi", "fireaxe1", HeldKind.Long));
        Assert.Equal(loaded.Wield, loaded.WieldOf("icons/obj/weapons/spear.dmi", "spearglass1", HeldKind.Long));
        Assert.True(new HeldTurn(0f, 0f, 0f).IsNone);
        Assert.False(new HeldTurn(0f, 0f, 0f, FlipX: true).IsNone);
    }

    // the flips of every held thing, of the common wielded poses and of a thing's own, saved and added up: the same flip twice undoes it
    [Fact]
    public void FlipsAddUpAndAreSaved()
    {
        const string axe = "icons/obj/weapons/fireaxe.dmi", gun = "icons/obj/weapons/guns/ballistic.dmi";
        HeldTurns turns = HeldTurns.Parse("{}");
        Assert.Equal(default, turns.FlipOf(axe, "fireaxe1", HeldKind.Long, wielded: true));
        turns.All = turns.All with { Flip = new HeldFlip(X: true) };
        turns.WieldFlip = new HeldFlip(Y: true);
        turns.WieldGunFlip = new HeldFlip(X: true);
        turns.Set(axe, "fireaxe0", new HeldTurn(0f, 0f, 0f, FlipX: true));
        turns.Set(axe, "spear1", new HeldTurn(0f, 0f, 0f, Wield: default(HeldOffset), WieldFlip: new HeldFlip(true, true)));
        Assert.True(turns.Unsaved);
        HeldTurns loaded = HeldTurns.Parse(turns.ToJson());
        Assert.False(loaded.Unsaved);
        Assert.Equal(new HeldFlip(X: true), loaded.FlipOf(axe, "fireaxe1", HeldKind.Long, wielded: false)); // all's alone
        Assert.Equal(new HeldFlip(true, true), loaded.FlipOf(axe, "fireaxe1", HeldKind.Long, wielded: true)); // all's and wielded melee's
        Assert.Equal(new HeldFlip(), loaded.FlipOf(axe, "fireaxe0", HeldKind.Long, wielded: false)); // its own undoes all's
        Assert.Equal(new HeldFlip(), loaded.FlipOf(gun, "pistol", HeldKind.Gun, wielded: true)); // all's and wielded gun's
        Assert.Equal(new HeldFlip(Y: true), loaded.FlipOf(axe, "spear1", HeldKind.Long, wielded: true)); // its own wielded flip, not the common one
    }

    [Fact]
    public void AFlippedPictureIsMirrored()
    {
        var picture = new Raster(2, 2, new byte[] { 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4 });
        Assert.Equal(new byte[] { 2, 2, 2, 2, 1, 1, 1, 1, 4, 4, 4, 4, 3, 3, 3, 3 }, picture.Flipped(true, false).Pixels);
        Assert.Equal(new byte[] { 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2 }, picture.Flipped(false, true).Pixels);
        Assert.Equal(new byte[] { 4, 4, 4, 4, 3, 3, 3, 3, 2, 2, 2, 2, 1, 1, 1, 1 }, picture.Flipped(true, true).Pixels);
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
}
