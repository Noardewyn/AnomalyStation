using System.IO;
using System.Numerics;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Models;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Models;

public sealed class FixtureModelTests
{
    private static string Root => Path.GetDirectoryName(ModelCatalog.Locate()!)!;
    private static ModelCatalog Catalog => ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));

    [Theory]
    [InlineData("icons/obj/machines/wallmounts.dmi", "access_button_cycle", "access_button_cycle_green")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "access_control_process", "airlock_controller_process_0")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "airlock_control_process", "airlock_pressure_controller_process_0")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "airlock_sensor_alert", "airlock_sensor_alert_0")]
    [InlineData("icons/obj/machines/wallmounts.dmi", "auth_off", "keycard_auth_wall")]
    [InlineData("icons/obj/wallmounts.dmi", "migniter", "mounted_igniter_ready")]
    [InlineData("icons/obj/wallmounts.dmi", "migniter-b", "mounted_igniter_disabled")]
    [InlineData("icons/obj/wallmounts.dmi", "telescreenb", "telescreen_broken")]
    [InlineData("icons/obj/wallmounts.dmi", "noticeboard", "notice_board_empty")]
    [InlineData("icons/obj/signs.dmi", "frame-ratvar", "picture_frame_portrait_ratvar")]
    [InlineData("icons/obj/signs.dmi", "radiation", "warning_sign_radiation")]
    [InlineData("icons/obj/storage/storage.dmi", "wall_safe_broken", "wall_safe_broken_lock")]
    [InlineData("icons/obj/service/bureaucracy.dmi", "ticketmachine_empty", "ticket_machine_empty")]
    [InlineData("icons/obj/machines/barsigns.dmi", "empty", "bar_sign_empty")]
    [InlineData("icons/obj/machines/barsigns.dmi", "neon-flamingo", "bar_sign_neon-flamingo")]
    [InlineData("icons/obj/machines/status_display.dmi", "entertainment_frame", "entertainment_monitor")]
    [InlineData("icons/obj/machines/turret_control.dmi", "control", "turret_controller")]
    [InlineData("icons/obj/machines/fax.dmi", "fax_receive", "fax_transfer")]
    public void SourceStatesKeepTheirModels(string icon, string state, string id)
        => Assert.Equal(id, Catalog.Find(icon, state)?.Id);

    [Fact]
    public void VariantsKeepTheirMountAndScale()
    {
        foreach (var family in Catalog.Entries.Where(e => e.Family.Length > 0).GroupBy(e => e.Family))
        {
            ModelEntry first = family.First();
            Assert.All(family, e => Assert.Equal(
                (first.Rule, first.Offset, first.SourceWidth, first.SourceHeight, first.FitWidth, first.FitHeight, first.Scale),
                (e.Rule, e.Offset, e.SourceWidth, e.SourceHeight, e.FitWidth, e.FitHeight, e.Scale)));
        }
    }

    [Theory]
    [InlineData("fireaxe", "axe")]
    [InlineData("mechremoval", "crowbar")]
    [InlineData("jaws_of_recovery", "jaws")]
    public void CabinetDamageToolsAndLockAreIndependent(string state, string tool)
    {
        ModelEntry entry = Catalog.Find("icons/obj/wallmounts.dmi", state)!;
        ModelPlacement[] placed = ModelRules.Choose(entry, state, 2, 1);
        Assert.Equal(entry.Parts.Count, placed.Length); // wall rules draw all conditional parts
        foreach (bool loaded in new[] { false, true })
        foreach (bool open in new[] { false, true })
        foreach (bool locked in new[] { false, true })
        for (int damage = 0; damage <= 4; damage++)
        {
            string glass = "glass" + (damage == 0 ? "" : damage);
            string shown = glass + (open ? "_raised" : "");
            var overlays = new List<string> { shown };
            if (loaded) overlays.Add(tool);
            if (!open) overlays.Add(locked ? "locked" : "unlocked");
            string[] parts = entry.Parts.Where(p => p.Value.DrawnWith(overlays)).Select(p => p.Key).ToArray();
            Assert.Contains("model", parts);
            Assert.Contains("handle", parts);
            Assert.Equal(loaded, parts.Contains("tool"));
            Assert.Equal(new[] { glass }, parts.Where(p => p.StartsWith("glass", StringComparison.Ordinal)));
            Assert.Equal(new[] { open || !locked ? "unlocked" : "locked" }, parts.Where(p => p is "locked" or "unlocked"));
            Assert.Equal(open, entry.OpenOverlays.Contains(shown));
        }
        Vector3[] pivots = entry.Parts.Where(p => p.Value.Pivot).Select(p => Build(entry, p.Value).Pivot).ToArray();
        Assert.All(pivots, p => Assert.InRange(Vector3.Distance(pivots[0], p), 0f, 0.0001f));
    }

    [Fact]
    public void ADefibrillatorKeepsEveryChargeAndClampCombination()
    {
        foreach (string state in new[] { "defibrillator_mount", "penlite_mount" })
        {
            ModelEntry entry = Catalog.Find("icons/obj/machines/defib_mount.dmi", state)!;
            foreach (bool locked in new[] { false, true })
            foreach (string safety in new[] { "online", "emagged" })
            foreach (int level in new[] { 25, 50, 75, 100 })
            {
                string[] overlays = locked ? new[] { "defib", "clamps", safety, "charge" + level } : new[] { "defib", safety, "charge" + level };
                string[] drawn = entry.Parts.Where(p => p.Value.DrawnWith(overlays)).Select(p => p.Key).ToArray();
                Assert.Contains("device", drawn);
                Assert.Contains(safety, drawn);
                Assert.Equal(new[] { "charge" + level }, drawn.Where(p => p.StartsWith("charge", StringComparison.Ordinal)));
                Assert.Equal(locked ? 2 : 0, drawn.Count(p => p.EndsWith("Clamp", StringComparison.Ordinal)));
            }
            Assert.Equal(new[] { "model" }, entry.Parts.Where(p => p.Value.DrawnWith(Array.Empty<string>())).Select(p => p.Key));
        }
    }

    [Fact]
    public void RepeatedClockDigitsStayInTheirOwnSlots()
    {
        ModelEntry entry = Catalog.Find("icons/obj/digital_clock.dmi", "digital_clock_base")!;
        var source = new Tables();
        var look = new AppearanceEntry();
        foreach (short slot in new short[] { -14, -10, -4, 0 })
            look.Overlays.Add(source.Add("+1", slot));
        ModelPart[] digits = Drawn(entry, source, look).Where(p => p.OverlayPixelW != null).ToArray();
        Assert.Equal(4, digits.Length);
        Assert.All(digits, p => Assert.Equal(new[] { "+1" }, p.Overlays));
        Assert.Equal(4, digits.Select(p => p.OverlayPixelW).Distinct().Count());
        Assert.Equal(new[] { "model" }, Drawn(entry, source, new AppearanceEntry()).Select(p => p.Name));
        // Every digit shape is available in each position, including emagged 88:99.
        Assert.Equal(40, entry.Parts.Values.Count(p => p.OverlayPixelW != null));
        foreach (short slot in new short[] { -14, -10, -4, 0 })
            Assert.Equal(10, entry.Parts.Values.Count(p => p.OverlayPixelW == slot));
    }

    [Fact]
    public void MixedReagentLevelsSelectTheirOwnReservoirs()
    {
        ModelEntry entry = Catalog.Find("icons/obj/machines/wall_healer.dmi", "wall_healer")!;
        var source = new Tables();
        var look = new AppearanceEntry();
        for (short i = 0; i < 4; i++) look.Overlays.Add(source.Add("bar" + i * 2, (short)(i * 4)));
        Assert.Equal(new[] { "Aiuri_2", "Blood_6", "Libital_0", "Syriniver_4" },
            Drawn(entry, source, look).Where(p => p.OverlayPixelW != null).Select(p => p.Name).Order());
        Assert.All(entry.Parts.Values.Where(p => p.OverlayPixelW != null), p => Assert.Equal(new[] { "bar_emissive" }, p.GlowOverlays));
    }

    [Fact]
    public void NestedIndicatorsAndCyclicReferencesAreHandledOnce()
    {
        var source = new Tables();
        TableRef defib = source.Add("defib"), charge = source.Add("charge25"), clamps = source.Add("clamps");
        source.Entries[defib].Overlays.AddRange(new[] { charge, clamps, defib });
        var root = new AppearanceEntry(); root.Overlays.Add(defib);
        Assert.Equal(new[] { "defib", "charge25", "clamps" }, ModelOverlays.All(source, root).Select(o => source.ResolveString(o.Entry.IconState)));
    }

    [Fact]
    public void ScriptedWorldInterningPreservesDisplaySlotsAndEmissionPlanes()
    {
        var builder = new AppearanceBuilder(new List<string>(), new List<string>(), new List<AppearanceEntry>());
        TableRef icon = builder.Res("icons/obj/digital_clock.dmi"), state = builder.Str("+1");
        TableRef original = builder.Add(new AppearanceEntry { Icon = icon, IconState = state });
        TableRef shifted = builder.Add(new AppearanceEntry { Icon = icon, IconState = state, PixelW = -14 });
        Assert.NotEqual(original, shifted);
        Assert.Equal(shifted, builder.Add(new AppearanceEntry { Icon = icon, IconState = state, PixelW = -14 }));
        Assert.NotEqual(original, builder.Add(new AppearanceEntry { Icon = icon, IconState = state, Plane = 13 }));
        Assert.NotEqual(original, builder.Add(new AppearanceEntry { Icon = icon, IconState = state, PixelZ = 2 }));
    }

    [Theory]
    [InlineData("/obj/machinery/door_buttons/access_button")]
    [InlineData("/obj/machinery/airlock_controller")]
    [InlineData("/obj/machinery/airlock_sensor")]
    [InlineData("/obj/machinery/turretid")]
    [InlineData("/obj/machinery/barsign")]
    [InlineData("/obj/machinery/igniter")]
    [InlineData("/obj/machinery/ticket_machine")]
    [InlineData("/obj/machinery/digital_clock")]
    [InlineData("/obj/machinery/wall_healer")]
    [InlineData("/obj/machinery/computer/security/telescreen/entertainment")]
    public void FixturesUseTheWallEvenWithoutMappingOffsets(string path)
    {
        var entity = new PlanEntity(1, path, 10, 10, 1, 1, TableRef.Public(1), 0, 0, LightState.None, "");
        Assert.True(ChunkPlanner.TryPlace(entity, (x, y, z) => y == 11, out Placed placed));
        Assert.Equal(PlacedKind.WallDecal, placed.Kind);
    }

    [Fact]
    public void AuthoredEmissionWorksWithoutASpecialMaterialName()
    {
        GlbFile file = Read("machines/turret_controller/turret_controller_kill_0.glb");
        Assert.All(file.Materials.Where(m => m.Name.Contains("_Button_", StringComparison.Ordinal)), m => Assert.True(m.Emissive && m.Glow > 0));
        Assert.All(Read("machines/access_button/access_button_off.glb").Materials, m => Assert.False(m.Emissive));
    }

    [Fact]
    public void ReusedClockGeometryMovesByTheConfiguredSourceOffset()
    {
        ModelEntry entry = Catalog.Find("icons/obj/digital_clock.dmi", "digital_clock_base")!;
        ModelPart part = entry.Parts["digit_0_1"];
        ModelMeshData moved = Build(entry, part), original = Build(entry, part with { Offset = Vector3.Zero });
        float scale = ModelMesh.Scale(entry, 2, 2, .6f).Horizontal;
        for (int i = 0; i < moved.Positions.Length; i++)
            Assert.InRange(Vector3.Distance(moved.Positions[i], original.Positions[i] + part.Offset * scale), 0f, .00001f);
    }

    [Theory]
    [InlineData("sink", "/obj/structure/sink", 1f)]
    [InlineData("sink_alt", "/obj/structure/sink/kitchen", 1f)]
    [InlineData("shower", "/obj/machinery/shower", 1.98f)]
    public void PlumbingFitsBelowTheWallAndItsRearFittingsStayInTheRoom(string state, string type, float top)
    {
        ModelEntry entry = Catalog.Find("icons/obj/watercloset.dmi", state)!;
        Vector3[] mesh = Build(entry, entry.Parts["model"]).Positions;
        var source = FakeMap.DefaultTypes();
        foreach (string direction in new[] { "north", "south", "east", "west" })
        {
            string path = type + "/directional/" + direction;
            Assert.True(source.TryGet(path, out TypeDefaults defaults));
            var entity = new PlanEntity(1, path, 10, 10, 1, defaults.Dir, TableRef.Public(1), defaults.PixelX, defaults.PixelY, LightState.None, state);
            Assert.True(ChunkPlanner.TryPlace(entity, (_, _, _) => false, out Placed placed));
            Matrix4x4 rotation = Matrix4x4.CreateRotationY(ModelRules.Choose(entry, state, defaults.Dir, 0)[0].Yaw);
            Vector3 rear = Vector3.Transform(-Vector3.UnitZ, rotation);
            Vector3 origin = new(placed.OffsetX, placed.Height, -placed.OffsetY);
            Vector3[] points = mesh.Select(p => origin + Vector3.Transform(p + entry.Offset, rotation)).ToArray();
            // The wall face is one metre from the tile centre; leave 5 mm for the rear pipes.
            Assert.InRange(points.Max(p => Vector3.Dot(p, rear)), .994f, .996f);
            Assert.InRange(points.Max(p => p.Y), top - .001f, top + .001f);
            Assert.All(points, p => Assert.True(p.Y >= 0f));
        }
    }

    [Theory]
    [InlineData("holopad0")]
    [InlineData("holopad_open")]
    [InlineData("holopad_ringing")]
    [InlineData("holopad1")]
    public void EnlargedHolopadHousingIsHalfBelowTheFloorInEveryState(string state)
    {
        ModelEntry entry = Catalog.Find("icons/obj/machines/floor.dmi", state)!;
        ModelPart housing = entry.Parts["model"] with { Node = entry.Id + "_Floor_Housing" };
        Vector3[] points = Build(entry, housing).Positions;
        Assert.Equal((1.5f, 1.5f), ModelMesh.Scale(entry, 2, 2, .6f));
        Assert.True(FloorLayers.TryLift(FloorLayers.FloorPlane, 10015, "/obj/machinery/holopad", out float lift));
        float bottom = points.Min(p => p.Y) + entry.Offset.Y + lift;
        float top = points.Max(p => p.Y) + entry.Offset.Y + lift;
        Assert.InRange(bottom, -.04126f, -.04124f);
        Assert.InRange(top, .04124f, .04126f);
    }

    [Fact]
    public void FloorLampRetainsAnExposedLensAboveItsRecessedHousing()
    {
        foreach (ModelEntry entry in Catalog.Entries.Where(e => e.Id.StartsWith("light_floor_", StringComparison.Ordinal)))
        {
            Vector3[] points = Build(entry, entry.Parts["model"]).Positions;
            Assert.InRange(points.Min(p => p.Y) + entry.Offset.Y, -.53f, -.52f);
            Assert.InRange(points.Max(p => p.Y) + entry.Offset.Y, .035f, .038f);
        }
    }

    [Theory]
    [InlineData("north")]
    [InlineData("south")]
    [InlineData("east")]
    [InlineData("west")]
    public void UrinalUsesAWallModelAtItsAuthoredHeight(string direction)
    {
        ModelEntry entry = Catalog.Find("icons/obj/watercloset.dmi", "urinal")!;
        Assert.Equal(ModelRule.Wall, entry.Rule); // The renderer accepts only wall models for a wall decal.
        string path = "/obj/structure/urinal/directional/" + direction;
        Assert.True(FakeMap.DefaultTypes().TryGet(path, out TypeDefaults defaults));
        (int dx, int dy) = WallMounts.Step(defaults.Dir);
        var entity = new PlanEntity(1, path, 10, 10, 1, defaults.Dir, TableRef.Public(1), defaults.PixelX, defaults.PixelY, LightState.None, "urinal");
        Assert.True(ChunkPlanner.TryPlace(entity, (x, y, _) => x == 10 + dx && y == 10 + dy, out Placed placed));
        Assert.Equal(PlacedKind.WallDecal, placed.Kind);
        Matrix4x4 rotation = Matrix4x4.CreateRotationY(ModelRules.Choose(entry, "urinal", defaults.Dir, placed.Face)[0].Yaw);
        Vector3[] points = Build(entry, entry.Parts["model"]).Positions
            .Select(p => new Vector3(0, placed.Height, 0) + Vector3.Transform(p + entry.Offset, rotation)).ToArray();
        Vector3 towardWall = new(dx, 0, -dy);
        Assert.InRange(points.Max(p => Vector3.Dot(p, towardWall)), .994f, .996f);
        Assert.InRange(points.Min(p => Vector3.Dot(p, towardWall)), .741f, .743f);
        Assert.InRange(points.Min(p => p.Y), .399f, .401f);
        Assert.InRange(points.Max(p => p.Y), 1.299f, 1.301f);
    }

    private static GlbFile Read(string file) => GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, file)));
    private static ModelMeshData Build(ModelEntry entry, ModelPart part) => ModelMesh.Build(Read(part.File), entry, part, 2, 2, .6f);
    private static IEnumerable<ModelPart> Drawn(ModelEntry entry, Tables source, AppearanceEntry look)
    {
        string[] states = ModelOverlays.All(source, look).Select(o => source.ResolveString(o.Entry.IconState)).ToArray();
        return entry.Parts.Values.Where(p => p.DrawnWith(states) && (p.OverlayPixelW is not short slot || ModelOverlays.HasSlot(source, look, p.Overlays, slot)));
    }

    private sealed class Tables : IAppearanceSource
    {
        public readonly Dictionary<TableRef, AppearanceEntry> Entries = new();
        private readonly Dictionary<TableRef, string> _states = new();
        public TableRef Add(string state, short pixelW = 0)
        {
            TableRef reference = TableRef.Public((uint)Entries.Count + 1);
            Entries[reference] = new AppearanceEntry { IconState = reference, PixelW = pixelW };
            _states[reference] = state;
            return reference;
        }
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => Entries.TryGetValue(reference, out entry!);
        public string ResolveString(TableRef reference) => _states.GetValueOrDefault(reference)!;
        public string ResolveResource(TableRef reference) => "test.dmi";
    }
}
