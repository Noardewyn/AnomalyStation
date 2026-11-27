// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/dreams/dream_projection.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the dream sect's projection rite (the chaplain sleeps and walks beside a follower as a figment only the follower
// hears), its status effect, the figment and the action and verb that end it. The var blocks are generated (D111).

// tg: code/modules/religion/dreams/dream_projection.dm:1
public partial class datum_religion_rites_dream_projection : datum_religion_rites
{
    // tg: code/modules/religion/dreams/dream_projection.dm:11
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        ritual_invocations = Dm.list(
            I18n.T("A member of our flock has gone astray, lost in the waking world..."),
            I18n.T("It is the duty of the shepherd to guide those who cannot find their way..."),
            I18n.T("Let me walk through their waking dream, and show them the way back..."));
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:19
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        DmList followers = Dm.list();
        foreach (mob_living follower in Dm.each_cast<mob_living>(GLOB.mob_living_list))
        {
            if (Dm.truthy(follower.mind?.holy_role) && user != follower)
                Dm.append(ref followers, follower);
        }

        if (!Dm.truthy(Dm.length(followers)))
        {
            to_chat(user, span_warning(I18n.T("You have no followers to project into!")));
            return FALSE;
        }

        return await base.perform_rite(user, religious_tool);
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:31
    public override async DmTask post_invoke_effects(mob_living user = null, atom religious_tool = null)
    {
        await base.post_invoke_effects(user, religious_tool);
        DmList followers = Dm.list();
        foreach (mob_living follower in Dm.each_cast<mob_living>(GLOB.mob_living_list))
        {
            if (Dm.truthy(follower.mind?.holy_role) && user != follower)
                Dm.append(ref followers, follower);
        }

        if (!Dm.truthy(Dm.length(followers)))
        {
            refund(0.8f);
            return;
        }

        mob_living_carbon_human target = await tgui_input_list(user, I18n.T("Choose a follower to project into:"), I18n.T("Dream Projection"), followers) as mob_living_carbon_human;
        if (QDELETED(target) || target.stat == DEAD || target.mind?.holy_role == null)
        {
            refund(0.8f);
            return;
        }

        if (!Dm.truthy(await user.apply_status_effect(typeof(datum_status_effect_dream_projection), target)))
        {
            to_chat(user, span_warning(I18n.T("You fail to fall asleep.")));
            refund(0.8f);
            return;
        }
    }
}

// tg: code/modules/religion/dreams/dream_projection.dm:52
public partial class datum_status_effect_dream_projection : datum_status_effect
{
    // tg: code/modules/religion/dreams/dream_projection.dm:63
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        mob_living_carbon_human target = Dm.arg(__extra, 0) as mob_living_carbon_human;
        if (target == null)
        {
            stack_trace("Dream projection created without a target!", "code/modules/religion/dreams/dream_projection.dm", 65);
            qdel(this);
            return null;
        }

        this.target = target;
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:72
    public override object get_examine_text(mob examiner = null)
    {
        return I18n.T("[] [] in a deep slumber, yet [] eyes show a distant look, as if [] [] somewhere far away...", owner.p_They(), owner.p_are(), owner.p_their(), owner.p_they(), owner.p_are());
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:75
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!Dm.truthy(await owner.SetSleeping(20 * SECONDS)))
        {
            to_chat(owner, span_warning(I18n.T("You fail to fall asleep.")));
            return FALSE;
        }

        object dot = await base.on_apply(__extra);
        RegisterSignal(target, COMSIG_QDELETING, nameof(end_projection));
        RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(end_projection));

        ADD_TRAIT(owner, TRAIT_DREAMING, TRAIT_STATUS_EFFECT(id));
        RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_KNOCKEDOUT), nameof(interrupt_projection));
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(interrupt_projection));
        RegisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE, nameof(interrupt_projection));

        projection = Dm.@new<mob_eye_imaginary_friend_dream_projection>(target.loc);
        await projection.AddComponent(typeof(datum_component_temporary_body), Dm.named("old_mind", owner.mind));
        projection.real_name = owner.real_name;
        projection.gender = owner.gender;
        // DEVIATION(DV440): tg's getFlatIcon(owner); the port's flat icon is one picture facing south (get_flat_uni_icon).
        projection.human_icon = get_flat_uni_icon(owner, SOUTH) as icon;
        projection.PossessByPlayer(owner.ckey);
        await projection.attach_to_owner(target);

        RegisterSignal(projection, COMSIG_QDELETING, nameof(stop_projection));

        owner.add_filter(id, 1, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", "#aee2b2"), Dm.kv("alpha", 0), Dm.kv("size", 2)));
        await owner.update_filters();
        object filter = owner.get_filter(id);
        animate(filter, Dm.named("alpha", 150), Dm.named("time", 2 * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)), Dm.named("loop", -1));
        animate(Dm.named("alpha", 0), Dm.named("time", 2 * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
        return dot;
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:105
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        UnregisterSignal(target, COMSIG_QDELETING);
        UnregisterSignal(target, COMSIG_LIVING_DEATH);
        target = null;

        REMOVE_TRAIT(owner, TRAIT_DREAMING, TRAIT_STATUS_EFFECT(id));
        UnregisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_KNOCKEDOUT));
        UnregisterSignal(owner, COMSIG_LIVING_DEATH);
        UnregisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE);

        if (!QDELING(owner))
        {
            await owner.adjust_drowsiness(10 * SECONDS);
            object filter = owner.get_filter(id);
            animate(filter, Dm.named("alpha", 0), Dm.named("time", 1 * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
            _addtimer(Dm.@new<datum_callback>(owner, nameof(datum.remove_filter), id), 1 * SECONDS, file: "code/modules/religion/dreams/dream_projection.dm", line: 122); // delay the filter removal to let the transition finish
        }

        UnregisterSignal(projection, COMSIG_QDELETING);
        if (QDELING(projection))
            projection = null;
        else
            QDEL_NULL(ref projection);
        return dot;
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:128
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (owner.mind?.holy_role == null)
        {
            end_projection();
            return null;
        }

        await owner.SetSleeping(20 * SECONDS); // keep the owner asleep
        return null;
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:135
    [SignalHandler]
    public virtual void end_projection()
    {
        to_chat(owner, span_warning(I18n.T("Your dream projection ends as your target is no longer valid.")));
        Waiting.NoSleep(owner.SetSleeping(10 * SECONDS));
        qdel(this);
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:141
    [SignalHandler]
    public virtual void interrupt_projection()
    {
        to_chat(owner, span_warning(I18n.T("Your dream projection is interrupted!")));
        Waiting.Detach(owner.emote("gasp"));
        owner.visible_message(span_notice(I18n.T("[]'s eyes snap open as they are jolted awake!", owner)), vision_distance: COMBAT_MESSAGE_RANGE, ignored_mobs: owner);
        qdel(this);
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:148
    [SignalHandler]
    public virtual void stop_projection()
    {
        to_chat(owner, span_warning(I18n.T("You end your dream projection and return to your body.")));
        Waiting.NoSleep(owner.SetSleeping(10 * SECONDS));
        qdel(this);
    }
}

// tg: code/modules/religion/dreams/dream_projection.dm:154
public partial class mob_eye_imaginary_friend_dream_projection : mob_eye_imaginary_friend
{
    // tg: code/modules/religion/dreams/dream_projection.dm:157
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_action_innate_stop_projection exit_action = Dm.@new<datum_action_innate_stop_projection>(this);
        Waiting.NoSleep(exit_action.Grant(this));
        overlay_fullscreen("curse", typeof(atom_movable_screen_fullscreen_curse), 1); // todo something more fitting?
        return dot;
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:163
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        client.eye = (owner is var or__1 && Dm.truthy(or__1) ? (atom)or__1 : this);
        return dot;
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:167
    public override void greet()
    {
        return;
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:173  GAME_VERB_DESC(/mob/eye/imaginary_friend/dream_projection, stop_projection,
    // "Stop Projection", ...): the verb's body
    public virtual void __gvb_stop_projection(DmList structured_args = null)
    {
        qdel(this);
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:173  the verb: a call of the body after SSverbs.invoke_verb's check (a caller
    // without a client does nothing)
    public virtual void stop_projection(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        __gvb_stop_projection(Dm.list());
    }

    // tg: code/modules/religion/dreams/dream_projection.dm:175
    public override async DmTask attach_to_owner(mob_living imaginary_friend_owner = null)
    {
        await base.attach_to_owner(imaginary_friend_owner);
        if (client != null)
            client.eye = owner;
    }
}

// tg: code/modules/religion/dreams/dream_projection.dm:179
public partial class datum_action_innate_stop_projection : datum_action_innate
{
    // tg: code/modules/religion/dreams/dream_projection.dm:183
    public override async DmTask<object> Activate()
    {
        qdel(owner);
        await DmTask.CompletedTask;
        return null;
    }
}
