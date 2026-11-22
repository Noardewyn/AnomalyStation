// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/pumps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111). The geysers (lavaland) are not ported, so the
// pump finds none on the station and beeps.

///We pump liquids from activated(plungerated) geysers to a plumbing outlet. We need to be wired.
// tg: code/modules/plumbing/plumbers/pumps.dm:2
public partial class obj_machinery_plumbing_liquid_pump : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/pumps.dm:21
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_supply), layer));
        return dot;
    }

    ///please note that the component has a hook in the parent call, wich handles activating and deactivating
    // tg: code/modules/plumbing/plumbers/pumps.dm:26
    [DmParams("user", "I", "time")]
    public override async DmTask<object> default_unfasten_wrench(mob user = null, obj_item I = null, float? time = null)
    {
        time ??= 20;
        object dot = await base.default_unfasten_wrench(user, I, time);
        if (Dm.eq(dot, SUCCESSFUL_UNFASTEN))
        {
            geyser = null;
            update_appearance();
            geyserless = FALSE; //we switched state, so lets just set this back aswell
        }
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/pumps.dm:33
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(anchored) || Dm.truthy(panel_open) || Dm.truthy(geyserless))
            return null;

        if (geyser == null)
        {
            foreach (obj_structure_geyser G in Dm.each<obj_structure_geyser>(loc.contents))
            {
                geyser = G;
                update_appearance();
            }
            if (geyser == null) //we didnt find one, abort
            {
                geyserless = TRUE;
                visible_message(span_warning(I18n.T("\\The [] makes a sad beep!", this)));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50);
                return null;
            }
        }

        Waiting.NoSleep(pump(seconds_per_tick));
        return null;
    }

    ///pump up that sweet geyser nectar
    // tg: code/modules/plumbing/plumbers/pumps.dm:50
    public virtual async DmTask pump(object seconds_per_tick = null)
    {
        if (geyser == null || !Dm.truthy(geyser.reagents))
            return;
        await ((datum_reagents)geyser.reagents).trans_to(this, pump_power * Dm.num(seconds_per_tick));
    }

    // tg: code/modules/plumbing/plumbers/pumps.dm:55
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (geyser != null)
        {
            icon_state = Dm.text("[]-on", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(panel_open) ? "-open" : null);
        return await base.update_icon_state(__extra);
    }
}
