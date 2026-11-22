// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/holsters.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C: the holsters' storage, whole (the holder lists of the shoulder, energy, detective, chameleon and operative holsters); the
// vars (slots, sizes, sounds) are the generated var blocks (D111). A subtype's `holdables = list(...)` then `..()` passes the new list
// on as the seventh argument (WithHoldables).

// tg: code/datums/storage/subtypes/holsters.dm:2
public partial class datum_storage_holster : datum_storage
{
    // The arguments of a New with the seventh, holdables, replaced (DM's bare ..() passes the proc's current argument values).
    protected static object[] WithHoldables(object[] args, DmList holdables)
    {
        object[] result = new object[7];
        for (int i = 0; i < 6 && i < args.Length; i++)
            result[i] = args[i];
        result[6] = holdables;
        return result;
    }

    // tg: code/datums/storage/subtypes/holsters.dm:8
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound", "holdables")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        DmList holdables = Dm.arg(__extra, 6) as DmList;
        if (holdables != null && holdables.Count > 0)
        {
            set_holdable(holdables);
            return;
        }
        set_holdable(Dm.list(
            typeof(obj_item_gun_ballistic_automatic_pistol),
            typeof(obj_item_gun_ballistic_revolver),
            typeof(obj_item_gun_energy_e_gun_mini),
            typeof(obj_item_gun_energy_disabler),
            typeof(obj_item_gun_energy_dueling),
            typeof(obj_item_food_grown_banana),
            typeof(obj_item_gun_energy_laser_thermal),
            typeof(obj_item_gun_energy_laser_pistol),
            typeof(obj_item_gun_ballistic_rifle_boltaction), //fits if you make it an obrez
            typeof(obj_item_gun_energy_laser_captain),
            typeof(obj_item_gun_energy_e_gun_hos)
        ));
    }
}

///Energy holster
// tg: code/datums/storage/subtypes/holsters.dm:29
public partial class datum_storage_holster_energy : datum_storage_holster
{
    // tg: code/datums/storage/subtypes/holsters.dm:32
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound", "holdables")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(WithHoldables(__extra, Dm.list(
            typeof(obj_item_gun_energy_e_gun_mini),
            typeof(obj_item_gun_energy_disabler),
            typeof(obj_item_gun_energy_dueling),
            typeof(obj_item_food_grown_banana),
            typeof(obj_item_gun_energy_laser_thermal),
            typeof(obj_item_gun_energy_recharge_ebow),
            typeof(obj_item_gun_energy_laser_captain),
            typeof(obj_item_gun_energy_laser_pistol),
            typeof(obj_item_gun_energy_e_gun_hos)
        )));
    }
}

///Detective holster
// tg: code/datums/storage/subtypes/holsters.dm:48
public partial class datum_storage_holster_detective : datum_storage_holster
{
    // tg: code/datums/storage/subtypes/holsters.dm:51
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound", "holdables")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(WithHoldables(__extra, Dm.list(
            typeof(obj_item_gun_ballistic_automatic_pistol),
            typeof(obj_item_ammo_box_magazine_m9mm), // Pistol magazines.
            typeof(obj_item_ammo_box_magazine_m9mm_aps),
            typeof(obj_item_ammo_box_magazine_m10mm),
            typeof(obj_item_ammo_box_magazine_m45),
            typeof(obj_item_ammo_box_magazine_m50),
            typeof(obj_item_gun_ballistic_revolver),
            typeof(obj_item_ammo_box_speedloader), // Speedloaders, which includes stripper clips on a technicality.
            typeof(obj_item_ammo_box_magazine_toy_pistol),
            typeof(obj_item_gun_energy_e_gun_mini),
            typeof(obj_item_gun_energy_disabler),
            typeof(obj_item_gun_energy_dueling),
            typeof(obj_item_gun_energy_laser_thermal),
            typeof(obj_item_gun_energy_laser_captain),
            typeof(obj_item_gun_energy_laser_pistol),
            typeof(obj_item_gun_energy_e_gun_hos),
            typeof(obj_item_gun_ballistic_rifle_boltaction) //fits if you make it an obrez
        )));
    }
}

///Chameleon Holster
// tg: code/datums/storage/subtypes/holsters.dm:75
public partial class datum_storage_holster_chameleon : datum_storage_holster
{
    // tg: code/datums/storage/subtypes/holsters.dm:79
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound", "holdables")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(WithHoldables(__extra, Dm.list(
            typeof(obj_item_gun_ballistic_automatic_pistol),
            typeof(obj_item_ammo_box_magazine_m9mm),
            typeof(obj_item_ammo_box_magazine_m9mm_aps),
            typeof(obj_item_ammo_box_magazine_m10mm),
            typeof(obj_item_ammo_box_magazine_m45),
            typeof(obj_item_ammo_box_magazine_m50),
            typeof(obj_item_gun_ballistic_revolver),
            typeof(obj_item_ammo_box_speedloader),
            typeof(obj_item_ammo_box_magazine_toy_pistol),
            typeof(obj_item_gun_energy_recharge_ebow),
            typeof(obj_item_gun_energy_e_gun_mini),
            typeof(obj_item_gun_energy_disabler),
            typeof(obj_item_gun_energy_dueling),
            typeof(obj_item_gun_energy_laser_captain),
            typeof(obj_item_gun_energy_laser_pistol),
            typeof(obj_item_gun_energy_e_gun_hos)
        )));
    }
}

///Nukie holster
// tg: code/datums/storage/subtypes/holsters.dm:102
public partial class datum_storage_holster_nukie : datum_storage_holster
{
    // tg: code/datums/storage/subtypes/holsters.dm:106
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound", "holdables")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(WithHoldables(__extra, Dm.list(
            typeof(obj_item_gun), // ALL guns.
            typeof(obj_item_ammo_box_magazine), // ALL magazines.
            typeof(obj_item_ammo_box_speedloader), // ALL speedloaders (there's 3 types at time of writing so it's probably fine)
            typeof(obj_item_ammo_casing), // For shotgun shells, rockets, launcher grenades, and a few other things.
            typeof(obj_item_grenade) // All regular grenades, the big grenade launcher fires these.
        )));
    }
}
