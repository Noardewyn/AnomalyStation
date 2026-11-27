// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/life_candle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The life candle (an admin's and an event's spawn): a touch links the toucher's mind to it (the
// flame lit, the candle floating) or severs it; while lit it checks its minds and five seconds after one's body dies or is gone it
// brings it back on the candle's turf, revived, or in a new body. Its light's flicker goes through the light middleman (DV688: the
// client's flame flicker). The var block is generated
// (D111).

// tg: code/game/objects/structures/life_candle.dm:1
public partial class obj_structure_life_candle : obj_structure
{
    // tg: code/game/objects/structures/life_candle.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (light_system == OVERLAY_LIGHT || light_system == OVERLAY_LIGHT_DIRECTIONAL || light_system == OVERLAY_LIGHT_BEAM) // IS_OVERLAY_LIGHT_SYSTEM (__DEFINES/lighting.dm), inline
        {
            middleman = Dm.@new<datum_light_middleman>(this, "life_candle");
            RegisterSignal(middleman, COMSIG_LIGHT_MIDDLEMAN_UPDATED, nameof(light_updated));
            middleman.being_overriding_light();
        }
        Waiting.NoSleep(AddElement(typeof(datum_element_movetype_handler)));
        return dot;
    }

    // tg: code/game/objects/structures/life_candle.dm:41
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref middleman);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/life_candle.dm:45
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (user.mind == null)
            return dot;
        if (Dm.@in(user.mind, linked_minds))
        {
            user.visible_message(span_notice(I18n.T("[] reaches out and pinches the flame of [].", user, this)), span_warning(I18n.T("You sever the connection between yourself and [].", this)));
            Dm.remove(ref linked_minds, user.mind);
            if (Dm.length(linked_minds) == 0)
                REMOVE_TRAIT(this, TRAIT_MOVE_FLOATING, LIFECANDLE_TRAIT);
        }
        else
        {
            if (Dm.length(linked_minds) == 0)
                ADD_TRAIT(this, TRAIT_MOVE_FLOATING, LIFECANDLE_TRAIT);
            user.visible_message(span_notice(I18n.T("[] touches []. It seems to respond to [] presence!", user, this, user.p_their())), span_warning(I18n.T("You create a connection between you and [].", this)));
            Dm.combine(ref linked_minds, user.mind);
        }

        update_appearance();
        if (Dm.length(linked_minds) > 0)
        {
            START_PROCESSING(SSobj, this);
            await set_light_on(TRUE);
        }
        else
        {
            STOP_PROCESSING(SSobj, this);
            await set_light_on(FALSE);
        }
        return dot;
    }

    // tg: code/game/objects/structures/life_candle.dm:70
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.length(linked_minds) > 0 ? icon_state_active : icon_state_inactive;
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/life_candle.dm:74
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.length(linked_minds) > 0)
            Dm.append(ref dot, I18n.T("[] is active, and linked to [] souls.", this, Dm.length(linked_minds)));
        else
            Dm.append(ref dot, I18n.T("It is static, still, unmoving."));
        return dot;
    }

    // tg: code/game/objects/structures/life_candle.dm:81
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.length(linked_minds) == 0)
        {
            STOP_PROCESSING(SSobj, this);
            return null;
        }

        foreach (datum_mind mind in Dm.each<datum_mind>(linked_minds))
        {
            if (mind.current == null || (mind.current != null && mind.current.stat == DEAD))
                _addtimer(Dm.@new<datum_callback>(this, nameof(respawn), mind), respawn_time, TIMER_UNIQUE, file: "code/game/objects/structures/life_candle.dm", line: 89);
        }
        return null;
    }

    // tg: code/game/objects/structures/life_candle.dm:91
    [SignalHandler]
    public virtual void light_updated(datum source = null)
    {
        fire_flicker_middleman(middleman);
    }

    // tg: code/game/objects/structures/life_candle.dm:95
    public virtual async DmTask respawn(datum_mind mind = null)
    {
        turf T = get_turf(this);
        mob_living body = null;
        if (mind.current != null)
        {
            if (mind.current.stat != DEAD)
                return;
            else
                body = mind.current as mob_living;
        }
        if (body == null)
        {
            body = Dm.@new(mob_type, T) as mob_living;
            mob ghostie = mind.get_ghost(TRUE) as mob;
            ghostie.client?.prefs?.safe_transfer_prefs_to(body as mob_living_carbon_human);
            await mind.transfer_to(body);
        }
        else
        {
            await body.forceMove(T);
            await body.revive(ADMIN_HEAL_ALL, force_grab_ghost: TRUE);
        }
        await body.flash_act();

        object outfit_datum = outfit; // istype(outfit): the var holds an outfit datum when an admin sets one
        if (ishuman(body) && outfit_datum is datum_outfit worn_outfit)
            await worn_outfit.equip(body as mob_living_carbon_human);
        playsound(T, respawn_sound, 50, TRUE);
    }
}
