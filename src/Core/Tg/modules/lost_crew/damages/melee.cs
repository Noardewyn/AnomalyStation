// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/damages/melee.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks are generated (D111); weapon is declared here (below).

/// Simulates a melee attack with a specified weapon
// tg: code/modules/lost_crew/damages/melee.dm:2
public partial class datum_corpse_damage_cause_of_death_melee_weapon : datum_corpse_damage_cause_of_death
{
    /// The weapon with which we hit
    // tg: code/modules/lost_crew/damages/melee.dm:4  var/obj/item/weapon: a typepath until apply_to_body, then the made item (rules 3.5)
    public object weapon;

    // tg: code/modules/lost_crew/damages/melee.dm:10
    [DmParams("body", "severity", "storage", "on_revive_and_player_occupancy")]
    public override async DmTask<object> apply_to_body(mob_living_carbon_human body = null, object severity = null, DmList storage = null, params object[] __extra)
    {
        weapon = await get_weapon(body);

        float hits = ((max_hits - min_hits) * Dm.num(severity) + min_hits);
        for (int i = 1; i <= hits; i++)
        {
            await ((obj_item)weapon).attack__proc(body, body); //needs an attacker, no reason it cant be the body as well
            body.zone_selected = pick(body.get_all_limbs()) as string;
        }
        return null;
    }

    // tg: code/modules/lost_crew/damages/melee.dm:18
    public virtual async DmTask<object> get_weapon(mob_living_carbon_human body = null)
    {
        return Dm.@new((Type)weapon, null);
    }
}

// tg: code/modules/lost_crew/damages/melee.dm:21
public partial class datum_corpse_damage_cause_of_death_melee_weapon_esword : datum_corpse_damage_cause_of_death_melee_weapon
{
    // tg: code/modules/lost_crew/damages/melee.dm:25
    public override async DmTask<object> get_weapon(mob_living_carbon_human body = null)
    {
        object dot = await base.get_weapon(body);

        var esword = dot as obj_item_melee_energy_sword;

        await esword.attack_self(); //need to activate it
        return dot;
    }
}

// tg: code/modules/lost_crew/damages/melee.dm:40
public partial class datum_corpse_damage_cause_of_death_melee_weapon_heretic : datum_corpse_damage_cause_of_death_melee_weapon
{
    // tg: code/modules/lost_crew/damages/melee.dm:43
    public override async DmTask<object> get_weapon(mob_living_carbon_human body = null)
    {
        var blade = pick(subtypesof(typeof(obj_item_melee_sickly_blade))) as Type; //pick a random blade, can be a bunch of fun stuff
        return Dm.@new(blade, null);
    }
}
