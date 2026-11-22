// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/DNA.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only GLOBAL_LIST_INIT(organ_process_order) translated (M2 P2: /proc/cmp_organ_slot_asc sorts a carbon's
// organs_slot with it); the constants of DNA.dm are generated into Defines, golem_stack_food_types and
// golem_stack_food_directory come with the golems.

public partial class datum_controller_global_vars
{
    /// Defines how a mob's organs_slot is ordered
    /// Exists so Life()'s organ process order is consistent
    // tg: code/__DEFINES/DNA.dm:160  GLOBAL_LIST_INIT(organ_process_order, list(...))
    public DmList organ_process_order;

    // tg: code/__DEFINES/DNA.dm:160  (GLOBAL_MANAGED(organ_process_order, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalorgan_process_order()
    {
        organ_process_order = Dm.list(
            ORGAN_SLOT_BRAIN,
            ORGAN_SLOT_APPENDIX,
            ORGAN_SLOT_RIGHT_ARM_AUG,
            ORGAN_SLOT_LEFT_ARM_AUG,
            ORGAN_SLOT_LEFT_ARM_MUSCLE,
            ORGAN_SLOT_RIGHT_ARM_MUSCLE,
            ORGAN_SLOT_STOMACH,
            ORGAN_SLOT_STOMACH_AID,
            ORGAN_SLOT_BREATHING_TUBE,
            ORGAN_SLOT_EARS,
            ORGAN_SLOT_EYES,
            ORGAN_SLOT_LUNGS,
            ORGAN_SLOT_HEART,
            ORGAN_SLOT_ZOMBIE,
            ORGAN_SLOT_THRUSTERS,
            ORGAN_SLOT_HUD,
            ORGAN_SLOT_LIVER,
            ORGAN_SLOT_TONGUE,
            ORGAN_SLOT_VOICE,
            ORGAN_SLOT_ADAMANTINE_RESONATOR,
            ORGAN_SLOT_HEART_AID,
            ORGAN_SLOT_BRAIN_CEREBELLUM,
            ORGAN_SLOT_BRAIN_CNS,
            ORGAN_SLOT_PARASITE_EGG,
            ORGAN_SLOT_MONSTER_CORE,
            ORGAN_SLOT_XENO_PLASMAVESSEL,
            ORGAN_SLOT_XENO_HIVENODE,
            ORGAN_SLOT_XENO_RESINSPINNER,
            ORGAN_SLOT_XENO_ACIDGLAND,
            ORGAN_SLOT_XENO_NEUROTOXINGLAND,
            ORGAN_SLOT_XENO_EGGSAC,
            ORGAN_SLOT_FANGS);
        Dm.append(ref gvars_datum_init_order, "organ_process_order");
    }
}
