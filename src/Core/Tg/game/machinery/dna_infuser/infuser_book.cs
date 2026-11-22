// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/infuser_book.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the DNA infusion book and the entries its InfuserBook window lists. The var block is generated (D111).

// tg: code/game/machinery/dna_infuser/infuser_book.dm:1
public partial class obj_item_tgui_book_manual_infuser : obj_item_tgui_book_manual
{
    // tg: code/game/machinery/dna_infuser/infuser_book.dm:9
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        // Collect all info from each intry.
        DmList entry_data = Dm.list();
        foreach (datum_infuser_entry entry in Dm.each_cast<datum_infuser_entry>(assoc_to_values(GLOB.infuser_entries) as DmList))
        {
            if (Dm.eq(entry.tier, DNA_MUTANT_UNOBTAINABLE))
                continue;
            DmList individual_entry_data = Dm.list();
            individual_entry_data["name"] = entry.name;
            individual_entry_data["infuse_mob_name"] = entry.infuse_mob_name;
            individual_entry_data["desc"] = entry.desc;
            individual_entry_data["threshold_desc"] = entry.threshold_desc;
            individual_entry_data["qualities"] = entry.qualities;
            individual_entry_data["tier"] = entry.tier;
            entry_data.Add(Dm.list(individual_entry_data));
        }
        data["entries"] = entry_data;
        return data;
    }
}
