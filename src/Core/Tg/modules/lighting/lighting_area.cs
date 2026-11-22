// Translated from tgstation (snapshot 1d99bd7f): code/modules/lighting/lighting_area.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: the area's base lighting is not ported (D24, rules 13; the rest of the module is, M7 lumcount): only the /area var block of this file (W4.0 contracts, rules 10.2;
// its override luminosity = 1, :2, is in areas.cs's dm_tg_vars, the one place of /area's overrides) and
// update_base_lighting, which /area/Initialize calls (areas.dm:217), as a deferred leaf (package B, wave 4).
// set_base_lighting, vv_edit_var, remove_base_lighting, add_base_lighting and starlight_changed are not declared.

// tg: code/modules/lighting/lighting_area.dm:1
public partial class area : atom
{
    ///List of mutable appearances we underlay to show light
    ///In the form plane offset + 1 -> appearance to use
    // tg: code/modules/lighting/lighting_area.dm:5
    public DmList lighting_effects;
    ///Whether this area has a currently active base lighting, bool
    // tg: code/modules/lighting/lighting_area.dm:7
    public float area_has_base_lighting = FALSE;
    ///alpha 0-255 of lighting_effect and thus baselighting intensity
    // tg: code/modules/lighting/lighting_area.dm:9
    public float base_lighting_alpha = 0;
    ///The colour of the light acting on this area
    // tg: code/modules/lighting/lighting_area.dm:11
    public string base_lighting_color = COLOR_WHITE;

    // tg: code/modules/lighting/lighting_area.dm:39
    public virtual void update_base_lighting()
    {
        Dm.Deferred("code/modules/lighting/lighting_area.dm:39", "update_base_lighting: the area's base lighting overlays are lighting, not ported (D24, rules 13); area_has_base_lighting stays FALSE");
    }
}
