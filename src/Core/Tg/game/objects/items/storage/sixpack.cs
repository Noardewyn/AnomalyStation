// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/sixpack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SR1 (D333): the file whole: the can ring (its sprite shows how many cans it holds) and its soda, beer, gamer and energy drink
// packs. Its storage (six cans or bottles) is in datums/storage/subtypes/others/misc.cs. The var blocks are generated (D111).

// tg: code/game/objects/items/storage/sixpack.dm:1
public partial class obj_item_storage_cans : obj_item_storage
{
    // tg: code/game/objects/items/storage/sixpack.dm:13
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins popping open a final cold one with the boys! It looks like [] trying to commit suicide!", user, user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/storage/sixpack.dm:17
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_storage_cans x) => x.icon_state), contents.len);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/storage/sixpack.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/items/storage/sixpack.dm:29
public partial class obj_item_storage_cans_sixsoda : obj_item_storage_cans
{
    // tg: code/game/objects/items/storage/sixpack.dm:29
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++)
        {
            Dm.@new<obj_item_reagent_containers_cup_soda_cans_cola>(this);
        }
    }
}

// tg: code/game/objects/items/storage/sixpack.dm:37
public partial class obj_item_storage_cans_sixbeer : obj_item_storage_cans
{
    // tg: code/game/objects/items/storage/sixpack.dm:37
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++)
        {
            Dm.@new<obj_item_reagent_containers_cup_soda_cans_beer>(this);
        }
    }
}

// tg: code/game/objects/items/storage/sixpack.dm:42
public partial class obj_item_storage_cans_sixgamerdrink : obj_item_storage_cans
{
    // tg: code/game/objects/items/storage/sixpack.dm:54
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++)
        {
            object chosen_gamer_drink = pick_weight(gamer_drink_options);
            Dm.@new((Type)chosen_gamer_drink, this);
        }
    }
}

// tg: code/game/objects/items/storage/sixpack.dm:60
public partial class obj_item_storage_cans_sixenergydrink : obj_item_storage_cans
{
    // tg: code/game/objects/items/storage/sixpack.dm:71
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++)
        {
            object chosen_energy_drink = pick_weight(energy_drink_options);
            Dm.@new((Type)chosen_energy_drink, this);
        }
    }
}
