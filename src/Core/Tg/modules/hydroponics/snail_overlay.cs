// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/snail_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file. VARSET_CALLBACK is the is_waddling__varset method (the GAP(lead) of assembly/doorcontrol.cs).

// tg: code/modules/hydroponics/snail_overlay.dm:4
public partial class obj_effect_overlay_vis_effect_snail : obj_effect_overlay_vis_effect
{
    // tg: code/modules/hydroponics/snail_overlay.dm:1
    private const float SNAIL_MOVEMENT_DISTANCE = 5;
    // tg: code/modules/hydroponics/snail_overlay.dm:2
    private const float SNAIL_MOVEMENT_TIME = 10 * SECONDS;

    // tg: code/modules/hydroponics/snail_overlay.dm:12
    public virtual void handle_animation()
    {
        if (Dm.truthy(is_waddling))
            return;
        is_waddling = TRUE;
        float movement_direction = Dm.num(pixel_x) >= 0 ? -1 : 1;
        transform = ((matrix)Dm.@new<matrix>(transform)).Scale(-1, 1); //face the other direction
        animate(this, Dm.named("pixel_x", movement_direction * SNAIL_MOVEMENT_DISTANCE), Dm.named("time", SNAIL_MOVEMENT_TIME));
        _addtimer(Dm.@new<datum_callback>(this, nameof(is_waddling__varset), FALSE), SNAIL_MOVEMENT_TIME, file: "code/modules/hydroponics/snail_overlay.dm", line: 20);
    }

    /// VARSET_CALLBACK(src, is_waddling, FALSE)'s target: ___callbackvarset's datum branch for this var.
    public virtual void is_waddling__varset(object var_value = null)
    {
        is_waddling = Dm.num(var_value);
    }
}
