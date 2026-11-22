// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/edit_complainer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3c, D316: the awakened plushie). The admins' variable edits that send COMSIG_GLOB_VAR_EDIT are not
// ported, so it says nothing yet. The var block is generated (D111).

// This is just a bit of fun while making an example for global signal
// tg: code/datums/components/edit_complainer.dm:2
public partial class datum_component_edit_complainer : datum_component
{
    // tg: code/datums/components/edit_complainer.dm:5
    [DmParams("text")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList text = Dm.arg<DmList>(__extra, 0);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        DmList default_lines = TgState.Current.datum_component_edit_complainer__default_lines ??= Dm.list(
            "CentCom's profligacy frays another thread.",
            "Another tug at the weave.",
            "Who knows when the stresses will finally shatter the form?",
            "Even now a light shines through the cracks.",
            "CentCom once more twists knowledge beyond its authority.",
            "There is an uncertain air in the mansus.");
        say_lines = text ?? default_lines;

        RegisterSignal(SSdcs, COMSIG_GLOB_VAR_EDIT, nameof(var_edit_react));
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/edit_complainer.dm:21
    [SignalHandler]
    public virtual void var_edit_react(datum source = null, DmList arguments = null)
    {
        atom_movable master = (atom_movable)parent;
        Waiting.Detach(master.say(pick(say_lines)));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/edit_complainer.dm:9  var/static/list/default_lines
    internal DmList datum_component_edit_complainer__default_lines;
}
