using System.IO;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Bots;
using Anomaly.Tools.Bots;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10x slice D2 (lobby.md 8) through the real server: a player's hair (tg's braid, its rear plait blended in by getCachedIcon), its
/// outer plait and its briefs reach another session as the overlays tg draws, the hair as the client's iconops recipe; a beret's hair
/// mask is blended into that recipe; and the client's compositor draws the hair (more of the head than a bald one).
/// </summary>
public class HairGameplayTests
{
    private static readonly bool HaveClone = TgStrings.Locate(AppContext.BaseDirectory, Directory.GetCurrentDirectory()) != null;

    internal sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    // The opaque pixels of the client's picture of an entity facing south (-1: no converted icons here).
    private static int Drawn(ReplicaWorld replica, long id)
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return -1;
        var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == id);
        Assert.True(compositor.TryCompose(new Tables(replica), seen.State.Appearance, 2, 0, out ComposedRaster picture));
        int drawn = 0;
        for (int i = 3; i < picture.Image.Pixels.Length; i += 4)
            if (picture.Image.Pixels[i] != 0)
                drawn++;
        return drawn;
    }

    [Fact]
    public void AnotherSessionSeesAPlayersHairItsMaskUnderAHatAndTheBriefs()
    {
        if (!HaveClone)
            return; // the tgstation clone holds tg's name lists (set ANOMALY_TGSTATION or put it at ../tgstation)

        using var rig = new BotServerRig(strict: true);
        Bot alice = rig.AddBot("Alice", BotBehaviour.Idle);
        Bot bob = rig.AddBot("Bob", BotBehaviour.Idle);
        rig.UntilJoined(alice);
        rig.UntilJoined(bob);
        var aliceMob = (mob_living_carbon_human)rig.MobOf("Alice");
        using (rig.World.Enter())
        {
            aliceMob.set_hairstyle("Bald", update: TRUE);
            aliceMob.set_facial_hairstyle("Shaved", update: TRUE);
        }
        rig.Tick(10);
        int bald = Drawn(bob.Replica, aliceMob.dm_id);

        using (rig.World.Enter())
        {
            aliceMob.set_haircolor("#aa5500", update: TRUE);
            aliceMob.set_hairstyle("Braid (Floorlength)", update: TRUE);
            aliceMob.underwear = "Briefs";
            Waiting.NoSleep(aliceMob.update_body(TRUE));
        }
        rig.Tick(10);
        ReplicaEntity seen = bob.Replica.Entities.Single(e => e.Id == aliceMob.dm_id);
        List<(string Icon, string State, float Layer)> icons = CrewOutfitTests.Icons(bob.Replica, seen.State.Appearance);
        string all = string.Join(" ; ", icons.Select(i => $"{i.Icon}:{i.State}@{i.Layer}"));
        Assert.True(icons.Any(i => i.Icon?.StartsWith("iconops:", StringComparison.Ordinal) == true && i.Icon.Contains("hair_braid_a1", StringComparison.Ordinal) && i.Layer == -HAIR_LAYER), all);
        Assert.True(icons.Any(i => i.State == "hair_braid_a1o" && i.Layer == -OUTER_HAIR_LAYER), all);
        Assert.True(icons.Any(i => i.Icon == "icons/mob/clothing/underwear.dmi" && i.State == "male_briefs"), all);
        int haired = Drawn(bob.Replica, aliceMob.dm_id);
        Assert.True(bald < 0 || haired > bald, $"bald {bald} pixels, braided {haired}");

        using (rig.World.Enter())
        {
            var hat = Dm.@new<obj_item_clothing_head_beret>(aliceMob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.equip_to_slot_if_possible(hat, ITEM_SLOT_HEAD))));
        }
        rig.Tick(10);
        seen = bob.Replica.Entities.Single(e => e.Id == aliceMob.dm_id);
        icons = CrewOutfitTests.Icons(bob.Replica, seen.State.Appearance);
        Assert.True(icons.Any(i => i.Icon?.Contains("hair_braid", StringComparison.Ordinal) == true && i.Icon.Contains("hide_above_45deg", StringComparison.Ordinal)),
            string.Join(" ; ", icons.Select(i => $"{i.Icon}:{i.State}@{i.Layer}")));
        Assert.True(Drawn(bob.Replica, aliceMob.dm_id) != 0);

        Assert.Equal(0, rig.World.RuntimeErrorCount);
    }

    // The owner's report (2026-11-02): twintails with a gradient kept their front shape from the side. tg's gradient is a one-dir fade blended
    // with the four-dir hair (head_hair_and_lips.dm:243); the client's recipe takes the hair's dirs, so each dir of the gradient lies on that dir's hair.
    [Fact]
    public void AHairGradientTurnsWithTheHair()
    {
        if (!HaveClone)
            return;
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no converted icons here

        using var rig = new BotServerRig(strict: true);
        Bot alice = rig.AddBot("Alice", BotBehaviour.Idle);
        Bot bob = rig.AddBot("Bob", BotBehaviour.Idle);
        rig.UntilJoined(alice);
        rig.UntilJoined(bob);
        var aliceMob = (mob_living_carbon_human)rig.MobOf("Alice");
        using (rig.World.Enter())
        {
            aliceMob.set_hairstyle("Twintails (Wavy)", update: FALSE);
            aliceMob.set_hair_gradient_style("Short Fade Up", update: FALSE);
            aliceMob.set_hair_gradient_color("#3e3002", update: TRUE);
        }
        rig.Tick(10);
        ReplicaEntity seen = bob.Replica.Entities.Single(e => e.Id == aliceMob.dm_id);
        List<(string Icon, string State, float Layer)> icons = CrewOutfitTests.Icons(bob.Replica, seen.State.Appearance);
        string all = string.Join(" ; ", icons.Select(i => $"{i.Icon}:{i.State}@{i.Layer}"));
        string hair = icons.FirstOrDefault(i => i.Icon?.StartsWith("iconops:icons/mob/human/human_face.dmi|hair_twintails_wavy|", StringComparison.Ordinal) == true).Icon;
        string gradient = icons.FirstOrDefault(i => i.Icon?.StartsWith("iconops:icons/mob/human/species/hair_gradients.dmi|", StringComparison.Ordinal) == true).Icon;
        Assert.True(hair != null && gradient != null, all);

        var library = new IconLibrary(new DirectoryIconFiles(root));
        IIconState hairState = null, gradientState = null;
        Assert.True(library.TryGetIcon(hair, out IIcon hairIcon) && hairIcon.TryGetShown(null, out hairState));
        Assert.True(library.TryGetIcon(gradient, out IIcon gradientIcon) && gradientIcon.TryGetShown(null, out gradientState));
        Assert.Equal(4, gradientState.Dirs);
        for (int d = 0; d < 4; d++)
        {
            byte[] h = hairState.Get(d, 0).Pixels, g = gradientState.Get(d, 0).Pixels;
            for (int i = 3; i < g.Length; i += 4)
                Assert.False(g[i] != 0 && h[i] == 0, $"dir {d}: the gradient is drawn off the hair at pixel {i / 4}");
        }
        Assert.Equal(0, rig.World.RuntimeErrorCount);
    }

    // M10x: tg's blink (_eyes.dm:465-566). A human's two eyelid effects sit in its vis_contents and reach the other sessions as visuals on it,
    // each with tg's looping chain (open, then shut for BLINK_DURATION every few seconds); the client draws a shut eyelid into the picture.
    [Fact]
    public void AnotherSessionSeesAPlayersEyelidsBlinkOnHer()
    {
        if (!HaveClone)
            return; // the tgstation clone holds tg's name lists

        using var rig = new BotServerRig(strict: true);
        Bot alice = rig.AddBot("Alice", BotBehaviour.Idle);
        Bot bob = rig.AddBot("Bob", BotBehaviour.Idle);
        rig.UntilJoined(alice);
        rig.UntilJoined(bob);
        var aliceMob = (mob_living_carbon_human)rig.MobOf("Alice");
        using (rig.World.Enter())
            aliceMob.set_hairstyle("Bald", update: TRUE); // a random hairstyle can cover her eyes, and then a shut eyelid changes no pixel
        rig.Tick(10);

        List<ReplicaEntity> eyelids = bob.Replica.Entities.Where(e => e.IsVisual && e.VisHost == aliceMob.dm_id).ToList();
        Assert.Equal(new[] { "eyes_l", "eyes_r" }, eyelids.Select(e => bob.Replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry a) ? bob.Replica.ResolveString(a.IconState) : null).OrderBy(x => x));
        Assert.All(eyelids, e => Assert.Equal("/obj/effect/abstract/eyelid_effect", e.TypePath));
        foreach (ReplicaEntity eyelid in eyelids)
        {
            AnimationTrack track = Assert.Single(eyelid.Animation.Tracks);
            Assert.Equal(0, track.Loop); // for ever (loop = -1)
            Assert.Contains(track.Steps, step => step.Vars.Mask.HasFlag(AnimatedVar.Alpha) && step.Vars.Alpha == 255f); // shut
            Assert.Contains(track.Steps, step => step.Vars.Mask.HasFlag(AnimatedVar.Alpha) && step.Vars.Alpha == 0f); // open again
        }

        // the client's picture with a shut eyelid differs from the open one
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return;
        var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
        var insets = new VisInsets(new Tables(bob.Replica));
        ReplicaEntity seen = bob.Replica.Entities.Single(e => e.Id == aliceMob.dm_id);
        TableRef shut = insets.With(seen.State.Appearance, eyelids[0].State.Appearance, 0, 0, 255);
        Assert.True(compositor.TryCompose(new Tables(bob.Replica), seen.State.Appearance, 2, 0, out ComposedRaster open));
        Assert.True(compositor.TryCompose(insets, shut, 2, 0, out ComposedRaster closed));
        Assert.NotEqual(open.Image.Pixels, closed.Image.Pixels);
        Assert.Equal(0, rig.World.RuntimeErrorCount);
    }
}
