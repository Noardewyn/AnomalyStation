// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/construction/material.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2: the function-like macros of the file (rules 11.1); its constants are generated into Defines. Gaps RD1 (D333): the
// material flags' display names (GLOB.material_flags_to_string), which the lathes' material requirements read.

public partial class datum_controller_global_vars
{
    /// Assoc list of material flags used in designs to their display names
    // tg: code/__DEFINES/construction/material.dm:53  GLOBAL_LIST_INIT(material_flags_to_string, alist(...))
    public DmList material_flags_to_string;

    // tg: code/__DEFINES/construction/material.dm:53  (GLOBAL_MANAGED(material_flags_to_string, alist(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmaterial_flags_to_string()
    {
        material_flags_to_string = Dm.alist(
            Dm.kv(MATERIAL_CLASS_RIGID, "rigid"),
            Dm.kv(MATERIAL_CLASS_AMORPHOUS, "amorphous"),
            Dm.kv(MATERIAL_CLASS_METAL, "metallic"),
            Dm.kv(MATERIAL_CLASS_FABRIC, "fabric"),
            Dm.kv(MATERIAL_CLASS_CRYSTAL, "crystalline"),
            Dm.kv(MATERIAL_CLASS_ORGANIC, "organic"),
            Dm.kv(MATERIAL_CLASS_POLYMER, "polymer"));
        Dm.append(ref gvars_datum_init_order, "material_flags_to_string");
    }
}

public static partial class GlobalProcs
{
    /// Allows to easily add "deadzones" for properties, and only adjust stats if they go below/above said deadzones. Basically two starting points for modifiers.
    // tg: code/__DEFINES/construction/material.dm:96  #define MATERIAL_PROPERTY_DIVERGENCE(property, minimum, maximum)
    public static float MATERIAL_PROPERTY_DIVERGENCE(object property, float minimum, float maximum)
        => (min(0, Dm.num(property) - minimum) + max(0, Dm.num(property) - maximum));

    /// A macro that ensures some multiplicative modifiers higher than 1 don't become lower than 1 and vice-versa because of the multiplier.
    // tg: code/__DEFINES/construction/material.dm:190  #define GET_MATERIAL_MODIFIER(modifier, multiplier)
    public static float GET_MATERIAL_MODIFIER(object modifier, object multiplier)
        => (Dm.num(modifier) >= 1 ? 1 + ((Dm.num(modifier)) - 1) * (Dm.num(multiplier)) : Dm.pow(Dm.num(modifier), Dm.num(multiplier)));
}
