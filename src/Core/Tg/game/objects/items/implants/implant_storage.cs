// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implant_storage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3). The var blocks are generated (D111).

// tg: code/game/objects/items/implants/implant_storage.dm:1
public partial class obj_item_implant_storage : obj_item_implant
{
    // tg: code/game/objects/items/implants/implant_storage.dm:8
    public override async DmTask<object> activate(params object[] __extra)
    {
        object dot = await base.activate(__extra);
        if (atom_storage != null)
            await atom_storage.open_storage(imp_in);
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_storage.dm:12
    public override async DmTask<object> removed(mob_living source = null, float? silent = null, float? special = null)
    {
        silent ??= FALSE;
        special ??= FALSE;
        if (Dm.truthy(special))
            return await base.removed(source, silent, special);

        mob_living implantee = source;
        foreach (obj_item stored in Dm.each<obj_item>(contents))
            await stored.add_mob_blood(implantee);
        await atom_storage.remove_all();
        implantee.visible_message(span_warning(I18n.T("A bluespace pocket opens around [] as it exits [], spewing out its contents and rupturing the surrounding tissue!", this, implantee)));
        await implantee.apply_damage(20, BRUTE, BODY_ZONE_CHEST);
        QDEL_NULL(ref atom_storage);
        return await base.removed(source, silent, special);
    }

    // tg: code/game/objects/items/implants/implant_storage.dm:26
    [DmParams("target", "user", "silent", "force")]
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        silent ??= FALSE;
        force ??= FALSE;
        foreach (object X in Dm.each(target.implants))
        {
            if (istype(X, type))
            {
                obj_item_implant_storage imp_e = (obj_item_implant_storage)X;
                if (imp_e.atom_storage == null)
                {
                    await imp_e.create_storage(storage_type: typeof(datum_storage_implant));
                    qdel(this);
                    return TRUE;
                }
                else if (imp_e.atom_storage.max_slots < max_slot_stacking)
                {
                    imp_e.atom_storage.max_slots += Dm.initial(imp_e.atom_storage, (datum_storage s) => s.max_slots);
                    imp_e.atom_storage.max_total_storage += Dm.initial(imp_e.atom_storage, (datum_storage s) => s.max_total_storage);
                    return TRUE;
                }
                return FALSE;
            }
        }
        await create_storage(storage_type: typeof(datum_storage_implant));
        ADD_TRAIT(this, TRAIT_CONTRABAND_BLOCKER, INNATE_TRAIT);
        return await base.implant(target, user, silent, force, __extra);
    }
}
