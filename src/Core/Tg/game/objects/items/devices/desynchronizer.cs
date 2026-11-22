// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/desynchronizer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file whole: the sync holder (gaps B1: the existential crisis trauma's veil of non-existence is one) and the desynchronizer
// itself (gaps DV2, D333): used in hand it pulls its user out of spacetime into a sync holder (tg's desynchronizer flash, the
// "-on" picture) for its set duration (Alt-click: up to five minutes), used again it brings them back early; the recharge lasts as
// long as the desync did. The var blocks are generated (D111).

// tg: code/game/objects/items/devices/desynchronizer.dm:1
public partial class obj_item_desynchronizer : obj_item
{
    // tg: code/game/objects/items/devices/desynchronizer.dm:26
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (world.time < next_use)
        {
            to_chat(user, span_warning(I18n.T("[] is still recharging.", this)));
            return null;
        }
        if (!Dm.truthy(sync_holder))
            await desync(user as mob_living);
        else
            resync();
        return null;
    }

    // tg: code/game/objects/items/devices/desynchronizer.dm:35
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (world.time < next_use)
            Dm.append(ref dot, span_warning(I18n.T("Time left to recharge: []", DisplayTimeText(next_use - world.time))));
        Dm.append(ref dot, span_notice(I18n.T("Alt-click to customize the duration. Current duration: [].", DisplayTimeText(duration))));
        Dm.append(ref dot, span_notice(I18n.T("Can be used again to interrupt the effect early. The recharge time is the same as the time spent in desync.")));
        return dot;
    }

    // tg: code/game/objects/items/devices/desynchronizer.dm:42
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        object new_duration = await tgui_input_number(user, I18n.T("Set the duration"), I18n.T("Desynchronizer"), Dm.num(duration) / 10, max_duration, 5);
        if (!Dm.truthy(new_duration) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
            return CLICK_ACTION_BLOCKING;
        duration = Dm.num(new_duration);
        to_chat(user, span_notice(I18n.T("You set the duration to [].", DisplayTimeText(duration))));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/devices/desynchronizer.dm:50
    public virtual async DmTask desync(mob_living user = null)
    {
        if (Dm.truthy(sync_holder))
            return;
        sync_holder = Dm.@new<obj_effect_abstract_sync_holder>(drop_location());
        Dm.@new<obj_effect_temp_visual_desynchronizer>(drop_location());
        to_chat(user, span_notice(I18n.T("You activate [], desynchronizing yourself from the present. You can still see your surroundings, but you feel eerily dissociated from reality.", this)));
        await user.forceMove(sync_holder);
        last_use = world.time;
        icon_state = "desynchronizer-on";
        resync_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(resync)), duration, TIMER_STOPPABLE, file: "code/game/objects/items/devices/desynchronizer.dm", line: 59);
    }

    // tg: code/game/objects/items/devices/desynchronizer.dm:61
    public virtual void resync()
    {
        Dm.@new<obj_effect_temp_visual_desynchronizer>(sync_holder.drop_location());
        QDEL_NULL(ref sync_holder);
        if (Dm.truthy(resync_timer))
        {
            deltimer(resync_timer);
            resync_timer = null;
        }
        icon_state = Dm.initial(this, (obj_item_desynchronizer p) => p.icon_state);
        next_use = world.time + (world.time - last_use); // Could be 2*world.time-last_use but that would just be confusing
    }

    // tg: code/game/objects/items/devices/desynchronizer.dm:70
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(sync_holder))
            resync();
        return base.Destroy(force);
    }
}

// tg: code/game/objects/items/devices/desynchronizer.dm:75
public partial class obj_effect_abstract_sync_holder : obj_effect_abstract
{
    // tg: code/game/objects/items/devices/desynchronizer.dm:86
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SECLUDED_LOCATION, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/items/devices/desynchronizer.dm:90
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        // While faded out of spacetime, no, you cannot move.
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/items/devices/desynchronizer.dm:94
    public override object Destroy(float? force = null)
    {
        foreach (object I in Dm.each(contents))
        {
            atom_movable AM = (atom_movable)I;
            Waiting.NoSleep(AM.forceMove(drop_location() as atom));
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/desynchronizer.dm:100
    public override object AllowDrop()
    {
        return TRUE; //no dropping spaghetti out of your spacetime pocket
    }
}
