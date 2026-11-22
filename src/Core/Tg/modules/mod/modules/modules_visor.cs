// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_visor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. The MOD visor modules: while on, a visor gives its wearer its HUD or vision traits (medical,
// diagnostic, security, meson, thermal, night); the night vision module gives night vision beside another visor. The var blocks
// are generated (D111).

///Base Visor - Adds a specific HUD and traits to you.
// tg: code/modules/mod/modules/modules_visor.dm:4
public partial class obj_item_mod_module_visor : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_visor.dm:15
    public override async DmTask on_activation(mob activator = null)
    {
        if (Dm.truthy(length(visor_traits)))
            mod.wearer.add_traits(visor_traits, REF(this));
    }

    // tg: code/modules/mod/modules/modules_visor.dm:19
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        if (Dm.truthy(length(visor_traits)))
            mod.wearer.remove_traits(visor_traits, REF(this));
    }
}

// tg: code/modules/mod/modules/modules_visor.dm:83
public partial class obj_item_mod_module_night : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_visor.dm:96
    public override async DmTask on_activation(mob activator = null)
    {
        ADD_TRAIT(mod.wearer, TRAIT_TRUE_NIGHT_VISION, REF(this));
    }

    // tg: code/modules/mod/modules/modules_visor.dm:99
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        REMOVE_TRAIT(mod.wearer, TRAIT_TRUE_NIGHT_VISION, REF(this));
    }
}
