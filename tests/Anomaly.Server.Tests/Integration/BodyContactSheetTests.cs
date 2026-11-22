using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
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
/// M6 slice 7, the contact sheet of ROADMAP M6 (D153: made from tg's pictures as the client draws them): the six zones of a naked
/// human of tg's consistent look (the unit tests' standard human), each intact, damaged (brute), bloodied (a severe cut's bleeding), missing (dismembered; the chest is split open) and
/// bandaged (tg's gauze), on the real server, composed by the client's compositor from what a headless client receives. The sheet is
/// a PNG: rows head, chest, left arm, right arm, left leg, right leg; columns intact, damaged, bloodied, missing, bandaged; each
/// picture facing south, four times its size. It is written to <c>ANOMALY_CONTACT_SHEET</c> (a file path) or the temporary folder.
/// </summary>
public class BodyContactSheetTests
{
    private const int Second = 20;
    private const int Scale = 4, Cell = 40 * Scale, Gap = 8;

    private static readonly string[] Zones = { BODY_ZONE_HEAD, BODY_ZONE_CHEST, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG };
    private static readonly string[] States = { "intact", "damaged", "bloodied", "missing", "bandaged" };

    private readonly ITestOutputHelper _output;

    public BodyContactSheetTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    // The state of one body: what the server does to the zone.
    private static void Make(mob_living_carbon_human h, string zone, string state)
    {
        var part = (obj_item_bodypart)h.get_bodypart(zone);
        switch (state)
        {
            case "damaged":
                Waiting.NoSleep(h.apply_damage(40, BRUTE, zone));
                break;
            case "bloodied":
                Assert.NotNull(Waiting.NoSleep(h.cause_wound_of_type_and_severity(WOUND_SLASH, part, WOUND_SEVERITY_SEVERE)));
                break;
            case "missing":
                Waiting.NoSleep(part.dismember());
                break;
            case "bandaged":
                var gauze = Dm.@new<obj_item_stack_medical_wrap_gauze>(h.loc);
                Waiting.NoSleep(part.apply_item(gauze, LIMB_ITEM_GAUZE));
                break;
        }
    }

    [Fact]
    public void The_six_zones_intact_damaged_bloodied_missing_and_bandaged_as_the_client_draws_them()
    {
        string iconRoot = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (iconRoot == null)
        {
            _output.WriteLine("no converted icons (tools/Anomaly.Tools.Icons): no sheet");
            return;
        }

        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 81, TimeoutSteps = 8000 }, strict: false);
        RealHeadlessWorld.Player p = w.Join("Viewer");
        var viewer = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int bootRuntimes = w.World.RuntimeErrorCount;

        var bodies = new mob_living_carbon_human[Zones.Length, States.Length];
        In(w, () =>
        {
            var at = (turf)viewer.loc;
            for (int z = 0; z < Zones.Length; z++)
                for (int s = 0; s < States.Length; s++)
                    bodies[z, s] = Dm.@new<mob_living_carbon_human_consistent>(at); // tg's standard look (dummy.dm:137), one skin for all
            return 0;
        });
        w.Run(Second);
        In(w, () =>
        {
            for (int z = 0; z < Zones.Length; z++)
                for (int s = 0; s < States.Length; s++)
                    Make(bodies[z, s], Zones[z], States[s]);
            return 0;
        });
        w.Run(2 * Second);

        var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(iconRoot)));
        var tables = new Tables(replica);
        int width = Gap + States.Length * (Cell + Gap), height = Gap + Zones.Length * (Cell + Gap);
        Raster sheet = Raster.Solid(width, height, 0xC8C8C8FF);
        var pictures = new byte[Zones.Length, States.Length][];
        for (int z = 0; z < Zones.Length; z++)
        {
            for (int s = 0; s < States.Length; s++)
            {
                Assert.True(replica.TryGet(bodies[z, s].dm_id, out ReplicaEntity seen), $"the client sees the {States[s]} {Zones[z]}");
                Assert.True(compositor.TryCompose(tables, seen.State.Appearance, ByondDir.South, 0, out ComposedRaster picture), string.Join("; ", compositor.Library.Problems));
                pictures[z, s] = Square(picture);
                Paste(sheet, picture, Gap + s * (Cell + Gap), Gap + z * (Cell + Gap));
            }
        }

        string path = Environment.GetEnvironmentVariable("ANOMALY_CONTACT_SHEET") ?? Path.Combine(Path.GetTempPath(), "anomaly-m6-contact-sheet.png");
        File.WriteAllBytes(path, PngEncoder.Encode(sheet, 6, 8, 0, false));
        _output.WriteLine("the contact sheet: " + path);

        // every changed state draws differently from the intact body of its zone (the chest split open keeps its picture: tg's
        // chest has no picture of its own split)
        for (int z = 0; z < Zones.Length; z++)
        {
            for (int s = 1; s < States.Length; s++)
            {
                bool same = pictures[z, s].AsSpan().SequenceEqual(pictures[z, 0]);
                _output.WriteLine($"{Zones[z]} {States[s]}: {(same ? "as intact" : "changed")}");
                if (!(Zones[z] == BODY_ZONE_CHEST && States[s] == "missing"))
                    Assert.False(same, $"the {States[s]} {Zones[z]} draws as the intact one");
            }
        }
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // The 32 x 32 icon square of a picture (pixels outside it are cut), to compare states.
    private static byte[] Square(ComposedRaster p)
    {
        var square = new Raster(32, 32);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int px = p.OriginX + x, py = p.Image.Height - 1 - (p.OriginY + y);
                if (px >= 0 && py >= 0 && px < p.Image.Width && py < p.Image.Height)
                    square.Set(x, 31 - y, p.Image.Get(px, py));
            }
        }
        return square.Pixels;
    }

    // The picture into its cell, scaled, its icon square at the cell's lower middle.
    private static void Paste(Raster sheet, ComposedRaster p, int cellX, int cellY)
    {
        int left = cellX + (Cell - 32 * Scale) / 2 - p.OriginX * Scale;
        int bottom = cellY + Cell - 4 * Scale; // the square's lower edge, from the top
        for (int y = 0; y < p.Image.Height; y++)
        {
            for (int x = 0; x < p.Image.Width; x++)
            {
                uint rgba = p.Image.Get(x, y);
                if ((rgba & 255) == 0)
                    continue;
                int fromBottom = p.Image.Height - 1 - y - p.OriginY; // rows above the square's lower edge
                for (int dy = 0; dy < Scale; dy++)
                {
                    for (int dx = 0; dx < Scale; dx++)
                    {
                        int sx = left + x * Scale + dx, sy = bottom - (fromBottom + 1) * Scale + dy;
                        if (sx < cellX || sy < cellY || sx >= cellX + Cell || sy >= cellY + Cell)
                            continue;
                        var one = new Raster(1, 1);
                        one.Set(0, 0, rgba);
                        sheet.Blend(one, IconBlend.Overlay, sx, sy);
                    }
                }
            }
        }
    }
}
