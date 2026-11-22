// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/brain/processor/_processor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the cyborg's and the AI's brain container. The var block is generated (D111) but the radio,
// declared here (the generator reads a var named radio as a typepath).

// tg: code/modules/mob/living/brain/processor/_processor.dm:1
public partial class obj_item_brain_processor : obj_item
{
    /// The processor's built-in radio.
    // tg: code/modules/mob/living/brain/processor/_processor.dm:16
    public obj_item_radio_brain_processor radio;

    // The type's overrides of inherited vars (the Hand mode, types-converter.md 3.2: the class declares radio, so the generated
    // part holds only the vars it does not declare)
    // tg: code/modules/mob/living/brain/processor/_processor.dm:2
    public obj_item_brain_processor()
    {
        abstract_type = typeof(obj_item_brain_processor);
        name = Dm.text("\\improper Cerebral Processing Unit");
        desc = "Pure processing potential packed into a puny but powerful pundit.";
        icon = Dm.resource("icons/obj/devices/assemblies.dmi");
        w_class = 3;
        custom_materials = null;
        buckle_message_cooldown = 1E+31f;
    }

    // tg: code/modules/mob/living/brain/processor/_processor.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        radio = Dm.@new<obj_item_radio_brain_processor>(this);
        radio.set_broadcasting(FALSE); //researching radio mmis turned the robofabs into radios because this didnt start as FALSE.
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/_processor.dm:34
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref brainmob);
        QDEL_NULL(ref laws);
        QDEL_NULL(ref radio);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/brain/processor/_processor.dm:40
    public virtual void set_brainmob(mob_living_brain new_brainmob = null)
    {
        if (brainmob == new_brainmob)
            return;

        mob_living_brain old_brainmob = brainmob;
        if (old_brainmob != null)
        {
            old_brainmob.container = null;
            old_brainmob.emp_damage = 0; // okay?
            REMOVE_TRAIT(old_brainmob, TRAIT_SILICON_EMOTES_ALLOWED, MMI_ASSISTED);
        }

        brainmob = new_brainmob;

        if (new_brainmob != null)
        {
            new_brainmob.container = this;
            ADD_TRAIT(new_brainmob, TRAIT_SILICON_EMOTES_ALLOWED, MMI_ASSISTED);
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_MMI_SET_BRAINMOB]))
            _SendSignal(COMSIG_MMI_SET_BRAINMOB, Dm.list(this, old_brainmob));
    }

    // tg: code/modules/mob/living/brain/processor/_processor.dm:60  GAME_VERB_SRC_DESC(/obj/item/brain_processor, Toggle_Listening, usr.loc, "Toggle Listening", "Toggle listening channel on or off.", "MMI")
    // The verb hands its arguments to SSverbs.invoke_verb, which runs the body for a caller with a client (observer.cs's
    // reenter_corpse); the verb panel and SSverbs are M9.
    public virtual void Toggle_Listening(params object[] __extra)
    {
        mob resolved_caller = usr;
        if (resolved_caller?.client == null)
            return;
        Waiting.Detach(__gvb_Toggle_Listening(Dm.list()));
    }

    // tg: code/modules/mob/living/brain/processor/_processor.dm:61  (the verb's body, __gvb_Toggle_Listening)
    public virtual async DmTask __gvb_Toggle_Listening(DmList structured_args = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(brainmob))
        {
            to_chat(brainmob, span_warning(I18n.T("Can't do that while incapacitated or dead!")));
            return;
        }
        if (!Dm.truthy(radio.is_on()))
        {
            to_chat(brainmob, span_warning(I18n.T("Your radio is disabled!")));
            return;
        }

        radio.set_listening(Dm.not(radio.get_listening()));
        to_chat(brainmob, span_notice(Dm.truthy(radio.get_listening()) ? I18n.T("Radio is now receiving broadcast.") : I18n.T("Radio is no longer receiving broadcast.")));
    }

    /// Transfers the user into the brain processor, preserving its information (such as name and DNA if applicable)
    /// Does not transfer mind or ckey, this must be handled by the user of this proc.
    // tg: code/modules/mob/living/brain/processor/_processor.dm:73
    public virtual async DmTask transfer_identity(mob_living transferred_user = null)
    {
        if (brainmob == null)
            set_brainmob(Dm.@new<mob_living_brain>(this));

        set_name(transferred_user.real_name);
        brainmob.timeofdeath = transferred_user.timeofdeath;

        if (transferred_user is mob_living_carbon carbon_user && Dm.truthy(carbon_user.has_dna()))
        {
            brainmob.stored_dna ??= Dm.@new<datum_dna_stored>(brainmob);
            await carbon_user.dna.copy_dna(brainmob.stored_dna);
        }
    }

    // tg: code/modules/mob/living/brain/processor/_processor.dm:85
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (brainmob == null || iscyborg(loc))
            return dot;

        switch (Dm.num(severity))
        {
            case 1:
                brainmob.emp_damage = min(brainmob.emp_damage + rand(20, 30), 30);
                break;
            case 2:
                brainmob.emp_damage = min(brainmob.emp_damage + rand(10, 20), 30);
                break;
            case 3:
                brainmob.emp_damage = min(brainmob.emp_damage + rand(0, 10), 30);
                break;
        }
        await brainmob.emote("alarm");
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/_processor.dm:101
    public virtual object brain_check(mob user = null)
    {
        if (brainmob == null)
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("\\The [] indicates that there is no mind present!", this)));
            return FALSE;
        }
        if (!Dm.truthy(brainmob.key) || brainmob.mind == null)
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("\\The [] indicates that their mind is completely unresponsive!", this)));
            return FALSE;
        }
        if (brainmob.client == null)
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("\\The [] indicates that their mind is currently inactive.", this)));
            return FALSE;
        }
        if (Dm.truthy(suicided()))
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("\\The [] indicates that their mind has no will to live!", this)));
            return FALSE;
        }
        if (brainmob.stat >= DEAD)
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("\\The [] indicates that the brain is dead!", this)));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/brain/processor/_processor.dm:126
    public virtual object replacement_ai_name()
    {
        return brainmob.name;
    }

    // tg: code/modules/mob/living/brain/processor/_processor.dm:129
    public virtual object suicided()
    {
        return HAS_TRAIT(brainmob, TRAIT_SUICIDED) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/brain/processor/_processor.dm:132
    public virtual void set_name(object new_name = null)
    {
        brainmob.name = Dm.str(new_name);
        brainmob.real_name = Dm.str(new_name);
    }

    // tg: code/modules/mob/living/brain/processor/_processor.dm:136
    public virtual void set_suicide(object suicided = null)
    {
        brainmob.set_suicide(suicided);
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:75  (the base type's attackby, interleaved in organic.dm)
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (brainmob == null)
            return await base.attackby(attacking_item, user, modifiers, attack_modifiers);
        await attacking_item.attack__proc(brainmob, user as mob_living); //Oh noooeeeee
        return null;
    }
}
