// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/damages/projectile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks are generated (D111); projectile is declared here (below).

/// Simulates being shot by a projectile
// tg: code/modules/lost_crew/damages/projectile.dm:2
public partial class datum_corpse_damage_cause_of_death_projectile : datum_corpse_damage_cause_of_death
{
    /// The projectile to simulate shooting
    // tg: code/modules/lost_crew/damages/projectile.dm:4  var/obj/projectile/projectile: a typepath until apply_to_body, then the made projectile (rules 3.5)
    public object projectile;

    // tg: code/modules/lost_crew/damages/projectile.dm:10
    public override async DmTask<object> apply_to_body(mob_living_carbon_human body = null, object severity = null, DmList storage = null, params object[] __extra)
    {
        projectile = Dm.@new((Type)projectile);

        float hits = ((max_hits - min_hits) * Dm.num(severity) + min_hits);

        for (int i = 1; i <= hits; i++)
            await body.projectile_hit((obj_projectile)projectile, def_zone: pick(body.get_all_limbs()), piercing_hit: TRUE);
        return null;
    }
}
