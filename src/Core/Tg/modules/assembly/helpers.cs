// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4b, D316: the tank transfer valve is an assembly holder).

// See _DEFINES/is_helpers.dm for type helpers

// tg: code/modules/assembly/helpers.dm:8
public partial class obj : atom_movable
{
    /*
    Name: IsSpecialAssembly
    Desc: If true is an object that can be attached to an assembly holder but is a special thing like a plasma can or door
    */
    // tg: code/modules/assembly/helpers.dm:8
    public virtual object IsSpecialAssembly()
    {
        return FALSE;
    }

    /*
    Name: IsAssemblyHolder
    Desc: If true is an object that can hold an assemblyholder object
    */
    // tg: code/modules/assembly/helpers.dm:15
    public virtual object IsAssemblyHolder()
    {
        return FALSE;
    }
}
