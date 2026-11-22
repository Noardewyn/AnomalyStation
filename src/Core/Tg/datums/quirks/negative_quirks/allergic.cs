// Translated from tgstation (snapshot 1d99bd7f): code/datums/quirks/negative_quirks/allergic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps P3): only GLOB.allergy_reagent_blacklist, which the chemical allergy mutation (mutations/body.dm:815) subtracts;
// the medicine allergy quirk (/datum/quirk/item_quirk/allergic) comes with the quirks (M10).

// tg: code/datums/quirks/negative_quirks/allergic.dm:2 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars
{
    /// List of medicine reagents that we generally always want to be usable for a mob
    // tg: code/datums/quirks/negative_quirks/allergic.dm:2  GLOBAL_LIST_INIT(allergy_reagent_blacklist, typecacheof(list(...)))
    public DmList allergy_reagent_blacklist;

    // tg: code/datums/quirks/negative_quirks/allergic.dm:2  (GLOBAL_MANAGED(allergy_reagent_blacklist, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalallergy_reagent_blacklist()
    {
        allergy_reagent_blacklist = typecacheof(Dm.list(
            typeof(datum_reagent_medicine_adminordrazine),
            typeof(datum_reagent_medicine_albuterol),
            typeof(datum_reagent_medicine_changelingadrenaline),
            typeof(datum_reagent_medicine_changelinghaste),
            typeof(datum_reagent_medicine_coagulant_banana_peel),
            typeof(datum_reagent_medicine_coagulant_seraka_extract),
            typeof(datum_reagent_medicine_cordiolis_hepatico),
            typeof(datum_reagent_medicine_diphenhydramine),
            typeof(datum_reagent_medicine_epinephrine),
            typeof(datum_reagent_medicine_insulin),
            typeof(datum_reagent_medicine_muscle_stimulant),
            typeof(datum_reagent_medicine_oculine_flumpuline),
            typeof(datum_reagent_medicine_omnizine_godblood),
            typeof(datum_reagent_medicine_omnizine_protozine),
            typeof(datum_reagent_medicine_regen_jelly),
            typeof(datum_reagent_medicine_salglu_solution),
            typeof(datum_reagent_medicine_sansufentanyl),
            typeof(datum_reagent_medicine_strange_reagent),
            typeof(datum_reagent_medicine_synaphydramine),
            typeof(datum_reagent_medicine_synaptizine_synaptizinevirusfood),
            typeof(datum_reagent_medicine_syndicate_nanites))) as DmList;
        Dm.append(ref gvars_datum_init_order, "allergy_reagent_blacklist");
    }
}
