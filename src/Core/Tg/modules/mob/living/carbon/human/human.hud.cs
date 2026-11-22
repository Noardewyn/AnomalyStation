// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/human.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the HUD proc of human.dm (update_health_hud :679, the health doll's refresh), split off for M2 P3 (D107)
// like carbon.hud.cs; the rest of human.dm is human.cs (P2).

// tg: code/modules/mob/living/carbon/human/human.dm:679
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human.dm:679
    public override object update_health_hud(object healthpercent = null)
    {
        if (!Dm.truthy(client) || !Dm.truthy(hud_used))
            return null;
        // Updates the health bar, also sends signal
        object dot = null;
        dot = base.update_health_hud(healthpercent);
        // Handles changing limb colors and stuff
        if (!Dm.truthy(Dm.bitand(living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS)))
            (hud_used.screen_objects[HUD_MOB_HEALTHDOLL] as atom)?.update_appearance();
        return dot;
    }
}
