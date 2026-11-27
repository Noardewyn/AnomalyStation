// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmodes/gizmisc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the small gizmo modes: the mover (the beyblade's start and stop of its random walk), the lights (an intense red
// glow and the toggle gizmo's on state) and the voice hints (the voice gizmo says its keywords, or speaks another language). The
// var blocks are generated (D111).

// tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:5
public partial class datum_gizpulse_start_moving : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:5
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await holder.AddElement(typeof(datum_element_moving_randomly));
        holder.SEND_SIGNAL(COMSIG_GIZMO_START_MOVING, Dm.list(holder, this));
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:9
public partial class datum_gizpulse_stop_moving : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:9
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        holder.RemoveElement(typeof(datum_element_moving_randomly));
        holder.SEND_SIGNAL(COMSIG_GIZMO_STOP_MOVING, Dm.list(holder, this));
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:21
public partial class datum_gizpulse_lights_on : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:21
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        holder.SEND_SIGNAL(COMSIG_GIZMO_ON_STATE, Dm.list(holder));

        await holder.set_light(
            l_range: 3,
            l_power: 2,
            l_color: LIGHT_COLOR_INTENSE_RED,
            l_on: TRUE
        );
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:31
public partial class datum_gizpulse_lights_off : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:31
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        holder.SEND_SIGNAL(COMSIG_GIZMO_OFF_STATE, Dm.list(holder));
        await holder.set_light_on(FALSE);
    }
}

/// Spit out a hint for using the voice interface
// tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:44
public partial class datum_gizpulse_voice_hint : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:44
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        datum_component_gizmo_voice voice = holder.GetComponent(typeof(datum_component_gizmo_voice)) as datum_component_gizmo_voice;

        await holder.say(datum_component_gizmo_voice.active_words.Join(" "));
    }
}

/// Pick a different language
// tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:50
public partial class datum_gizpulse_language_change : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizmisc.dm:50
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        holder.grant_random_uncommon_language("gizmo");
    }
}
