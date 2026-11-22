using System.IO;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// The machine and furniture waves (D277, docs/ai/specs/models.md "Machine and furniture waves"): a part shown by tg's icon state, a slide, a steady spin, a
/// node's scale, a hidden entry, the row fit; and the generated entries of the repository's catalog that use them.
/// </summary>
public sealed class WaveModelTests
{
    private static ModelCatalog Repository()
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "godot", "assets", "models", ModelCatalog.FileName);
            if (File.Exists(path))
                return ModelCatalog.Parse(File.ReadAllText(path));
        }
        throw new InvalidOperationException("godot/assets/models/models.json not found above the test's folder");
    }

    [Fact]
    public void APartFollowsTheIconStateAndMayMoveSpinOrTakeItsOwnScale()
    {
        ModelCatalog catalog = ModelCatalog.Parse("""
            { "models": [
              { "id": "x", "icons": [ "a.dmi" ], "states": [ "on", "off" ], "rule": "single", "texture": "model", "fit": { "width": "row", "height": 2.0 },
                "parts": { "model": { "file": "x.glb", "node": "Body" },
                           "lit": { "file": "x.glb", "node": "Lamp", "states": [ "on" ] },
                           "dark": { "file": "x.glb", "node": "Lamp", "exceptStates": [ "on" ] },
                           "tray": { "file": "x.glb", "node": "Tray", "pivot": true, "move": [ 0, 0, 1.8 ] },
                           "ring": { "file": "x.glb", "node": "Ring", "pivot": true, "axis": [ 0, 1, 0 ], "spin": 45 },
                           "shutter": { "file": "x.glb", "node": "Shutter", "nodeScale": [ 1, 1, 1 ] } } },
              { "id": "y", "hidden": true, "icons": [ "a.dmi" ], "states": [ "part" ], "rule": "single", "texture": "model", "parts": {} } ] }
            """);
        ModelEntry x = catalog.Entries[0];
        Assert.True(x.Parts["lit"].ShownIn("on") && !x.Parts["lit"].ShownIn("off"));
        Assert.True(x.Parts["dark"].ShownIn("off") && !x.Parts["dark"].ShownIn("on"));
        Assert.True(x.Parts["model"].ShownIn("anything"));
        Assert.Equal(new System.Numerics.Vector3(0, 0, 1.8f), x.Parts["tray"].Move);
        Assert.Equal(45f, x.Parts["ring"].Spin);
        Assert.Equal(System.Numerics.Vector3.One, x.Parts["shutter"].NodeScale);
        Assert.Null(x.Parts["model"].NodeScale);
        Assert.Equal("row", x.FitWidth);
        Assert.True(catalog.Find("a.dmi", "part", Array.Empty<string>()).Hidden);
    }

    [Fact]
    public void AFlickMovesItsPartsOnItsMarkedFrames()
    {
        ModelCatalog catalog = Repository();
        // mass_driver.dm: flick("mass_driver1") throws the carriage out and back, its actuator squeezed; the hatch keeps its own swing for the open panel
        ModelEntry driver = catalog.Find("icons/obj/machines/floor.dmi", "mass_driver", Array.Empty<string>());
        ModelFlick carriage = driver.Parts["Pusher_Housing"].Flick;
        Assert.Equal(new[] { "mass_driver1" }, carriage.States);
        Assert.Equal(1.28f, carriage.Move.Z, 3);
        Assert.True(carriage.TargetAt(0) == 1f && carriage.TargetAt(6) == 0f && carriage.TargetAt(11) == 0f);
        Assert.True(driver.Parts["Central_Ribbed_Actuator"].Flick.Scale.Z < 0.1f);
        Assert.True(driver.Parts["Service_Hatch"].Degrees == 105f && driver.Parts["Service_Hatch"].Flick != null);
        Assert.Null(driver.Parts["Rail_Bed"].Flick);
        // outlet.dm: flick("outlet-open"): its doors open on the frames after the warning lights only
        ModelFlick door = catalog.Find("icons/obj/pipes_n_cables/disposal.dmi", "outlet", Array.Empty<string>()).Parts["Outlet_Left"].Flick;
        Assert.True(door.TargetAt(4) == 0f && door.TargetAt(5) == 1f && door.TargetAt(7) == 0f && door.Degrees == 105f);
    }

    [Fact]
    public void TheWaveEntriesDrawTgsStates()
    {
        ModelCatalog catalog = Repository();
        // spaceheater.dm: the improvised heater (sheater-rigged) before the plain one; each core panel by tg's mode, the hatch with sheater-open
        Assert.Equal("improvised_chemical_heater", catalog.Find("icons/obj/pipes_n_cables/atmos.dmi", "sheater-heat", new[] { "sheater-rigged" }).Id);
        ModelEntry heater = catalog.Find("icons/obj/pipes_n_cables/atmos.dmi", "sheater-heat", Array.Empty<string>());
        Assert.Equal("space_heater", heater.Id);
        Assert.Equal(new[] { "sheater-heat" }, heater.Parts["Thermal_Core_0_sheater-heat"].States);
        Assert.Equal(new[] { "sheater-open" }, heater.OpenOverlays);
        Assert.True(heater.Parts["Service_Hatch"].Pivot && heater.Parts["Service_Hatch"].Degrees == 90f);
        // gravitygenerator.dm: the main part draws the generator, without its flat field ring (the owner); the outer eight are hidden
        ModelEntry gravity = catalog.Find("icons/obj/machines/gravity_generator.dmi", "on_8", Array.Empty<string>());
        Assert.Equal("gravity_generator", gravity.Id);
        Assert.True(gravity.Parts.ContainsKey("Corner_4_Peripheral_Lights") && !gravity.Parts.Keys.Any(k => k.StartsWith("Field_")));
        // each repair stage its own file, without the corner lights
        ModelEntry fix3 = catalog.Find("icons/obj/machines/gravity_generator.dmi", "fix3_8", Array.Empty<string>());
        Assert.Equal("gravity_generator_fix3", fix3.Id);
        Assert.False(fix3.Parts.ContainsKey("Corner_4_Peripheral_Lights"));
        Assert.Equal("gravity_generator_off", catalog.Find("icons/obj/machines/gravity_generator.dmi", "off_8", Array.Empty<string>()).Id);
        // set_state(): the centre part (5) carries the charge overlay, drawn on its field mesh with the overlay's own frames; none without one
        ModelEntry field = catalog.Find("icons/obj/machines/gravity_generator.dmi", "on_5", new[] { "idle" });
        Assert.True(field.Id == "gravity_generator_field" && field.OverlayFrames && !field.Hidden);
        Assert.True(field.Parts["Field_idle_0"].DrawnWith(new[] { "idle" }) && !field.Parts["Field_idle_0"].DrawnWith(new[] { "activated" }));
        Assert.True(field.Parts.Values.All(p => !p.DrawnWith(Array.Empty<string>())));
        Assert.True(catalog.Find("icons/obj/machines/gravity_generator.dmi", "fix0_4", Array.Empty<string>()).Hidden);
        Assert.Equal("delivery_chute", catalog.Find("icons/obj/pipes_n_cables/disposal.dmi", "intake", Array.Empty<string>()).Id);
        // stationary.dm, slotmachine.dm: each broken state its own file; the slot machine's screens only with its icon_screen (none unpowered)
        Assert.Equal("piano_broken", catalog.Find("icons/obj/art/musician.dmi", "pianobroken", Array.Empty<string>()).Id);
        Assert.Equal("slot_machine_broken", catalog.Find("icons/obj/machines/computer.dmi", "slots_broken", Array.Empty<string>()).Id);
        ModelPart reels = catalog.Find("icons/obj/machines/computer.dmi", "slots", Array.Empty<string>()).Parts["Screen_Reels"];
        Assert.True(reels.DrawnWith(new[] { "slots_screen_working" }) && !reels.DrawnWith(Array.Empty<string>()));
        Assert.Equal("1.8", catalog.Entries.Single(e => e.Id == "arcade").FitHeight);
        Assert.True(catalog.Find("icons/obj/machines/gravity_generator.dmi", "off_4", Array.Empty<string>()).Hidden);
        // morgue.dm: the tray slid out is the tray thing's; the container's own is drawn while it is shut
        ModelEntry morgue = catalog.Find("icons/obj/structures.dmi", "morgue1", Array.Empty<string>());
        Assert.True(morgue.Parts["Sliding_Tray"].ShownIn("morgue1") && !morgue.Parts["Sliding_Tray"].ShownIn("morgue0"));
        Assert.Equal("morgue_tray", catalog.Find("icons/obj/structures.dmi", "morguet", Array.Empty<string>()).Id);
        // filingcabinet.dm: the middle drawer slides out while tg shows -open
        ModelEntry cabinet = catalog.Find("icons/obj/service/bureaucracy.dmi", "filingcabinet", Array.Empty<string>());
        Assert.Equal(new[] { "filingcabinet-open" }, cabinet.OpenStates);
        Assert.True(cabinet.Parts["Drawer_2"].Move.Z > 0.3f);
        // the owner (2026-10-27): every bomb of nuke.dmi, the sofas stretched along their row
        Assert.Equal("nuclear_bomb", catalog.Find("icons/obj/machines/nuke.dmi", "nuclearbomb_timing", Array.Empty<string>()).Id);
        Assert.Equal("row", catalog.Find("icons/obj/chairs_wide.dmi", "sofamiddle", Array.Empty<string>()).FitWidth);
    }

    [Fact]
    public void TheStationExtrasAndTheMapsOwnPicturesAreDrawn()
    {
        ModelCatalog catalog = Repository();
        // the port's bookcase and smart fridge keep the map's picture (no procs yet): the full random bookcase shows every row of books
        ModelEntry bookcase = catalog.Find("icons/obj/service/library.dmi", "random_bookcase", Array.Empty<string>());
        Assert.Equal("bookcase", bookcase.Id);
        Assert.True(bookcase.Parts["Books_Row_3"].ShownIn("random_bookcase") && !bookcase.Parts["Books_Row_1"].ShownIn("bookempty"));
        Assert.Equal("smartfridge", catalog.Find("icons/obj/machines/smartfridge.dmi", "smartfridge-icon", Array.Empty<string>()).Id);
        // kitchen_spike.dm: the hooks only on the spike, not its frame
        ModelEntry spike = catalog.Find("icons/obj/service/kitchen.dmi", "spikeframe", Array.Empty<string>());
        Assert.True(!spike.Parts["Installed_Spikes"].ShownIn("spikeframe") && spike.Parts["Installed_Spikes"].ShownIn("spike"));
        // suit_storage_unit.dm: the industrial unit's shutter is up while it is open
        ModelEntry suit = catalog.Find("icons/obj/machines/suit_storage.dmi", "industrial", Array.Empty<string>());
        Assert.Equal(new[] { "industrial_open" }, suit.Parts["Retracting_Shutter"].ExceptOverlays);
        Assert.Equal("gulag_item_reclaimer", catalog.Find("icons/obj/machines/wallmounts.dmi", "gulag_off", Array.Empty<string>()).Id);
        Assert.Equal("skill_station", catalog.Find("icons/obj/machines/implant_chair.dmi", "implantchair", Array.Empty<string>()).Id);
    }

    [Fact]
    public void AModelExportedPerStateDrawsTheFileOfTgsState()
    {
        ModelCatalog catalog = Repository();
        // stasis.dm: one file per icon state
        ModelEntry stasis = catalog.Find("icons/obj/machines/stasis.dmi", "stasis_broken", Array.Empty<string>());
        Assert.True(stasis.Parts["stasis_v3_broken"].ShownIn("stasis_broken") && !stasis.Parts["model"].ShownIn("stasis_broken"));
        // suit_storage_unit.dm: open wins over the panel and the lights; a shut unit with no light overlay is the closed file
        ModelEntry suit = catalog.Find("icons/obj/machines/suit_storage.dmi", "classic", Array.Empty<string>());
        Assert.True(suit.FlatColors);
        string[] Drawn(params string[] overlays) => suit.Parts.Where(p => p.Value.DrawnWith(overlays)).Select(p => p.Key).ToArray();
        Assert.Equal(new[] { "model" }, Drawn("classic_open", "classic_panel", "classic_uv"));
        Assert.Equal(new[] { "suit_storage_v3_uv" }, Drawn("classic_uv"));
        Assert.Equal(new[] { "suit_storage_v3_closed" }, Drawn());
    }

    [Fact]
    public void TheRevisedMachinesShowTgsStatesOnTheirNewParts()
    {
        ModelCatalog catalog = Repository();
        const string engine = "icons/obj/machines/engine/tesla_coil.dmi";
        // coil.dm: each frame of the flick has its own bolt, on the whole coil
        ModelEntry bolt = catalog.FindFlickFrame(engine, "coilhit", 3);
        Assert.True(bolt.Id == "tesla_coil_hit_3" && bolt.Parts.ContainsKey("Discharge_3") && !bolt.Parts.ContainsKey("Discharge_2") && bolt.Parts.Count > 10);
        Assert.Equal("grounding_rod_hit_4", catalog.FindFlickFrame(engine, "grounding_rodhit", 4).Id);
        // field_generator.dm: its collars are lit with a field up or warming up, its meters follow +a and +p
        ModelEntry field = catalog.Find("icons/obj/machines/field_generator.dmi", "Field_Gen", Array.Empty<string>());
        Assert.True(field.Parts["Emitter_Collar"].DrawnWith(Array.Empty<string>()) && !field.Parts["Emitter_Collar"].DrawnWith(new[] { "+on" }));
        Assert.True(field.Parts["Meter_p4"].DrawnWith(new[] { "+p4" }) && !field.Parts["Meter_p4"].DrawnWith(new[] { "+p5" }));
        // a reflector's head and a turbine's rotor are several meshes now: all of them turn
        ModelEntry reflector = catalog.Entries.Single(e => e.Id == "reflector_box");
        Assert.Equal(4, reflector.Parts.Keys.Count(k => k.StartsWith(ModelRules.SunHead, StringComparison.Ordinal)));
        ModelEntry core = catalog.Entries.Single(e => e.Id == "gas_turbine_core");
        Assert.True(core.Parts["Pitched_Blades"].Spin != 0f && core.Parts["Axial_Hub"].DrawnWith(new[] { "core_on" }) && core.Parts["Pitched_Blades_still"].DrawnWith(Array.Empty<string>()));
        // iv_drip.dm: the bag and its fill only with a container, the fill in the reagents' colour
        ModelEntry drip = catalog.Find("icons/obj/medical/iv_drip.dmi", "iv_drip_injectidle", Array.Empty<string>());
        Assert.True(drip.Parts["IV_Reservoir"].DrawnWith(new[] { "beakeridle" }) && !drip.Parts["IV_Reservoir"].DrawnWith(Array.Empty<string>()));
        Assert.Equal("reagent50", drip.Parts["Container_reagent50"].TintOverlay);
        // the morgue's tray thing is the tray alone
        Assert.Single(catalog.Entries.Single(e => e.Id == "morgue_tray").Parts);
    }

    [Fact]
    public void MeshesThatPlayAsFramesBlinkInTurn()
    {
        ModelPart[] arcs = Enumerable.Range(0, 8).Select(n => Repository().Entries.Single(e => e.Id == "shield_wall_generator").Parts[$"Powered_Arc_{n}"]).ToArray();
        foreach (double t in new[] { 0.05, 0.35, 0.75, 1.15 })
            Assert.Single(arcs, a => a.LitAt(t));
        Assert.True(arcs[3].LitAt(0.35) && arcs[3].LitAt(1.15));
    }

    [Fact]
    public void EveryModelImageIsEmbeddedOrOnATgSheet()
    {
        string root = null;
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null && root == null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "godot", "assets", "models")))
                root = Path.Combine(dir.FullName, "godot", "assets", "models");
        // an image that reads as null draws its parts in a plain white swatch
        foreach (string file in Repository().Entries.SelectMany(e => e.Parts.Values).Select(p => p.File).Where(f => f != null).Distinct())
        {
            GlbFile glb = GlbFile.Read(File.ReadAllBytes(Path.Combine(root, file)), uri => GlbFile.TgIcon(uri) == null ? null : Array.Empty<byte>());
            Assert.True(glb.Images.All(i => i != null), file);
        }
        Assert.Equal("icons/obj/machines/engine/tesla_coil.dmi", GlbFile.TgIcon("../../../tg/icons/obj/machines/engine/tesla_coil.png"));
    }

    [Fact]
    public void AWholeFilePartLeavesOutTheLaterFramesOfItsClips()
    {
        var whole = new ModelPart("f.glb", null, default);
        Assert.True(ModelMesh.Takes(whole, "Dock_Status_Overlay") && ModelMesh.Takes(whole, "Lamp_Frame_0"));
        Assert.False(ModelMesh.Takes(whole, "Dock_Light_Frame_1") || ModelMesh.Takes(whole, "charging_Light_Frame_12"));
        Assert.True(ModelMesh.Takes(new ModelPart("f.glb", "Dock_Light_Frame_1", default), "Dock_Light_Frame_1"));
        // cell_charger.dm: ccharger-oN is drawn by its first frame's mesh, whose live picture plays the overlay's own frames
        ModelEntry charger = Repository().Entries.Single(e => e.Id == "cell_charger_level_4");
        Assert.True(charger.OverlayFrames);
    }

    [Fact]
    public void AnEmptyFaxDrawsItsSheetNotTheLivePicture()
    {
        // fax_idle.glb's uv lie in one frame of fax.dmi's "fax": as a live picture an empty, turned fax lost faces (the owner, 2026-11-07)
        Assert.False(Repository().Entries.Single(e => e.Id == "fax_idle").Parts["model"].Live);
    }

    [Fact]
    public void ANetpodRollsItsShutterUpWhenOpen()
    {
        // netpod.glb's open clip squeezes the door to its top; a scale to zero (a frame shown) is not a squeeze
        ModelEntry pod = Repository().Entries.Single(e => e.Id == "netpod");
        Assert.True(pod.Parts["Chamber_Door"].OpenScale.Y < 0.01f);
        Assert.Equal(System.Numerics.Vector3.One, Repository().Entries.Single(e => e.Id == "destructive_scanner").Parts["Scanning_Grille_0"].OpenScale);
        Assert.DoesNotContain(pod.Parts.Values, p => p.Node?.Contains("_frame_") == true);
    }

    [Fact]
    public void AnOpaquePartDrawsItsFileWithoutTheLivePicture()
    {
        // the breach shield generator's ring tops sample shieldoff's transparent pixels: opaque draws the grey under them (the owner, 2026-11-08)
        ModelPart column = Repository().Entries.Single(e => e.Id == "breach_shield_generator").Parts["model"];
        Assert.True(column.Opaque);
        Assert.False(column.Live);
    }

    [Fact]
    public void ATankHolderDrawsOnlyTheThingItHolds()
    {
        // four held things share the node names Item_*: each part is one thing's, by its material, shown in that thing's state
        ModelEntry holder = Repository().Entries.Single(e => e.Id == "tank_holder");
        Assert.True(holder.Draws("icons/obj/canisters.dmi", "holder") && holder.Draws("icons/obj/canisters.dmi", "holder_extinguisher"));
        ModelPart body = holder.Parts["holder_oxygen_Body"];
        Assert.Equal(("Item_Body", "TG_holder_oxygen"), (body.Node, body.Material));
        Assert.True(body.ShownIn("holder_oxygen"));
        Assert.False(body.ShownIn("holder_extinguisher"));
        Assert.False(holder.Parts["holder_generic_Body"].ShownIn("holder"));
        Assert.True(holder.Parts["model"].ShownIn("holder"));
    }

    [Fact]
    public void ANumberedAnimationIsDrawnByItsFirstFrameOnly()
    {
        // messaging_server.glb exports each state's frames as <state>_N: frame 0's live picture plays them, the others on top of it would fight
        ModelEntry server = Repository().Entries.Single(e => e.Id == "messaging_server");
        Assert.True(server.Parts["message_server_0"].ShownIn("message_server"));
        Assert.False(server.Parts["message_server_0"].ShownIn("message_server_off"));
        Assert.DoesNotContain(server.Parts.Keys, p => System.Text.RegularExpressions.Regex.IsMatch(p, "_[1-9][0-9]*$"));
    }

    [Fact]
    public void AConveyorSwitchLeansItsLeverByItsState()
    {
        ModelEntry[] entries = Repository().Entries.Where(e => e.Family == "conveyor_switch").ToArray();
        Assert.Equal(3, entries.Length);
        Assert.Equal(0f, entries.Single(e => e.States.Contains("switch-off")).Parts["model"].Degrees);
        Assert.Equal(-entries.Single(e => e.States.Contains("switch-fwd")).Parts["Lever"].Axis.Z, entries.Single(e => e.States.Contains("switch-rev")).Parts["Lever"].Axis.Z);
    }

    [Fact]
    public void OnlyABedsheetAndTheCodexAreItemsDrawnByTheirModels()
    {
        // D323: the sheet's colours are all drawn by the one mesh, with the thing's own picture
        Assert.True(Anomaly.Client.Shared.World.PropKinds.ItemHasModel("/obj/item/bedsheet/red") && Anomaly.Client.Shared.World.PropKinds.ItemHasModel("/obj/item/book/codex_gigas"));
        Assert.False(Anomaly.Client.Shared.World.PropKinds.ItemHasModel("/obj/item/book/manual") || Anomaly.Client.Shared.World.PropKinds.ItemHasModel("/obj/item/wrench"));
        Assert.Null(Repository().Entries.Single(e => e.Id == "bedsheet").Parts["model"].LiveFrame(new ModelFrame("sheetwhite", 2, 0, 0, 32, 32)).State);
    }

    [Fact]
    public void AFilledPictureTakesTheNearestSolidColour()
    {
        var picture = new Anomaly.Client.Shared.Icons.Raster(4, 1);
        picture.Set(0, 0, 0x102030FFu);
        picture.Set(3, 0, 0x405060FFu);
        picture.Set(1, 0, 0xC0C0C000u); // a grey stored under a transparent pixel
        picture.FillTransparent();
        Assert.Equal(0x10203000u, picture.Get(1, 0));
        Assert.Equal(0x40506000u, picture.Get(2, 0));
        Assert.True(Repository().Entries.Single(e => e.Id == "emitter").Parts["model"].Fill);
    }
}
