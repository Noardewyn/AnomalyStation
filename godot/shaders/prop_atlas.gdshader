// The prop shader (M3 P6, docs/ai/specs/apps.md 2.9): one MultiMesh per chunk draws its static props, wall decals, items, floor hatches and shaped pieces
// (doors, windows, tables, lights) from ONE atlas. An instance names its picture by an id; a metrics texture (two texels per
// id) gives the picture's rectangle in the atlas and its quad's size and offset in metres.
//
// Instance colour: r = mode / 8 (0 billboard, 1 wall decal, 2 piece, 3 item, 4 floor decal, 5 resting billboard, 6 3D model part), g = part / 32 (a floor decal: its height above the floor in metres * 16; a billboard, item or resting billboard: its DepthRank), b = flag (a light's glow / a door's deny flash (M4v slice 4)),
// a = a door's open fraction (a wall decal's or a fin's yaw in turns, M4v slice 4: its basis carries the look below). Instance custom: x, y = the picture id as (id % 2048, id / 2048); z = the facing (256 per turn,
// clockwise from north) of a billboard, or of a piece the joints mask (M3 R3: the arms of a joined window or grille, the corner posts of a border piece);
// w = the number of direction frames (1, 4 or 8; the frames are consecutive ids, front first).
//
// M3 R3 (D121): an ITEM (mode 3) is a billboard at item_scale of the tile scale about the bottom centre of its picture, which rests on the instance's origin (the
// floor, or the table top the instance was lifted to): the picture's own vertical offset in its frame (quad.w) is dropped, because tg draws items mid-frame for the
// top-down depth, not for height. A RESTING billboard (mode 5, a machine on a table, a holopad) is the same at the tile scale. A FLOOR DECAL (mode 4: an air vent, a scrubber) lies flat a hair above the floor, the picture's up pointing north, at the tile scale.
// A pod door or shutters (part 9) rise into the ceiling when they open (M3 R5) and are cut at their level's ceiling.
// A piece's vertices carry UV2.y, the arm or corner post they belong to (0: always drawn): the instance draws the arms whose bit its joints mask has.
// M4v slice 1 (protocol version 9): a billboard, item or resting billboard does not use its instance basis (it faces the camera), so it carries the
// look of BYOND's animate() (MapRenderer.StepLooks): column 0 = (a, d, 1 - alpha), column 1 = (b, e, sideways move in metres), column 2 =
// (1 - red, 1 - green, blue): the transform's linear part about the icon's centre in the quad's plane, and the colour and alpha factors (an
// identity basis is no change). An alpha below one is a screen-door dither. M4v slice 4: a wall decal and a floor decal carry it the same way (their
// placement's yaw moved to the colour's alpha; a floor decal lies unturned): the transform turns the picture in its own plane.
// A 3D MODEL part (mode 6, docs/ai/specs/models.md) has a mesh of its own, drawn whole like a piece: UV2 is how far it slides along its x and up at open fraction 1 (an
// airlock's leaf, an external airlock's upper and lower leaves, a hatch's sections); a sliding part is cut at the jamb, the floor and the ceiling. UV2.y below -8 (-16
// added k times) marks a face of a glass insert, drawn see-through by the picture's alpha times its own, 1 - (k - 1) / 100, (tg's glass overlay) as a screen-door stipple, so it needs no sorting; a door
// with the fill overlay shows the fill there, opaque.
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_disabled;

#include "res://shaders/prop_atlas.gdshaderinc"
