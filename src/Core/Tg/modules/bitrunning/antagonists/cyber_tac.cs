// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/antagonists/cyber_tac.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the cyber tactical unit, the armed glitch of a threatened server (its MODsuit, its M-90 with the weapons
// authorisation implant). The var blocks are generated.

// tg: code/modules/bitrunning/antagonists/cyber_tac.dm:1
public partial class datum_antagonist_bitrunning_glitch_cyber_tac : datum_antagonist_bitrunning_glitch
{
    // tg: code/modules/bitrunning/antagonists/cyber_tac.dm:7
    public override async DmTask on_gain()
    {
        await base.on_gain();

        if (!ishuman(owner.current))
        {
            stack_trace("humans only for this position", "code/modules/bitrunning/antagonists/cyber_tac.dm", 11);
            return;
        }

        await convert_agent();
    }
}

// tg: code/modules/bitrunning/antagonists/cyber_tac.dm:16
public partial class datum_outfit_cyber_police_tactical : datum_outfit_cyber_police
{
    // tg: code/modules/bitrunning/antagonists/cyber_tac.dm:27
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        await base.post_equip(user, visuals_only);

        obj_item_implant_weapons_auth auth = Dm.@new<obj_item_implant_weapons_auth>(user);
        await auth.implant(user);
    }
}
