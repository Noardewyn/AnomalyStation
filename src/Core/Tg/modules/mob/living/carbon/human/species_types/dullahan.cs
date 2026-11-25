// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/dullahan.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: the dullahan, whose head is carried in the hands; the relay in the head speaks and hears for the body
// and the Toggle Perspective action moves the owner's view between the head and the body. The var blocks are generated (D111).

// tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:2
public partial class datum_species_dullahan : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:37
    public override object check_roundstart_eligible()
    {
        if (Dm.truthy(check_holidays(HALLOWEEN)))
            return TRUE;
        return base.check_roundstart_eligible();
    }

    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:42
    public override async DmTask on_species_gain(mob_living_carbon_human human = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(human, old_species, pref_load, regenerate_icons, replace_missing);
        human.lose_hearing_sensitivity(TRAIT_GENERIC);
        RegisterSignal(human, COMSIG_CARBON_ATTACH_LIMB, nameof(on_gained_part));
        RegisterSignal(human, COMSIG_CARBON_DEFIB_BRAIN_CHECK, nameof(defib_check));

        obj_item_bodypart_head head = human.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        head.speech_span = null;
        if (human.drop_location() == null)
            return;
        if (head != null)
            await head.drop_limb();
        if (QDELETED(head)) //drop_limb() deletes the limb if no drop location exists and character setup dummies are located in nullspace.
            return;
        my_head = Dm.@new<obj_item_dullahan_relay>(head, human) as obj_item_dullahan_relay;
        await human.put_in_hands(head);

        // We want to give the head some boring old eyes just so it doesn't look too jank on the head sprite.
        obj_item_organ_eyes eyes = Dm.@new<obj_item_organ_eyes>(head) as obj_item_organ_eyes;
        eyes.eye_color_left = human.eye_color_left;
        eyes.eye_color_right = human.eye_color_right;
        await eyes.bodypart_insert(head);
        await head.update_limb();
        await head.update_icon_dropped();
        RegisterSignal(head, COMSIG_QDELETING, nameof(on_head_destroyed));
        RegisterSignal(my_head, COMSIG_MOVABLE_MOVED, nameof(on_relay_move));
    }

    /// If we gained a new body part, it had better not be a head
    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:69
    [SignalHandler]
    public virtual void on_gained_part(mob_living_carbon_human dullahan = null, obj_item_bodypart part = null)
    {
        if (!Dm.eq(part.body_zone, BODY_ZONE_HEAD))
            return;
        if (dullahan.drop_location() == null)
            return; // don't gib nullspace
        my_head = null;
        dullahan.investigate_log(Dm.text("has been gibbed by having an illegal head put on [] shoulders.", dullahan.p_their()), INVESTIGATE_DEATHS);
        Waiting.NoSleep(dullahan.gib(DROP_ALL_REMAINS)); // Yeah so giving them a head on their body is really not a good idea, so their original head will remain but uh, good luck fixing it after that.
    }

    /// If our head is destroyed, so are we
    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:80
    [SignalHandler]
    public virtual void on_head_destroyed()
    {
        mob_living human = my_head?.owner;
        if (QDELETED(human))
            return; // guess we already died
        my_head = null;
        human.investigate_log(Dm.text("has been gibbed by the loss of [] head.", human.p_their()), INVESTIGATE_DEATHS);
        Waiting.NoSleep(human.gib(DROP_ALL_REMAINS));
    }

    /// Head was butchered? No more dullahan
    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:90
    [SignalHandler]
    public virtual void on_relay_move()
    {
        if (QDELETED(my_head?.owner) || !isdullahan(my_head?.owner))
            return;
        Waiting.NoSleep(my_head.owner.gib(DROP_ALL_REMAINS));
        QDEL_NULL(ref my_head);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:97
    [SignalHandler]
    public virtual object defib_check(mob_living_carbon_human human = null)
    {
        object defib_result = human.can_defib_brain(Dm.locate<obj_item_organ_brain>(my_head.loc));
        return Dm.truthy(defib_result) ? defib_result : DEFIB_POSSIBLE;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:101
    public override async DmTask on_species_loss(mob_living_carbon_human human = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        await base.on_species_loss(human, new_species, pref_load, __extra);
        if (my_head != null)
        {
            obj_item_bodypart_head detached_head = my_head.loc as obj_item_bodypart_head;
            UnregisterSignal(detached_head, COMSIG_QDELETING);
            my_head.owner = null;
            QDEL_NULL(ref my_head);
            if (detached_head != null)
                qdel(detached_head);
        }

        UnregisterSignal(human, COMSIG_CARBON_ATTACH_LIMB);
        UnregisterSignal(human, COMSIG_CARBON_DEFIB_BRAIN_CHECK);
        await human.regenerate_limb(BODY_ZONE_HEAD, Dm.list()); // tg passes FALSE: no scar list (null would take the default copy)
        human.become_hearing_sensitive();
        prevent_perspective_change = FALSE;
        await human.reset_perspective(human);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:118
    public virtual async DmTask update_vision_perspective(mob_living_carbon_human human = null)
    {
        obj_item_organ_eyes eyes = human.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes != null)
        {
            await human.update_tint();
            if (Dm.truthy(eyes.tint))
            {
                prevent_perspective_change = FALSE;
                await human.reset_perspective(human, TRUE);
            }
            else
            {
                await human.reset_perspective(my_head, TRUE);
                prevent_perspective_change = TRUE;
            }
        }
    }

    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:129
    public override async DmTask on_owner_login(mob_living_carbon_human owner = null)
    {
        obj_item_organ_eyes eyes = owner.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (Dm.truthy(owner_first_client_connection_handled))
        {
            if (!Dm.truthy(eyes.tint))
            {
                await owner.reset_perspective(my_head, TRUE);
                prevent_perspective_change = TRUE;
            }
            return;
        }

        // As it's the first time there's a client in our mob, we can finally update its vision to place it in the head instead!
        datum_action_item_action_organ_action_dullahan eyes_toggle_perspective_action = eyes != null ? Dm.locate<datum_action_item_action_organ_action_dullahan>(eyes.actions) : null;

        if (eyes_toggle_perspective_action != null)
            await eyes_toggle_perspective_action.Trigger();
        owner_first_client_connection_handled = TRUE;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:143
    public override object get_physical_attributes()
    {
        return I18n.T("A dullahan is much like a human, but their head is detached from their body and must be carried around.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:146
    public override object get_species_description()
    {
        return I18n.T("An angry spirit, hanging onto the land of the living for unfinished business. Or that's what the books say. They're quite nice when you get to know them.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:151
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("\"No wonder they're all so grumpy! Their hands are always full! I used to think, \"Wouldn't this be cool?\" but after watching these creatures suffer from their head getting dunked down disposals for the nth time, I think I'm good.\" - Captain Larry Dodd"));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:158
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "horse-head"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Headless and Horseless")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Dullahans must lug their head around in their arms. While many creative uses can come out of your head being independent of your body, Dullahans will find it mostly a pain.")))));

        return to_add;
    }

    // There isn't a "Minor Undead" biotype, so we have to explain it in an override (see: vampires)
    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:173
    public override object create_pref_biotypes_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "skull"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Minor Undead")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are minor undead. Minor undead enjoy some of the perks of being dead, like not needing to breathe or eat, but do not get many of the environmental immunities involved with being fully undead.", name)))));

        return to_add;
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:193
public partial class obj_item_organ_tongue_dullahan : obj_item_organ_tongue
{
    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:197
    public override void handle_speech(datum source = null, DmList speech_args = null)
    {
        if (ishuman(owner))
        {
            mob_living_carbon_human human = (mob_living_carbon_human)owner;
            if (isdullahan(human))
            {
                datum_species_dullahan dullahan_species = (datum_species_dullahan)human.dna.species;
                if (isobj(dullahan_species.my_head.loc))
                {
                    obj head = (obj)dullahan_species.my_head.loc;
                    DmList mods = (DmList)speech_args[SPEECH_MODS];
                    if (Dm.truthy(mods[WHISPER_MODE])) // whisper away
                        Dm.combine((DmList)speech_args[SPEECH_SPANS], SPAN_ITALICS);
                    Waiting.Detach(head.say(speech_args[SPEECH_MESSAGE], spans: speech_args[SPEECH_SPANS] as DmList, sanitize: FALSE, language: speech_args[SPEECH_LANGUAGE] as datum_language, message_range: Dm.num(speech_args[SPEECH_RANGE]), message_mods: mods));
                }
            }
        }
        speech_args[SPEECH_MESSAGE] = "";
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:224
public partial class datum_action_item_action_organ_action_dullahan : datum_action_item_action_organ_action
{
    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:227
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        obj_item_organ_eyes_dullahan dullahan_eyes = (obj_item_organ_eyes_dullahan)target;
        dullahan_eyes.tint = Dm.truthy(dullahan_eyes.tint) ? NONE : INFINITY;
        if (!isdullahan(owner))
            return FALSE;
        mob_living_carbon_human human = (mob_living_carbon_human)owner;
        datum_species_dullahan dullahan_species = (datum_species_dullahan)human.dna.species;
        await dullahan_species.update_vision_perspective(human);
        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:239
public partial class obj_item_dullahan_relay : obj_item
{
    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:243
    [DmParams("mapload", "new_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living_carbon_human new_owner = Dm.arg(__extra, 0) as mob_living_carbon_human;
        object dot = base.Initialize(mapload);
        if (new_owner == null)
            return INITIALIZE_HINT_QDEL;
        obj_item_bodypart_head detached_head = loc as obj_item_bodypart_head;
        if (detached_head == null)
            return INITIALIZE_HINT_QDEL;
        owner = new_owner;
        START_PROCESSING(SSobj, this);
        RegisterSignal(owner, COMSIG_CARBON_REGENERATE_LIMBS, nameof(unlist_head));
        RegisterSignal(owner, COMSIG_LIVING_REVIVE, nameof(retrieve_head));
        RegisterSignal(owner, COMSIG_HUMAN_PREFS_APPLIED, nameof(on_prefs_loaded));
        become_hearing_sensitive(ROUNDSTART_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:257
    public override object Destroy(float? force = null)
    {
        lose_hearing_sensitivity(ROUNDSTART_TRAIT);
        owner = null;
        return base.Destroy(force);
    }

    /// Updates our names after applying name prefs
    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:263
    [SignalHandler]
    public virtual void on_prefs_loaded(mob_living_carbon_human headless = null)
    {
        obj_item_bodypart_head detached_head = loc as obj_item_bodypart_head;
        if (detached_head == null)
            return; // It's so over
        detached_head.real_name = Dm.str(headless.real_name);
        name = Dm.str(headless.real_name);
        detached_head.voice = headless.voice;
        detached_head.pitch = pitch;
        obj_item_organ_brain brain = Dm.locate<obj_item_organ_brain>(detached_head);
        brain.name = Dm.text("[]'s brain", headless.name);

        detached_head.copy_appearance_from(headless, TRUE);
        Waiting.NoSleep(detached_head.update_icon_dropped());
    }

    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:278
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (owner == null)
            return FALSE;

        float dist = Dm.num(get_dist(speaker, this)) - Dm.num(message_range);
        if (!Dm.eq(message_range, INFINITY))
        {
            if (dist > EAVESDROP_RANGE)
                return FALSE;
            if (dist > 0)
                raw_message = stars(raw_message);
        }

        return await owner.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, INFINITY);
    }

    ///Stops dullahans from gibbing when regenerating limbs
    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:293
    [SignalHandler]
    public virtual void unlist_head(datum source = null, DmList excluded_zones = null)
    {
        Dm.combine(excluded_zones, BODY_ZONE_HEAD);
    }

    ///Retrieving the owner's head for better ahealing.
    // tg: code/modules/mob/living/carbon/human/species_types/dullahan.dm:298
    [SignalHandler]
    public virtual void retrieve_head(datum source = null, object full_heal_flags = null)
    {
        if (!Dm.truthy(Dm.bitand(full_heal_flags, HEAL_ADMIN)))
            return;

        obj_item_bodypart_head head = loc as obj_item_bodypart_head;
        turf body_turf = get_turf(owner);
        if (head != null && body_turf != null && !Dm.truthy(Dm.@in(head, owner.get_all_contents())))
            Waiting.NoSleep(head.forceMove(body_turf));
    }
}
