// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/items/stealth_box.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3: the stealth implant's box). The suicide log is a not-translated leaf (the suicide verb's logs).
// The var blocks are generated (D111).

///MGS BOX!
// tg: code/datums/actions/items/stealth_box.dm:2
public partial class datum_action_item_action_agent_box : datum_action_item_action
{
    ///Handles opening and closing the box.
    // tg: code/datums/actions/items/stealth_box.dm:15
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        object dot = await base.do_effect(trigger_flags);
        if (!Dm.truthy(dot))
            return FALSE;
        if (owner.loc is obj_structure_closet_cardboard_agent box)
        {
            if (Dm.truthy(await box.open()))
                owner.playsound_local(box, Dm.resource("sound/misc/box_deploy.ogg"), 50, TRUE);
            return FALSE;
        }
        //Box closing from here on out.
        if (!isturf(owner.loc)) //Don't let the player use this to escape mechs/welded closets.
        {
            to_chat(owner, span_warning(I18n.T("You need more space to activate this implant!")));
            return FALSE;
        }
        if (!(box_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return FALSE;
        box_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))
        atom new_box = await Dm.new_async(boxtype, owner.drop_location()) as atom;
        await owner.forceMove(new_box);
        owner.playsound_local(new_box, Dm.resource("sound/misc/box_deploy.ogg"), 50, TRUE);
        return dot;
    }

    // tg: code/datums/actions/items/stealth_box.dm:35
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        if (owner != null)
            RegisterSignal(owner, COMSIG_LIVING_SUICIDE_ACT, nameof(suicide_act));
        return dot;
    }

    // tg: code/datums/actions/items/stealth_box.dm:40
    public override async DmTask Remove(mob M = null)
    {
        if (owner != null)
            UnregisterSignal(owner, COMSIG_LIVING_SUICIDE_ACT);
        await base.Remove(M);
    }

    // tg: code/datums/actions/items/stealth_box.dm:45
    [SignalHandler]
    public virtual object suicide_act(datum source = null)
    {
        if (owner.loc is not obj_structure_closet_cardboard_agent box)
            return null;

        owner.playsound_local(box, Dm.resource("sound/misc/box_deploy.ogg"), 50, TRUE);
        Waiting.NoSleep(box.open());
        owner.visible_message(span_suicide(I18n.T("[] falls out of []! It looks like [] committed suicide!", owner, box, owner.p_they())));
        Waiting.NoSleep(owner.throw_at(get_turf(owner)));
        if (isliving(owner))
            // tg: :57-58  suicider.suicide_log()
            Dm.Deferred("code/datums/actions/items/stealth_box.dm:58", "suicide_act: suicide_log (the suicide verb's logs, not ported)");
        return OXYLOSS;
    }
}
