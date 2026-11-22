// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/experimentor/wires.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RD4, D333: the experimentor): its randomized wires, one per scan type and an eject wire; a pulse runs that
// experiment on the loaded item, or ejects it. The var block is generated (D111).

// tg: code/modules/research/experimentor/wires.dm:1
public partial class datum_wires_experimentor : datum_wires
{
    // tg: code/modules/research/experimentor/wires.dm:6
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(
            SCANTYPE_POKE,
            SCANTYPE_IRRADIATE,
            SCANTYPE_GAS,
            SCANTYPE_HEAT,
            SCANTYPE_COLD,
            SCANTYPE_OBLITERATE,
            SCANTYPE_DISCOVER,
            WIRE_EJECT
        );
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/modules/research/experimentor/wires.dm:19
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        var R = (obj_machinery_rnd_experimentor)holder;
        if (Dm.truthy(R.panel_open))
            return true;
        return false;
    }

    // tg: code/modules/research/experimentor/wires.dm:26
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        var experimentor = (obj_machinery_rnd_experimentor)holder;
        if (experimentor.loaded_item == null)
            return;

        if (Dm.eq(wire, WIRE_EJECT))
            experimentor.item_eject();
        else
            await experimentor.try_perform_experiment(wire);
    }
}
