// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/brain/processor/organic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326) but the base type's attackby, written with the base type in _processor.cs. The var block is
// generated (D111).

// tg: code/modules/mob/living/brain/processor/organic.dm:1
public partial class obj_item_brain_processor_organic : obj_item_brain_processor
{
    // tg: code/modules/mob/living/brain/processor/organic.dm:11
    [DmParams("mapload", "initial_brain")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_organ_brain initial_brain = Dm.arg<obj_item_organ_brain>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (initial_brain != null)
        {
            initial_brain.brainmob ??= Dm.@new<mob_living_brain>(initial_brain);
            Waiting.NoSleep(insert_brain(initial_brain));
        }
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:17
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref brain);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:21
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (brain == null)
            icon_state = Dm.text("[]_off", base_icon_state);
        else
            icon_state = Dm.text("[]_brain[]", base_icon_state, brain is obj_item_organ_brain_alien ? "_alien" : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:28
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (brain != null) // doing it like this will make it extremely apparent if something goes wrong
        {
            if (brainmob != null && brainmob.stat <= DEAD)
                Dm.append(ref dot, "mmi_alive");
            else
                Dm.append(ref dot, "mmi_dead");
        }
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:36
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_organ_brain newbrain) //Time to stick a brain in it --NEO
            return NONE;

        if (brain != null)
        {
            balloon_alert(user, I18n.T("[] already full!", p_Theyre()));
            return ITEM_INTERACT_BLOCKING;
        }

        if (newbrain.brainmob == null)
        {
            object install = await tgui_alert(user, I18n.T("[] is inactive, slot it in anyway?", newbrain), I18n.T("Installing Brain"), Dm.list("Yes", "No"));
            if (!Dm.eq(install, "Yes"))
                return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(newbrain, this)))
            return ITEM_INTERACT_BLOCKING;

        await insert_brain(newbrain);
        user.visible_message(
            span_notice(I18n.T("[] sticks \\a [] into []...", user, newbrain, this)),
            span_notice(I18n.T("You stick [] into []...", newbrain, this)),
            span_hear(I18n.T("You hear a wet squelch...")),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );

        if (Dm.truthy(suicided()))
        {
            to_chat(user, span_warning(I18n.T("[]'s indicator light turns red and its brainwave activity alarm beeps harshly.", this)));
            playsound(this, Dm.resource("sound/machines/beep/triple_beep.ogg"), 5, TRUE);
        }
        else if (Dm.truthy(Dm.bitand(newbrain.organ_flags, ORGAN_FAILING))) // the brain is damaged, but not from a suicider
        {
            to_chat(user, span_warning(I18n.T("[]'s indicator light turns yellow and its brain integrity alarm beeps softly.", this)));
            playsound(this, Dm.resource("sound/machines/synth/synth_no.ogg"), 5, TRUE);
        }
        else
        {
            to_chat(user, span_notice(I18n.T("[]'s indicator light turns green!", this)));
            playsound(this, Dm.resource("sound/machines/beep/beep.ogg"), 5, TRUE);
        }

        SSblackbox.record_feedback("amount", "mmis_filled", 1);
        user.log_message(Dm.text("has put the brain of [] into an MMI", key_name(brainmob)), LOG_GAME);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:80
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (brain == null)
        {
            if (radio != null)
            {
                radio.set_on(Dm.not(radio.is_on()));
                string new_state = Dm.truthy(radio.is_on()) ? "on" : "off";
                to_chat(user, span_notice(I18n.T("You switch []'s radio system [].", this, span_bold(new_state))));
                balloon_alert(user, I18n.T("radio turned []", new_state));
            }
            return dot;
        }

        obj_item_organ_brain dumped_brain = await remove_brain() as obj_item_organ_brain;
        object caught_brain = await user.put_in_hands(dumped_brain);
        string fate_of_brain = Dm.truthy(caught_brain)
            ? I18n.T("[] scooping [] into your hand", span_italics(I18n.T("gently")), dumped_brain)
            : I18n.T("spilling [] onto the floor", dumped_brain);

        to_chat(user, span_notice(I18n.T("You unlock and upend [], [].", this, fate_of_brain)));
        if (Dm.truthy(caught_brain))
            balloon_alert(user, I18n.T("scooped up brain"));
        else
            user.balloon_alert_to_viewers(I18n.T("dropped a brain!"), I18n.T("dropped the brain!"));

        if (dumped_brain.brainmob != null)
            user.log_message(Dm.text("has ejected the brain of [] from \\a []", key_name(brainmob), this), LOG_GAME);
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:107
    public virtual async DmTask insert_brain(obj_item_organ_brain new_brain = null)
    {
        if (new_brain is not obj_item_organ_brain)
            throw Dm.CRASH(Dm.text("tried to insert invalid value into []", GetType()), "code/modules/mob/living/brain/processor/organic.dm", 124);

        if (brain != null)
            await remove_brain(); // also clears the brainmob

        brain = new_brain;
        if (new_brain.loc != this)
            await new_brain.forceMove(this);
        new_brain.organ_flags = Dm.bitor(new_brain.organ_flags, ORGAN_FROZEN);
        braintype = brain is obj_item_organ_brain_alien ? "Xenoborg" : "Cyborg";

        set_brainmob(brain.brainmob);
        brain.brainmob = null;
        if (brainmob != null)
        {
            await brainmob.forceMove(this);
            await brainmob.reset_perspective();
            bool fubar_brain = Dm.truthy(brain.suicided) || HAS_TRAIT(brainmob, TRAIT_SUICIDED);
            if (!fubar_brain && !Dm.truthy(Dm.bitand(brain.organ_flags, ORGAN_FAILING)))
                await brainmob.set_stat(STABLE);
            if (Dm.truthy(brainmob.key) && !Dm.truthy(brain.decoy_override))
                brainmob.notify_revival(I18n.T("Your brain was inserted into an MMI!"), source: this);
        }

        update_appearance();
    }

    /// Removes the currently inserted brain from the MMI.
    // tg: code/modules/mob/living/brain/processor/organic.dm:136
    public virtual async DmTask<object> remove_brain(atom destination = null)
    {
        destination ??= drop_location() as atom;
        if (brain == null)
            return null;

        if (brainmob != null)
        {
            mob_living_brain old_brainmob = brainmob;
            set_brainmob(null);
            brain.brainmob = old_brainmob;
            await old_brainmob.forceMove(brain); //Throw mob into brain.
            await old_brainmob.set_stat(DEAD);
            await old_brainmob.reset_perspective(); //so the brainmob follows the brain organ instead of the mmi. And to update our vision
        }

        await brain.forceMove(destination);
        brain.organ_flags = Dm.bitand(brain.organ_flags, Dm.bitnot(ORGAN_FROZEN));
        object dot = brain;
        brain = null;

        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:157
    public override async DmTask transfer_identity(mob_living transferred_user = null) //Same deal as the regular brain proc. Used for human-->robot people.
    {
        obj_item_organ_brain new_brain = (transferred_user as mob_living_carbon)?.get_organ_by_type(typeof(obj_item_organ_brain)) as obj_item_organ_brain;
        if (new_brain == null)
        {
            new_brain = Dm.@new<obj_item_organ_brain>(this);
            await new_brain.transfer_identity(transferred_user);
        }
        else
        {
            await new_brain.Remove(transferred_user as mob_living_carbon, special: TRUE);
            await new_brain.forceMove(this);
        }

        await insert_brain(new_brain);
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:168
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        await remove_brain();
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:171
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (brain == null)
        {
            if (radio != null)
                Dm.append(ref dot, span_notice(Dm.truthy(radio.is_on()) ? I18n.T("There is a switch to toggle the radio system off.") : I18n.T("There is a switch to toggle the radio system on.")));
            return dot;
        }

        if ((brainmob == null || brainmob.mind == null) && !Dm.truthy(brain.decoy_override)) // covers suicide and ghosting
            Dm.append(ref dot, span_danger(I18n.T("[]'s indicator light glows a grim red.", this)));
        else if (Dm.truthy(Dm.bitand(brain.organ_flags, ORGAN_FAILING)) || (brainmob != null && brainmob.stat >= DEAD))
            Dm.append(ref dot, span_warning(I18n.T("[]'s indicator light glows yellow.", this)));
        else if (brainmob.client == null && !Dm.truthy(brain.decoy_override))
            Dm.append(ref dot, span_notice(I18n.T("[]'s indicator light slowly pulses green...", this)));
        else
            Dm.append(ref dot, span_nicegreen(I18n.T("[]'s indicator light glows green!", this)));
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:187
    public override object brain_check(mob user = null)
    {
        object dot = base.brain_check(user);
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(brain?.decoy_override))
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("This [] does not seem to fit!", name)));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(brain?.organ_flags, ORGAN_FAILING)))
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("\\The [] indicates that the brain is damaged!", this)));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:201
    public override object suicided()
    {
        return Dm.truthy(brain.suicided) || HAS_TRAIT(brainmob, TRAIT_SUICIDED) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:204
    public override void set_name(object new_name = null)
    {
        base.set_name(new_name);
        brain.name = Dm.text("[]'s brain", new_name);
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:208
    public override void set_suicide(object suicided = null)
    {
        base.set_suicide(suicided);
        brain.suicided = Dm.num(suicided);
    }
}

// tg: code/modules/mob/living/brain/processor/organic.dm:212
public partial class obj_item_brain_processor_organic_syndie : obj_item_brain_processor_organic
{
    // tg: code/modules/mob/living/brain/processor/organic.dm:217
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        laws = Dm.@new<datum_ai_laws_syndicate_override>();
        radio.set_on(FALSE);
        return dot;
    }

    // tg: code/modules/mob/living/brain/processor/organic.dm:222
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("If used to create a cyborg, it will be unlinked from the station's AI. The lawset cannot be modified until it is synced to a module rack or an AI.")));
        Dm.append(ref dot, span_notice(I18n.T("If used to create an AI, it will not automatically sync to a module rack. The lawset cannot be modified until it is synced to a module rack.")));
        return dot;
    }
}
