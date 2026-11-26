// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/DNA.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: GLOBAL_LIST_INIT(organ_process_order) (M2 P2: /proc/cmp_organ_slot_asc sorts a carbon's organs_slot with it) and the
// golem food lists (gaps SP3a, D349); the constants of DNA.dm are generated into Defines.

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

    /// Golem food datum singletons
    // tg: code/__DEFINES/DNA.dm:227  GLOBAL_LIST_INIT(golem_stack_food_types, list(...))
    public DmList golem_stack_food_types;

    // tg: code/__DEFINES/DNA.dm:227  (GLOBAL_MANAGED(golem_stack_food_types, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgolem_stack_food_types()
    {
        golem_stack_food_types = Dm.list(
            Dm.kv(GOLEM_FOOD_IRON, Dm.@new<datum_golem_food_buff_iron>()),
            Dm.kv(GOLEM_FOOD_GLASS, Dm.@new<datum_golem_food_buff_glass>()),
            Dm.kv(GOLEM_FOOD_URANIUM, Dm.@new<datum_golem_food_buff_uranium>()),
            Dm.kv(GOLEM_FOOD_SILVER, Dm.@new<datum_golem_food_buff_silver>()),
            Dm.kv(GOLEM_FOOD_PLASMA, Dm.@new<datum_golem_food_buff_plasma>()),
            Dm.kv(GOLEM_FOOD_GOLD, Dm.@new<datum_golem_food_buff_gold>()),
            Dm.kv(GOLEM_FOOD_DIAMOND, Dm.@new<datum_golem_food_buff_diamond>()),
            Dm.kv(GOLEM_FOOD_TITANIUM, Dm.@new<datum_golem_food_buff_titanium>()),
            Dm.kv(GOLEM_FOOD_PLASTEEL, Dm.@new<datum_golem_food_buff_plasteel>()),
            Dm.kv(GOLEM_FOOD_BANANIUM, Dm.@new<datum_golem_food_buff_bananium>()),
            Dm.kv(GOLEM_FOOD_BLUESPACE, Dm.@new<datum_golem_food_buff_bluespace>()),
            Dm.kv(GOLEM_FOOD_GIBTONITE, Dm.@new<datum_golem_food_buff_gibtonite>()),
            Dm.kv(GOLEM_FOOD_LIGHTBULB, Dm.@new<datum_golem_food_buff_lightbulb>()));
        Dm.append(ref gvars_datum_init_order, "golem_stack_food_types");
    }

    /// Associated list of stack types to a golem food
    // tg: code/__DEFINES/DNA.dm:244  GLOBAL_LIST_INIT(golem_stack_food_directory, list(...))
    public DmList golem_stack_food_directory;

    // tg: code/__DEFINES/DNA.dm:244  (GLOBAL_MANAGED(golem_stack_food_directory, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgolem_stack_food_directory()
    {
        golem_stack_food_directory = Dm.list(
            Dm.kv(typeof(obj_item_gibtonite), golem_stack_food_types[GOLEM_FOOD_GIBTONITE]),
            Dm.kv(typeof(obj_item_light), golem_stack_food_types[GOLEM_FOOD_LIGHTBULB]),
            Dm.kv(typeof(obj_item_stack_sheet_iron), golem_stack_food_types[GOLEM_FOOD_IRON]),
            Dm.kv(typeof(obj_item_stack_ore_iron), golem_stack_food_types[GOLEM_FOOD_IRON]),
            Dm.kv(typeof(obj_item_stack_sheet_glass), golem_stack_food_types[GOLEM_FOOD_GLASS]),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_uranium), golem_stack_food_types[GOLEM_FOOD_URANIUM]),
            Dm.kv(typeof(obj_item_stack_ore_uranium), golem_stack_food_types[GOLEM_FOOD_URANIUM]),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_silver), golem_stack_food_types[GOLEM_FOOD_SILVER]),
            Dm.kv(typeof(obj_item_stack_ore_silver), golem_stack_food_types[GOLEM_FOOD_SILVER]),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_plasma), golem_stack_food_types[GOLEM_FOOD_PLASMA]),
            Dm.kv(typeof(obj_item_stack_ore_plasma), golem_stack_food_types[GOLEM_FOOD_PLASMA]),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_gold), golem_stack_food_types[GOLEM_FOOD_GOLD]),
            Dm.kv(typeof(obj_item_stack_ore_gold), golem_stack_food_types[GOLEM_FOOD_GOLD]),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_diamond), golem_stack_food_types[GOLEM_FOOD_DIAMOND]),
            Dm.kv(typeof(obj_item_stack_ore_diamond), golem_stack_food_types[GOLEM_FOOD_DIAMOND]),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_titanium), golem_stack_food_types[GOLEM_FOOD_TITANIUM]),
            Dm.kv(typeof(obj_item_stack_ore_titanium), golem_stack_food_types[GOLEM_FOOD_TITANIUM]),
            Dm.kv(typeof(obj_item_stack_sheet_plasteel), golem_stack_food_types[GOLEM_FOOD_PLASTEEL]),
            Dm.kv(typeof(obj_item_stack_ore_bananium), golem_stack_food_types[GOLEM_FOOD_BANANIUM]),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_bananium), golem_stack_food_types[GOLEM_FOOD_BANANIUM]),
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), golem_stack_food_types[GOLEM_FOOD_BLUESPACE]),
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal_refined), golem_stack_food_types[GOLEM_FOOD_BLUESPACE]),
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal_artificial), golem_stack_food_types[GOLEM_FOOD_BLUESPACE]),
            Dm.kv(typeof(obj_item_stack_sheet_bluespace_crystal), golem_stack_food_types[GOLEM_FOOD_BLUESPACE]));
        Dm.append(ref gvars_datum_init_order, "golem_stack_food_directory");
    }
}
