// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/keep_me_secure.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3c, D316: the whiny plushie; N1a: the nuclear disk). The var block is generated (D111).

/**
 * ### Keep Me Secure component!
 *
 * Component that attaches to items, invoking a function to react when left unmoved and unsecured for too long.
 * Used for Nuclear Authentication Disks, and whiny plushy as an example (which changes sprites depending on whether it considers itself secure.)
 */
// tg: code/datums/components/keep_me_secure.dm:7
public partial class datum_component_keep_me_secure : datum_component
{
    // tg: code/datums/components/keep_me_secure.dm:20
    [DmParams("secured_callback", "unsecured_callback", "min_pop_limit")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        secured_callback = Dm.arg<datum_callback>(__extra, 0);
        unsecured_callback = Dm.arg<datum_callback>(__extra, 1);
        min_pop_limit = Dm.arg(__extra, 2);
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/keep_me_secure.dm:28
    public override object Destroy(float? force = null)
    {
        secured_callback = null;
        unsecured_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/keep_me_secure.dm:33
    public override async DmTask RegisterWithParent()
    {
        last_move = world.time;
        if (secured_callback != null || unsecured_callback != null)
            START_PROCESSING(SSobj, this);
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE_MORE, nameof(on_examine_more));
        await base.RegisterWithParent();
    }

    // tg: code/datums/components/keep_me_secure.dm:41
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        STOP_PROCESSING(SSobj, this);
        UnregisterSignal(parent, COMSIG_ATOM_EXAMINE);
        await base.UnregisterFromParent(__extra);
    }

    /// Returns whether the game is supposed to consider the parent "secure".
    // tg: code/datums/components/keep_me_secure.dm:46
    public virtual bool is_secured()
    {
        if (living_player_count() < Dm.num(min_pop_limit))
            return true;

        obj_item item_parent = (obj_item)parent;
        if (last_secured_location == get_turf(item_parent))
            return false;

        mob holder = item_parent.pulledby as mob ?? get(parent, typeof(mob)) as mob;
        if (holder?.client == null)
            return false;

        return true;
    }

    // tg: code/datums/components/keep_me_secure.dm:60
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (is_secured())
        {
            last_secured_location = get_turf((atom)parent);
            last_move = world.time;
            if (secured_callback != null)
                Waiting.NoSleep(secured_callback.Invoke(last_move));
        }
        else if (unsecured_callback != null)
            Waiting.NoSleep(unsecured_callback.Invoke(last_move));
        return null;
    }

    /// signal sent when parent is examined
    // tg: code/datums/components/keep_me_secure.dm:71
    [SignalHandler]
    public virtual void on_examine(mob_living source = null, mob examiner = null, DmList examine_list = null)
    {
        Dm.append(examine_list, span_boldnotice(I18n.T("[] should be secured at all times.", parent)));
        if (is_secured())
            Dm.append(examine_list, span_notice(I18n.T("Right now, it is.")));
        else
            Dm.append(examine_list, span_warning(I18n.T("Right now, it isn't...")));
        Dm.append(examine_list, span_notice(I18n.T("Examine closer for more info.")));
    }

    /// signal sent when parent is examined more
    // tg: code/datums/components/keep_me_secure.dm:82
    [SignalHandler]
    public virtual void on_examine_more(mob_living source = null, mob examiner = null, DmList examine_list = null)
    {
        Dm.append(examine_list, span_notice(I18n.T("For [] to be secure, it needs to be:", parent)));
        Dm.append(examine_list, span_notice(I18n.T("1. Always on the move, and...")));
        Dm.append(examine_list, span_notice(I18n.T("2. Held or dragged by someone.")));
    }
}
