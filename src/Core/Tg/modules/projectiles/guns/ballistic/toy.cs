// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/ballistic/toy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the Donksoft foam force guns. The SMG, the pistol, the Donksoft C-20r and L6 SAW only set vars; the
// riot pistol comes with a riot dart magazine, and the foam force shotgun and crossbow throw away a spent dart's casing. The foam
// darts are ammunition/ballistic/foam.dm, their magazines boxes_magazines/external/toy.dm. The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/ballistic/toy.dm:34
public partial class obj_item_gun_ballistic_automatic_pistol_riot : obj_item_gun_ballistic_automatic_pistol
{
    // tg: code/modules/projectiles/guns/ballistic/toy.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        magazine = Dm.@new<obj_item_ammo_box_magazine_toy_pistol_riot>(this);
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/projectiles/guns/ballistic/toy.dm:41
public partial class obj_item_gun_ballistic_shotgun_toy : obj_item_gun_ballistic_shotgun
{
    // tg: code/modules/projectiles/guns/ballistic/toy.dm:58
    public override async DmTask handle_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        empty_chamber ??= TRUE;
        from_firing ??= TRUE;
        chamber_next_round ??= TRUE;
        await base.handle_chamber(empty_chamber, from_firing, chamber_next_round);
        if (chambered != null && chambered.loaded_projectile == null)
            qdel(chambered);
    }
}
