// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/antenna.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the antenna, an internal radio implant, and the mind reader, who reads a mind's recent speech,
// intent and identity, and the thoughts of whoever it examines. The var blocks are generated (D111).

// tg: code/datums/mutations/antenna.dm:1
public partial class datum_mutation_antenna : datum_mutation
{
    // tg: code/datums/mutations/antenna.dm:23
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_implant_radio_antenna linked_radio = Dm.@new<obj_item_implant_radio_antenna>(owner);
        await linked_radio.implant(owner, null, TRUE, TRUE);
        radio_weakref = WEAKREF(linked_radio) as datum_weakref;
        return dot;
    }

    // tg: code/datums/mutations/antenna.dm:31
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        obj_item_implant_radio_antenna linked_radio = radio_weakref.resolve() as obj_item_implant_radio_antenna;
        if (linked_radio != null)
            QDEL_NULL(ref linked_radio);
        return dot;
    }
}

// tg: code/datums/mutations/antenna.dm:13
public partial class obj_item_implant_radio_antenna : obj_item_implant_radio
{
    // tg: code/datums/mutations/antenna.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        radio.name = "internal antenna";
        return dot;
    }
}

// tg: code/datums/mutations/antenna.dm:51
public partial class datum_action_cooldown_spell_pointed_mindread : datum_action_cooldown_spell_pointed
{
    // tg: code/datums/mutations/antenna.dm:62
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        if (owner == null)
            return dot;
        ADD_TRAIT(grant_to, TRAIT_MIND_READER, GENETIC_MUTATION);
        RegisterSignal(grant_to, COMSIG_MOB_EXAMINATE, nameof(on_examining));
        return dot;
    }

    // tg: code/datums/mutations/antenna.dm:69
    public override async DmTask Remove(mob remove_from = null)
    {
        await base.Remove(remove_from);
        REMOVE_TRAIT(remove_from, TRAIT_MIND_READER, GENETIC_MUTATION);
        UnregisterSignal(remove_from, COMSIG_MOB_EXAMINATE);
    }

    // tg: code/datums/mutations/antenna.dm:74
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        await DmTask.CompletedTask;
        if (!isliving(cast_on))
            return FALSE;
        mob_living living_cast_on = cast_on as mob_living;
        if (living_cast_on.mind == null)
        {
            to_chat(owner, span_warning(I18n.T("[] has no mind to read!", cast_on)));
            return FALSE;
        }
        if (living_cast_on.stat == DEAD)
        {
            to_chat(owner, span_warning(I18n.T("[] is dead!", cast_on)));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(living_cast_on.mob_biotypes, MOB_ROBOTIC)))
        {
            to_chat(owner, span_warning(I18n.T("[] is robotic, you can't read [] mind!", cast_on, cast_on.p_their())));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/datums/mutations/antenna.dm:90
    public override async DmTask<object> cast(atom target = null)
    {
        mob_living cast_on = target as mob_living;
        object dot = await base.cast(target);
        if (Dm.truthy(cast_on.can_block_magic(antimagic_flags, charge_cost: 0)))
        {
            to_chat(owner, span_warning(I18n.T("As you reach into []'s mind, you are stopped by a mental blockage. It seems you've been foiled.", cast_on)));
            return dot;
        }

        if (cast_on == owner)
        {
            to_chat(owner, span_warning(I18n.T("You plunge into your mind... Yep, it's your mind.")));
            return dot;
        }

        if (Dm.truthy(cast_on.has_status_effect(typeof(datum_status_effect_heretic_passive_moon))))
        {
            to_chat(owner, span_hypnophrase(span_bolddanger(I18n.T("YOU SEEK THE TRUTH? I WILL SHOW YOU EVERYTHING."))));
            if (isliving(owner))
            {
                mob_living reader = owner as mob_living;
                await reader.apply_status_effect(typeof(datum_status_effect_moon_converted));
            }
            return dot;
        }

        if (HAS_TRAIT(cast_on, TRAIT_EVIL))
        {
            to_chat(owner, span_warning(I18n.T("As you reach into []'s mind, you feel the overwhelming emptiness within. A truly evil being. []", cast_on,
                HAS_TRAIT(owner, TRAIT_EVIL) ? I18n.T("It's nice to find someone who is like-minded.") : I18n.T("What is wrong with this person?"))));
        }

        DmList log_info = Dm.list();
        DmList discovered_info = Dm.list(I18n.T("<i>You plunge into []'s mind and discover...</i>", cast_on));
        if (prob(20))
        {
            // chance to alert the read-ee
            to_chat(cast_on, span_danger(I18n.T("You feel something foreign enter your mind.")));
            Dm.append(ref log_info, "Target alerted!");
        }

        DmList recent_speech = cast_on.copy_recent_speech(copy_amount: 3, line_chance: 50);
        if (Dm.truthy(length(recent_speech)))
        {
            Dm.append(ref discovered_info, I18n.T("...Drifting memories of past conversations:"));
            DmList speech_block = Dm.list();
            foreach (object spoken_memory in Dm.each(recent_speech))
            {
                Dm.append(ref speech_block, Dm.text("&emsp;\"[]\"...", spoken_memory));
                Dm.append(ref log_info, Dm.text("Recent speech: \"[]\"", spoken_memory));
            }
            Dm.append(ref discovered_info, jointext(speech_block, "<br>"));
        }

        if (iscarbon(cast_on))
        {
            mob_living_carbon carbon_cast_on = cast_on as mob_living_carbon;
            Dm.append(ref discovered_info, I18n.T("...Intent to <b>[]</b>.", Dm.truthy(carbon_cast_on.combat_mode) ? I18n.T("harm") : I18n.T("help")));
            Dm.append(ref discovered_info, I18n.T("...True identity of <b>[]</b>.", carbon_cast_on.mind.name));
            Dm.append(ref log_info, Dm.text("Intent: \"[]\"", Dm.truthy(carbon_cast_on.combat_mode) ? "harm" : "help"));
            Dm.append(ref log_info, Dm.text("Identity: \"[]\"", carbon_cast_on.mind.name));
        }

        to_chat(owner, boxed_message(span_notice(jointext(discovered_info, "<br>"))));
        log_combat(owner, cast_on, "mind read (cast intentionally)", null, Dm.text("info: []", english_list(log_info, and_text: ", ")));
        return dot;
    }

    // tg: code/datums/mutations/antenna.dm:139
    [SignalHandler]
    public virtual void on_examining(mob examiner = null, atom examining = null)
    {
        if (!isliving(examining) || examiner == examining)
            return;

        Waiting.Detach(read_mind(examiner as mob_living, examining as mob_living)); // INVOKE_ASYNC
    }

    // tg: code/datums/mutations/antenna.dm:146
    public virtual async DmTask read_mind(mob_living examiner = null, mob_living examined = null)
    {
        if (IS_UNCONSCIOUS(examined) || examined.mind == null || Dm.truthy(Dm.bitand(examined.mob_biotypes, MOB_ROBOTIC)))
            return;

        object antimagic = examined.can_block_magic(antimagic_flags, charge_cost: 0);
        object read_text = "";
        if (!Dm.truthy(antimagic))
        {
            read_text = await examined.get_typing_text();
            if (!Dm.truthy(read_text))
                return;
        }

        await Dm.sleep(0.5f * SECONDS); // small pause so it comes after all examine text and effects
        if (QDELETED(examiner))
            return;
        if (Dm.truthy(antimagic))
        {
            to_chat(examiner, boxed_message(span_warning(I18n.T("You attempt to analyze []'s current thoughts, but fail to penetrate [] mind - It seems you've been foiled.", examined, examined.p_their()))));
            return;
        }

        DmList log_info = Dm.list();
        if (prob(10))
        {
            to_chat(examined, span_danger(I18n.T("You feel something foreign enter your mind.")));
            Dm.append(ref log_info, "Target alerted!");
        }

        to_chat(examiner, boxed_message(span_notice(I18n.T("<i>You analyze []'s current thoughts...</i><br>&emsp;\"[]\"...", examined, read_text))));
        Dm.append(ref log_info, Dm.text("Current thought: \"[]\"", read_text));

        log_combat(examiner, examined, "mind read (triggered on examine)", null, Dm.text("info: []", english_list(log_info, and_text: ", ")));
    }
}
