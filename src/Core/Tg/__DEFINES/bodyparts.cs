// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/bodyparts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the surgery state and biostate helpers that the bodyparts use (M2 P2 body: HAS_SURGERY_STATE,
// HAS_ANY_SURGERY_STATE, LIMB_HAS_SURGERY_STATE, LIMB_HAS_ANY_SURGERY_STATE (M6 slice 2: the surgery self-check), ALL_SURGERY_FISH_STATES, LIMB_HAS_SKIN, LIMB_HAS_BONES, LIMB_HAS_VESSELS; gaps P1: SURGERY_STATE_READABLE, SURGERY_STATE_GUIDES)
// translated; the constants are generated into Defines, the other macros come with their callers.

public static partial class GlobalProcs
{
    // Yes these are glorified bitflag manipulation macros, they're meant to make reading surgical operations a bit easier
    /// Checks if the input surgery state has all of the bitflags passed
    // tg: code/__DEFINES/bodyparts.dm:139  #define HAS_SURGERY_STATE(input_state, check_state) ((input_state & (check_state)) == (check_state))
    public static bool HAS_SURGERY_STATE(float input_state, float check_state) => Dm.bitand(input_state, check_state) == check_state;

    /// Checks if the input surgery state has any of the bitflags passed
    // tg: code/__DEFINES/bodyparts.dm:141  #define HAS_ANY_SURGERY_STATE(input_state, check_state) ((input_state & (check_state)))
    public static float HAS_ANY_SURGERY_STATE(float input_state, float check_state) => Dm.bitand(input_state, check_state);

    /// Checks if the limb has all of the bitflags passed
    // tg: code/__DEFINES/bodyparts.dm:143  #define LIMB_HAS_SURGERY_STATE(limb, check_state) HAS_SURGERY_STATE(limb?.surgery_state, check_state)
    public static bool LIMB_HAS_SURGERY_STATE(object limb, float check_state) => HAS_SURGERY_STATE(Dm.num(((obj_item_bodypart)limb)?.surgery_state), check_state);

    /// Checks if the limb has any of the bitflags passed
    // tg: code/__DEFINES/bodyparts.dm:145  #define LIMB_HAS_ANY_SURGERY_STATE(limb, check_state) HAS_ANY_SURGERY_STATE(limb?.surgery_state, check_state)
    public static float LIMB_HAS_ANY_SURGERY_STATE(object limb, float check_state) => HAS_ANY_SURGERY_STATE(Dm.num(((obj_item_bodypart)limb)?.surgery_state), check_state);

    /// Surgery state required for a limb with a certain zone to... be... fished... in...
    // tg: code/__DEFINES/bodyparts.dm:159  #define ALL_SURGERY_FISH_STATES(for_zone) (SURGERY_SKIN_OPEN|SURGERY_ORGANS_CUT|(for_zone == BODY_ZONE_CHEST ? SURGERY_BONE_SAWED : NONE))
    public static float ALL_SURGERY_FISH_STATES(object for_zone) => Dm.bitor(Dm.bitor(SURGERY_SKIN_OPEN, SURGERY_ORGANS_CUT), (Dm.eq(for_zone, BODY_ZONE_CHEST) ? SURGERY_BONE_SAWED : NONE));

    /// Checks if a bodypart lacks both flesh and metal, meaning it has no skin to cut.
    // tg: code/__DEFINES/bodyparts.dm:207  #define LIMB_HAS_SKIN(limb) (limb?.biological_state & BIOSTATE_HAS_SKIN)
    public static float LIMB_HAS_SKIN(object limb) => Dm.bitand(Dm.num(((obj_item_bodypart)limb)?.biological_state), BIOSTATE_HAS_SKIN);

    /// Checks if a bodypart lacks both bone and metal, meaning it has no bones to saw.
    // tg: code/__DEFINES/bodyparts.dm:211  #define LIMB_HAS_BONES(limb) (limb?.biological_state & BIOSTATE_HAS_BONES)
    public static float LIMB_HAS_BONES(object limb) => Dm.bitand(Dm.num(((obj_item_bodypart)limb)?.biological_state), BIOSTATE_HAS_BONES);

    /// Checks if a bodypart lacks both blood and wires, meaning it has no vessels to manipulate.
    // tg: code/__DEFINES/bodyparts.dm:215  #define LIMB_HAS_VESSELS(limb) (limb?.biological_state & BIOSTATE_HAS_VESSELS)
    public static float LIMB_HAS_VESSELS(object limb) => Dm.bitand(Dm.num(((obj_item_bodypart)limb)?.biological_state), BIOSTATE_HAS_VESSELS);

    /// For use in translating bitfield to human readable strings. Keep in the correct order!
    // tg: code/__DEFINES/bodyparts.dm:110  #define SURGERY_STATE_READABLE list(...)
    public static DmList SURGERY_STATE_READABLE() => Dm.list(
        Dm.kv(I18n.T("Skin is cut"), SURGERY_SKIN_CUT),
        Dm.kv(I18n.T("Skin is open"), SURGERY_SKIN_OPEN),
        Dm.kv(I18n.T("Blood vessels are unclamped"), SURGERY_VESSELS_UNCLAMPED),
        Dm.kv(I18n.T("Blood vessels are clamped"), SURGERY_VESSELS_CLAMPED),
        Dm.kv(I18n.T("Organs are cut"), SURGERY_ORGANS_CUT),
        Dm.kv(I18n.T("Bone is drilled"), SURGERY_BONE_DRILLED),
        Dm.kv(I18n.T("Bone is sawed"), SURGERY_BONE_SAWED),
        Dm.kv(I18n.T("Plastic is applied"), SURGERY_PLASTIC_APPLIED),
        Dm.kv(I18n.T("Prosthetic is unsecured"), SURGERY_PROSTHETIC_UNSECURED),
        Dm.kv(I18n.T("Cavity is opened wide"), SURGERY_CAVITY_WIDENED));

    /// For use in translating bitfield to steps required for surgery. Keep in the correct order!
    // tg: code/__DEFINES/bodyparts.dm:124  #define SURGERY_STATE_GUIDES(must_must_not) list(...)
    public static DmList SURGERY_STATE_GUIDES(object must_must_not) => Dm.list(
        Dm.kv(I18n.T("the skin [] be cut", must_must_not), SURGERY_SKIN_CUT),
        Dm.kv(I18n.T("the skin [] be open", must_must_not), SURGERY_SKIN_OPEN),
        Dm.kv(I18n.T("the blood vessels [] be unclamped", must_must_not), SURGERY_VESSELS_UNCLAMPED),
        Dm.kv(I18n.T("the blood vessels [] be clamped", must_must_not), SURGERY_VESSELS_CLAMPED),
        Dm.kv(I18n.T("the organs [] be cut", must_must_not), SURGERY_ORGANS_CUT),
        Dm.kv(I18n.T("the bone [] be drilled", must_must_not), SURGERY_BONE_DRILLED),
        Dm.kv(I18n.T("the bone [] be sawed", must_must_not), SURGERY_BONE_SAWED),
        Dm.kv(I18n.T("plastic [] be applied", must_must_not), SURGERY_PLASTIC_APPLIED),
        Dm.kv(I18n.T("the prosthetic [] be unsecured", must_must_not), SURGERY_PROSTHETIC_UNSECURED),
        Dm.kv(I18n.T("the chest cavity [] be opened wide", must_must_not), SURGERY_CAVITY_WIDENED));
}
