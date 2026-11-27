// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmodes/gizmood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the mood pulser gizmo mode: a green ring that cheers up every human within fourteen tiles, a red one that saddens
// them, and the radiation pulse with its green ring. The var blocks are generated (D111).

/// Send out a mood pulse
// tg: code/modules/research/gizmo/gizmodes/gizmood.dm:11
public partial class datum_gizpulse_mood_pulser : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizmood.dm:19
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await mood_pulse(holder);
    }

    /// Send a mood pulse to a range
    // tg: code/modules/research/gizmo/gizmodes/gizmood.dm:23
    public virtual async DmTask mood_pulse(atom_movable holder = null)
    {
        Dm.@new<obj_effect_temp_visual_circle_wave>(get_turf(holder), ring_color);
        foreach (mob_living_carbon_human human in Dm.each<mob_living_carbon_human>(urange(range, holder)))
            await human.add_mood_event("gizmo_mood_pulse", mood);
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizmood.dm:38
public partial class datum_gizpulse_radiation_pulse : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizmood.dm:38
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        Dm.@new<obj_effect_temp_visual_circle_wave>(get_turf(holder), COLOR_GREEN);

        radiation_pulse(holder, max_range: 5, threshold: RAD_LIGHT_INSULATION, chance: 30);
    }
}
