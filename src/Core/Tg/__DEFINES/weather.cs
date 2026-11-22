// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/weather.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps E2a): only the globals (lines 2-8): the storms' sound playlists (an impacted area to the looping sound its storm plays
// there) and the wizard's rain reagents; the constants of the file are generated into Defines.

public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/weather.dm:2  GLOBAL_LIST_EMPTY(ash_storm_sounds)
    public DmList ash_storm_sounds;
    public virtual void InitGlobalash_storm_sounds()
    {
        ash_storm_sounds = Dm.list();
        Dm.append(ref gvars_datum_init_order, "ash_storm_sounds");
    }

    // tg: code/__DEFINES/weather.dm:3  GLOBAL_LIST_EMPTY(rain_storm_sounds)
    public DmList rain_storm_sounds;
    public virtual void InitGlobalrain_storm_sounds()
    {
        rain_storm_sounds = Dm.list();
        Dm.append(ref gvars_datum_init_order, "rain_storm_sounds");
    }

    // tg: code/__DEFINES/weather.dm:4  GLOBAL_LIST_EMPTY(sand_storm_sounds)
    public DmList sand_storm_sounds;
    public virtual void InitGlobalsand_storm_sounds()
    {
        sand_storm_sounds = Dm.list();
        Dm.append(ref gvars_datum_init_order, "sand_storm_sounds");
    }

    // tg: code/__DEFINES/weather.dm:6  GLOBAL_LIST_EMPTY(snowstorm_sounds)
    public DmList snowstorm_sounds;
    public virtual void InitGlobalsnowstorm_sounds()
    {
        snowstorm_sounds = Dm.list();
        Dm.append(ref gvars_datum_init_order, "snowstorm_sounds");
    }

    // tg: code/__DEFINES/weather.dm:8  GLOBAL_LIST_EMPTY(wizard_rain_reagents)
    public DmList wizard_rain_reagents;
    public virtual void InitGlobalwizard_rain_reagents()
    {
        wizard_rain_reagents = Dm.list();
        Dm.append(ref gvars_datum_init_order, "wizard_rain_reagents");
    }
}
