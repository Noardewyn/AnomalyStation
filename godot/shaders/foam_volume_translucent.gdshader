// DV247 (gaps F1): a see-through foam volume (Render/FoamVolume.cs; tg's resin wall, alpha 120): tg's picture blended by its alpha.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, diffuse_burley, specular_disabled;

#define FOAM_TRANSLUCENT
#include "res://shaders/foam_volume.gdshaderinc"
