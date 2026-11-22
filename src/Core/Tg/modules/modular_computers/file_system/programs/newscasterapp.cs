// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/newscasterapp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6b (D326): the whole file, the Newscaster app (its NtosNewscaster window): a newscaster in nullspace answers for the
// app. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/newscasterapp.dm:1
public partial class datum_computer_file_program_newscaster : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/newscasterapp.dm:15
    public override async DmTask New(params object[] __extra)
    {
        newscaster_ui = Dm.@new<obj_machinery_newscaster>();
        await base.New(__extra);
    }

    // tg: code/modules/modular_computers/file_system/programs/newscasterapp.dm:19
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref newscaster_ui);
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/newscasterapp.dm:23
    public override async DmTask<object> ui_data(mob user = null) => await newscaster_ui.ui_data(user);

    // tg: code/modules/modular_computers/file_system/programs/newscasterapp.dm:26
    public override async DmTask<object> ui_static_data(mob user = null) => await newscaster_ui.ui_static_data(user);

    // tg: code/modules/modular_computers/file_system/programs/newscasterapp.dm:29
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        return await newscaster_ui.ui_act(action, @params, ui, state);
    }
}
