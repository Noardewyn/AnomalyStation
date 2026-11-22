// Translated from tgstation (snapshot 1d99bd7f): code/genesis_call.dme
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*

	You look around.

	There is nothing but naught about you.

	You've come to the end of the world.

	You get a feeling that you really shouldn't be here.

	Ever.

	But with all ends come beginnings.

	As you turn to leave, you spot it out of the corner of your eye.

	Your eye widen in wonder as you look upon the the legendary treasure.

	After all these years of pouring through shitcode
	your endevours have brought you to...

*/

public static partial class GlobalProcs
{
    /**
     * THE GENESIS CALL
     *
     * THE VERY FIRST LINE OF DM CODE TO EXECUTE
     * Ong this must be done before !!!EVERYTHING!!! else
     * NO IFS ANDS OR BUTS
     * it's a hack, not an example of any sort, and DEFINITELY should NOT be emulated
     * IT JUST HAS TO BE FIRST!!!!!!
     * If you want to do something in the initialization pipeline
     * FIRST RTFM IN /code/game/world.dm
     * AND THEN NEVER RETURN TO THIS PLACE
     *
     *
     *
     * If you're still here, here's an explanation:
     * BYOND loves to tell you about its loving spouse /global
     * But it's actually having a sexy an affair with /static
     * Specifically statics in procs
     * Priority is given to these lines of code in REVERSE order of declaration in the .dme
     * Which is why this file has a funky name
     * So this is what we use to call world.Genesis()
     * It's a nameless, no-op function, because it does absolutely nothing
     * It exists to hold a static var which is initialized to null
     * It's on /world to hide it from reflection
     * Painful right? Good, now you share my suffering
     * Please lock the door on your way out
     */
    // tg: code/genesis_call.dme:51
    public static async DmTask _(this DmWorld world)
    {
        // tg: code/genesis_call.dme:52  var/static/_ = world.Genesis(): a proc-level static, so its initializer runs at world
        // start, first of all static initializers, in TgState.InitStatics (rules 10.6), never when _() is called. Genesis
        // returns nothing, so the static holds null and nothing reads it: the port keeps no field for it.
    }
}
