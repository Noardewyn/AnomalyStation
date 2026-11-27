// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/mining_mobs/mining_mobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

//the base mining mob
// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/mining_mobs.dm:2
public partial class mob_living_simple_animal_hostile_asteroid : mob_living_simple_animal_hostile
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/mining_mobs.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (crusher_loot != null)
            Waiting.NoSleep(AddElement(typeof(datum_element_crusher_loot), crusher_loot, crusher_drop_mod, del_on_death));
        Waiting.NoSleep(AddElement(typeof(datum_element_mob_killed_tally), "mobs_killed_mining"));
        if (TgState.Current.mob_living_simple_animal_hostile_asteroid__Initialize__vulnerable_projectiles == null)
            TgState.Current.mob_living_simple_animal_hostile_asteroid__Initialize__vulnerable_projectiles = string_list(Dm.list(BRUTE)) as DmList; // MINING_MOB_PROJECTILE_VULNERABILITY (__DEFINES/mobs.dm:1073)
        Waiting.NoSleep(AddElement(
            typeof(datum_element_ranged_armour),
            Dm.named("minimum_projectile_force", 30),
            Dm.named("below_projectile_multiplier", 0.3f),
            Dm.named("vulnerable_projectile_types", TgState.Current.mob_living_simple_animal_hostile_asteroid__Initialize__vulnerable_projectiles),
            Dm.named("minimum_thrown_force", 20),
            Dm.named("throw_blocked_message", throw_message)));

        RegisterSignals(this, Dm.list(COMSIG_PROJECTILE_PREHIT, COMSIG_ATOM_PREHITBY), nameof(Aggro));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/mining_mobs.dm:51
    public override void Aggro()
    {
        base.Aggro();
        if (vision_range == aggro_vision_range && icon_aggro != null)
            icon_state = icon_aggro as string;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/mining_mobs.dm:56
    public override void LoseAggro()
    {
        base.LoseAggro();
        if (stat == DEAD)
            return;
        icon_state = icon_living as string;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/mining_mobs.dm:36 (var/static/list/vulnerable_projectiles)
    internal DmList mob_living_simple_animal_hostile_asteroid__Initialize__vulnerable_projectiles;
}
