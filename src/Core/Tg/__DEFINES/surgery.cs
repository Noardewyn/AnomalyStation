// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/surgery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only IS_ORGANIC_ORGAN, IS_ROBOTIC_ORGAN, IS_ORGANIC_LIMB, IS_ROBOTIC_LIMB (M5 slice 2) IS_STUMP and IS_LYING_OR_CANNOT_LIE (M8y slice A: the operating table's patient), GET_FAILURE_CHANCE, FORMAT_LIMB_OWNER and FORMAT_ORGAN_OWNER (gaps P1: the surgery operations), IS_MINERAL_ORGAN (gaps VI1) translated (M2 P2 body: the bodyparts'
// Initialize, the carbon's add_bodypart and remove_bodypart, can_heartattack); the constants are generated into Defines,
// the other macros come with their callers.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/surgery.dm:2  #define IS_ORGANIC_ORGAN(organ) (organ.organ_flags & ORGAN_ORGANIC)
    public static object IS_ORGANIC_ORGAN(object organ) => Dm.bitand(((obj_item_organ)organ).organ_flags, ORGAN_ORGANIC);
    // tg: code/__DEFINES/surgery.dm:4  #define IS_ROBOTIC_ORGAN(organ) (organ.organ_flags & ORGAN_ROBOTIC)
    public static object IS_ROBOTIC_ORGAN(object organ) => Dm.bitand(((obj_item_organ)organ).organ_flags, ORGAN_ROBOTIC);

    // tg: code/__DEFINES/surgery.dm:6  #define IS_MINERAL_ORGAN(organ) (organ.organ_flags & ORGAN_MINERAL)  (gaps VI1: the diseases' organ checks)
    public static object IS_MINERAL_ORGAN(object organ) => Dm.bitand(((obj_item_organ)organ).organ_flags, ORGAN_MINERAL);

    /// Helper to figure out if a limb is organic
    // tg: code/__DEFINES/surgery.dm:60  #define IS_ORGANIC_LIMB(limb) (limb.bodytype & BODYTYPE_ORGANIC)
    public static float IS_ORGANIC_LIMB(object limb) => Dm.bitand(((obj_item_bodypart)limb).bodytype, BODYTYPE_ORGANIC);

    /// Helper to figure out if a limb is robotic
    // tg: code/__DEFINES/surgery.dm:62  #define IS_ROBOTIC_LIMB(limb) (limb.bodytype & BODYTYPE_ROBOTIC) (M5 slice 2: the cable coil's repair)
    public static float IS_ROBOTIC_LIMB(object limb) => Dm.bitand(((obj_item_bodypart)limb).bodytype, BODYTYPE_ROBOTIC);

    /// Is the bodypart a stump
    // tg: code/__DEFINES/surgery.dm:67  #define IS_STUMP(limb) (limb.bodypart_flags & BODYPART_STUMP)
    public static float IS_STUMP(object limb) => Dm.bitand(((obj_item_bodypart)limb).bodypart_flags, BODYPART_STUMP);

    /// Checks if the mob is lying down if they can lie down, otherwise always passes
    // tg: code/__DEFINES/surgery.dm:114
    public static bool IS_LYING_OR_CANNOT_LIE(mob_living mob) => Dm.truthy(Dm.bitand(mob.mobility_flags, MOBILITY_LIEDOWN)) ? Dm.eq(mob.body_position, LYING_DOWN) : true;

    /// Calculates failure chance of an operation based on the base time and the effective speed modifier
    // tg: code/__DEFINES/surgery.dm:177  #define GET_FAILURE_CHANCE(base_time, speed_mod) (FAILURE_CHANCE_PER_SECOND * (((speed_mod * (base_time)) - (SURGERY_MODIFIER_FAILURE_THRESHOLD * (base_time))) / (1 SECONDS)))
    public static float GET_FAILURE_CHANCE(float base_time, float speed_mod) => FAILURE_CHANCE_PER_SECOND * (((speed_mod * base_time) - (SURGERY_MODIFIER_FAILURE_THRESHOLD * base_time)) / (1 * SECONDS));

    /// Used in string formatting to print a limb as "John's right arm" or "the human right arm"
    // tg: code/__DEFINES/surgery.dm:197  #define FORMAT_LIMB_OWNER(limb) (limb.owner ? "[limb.owner]'s [limb.plaintext_zone]" : limb)
    public static object FORMAT_LIMB_OWNER(object limb) => Dm.truthy(((obj_item_bodypart)limb).owner) ? I18n.T("[]'s []", ((obj_item_bodypart)limb).owner, ((obj_item_bodypart)limb).plaintext_zone) : limb;

    /// Used in string formatting to print an organ's location as "John" or "the human chest"
    // tg: code/__DEFINES/surgery.dm:199  #define FORMAT_ORGAN_OWNER(organ) (organ.owner || organ.loc)
    public static object FORMAT_ORGAN_OWNER(object organ) => ((obj_item_organ)organ).owner is var or__1 && Dm.truthy(or__1) ? or__1 : ((obj_item_organ)organ).loc;
}
