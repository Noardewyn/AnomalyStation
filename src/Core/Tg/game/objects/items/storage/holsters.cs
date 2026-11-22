// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/holsters.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C: the whole file but for the chameleon holster's action (the chameleon clothes are M10+) and the psyker's. The holster
// grants the gun flip while worn on the belt or the suit slot; the filled holsters spawn their guns and speed loaders. The var blocks
// are generated (D111).

// tg: code/game/objects/items/storage/holsters.dm:2
public partial class obj_item_storage_belt_holster : obj_item_storage_belt
{
    // tg: code/game/objects/items/storage/holsters.dm:12
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, Dm.bitor(ITEM_SLOT_BELT, ITEM_SLOT_SUITSTORE))))
            ADD_TRAIT(user, TRAIT_GUNFLIP, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this))); // ADD_CLOTHING_TRAIT, expanded inline (rules 11.1 (d))
    }

    // tg: code/game/objects/items/storage/holsters.dm:17
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        REMOVE_TRAIT(user, TRAIT_GUNFLIP, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this))); // REMOVE_CLOTHING_TRAIT, expanded inline
        return dot;
    }
}

// tg: code/game/objects/items/storage/holsters.dm:26
public partial class obj_item_storage_belt_holster_energy_thermal : obj_item_storage_belt_holster_energy
{
    // tg: code/game/objects/items/storage/holsters.dm:30
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_gun_energy_laser_thermal_inferno>(this);
        Dm.@new<obj_item_gun_energy_laser_thermal_cryo>(this);
    }
}

// tg: code/game/objects/items/storage/holsters.dm:36
public partial class obj_item_storage_belt_holster_energy_disabler : obj_item_storage_belt_holster_energy
{
    // tg: code/game/objects/items/storage/holsters.dm:39
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_gun_energy_disabler>(this);
    }
}

// tg: code/game/objects/items/storage/holsters.dm:42
public partial class obj_item_storage_belt_holster_energy_laser_pistol : obj_item_storage_belt_holster_energy
{
    // tg: code/game/objects/items/storage/holsters.dm:45
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_gun_energy_laser_pistol>(this);
    }
}

// tg: code/game/objects/items/storage/holsters.dm:48
public partial class obj_item_storage_belt_holster_energy_smoothbore : obj_item_storage_belt_holster_energy
{
    // tg: code/game/objects/items/storage/holsters.dm:51
    public override async DmTask PopulateContents()
    {
        for (int i = 0; i < 2; i++)
            Dm.@new<obj_item_gun_energy_disabler_smoothbore>(this);
    }
}

// tg: code/game/objects/items/storage/holsters.dm:62
public partial class obj_item_storage_belt_holster_detective_full : obj_item_storage_belt_holster_detective
{
    // tg: code/game/objects/items/storage/holsters.dm:62
    public override async DmTask PopulateContents()
    {
        for (int i = 0; i < 2; i++)
            Dm.@new<obj_item_ammo_box_speedloader_c38>(this);
        Dm.@new<obj_item_gun_ballistic_revolver_c38_detective>(this);
    }
}

// tg: code/game/objects/items/storage/holsters.dm:68
public partial class obj_item_storage_belt_holster_detective_full_ert : obj_item_storage_belt_holster_detective_full
{
    // tg: code/game/objects/items/storage/holsters.dm:75
    public override async DmTask PopulateContents()
    {
        for (int i = 0; i < 2; i++)
            Dm.@new<obj_item_ammo_box_magazine_m45>(this);
        Dm.@new<obj_item_gun_ballistic_automatic_pistol_m1911>(this);
    }
}

// tg: code/game/objects/items/storage/holsters.dm:104
public partial class obj_item_storage_belt_holster_nukie_cowboy_full : obj_item_storage_belt_holster_nukie_cowboy
{
    // tg: code/game/objects/items/storage/holsters.dm:104
    public override async DmTask PopulateContents()
    {
        for (int i = 0; i < 2; i++)
            Dm.@new<obj_item_ammo_box_speedloader_c357>(this);
        Dm.@new<obj_item_gun_ballistic_revolver_cowboy_nuclear>(this);
    }
}
