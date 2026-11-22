// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST3 (D333): the file whole, the abandoned crate's loot spawners (each makes its loot list's things on its turf, then goes as
// every spawner does). One of the things they make stays inert until its package: the crate mimic (`/mob/living/basic/mimic/crate`),
// not-translated.md 207 (the bwoink sound mine is live since gaps ST5's mines). The var blocks are generated (D111).

// tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:6
public partial class obj_effect_spawner_abandoned_crate : obj_effect_spawner
{
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (LAZYLEN(loot) > 0)
        {
            foreach (object atom in Dm.each(loot))
            {
                for (float i = 1, i__end = Dm.num(loot[atom]); i <= i__end; i++)
                    Dm.@new((Type)atom, loc);
            }
        }
        return dot;
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:40
public partial class obj_effect_spawner_abandoned_crate_boda : obj_effect_spawner_abandoned_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        foreach (obj_item_circuitboard_machine_vendor board in Dm.each<obj_item_circuitboard_machine_vendor>(loc))
            board.set_type(typeof(obj_machinery_vending_sovietsoda));
        return dot;
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:57
public partial class obj_effect_spawner_abandoned_crate_mecha : obj_effect_spawner_abandoned_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object mecha = pick(subtypesof(typeof(obj_item_toy_mecha)));
        loot[mecha] = 1;

        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:85
public partial class obj_effect_spawner_abandoned_crate_gibtonite : obj_effect_spawner_abandoned_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:89
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        foreach (obj_item_gibtonite free_bomb in Dm.each<obj_item_gibtonite>(loc))
        {
            free_bomb.quality = rand(GIBTONITE_QUALITY_LOW, GIBTONITE_QUALITY_HIGH);
            free_bomb.GibtoniteReaction(null, "A secure loot closet has spawned a live");
        }
        return dot;
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:107
public partial class obj_effect_spawner_abandoned_crate_weed : obj_effect_spawner_abandoned_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object seed_type = pick(typesof(typeof(obj_item_seeds_cannabis)));
        object cannabis_type = pick(typesof(typeof(obj_item_food_grown_cannabis)));
        float weed_amount = rand(2, 4);

        for (float i = 1; i <= weed_amount; i++)
        {
            loot[seed_type] = (Dm.num(loot[seed_type] ?? 0f)) + 1;
            loot[cannabis_type] = (Dm.num(loot[cannabis_type] ?? 0f)) + 1;
        }

        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:126
public partial class obj_effect_spawner_abandoned_crate_mimic : obj_effect_spawner_abandoned_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:130
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (loc is obj_structure_closet_crate_secure_loot parent_crate)
            parent_crate.qdel_on_open = TRUE;
        return dot;
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:138
public partial class obj_effect_spawner_abandoned_crate_bwoink : obj_effect_spawner_abandoned_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates_loot.dm:143
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        foreach (obj_effect_mine_sound_bwoink mine in Dm.each<obj_effect_mine_sound_bwoink>(loc))
        {
            Waiting.NoSleep(mine.set_anchored(FALSE));
            mine.move_resist = MOVE_RESIST_DEFAULT;
        }
        return dot;
    }
}
