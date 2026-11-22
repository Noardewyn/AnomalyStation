// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/boulder_processing/boulder_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the whole file. The var block is generated (D111). The artifact inside an artifact boulder (the strange
// relic, research) stays an inert item until research is ported.

///Boulders with special artificats that can give higher mining points
// tg: code/modules/mining/boulder_processing/boulder_types.dm:5
public partial class obj_item_boulder_artifact : obj_item_boulder
{
    private const float BONUS_MATS_MINIMUM = 1;
    private const float BONUS_MATS_MAXIMUM = 5;

    // tg: code/modules/mining/boulder_processing/boulder_types.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        artifact_inside = Dm.@new(artifact_type, this) as obj_item; /// This could be poggers for archaeology in the future.
        if (bonus_mat != null)
            Waiting.NoSleep(add_bonus_mats());
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/boulder_types.dm:22
    public override object Destroy(float? force = null)
    {
        qdel(artifact_inside);
        artifact_inside = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mining/boulder_processing/boulder_types.dm:26
    public override async DmTask convert_to_ore(atom target_destination = null)
    {
        await base.convert_to_ore(target_destination);
        await artifact_inside.forceMove(drop_location() as atom);
        artifact_inside = null;
    }

    // tg: code/modules/mining/boulder_processing/boulder_types.dm:31
    public override async DmTask break_apart()
    {
        artifact_inside = null;
        await base.break_apart();
    }

    // tg: code/modules/mining/boulder_processing/boulder_types.dm:35
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = (string)Dm.initial(this, "icon_state"); // Hardset to artifact sprites for consistency
        return dot;
    }

    /// Adds a random amount of material to an artifact boulder, determined by BONUS_MAT defines and of the type bonus_mat defined on the boulder.
    // tg: code/modules/mining/boulder_processing/boulder_types.dm:40
    public virtual async DmTask add_bonus_mats()
    {
        DmList bonus_mats = Dm.list();
        if (custom_materials != null)
            bonus_mats = custom_materials.Copy();
        bonus_mats[bonus_mat] = Dm.num(bonus_mats[bonus_mat]) + Dm.rand(BONUS_MATS_MINIMUM, BONUS_MATS_MAXIMUM) * SHEET_MATERIAL_AMOUNT;
        await set_custom_materials(bonus_mats);
    }
}

// tg: code/modules/mining/boulder_processing/boulder_types.dm:71
public partial class obj_item_boulder_gulag : obj_item_boulder
{
    /// Static list of all minerals to populate gulag boulders with.
    // tg: code/modules/mining/boulder_processing/boulder_types.dm:75
    private static DmList gulag_minerals => TgState.Current.obj_item_boulder_gulag__gulag_minerals ??= Dm.list(
        Dm.kv(typeof(datum_material_gold), 1),
        Dm.kv(typeof(datum_material_iron), 5),
        Dm.kv(typeof(datum_material_plasma), 4),
        Dm.kv(typeof(datum_material_silver), 2));

    // tg: code/modules/mining/boulder_processing/boulder_types.dm:72
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        float amount = Dm.rand(10, 13);
        Waiting.NoSleep(set_custom_materials(Dm.list(Dm.kv(pick_weight(gulag_minerals), SHEET_MATERIAL_AMOUNT * amount))));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/boulder_processing/boulder_types.dm:75  var/static/list/gulag_minerals
    internal DmList obj_item_boulder_gulag__gulag_minerals;
}
