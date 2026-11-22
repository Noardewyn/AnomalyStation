using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>
/// Gaps AS1 (D142): the client draws the assemblies with tg's sprites. Every state the timer, health sensor, infrared emitter, voice
/// analyzer, mousetrap and holder set (their own, their holder halves, the timer's lights, the emitter's on states), the infrared beam
/// and the electric chair's overlay are in the converted sheets and draw pixels; a holder draws its two halves over it; and its right
/// half (holder.dm's <c>right.transform = matrix(-1, 0, 0, 0, 1, 0)</c>, sent as the overlay's transform) is drawn mirrored.
/// </summary>
public class AssembliesPictureTests
{
    private const string Assemblies = "icons/obj/devices/new_assemblies.dmi";

    private static readonly (string Icon, string[] States)[] Sheets =
    {
        (Assemblies, new[]
        {
            "assembly_holder", "timer", "timer_left", "timer_light", "timer_timing_l", "health", "health_left", "infrared", "infrared_left",
            "infrared_on", "infrared_on_l", "voice", "voice_left", "mousetrap", "mousetraparmed", "mousetrap_left", "mousetraparmed_left",
            "igniter_left", "prox_left", "signaller_left",
        }),
        ("icons/effects/beam.dmi", new[] { "infrared" }),
        ("icons/obj/chairs.dmi", new[] { "echair0", "echair_over" }),
    };

    [RealIconsFact]
    public void EveryLookOfTheAssembliesTheBeamAndTheElectricChairDraws()
    {
        IconLibrary library = RealIcons.NewLibrary();
        var missing = new List<string>();
        foreach ((string path, string[] states) in Sheets)
        {
            if (!library.TryGetIcon(path, out IIcon icon))
            {
                missing.Add(path);
                continue;
            }
            foreach (string name in states)
            {
                if (!icon.TryGetState(name, out IIconState state) || state.Get(0, 0).IsEmpty())
                    missing.Add(path + ":" + name);
            }
        }
        Assert.Empty(missing);
    }

    [RealIconsFact]
    public void AHoldersRightHalfIsDrawnMirrored()
    {
        var tables = new TestTables();
        var compositor = new AppearanceCompositor(RealIcons.NewLibrary());
        float[] mirror = { -1, 0, 0, 0, 1, 0 }; // holder.dm:122
        TableRef plain = tables.Add(Assemblies, "igniter_left");
        TableRef mirrored = tables.Add(Assemblies, "igniter_left", e => e.Transform = mirror);
        Assert.True(compositor.TryCompose(tables, plain, ByondDir.South, 0, out ComposedRaster left));
        Assert.True(compositor.TryCompose(tables, mirrored, ByondDir.South, 0, out ComposedRaster right));
        int w = left.Image.Width, h = left.Image.Height;
        Assert.Equal((w, h), (right.Image.Width, right.Image.Height));
        int drawn = 0, wrong = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool shown = (left.Image.Get(x, y) & 0xFF) != 0;
                drawn += shown ? 1 : 0;
                if (shown != ((right.Image.Get(w - 1 - x, y) & 0xFF) != 0))
                    wrong++;
            }
        }
        Assert.True(drawn > 0, "the igniter's half draws nothing");
        Assert.Equal(0, wrong);

        // a holder with a timer on its left and the igniter on its right draws both halves over its frame
        TableRef timerHalf = tables.Add(Assemblies, "timer_left");
        TableRef igniterHalf = tables.Add(Assemblies, "igniter_left", e => e.Transform = mirror);
        TableRef holder = tables.Add(Assemblies, "assembly_holder", e =>
        {
            e.Overlays.Add(timerHalf);
            e.Overlays.Add(igniterHalf);
        });
        TableRef bare = tables.Add(Assemblies, "assembly_holder");
        Assert.True(compositor.TryCompose(tables, holder, ByondDir.South, 0, out ComposedRaster full));
        Assert.True(compositor.TryCompose(tables, bare, ByondDir.South, 0, out ComposedRaster empty));
        int differLeft = 0, differRight = 0;
        for (int y = 0; y < full.Image.Height; y++)
        {
            for (int x = 0; x < full.Image.Width; x++)
            {
                if (full.Image.Get(x, y) == empty.Image.Get(x, y))
                    continue;
                if (x < full.Image.Width / 2)
                    differLeft++;
                else
                    differRight++;
            }
        }
        Assert.True(differLeft > 0, "the timer's half draws nothing on the left");
        Assert.True(differRight > 0, "the igniter's half draws nothing on the right");
    }
}
