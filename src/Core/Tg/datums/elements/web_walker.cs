// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/web_walker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the web walker (a maintenance spider is slower off its webs). The var block is generated (D111),
// but the slowdown, which holds a movespeed modifier's typepath (GAP(types): the generator types it as the modifier datum), so it is
// declared here as the typepath it is.

/**
 * # Web Walker element
 *
 * A mob with this element will move more slowly when it's not stood on a webbed turf.
 */
// tg: code/datums/elements/web_walker.dm:6
public partial class datum_element_web_walker : datum_element
{
    /// Move speed modifier to apply when not stood on webs
    // tg: code/datums/elements/web_walker.dm:10  (hand-written: see the GAP(types) above)
    public Type off_web_slowdown;

    // tg: code/datums/elements/web_walker.dm:6  (the type's overrides, hand-written with the var above)
    public datum_element_web_walker()
    {
        element_flags = ELEMENT_BESPOKE;
        argument_hash_start_idx = 2;
    }

    // tg: code/datums/elements/web_walker.dm:12
    [DmParams("target", "off_web_slowdown")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        var off_web_slowdown = Dm.arg(__extra, 0) as Type;
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        this.off_web_slowdown = off_web_slowdown;

        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        return dot;
    }

    // tg: code/datums/elements/web_walker.dm:20
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_MOVABLE_MOVED);
        return dot;
    }

    /// When we move, check if we're still on a web
    // tg: code/datums/elements/web_walker.dm:25
    [SignalHandler]
    public virtual void on_moved(mob_living source = null)
    {
        var web = Dm.locate<obj_structure_spider_stickyweb>(get_turf(source));
        if (web != null)
            source.remove_movespeed_modifier(off_web_slowdown);
        else
            Waiting.NoSleep(source.add_movespeed_modifier(off_web_slowdown));
    }
}
