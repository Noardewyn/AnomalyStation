// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/secure/freezer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'.

// tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:2
public partial class obj_structure_closet_secure_closet_freezer : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:19
    public override async DmTask process_internal_air(object seconds_per_tick = null)
    {
        if (Dm.truthy(opened))
        {
            datum_gas_mixture current_exposed_air = await loc.return_air() as datum_gas_mixture;
            if (current_exposed_air == null)
                return;
            // The internal air won't cool down the external air when the freezer is opened.
            internal_air.temperature = Math.Max(current_exposed_air.temperature, internal_air.temperature);
            await base.process_internal_air(seconds_per_tick);
            return;
        }
        else
        {
            if (internal_air.temperature <= minimum_temperature)
                return;
            float temperature_decrease_this_tick = Math.Min(cooling_rate_per_second * Dm.num(seconds_per_tick), internal_air.temperature - minimum_temperature);
            internal_air.temperature -= temperature_decrease_this_tick;
        }
    }

    // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:33
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.truthy(jones))
            return base.ex_act(severity, target, __extra);
        jones = TRUE;
        Dm.mask(ref flags_1, Dm.bitnot(PREVENT_CONTENTS_EXPLOSION_1));
        return FALSE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:40
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_assembly_igniter_condenser>(drop_location());
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:83
public partial class obj_structure_closet_secure_closet_freezer_meat : obj_structure_closet_secure_closet_freezer
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:86
    public override object update_name(float? updates = null)
    {
        name = Dm.truthy(check_holidays(VEGAN_DAY)) ? "vegan fridge" : "meat fridge";
        return base.update_name(updates);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:90
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        if (!Dm.truthy(check_holidays(VEGAN_DAY)))
        {
            for (float i = 1; i <= 4; i++)
                Dm.@new<obj_item_food_meat_slab_monkey>(this);
        }
        Dm.@new<obj_effect_spawner_holiday_meat_ingredient>(this); //this contains holiday specials (and vegan day stuff)
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:116
public partial class obj_structure_closet_secure_closet_freezer_fridge : obj_structure_closet_secure_closet_freezer
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:116
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 5; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:118
        {
            Dm.@new<obj_item_reagent_containers_condiment_milk>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:119
            Dm.@new<obj_item_reagent_containers_condiment_soymilk>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:120
        }
        for (float i = 1; i <= 2; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:121
        {
            Dm.@new<obj_item_storage_fancy_egg_box>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:122
        }
        Dm.@new<obj_effect_spawner_holiday_liquid_ingredient>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:123
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:54
public partial class obj_structure_closet_secure_closet_freezer_kitchen : obj_structure_closet_secure_closet_freezer
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:54
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:56
        {
            Dm.@new<obj_item_reagent_containers_condiment_flour>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:57
        }
        Dm.@new<obj_item_reagent_containers_condiment_rice>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:58
        Dm.@new<obj_item_reagent_containers_condiment_sugar>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:59
        Dm.@new<obj_effect_spawner_holiday_powdered_ingredient>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:60
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:70
public partial class obj_structure_closet_secure_closet_freezer_kitchen_maintenance : obj_structure_closet_secure_closet_freezer_kitchen
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:70
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 5; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:72
        {
            Dm.@new<obj_item_reagent_containers_condiment_milk>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:73
            Dm.@new<obj_item_reagent_containers_condiment_soymilk>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:74
        }
        for (float i = 1; i <= 2; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:75
        {
            Dm.@new<obj_item_storage_fancy_egg_box>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:76
        }
        Dm.@new<obj_effect_spawner_holiday_liquid_ingredient>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:77
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:105  (gaps ST3, D333)
public partial class obj_structure_closet_secure_closet_freezer_gulag_fridge : obj_structure_closet_secure_closet_freezer
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:107
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:108
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:109
        {
            Dm.@new<obj_item_reagent_containers_cup_glass_bottle_beer_light>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:110
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:138  (gaps ST3, D333)
public partial class obj_structure_closet_secure_closet_freezer_money : obj_structure_closet_secure_closet_freezer
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:142
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:143
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:144
        {
            Dm.@new<obj_item_stack_spacecash_c1000>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:145
        }
        for (float i = 1; i <= 5; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:146
        {
            Dm.@new<obj_item_stack_spacecash_c500>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:147
        }
        for (float i = 1; i <= 6; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:148
        {
            Dm.@new<obj_item_stack_spacecash_c200>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:149
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:152  (gaps ST3, D333)
public partial class obj_structure_closet_secure_closet_freezer_cream_pie : obj_structure_closet_secure_closet_freezer
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:156
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:157
        Dm.@new<obj_item_food_pie_cream>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/freezer.dm:158
    }
}
