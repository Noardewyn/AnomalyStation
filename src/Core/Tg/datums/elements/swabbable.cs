// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/swabbable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the cell lines a swab or a biopsy tool collects). The var block is generated (D111).

/*!

This element is used in vat growing to allow for the object to be

*/
// tg: code/datums/elements/swabbable.dm:5
public partial class datum_element_swabable : datum_element
{
    ///Listens for the swab signal and then generate a sample based on pre-determined lists that are saved as GLOBs. this allows us to have very few swabbable element instances.
    // tg: code/datums/elements/swabbable.dm:19
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object cell_line_define = Dm.arg(__extra, 0);
        object virus_define = Dm.arg(__extra, 1);
        object cell_line_amount = Dm.arg(__extra, 2) ?? 1;
        object virus_chance = Dm.arg(__extra, 3) ?? 10;
        object dot = await base.Attach(target, __extra);
        if (!isatom(target) || isarea(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_SWAB_FOR_SAMPLES, nameof(GetSwabbed));

        this.cell_line_define = cell_line_define;
        this.virus_define = virus_define;
        this.cell_line_amount = cell_line_amount;
        this.virus_chance = virus_chance;
        return dot;
    }

    ///Stops listening to the swab signal; you can no longer be swabbed.
    // tg: code/datums/elements/swabbable.dm:32
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        if (!isatom(source) || isarea(source))
            return ELEMENT_INCOMPATIBLE;
        UnregisterSignal(source, COMSIG_SWAB_FOR_SAMPLES);
        return dot;
    }

    ///Ran when the parent is swabbed by an object that can swab that type of obj. The list is sent by ref, which means the thing which sent the signal will still have the updated list.
    // tg: code/datums/elements/swabbable.dm:39
    [SignalHandler]
    public virtual object GetSwabbed(datum source = null, DmList mutable_results = null)
    {
        object dot = COMPONENT_SWAB_FOUND; //Return this so the swabbing component knows hes a good boy and found something that needs swabbing.

        LAZYADD(ref mutable_results, Waiting.NoSleep(GenerateSample()));
        Detach(source);
        return dot;
    }

    ///Generates a /datum/biological_sample
    // tg: code/datums/elements/swabbable.dm:47
    public virtual async DmTask<object> GenerateSample()
    {
        datum_biological_sample generated_sample = Dm.@new<datum_biological_sample>();
        await generated_sample.GenerateSample(cell_line_define, virus_define, cell_line_amount, virus_chance);
        return generated_sample;
    }
}
