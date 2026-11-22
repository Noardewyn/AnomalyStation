// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/ai_slipper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the AI's foam dispenser (Meta's upload and core). The var block is generated (D111).

// tg: code/game/machinery/ai_slipper.dm:1
public partial class obj_machinery_ai_slipper : obj_machinery
{
    // tg: code/game/machinery/ai_slipper.dm:25
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("It has <b>[]</b> uses of foam remaining.", uses)));
        return dot;
    }

    // tg: code/game/machinery/ai_slipper.dm:29
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return await base.update_icon_state(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) || !(foam_cooldown <= world.time) /* COOLDOWN_FINISHED */ || !Dm.truthy(uses))
        {
            icon_state = Dm.text("[]0", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[]1", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/ai_slipper.dm:38
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(await allowed__proc(user)))
        {
            to_chat(user, span_danger(I18n.T("Access denied.")));
            return null;
        }
        if (!Dm.truthy(uses))
        {
            to_chat(user, span_warning(I18n.T("[] is out of foam and cannot be activated!", this)));
            return null;
        }
        if (!(foam_cooldown <= world.time)) // COOLDOWN_FINISHED
        {
            to_chat(user, span_warning(I18n.T("[] cannot be activated for <b>[]</b>!", this, DisplayTimeText(foam_cooldown - world.time)))); // COOLDOWN_TIMELEFT
            return null;
        }
        datum_effect_system_fluid_spread_foam foam = Dm.@new<datum_effect_system_fluid_spread_foam>(loc, 4, null, this); // new(loc, 4, holder = src)
        await foam.start();
        uses--;
        to_chat(user, span_notice(I18n.T("You activate []. It now has <b>[]</b> uses of foam remaining.", this, uses)));
        foam_cooldown = world.time + Dm.num(cooldown_time); // COOLDOWN_START
        power_change();
        _addtimer(Dm.@new<datum_callback>(this, nameof(power_change)), Dm.num(cooldown_time), file: "code/game/machinery/ai_slipper.dm", line: 54);
        return null;
    }
}
