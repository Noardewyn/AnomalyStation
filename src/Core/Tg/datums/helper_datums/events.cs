// Translated from tgstation (snapshot 1d99bd7f): code/datums/helper_datums/events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298): the keycard devices talk to each other through it.

// tg: code/datums/helper_datums/events.dm:6
public partial class datum_events : datum
{
    // tg: code/datums/helper_datums/events.dm:9
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        events = Dm.list();
    }

    // tg: code/datums/helper_datums/events.dm:13
    public override object Destroy(float? force = null)
    {
        events = null;
        return base.Destroy(force);
    }

    // tg: code/datums/helper_datums/events.dm:17
    public virtual object addEventType(object event_type = null)
    {
        if (!Dm.@in(event_type, events) || !islist(events[event_type]))
        {
            events[event_type] = Dm.list();
            return TRUE;
        }
        return FALSE;
    }

    /// Arguments: event_type as text, proc_holder as datum, proc_name as text
    /// Returns: New event, null on error.
    // tg: code/datums/helper_datums/events.dm:25
    public virtual object addEvent(object event_type = null, datum_callback cb = null)
    {
        if (!Dm.truthy(event_type) || cb == null)
            return null;
        addEventType(event_type);
        DmList @event = events[event_type] as DmList;
        Dm.append(ref @event, cb);
        return cb;
    }

    /// Arguments: event_type as text, any number of additional arguments to pass to event handler
    /// Returns: null
    // tg: code/datums/helper_datums/events.dm:35
    public virtual void fireEvent(object eventName = null, params object[] args)
    {
        DmList @event = LAZYACCESS(events, eventName) as DmList;
        if (@event != null)
        {
            foreach (datum_callback cb in Dm.each<datum_callback>(@event))
                cb.InvokeAsync(args);
        }
    }

    /// Arguments: event_type as text, E as /datum/event
    /// Returns: TRUE if event cleared, FALSE on error
    // tg: code/datums/helper_datums/events.dm:44
    public virtual object clearEvent(object event_type = null, datum_callback cb = null)
    {
        if (!Dm.truthy(event_type) || cb == null)
            return FALSE;
        DmList @event = LAZYACCESS(events, event_type) as DmList;
        @event?.Remove(cb);
        return TRUE;
    }
}
