// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/traits/macros.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// This file contains all of the "dynamic" traits and trait sources that can be used
// in a number of versatile and everchanging ways.
// If it uses psuedo-variables like the examples below, it's a macro-trait.

public static partial class GlobalProcs
{
    /// From [/datum/element/elevation] for purpose of registering/removing signals and detaching the elevation_core when the trait is absent.
    // tg: code/__DEFINES/traits/macros.dm:7
    public static string TRAIT_TURF_HAS_ELEVATED_OBJ(object z) => Dm.text("turf_has_elevated_obj_[]", z);

    /// The item is magically cursed
    // tg: code/__DEFINES/traits/macros.dm:10
    public static string CURSED_ITEM_TRAIT(object item_type) => Dm.text("cursed_item_[]", item_type);
    /// A trait given by a specific status effect (not sure why we need both but whatever!)
    // tg: code/__DEFINES/traits/macros.dm:12
    public static string TRAIT_STATUS_EFFECT(object effect_id) => Dm.text("[]-trait", effect_id);
    /// Trait given by mech equipment
    // tg: code/__DEFINES/traits/macros.dm:14
    public static string TRAIT_MECH_EQUIPMENT(object equipment_type) => Dm.text("mech_equipment_[]", equipment_type);
    /// Trait applied by element
    // tg: code/__DEFINES/traits/macros.dm:16
    public static string ELEMENT_TRAIT(object source) => Dm.text("element_trait_[]", source);
}
