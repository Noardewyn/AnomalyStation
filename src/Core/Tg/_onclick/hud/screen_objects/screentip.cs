// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/screentip.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (D125). The screentip's maptext stays empty in the port: tg's on_mouse_enter writes the text into
// it, which the screentip seam replaces (DV28, docs/ai/specs/interaction.md 6.1). The var block is generated.

// tg: code/_onclick/hud/screen_objects/screentip.dm:1
[DmPath("/atom/movable/screen/screentip")]
public partial class atom_movable_screen_screentip : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screentip.dm:11
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        update_view();
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screentip.dm:15
    [SignalHandler]
    public virtual void update_view(datum source = null)
    {
        if (!Dm.truthy(hud) || !Dm.truthy(hud.mymob.canon_client?.view_size)) //Might not have been initialized by now
            return;
        // tg: :19  maptext_width = view_to_pixels(hud.mymob.canon_client.view_size.getView())[1]
        Dm.NotTranslated("code/_onclick/hud/screen_objects/screentip.dm:19", "update_view: the screentip's maptext width from the client's view size (view_to_pixels and /datum/view_data are not declared; the view, M10+)");
    }
}
