// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/filter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111).

///chemical plumbing filter. If it's not filtered by left and right, it goes straight.
// tg: code/modules/plumbing/plumbers/filter.dm:2
public partial class obj_machinery_plumbing_filter : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/filter.dm:14
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_multidirectional_filter), layer));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/filter.dm:18
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemFilter", name);
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/plumbing/plumbers/filter.dm:25
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;

        dot["left"] = Dm.list();
        foreach (Type id in Dm.each_cast<Type>(left))
            dot["left"] = Dm.add(dot["left"], Dm.initial(id, (datum_reagent r) => r.name));

        dot["right"] = Dm.list();
        foreach (Type id in Dm.each_cast<Type>(right))
            dot["right"] = Dm.add(dot["right"], Dm.initial(id, (datum_reagent r) => r.name));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/filter.dm:36
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "add":
            {
                object which = @params["which"];

                object selected_reagent = await tgui_input_list(usr, Dm.text("Select [] reagent", which), "Reagent", GLOB.name2reagent);
                if (!Dm.truthy(selected_reagent))
                    return dot;
                if (QDELETED(ui) || ui.status != UI_INTERACTIVE)
                    return dot;

                object chem_id = GLOB.name2reagent[selected_reagent];
                if (!Dm.truthy(chem_id))
                    return FALSE;

                switch (which)
                {
                    case "left":
                        Dm.combine(ref left, chem_id);
                        return TRUE;

                    case "right":
                        Dm.combine(ref right, chem_id);
                        return TRUE;
                }
                break;
            }

            case "remove":
            {
                object chem_id = GLOB.name2reagent[@params["reagent"]];
                if (!Dm.truthy(chem_id))
                    return FALSE;

                switch (@params["which"])
                {
                    case "left":
                        Dm.remove(ref left, chem_id);
                        return TRUE;

                    case "right":
                        Dm.remove(ref right, chem_id);
                        return TRUE;
                }
                break;
            }
        }
        return dot;
    }
}
