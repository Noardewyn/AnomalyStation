// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/soft_caps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /obj/item/clothing/head/soft's dropped (M4a clothing: a cap is put on and taken off, and comes off facing forward
// again) and, J1 (D298), flipping the cap (click_alt, flip) and its examine. The Flip cap verb is a live-partial row (verbs, M10+).
// Gaps CL1 (D333): the fishing hat (its crustacean speech and, since gaps SL1, the fishing skill's reward; the fishing difficulty
// waits for fishing) and the propeller hat (its spinning propeller's worn state, its mood and its suicide).

// tg: code/modules/clothing/head/soft_caps.dm:1
public partial class obj_item_clothing_head_soft : obj_item_clothing_head
{
    // tg: code/modules/clothing/head/soft_caps.dm:19
    [DmParams()]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        icon_state = Dm.text("[][]", soft_type, soft_suffix);
        flipped = FALSE;
        await base.dropped(user, silent, __extra);
        return null;
    }

    // tg: code/modules/clothing/head/soft_caps.dm:29
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await flip(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/clothing/head/soft_caps.dm:34
    public virtual async DmTask flip(mob user = null)
    {
        if (!Dm.truthy(user.incapacitated))
        {
            flipped = Dm.truthy(flipped) ? FALSE : TRUE;
            if (Dm.truthy(flipped))
            {
                icon_state = Dm.text("[][]_flipped", soft_type, soft_suffix);
                to_chat(user, span_notice(I18n.T("You flip the hat backwards.")));
            }
            else
            {
                icon_state = Dm.text("[][]", soft_type, soft_suffix);
                to_chat(user, span_notice(I18n.T("You flip the hat back in normal position.")));
            }
            await update_icon();
            await usr.update_worn_head(); //so our mob-overlays update
        }
    }

    // tg: code/modules/clothing/head/soft_caps.dm:46
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Alt-click the cap to flip it [].", Dm.truthy(flipped) ? I18n.T("forwards") : I18n.T("backwards"))));
        return dot;
    }
}

// tg: code/modules/clothing/head/soft_caps.dm:146
public partial class obj_item_clothing_head_soft_fishing_hat : obj_item_clothing_head_soft
{
    // tg: code/modules/clothing/head/soft_caps.dm:164
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", strings("crustacean_replacement.json", "crustacean")))); //you asked for this.
        Waiting.NoSleep(AddElement(typeof(datum_element_skill_reward), typeof(datum_skill_fishing)));
        Dm.Deferred("code/modules/clothing/head/soft_caps.dm:168", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/head/soft_caps.dm:173
public partial class obj_item_clothing_head_soft_propeller_hat : obj_item_clothing_head_soft
{
    // tg: code/modules/clothing/head/soft_caps.dm:170  #define PROPHAT_MOOD "prophat" (file-local, rules 11.4)
    private const string PROPHAT_MOOD = "prophat";
    // tg: code/modules/clothing/head/soft_caps.dm:171  #define PROPHAT_SUICIDE_TIME (10 SECONDS)
    private const float PROPHAT_SUICIDE_TIME = 10 * SECONDS;

    // tg: code/modules/clothing/head/soft_caps.dm:185
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        worn_icon_state = Dm.text("[][][]", soft_type, Dm.truthy(flipped) ? "_flipped" : null, Dm.truthy(active) ? "_on" : null);
        return dot;
    }

    // tg: code/modules/clothing/head/soft_caps.dm:189
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        active = Dm.num(!Dm.truthy(active));
        balloon_alert(user, Dm.truthy(active) ? I18n.T("started propeller") : I18n.T("stopped propeller"));
        await update_icon();
        await user.update_worn_head();
        add_fingerprint(user);
        return null;
    }

    // tg: code/modules/clothing/head/soft_caps.dm:196
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HEAD)))
        {
            await ((mob_living)user).add_mood_event(PROPHAT_MOOD, typeof(datum_mood_event_prophat));
            RegisterSignal(user, COMSIG_LIVING_SUICIDE_ACT, nameof(on_suicide_act));
        }
    }

    // tg: code/modules/clothing/head/soft_caps.dm:202
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, COMSIG_LIVING_SUICIDE_ACT);
        ((mob_living)user).clear_mood_event(PROPHAT_MOOD);
        active = FALSE;
        await update_icon();
        return dot;
    }

    // tg: code/modules/clothing/head/soft_caps.dm:209
    [SignalHandler]
    public virtual object on_suicide_act(mob_living source = null)
    {
        if (Dm.truthy(source.get_active_held_item()))
            return NONE;

        return Waiting.NoSleep(suicide_act(source));
    }

    // tg: code/modules/clothing/head/soft_caps.dm:216
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (!isturf(user.loc))
        {
            user.visible_message(span_suicide(I18n.T("[] starts spinning [] as fast as possible! It looks like [] trying to fly off into the sunset... yet the sky is out of reach for []...", user, this, user.p_theyre(), user.p_them())));
            return SHAME;
        }

        ADD_TRAIT(this, TRAIT_NODROP, TRAIT_GENERIC);
        user.add_traits(Dm.list(TRAIT_GODMODE, TRAIT_FORCED_STANDING, TRAIT_UNDENSE, TRAIT_IMMOBILIZED, TRAIT_INCAPACITATED, TRAIT_HANDS_BLOCKED), TRAIT_GENERIC);
        user.move_resist = INFINITY;
        user.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        user.set_suicide(TRUE);
        user.visible_message(span_suicide(I18n.T("[] starts spinning [] as fast as possible! It looks like [] trying to fly off into the sunset!", user, this, user.p_theyre())));
        playsound(this, Dm.resource("sound/effects/whirthunk.ogg"), 75);
        animate(user, PROPHAT_SUICIDE_TIME, Dm.named("pixel_z", 256), Dm.named("alpha", 0));
        // QDEL_IN(user, PROPHAT_SUICIDE_TIME), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), PROPHAT_SUICIDE_TIME > GC_FILTER_QUEUE ? (object)WEAKREF(user) : user), PROPHAT_SUICIDE_TIME, file: "code/modules/clothing/head/soft_caps.dm", line: 231);
        // drop objects that will get flagged by tsa before we board
        foreach (obj_item should_keep in Dm.each<obj_item>(user.get_all_contents()))
        {
            if (Dm.truthy(Dm.bitand(should_keep.resistance_flags, INDESTRUCTIBLE)))
                await should_keep.forceMove(user.drop_location() as atom);
        }
        return MANUAL_SUICIDE;
    }
}
