// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/ordnance/_scipaper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (silicons SI3, D326): the scientific partners, whole (SSresearch makes them; their boosts discount the nodes). The papers
// themselves (/datum/scientific_paper) and the NT Frontier program that publishes them are a later research campaign. The var blocks
// are generated (D111).

/// Various informations on companies/scientific programs/journals etc that the players can sign on to.
// tg: code/modules/research/ordnance/_scipaper.dm:272
public partial class datum_scientific_partner : datum
{
    // tg: code/modules/research/ordnance/_scipaper.dm:284
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // Convey boosts to their associated nodes so that they can then be passed
        // to techweb UIs as static data.
        foreach (object node_path in Dm.each(boostable_nodes))
        {
            object discount_amount = boostable_nodes[node_path];
            datum_techweb_node node = SSresearch.techweb_nodes[node_path] as datum_techweb_node;
            LAZYSET(ref node.discount_boosts, TECHWEB_POINT_TYPE_GENERIC, discount_amount);
        }
    }

    // tg: code/modules/research/ordnance/_scipaper.dm:292
    public virtual object purchase_boost(datum_techweb purchasing_techweb = null, datum_techweb_node node = null)
    {
        object possible_boost = allowed_to_boost(purchasing_techweb, node.type);
        if (!Dm.truthy(possible_boost))
            return FALSE;
        object boost_amount = boostable_nodes[node.type];

        purchasing_techweb.boost_techweb_node(node, Dm.list(Dm.kv(TECHWEB_POINT_TYPE_GENERIC, boost_amount)));
        purchasing_techweb.scientific_cooperation[type] = Dm.num(purchasing_techweb.scientific_cooperation[type]) - Dm.num(boost_amount) * SCIENTIFIC_COOPERATION_PURCHASE_MULTIPLIER;
        if (Dm.eq(possible_boost, SCIPAPER_ALREADY_BOUGHT)) // Refund the original price
        {
            purchasing_techweb.adjust_points(TECHWEB_POINT_TYPE_GENERIC, boost_amount);
            return SCIPAPER_ALREADY_BOUGHT;
        }
        return TRUE;
    }

    // tg: code/modules/research/ordnance/_scipaper.dm:305
    public virtual object allowed_to_boost(datum_techweb purchasing_techweb = null, object node_path = null)
    {
        datum_techweb_node boosting_node = SSresearch.techweb_nodes[node_path] as datum_techweb_node;
        object boost_amount = boostable_nodes[node_path];

        if (Dm.num(purchasing_techweb.scientific_cooperation[type]) < Dm.num(boost_amount) * SCIENTIFIC_COOPERATION_PURCHASE_MULTIPLIER) // Too expensive
            return FALSE;
        if (Dm.truthy(purchasing_techweb.boosted_nodes[node_path]) && Dm.num(boosting_node.discount_boosts[TECHWEB_POINT_TYPE_GENERIC]) >= Dm.num(boost_amount)) // Already bought or we have a bigger discount
            return FALSE;
        if (Dm.truthy(purchasing_techweb.researched_nodes[node_path]))
            return SCIPAPER_ALREADY_BOUGHT;
        if (!Dm.truthy((purchasing_techweb.get_available_nodes() as DmList)[node_path])) // Not currently available
            return FALSE;
        return TRUE;
    }
}
