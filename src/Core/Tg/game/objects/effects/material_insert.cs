// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/material_insert.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD1, D333: the fabricators): the material insertion animation the autolathe and the R&D fabricators
// flick over themselves when a material goes in.

public static partial class GlobalProcs
{
    /**
     * Creates a mutable appearance with the material color applied for its insertion animation into an autolathe or techfab
     * Arguments
     *
     * * material - the material used to generate the overlay
     */
    // tg: code/game/objects/effects/material_insert.dm:7
    public static object material_insertion_animation(datum_material material = null)
    {
        ref DmList apps = ref TgState.Current.GlobalProcs__material_insertion_animation__apps;

        mutable_appearance cached_app = apps[material] as mutable_appearance;
        if (cached_app == null)
        {
            cached_app = mutable_appearance(Dm.resource("icons/obj/machines/research.dmi"), "material_insertion") as mutable_appearance;
            cached_app.color = material.color;
            cached_app.alpha = Dm.num(material.alpha);

            apps[material] = cached_app;
        }
        return cached_app;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/material_insert.dm:10  material_insertion_animation's var/static/list/mutable_appearance/apps = list() (rules 10.6)
    internal DmList GlobalProcs__material_insertion_animation__apps = Dm.list();
}
