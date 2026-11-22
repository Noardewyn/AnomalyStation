// The glass pass of the props (the next pass of prop_atlas.gdshader's material): the see-through pixels of the windows' panes and of the
// models' glass inserts, blended over what is behind them instead of stippled. No depth is written, so the panes need no sorting among
// the opaque things, and the ambient occlusion and the outlines do not see them. No shadow falls on a pane (the owner, 2026-11-13: the player's
// and the things' shadows lay on the windows), and it reflects what is on screen beside it (prop_atlas.gdshaderinc).
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_disabled, blend_mix, depth_draw_never, shadows_disabled;
#define GLASS_PASS

#include "res://shaders/prop_atlas.gdshaderinc"
