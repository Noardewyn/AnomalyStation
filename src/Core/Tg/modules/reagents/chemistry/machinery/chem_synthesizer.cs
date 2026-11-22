// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/machinery/chem_synthesizer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the admins' debug chemical synthesizer and its ChemDebugSynthesizer window. The var block is generated.

// tg: code/modules/reagents/chemistry/machinery/chem_synthesizer.dm:1
public partial class obj_machinery_chem_dispenser_chem_synthesizer : obj_machinery_chem_dispenser
{
    // tg: code/modules/reagents/chemistry/machinery/chem_synthesizer.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_SCREWDRIVER, TOOL_ACT_PRIMARY));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_CROWBAR, TOOL_ACT_PRIMARY));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_synthesizer.dm:21
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref beaker);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_synthesizer.dm:25
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemDebugSynthesizer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_synthesizer.dm:32
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        dot["purity"] = purity;
        dot["temp"] = temperature;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_synthesizer.dm:37
    public override async DmTask<object> handle_ui_act(object action = null, object @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        DmList p = @params as DmList;
        switch (action)
        {
            case "input":
            {
                if (QDELETED(beaker))
                    return FALSE;

                object selected_reagent = await tgui_input_list(ui.user, "Select reagent", "Reagent", GLOB.name2reagent);
                if (!Dm.truthy(selected_reagent))
                    return FALSE;

                object input_reagent = GLOB.name2reagent[selected_reagent];
                if (!Dm.truthy(input_reagent))
                    return FALSE;

                beaker.add_hiddenprint(ui.user);
                await (beaker.reagents as datum_reagents).add_reagent(input_reagent, amount, reagtemp: temperature, added_purity: Dm.num(purity) / 100);
                return TRUE;
            }

            case "makecup":
                if (beaker != null)
                    return null;
                beaker = Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(this);
                visible_message(span_notice(I18n.T("[] dispenses a bluespace beaker.", this)));
                return TRUE;

            case "amount":
            {
                object input = p["amount"];
                if (input == null)
                    return FALSE;

                float? number = text2num(input);
                if (number == null)
                    return FALSE;

                amount = number;
                return TRUE;
            }

            case "temp":
            {
                object input = p["amount"];
                if (input == null)
                    return FALSE;

                float? number = text2num(input);
                if (number == null)
                    return FALSE;

                temperature = number.Value;
                return TRUE;
            }

            case "purity":
            {
                object input = p["amount"];
                if (input == null)
                    return FALSE;

                float? number = text2num(input);
                if (number == null)
                    return FALSE;

                purity = number.Value;
                return TRUE;
            }
        }

        update_appearance();
        return null;
    }
}
