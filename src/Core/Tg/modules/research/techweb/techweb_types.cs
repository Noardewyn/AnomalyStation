// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/techweb/techweb_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): the techweb types, whole: the station's science techweb, the old station's and the admins', and the
// autounlocking techwebs of the lathes and printers. The var blocks are generated (D111).

/**
 * Global Science techweb for RND consoles
 */
// tg: code/modules/research/techweb/techweb_types.dm:4
public partial class datum_techweb_science : datum_techweb
{
    // tg: code/modules/research/techweb/techweb_types.dm:9
    public override async DmTask<bool> research_node(object node = null, float? force = null, float? auto_adjust_cost = null, float? get_that_dosh = null, atom research_source = null)
    {
        bool dot = await base.research_node(node, force, auto_adjust_cost, get_that_dosh, research_source);
        if (!dot)
            return dot;

        if (ispath(node))
            node = SSresearch.techweb_nodes[node];
        await ((datum_techweb_node)node).on_station_research(research_source);
        return dot;
    }
}

// tg: code/modules/research/techweb/techweb_types.dm:18
public partial class datum_techweb_oldstation : datum_techweb
{
    // tg: code/modules/research/techweb/techweb_types.dm:23
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        await research_node(typeof(datum_techweb_node_oldstation_surgery), TRUE, TRUE, FALSE);
    }
}

/**
 * Admin techweb that has everything unlocked by default
 */
// tg: code/modules/research/techweb/techweb_types.dm:30
public partial class datum_techweb_admin : datum_techweb
{
    // tg: code/modules/research/techweb/techweb_types.dm:34
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        foreach (object node_path in Dm.each(SSresearch.techweb_nodes))
            await research_node(SSresearch.techweb_nodes[node_path], TRUE, TRUE, FALSE);
        adjust_all_points(INFINITY);
        hidden_nodes.Cut();
    }
}

/**
 * Techweb node that automatically unlocks a given buildtype.
 * Saved in GLOB.autounlock_techwebs and used to prevent
 * creating new ones each time it's needed.
 */
// tg: code/modules/research/techweb/techweb_types.dm:48
public partial class datum_techweb_autounlocking : datum_techweb
{
    // tg: code/modules/research/techweb/techweb_types.dm:54
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        foreach (object design_path in Dm.each(SSresearch.techweb_designs))
        {
            datum_design design = SSresearch.techweb_designs[design_path] as datum_design;
            if (!Dm.truthy(Dm.bitand(design.build_type, allowed_buildtypes)))
                continue;
            if (Dm.@in(RND_CATEGORY_INITIAL, design.category))
                add_design(design_path);
            if (Dm.@in(RND_CATEGORY_HACKED, design.category))
                add_design(design_path, add_to: hacked_designs);
        }
    }

    // tg: code/modules/research/techweb/techweb_types.dm:64
    public override bool add_design(object design = null, float? custom = null, DmList add_to = null)
    {
        if (ispath(design))
            design = SSresearch.techweb_designs[design];
        if (design is not datum_design added || !Dm.truthy(Dm.bitand(added.build_type, allowed_buildtypes)))
            return false;
        return base.add_design(design, custom, add_to);
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/modules/research/techweb/techweb_types.dm:42  GLOBAL_LIST_EMPTY(autounlock_techwebs)
    public DmList autounlock_techwebs;

    public virtual void InitGlobalautounlock_techwebs()
    {
        autounlock_techwebs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "autounlock_techwebs");
    }
}
