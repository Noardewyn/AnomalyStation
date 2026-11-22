// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/combat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: GLOBAL_LIST_INIT(shove_disarming_types) translated (M2 integration: a shove reads it, living_defense.dm:877);
// the constants of combat.dm are generated into Defines. M4a P1: CALCULATE_FORCE (the item attack chain, item_attack.dm).
// M6 slice 2: the body zone lists (:300-304; wounds and dismemberment read them). M6 slice 6: InitGlobalall_body_zones was
// misnamed, so GLOB.all_body_zones stayed null (the global_vars New runs the InitGlobal procs by their DM names). M7 slice 1:
// ELECTROCUTE_DAMAGE (a powernet's shock damage, powernet.dm:95).

public partial class datum_controller_global_vars
{
    //Shove disarming item list
    // tg: code/__DEFINES/combat.dm:188  GLOBAL_LIST_INIT(shove_disarming_types, typecacheof(list(/obj/item/gun)))
    public DmList shove_disarming_types;

    // tg: code/__DEFINES/combat.dm:188  (GLOBAL_MANAGED(shove_disarming_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalshove_disarming_types()
    {
        shove_disarming_types = typecacheof(Dm.list(typeof(obj_item_gun))) as DmList;
        Dm.append(ref gvars_datum_init_order, "shove_disarming_types");
    }

    // These lists are ordered as bodyparts would be ordered
    // tg: code/__DEFINES/combat.dm:300  GLOBAL_LIST_INIT(all_body_zones, list(...))
    public DmList all_body_zones;

    // tg: code/__DEFINES/combat.dm:300  (GLOBAL_MANAGED(all_body_zones, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_body_zones()
    {
        all_body_zones = Dm.list(BODY_ZONE_CHEST, BODY_ZONE_HEAD, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM);
        Dm.append(ref gvars_datum_init_order, "all_body_zones");
    }

    // tg: code/__DEFINES/combat.dm:301  GLOBAL_LIST_INIT(limb_zones, list(...))
    public DmList limb_zones;

    // tg: code/__DEFINES/combat.dm:301  (GLOBAL_MANAGED(limb_zones, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGloballimb_zones()
    {
        limb_zones = Dm.list(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM);
        Dm.append(ref gvars_datum_init_order, "limb_zones");
    }

    // tg: code/__DEFINES/combat.dm:302  GLOBAL_LIST_INIT(arm_zones, list(...))
    public DmList arm_zones;

    // tg: code/__DEFINES/combat.dm:302  (GLOBAL_MANAGED(arm_zones, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalarm_zones()
    {
        arm_zones = Dm.list(BODY_ZONE_L_ARM, BODY_ZONE_R_ARM);
        Dm.append(ref gvars_datum_init_order, "arm_zones");
    }

    // tg: code/__DEFINES/combat.dm:303  GLOBAL_LIST_INIT(leg_zones, list(...))
    public DmList leg_zones;

    // tg: code/__DEFINES/combat.dm:303  (GLOBAL_MANAGED(leg_zones, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGloballeg_zones()
    {
        leg_zones = Dm.list(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG);
        Dm.append(ref gvars_datum_init_order, "leg_zones");
    }

    // tg: code/__DEFINES/combat.dm:304  GLOBAL_LIST_INIT(all_precise_body_zones, list(...))
    public DmList all_precise_body_zones;

    // tg: code/__DEFINES/combat.dm:304  (GLOBAL_MANAGED(all_precise_body_zones, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_precise_body_zones()
    {
        all_precise_body_zones = Dm.list(BODY_ZONE_PRECISE_EYES, BODY_ZONE_PRECISE_MOUTH, BODY_ZONE_PRECISE_GROIN, BODY_ZONE_PRECISE_L_HAND, BODY_ZONE_PRECISE_R_HAND, BODY_ZONE_PRECISE_L_FOOT, BODY_ZONE_PRECISE_R_FOOT);
        Dm.append(ref gvars_datum_init_order, "all_precise_body_zones");
    }
}

public static partial class GlobalProcs
{
    /// Calculates the final force of some item based on atk_mods
    /// Needs to have support for force overrides and multipliers of 0 (hence why we ternaries are used over 'or's)
    // tg: code/__DEFINES/combat.dm:444  #define CALCULATE_FORCE(some_item, atk_mods) (M4a P1: a member, rules 11.1)
    public static float CALCULATE_FORCE(obj some_item, DmList atk_mods)
        => (Dm.num(Dm.@in(FORCE_OVERRIDE, atk_mods) ? atk_mods[FORCE_OVERRIDE] : some_item.force) + Dm.num(atk_mods?[FORCE_MODIFIER] is var or__1 && Dm.truthy(or__1) ? or__1 : 0))
            * Dm.num(Dm.@in(FORCE_MULTIPLIER, atk_mods) ? atk_mods[FORCE_MULTIPLIER] : 1);

    /// Calculates the amount of burn force when applying this much energy to a mob via electrocution from an energy source.
    // tg: code/__DEFINES/combat.dm:400  #define ELECTROCUTE_DAMAGE(energy) (a member, rules 11.1: energy is evaluated once)
    public static float ELECTROCUTE_DAMAGE(float energy)
        => (energy >= 1 * KILO * JOULES ? clamp(20 + round(energy / JOULES_PER_DAMAGE), 20, 195) + rand(-5, 5) : 0);
}
