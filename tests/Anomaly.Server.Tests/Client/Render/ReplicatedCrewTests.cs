using System.IO;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Bots;
using Anomaly.Tools.Bots;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>
/// M2 F1 on the client side: the client's compositor (P7) draws a replicated crew member from the real server, jumpsuit
/// included: the GAGS worn icon the server names (<c>gags:/datum/greyscale_config/jumpsuit/worn|#rrggbb</c>) is composed
/// from tg's config and the jumpsuit's colour, on top of the body.
/// </summary>
public class ReplicatedCrewTests
{
    private readonly ITestOutputHelper _output;

    public ReplicatedCrewTests(ITestOutputHelper output) => _output = output;

    // The compositor's view of a bot's replica (what godot/scripts/Render/ReplicaTables.cs gives the Godot client).
    private sealed class Tables : IAppearanceSource
    {
        private readonly ReplicaWorld _replica;
        public Tables(ReplicaWorld replica) => _replica = replica;
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => _replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => _replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => _replica.ResolveResource(reference);
    }

    private static (byte R, byte G, byte B) Rgb(string hex)
        => (Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));

    // Opaque pixels whose colour is the jumpsuit's colour times a grey shade (GAGS multiplies the colour into a greyscale
    // sprite): each channel's ratio to the colour's is about the same.
    private static int ShadedPixelsOf(Raster picture, string hex)
    {
        (byte r, byte g, byte b) = Rgb(hex);
        int count = 0;
        for (int i = 0; i < picture.Width * picture.Height; i++)
        {
            byte pr = picture.Pixels[i * 4], pg = picture.Pixels[i * 4 + 1], pb = picture.Pixels[i * 4 + 2], pa = picture.Pixels[i * 4 + 3];
            if (pa < 200)
                continue;
            double max = Math.Max(r, Math.Max(g, b));
            double shade = Math.Max(pr, Math.Max(pg, pb)) / max;
            if (shade < 0.25 || shade > 1.05)
                continue;
            if (Math.Abs(pr - r * shade) <= 12 && Math.Abs(pg - g * shade) <= 12 && Math.Abs(pb - b * shade) <= 12)
                count++;
        }
        return count;
    }

    [RealIconsFact]
    public void TheCompositorDrawsAReplicatedCrewMembersJumpsuitInItsColour()
    {
        if (TgStrings.Locate(AppContext.BaseDirectory, Directory.GetCurrentDirectory()) == null)
            return; // names and the server's tg strings need the clone; the icons test already needs the converted icons

        using var rig = new BotServerRig(strict: true);
        var bots = new List<Bot>();
        foreach (string name in new[] { "Alice", "Bob", "Carol", "Dave" })
            bots.Add(rig.AddBot(name, BotBehaviour.Idle));
        foreach (Bot bot in bots)
            rig.UntilJoined(bot);
        rig.Tick(10);

        IconLibrary library = RealIcons.NewLibrary();
        var compositor = new AppearanceCompositor(library);
        int checkedColours = 0;
        foreach (string name in new[] { "Bob", "Carol", "Dave" })
        {
            var crew = (mob_living_carbon_human)rig.MobOf(name);
            string colours;
            bool gags;
            using (rig.World.Enter())
            {
                colours = crew.w_uniform.greyscale_colors as string;
                gags = crew.w_uniform.worn_icon is string text && text.StartsWith("gags:", StringComparison.Ordinal);
            }
            ReplicaEntity seen = bots[0].Replica.Entities.Single(e => e.Id == crew.dm_id);
            Assert.True(compositor.TryCompose(new Tables(bots[0].Replica), seen.State.Appearance, ByondDir.South, 0, out ComposedRaster composed),
                string.Join("; ", library.Problems));
            Assert.Empty(library.Problems);
            int shaded = ShadedPixelsOf(composed.Image, colours ?? "#3f3f3f");
            _output.WriteLine($"{name} ({seen.State.Name}): {(gags ? "GAGS" : "plain")} jumpsuit {colours}, {shaded} pixels of its colour");
            if (gags)
            {
                Assert.True(shaded >= 40, $"{name}'s jumpsuit {colours}: only {shaded} pixels of its colour");
                checkedColours++;
            }
        }
        Assert.True(checkedColours >= 1, "no crew member wore a GAGS jumpsuit");
    }
}
