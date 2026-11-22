using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Light;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.Light;

/// <summary>D168, D288: the graphics presets and the Graphics tab's settings, the options' own set, the shadow atlas sized for the count, the selector's line of sight.</summary>
public class ShadowBudgetTests
{
    [Fact]
    public void ThePresetsAreLowMediumHighAndMaxEachDrawingMoreLampsWithMoreShadows()
    {
        Assert.Equal(new[] { "low", "medium", "high", "max" }, RenderPreset.All.Select(p => p.Name));
        Assert.Equal(new[] { 4, 8, 16, 32 }, RenderPreset.All.Select(p => p.ShadowedLights));
        Assert.Equal(new[] { 0, 1, 1, 2 }, RenderPreset.All.Select(p => p.ObjectShadows));
        Assert.Equal(new[] { 4, 4, 8, 16 }, RenderPreset.All.Select(p => p.ObjectShadowLamps)); // the owner: max held the crates' shadows too short
        for (int i = 1; i < RenderPreset.All.Length; i++)
        {
            Assert.True(RenderPreset.All[i].LightDistance > RenderPreset.All[i - 1].LightDistance);
            Assert.True(RenderPreset.All[i].ViewDistance > RenderPreset.All[i - 1].ViewDistance);
            Assert.True(RenderPreset.All[i].SunShadow > RenderPreset.All[i - 1].SunShadow);
        }
        Assert.All(RenderPreset.All, p => Assert.Equal(0, p.Fog)); // the owner removed the haze (2026-10-24)
    }

    [Fact]
    public void EveryPresetsSettingIsOneTheGraphicsTabOffers()
    {
        foreach (RenderPreset preset in RenderPreset.All)
            foreach (GraphicsSetting setting in GraphicsSetting.All)
                Assert.True(setting.Offers(setting.Get(preset)), $"{preset.Name}: {setting.Id} = {setting.Get(preset)}");
        Assert.Equal(GraphicsSetting.All.Length, GraphicsSetting.All.Select(s => s.Id).Distinct().Count());
        foreach (GraphicsSetting setting in GraphicsSetting.All)
            foreach ((string _, int value) in setting.Choices)
                Assert.Equal(value, setting.Get(setting.Set(RenderPreset.Medium, value)));
    }

    [Fact]
    public void APresetsSettingsMatchItsNameAndAChangedOneIsCustom()
    {
        Assert.Equal("high", (RenderPreset.High with { Name = RenderPreset.CustomName }).Matching);
        Assert.Equal(RenderPreset.CustomName, (RenderPreset.High with { ShadowedLights = 2 }).Matching);
    }

    [Fact]
    public void TheAtlasHoldsTwoSlotsALampPlusTheCamerasSpotAtAUsableSlotSize()
    {
        foreach (RenderPreset preset in RenderPreset.All)
        {
            ShadowAtlasPlan plan = ShadowAtlasPlan.For(preset.ShadowedLights, preset.ShadowAtlas);
            Assert.True(plan.Slots >= 2 * preset.ShadowedLights + 1, $"{preset.Name}: {plan.Slots} slots");
            Assert.True(plan.SmallestSlotPixels >= 512, $"{preset.Name}: slot {plan.SmallestSlotPixels}");
        }
        // a quadrant of one slot would hold no omni lamp (its shadow takes two slots of one quadrant)
        Assert.Equal(new[] { 4, 4, 4, 4 }, ShadowAtlasPlan.For(0, 2048).Quadrants);
        Assert.Equal(new[] { 4, 4, 4, 4 }, ShadowAtlasPlan.For(-5, 2048).Quadrants);
        Assert.Equal(new[] { 4, 4, 4, 4 }, ShadowAtlasPlan.For(4, 2048).Quadrants); // low: 8 + 1 of 16
        // medium: 8 lamps and the spot are 17, more than 16: one quadrant cut finer, 12 slots of 1024 px stay
        ShadowAtlasPlan medium = ShadowAtlasPlan.For(8, 4096);
        Assert.Equal(new[] { 4, 4, 4, 16 }, medium.Quadrants);
        Assert.Equal((1024, 512), (medium.SlotPixels, medium.SmallestSlotPixels));
        Assert.Equal(new[] { 16, 16, 16, 64 }, ShadowAtlasPlan.For(32, 8192).Quadrants); // max: 65 of 112
        Assert.Equal(1024, ShadowAtlasPlan.For(32, 8192).SlotPixels);
        for (int n = 1; n <= 63; n++)
            Assert.True(ShadowAtlasPlan.For(n, 4096).Slots >= ShadowAtlasPlan.For(n - 1, 4096).Slots);
    }

    [Fact]
    public void TheEffectsTakeTheShadowsFirstThenTheLightsOnBodiesThenTheLamps()
    {
        Assert.Equal(0, ShadowedLightSelector.Priority("/obj/effect/hotspot", false)); // an explosion's fire
        Assert.Equal(0, ShadowedLightSelector.Priority("/obj/effect/particle_effect/sparks", false));
        Assert.Equal(0, ShadowedLightSelector.Priority("/obj/effect/dummy/lighting_obj", false)); // a flash
        Assert.Equal(0, ShadowedLightSelector.Priority("/obj/projectile/beam/laser", false));
        Assert.Equal(1, ShadowedLightSelector.Priority("/mob/living/carbon/human", true)); // a flare or a lamp in hand
        Assert.Equal(2, ShadowedLightSelector.Priority("/obj/machinery/light", false));
        Assert.Equal(2, ShadowedLightSelector.Priority("/obj/machinery/computer", false));
        Assert.Equal(2, ShadowedLightSelector.Priority(null, false)); // the dev light room's sources
    }

    [Fact]
    public void TheSunsShadowReachesFartherAtEachLevelAndNoneAtOff()
    {
        Assert.Equal(0, SunShadowPlan.For(0).Splits);
        for (int level = 2; level <= 4; level++)
            Assert.True(SunShadowPlan.For(level).Distance > SunShadowPlan.For(level - 1).Distance);
    }

    [Fact]
    public void TenShadowedLightsAreTheTenNearestWithALineOfSight()
    {
        var cells = new ArrayCells(40, 20);
        cells.Fill(20, 1, 20, 20, true); // a wall at x = 20
        var candidates = new List<ShadowCandidate>();
        // Twelve open lamps at distances 1..12 west of the camera's x = 19, two lamps close but behind the wall, one in the wall tile.
        for (int i = 1; i <= 12; i++)
            candidates.Add(new ShadowCandidate(100 + i, 19 - i, 10, 20f, 1f));
        candidates.Add(new ShadowCandidate(1, 22, 10, 20f, 1f));
        candidates.Add(new ShadowCandidate(2, 23, 11, 20f, 1f));
        candidates.Add(new ShadowCandidate(3, 20, 10, 20f, 1f)); // the end tile does not block
        var chosen = new List<ShadowCandidate>();
        var scratch = new List<(float, ShadowCandidate)>();
        ShadowedLightSelector.Select(cells, 1, 19f, 10f, candidates, 10, chosen, scratch);
        Assert.Equal(10, chosen.Count);
        Assert.Equal(new long[] { 3, 101, 102, 103, 104, 105, 106, 107, 108, 109 }, chosen.Select(c => c.Key).ToArray());
        Assert.DoesNotContain(chosen, c => c.Key is 1 or 2);
    }

    [Fact]
    public void AChangedSettingMakesTheCustomSetWhichRoundTrips()
    {
        var options = new PlayOptions();
        Assert.Equal("medium", options.Quality);
        Assert.Equal(RenderPreset.Medium, options.Graphics);
        Assert.DoesNotContain("gfx.", options.ToText());
        options.Quality = "high";
        options.SetGraphics(GraphicsSetting.Find("shadowed_lights"), 24);
        Assert.Equal(RenderPreset.CustomName, options.Quality);
        Assert.Equal(24, options.Graphics.ShadowedLights);
        Assert.Equal(RenderPreset.High.LightDistance, options.Graphics.LightDistance); // the rest is the preset it was changed from
        var errors = new List<string>();
        PlayOptions back = PlayOptions.Parse(options.ToText(), errors);
        Assert.Empty(errors);
        Assert.Equal(options.Graphics, back.Graphics);
        Assert.Equal(RenderPreset.Max, PlayOptions.Parse("quality=max\n", null).Graphics);
    }

    [Fact]
    public void TheOldShadowedLightsLineBecomesTheSettingAndBadValuesAreRefused()
    {
        var errors = new List<string>();
        PlayOptions ten = PlayOptions.Parse("quality=high\nshadowed_lights=10\n", errors);
        Assert.Empty(errors);
        Assert.Equal(10, ten.Graphics.ShadowedLights);
        Assert.Equal(RenderPreset.High.ViewDistance, ten.Graphics.ViewDistance);
        Assert.Equal(RenderPreset.Medium, PlayOptions.Parse("shadowed_lights=preset\n", null).Graphics);
        var bad = new List<string>();
        PlayOptions.Parse("shadowed_lights=17\ngfx.msaa=3\ngfx.nothing=1\nquality=ultra\n", bad);
        Assert.Equal(4, bad.Count);
        Assert.False(new PlayOptions().TrySet("gfx.shadowed_lights", "40", out string problem));
        Assert.Contains("32", problem);
    }

    [Fact]
    public void TheBenchTakesASettingASpotAndTheCustomSet()
    {
        ClientArgs args = ClientArgs.Parse(new[] { "--fake-server", "--bench=10", "--bench-at=110,134", "--option=gfx.shadowed_lights=10", "--bench-presets=max,custom" });
        Assert.Empty(args.Errors);
        Assert.Equal((110, 134), args.BenchAt);
        Assert.Contains(("gfx.shadowed_lights", "10"), args.Options);
        Assert.Equal(new[] { "max", "custom" }, args.BenchPresets);
        Assert.NotEmpty(ClientArgs.Parse(new[] { "--bench-at=110" }).Errors);
        Assert.NotEmpty(ClientArgs.Parse(new[] { "--option=gfx.shadowed_lights=40" }).Errors);
    }
}
