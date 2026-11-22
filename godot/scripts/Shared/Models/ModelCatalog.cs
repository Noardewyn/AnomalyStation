using System.Linq;
using System.IO;
using System.Text.Json;

namespace Anomaly.Client.Shared.Models;

// Engine-free. The catalog of the 3D models that replace tg's 2D pictures (docs/ai/specs/models.md): godot/assets/models/models.json. One entry says which icons (and
// states) a model draws, the rule that picks its variants and turns them, where its texture comes from, how its source frame fits the world, and its parts (a node of
// a .glb file, or all of the file). The renderer reads it once; adding a model is an entry and its files, no code, as long as one of the rules fits.

/// <summary>How a model picks its parts and turns them.</summary>
public enum ModelRule : byte
{
    /// <summary>One part (<c>model</c>), not turned.</summary>
    Single,

    /// <summary>One part (<c>model</c>) turned by the entity's dir; the model's front looks south (+Z) unturned.</summary>
    Dir4,

    /// <summary>
    /// An airlock: its parts (two leaves, four hatch sections, a frame and two leaves) turned to the door's face (across its passage); each part with a
    /// <see cref="ModelPart.Slide"/> slides by it at the door's open fraction (a frame has none and stays).
    /// </summary>
    Airlock,

    /// <summary>
    /// A smoothing structure that joins its neighbours (tg's bitmask smoothing, a table): its icon state ends with tg's junction (<c>table-38</c>); the four cardinal
    /// joins pick one of seven modules turned in quarter turns (<see cref="ModelRules.Smooth4"/>), a corner by whether its diagonal is joined too.
    /// </summary>
    Smooth4,

    /// <summary>
    /// tg's closet (closets.dm): a body (<c>body</c>) and a door on its hinge (<c>doorFront</c>, <c>doorInside</c>), turned away from the wall behind it. The door swings
    /// open by <see cref="ModelEntry.OpenDegrees"/> over <see cref="ModelEntry.OpenSeconds"/> (animate_door) when the appearance shows the closet open; things on its tile
    /// rest on its shelves.
    /// </summary>
    Closet,

    /// <summary>
    /// tg's crate (crates.dm): every part is drawn, the parts with a hinge (<see cref="ModelPart.Degrees"/>) swing open while the crate is open (its icon state is
    /// <c>[base]open</c>, or <see cref="ModelEntry.OpenStates"/>, or it has one of <see cref="ModelEntry.OpenOverlays"/>), each part shows the picture
    /// <see cref="ModelPart.Picture"/> names. Things on its tile rest inside it.
    /// </summary>
    Crate,

    /// <summary>
    /// tg's computer (_computer.dm): a body (<c>body</c>, the icon state) and its two overlays as parts (<c>screen</c>: <c>icon_screen</c>, drawn only while the
    /// appearance has it, unpowered there is none; <c>keyboard</c>: <c>icon_keyboard</c> or its <c>_off</c>), turned by the entity's dir.
    /// </summary>
    Console,

    /// <summary>
    /// A thing of tg's eight directions (a conveyor, a railing): a cardinal dir draws <c>model</c> (its front south unturned, as <see cref="Dir4"/>), a diagonal one
    /// <c>corner</c> (made for southeast) turned from there.
    /// </summary>
    Dir8,

    /// <summary>
    /// A wall fixture (a lamp): <c>model</c>, its back on the wall at -Z unturned (the north wall), on the wall the planner puts the thing on, at its mount height and
    /// its offset along the wall (<see cref="ModelEntry.Offset"/> then takes it to the wall's face).
    /// </summary>
    Wall,

    /// <summary>
    /// An overlay drawn as a model on the thing it is stuck to (C-4's charge, <c>plastic-explosive2</c>): <c>model</c>, its back at -Z unturned, on the face of
    /// a wall the overlay's dir names, on the side of a door, or lying on a floor; the overlay is then not drawn in the picture (docs/ai/specs/models.md).
    /// </summary>
    Attach,

    /// <summary>
    /// tg's solar panel and tracker (solar.dm, tracker.dm): tg turns the glass and the dish, overlays in the thing's <c>vis_contents</c>, toward the sun by their transform
    /// (<c>get_panel_transform</c>). The parts named <c>head...</c> are the turning head: they turn about the tile's vertical axis to the azimuth that the transform of the
    /// appearance's overlay whose state starts with <see cref="ModelEntry.Azimuth"/> carries (<see cref="ModelRules.AzimuthYaw"/>); the others (the pedestal) stand.
    /// </summary>
    Sun,
}

/// <summary>Where a model's faces take their colours.</summary>
public enum ModelTexture : byte
{
    /// <summary>The model's own embedded texture (one image per model), in the model's uv.</summary>
    Model,

    /// <summary>
    /// The entity's composed picture, with tg's overlays: every face takes the picture's pixel it stands on in the front view of the source frame (a door whose
    /// lights, panel and weld are tg's overlays).
    /// </summary>
    Sprite,

    /// <summary>
    /// A cell of a tg icon sheet: the model's uv point into one icon state's frame of the sheet it embeds (<see cref="ModelEntry.CellWidth"/> by
    /// <see cref="ModelEntry.CellHeight"/> pixels); each part is given the picture of the state its rule picks (a closet's body, door and door back).
    /// </summary>
    Cell,
}

/// <summary>
/// A part of a model: a .glb file (relative to the models folder) and optionally one node of it; a leaf's slide (source metres, x across the door and y up, when
/// fully open); the materials it takes (those whose name starts with <paramref name="Material"/>; null: all); whether it is built about its node's origin (a
/// door on its hinge, which the renderer turns).
/// </summary>
/// <summary>
/// A part's motion while tg flicks one of <see cref="States"/> on its thing (flick(state, atom)): it goes toward its farthest pose (<see cref="Move"/>, source metres;
/// <see cref="Degrees"/> about its part's axis; <see cref="Scale"/>, about its pivot) on the flick's frames whose <see cref="Frames"/> fraction is 1 and back on
/// those at 0, the whole way in <see cref="Seconds"/>; without frames, over the whole flick.
/// </summary>
public sealed record ModelFlick(string[] States, float[] Frames, float Seconds, System.Numerics.Vector3 Move, float Degrees, System.Numerics.Vector3 Scale)
{
    /// <summary>The pose the part goes toward on frame <paramref name="frame"/> of the flick (0: at rest, 1: its farthest).</summary>
    public float TargetAt(int frame) => Frames.Length == 0 ? 1f : frame >= 0 && frame < Frames.Length ? Frames[frame] : 0f;
}

public sealed record ModelPart(string File, string Node, System.Numerics.Vector2 Slide, string Material = null, bool Pivot = false)
{
    /// <summary>A crate's part: the axis it swings about on its pivot (Godot's axes, the model unturned) and how far (degrees; 0: it does not move).</summary>
    public System.Numerics.Vector3 Axis { get; init; } = System.Numerics.Vector3.UnitX;

    public float Degrees { get; init; }

    /// <summary>Degrees this part is turned by besides its rule's turn (a kit whose module joins north where the rule's joins east: -90).</summary>
    public float Turn { get; init; }

    /// <summary>
    /// The picture a part shows. A crate's: <c>closed</c> (the crate's closed state; its composed picture, with tg's overlays, while it is shut), <c>open</c> (the
    /// open state), or an icon state of the sheet. A sprite-textured part's: <c>closed</c> for the entity's composed picture, else a state of its icon (an
    /// external airlock's frame shows <c>open</c>).
    /// </summary>
    public string Picture { get; init; } = "closed";

    /// <summary>For a model-textured surface, use the entity's composed appearance regardless of the authored frame's state.
    /// Screens keep their original UV rectangle while programs, power and damage overlays change.</summary>
    public ModelFrame LiveFrame(ModelFrame authored)
        => Picture == "appearance" && authored != null ? authored with { State = null } : authored;

    /// <summary>Its name in the entry's parts (a smoothing kit's module: <c>single</c>, <c>end</c>, ...).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// A single model's extra part: overlay states of which the appearance must have one for it to be drawn (empty: no condition), and those it must have none
    /// of (a canister's pressure lights, tg's <c>can-0</c> .. <c>can-3</c>; its held tank and open door, <c>can-open</c>, and its shut door without it).
    /// </summary>
    public string[] Overlays { get; init; } = Array.Empty<string>();

    public string[] ExceptOverlays { get; init; } = Array.Empty<string>();

    /// <summary>Every listed overlay must be present (independent tool, damage and lock combinations).</summary>
    public string[] AllOverlays { get; init; } = Array.Empty<string>();

    /// <summary>Icon states the thing must show for the part to be drawn (empty: any), and those it must not (a heater's core panel per tg's mode).</summary>
    public string[] States { get; init; } = Array.Empty<string>();

    public string[] ExceptStates { get; init; } = Array.Empty<string>();

    /// <summary>
    /// DEVIATION(DV402, the owner, 2026-11-12): a multi-deck hub's part drawn by where its link goes (<c>"deck"</c>): <c>"up"</c> while a thing of the same model stands on the
    /// tile above, or none below (a lone hub keeps its column); <c>"down"</c> while one stands only below (the flat floor fitting on the deck the link leaves); null: always.
    /// </summary>
    public string Deck { get; init; }

    /// <summary>True when the part is drawn on a hub linked to the deck above (<paramref name="above"/>) and below (<paramref name="below"/>) as <see cref="Deck"/> says.</summary>
    public bool ShownOnDeck(bool above, bool below) => Deck switch
    {
        "up" => above || !below,
        "down" => below && !above,
        _ => true,
    };

    /// <summary>The node's scale in place of its rest pose's (a cyborg charger's shutter, kept rolled up in the file and drawn let down); null: its own.</summary>
    public System.Numerics.Vector3? NodeScale { get; init; }

    /// <summary>A pivoted part's slide while the model is open, in source metres (a drawer, a morgue tray), with or besides its swing.</summary>
    public System.Numerics.Vector3 Move { get; init; }

    /// <summary>A pivoted part's scale while the model is open, about its pivot (<c>"openScale"</c>: a netpod's shutter rolled up, a scanner's tube let down); one: none.</summary>
    public System.Numerics.Vector3 OpenScale { get; init; } = System.Numerics.Vector3.One;

    /// <summary>The part's texture drawn without its alpha (<c>"opaque"</c>: glTF's OPAQUE, as Blender shows it), from the file, not the live picture: faces whose
    /// uv reach a sprite's transparent pixels (a shield generator's ring tops) show the colour stored under them instead of a hole.</summary>
    public bool Opaque { get; init; }

    /// <summary>With <see cref="Opaque"/>: a see-through pixel takes the nearest solid pixel's colour instead of the one stored under it (<c>"fill"</c>: faces whose uv stray past a dark sprite's edge).</summary>
    public bool Fill { get; init; }

    /// <summary>A pivoted part moved while tg flicks one of its states (<c>"flick"</c>: a mass driver's carriage thrown out, an outlet's doors); null: none.</summary>
    public ModelFlick Flick { get; init; }

    /// <summary>A pivoted part's steady turn about its <see cref="Axis"/>, degrees per second of the renderer's clock (a gravity generator's field ring); 0: none.</summary>
    public float Spin { get; init; }

    /// <summary>False for authored surfaces that must retain their own pixels, including hinged cabinet glass.</summary>
    public bool Live { get; init; } = true;

    /// <summary>Select one of several identically named overlays by its source pixel_w (clock digits, reagent bars).</summary>
    public short? OverlayPixelW { get; init; }

    /// <summary>Translation in source metres when reusing a part in another display slot.</summary>
    public System.Numerics.Vector3 Offset { get; init; }

    /// <summary>A pivoted part's hinge in source metres, when its node's origin is not on it (a door's glass that swings with the door's ring).</summary>
    public System.Numerics.Vector3? PivotAt { get; init; }

    /// <summary>Optional light strength override and overlays that enable the part's authored emission.</summary>
    public float? Glow { get; init; }
    public string[] GlowOverlays { get; init; } = Array.Empty<string>();

    /// <summary>Optional sRGB colour override for an untextured part and its local light.</summary>
    public System.Numerics.Vector3? Color { get; init; }

    /// <summary>A blinking indicator's dark colour; null hides its geometry during the off phase.</summary>
    public System.Numerics.Vector3? OffColor { get; init; }

    /// <summary>
    /// A part that blinks (the catalog's <c>"blink": [on, off]</c>, seconds; 0: steady): shown for <c>on</c>, hidden for <c>off</c>, as a tg state whose frames
    /// alternate with an empty one (a canister's <c>can-0</c>: two frames of 2 deciseconds, the second empty).
    /// </summary>
    public float BlinkOn { get; init; }

    public float BlinkOff { get; init; }

    /// <summary>Seconds into the cycle its <c>on</c> starts (the catalog's third <c>"blink"</c> value): meshes that play as a state's frames blink in turn.</summary>
    public float BlinkAt { get; init; }

    /// <summary>
    /// A part whose <c>Tint_</c> materials take a colour of the thing's (<see cref="ModelColors.Swatch"/>): <see cref="TintIcon"/>, the main colour of the
    /// appearance's overlay of that icon (DV73: a canister's held tank, <c>icons/obj/canisters.dmi</c>); <see cref="TintVisuals"/>, the pictures inside
    /// the overlay of that icon state, each by its alpha (tg's update_window: the gases in a canister's <c>window-base</c>). Null: neither.
    /// </summary>
    public string TintIcon { get; init; }

    public string TintVisuals { get; init; }

    /// <summary>The replicated colour of this overlay state, applied to the part's Tint_ materials.</summary>
    public string TintOverlay { get; init; }

    /// <summary>A small local light controlled by this part's visibility and blink timeline.</summary>
    public ModelPointLight Light { get; init; }

    /// <summary>
    /// The shader a part is drawn by instead of the prop shader (<c>"shader"</c>): <c>crystal</c>, a see-through gem that bends the room behind it, shimmers and
    /// glows by itself (the supermatter, whose sprite tg draws on its emissive plane); <c>field</c>, a see-through energy box (its causality field); <c>nozzle</c>, a blue self-lit energy core that stays blue (a propulsion engine's). Null: the prop shader.
    /// </summary>
    public string Shader { get; init; }

    /// <summary>The <see cref="Shader"/> names: a crystal, a field.</summary>
    public const string CrystalShader = "crystal", FieldShader = "field", NozzleShader = "nozzle";

    /// <summary>The most frames a <see cref="Pulse"/> has (the shaders' arrays).</summary>
    public const int MaxPulse = 8;

    /// <summary>
    /// The frames of the tg icon state a <see cref="Shader"/> part plays (<c>"pulse": [[seconds, strength], [seconds, strength, r, g, b], ...]</c>), looped by the
    /// shader's clock: each frame's length (tg's delay), its strength (a crystal's glow, a field's alpha: tg's frames' emissive or alpha, the brightest 1) and its
    /// colour as a factor on the part's own (the supermatter's sm-glow frames). Empty: steady.
    /// </summary>
    public ModelPulse[] Pulse { get; init; } = Array.Empty<ModelPulse>();

    /// <summary>A crystal's halo at its silhouette, sRGB 0..1 (<c>"halo": "#9a0c06"</c>: tg's red glow round sm-glow); null: its own colour, lighter.</summary>
    public System.Numerics.Vector3? Halo { get; init; }

    /// <summary>Whether a blinking part shows at <paramref name="seconds"/> of the renderer's clock (a steady one always does).</summary>
    public bool LitAt(double seconds)
        => BlinkOn <= 0f || BlinkOff <= 0f || (seconds + BlinkOn + BlinkOff - BlinkAt) % (BlinkOn + BlinkOff) < BlinkOn;

    /// <summary>True when the part is drawn on an appearance with these overlay states (<see cref="Overlays"/>, <see cref="ExceptOverlays"/>).</summary>
    public bool DrawnWith(IReadOnlyCollection<string> overlays)
        => (Overlays.Length == 0 || Overlays.Any(overlays.Contains)) && AllOverlays.All(overlays.Contains) && !ExceptOverlays.Any(overlays.Contains);

    /// <summary>True when the part is drawn on a thing showing <paramref name="state"/> (<see cref="States"/>, <see cref="ExceptStates"/>).</summary>
    public bool ShownIn(string state)
        => (States.Length == 0 || States.Contains(state)) && !ExceptStates.Contains(state);

    /// <summary>
    /// The BYOND dirs the part is drawn facing (<c>"dirs"</c>); empty: every dir. A part offset that tg shifts along the map's axes, not the thing's (a pipe's layer
    /// shift, PIPING_LAYER_SHIFT), takes one part per dir.
    /// </summary>
    public int[] Dirs { get; init; } = Array.Empty<int>();

    /// <summary>True when the part is drawn on a thing facing <paramref name="dir"/> (0: south, as the rules take it).</summary>
    public bool ShownFacing(byte dir) => Dirs.Length == 0 || Dirs.Contains(dir == 0 ? 2 : dir);
}

/// <summary>A model indicator's local spill; position and range are in the asset's source metres.</summary>
public sealed record ModelPointLight(System.Numerics.Vector3 Position, System.Numerics.Vector3 Color, float Range, float Energy);

/// <summary>One frame of a <see cref="ModelPart.Pulse"/>: how long it lasts (seconds), its strength and its colour factor.</summary>
public readonly record struct ModelPulse(float Seconds, float Strength, System.Numerics.Vector3 Scale);

/// <summary>One catalog entry.</summary>
public sealed class ModelEntry
{
    /// <summary>Variants prepared together on first use, before an in-game state switch.</summary>
    public string Family = string.Empty;

    /// <summary>Play each matching overlay surface's own animation, independently of the body.</summary>
    public bool OverlayFrames;
    public string Id = string.Empty;

    /// <summary>The icons it draws: an exact icon path, or a folder ending with '/' for every icon in it.</summary>
    public string[] Icons = Array.Empty<string>();

    /// <summary>Icons it does not draw although <see cref="Icons"/> names their folder.</summary>
    public string[] ExceptIcons = Array.Empty<string>();

    /// <summary>The icon states it draws (empty: any).</summary>
    public string[] States = Array.Empty<string>();

    /// <summary>Untextured materials are authored flat sRGB colours, even when their names lack Flat.</summary>
    public bool FlatColors;

    /// <summary>
    /// Overlay states of which the appearance must have one for this entry to draw it (empty: no condition); <see cref="ModelCatalog.Find"/> tries such entries
    /// first (a lamp's state is <c>tube</c> lit or not; its glow overlay <c>tube</c>, <c>tube_nightshift</c> says which model).
    /// </summary>
    public string[] Overlays = Array.Empty<string>();

    /// <summary>
    /// Overlay states the appearance must all have for this entry to draw it (empty: no condition), with <see cref="Overlays"/> when both are given: a
    /// combination one overlay does not tell (a newscaster's wanted screen with its alert, its screen with a crack).
    /// </summary>
    public string[] AllOverlays = Array.Empty<string>();

    /// <summary>
    /// The model's own attach points from the catalog (<c>attachPoints</c>: <c>{ "name", "at": [x, y, z], "normal": [x, y, z] }</c>, metres in the fitted model's own
    /// frame before it is turned or placed); a model file's <c>Attach_*</c> nodes add theirs (<see cref="AttachPoints"/>).
    /// </summary>
    public AttachPoint[] AttachPoints = Array.Empty<AttachPoint>();

    /// <summary>
    /// The pieces whose material name starts with this glow on <see cref="FlashFrames"/> of the picture's animation (the catalog's
    /// <c>"flash": { "material", "frames" }</c>; DV64: a planted C-4's timer window lights on tg's bright frame); null for none.
    /// </summary>
    public string FlashMaterial;

    /// <summary>
    /// The frame (from 0) of a flick of its state this entry draws (the catalog's <c>"flickFrame"</c>; a mounted flash's mflash_flash: its bulb lighting up), or
    /// -1: a thing's own state. While tg flicks that state on a thing a model draws, the model of each frame stands in for it (MapRenderer's flick models).
    /// </summary>
    public int FlickFrame = -1;

    /// <summary>The animation frames (from 0) on which <see cref="FlashMaterial"/>'s pieces glow.</summary>
    public int[] FlashFrames = Array.Empty<int>();

    /// <summary>True when the entry draws only with certain overlays (<see cref="Overlays"/>, <see cref="AllOverlays"/>).</summary>
    public bool NeedsOverlays => Overlays.Length > 0 || AllOverlays.Length > 0;

    /// <summary>True when <paramref name="have"/> (an appearance's overlay states) meets the entry's overlay conditions.</summary>
    public bool HasOverlays(IReadOnlyCollection<string> have)
        => (Overlays.Length == 0 || Overlays.Any(have.Contains)) && AllOverlays.All(have.Contains);

    public ModelRule Rule;
    public ModelTexture Texture;

    /// <summary>The source frame in the model's metres: its width (centred on x 0) and its height (from y 0).</summary>
    public float SourceWidth = 2f, SourceHeight = 2f;

    /// <summary>The width it is fitted to: "tile" (the source width stretched over the world tile), "row" (its x alone: a sofa module), else "uniform" (scaled as its height is).</summary>
    public string FitWidth = "uniform";

    /// <summary>The height it is fitted to: "wall" (<see cref="WorldScale.WallHeight"/>), "table" (<see cref="WorldScale.TableTop"/>) or a number of metres.</summary>
    public string FitHeight = "wall";

    /// <summary>
    /// Source metres: a connected piece of the model that stays at or below this height (a table's leg) keeps its proportions when the width is stretched, scaled as the
    /// height is about its own centre, which moves with the stretch. 0: none.
    /// </summary>
    public float RigidBelow;

    /// <summary>
    /// Mesh node name prefixes picked apart from the rest of the model (a ladder's hatch frame on the floor): each such node is a pick box of its own and the other nodes one more,
    /// so the whole model's box (a whole tile with the frame) does not hold the eye of someone standing on its tile, which no ray from inside a box hits. Null: one box.
    /// </summary>
    public string[] PickApart;

    /// <summary>A size multiplier after the fit (the catalog's <c>"scale"</c>, 1 by default): the whole model grows or shrinks about its origin on the floor.</summary>
    public float Scale = 1f;

    /// <summary>
    /// A smoothing kit's share of the tile its open sides reach (the catalog's <c>"footprint"</c>, 1 by default): a module's sides that join no neighbour are
    /// drawn in to this share of the tile's half, its joined sides stay at the tile's edge, so a run of tables is unbroken and narrower than the tile (the owner,
    /// 2026-10-19: tables 0.8 of a tile, not out of the doorways). A rigid piece (a leg) moves with its side and keeps its size.
    /// </summary>
    public float Footprint = 1f;

    /// <summary>
    /// A smoothing kit's top shows tg's own picture of the table (the catalog's <c>"spriteTop"</c>): the top face's uv are laid over the cell of tg's junction state
    /// (<c>table-N</c>) in the sheet, the outline on the open sides where tg draws it, the joined sides running on into the neighbour's cell (the owner, 2026-10-19:
    /// the kit's tops showed rims on some modules and none on others). Its modules are built turned (<see cref="ModelMesh.Build"/>'s turns), not turned when placed.
    /// </summary>
    public bool SpriteTop;

    /// <summary>A closet's door: how far it swings open (degrees about +Y, negative: clockwise seen from above) and how long it takes (tg's door_anim_time).</summary>
    public float OpenDegrees = -140f, OpenSeconds = 0.2f;

    /// <summary>
    /// Where the model stands from the tile's centre (metres, the model unturned: x across, y up, z toward its front): a firelock beside the airlock of its
    /// tile, a windoor on the tile's edge.
    /// </summary>
    public System.Numerics.Vector3 Offset;

    /// <summary>Degrees the model is turned by (about +Y, counter-clockwise seen from above) besides its rule's turn: a model made looking north.</summary>
    public float Turn;

    /// <summary>
    /// Degrees the model is tipped over about its x axis, its top toward its front (the catalog's <c>"tip"</c>): tg's flipped table (<c>flip_table</c>) is its table's
    /// model on its side, lying on the floor, centred on the tile.
    /// </summary>
    public float Tip;

    /// <summary>
    /// A two-tile airlock (tg's <c>multi_tile</c>, the catalog's <c>"multiTile"</c>): its span follows its dir, not the walls (<c>get_adjusted_dir</c>: NORTH or SOUTH,
    /// its filler east of it; EAST or WEST, north), and the model's +x points at the filler.
    /// </summary>
    public bool MultiTile;

    /// <summary>A <see cref="ModelRule.Sun"/> entry's overlay: the state prefix of the appearance's overlay whose transform is the head's turn (the catalog's <c>azimuth</c>).</summary>
    public string Azimuth;

    /// <summary>Things on its tile rest on every upward level of its model, the top too (a rack's shelves; the catalog's <c>"rest": "shelves"</c>).</summary>
    public bool RestOnShelves;

    /// <summary>A body lying on its tile lies on its model's top (a bed, a stretcher; the catalog's <c>"rest": "top"</c>; M8y, DV127).</summary>
    public bool RestOnTop;

    /// <summary>D325: what stands on its tile is lifted along it, a level's height over the tile in its dir (tg's stairs; the catalog's <c>"rest": "ramp"</c>, DV370).</summary>
    public bool RestOnRamp;

    /// <summary>
    /// A material showing a frame of an overlay state of its own sheet (not the thing's icon state) is drawn only while the appearance has that overlay (the
    /// catalog's <c>"overlayParts": true</c>): a suit storage unit's status lights, its stored suit and helmet come and go as tg's overlays do, and the live frame
    /// under them shows the others (the UV cycle's red lights).
    /// </summary>
    public bool OverlayParts;

    /// <summary>
    /// The things it matches are drawn by another thing's model (<c>"hidden": true</c>, no parts): the eight parts of tg's gravity generator around its
    /// main part, which draws the whole machine; neither their model nor their picture is drawn.
    /// </summary>
    public bool Hidden;

    /// <summary>
    /// The image a <see cref="ModelTexture.Model"/> entry shows in place of its files' embedded texture (one kit, many tg variants): a tg icon (<c>.dmi</c>, its
    /// converted sheet, which shares the kit's sheet's layout), or a PNG relative to the models folder.
    /// </summary>
    public string Image;

    /// <summary>
    /// A tg icon (<c>.dmi</c>) whose sheet stands for the thing's own picture in a <see cref="ModelTexture.Model"/> entry's files (a canister's map icon,
    /// <c>icons/map_icons/objects.dmi</c>, for its GAGS picture): a material showing one frame of it is a live frame (<see cref="ModelFrame"/>) of any state.
    /// </summary>
    public string FrameSheet;

    /// <summary>A crate: icon states that show it open besides <c>[base]open</c> (a mail crate's filled <c>mail</c>), and overlays that do (a cardboard box's lid).</summary>
    public string[] OpenStates = Array.Empty<string>(), OpenOverlays = Array.Empty<string>();

    /// <summary>A crate whose closed state is not its open state without <c>open</c> (the mail crate's <c>mailsealed</c>); null: that rule.</summary>
    public string ClosedState;

    /// <summary>The frame of an icon state in a <see cref="ModelTexture.Cell"/> model's sheet (pixels).</summary>
    public int CellWidth = 32, CellHeight = 48;

    /// <summary>
    /// Where things on its tile rest, in source metres (x, height, z; the model unturned). Empty: found from the model's own shelves (the upward faces inside it,
    /// <see cref="ModelMesh.Shelves"/>).
    /// </summary>
    public System.Numerics.Vector3[] Slots = Array.Empty<System.Numerics.Vector3>();

    /// <summary>
    /// The way a thing the entry draws faces (the catalog's <c>"dir"</c>, which the server reads, DV56): made facing away from the wall behind it, and kept
    /// when pushed.
    /// </summary>
    public bool AwayFromWall, KeepOnPush;

    /// <summary>The parts by the rule's names.</summary>
    public Dictionary<string, ModelPart> Parts = new();

    /// <summary>True when this entry draws <paramref name="icon"/> in <paramref name="state"/>.</summary>
    public bool Draws(string icon, string state)
    {
        if (string.IsNullOrEmpty(icon) || ExceptIcons.Contains(icon, StringComparer.Ordinal))
            return false;
        bool iconMatches = Icons.Any(i => i.EndsWith('/') ? icon.StartsWith(i, StringComparison.Ordinal)
            : i.EndsWith('*') ? icon.StartsWith(i[..^1], StringComparison.Ordinal) : icon == i);
        return iconMatches && (States.Length == 0 || States.Contains(state ?? string.Empty, StringComparer.Ordinal));
    }
}

/// <summary>The catalog.</summary>
public sealed class ModelCatalog
{
    /// <summary>The catalog's file name in the models folder.</summary>
    public const string FileName = "models.json";

    public readonly List<ModelEntry> Entries = new();

    /// <summary>
    /// The catalog file found upward from the working folder and the program's own (the repository's <c>godot/assets/models/models.json</c>, or an
    /// export's <c>assets/models/models.json</c>); null when there is none.
    /// </summary>
    public static string Locate()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (string dir = Path.GetFullPath(start); dir != null; dir = Path.GetDirectoryName(dir))
            {
                foreach (string relative in new[] { "godot/assets/models", "assets/models" })
                {
                    string candidate = Path.Combine(dir, relative, FileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// The first entry that draws the icon and state, an entry with an overlay condition (<see cref="ModelEntry.Overlays"/>, <see cref="ModelEntry.AllOverlays"/>)
    /// first when the appearance's overlays (<paramref name="overlays"/>, their icon states) meet it; null for none.
    /// </summary>
    public ModelEntry Find(string icon, string state, IEnumerable<string> overlays = null)
    {
        var have = new HashSet<string>(overlays?.Where(o => o != null) ?? Array.Empty<string>(), StringComparer.Ordinal);
        if (have.Count > 0)
        {
            foreach (ModelEntry entry in Entries)
            {
                if (entry.NeedsOverlays && entry.Draws(icon, state) && entry.HasOverlays(have))
                    return entry;
            }
        }
        foreach (ModelEntry entry in Entries)
        {
            if (!entry.NeedsOverlays && entry.Draws(icon, state))
                return entry;
        }
        return null;
    }

    /// <summary>The entry that draws <paramref name="frame"/> of a flick of <paramref name="state"/> (<see cref="ModelEntry.FlickFrame"/>), else null.</summary>
    public ModelEntry FindFlickFrame(string icon, string state, int frame)
    {
        ModelEntry last = null;
        foreach (ModelEntry entry in Entries)
        {
            if (entry.FlickFrame < 0 || !entry.Draws(icon, state))
                continue;
            if (entry.FlickFrame == frame)
                return entry;
            if (entry.FlickFrame < frame && (last == null || entry.FlickFrame > last.FlickFrame))
                last = entry; // a frame without its own model keeps the last one before it
        }
        return last;
    }

    /// <summary>
    /// The variant that draws a short-lived visual <paramref name="visualState"/> on a thing (tg's flick_overlay_view: a button's button-overlay-success) along
    /// with the overlays it already shows (<paramref name="overlays"/>: its appearance's and those of the visuals its model already draws); null when no variant
    /// names that state (nor a part of it). It may be the model already drawn: a button pressed again while its success light still shows (the owner, 2026-10-19) has a second
    /// success visual the same variant draws, which must not hang below the wall model as a picture of its own.
    /// </summary>
    public ModelEntry FindVisualVariant(string icon, string state, IEnumerable<string> overlays, string visualState)
    {
        ModelEntry variant = Find(icon, state, (overlays ?? Array.Empty<string>()).Append(visualState));
        // the variant names it, or one of its parts is drawn with it (the elevator call button's tram-overlay-success light, the owner 2026-11-13)
        return variant != null && (variant.Overlays.Contains(visualState) || variant.AllOverlays.Contains(visualState)
            || variant.Parts.Values.Any(p => p.Overlays.Contains(visualState))) ? variant : null;
    }

    private static AttachPoint[] ReadAttachPoints(JsonElement e)
    {
        if (!e.TryGetProperty("attachPoints", out JsonElement points))
            return Array.Empty<AttachPoint>();
        static System.Numerics.Vector3 V3(JsonElement v) => new(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle());
        return points.EnumerateArray().Select(p => new AttachPoint(p.GetProperty("name").GetString() ?? string.Empty, V3(p.GetProperty("at")),
            System.Numerics.Vector3.Normalize(V3(p.GetProperty("normal"))))).ToArray();
    }

    /// <summary>Reads a catalog; throws <see cref="InvalidDataException"/> on a malformed one (an unknown rule, a missing part the rule needs).</summary>
    public static ModelCatalog Parse(string json)
    {
        var catalog = new ModelCatalog();
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        foreach (JsonElement e in document.RootElement.GetProperty("models").EnumerateArray())
        {
            var entry = new ModelEntry
            {
                Id = e.GetProperty("id").GetString() ?? string.Empty,
                Family = e.TryGetProperty("family", out JsonElement family) ? family.GetString() ?? string.Empty : string.Empty,
                OverlayFrames = e.TryGetProperty("overlayFrames", out JsonElement overlayFrames) && overlayFrames.GetBoolean(),
                FlatColors = e.TryGetProperty("flatColors", out JsonElement flatColors) && flatColors.GetBoolean(),
                Icons = Strings(e, "icons"),
                ExceptIcons = Strings(e, "exceptIcons"),
                States = Strings(e, "states"),
                Overlays = Strings(e, "overlays"),
                AllOverlays = Strings(e, "allOverlays"),
                AttachPoints = ReadAttachPoints(e),
                Rule = Enum.Parse<ModelRule>(e.GetProperty("rule").GetString() ?? string.Empty, ignoreCase: true),
                Texture = Enum.Parse<ModelTexture>(e.GetProperty("texture").GetString() ?? string.Empty, ignoreCase: true),
            };
            if (e.TryGetProperty("source", out JsonElement source))
            {
                entry.SourceWidth = source.GetProperty("width").GetSingle();
                entry.SourceHeight = source.GetProperty("height").GetSingle();
            }
            if (e.TryGetProperty("fit", out JsonElement fit))
            {
                if (fit.TryGetProperty("width", out JsonElement w))
                    entry.FitWidth = w.GetString() ?? entry.FitWidth;
                if (fit.TryGetProperty("height", out JsonElement h))
                    entry.FitHeight = h.ValueKind == JsonValueKind.Number ? h.GetSingle().ToString(System.Globalization.CultureInfo.InvariantCulture) : h.GetString() ?? entry.FitHeight;
            }
            if (e.TryGetProperty("rigidBelow", out JsonElement rigid))
                entry.RigidBelow = rigid.GetSingle();
            if (e.TryGetProperty("pickApart", out JsonElement apart))
                entry.PickApart = apart.EnumerateArray().Select(n => n.GetString() ?? string.Empty).ToArray();
            if (e.TryGetProperty("scale", out JsonElement scale))
                entry.Scale = scale.GetSingle();
            if (e.TryGetProperty("footprint", out JsonElement footprint))
                entry.Footprint = footprint.GetSingle();
            entry.SpriteTop = e.TryGetProperty("spriteTop", out JsonElement spriteTop) && spriteTop.GetBoolean();
            if (e.TryGetProperty("open", out JsonElement open))
            {
                if (open.TryGetProperty("degrees", out JsonElement d))
                    entry.OpenDegrees = d.GetSingle();
                if (open.TryGetProperty("seconds", out JsonElement t))
                    entry.OpenSeconds = t.GetSingle();
                entry.OpenStates = Strings(open, "states");
                entry.OpenOverlays = Strings(open, "overlays");
                if (open.TryGetProperty("closedState", out JsonElement closed))
                    entry.ClosedState = closed.GetString();
            }
            if (e.TryGetProperty("dir", out JsonElement dirRule))
            {
                entry.AwayFromWall = dirRule.TryGetProperty("awayFromWall", out JsonElement away) && away.GetBoolean();
                entry.KeepOnPush = dirRule.TryGetProperty("keepOnPush", out JsonElement keep) && keep.GetBoolean();
            }
            if (e.TryGetProperty("cell", out JsonElement cell))
            {
                entry.CellWidth = cell[0].GetInt32();
                entry.CellHeight = cell[1].GetInt32();
            }
            if (e.TryGetProperty("offset", out JsonElement offset))
                entry.Offset = new System.Numerics.Vector3(offset[0].GetSingle(), offset[1].GetSingle(), offset[2].GetSingle());
            if (e.TryGetProperty("turn", out JsonElement turn))
                entry.Turn = turn.GetSingle();
            if (e.TryGetProperty("tip", out JsonElement tip))
                entry.Tip = tip.GetSingle();
            if (e.TryGetProperty("multiTile", out JsonElement multiTile))
                entry.MultiTile = multiTile.GetBoolean();
            if (e.TryGetProperty("azimuth", out JsonElement azimuth))
                entry.Azimuth = azimuth.GetString();
            if (e.TryGetProperty("image", out JsonElement image))
                entry.Image = image.GetString();
            if (e.TryGetProperty("frameSheet", out JsonElement frameSheet))
                entry.FrameSheet = frameSheet.GetString();
            if (e.TryGetProperty("flickFrame", out JsonElement flickFrame))
                entry.FlickFrame = flickFrame.GetInt32();
            if (e.TryGetProperty("flash", out JsonElement flash))
            {
                entry.FlashMaterial = flash.GetProperty("material").GetString();
                entry.FlashFrames = flash.GetProperty("frames").EnumerateArray().Select(f => f.GetInt32()).ToArray();
            }
            entry.RestOnShelves = e.TryGetProperty("rest", out JsonElement rest) && rest.GetString() == "shelves";
            entry.RestOnTop = rest.ValueKind == JsonValueKind.String && rest.GetString() == "top";
            entry.RestOnRamp = rest.ValueKind == JsonValueKind.String && rest.GetString() == "ramp";
            entry.OverlayParts = e.TryGetProperty("overlayParts", out JsonElement overlayParts) && overlayParts.GetBoolean();
            entry.Hidden = e.TryGetProperty("hidden", out JsonElement hidden) && hidden.GetBoolean();
            if (e.TryGetProperty("slots", out JsonElement slots))
                entry.Slots = slots.EnumerateArray().Select(v => new System.Numerics.Vector3(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle())).ToArray();
            foreach (JsonProperty p in e.GetProperty("parts").EnumerateObject())
            {
                JsonElement v = p.Value;
                entry.Parts[p.Name] = v.ValueKind == JsonValueKind.String
                    ? new ModelPart(v.GetString(), null, System.Numerics.Vector2.Zero)
                    : new ModelPart(v.GetProperty("file").GetString(), v.TryGetProperty("node", out JsonElement n) ? n.GetString() : null,
                        v.TryGetProperty("slide", out JsonElement s) ? Slide(s) : System.Numerics.Vector2.Zero,
                        v.TryGetProperty("material", out JsonElement m) ? m.GetString() : null,
                        v.TryGetProperty("pivot", out JsonElement pv) && pv.GetBoolean())
                    {
                        Axis = v.TryGetProperty("axis", out JsonElement ax) ? new System.Numerics.Vector3(ax[0].GetSingle(), ax[1].GetSingle(), ax[2].GetSingle()) : System.Numerics.Vector3.UnitX,
                        Degrees = v.TryGetProperty("degrees", out JsonElement deg) ? deg.GetSingle() : 0f,
                        Picture = v.TryGetProperty("picture", out JsonElement pic) ? pic.GetString() ?? "closed" : "closed",
                        Turn = v.TryGetProperty("turn", out JsonElement partTurn) ? partTurn.GetSingle() : 0f,
                        Overlays = Strings(v, "overlays"),
                        ExceptOverlays = Strings(v, "exceptOverlays"),
                        AllOverlays = Strings(v, "allOverlays"),
                        States = Strings(v, "states"),
                        ExceptStates = Strings(v, "exceptStates"),
                        Deck = v.TryGetProperty("deck", out JsonElement deck) ? deck.GetString() is "up" or "down" ? deck.GetString() : throw new InvalidDataException($"model {entry.Id}: deck is \"up\" or \"down\"") : null,
                        Move = v.TryGetProperty("move", out JsonElement move) ? new System.Numerics.Vector3(move[0].GetSingle(), move[1].GetSingle(), move[2].GetSingle()) : default,
                        OpenScale = v.TryGetProperty("openScale", out JsonElement openScale) ? new System.Numerics.Vector3(openScale[0].GetSingle(), openScale[1].GetSingle(), openScale[2].GetSingle()) : System.Numerics.Vector3.One,
                        Spin = v.TryGetProperty("spin", out JsonElement spin) ? spin.GetSingle() : 0f,
                        Flick = v.TryGetProperty("flick", out JsonElement flick) ? FlickOf(flick) : null,
                        NodeScale = v.TryGetProperty("nodeScale", out JsonElement nodeScale) ? new System.Numerics.Vector3(nodeScale[0].GetSingle(), nodeScale[1].GetSingle(), nodeScale[2].GetSingle()) : null,
                        Opaque = v.TryGetProperty("opaque", out JsonElement opaque) && opaque.GetBoolean(),
                        Fill = v.TryGetProperty("fill", out JsonElement fill) && fill.GetBoolean(),
                        Dirs = v.TryGetProperty("dirs", out JsonElement dirs) ? dirs.EnumerateArray().Select(d => d.GetInt32()).ToArray() : Array.Empty<int>(),
                        Live = !(v.TryGetProperty("opaque", out JsonElement opaqueLive) && opaqueLive.GetBoolean()) && (!v.TryGetProperty("live", out JsonElement live) || live.GetBoolean()),
                        OverlayPixelW = v.TryGetProperty("overlayPixelW", out JsonElement pixelW) ? pixelW.GetInt16() : null,
                        Offset = v.TryGetProperty("offset", out JsonElement partOffset) ? new System.Numerics.Vector3(partOffset[0].GetSingle(), partOffset[1].GetSingle(), partOffset[2].GetSingle()) : default,
                        PivotAt = v.TryGetProperty("pivotAt", out JsonElement pivotAt) ? new System.Numerics.Vector3(pivotAt[0].GetSingle(), pivotAt[1].GetSingle(), pivotAt[2].GetSingle()) : null,
                        Glow = v.TryGetProperty("glow", out JsonElement glow) ? glow.GetSingle() : null,
                        GlowOverlays = Strings(v, "glowOverlays"),
                        Color = v.TryGetProperty("color", out JsonElement color) ? Hex(color.GetString()) : null,
                        OffColor = v.TryGetProperty("offColor", out JsonElement offColor) ? Hex(offColor.GetString()) : null,
                        BlinkOn = v.TryGetProperty("blink", out JsonElement blink) ? blink[0].GetSingle() : 0f,
                        BlinkOff = v.TryGetProperty("blink", out JsonElement blinkOff) ? blinkOff[1].GetSingle() : 0f,
                        BlinkAt = v.TryGetProperty("blink", out JsonElement blinkAt) && blinkAt.GetArrayLength() > 2 ? blinkAt[2].GetSingle() : 0f,
                        TintIcon = v.TryGetProperty("tintIcon", out JsonElement tintIcon) ? tintIcon.GetString() : null,
                        TintVisuals = v.TryGetProperty("tintVisuals", out JsonElement tintVisuals) ? tintVisuals.GetString() : null,
                        TintOverlay = v.TryGetProperty("tintOverlay", out JsonElement tintOverlay) ? tintOverlay.GetString() : null,
                        Light = v.TryGetProperty("light", out JsonElement light) ? new ModelPointLight(
                            new System.Numerics.Vector3(light.GetProperty("position")[0].GetSingle(), light.GetProperty("position")[1].GetSingle(), light.GetProperty("position")[2].GetSingle()),
                            Hex(light.GetProperty("color").GetString()), light.GetProperty("range").GetSingle(), light.GetProperty("energy").GetSingle()) : null,
                        Shader = v.TryGetProperty("shader", out JsonElement shader) ? ShaderName(shader.GetString()) : null,
                        Pulse = v.TryGetProperty("pulse", out JsonElement pulse) ? Pulses(pulse) : Array.Empty<ModelPulse>(),
                        Halo = v.TryGetProperty("halo", out JsonElement halo) ? Hex(halo.GetString()) : null,
                    };
                entry.Parts[p.Name] = entry.Parts[p.Name] with { Name = p.Name };
            }
            foreach (string needed in entry.Hidden ? Array.Empty<string>() : ModelRules.PartsOf(entry.Rule))
            {
                if (!entry.Parts.ContainsKey(needed))
                    throw new InvalidDataException($"model {entry.Id}: the rule {entry.Rule} needs the part '{needed}'");
            }
            if (entry.Parts.Count == 0 && !entry.Hidden)
                throw new InvalidDataException($"model {entry.Id}: no parts");
            catalog.Entries.Add(entry);
        }
        return catalog;
    }

    // A slide: metres across the door (a number), or [across, up].
    private static System.Numerics.Vector2 Slide(JsonElement s)
        => s.ValueKind == JsonValueKind.Array ? new System.Numerics.Vector2(s[0].GetSingle(), s[1].GetSingle()) : new System.Numerics.Vector2(s.GetSingle(), 0f);

    private static ModelFlick FlickOf(JsonElement f)
    {
        System.Numerics.Vector3 V(string name, float or)
            => f.TryGetProperty(name, out JsonElement v) ? new System.Numerics.Vector3(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle()) : new System.Numerics.Vector3(or);
        return new ModelFlick(Strings(f, "states"),
            f.TryGetProperty("frames", out JsonElement frames) ? frames.EnumerateArray().Select(x => x.GetSingle()).ToArray() : Array.Empty<float>(),
            f.GetProperty("seconds").GetSingle(), V("flickMove", 0f), f.TryGetProperty("flickDegrees", out JsonElement deg) ? deg.GetSingle() : 0f, V("flickScale", 1f));
    }

    private static string ShaderName(string name)
        => name is ModelPart.CrystalShader or ModelPart.FieldShader or ModelPart.NozzleShader ? name : throw new InvalidDataException($"no model shader '{name}' (crystal, field or nozzle)");

    private static ModelPulse[] Pulses(JsonElement frames)
    {
        ModelPulse[] pulse = frames.EnumerateArray().Select(Pulse).ToArray();
        return pulse.Length <= ModelPart.MaxPulse ? pulse : throw new InvalidDataException($"a pulse has at most {ModelPart.MaxPulse} frames, not {pulse.Length}");
    }

    // A pulse frame: [seconds, strength] or [seconds, strength, r, g, b].
    private static ModelPulse Pulse(JsonElement f)
    {
        int n = f.GetArrayLength();
        if (n != 2 && n != 5 || f[0].GetSingle() <= 0f)
            throw new InvalidDataException($"a pulse frame is [seconds above 0, strength] or [seconds, strength, r, g, b], not {f}");
        var scale = n == 5 ? new System.Numerics.Vector3(f[2].GetSingle(), f[3].GetSingle(), f[4].GetSingle()) : System.Numerics.Vector3.One;
        return new ModelPulse(f[0].GetSingle(), f[1].GetSingle(), scale);
    }

    // "#rrggbb" as sRGB 0..1.
    private static System.Numerics.Vector3 Hex(string hex)
    {
        if (hex == null || hex.Length != 7 || hex[0] != '#'
            || !int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            throw new InvalidDataException($"a colour is #rrggbb, not '{hex}'");
        return new System.Numerics.Vector3(rgb >> 16 & 0xFF, rgb >> 8 & 0xFF, rgb & 0xFF) / 255f;
    }

    private static string[] Strings(JsonElement e, string name)
        => e.TryGetProperty(name, out JsonElement a) ? a.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray() : Array.Empty<string>();
}
