// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/crafting/ranged_weapon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps GU1 (D333): the file's procs, the gun kits' upgrade recipes keeping the subtypes of the gun they take out of their parts (the
// advanced energy gun, the temperature gun, the X-ray, hellfire and ion carbine lasers, the regal condor). The recipes themselves are
// var blocks (generated, D111).

// tg: code/datums/components/crafting/ranged_weapon.dm:59
public partial class datum_crafting_recipe_advancedegun : datum_crafting_recipe
{
    // tg: code/datums/components/crafting/ranged_weapon.dm:69
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        LAZYADD(ref blacklist, typecacheof(typeof(obj_item_gun_energy_e_gun), ignore_root_path: TRUE));
        await base.New(__extra);
    }
}

// tg: code/datums/components/crafting/ranged_weapon.dm:73
public partial class datum_crafting_recipe_tempgun : datum_crafting_recipe
{
    // tg: code/datums/components/crafting/ranged_weapon.dm:83
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        LAZYADD(ref blacklist, typecacheof(typeof(obj_item_gun_energy_e_gun), ignore_root_path: TRUE));
        await base.New(__extra);
    }
}

// tg: code/datums/components/crafting/ranged_weapon.dm:112
public partial class datum_crafting_recipe_laser : datum_crafting_recipe
{
    /// We will use the same blacklist for every type here to avoid repeating lists
    // tg: code/datums/components/crafting/ranged_weapon.dm:115  var/static/list/laser_blacklist
    public static ref DmList laser_blacklist => ref TgState.Current.datum_crafting_recipe_laser__laser_blacklist;

    // tg: code/datums/components/crafting/ranged_weapon.dm:116
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        if (laser_blacklist == null)
            laser_blacklist = typecacheof(typeof(obj_item_gun_energy_laser), ignore_root_path: TRUE) as DmList;
        blacklist = laser_blacklist;
        await base.New(__extra);
    }
}

// tg: code/datums/components/crafting/ranged_weapon.dm:268
public partial class datum_crafting_recipe_deagle_prime : datum_crafting_recipe
{
    // tg: code/datums/components/crafting/ranged_weapon.dm:288
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        LAZYADD(ref blacklist, typecacheof(typeof(obj_item_gun_ballistic_automatic_pistol), ignore_root_path: TRUE));
        await base.New(__extra);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/crafting/ranged_weapon.dm:115  /datum/crafting_recipe/laser/var/static/list/laser_blacklist
    internal DmList datum_crafting_recipe_laser__laser_blacklist;
}
