// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/wardrobes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the wardrobes' procs (the mothroaches that may nest in one at round start, the clothes they chew). The var blocks are
// generated. Deferred leaf: a damaged wardrobe's mothroaches flee through their AI's blackboard (the creatures' AI, C1).

public partial class datum_controller_global_vars
{
    // tg: code/modules/vending/wardrobes.dm:1  GLOBAL_VAR_INIT(roaches_deployed, FALSE)
    public float roaches_deployed;

    // tg: code/modules/vending/wardrobes.dm:1  (GLOBAL_MANAGED(roaches_deployed, FALSE), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalroaches_deployed()
    {
        roaches_deployed = FALSE;
        Dm.append(ref gvars_datum_init_order, "roaches_deployed");
    }
}

// tg: code/modules/vending/wardrobes.dm:8
public partial class obj_machinery_vending_wardrobe : obj_machinery_vending
{
    // tg: code/modules/vending/wardrobes.dm:2  #define MOTHROACH_START_CHANCE 5, #define MAX_MOTHROACH_AMOUNT 3 (file-local)
    public const float MOTHROACH_START_CHANCE = 5;
    public const float MAX_MOTHROACH_AMOUNT = 3;

    // tg: code/modules/vending/wardrobes.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
            return dot;
        if (Dm.truthy(GLOB.roaches_deployed) || !Dm.truthy(is_station_level(z)) || !prob(MOTHROACH_START_CHANCE))
            return dot;
        for (float count = 1, count__end = rand(1, MAX_MOTHROACH_AMOUNT); count <= count__end; count++)
            Dm.@new<mob_living_basic_mothroach>(this);
        GLOB.roaches_deployed = TRUE;
        return dot;
    }

    // tg: code/modules/vending/wardrobes.dm:26
    public override async DmTask on_dispense(obj_item vended_item = null, float? dispense_returned = null)
    {
        obj_item_clothing food = vended_item as obj_item_clothing;
        if (food == null)
            return;
        foreach (mob_living_basic_mothroach roach in Dm.each<mob_living_basic_mothroach>(contents))
        {
            // Be slightly nicer on returned items
            float damage_mult = Dm.truthy(dispense_returned) ? 0.1f : 0.5f;
            await food.take_damage(Dm.num(food.get_integrity()) * damage_mult);
        }
    }

    // tg: code/modules/vending/wardrobes.dm:34
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        sound_effect ??= 1;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        foreach (mob_living_basic_mothroach roach in Dm.each<mob_living_basic_mothroach>(contents))
        {
            // roach.ai_controller.set_blackboard_key(BB_BASIC_MOB_FLEE_TARGET, src) //scatter away!
            Dm.Deferred("code/modules/vending/wardrobes.dm:37", "take_damage: the mothroach's AI flees the wardrobe (set_blackboard_key, the creatures' AI, C1)");
            await roach.forceMove(drop_location() as atom);
        }
        return dot;
    }
}
