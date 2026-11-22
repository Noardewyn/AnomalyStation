// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/body_bags.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks are generated (D111).

/// Filled with one body. If folded, gives the parent type so we dont make infinite corpses
// tg: code/modules/lost_crew/body_bags.dm:14
public partial class obj_structure_closet_body_bag_lost_crew_with_body : obj_structure_closet_body_bag_lost_crew
{
    // tg: code/modules/lost_crew/body_bags.dm:20
    public override object attempt_fold(mob_living_carbon_human the_folder = null)
    {
        if (!Dm.truthy(body_spawned))
        {
            to_chat(the_folder, span_warning(I18n.T("The body inside of [] prevents you from folding it up!", this)));
            return FALSE;
        }
        return base.attempt_fold(the_folder);
    }

    // tg: code/modules/lost_crew/body_bags.dm:26
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);

        DmList recovered_items = Dm.list();
        DmList protected_items = Dm.list();
        DmList lost_crew_data = Dm.list();
        var corpse = await GLOB.lost_crew_manager.create_lost_crew(revivable: TRUE, recovered_items: recovered_items, protected_items: protected_items, body_data: lost_crew_data) as mob_living;
        await corpse.mind_initialize();
        await corpse.forceMove(this);
        body_spawned = TRUE;

        // Drop stuff like dropped limbs and organs with them in the bag
        foreach (obj @object in Dm.each<obj>(recovered_items))
            await @object.forceMove(this);

        // Spawn a mind lockbox with job stuffs for them to work with when they return
        if (Dm.truthy(protected_items.len) && corpse.mind != null)
        {
            var box = Dm.@new<obj_item_storage_lockbox_mind>(this);
            box.mind = corpse.mind;
            foreach (obj @object in Dm.each<obj>(protected_items))
                await @object.forceMove(box);
        }

        process_data(lost_crew_data);
    }

    // tg: code/modules/lost_crew/body_bags.dm:50
    public virtual void process_data(DmList crew_data = null)
    {
        if (!Dm.truthy(debug))
            return;

        var paper = Dm.@new<obj_item_paper>(this);
        paper.add_raw_text(english_list(crew_data), advanced_html: TRUE);
    }
}
