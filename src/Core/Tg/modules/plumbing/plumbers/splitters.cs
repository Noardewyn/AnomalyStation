// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/splitters.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111).

///it splits the reagents however you want. So you can "every 60 units, 45 goes left and 15 goes straight". The side direction is EAST, you can change this in the component
// tg: code/modules/plumbing/plumbers/splitters.dm:4
public partial class obj_machinery_plumbing_splitter : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/splitters.dm:1  #define MAX_TRANSFER 9 (file-local, rules 11.4)
    private const float MAX_TRANSFER = 9;

    // tg: code/modules/plumbing/plumbers/splitters.dm:18
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_multidirectional_splitter), layer));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/splitters.dm:22
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemSplitter", name);
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/plumbing/plumbers/splitters.dm:29
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("max_transfer", MAX_TRANSFER));
    }

    // tg: code/modules/plumbing/plumbers/splitters.dm:34
    public override async DmTask<object> ui_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("straight", transfer_straight),
            Dm.kv("left", transfer_left),
            Dm.kv("right", transfer_right));
    }

    // tg: code/modules/plumbing/plumbers/splitters.dm:41
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "set_amount":
            {
                float? value = text2num(@params["amount"]);
                if (!Dm.truthy(value))
                    return FALSE;
                value = clamp(value.Value, 1, MAX_TRANSFER);

                switch (@params["target"])
                {
                    case "straight":
                        transfer_straight = value.Value;
                        return TRUE;

                    case "left":
                        transfer_left = value.Value;
                        return TRUE;

                    case "right":
                        transfer_right = value.Value;
                        return TRUE;
                }
                break;
            }
        }
        return dot;
    }
}
