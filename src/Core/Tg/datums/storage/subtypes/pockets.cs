// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/pockets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the regular pockets and the pockets of the fedoras, the jackboots and work boots (the shoes' knife slot) and the pocket
// protector. The chef hat's, the clown shoes', the helmets' and the void cloak's pockets are left to their types' packages (J2 for the
// chef hat and the clown shoes, below); gaps CL1 (D333): the helmet pockets (the Russian helmet). The var blocks are generated.

///Regular pockets
// tg: code/datums/storage/subtypes/pockets.dm:2
public partial class datum_storage_pockets : datum_storage
{
    // tg: code/datums/storage/subtypes/pockets.dm:9
    public override object attempt_insert(obj_item to_insert = null, mob user = null, float? @override = null, float? force = null, float? messages = null)
    {
        object dot = base.attempt_insert(to_insert, user, @override, force, messages);
        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(silent) || Dm.truthy(@override))
            return dot;

        if (Dm.truthy(quickdraw))
            to_chat(user, span_notice(I18n.T("You discreetly slip [] into []. Right-click to remove it.", to_insert, parent)));
        else
            to_chat(user, span_notice(I18n.T("You discreetly slip [] into [].", to_insert, parent)));
        return dot;
    }
}

///Fedora pockets
// tg: code/datums/storage/subtypes/pockets.dm:37
public partial class datum_storage_pockets_small_fedora : datum_storage_pockets_small
{
    // tg: code/datums/storage/subtypes/pockets.dm:37
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(exception_hold_list: Dm.list(
            typeof(obj_item_katana),
            typeof(obj_item_toy_katana),
            typeof(obj_item_nullrod_claymore_katana),
            typeof(obj_item_energy_katana),
            typeof(obj_item_gun_ballistic_automatic_tommygun)
        ));
    }
}

///Shoe pockets
// tg: code/datums/storage/subtypes/pockets.dm:74
public partial class datum_storage_pockets_shoes : datum_storage_pockets
{
    // tg: code/datums/storage/subtypes/pockets.dm:80
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(
            can_hold_list: Dm.list(
                typeof(obj_item_knife),
                typeof(obj_item_spess_knife),
                typeof(obj_item_switchblade),
                typeof(obj_item_boxcutter),
                typeof(obj_item_pen),
                typeof(obj_item_flashlight_pen), //i mean cmon if a pen fits in there this does
                typeof(obj_item_scalpel),
                typeof(obj_item_dnainjector),
                typeof(obj_item_reagent_containers_syringe),
                typeof(obj_item_reagent_containers_applicator_pill),
                typeof(obj_item_reagent_containers_hypospray_medipen),
                typeof(obj_item_reagent_containers_dropper),
                typeof(obj_item_implanter),
                typeof(obj_item_screwdriver),
                typeof(obj_item_weldingtool_mini),
                typeof(obj_item_firing_pin),
                typeof(obj_item_suppressor),
                typeof(obj_item_ammo_box_magazine_m9mm),
                typeof(obj_item_ammo_box_magazine_m10mm),
                typeof(obj_item_ammo_box_magazine_m45),
                typeof(obj_item_ammo_box_magazine_toy_pistol),
                typeof(obj_item_ammo_casing),
                typeof(obj_item_lipstick),
                typeof(obj_item_cigarette),
                typeof(obj_item_lighter),
                typeof(obj_item_match),
                typeof(obj_item_holochip),
                typeof(obj_item_toy_crayon),
                typeof(obj_item_reagent_containers_cup_glass_flask)
            ),
            cant_hold_list: Dm.list(
                typeof(obj_item_screwdriver_power),
                typeof(obj_item_ammo_casing_rocket),
                typeof(obj_item_cigarette_pipe),
                typeof(obj_item_toy_crayon_spraycan)
            )
        );
    }
}

///Protector pocket
// tg: code/datums/storage/subtypes/pockets.dm:155
public partial class datum_storage_pockets_pocketprotector : datum_storage_pockets
{
    // tg: code/datums/storage/subtypes/pockets.dm:159
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list( //Same items as a PDA
            typeof(obj_item_pen),
            typeof(obj_item_toy_crayon),
            typeof(obj_item_lipstick),
            typeof(obj_item_flashlight_pen),
            typeof(obj_item_lipstick)
        ));
    }
}

// tg: code/datums/storage/subtypes/pockets.dm:53
public partial class datum_storage_pockets_chefhat : datum_storage_pockets
{
    // tg: code/datums/storage/subtypes/pockets.dm:59
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(typeof(obj_item_mob_holder), typeof(obj_item_food_deadmouse)));
    }

    // tg: code/datums/storage/subtypes/pockets.dm:71
    public override object can_insert(obj_item to_insert = null, mob user = null, float? messages = null, float? force = null)
    {
        object dot = base.can_insert(to_insert, user, messages, force);
        if (to_insert is obj_item_mob_holder) // ispickedupmob
        {
            obj_item_mob_holder mausholder = to_insert as obj_item_mob_holder;
            if (Dm.locate<mob_living_basic_mouse>(mausholder.contents) != null)
                return dot;
            return FALSE;
        }
        return dot;
    }
}

// tg: code/datums/storage/subtypes/pockets.dm:133
public partial class datum_storage_pockets_shoes_clown : datum_storage_pockets_shoes
{
    // tg: code/datums/storage/subtypes/pockets.dm:134
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(
            can_hold_list: Dm.list(typeof(obj_item_ammo_box_magazine_m10mm), typeof(obj_item_ammo_box_magazine_m45), typeof(obj_item_ammo_box_magazine_m9mm), typeof(obj_item_ammo_casing), typeof(obj_item_bikehorn), typeof(obj_item_cigarette), typeof(obj_item_dnainjector), typeof(obj_item_firing_pin), typeof(obj_item_holochip), typeof(obj_item_implanter), typeof(obj_item_knife), typeof(obj_item_lighter), typeof(obj_item_lipstick), typeof(obj_item_match), typeof(obj_item_pen), typeof(obj_item_flashlight_pen), typeof(obj_item_reagent_containers_cup_glass_flask), typeof(obj_item_reagent_containers_dropper), typeof(obj_item_reagent_containers_hypospray_medipen), typeof(obj_item_reagent_containers_syringe), typeof(obj_item_scalpel), typeof(obj_item_screwdriver), typeof(obj_item_spess_knife), typeof(obj_item_suppressor), typeof(obj_item_switchblade), typeof(obj_item_toy_crayon), typeof(obj_item_weldingtool_mini)),
            cant_hold_list: Dm.list(typeof(obj_item_ammo_casing_rocket), typeof(obj_item_cigarette_pipe), typeof(obj_item_screwdriver_power), typeof(obj_item_toy_crayon_spraycan)));
    }
}

///Helmet pockets
// tg: code/datums/storage/subtypes/pockets.dm:200
public partial class datum_storage_pockets_helmet : datum_storage_pockets
{
    // tg: code/datums/storage/subtypes/pockets.dm:205
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_reagent_containers_cup_glass_bottle_vodka),
            typeof(obj_item_reagent_containers_cup_glass_bottle_molotov),
            typeof(obj_item_reagent_containers_cup_glass_drinkingglass),
            typeof(obj_item_ammo_box_speedloader_strilka310)
        ));
    }
}
