using System.IO;
using Anomaly.Client.Shared.Models;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>M7 slice G1: the models of the solar panel and the tracker turn their head to the azimuth tg's transform carries (ModelRule.Sun).</summary>
public sealed class SolarModelTests
{
    private static ModelCatalog Catalog
    {
        get
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "godot", "assets", "models", ModelCatalog.FileName);
                if (File.Exists(candidate))
                    return ModelCatalog.Parse(File.ReadAllText(candidate));
            }
            throw new InvalidOperationException("godot/assets/models/models.json not found above the test's folder");
        }
    }

    private const string Icon = "icons/obj/machines/solar.dmi";

    [Fact]
    public void ThePanelTheBrokenPanelAndTheTrackerAreSunModelsFoundByTheirStateAndOverlays()
    {
        ModelCatalog catalog = Catalog;
        Assert.Equal("solar_panel", catalog.Find(Icon, "sp_base", new[] { "solar_panel_glass", "solar_panel_glass_edge" }).Id);
        Assert.Equal("solar_panel_broken", catalog.Find(Icon, "sp_base", new[] { "solar_panel_glass-b", "solar_panel_glass-b" }).Id);
        Assert.Equal("solar_tracker", catalog.Find(Icon, "tracker_base", new[] { "tracker", "tracker_edge" }).Id);
        Assert.All(new[] { "solar_panel", "solar_panel_broken", "solar_tracker" }, id =>
        {
            ModelEntry entry = catalog.Entries.Single(e => e.Id == id);
            Assert.Equal(ModelRule.Sun, entry.Rule);
            Assert.Contains(entry.Parts.Keys, part => part.StartsWith(ModelRules.SunHead, StringComparison.Ordinal));
            Assert.Contains(entry.Parts.Keys, part => !part.StartsWith(ModelRules.SunHead, StringComparison.Ordinal));
            Assert.False(string.IsNullOrEmpty(entry.Azimuth));
        });
    }

    [Fact]
    public void OnlyTheHeadTurnsAndFacesTheAzimuthWithItsFrontToSouthUnturned()
    {
        ModelEntry panel = Catalog.Entries.Single(e => e.Id == "solar_panel");
        foreach ((float azimuth, float yawDegrees) in new[] { (180f, 0f), (0f, 180f), (270f, -90f), (90f, 90f) })
        {
            ModelPlacement[] placed = ModelRules.Choose(panel, "sp_base", 2, 0, azimuth);
            Assert.Equal(panel.Parts.Count, placed.Length);
            Assert.All(placed, p =>
            {
                float expected = p.Part.StartsWith(ModelRules.SunHead, StringComparison.Ordinal) ? yawDegrees * MathF.PI / 180f : 0f;
                Assert.Equal(expected, p.Yaw, 4);
            });
        }
    }

    [Fact]
    public void TgsPanelAndTrackerTransformsGiveBackTheAngleTheyWereTurnedTo()
    {
        for (int angle = 0; angle < 360; angle += 15)
        {
            // get_panel_transform (solar.dm:174) and get_tracker_transform (tracker.dm:84), by tg's own matrix procs
            matrix panel = Dm.matrix();
            panel.Turn(angle);
            panel.Shear(0, -0.6f * Dm.sin(angle));
            panel.Scale(1, 0.85f * (Dm.cos(angle) * -0.5f + 0.5f) + 0.15f);
            matrix dish = Dm.matrix();
            dish.Turn(angle);
            dish.Scale(1, 0.5f);
            foreach (matrix m in new[] { panel, dish })
            {
                float seen = (ModelRules.AzimuthOf(m.a, m.b) + 360f) % 360f;
                Assert.Equal(angle, seen, 2);
            }
        }
    }

    [Fact]
    public void AHeadTurnsTheShortWayAlongAnEasedCurveAndSlowly()
    {
        var head = new SunHeadMotion(350f, 10f);
        head.Aim(20f, 55f); // 30 degrees across north, 45 of tilt: the longer, at 12 a second, under the 2 s floor? no: 3.75 s
        Assert.True(head.Moving);
        head.Step(0.1f);
        float early = head.Azimuth;
        Assert.InRange(early, 350f, 351f); // eases in: barely moved after a tenth of a second
        head.Step(1.775f); // half way
        Assert.Equal(5f, head.Azimuth, 1);
        Assert.Equal(32.5f, head.Tilt, 1);
        head.Step(10f);
        Assert.False(head.Moving);
        Assert.Equal(20f, head.Azimuth, 3);
        Assert.Equal(55f, head.Tilt, 3);
    }

    [Fact]
    public void APanelAtItsStartBearingKeepsItsOwnTiltAboveFifteenDegreesAndATurnedOneFacesTheSun()
    {
        for (long id = 1; id < 200; id++)
        {
            float start = obj_machinery_power_solar.anomaly_start_azimuth(id);
            Assert.InRange(start, 0f, 359.99f);
            float tilt = SunHeadMotion.TiltFor(id, start, panel: true);
            Assert.InRange(tilt, 0f, 75f); // its face never below 15 degrees over the horizon
            Assert.Equal(90f - ModelRules.SunElevationDegrees, SunHeadMotion.TiltFor(id, start + 5f, panel: true));
            Assert.Equal(90f - ModelRules.SunElevationDegrees, SunHeadMotion.TiltFor(id, start, panel: false));
        }
    }
}
