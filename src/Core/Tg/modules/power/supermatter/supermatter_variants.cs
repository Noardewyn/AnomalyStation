// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/supermatter/supermatter_variants.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the M7 supermatter slice (G5, D163): the engine (Meta's), the shard and the wizard's small crystal. The var blocks are generated (D111).

/// Shard SM.
// tg: code/modules/power/supermatter/supermatter_variants.dm:13
public partial class obj_machinery_power_supermatter_crystal_shard : obj_machinery_power_supermatter_crystal
{
    // tg: code/modules/power/supermatter/supermatter_variants.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        register_context();
        return dot;
    }

    // tg: code/modules/power/supermatter/supermatter_variants.dm:31
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = null;
        dot = base.add_context(source, context, held_item, user);

        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(anchored) ? "Unanchor" : "Anchor";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }
}

// tg: code/modules/power/supermatter/supermatter_variants.dm:57
public partial class obj_machinery_power_supermatter_crystal_small : obj_machinery_power_supermatter_crystal
{
    // tg: code/modules/power/supermatter/supermatter_variants.dm:67
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_gps), "Adamantium Signal"));
        priority_announce("Anomalous crystal detected onboard. Location is marked on every GPS device.", "Nanotrasen Anomaly Department Announcement");
        return dot;
    }
}
