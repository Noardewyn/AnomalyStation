// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/hourglass.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps TY1 (D333): the whole file. The hourglass: flipped in hand (tg's flip flick) its sand runs down through seven states in a
// minute while its countdown counts the seconds, and it stops by itself or when flipped again; the admins' hourglass is turned by an
// admin's hand or ghost. The var blocks are generated (D111).

// tg: code/game/objects/items/hourglass.dm:4
public partial class obj_item_hourglass : obj_item
{
    // tg: code/game/objects/items/hourglass.dm:1  #define HOURGLASS_STATES 7 (file-local, #undef at :91)
    private const int HOURGLASS_STATES = 7; //Remember to update if you change the sprite

    // tg: code/game/objects/items/hourglass.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        countdown = Dm.@new<obj_effect_countdown_hourglass>(this);
        return dot;
    }

    // tg: code/game/objects/items/hourglass.dm:18
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(hand_activated))
            toggle(user);
        return dot;
    }

    // tg: code/game/objects/items/hourglass.dm:23
    public virtual void toggle(mob user = null)
    {
        if (!Dm.truthy(timing_id))
        {
            to_chat(user, span_notice(I18n.T("You flip the [].", this)));
            start();
            flick("hourglass_flip", this);
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You stop the [].", this))); //Sand magically flows back because that's more convinient to use.
            stop();
        }
    }

    // tg: code/game/objects/items/hourglass.dm:32
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("hourglass_[]", Dm.truthy(timing_id) ? "active" : "idle");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/hourglass.dm:36
    public virtual void start()
    {
        finish_time = world.time + Dm.num(time);
        timing_id = _addtimer(Dm.@new<datum_callback>(this, nameof(finish)), time, TIMER_STOPPABLE, file: "code/game/objects/items/hourglass.dm", line: 38);
        countdown.start();
        timing_animation();
    }

    // tg: code/game/objects/items/hourglass.dm:42
    public virtual void timing_animation()
    {
        float step_time = Dm.num(time) / HOURGLASS_STATES;
        animate(this, Dm.named("time", step_time), Dm.named("icon_state", "hourglass_1"));
        for (float i = 2, i__end = HOURGLASS_STATES; i <= i__end; i++)
            animate(Dm.named("time", step_time), Dm.named("icon_state", Dm.text("hourglass_[]", i)));
    }

    // tg: code/game/objects/items/hourglass.dm:48
    public virtual void stop()
    {
        if (Dm.truthy(timing_id))
        {
            deltimer(timing_id);
            timing_id = null;
        }
        countdown.stop();
        finish_time = null;
        animate(this);
        update_appearance();
    }

    // tg: code/game/objects/items/hourglass.dm:57
    public virtual void finish()
    {
        visible_message(span_notice(I18n.T("[] stops.", this)));
        stop();
    }

    // tg: code/game/objects/items/hourglass.dm:61
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref countdown);
        return base.Destroy(force);
    }
}

//Admin events zone.
// tg: code/game/objects/items/hourglass.dm:67
public partial class obj_item_hourglass_admin : obj_item_hourglass
{
    // tg: code/game/objects/items/hourglass.dm:72
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (user.client != null && user.client.holder != null)
            toggle(user);
        return dot;
    }

    // tg: code/game/objects/items/hourglass.dm:77
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        if (user.client != null && user.client.holder != null)
            toggle(user);
        await DmTask.CompletedTask;
        return null;
    }
}
