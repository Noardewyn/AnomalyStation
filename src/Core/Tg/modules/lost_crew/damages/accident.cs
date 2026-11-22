// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/damages/accident.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks are generated (D111).

// tg: code/modules/lost_crew/damages/accident.dm:1
public partial class datum_corpse_damage_cause_of_death_plasmafire : datum_corpse_damage_cause_of_death
{
    // tg: code/modules/lost_crew/damages/accident.dm:10
    [DmParams("body", "severity", "storage", "on_revive_and_player_occupancy")]
    public override async DmTask<object> apply_to_body(mob_living_carbon_human body = null, object severity = null, DmList storage = null, params object[] __extra)
    {
        float sev = Dm.num(severity);
        await body.apply_damage(burn_damage_base + burn_damage_max * sev, BURN, wound_bonus: 100 * sev, spread_damage: TRUE);
        await body.apply_damage(tox_damage_max * sev, TOX);
        return null;
    }
}

// tg: code/modules/lost_crew/damages/accident.dm:14
public partial class datum_corpse_damage_cause_of_death_explosion : datum_corpse_damage_cause_of_death
{
    // tg: code/modules/lost_crew/damages/accident.dm:22
    [DmParams("body", "severity", "storage", "on_revive_and_player_occupancy")]
    public override async DmTask<object> apply_to_body(mob_living_carbon_human body = null, object severity = null, DmList storage = null, params object[] __extra)
    {
        float count = Dm.max(round(explosion_count_max * Dm.num(severity)), 1);
        for (int i = 1; i <= count; i++)
            body.ex_act(EXPLODE_HEAVY);
        return null;
    }
}

// tg: code/modules/lost_crew/damages/accident.dm:26
public partial class datum_corpse_damage_cause_of_death_spaced : datum_corpse_damage_cause_of_death
{
    // tg: code/modules/lost_crew/damages/accident.dm:33
    [DmParams("body", "severity", "storage", "on_revive_and_player_occupancy")]
    public override async DmTask<object> apply_to_body(mob_living_carbon_human body = null, object severity = null, DmList storage = null, params object[] __extra)
    {
        float sev = Dm.num(severity);
        await body.apply_damage(base_damage + damage_max * (sev * rand(80, 120) * 0.01f), BURN, spread_damage: TRUE);
        await body.apply_damage(base_damage + damage_max * (sev * rand(80, 120) * 0.01f), BRUTE, spread_damage: TRUE);
        body.set_coretemperature(TCMB);
        return null;
    }
}
