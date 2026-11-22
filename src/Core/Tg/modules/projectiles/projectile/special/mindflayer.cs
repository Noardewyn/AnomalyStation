// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/special/mindflayer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the Mind Flayer's ray hurts a human's brain and makes them hallucinate. The var block is
// generated (D111).

// tg: code/modules/projectiles/projectile/special/mindflayer.dm:1
public partial class obj_projectile_beam_mindflayer : obj_projectile_beam
{
    // tg: code/modules/projectiles/projectile/special/mindflayer.dm:4
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (ishuman(target))
        {
            mob_living_carbon_human human_hit = target as mob_living_carbon_human;
            await human_hit.adjust_organ_loss(ORGAN_SLOT_BRAIN, 20);
            await human_hit.adjust_hallucinations(60 * SECONDS);
        }
        return dot;
    }
}
