// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/crates/secure.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate G4c, D316: the united surplus crate's syndicrate stands on it): the secure crate base; the pizza, blood, trash cart
// and owned crates' contents come with their packages. The bomber log is a comment. The var blocks are generated (D111).

// tg: code/game/objects/structures/crates_lockers/crates/secure.dm:1
public partial class obj_structure_closet_crate_secure : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_MISSING_ITEM_ERROR, TRAIT_GENERIC);
        ADD_TRAIT(this, TRAIT_NO_MANIFEST_CONTENTS_ERROR, TRAIT_GENERIC);
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:27
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        if (prob(tamperproof) && Dm.num(damage_amount) >= DAMAGE_PRECISION)
        {
            await boom();
            return null;
        }
        return await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
    }

    // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:33
    public virtual async DmTask boom(mob user = null)
    {
        if (user != null)
        {
            to_chat(user, span_danger(I18n.T("The crate's anti-tamper system activates!")));
            log_bomber(user, "has detonated a", this);
        }
        await dump_contents();
        await explosion(this, heavy_impact_range: 1, light_impact_range: 5, flash_range: 5);
        qdel(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/secure.dm:73  (gaps ST3, D333)
public partial class obj_structure_closet_crate_secure_freezer_pizza : obj_structure_closet_crate_secure_freezer
{
    // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:78
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:79
        Dm.@new<obj_effect_spawner_random_food_or_drink_pizzaparty>(this); // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:80
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/secure.dm:157  (gaps ST3, D333)
public partial class obj_structure_closet_crate_secure_freezer_interdyne_blood : obj_structure_closet_crate_secure_freezer_interdyne
{
    // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:160
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:161
        for (float i = 1; i <= 13; i++) // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:162
        {
            Dm.@new<obj_item_reagent_containers_blood_random>(this); // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:163
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/secure.dm:139  (gaps ST3, D333)
public partial class obj_structure_closet_crate_secure_trashcart_filled : obj_structure_closet_crate_secure_trashcart
{
    // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:139
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:140
        for (float i = 1, i__end = rand(8, 12); i <= i__end; i++) // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:141
        {
            Dm.@new<obj_effect_spawner_random_trash_deluxe_garbage>(this); // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:142
            if (prob(35)) // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:143
            {
                Dm.@new<obj_effect_spawner_random_trash_garbage>(this); // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:144
            }
        }
        for (float i = 1, i__end = rand(4, 6); i <= i__end; i++) // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:145
        {
            if (prob(30)) // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:146
            {
                Dm.@new<obj_item_storage_bag_trash_filled>(this); // tg: code/game/objects/structures/crates_lockers/crates/secure.dm:147
            }
        }
    }
}
