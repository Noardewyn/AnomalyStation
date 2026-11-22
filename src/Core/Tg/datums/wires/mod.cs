// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/mod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. A MOD control unit's wires, reached with its panel open: the hack wire toggles its lock (cut:
// it loses its access), the disable wire makes it malfunction, the shock wire electrifies it, the interface wire breaks its
// interface; a non-silicon working an electrified suit's wires may be shocked first; roboticists know them. The var block is
// generated (D111).

// tg: code/datums/wires/mod.dm:1
public partial class datum_wires_mod : datum_wires
{
    // tg: code/datums/wires/mod.dm:5
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(WIRE_HACK, WIRE_DISABLE, WIRE_SHOCK, WIRE_INTERFACE);
        add_duds(2);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/mod.dm:10
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        obj_item_mod_control mod = holder as obj_item_mod_control;
        return Dm.truthy(mod.open);
    }

    // tg: code/datums/wires/mod.dm:16
    public override DmList get_status()
    {
        obj_item_mod_control mod = holder as obj_item_mod_control;
        DmList status = Dm.list();
        Dm.append(ref status, Dm.truthy(mod.seconds_electrified) ? I18n.T("The orange light is on.") : I18n.T("The orange light is off."));
        Dm.append(ref status, Dm.truthy(mod.malfunctioning) ? I18n.T("The red light is off.") : I18n.T("The red light is blinking."));
        Dm.append(ref status, Dm.truthy(mod.locked) ? I18n.T("The green light is on.") : I18n.T("The green light is off."));
        Dm.append(ref status, Dm.truthy(mod.interface_break) ? I18n.T("The yellow light is off.") : I18n.T("The yellow light is on."));
        return status;
    }

    // tg: code/datums/wires/mod.dm:25
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_item_mod_control mod = holder as obj_item_mod_control;
        if (Dm.eq(wire, WIRE_HACK))
            mod.locked = Dm.num(!Dm.truthy(mod.locked));
        else if (Dm.eq(wire, WIRE_DISABLE))
            mod.malfunctioning = TRUE;
        else if (Dm.eq(wire, WIRE_SHOCK))
            mod.seconds_electrified = MACHINE_DEFAULT_ELECTRIFY_TIME;
        else if (Dm.eq(wire, WIRE_INTERFACE))
            mod.interface_break = Dm.num(!Dm.truthy(mod.interface_break));
    }

    // tg: code/datums/wires/mod.dm:37
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        obj_item_mod_control mod = holder as obj_item_mod_control;
        if (Dm.eq(wire, WIRE_HACK))
        {
            if (!Dm.truthy(mend))
                mod.req_access = Dm.list();
        }
        else if (Dm.eq(wire, WIRE_DISABLE))
            mod.malfunctioning = Dm.num(!Dm.truthy(mend));
        else if (Dm.eq(wire, WIRE_SHOCK))
        {
            if (Dm.truthy(mend))
                mod.seconds_electrified = MACHINE_NOT_ELECTRIFIED;
            else
                mod.seconds_electrified = MACHINE_ELECTRIFIED_PERMANENT;
        }
        else if (Dm.eq(wire, WIRE_INTERFACE))
            mod.interface_break = Dm.num(!Dm.truthy(mend));
    }

    // tg: code/datums/wires/mod.dm:53
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        obj_item_mod_control mod = holder as obj_item_mod_control;
        mob user = ui.user;
        if (!HAS_SILICON_ACCESS(user) && Dm.truthy(mod.seconds_electrified) && Dm.truthy(await mod.shock__proc(user as mob_living, 100)))
            return FALSE;
        return await base.ui_act(action, @params, ui, state);
    }

    // tg: code/datums/wires/mod.dm:60
    public override bool can_reveal_wires(mob user)
    {
        if (HAS_TRAIT(user, TRAIT_KNOW_ROBO_WIRES))
            return true;
        return base.can_reveal_wires(user);
    }
}
