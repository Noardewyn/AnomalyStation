// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/external/toy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the foam force magazines' pictures: the SMG magazine full or empty, the Donksoft SMG's drawn with
// a dart tip of each pair of rounds in it in its colour, the Donksoft box magazine stepping by ten rounds. The pistol magazine
// only sets vars. The var blocks are generated (D111).

// tg: code/modules/projectiles/boxes_magazines/external/toy.dm:8
public partial class obj_item_ammo_box_magazine_toy_smg : obj_item_ammo_box_magazine_toy
{
    // tg: code/modules/projectiles/boxes_magazines/external/toy.dm:15
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-[]", base_icon_state, LAZYLEN(stored_ammo) != 0 ? "full" : "empty");
        return dot;
    }
}

// tg: code/modules/projectiles/boxes_magazines/external/toy.dm:31
public partial class obj_item_ammo_box_magazine_toy_smgm45 : obj_item_ammo_box_magazine_toy
{
    // tg: code/modules/projectiles/boxes_magazines/external/toy.dm:39
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-base", base_icon_state);
        return dot;
    }

    // tg: code/modules/projectiles/boxes_magazines/external/toy.dm:43
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (LAZYLEN(stored_ammo) == 0)
            return dot;
        for (float i = 1, i__end = stored_ammo.len; i <= i__end; i++)
        {
            float round_number = round(i, 2); //i meant the number of the round in the magazine, but i guess it's a round number too lol.
            if (round_number == i) //only count odd numbers.
                continue;
            object boolet = stored_ammo[i];
            Dm.append(ref dot, Dm.text("c20r45-foam-[]-[]", Dm.initial(boolet, (obj_item_ammo_casing_foam_dart p) => p.tip_color), round_number));
        }
        return dot;
    }
}

// tg: code/modules/projectiles/boxes_magazines/external/toy.dm:60
public partial class obj_item_ammo_box_magazine_toy_m762 : obj_item_ammo_box_magazine_toy
{
    // tg: code/modules/projectiles/boxes_magazines/external/toy.dm:67
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-[]", base_icon_state, round(ammo_count(), 10));
        return dot;
    }
}
