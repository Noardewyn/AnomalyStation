// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/festive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the owner, 2026-11-18): the sparkler and the large firecracker. The var blocks are generated (D111).

//~*~*~*~*SPARKLER*~*~*~*~*~*~

// tg: code/game/objects/items/grenades/festive.dm:4
public partial class obj_item_sparkler : obj_item
{
    // tg: code/game/objects/items/grenades/festive.dm:14
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        await light__proc();
        return null;
    }

    // tg: code/game/objects/items/grenades/festive.dm:17
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object ignition_msg = await tool.ignition_effect(this, user);
        if (!Dm.truthy(ignition_msg))
            return NONE;
        await light__proc(user, ignition_msg);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/grenades/festive.dm:24
    public virtual async DmTask light__proc(mob user = null, object message = null) // tg: proc/light (the atom var light takes the name)
    {
        if (Dm.truthy(lit))
            return;
        if (user != null && Dm.truthy(message))
            user.visible_message(message);
        lit = TRUE;
        icon_state = "sparkler_on";
        force = 6;
        hitsound = Dm.resource("sound/items/tools/welder.ogg");
        name = Dm.text("lit []", Dm.initial(this, (obj_item_sparkler p) => p.name));
        attack_verb_continuous = Dm.list("burns");
        attack_verb_simple = Dm.list("burn");
        await set_light(l_range: 2, l_power: 2);
        damtype = BURN;
        START_PROCESSING(SSobj, this);
        playsound(this, Dm.resource("sound/effects/fuse.ogg"), 20, TRUE);
        update_appearance();
    }

    // tg: code/game/objects/items/grenades/festive.dm:42
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        burntime -= Dm.num(seconds_per_tick);
        if (burntime <= 0)
        {
            Dm.@new<obj_item_stack_rods>(drop_location());
            qdel(this);
        }
        else
            open_flame(heat);
        return null;
    }

    // tg: code/game/objects/items/grenades/festive.dm:50
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/grenades/festive.dm:54
    public override async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        return span_notice(I18n.T("[] gracefully lights [] with [].", user, A, this));
    }

    // tg: code/game/objects/items/grenades/festive.dm:57
    public override object get_temperature()
    {
        return Dm.num(lit) * Dm.num(heat);
    }
}

//~*~*~*~*FIRECRACKER*~*~*~*~*~*~

// tg: code/game/objects/items/grenades/festive.dm:63
public partial class obj_item_grenade_firecracker : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/festive.dm:75
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra) // You need to light it manually.
    {
        return null;
    }

    // tg: code/game/objects/items/grenades/festive.dm:78
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(active))
            return NONE;
        object ignition_msg = await tool.ignition_effect(this, user);
        if (!Dm.truthy(ignition_msg))
            return NONE;
        visible_message(ignition_msg);
        await arm_grenade(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/grenades/festive.dm:88
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        await detonate();
        return null;
    }

    // tg: code/game/objects/items/grenades/festive.dm:91
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(active))
            return null;
        if (Dm.truthy(det_time))
        {
            det_time -= 10;
            to_chat(user, span_notice(I18n.T("You shorten the fuse of [] with [].", this, tool)));
            playsound(this, Dm.resource("sound/items/tools/wirecutter.ogg"), 20, TRUE);
            icon_state = Dm.text("[]_[]", Dm.initial(this, (obj_item_grenade_firecracker p) => p.icon_state), det_time);
            update_appearance();
        }
        else
            to_chat(user, span_danger("You've already removed all of the fuse!"));
        return null;
    }

    // tg: code/game/objects/items/grenades/festive.dm:103
    public override async DmTask arm_grenade(mob user = null, object delayoverride = null, float? msg = null, float? volume = null)
    {
        msg ??= TRUE;
        volume ??= 80;
        log_grenade(user);
        if (user != null)
        {
            add_fingerprint(user);
            if (Dm.truthy(msg))
                to_chat(user, span_warning(I18n.T("You prime []! []!", this, capitalize(DisplayTimeText(det_time)))));
        }
        playsound(this, Dm.resource("sound/effects/fuse.ogg"), volume, TRUE);
        active = TRUE;
        icon_state = Dm.text("[]_active", Dm.initial(this, (obj_item_grenade_firecracker p) => p.icon_state));
        _addtimer(Dm.@new<datum_callback>(this, nameof(detonate)), delayoverride == null ? det_time : Dm.num(delayoverride), file: "code/game/objects/items/grenades/festive.dm", line: 112);
    }

    // tg: code/game/objects/items/grenades/festive.dm:114
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;
        await update_mob();
        await explosion(this, devastation_range: -1, heavy_impact_range: -1, light_impact_range: 2);
        qdel(this);
        return dot;
    }
}
