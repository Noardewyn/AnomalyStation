using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.Render;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The M7 husks slice through the real server with a headless client: the player's body is burnt to a husk (become_husk(BURN),
/// status_procs.dm:575) and the client receives tg's husk look (_bodyparts.dm:1486 huskify_image): the chest drawn from the
/// greyscale limb file toned by ColorTone(HUSK_COLOR_TONE), sent as its iconops recipe, with the husk blood overlay
/// "humanoid_husk_chest" (BLEND_INSET_OVERLAY) beside it. With the converted icons the client draws the husk, and it looks
/// different from the living body. No new runtime.
/// </summary>
public class HuskGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string Greyscale = "icons/mob/human/bodyparts_greyscale.dmi";

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    // Every appearance of the tree under a reference: its icon resource, its state and its blend mode.
    private static List<(string Icon, string State, int Blend)> Looks(ReplicaWorld replica, TableRef reference, int depth = 0)
    {
        var found = new List<(string, string, int)>();
        if (depth > 8 || !replica.TryGetAppearance(reference, out AppearanceEntry entry))
            return found;
        found.Add((replica.ResolveResource(entry.Icon), replica.ResolveString(entry.IconState), entry.BlendMode));
        foreach (TableRef child in entry.Overlays.Concat(entry.Underlays))
            found.AddRange(Looks(replica, child, depth + 1));
        return found;
    }

    private static byte[] Picture(AppearanceCompositor compositor, Tables tables, ReplicaEntity seen)
    {
        Assert.True(compositor.TryCompose(tables, seen.State.Appearance, ByondDir.South, 0, out ComposedRaster picture), string.Join("; ", compositor.Library.Problems));
        return picture.Image.Pixels.ToArray();
    }

    [Fact]
    public void A_player_burnt_to_a_husk_is_seen_as_tgs_husk()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 83, TimeoutSteps = 4000 }, strict: false);
        RealHeadlessWorld.Player p = w.Join("Ash");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        Assert.True(replica.TryGet(me.dm_id, out ReplicaEntity alive));
        // the random character's body (tg's chest state has the sex in it: _bodyparts.dm limb_gender)
        string chestState;
        using (w.World.Enter())
            chestState = "human_chest_" + Dm.str(((obj_item_bodypart)me.get_bodypart(Defines.BODY_ZONE_CHEST)).limb_gender);
        Assert.Contains(Looks(replica, alive.State.Appearance), l => l.Icon == Greyscale && l.State == chestState); // the living chest: its file

        string iconRoot = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        var compositor = iconRoot == null ? null : new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(iconRoot)));
        var tables = new Tables(replica);
        byte[] before = compositor == null ? null : Picture(compositor, tables, alive);

        using (w.World.Enter())
            me.become_husk(BURN);
        w.Run(Second);

        Assert.True(replica.TryGet(me.dm_id, out ReplicaEntity husk));
        List<(string Icon, string State, int Blend)> looks = Looks(replica, husk.State.Appearance);
        output.WriteLine(string.Join("\n", looks.Select(l => $"{l.Icon}:{l.State} blend {l.Blend}")));
        // the chest: new /icon(file) of the greyscale file, GrayScale's MapColors first and the husk tone multiplied in
        (string Icon, string State, int Blend) chest = Assert.Single(looks, l => l.State == chestState);
        Assert.StartsWith("iconops:" + Greyscale + "||0|101,1,1,m,0.3,0.3,0.3,", chest.Icon);
        Assert.Contains("|2,1,1,c," + HUSK_COLOR_TONE + "|", chest.Icon);
        Assert.DoesNotContain(looks, l => l.Icon == null && l.State != ""); // every recipe fits a resource (Limits.MaxResourceBytes)
        // its husk blood, inset into the body drawn below it
        Assert.Contains(looks, l => l.Icon == "icons/mob/human/bodyparts.dmi" && l.State == "humanoid_husk_chest" && l.Blend == (int)Dm.BLEND_INSET_OVERLAY);
        // a leg's blood is cut by the leg masks with the leg (handle_masking's new(limb_overlay) keeps its blend mode)
        Assert.Equal(2, looks.Count(l => l.State == "humanoid_husk_l_leg" && l.Blend == (int)Dm.BLEND_INSET_OVERLAY));

        if (compositor == null)
            output.WriteLine("no converted icons (tools/Anomaly.Tools.Icons): the husk is not drawn");
        else
        {
            Assert.True(compositor.TryCompose(tables, husk.State.Appearance, ByondDir.South, 0, out ComposedRaster drawn), string.Join("; ", compositor.Library.Problems));
            Assert.NotEqual(before, drawn.Image.Pixels); // the client draws the recipe (a missing file would fail TryCompose)
            if (Environment.GetEnvironmentVariable("ANOMALY_HUSK_PICTURE") is { } path)
                File.WriteAllBytes(path, PngEncoder.Encode(drawn.Image, 6, 8, 0, false));
        }
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
