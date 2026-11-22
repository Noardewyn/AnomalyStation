namespace Anomaly.Client.Shared.World;

// Owner: M3 P6. Engine-free. Which shape the client draws for an entity by its DM type (D113, scope 2e).

/// <summary>How the renderer draws an entity (D113: the art level).</summary>
public enum RenderKind : byte
{
    /// <summary>A tg picture on a billboard (a static prop in its chunk's batch, a mover as a node): everything D113 names no shape for.</summary>
    Billboard = 0,

    /// <summary>An airlock: two sliding panels.</summary>
    Airlock,

    /// <summary>A pod door or shutters: a slab that drops.</summary>
    PodDoor,

    /// <summary>A windoor: a panel on one face that slides.</summary>
    Windoor,

    /// <summary>A full-tile window: a glass slab.</summary>
    FullWindow,

    /// <summary>A directional window: a slab on one face.</summary>
    DirectionalWindow,

    /// <summary>A grille: bars.</summary>
    Grille,

    /// <summary>A table: a top on four legs.</summary>
    Table,

    /// <summary>A railing: a thin rail on one face.</summary>
    Railing,

    /// <summary>Plastic flaps: strips on one face.</summary>
    Flap,

    /// <summary>A light fixture: a box on the wall face with the tube or bulb emissive.</summary>
    LightFixture,

    /// <summary>A firelock (M3 R2): a thin slab on each side of the doorway, as tall as the wall; open it shows the frame sprite (the alpha scissor leaves just the frame), closed the whole sprite.</summary>
    Firelock,

    /// <summary>A border-only firelock (<c>ON_BORDER_1</c>, M3 R2): a thin slab on the tile edge its dir faces, like the windoor.</summary>
    FirelockBorder,

    /// <summary>
    /// A girder (M5 slice 4, D142): the full-tile window's joined slab (a post and an arm toward each neighbour it joins, as high as the wall)
    /// showing tg's girder sprite, the straight run of its smoothing states, whose see-through frame the alpha scissor keeps.
    /// </summary>
    Girder,

    /// <summary>
    /// An airlock assembly (M5 slice 6, D142): the airlock's two panels standing closed across the passage, showing the assembly's own
    /// picture (tg's "construction" frame with its fill or glass and the panel overlay of its construction state, door_assembly.dm:386).
    /// It is not a door (nothing slides) and not a frame windows join (tg's assembly does not smooth).
    /// </summary>
    DoorAssembly,

    /// <summary>
    /// A false wall (false_walls.dm, the owner 2026-10-17: it showed as a billboard): shut, a wall block as tg draws it (its fake icon, wall.dmi's smoothing states),
    /// the sides tg's straight run as every wall's (D121), the top its junction. Open, opening or closing it is tg's own frame sprite (<see cref="IsOpenFalseWallState"/>).
    /// </summary>
    FalseWall,

    /// <summary>
    /// A curtain (the owner, 2026-10-16: not a billboard): tg's cloth hanging across its tile (curtains.dm), a thin sheet as tall as the wall across the passage as a door
    /// stands, showing its own picture (open or closed). One in a window block's cell hangs on both faces of the glass, as the shutters do.
    /// </summary>
    Curtain,
}

/// <summary>The classification of DM type paths for the renderer.</summary>
public static class PropKinds
{
    /// <summary>The shape of an entity of this type (<paramref name="onBorder"/> is <c>ON_BORDER_1</c>).</summary>
    public static RenderKind Classify(string typePath)
    {
        if (typePath == null)
            return RenderKind.Billboard;
        if (Is(typePath, "/obj/machinery/door/window"))
            return RenderKind.Windoor;
        // M3 R2: a firelock is a 3D piece too (it was a billboard turned to the camera, which drew the frame as a flat picture).
        if (Is(typePath, "/obj/machinery/door/firedoor/border_only"))
            return RenderKind.FirelockBorder;
        if (Is(typePath, "/obj/machinery/door/firedoor"))
            return RenderKind.Firelock;
        // tg's holofirelock ("a holographic barrier resembling a firelock", holosign.dm: the tram's tunnel barriers too): its picture on a firelock's slabs
        if (Is(typePath, "/obj/structure/holosign/barrier/atmos"))
            return RenderKind.Firelock;
        if (Is(typePath, "/obj/machinery/door/poddoor"))
            return RenderKind.PodDoor;
        // The airlock and every other door of tg's door family (the morgue door, the shuttle and puzzle doors): a pair of sliding panels. No door is a billboard.
        if (Is(typePath, "/obj/machinery/door"))
            return RenderKind.Airlock;
        if (Is(typePath, "/obj/structure/window"))
            return IsFullTileWindow(typePath) ? RenderKind.FullWindow : RenderKind.DirectionalWindow;
        if (Is(typePath, "/obj/structure/grille"))
            return RenderKind.Grille;
        if (IsTable(typePath))
            return RenderKind.Table;
        if (Is(typePath, "/obj/structure/railing"))
            return RenderKind.Railing;
        if (Is(typePath, "/obj/structure/plasticflaps"))
            return RenderKind.Flap;
        if (Is(typePath, "/obj/machinery/light"))
            return RenderKind.LightFixture;
        if (Is(typePath, "/obj/structure/girder"))
            return RenderKind.Girder;
        if (Is(typePath, "/obj/structure/door_assembly"))
            return RenderKind.DoorAssembly;
        if (Is(typePath, "/obj/structure/curtain"))
            return RenderKind.Curtain;
        if (Is(typePath, "/obj/structure/falsewall"))
            return RenderKind.FalseWall;
        // tg's tram wall (tram_structures.dm: titanium glass panels, SMOOTH_BITMASK as a window): a block of its smoothing states, as a full-tile window
        // (the owner, 2026-11-19: tram walls are cubes); its spoiler is a model of its own
        if (Is(typePath, "/obj/structure/tram") && !Is(typePath, "/obj/structure/tram/spoiler"))
            return RenderKind.FullWindow;
        return RenderKind.Billboard;
    }

    /// <summary>
    /// M5 slice 5 (D142): the shape of an entity from its type and its icon state. A broken grille (tg's <c>brokengrille</c>, grille.dm:51-53, density 0) is not
    /// a joined grille piece: it stands as tg's own sprite in its tile, and the windows and grilles beside it do not join it.
    /// </summary>
    public static RenderKind Classify(string typePath, string iconState)
    {
        RenderKind shape = Classify(typePath);
        // D152 (block windows): a broken grille keeps its block, its sides showing tg's broken bars (the owner, 2026-10-17: as a billboard it read as a wall);
        // it is still not dense (the server's tg rule)
        if (shape == RenderKind.FalseWall && IsOpenFalseWallState(iconState))
            return RenderKind.Billboard;
        return shape == RenderKind.Grille && IsBrokenGrilleState(iconState) && !WorldScale.BlockPieces ? RenderKind.Billboard : shape;
    }

    /// <summary>tg's icon states of a false wall that is not shut: <c>[base]-open</c>, <c>-opening</c>, <c>-closing</c> (false_walls.dm:80-90).</summary>
    public static bool IsOpenFalseWallState(string iconState)
        => iconState != null && (iconState.EndsWith("-open", StringComparison.Ordinal) || iconState.EndsWith("-opening", StringComparison.Ordinal) || iconState.EndsWith("-closing", StringComparison.Ordinal));

    /// <summary>tg's icon state of a broken grille: <c>"broken" + base_icon_state</c> (grille.dm:53).</summary>
    public static bool IsBrokenGrilleState(string iconState) => iconState != null && iconState.StartsWith("broken", StringComparison.Ordinal);

    /// <summary>True when <paramref name="path"/> is <paramref name="prefix"/> or one of its subtypes (a whole path segment: <c>/obj/machinery/light_switch</c> is not a light).</summary>
    public static bool Is(string path, string prefix)
        => path.StartsWith(prefix, StringComparison.Ordinal) && (path.Length == prefix.Length || path[prefix.Length] == '/');

    /// <summary>A door that fills its tile's doorway (an airlock or any other door of the family, a pod door or shutters): not a windoor (a border pane) and not a firelock (which stands beside another door).</summary>
    public static bool IsDoor(string path)
        => Is(path, "/obj/machinery/door") && !Is(path, "/obj/machinery/door/window") && !Is(path, "/obj/machinery/door/firedoor");

    /// <summary>
    /// A door of tg's door family for the question "which way does it close the passage": an airlock or any other door, a pod door, shutters, and a firelock that fills its
    /// tile; not a windoor and not a border firelock, which are border objects and keep their dir (M3 R3).
    /// </summary>
    public static bool IsPassageDoor(string path)
        => Is(path, "/obj/machinery/door") && !Is(path, "/obj/machinery/door/window") && !Is(path, "/obj/machinery/door/firedoor/border_only");

    /// <summary>A grille (M3 R3: a grille joins the windows and walls beside it).</summary>
    public static bool IsGrille(string path) => Is(path, "/obj/structure/grille");

    /// <summary>
    /// A thing a hand can hold (D121, the owner: <c>/obj/item</c>): drawn at <see cref="WorldScale.ItemScale"/> of the tile scale, its picture's bottom on its support. A wall-mounted
    /// item (an intercom) is a decal first and never reaches the item rule.
    /// </summary>
    public static bool IsItem(string path) => Is(path, "/obj/item");

    /// <summary>D323: the items drawn on the floor by their 3D models; every other item lying about is a billboard (D104).</summary>
    public static bool ItemHasModel(string path) => Is(path, "/obj/item/bedsheet") || Is(path, "/obj/item/book/codex_gigas");

    /// <summary>
    /// tg's floor-mounted atmospherics ends that show above the floor (D121, the owner: "the hatches lie on the floor"): the air vents (<c>vent_pump</c>), the scrubbers
    /// (<c>vent_scrubber</c>), the passive vents, the outlet injectors and the dual-port vent of the atmos room (<c>dp_vent_pump</c>). They are drawn as a horizontal
    /// picture a hair above the floor, turned by their dir. The other atmospherics machines (pumps, valves, filters, tanks, thermomachines, portable connectors, airlock pumps) stand up.
    /// The atmospheric shield generators (atmos_field_gen.dm) lie flat too (the owner, 2026-10-16: standing they read as a strange billboard): tg draws one as a thin
    /// emitter strip along the tile edge its dir faces, mapped in pairs in a mass driver's doorway.
    /// </summary>
    public static bool IsFloorHatch(string path)
    {
        const string unary = "/obj/machinery/atmospherics/components/unary";
        return Is(path, unary + "/vent_pump") || Is(path, unary + "/vent_scrubber") || Is(path, unary + "/passive_vent") || Is(path, unary + "/outlet_injector")
            || Is(path, "/obj/machinery/atmospherics/components/binary/dp_vent_pump") || Is(path, "/obj/machinery/atmos_shield_gen")
            || Is(path, "/obj/machinery/air_sensor") || Is(path, "/obj/structure/fans") || Is(path, "/obj/structure/water_source/puddle") || Is(path, "/obj/structure/steam_vent");
    }

    /// <summary>
    /// tg's tile marks seen from above (DV383): the greenscreen camera's mark on the tile it records (status_display.dm:902), a translucent square outlined
    /// in yellow. It lies on its tile over the floor's own things; standing up it was a pane where the person on the greenscreen stands.
    /// </summary>
    public static bool IsFloorMark(string path) => Is(path, "/obj/effect/abstract/greenscreen_location_indicator");

    /// <summary>
    /// The families that carry tg's undertile element (code/datums/elements/undertile.dm, D122): cables, disposal pipes, pipes, ducts, wire terminals and navigation beacons. Under
    /// an intact floor tile the element sets their invisibility, so the real server does not replicate them; plating, space, catwalk floors and glass floors show them
    /// (<see cref="TileAccess"/>). The fake feed does what the server does from the turf (<c>FakeMap</c>).
    /// </summary>
    public static bool HasUndertile(string path)
    {
        foreach (string prefix in UndertileFamilies)
        {
            if (Is(path, prefix))
                return true;
        }
        return false;
    }

    private static readonly string[] UndertileFamilies =
    {
        "/obj/structure/cable", "/obj/structure/disposalpipe", "/obj/structure/disposalconstruct", "/obj/machinery/atmospherics/pipe", "/obj/machinery/duct",
        "/obj/machinery/power/terminal", "/obj/machinery/navbeacon", "/obj/machinery/gigabeacon",
    };

    /// <summary>tg's <c>underfloor_accessibility</c> of a turf (code/__DEFINES/turfs.dm): what shows of the things under its floor.</summary>
    public static class TileAccess
    {
        /// <summary>An intact floor tile: everything under it is invisible (the turf's default).</summary>
        public const int Hidden = 0;

        /// <summary>A glass floor, a covered catwalk floor: seen through it, not reachable.</summary>
        public const int Visible = 1;

        /// <summary>Plating, space and the misc turfs: seen and reachable.</summary>
        public const int Interactable = 2;

        /// <summary>Whether the undertile element leaves a thing visible on a turf of this accessibility (<c>underfloor_accessibility &gt;= UNDERFLOOR_VISIBLE</c>).</summary>
        public static bool Shows(int accessibility) => accessibility >= Visible;
    }

    /// <summary>
    /// Flat floor equipment that tg draws mid-frame in the top-down view (the icon has 4 to 7 clear rows under what is drawn, which at 2 m per 32 pixels held a holopad 0.4 m above the
    /// floor): the picture rests on the floor instead (M3 R3, "check that machines and structures stand on the floor"). The things that stand up (a disposal bin, a toilet) keep their
    /// picture's place, and the wall fixtures (a sink, a shower) are mounted high on purpose. M3 R5 (D122): the holopad, the navigation beacon, the AI slipper and the igniter are on tg's
    /// floor plane, so they lie flat (<see cref="FloorLayers"/>) whenever the appearance says its plane and layer; this list is what they were before, and what a feed that sends
    /// neither still gets.
    /// </summary>
    public static bool RestsOnFloor(string path)
    {
        foreach (string prefix in FlatFloorEquipment)
        {
            if (Is(path, prefix))
                return true;
        }
        return false;
    }

    private static readonly string[] FlatFloorEquipment =
    {
        "/obj/machinery/holopad", "/obj/machinery/navbeacon", "/obj/machinery/ai_slipper", "/obj/machinery/igniter", "/obj/structure/marker_beacon",
        "/obj/machinery/power/solar", "/obj/machinery/power/tracker", "/obj/machinery/hydroponics/soil",
    };

    /// <summary>A full-tile window (tg's <c>fulltile</c> subtypes, which the map's window spawners make).</summary>
    public static bool IsFullTileWindow(string path)
        => Is(path, "/obj/structure/window") && (path.Contains("fulltile", StringComparison.Ordinal) || path.Contains("/shuttle", StringComparison.Ordinal)); // shuttle windows are fulltile = TRUE without the word in their path

    /// <summary>A table.</summary>
    public static bool IsTable(string path) => Is(path, "/obj/structure/table");

    /// <summary>The smoothing family of a table type: tables join tables of the same family.</summary>
    public static string TableFamily(string path)
    {
        const string prefix = "/obj/structure/table";
        return path.Length == prefix.Length ? string.Empty : path.Substring(prefix.Length + 1).Split('/')[0];
    }

    /// <summary>
    /// The structures a map spawner makes at init (the real server's init replaces them; the scripted map does it up front so that
    /// a window has a picture and faces): a window spawner is a full-tile window and a grille. Every other path is itself.
    /// </summary>
    public static IEnumerable<string> ExpandSpawner(string path)
    {
        const string spawner = "/obj/effect/spawner/structure/window";
        if (!Is(path, spawner))
        {
            yield return path;
            yield break;
        }
        if (path.Contains("hollow", StringComparison.Ordinal))
            yield break; // the hollow spawners make border windows by direction: not needed for the scripted map
        string rest = path.Substring(spawner.Length);
        bool reinforced = rest.Contains("reinforced", StringComparison.Ordinal) || rest.Contains("plasma", StringComparison.Ordinal) || rest.Contains("shuttle", StringComparison.Ordinal);
        // tg's tinted spawner makes the tinted full-tile window (spawners/structure.dm)
        yield return rest.Contains("tinted", StringComparison.Ordinal) ? "/obj/structure/window/reinforced/tinted/fulltile"
            : reinforced ? "/obj/structure/window/reinforced/fulltile" : "/obj/structure/window/fulltile";
        yield return "/obj/structure/grille";
    }

    /// <summary>
    /// Whether the type is wall furniture: drawn on the wall face its <c>dir</c> points at even when it has no pixel offset (tg's lights and
    /// cameras have none: <c>MAPPING_DIRECTIONAL_HELPERS(path, 0)</c>). An object with a big pixel offset toward a wall is a mount by that offset
    /// (<see cref="WallMounts.TryPlace"/>), whatever its family. A <c>/directional/</c> path alone proves nothing: tg's stools, sinks,
    /// showers and windoors have the helpers with their own, small or zero, offsets (M3 R2).
    /// </summary>
    public static bool IsWallMountType(string path)
    {
        foreach (string prefix in WallFurniture)
        {
            if (Is(path, prefix))
                return true;
        }
        return false;
    }

    /// <summary>
    /// A type that stands on the floor, never flat on a wall, however big its pixel offset or however it faces: seats, beds, sinks, showers,
    /// storage and stairs (tg's showers and sinks are offset 14 to 16 pixels toward the wall they stand against, which is where they stand, not a
    /// picture on it).
    /// </summary>
    public static bool IsNeverDecal(string path)
    {
        foreach (string prefix in NeverDecal)
        {
            if (Is(path, prefix))
                return true;
        }
        return false;
    }

    private static readonly string[] NeverDecal =
    {
        "/obj/structure/chair", "/obj/structure/bed", "/obj/structure/table", "/obj/structure/sink", "/obj/machinery/shower", "/obj/structure/closet",
        "/obj/structure/rack", "/obj/structure/steps", "/obj/structure/fake_stairs", "/obj/structure/toilet",
    };

    /// <summary>
    /// Whether a thing of this type that shares a tile with a table stands on the table top: the items and the small machines tg's maps put
    /// on tables (a microwave, a coffee maker), and the few small structures (a desk bell, a microscope, a serving dish, the newsfeed TV). The other
    /// structures stay on the floor (a sign is a mount, a chair never shares a table's tile).
    /// </summary>
    public static bool StandsOnTable(string path)
        => Is(path, "/obj/item") || Is(path, "/obj/machinery") || Is(path, "/obj/structure/desk_bell") || Is(path, "/obj/structure/microscope")
            || Is(path, "/obj/structure/reagent_dispensers/servingdish") || Is(path, "/obj/structure/showcase/machinery/tv");

    /// <summary>
    /// Wall furniture that stands out of the wall face instead of lying flat on it (<see cref="Placed.Fin"/>): tg's security cameras, whose top-down
    /// picture is a camera on an arm seen from above (the owner's play test 2026-10-14: flat on the wall its pan looked like the picture spinning).
    /// </summary>
    // DEVIATION(DV35): tg draws the camera flat in the top-down view; the port stands it out of the wall.
    public static bool IsWallFin(string path) => Is(path, "/obj/machinery/camera");

    private static readonly string[] WallFurniture =
    {
        "/obj/structure/sign", "/obj/machinery/light", "/obj/machinery/power/apc", "/obj/machinery/airalarm", "/obj/machinery/firealarm",
        "/obj/machinery/button", "/obj/machinery/light_switch", "/obj/machinery/camera", "/obj/structure/extinguisher_cabinet",
        "/obj/item/radio/intercom", "/obj/machinery/newscaster", "/obj/machinery/status_display", "/obj/machinery/requests_console",
        "/obj/structure/noticeboard", "/obj/machinery/defibrillator_mount", "/obj/structure/light_construct",
        "/obj/machinery/flasher", "/obj/structure/mirror", "/obj/structure/urinal", "/obj/structure/secure_safe", "/obj/structure/fireaxecabinet",
        "/obj/machinery/door_buttons", "/obj/machinery/airlock_controller", "/obj/machinery/airlock_sensor",
        "/obj/machinery/keycard_auth", "/obj/machinery/turretid", "/obj/machinery/igniter", "/obj/machinery/barsign",
        "/obj/machinery/computer/security/telescreen", "/obj/machinery/entertainment", "/obj/machinery/ticket_machine",
        "/obj/machinery/digital_clock", "/obj/machinery/wall_healer",
        "/obj/machinery/transport/destination_sign", "/obj/machinery/transport/tram_controller",
    };

    /// <summary>
    /// The height (metres above the floor) of the centre of a wall-mounted picture, from a small table by type prefix (scope 2e). The
    /// heights are shares of the wall (<see cref="WorldScale"/>, D119): at 2 m lights are 1.85 m, signs 1.5 m, buttons, APCs and alarms 1.1 m,
    /// wall cameras 1.7 m and everything else 1.2 m. A picture taller than what fits is moved by <see cref="WorldScale.FitMount"/>.
    /// </summary>
    public static float MountHeight(string path)
    {
        if (Is(path, "/obj/machinery/light") || Is(path, "/obj/structure/light_construct")) // an unbuilt fixture hangs where its lamp will
            return WorldScale.LampHeight;
        if (Is(path, "/obj/structure/sign") || Is(path, "/obj/structure/noticeboard"))
            return WorldScale.SignHeight;
        if (Is(path, "/obj/machinery/lift_indicator"))
            return WorldScale.LiftIndicatorHeight;
        if (Is(path, "/obj/machinery/button/elevator"))
            return WorldScale.ElevatorButtonHeight;
        if (Is(path, "/obj/machinery/button") || Is(path, "/obj/machinery/power/apc") || Is(path, "/obj/machinery/airalarm")
            || Is(path, "/obj/machinery/firealarm") || Is(path, "/obj/machinery/light_switch") || Is(path, "/obj/machinery/newscaster")
            || Is(path, "/obj/machinery/requests_console") || Is(path, "/obj/item/radio/intercom"))
            return WorldScale.PanelHeight;
        if (Is(path, "/obj/machinery/camera"))
            return WorldScale.CameraHeight;
        return WorldScale.OtherMountHeight;
    }
}
