// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/others/extract_inventory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the reproductive crossbreed's stomach, three monkey cubes that it digests into one to four extracts of its colour.
// The var block is generated (D111).

// tg: code/datums/storage/subtypes/others/extract_inventory.dm:2
public partial class datum_storage_extract_inventory : datum_storage
{
    // tg: code/datums/storage/subtypes/others/extract_inventory.dm:13
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_food_monkeycube));

        obj_item_slimecross_reproductive parent_slime = Dm.arg(__extra, 0) as obj_item_slimecross_reproductive;
        if (!(parent_slime is obj_item_slimecross_reproductive))
        {
            atom given = Dm.arg(__extra, 0) as atom;
            stack_trace(Dm.text("storage subtype ([]) incompatible with [] ([])", type, given, given?.type), "code/datums/storage/subtypes/others/extract_inventory.dm", 24);
            qdel(this);
        }
    }

    // tg: code/datums/storage/subtypes/others/extract_inventory.dm:27
    public virtual async DmTask processCubes(mob user = null)
    {
        obj_item_slimecross_reproductive parentSlimeExtract = parent as obj_item_slimecross_reproductive;
        if (real_location.contents.len >= Dm.num(max_slots))
        {
            QDEL_LIST(parentSlimeExtract.contents);
            await createExtracts(user);
        }
    }

    // tg: code/datums/storage/subtypes/others/extract_inventory.dm:33
    public virtual async DmTask createExtracts(mob user = null)
    {
        await DmTask.CompletedTask;
        obj_item_slimecross_reproductive parentSlimeExtract = parent as obj_item_slimecross_reproductive;

        float cores = rand(1, 4);
        playsound(parentSlimeExtract, "sound/effects/splat.ogg", 40, TRUE);
        parentSlimeExtract.last_produce = world.time;
        to_chat(user, span_notice(I18n.T("[] briefly swells to a massive size, and expels [] extract[]!", parentSlimeExtract, cores, cores > 1 ? "s" : "")));
        for (int i = 1; i <= cores; i++)
            Dm.@new(parentSlimeExtract.extract_type as Type, parentSlimeExtract.drop_location());
    }
}
