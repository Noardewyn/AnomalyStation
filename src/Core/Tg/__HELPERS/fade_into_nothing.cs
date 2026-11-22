// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/fade_into_nothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/__HELPERS/fade_into_nothing.dm:2
public partial class atom
{
    /// Applies a fading animation, and then deletes the atom.
    // tg: code/__HELPERS/fade_into_nothing.dm:2
    public virtual void fade_into_nothing(object life_time = null, object fade_time = null)
    {
        float life = Dm.num(life_time ?? (5 * SECONDS));
        float fade = Dm.num(fade_time ?? (3 * SECONDS));
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), life, file: "code/__HELPERS/fade_into_nothing.dm", line: 3);
        if (fade <= 0)
            return;
        if (life > fade)
            _addtimer(Dm.@new<datum_callback>(this, nameof(fade_into_nothing_animate), fade), life - fade, TIMER_DELETE_ME, file: "code/__HELPERS/fade_into_nothing.dm", line: 7);
        else
            fade_into_nothing_animate(fade);
    }

    // tg: code/__HELPERS/fade_into_nothing.dm:11
    public virtual void fade_into_nothing_animate(object fade_time = null)
    {
        animate(this, Dm.named("alpha", 0), Dm.named("time", fade_time), Dm.named("flags", ANIMATION_PARALLEL));
    }
}
