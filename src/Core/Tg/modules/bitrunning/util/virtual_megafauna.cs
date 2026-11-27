// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/util/virtual_megafauna.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The [DmPath] of /mob/living/simple_animal/hostile/megafauna is here: this is the earliest .dme file that
// declares it (rules 2.3).

// tg: code/modules/bitrunning/util/virtual_megafauna.dm:2
[DmPath("/mob/living/simple_animal/hostile/megafauna")]
public partial class mob_living_simple_animal_hostile_megafauna : mob_living_simple_animal_hostile
{
    /// Removes all the loot and achievements from megafauna for bitrunning related (/simple_animal/hostile/megafauna version)
    // tg: code/modules/bitrunning/util/virtual_megafauna.dm:2
    public virtual void make_virtual_megafauna()
    {
        float new_max = clamp(maxHealth * 0.5f, 600, 1300);
        maxHealth = new_max;
        health = new_max;

        true_spawn = FALSE;

        // rebuild the achievement element's arguments to remove it appropriately
        if (achievement_type != null || score_achievement_type != null)
        {
            DmList achievements = Dm.list(typeof(datum_award_achievement_boss_boss_killer), typeof(datum_award_score_boss_score));
            if (achievement_type != null)
                Dm.append(ref achievements, achievement_type);
            if (score_achievement_type != null)
                Dm.append(ref achievements, score_achievement_type);
            RemoveElement(typeof(datum_element_kill_achievement), string_list(achievements), crusher_achievement_type, typeof(datum_memory_megafauna_slayer));
        }

        // remove the crusher loot element's arguments also to remove it appropriately
        RemoveElement(
            typeof(datum_element_crusher_loot),
            Dm.named("trophy_type", crusher_loot),
            Dm.named("guaranteed_drop", 0.6f),
            Dm.named("replace_all", replace_crusher_drop),
            Dm.named("drop_immediately", del_on_death));

        loot.Cut();
        Dm.append(ref loot, typeof(obj_structure_closet_crate_secure_bitrunning_encrypted));
    }
}

// tg: code/modules/bitrunning/util/virtual_megafauna.dm:31
public partial class mob_living_basic_boss : mob_living_basic
{
    /// Removes all the loot and achievements from megafauna for bitrunning related (/basic/boss version)
    // tg: code/modules/bitrunning/util/virtual_megafauna.dm:31
    public virtual void make_virtual_megafauna()
    {
        float new_max = clamp(maxHealth * 0.5f, 600, 1300);
        maxHealth = new_max;
        health = new_max;

        // rebuild the achievement element's arguments to remove it appropriately
        if (Dm.truthy(achievements))
        {
            DmList achievements_list = Dm.list(typeof(datum_award_achievement_boss_boss_killer), typeof(datum_award_score_boss_score));
            Dm.append(ref achievements_list, achievements);
            RemoveElement(typeof(datum_element_kill_achievement), string_list(achievements_list), crusher_achievement_type, typeof(datum_memory_megafauna_slayer));
        }

        // remove the crusher loot element's arguments also to remove it appropriately
        RemoveElement(
            typeof(datum_element_crusher_loot),
            Dm.named("trophy_type", string_list(crusher_loot)),
            Dm.named("guaranteed_drop", 0.6f),
            Dm.named("drop_immediately", DEL_ON_DEATH));

        RemoveElement(typeof(datum_element_death_drops), string_list(regular_loot));
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_structure_closet_crate_secure_bitrunning_encrypted)));
    }
}
