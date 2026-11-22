// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/contract.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298). The var block is generated.

/* For employment contracts */

// tg: code/modules/paperwork/contract.dm:3
public partial class obj_item_paper_employment_contract : obj_item_paper
{
    // tg: code/modules/paperwork/contract.dm:10
    [DmParams("mapload", "new_employee_name")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_employee_name = Dm.arg(__extra, 0);
        if (!Dm.truthy(new_employee_name))
            return INITIALIZE_HINT_QDEL;
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        object dot = base.Initialize(mapload, __extra);
        employee_name = Dm.str(new_employee_name);
        name = I18n.T("paper- [] employment contract", employee_name);
        add_raw_text(Dm.text("<center>Conditions of Employment</center><BR><BR><BR><BR>This Agreement is made and entered into as of the date of last signature below, by and between [] (hereafter referred to as SLAVE), and Nanotrasen (hereafter referred to as the omnipresent and helpful watcher of humanity).<BR>WITNESSETH:<BR>WHEREAS, SLAVE is a natural born human or humanoid, possessing skills upon which he can aid the omnipresent and helpful watcher of humanity, who seeks employment in the omnipresent and helpful watcher of humanity.<BR>WHEREAS, the omnipresent and helpful watcher of humanity agrees to sporadically provide payment to SLAVE, in exchange for permanent servitude.<BR>NOW THEREFORE in consideration of the mutual covenants herein contained, and other good and valuable consideration, the parties hereto mutually agree as follows:<BR>In exchange for paltry payments, SLAVE agrees to work for the omnipresent and helpful watcher of humanity, for the remainder of his or her current and future lives.<BR>Further, SLAVE agrees to transfer ownership of his or her soul to the loyalty department of the omnipresent and helpful watcher of humanity.<BR>Should transfership of a soul not be possible, a lien shall be placed instead.<BR>Signed,<BR><i>[]</i>", employee_name, employee_name));
        return dot;
    }
}
