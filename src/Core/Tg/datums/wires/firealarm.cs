// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/firealarm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class datum_wires_firealarm : datum_wires
{
    public override bool always_reveal_wire(object color) => Dm.eq(get_color_of_wire(WIRE_FIRE_DETECT), color);

    public override DmList get_status() => Dm.list(Dm.truthy(((obj_machinery_firealarm)holder).my_area?.fire_detect)
        ? "The thermal sensor light is on." : "The thermal sensor light is off.");

    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        wires = Dm.list(WIRE_FIRE_DETECT, WIRE_FIRE_RESET, WIRE_FIRE_TRIGGER);
        add_duds(1);
        await base.New(__extra);
    }

    public override bool interactable(mob user) => base.interactable(user)
        && holder is obj_machinery_firealarm alarm && Dm.truthy(alarm.panel_open) && Dm.eq(alarm.buildstage, FIRE_ALARM_BUILD_SECURED);

    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        var alarm = (obj_machinery_firealarm)holder;
        if (Dm.eq(wire, WIRE_FIRE_DETECT)) alarm.toggle_fire_detect(user, TRUE);
        else if (Dm.eq(wire, WIRE_FIRE_TRIGGER)) alarm.alarm(user, TRUE);
        else if (Dm.eq(wire, WIRE_FIRE_RESET)) alarm.reset(user, TRUE);
    }

    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        var alarm = (obj_machinery_firealarm)holder;
        bool mending = Dm.truthy(mend);
        if (Dm.eq(wire, WIRE_FIRE_DETECT))
        {
            alarm.can_toggle_detection = Dm.num(!mending);
            int count = 0;
            foreach (obj_machinery_firealarm other in Dm.each<obj_machinery_firealarm>(alarm.my_area?.firealarms))
                if (Dm.truthy(other.wires?.is_cut(WIRE_FIRE_DETECT))) count++;
            if (mending) alarm.enable_fire_detect(source);
            else if (length(alarm.my_area?.firealarms) == count) alarm.disable_fire_detect(source);
        }
        else if (Dm.eq(wire, WIRE_FIRE_TRIGGER)) alarm.can_trigger = Dm.num(!mending);
        else if (Dm.eq(wire, WIRE_FIRE_RESET)) alarm.can_reset = Dm.num(!mending);
    }
}
