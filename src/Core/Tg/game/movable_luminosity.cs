// Translated from tgstation (snapshot 1d99bd7f): code/game/movable_luminosity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the flashlight, M4a: the overlay light's dynamic luminosity).

// tg: code/game/movable_luminosity.dm:2
public partial class atom_movable : atom
{
    ///Keeps track of the sources of dynamic luminosity and updates our visibility with the highest.
    // tg: code/game/movable_luminosity.dm:2
    public virtual void update_dynamic_luminosity()
    {
        float highest = 0;
        if (affected_dynamic_lights != null)
        {
            foreach (object i in Dm.each(affected_dynamic_lights))
            {
                if (Dm.num(affected_dynamic_lights[i]) <= highest)
                    continue;
                highest = Dm.num(affected_dynamic_lights[i]);
            }
        }
        if (highest == affecting_dynamic_lumi)
            return;
        luminosity -= affecting_dynamic_lumi;
        affecting_dynamic_lumi = highest;
        luminosity += affecting_dynamic_lumi;
    }

    ///Helper to change several lighting overlay settings.
    // tg: code/game/movable_luminosity.dm:16
    public virtual void set_light_range_power_color(object range = null, object power = null, object color = null)
    {
        set_light_range(range);
        set_light_power(power);
        set_light_color(color);
    }
}
