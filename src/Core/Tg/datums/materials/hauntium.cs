// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/hauntium.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package SK1, D333, with the hauntium sheet it is made of): an item made of hauntium is haunted (it floats and
// glows ghost-white), a bodypart or organ made of it is a ghost's. The var block is generated (D111).

// tg: code/datums/materials/hauntium.dm:1
public partial class datum_material_hauntium : datum_material
{
    // tg: code/datums/materials/hauntium.dm:23
    public override async DmTask on_main_applied(atom source = null, object mat_amount = null, object multiplier = null)
    {
        await base.on_main_applied(source, mat_amount, multiplier);
        if (!isitem(source))
            return;
        obj_item source_item = (obj_item)source;
        source_item.make_haunted(INNATE_TRAIT, "#f8f8ff");
        if (isbodypart(source))
        {
            obj_item_bodypart bodypart = (obj_item_bodypart)source;
            if (!Dm.truthy(Dm.bitand(Dm.initial(bodypart, (obj_item_bodypart b) => b.bodytype), BODYTYPE_GHOST)))
                Dm.combine(ref bodypart.bodytype, BODYTYPE_GHOST);
        }
        if (isorgan(source))
        {
            obj_item_organ organ = (obj_item_organ)source;
            if (!Dm.truthy(Dm.bitand(Dm.initial(organ, (obj_item_organ o) => o.organ_flags), ORGAN_GHOST)))
                Dm.combine(ref organ.organ_flags, ORGAN_GHOST);
        }
    }

    // tg: code/datums/materials/hauntium.dm:37
    public override void on_main_removed(atom source = null, object mat_amount = null, object multiplier = null)
    {
        base.on_main_removed(source, mat_amount, multiplier);
        if (!isitem(source))
            return;
        obj_item source_item = (obj_item)source;
        source_item.remove_haunted(INNATE_TRAIT);
        if (isbodypart(source))
        {
            obj_item_bodypart bodypart = (obj_item_bodypart)source;
            if (!Dm.truthy(Dm.bitand(Dm.initial(bodypart, (obj_item_bodypart b) => b.bodytype), BODYTYPE_GHOST)))
                Dm.mask(ref bodypart.bodytype, ~(int)BODYTYPE_GHOST);
        }
        if (isorgan(source))
        {
            obj_item_organ organ = (obj_item_organ)source;
            if (!Dm.truthy(Dm.bitand(Dm.initial(organ, (obj_item_organ o) => o.organ_flags), ORGAN_GHOST)))
                Dm.mask(ref organ.organ_flags, ~(int)ORGAN_GHOST);
        }
    }
}
