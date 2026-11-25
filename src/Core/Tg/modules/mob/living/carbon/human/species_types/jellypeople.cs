// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/jellypeople.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2c (D349): the whole file: the jellyperson, the slimeperson (split body, body swap), the luminescent (glow, the integrated slime
// extract) and the stargazer (telepathy, link minds), with their actions. The luminescent's extract activation calls the extract's
// activate(), whose per-colour effects belong to the slime extracts (docs/ai/specs/live-partial.txt). The var blocks are generated (D111).

public partial class datum_species_jelly
{
    ///The rate at which slimes regenerate their jelly normally
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:2
    private const float JELLY_REGEN_RATE = 1.5f;
    ///The rate at which slimes regenerate their jelly when they completely run out of it and start taking damage, usually after having cannibalized all their limbs already
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:4
    private const float JELLY_REGEN_RATE_EMPTY = 2.5f;
    ///The blood volume at which slimes begin to start losing nutrition -- so that IV drips can work for blood deficient slimes
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:6
    internal const float BLOOD_VOLUME_LOSE_NUTRITION = 550;
}

// tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:8
public partial class datum_species_jelly : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:45
    public override async DmTask on_species_gain(mob_living_carbon_human new_jellyperson = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(new_jellyperson, old_species, pref_load, regenerate_icons, replace_missing);
        if (ishuman(new_jellyperson))
        {
            regenerate_limbs = Dm.@new<datum_action_innate_regenerate_limbs>();
            await regenerate_limbs.Grant(new_jellyperson);
        }
        await new_jellyperson.AddElement(typeof(datum_element_soft_landing));
        RegisterSignal(new_jellyperson, COMSIG_HUMAN_ON_HANDLE_BLOOD, nameof(slime_blood));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:53
    public override async DmTask on_species_loss(mob_living_carbon_human former_jellyperson = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        if (regenerate_limbs != null)
            await regenerate_limbs.Remove(former_jellyperson);
        former_jellyperson.RemoveElement(typeof(datum_element_soft_landing));
        UnregisterSignal(former_jellyperson, COMSIG_HUMAN_ON_HANDLE_BLOOD);
        await base.on_species_loss(former_jellyperson, new_species, pref_load, __extra);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:60
    [SignalHandler]
    public virtual object slime_blood(mob_living_carbon_human slime = null, object seconds_per_tick = null)
    {
        if (slime.stat == DEAD)
            return HANDLE_BLOOD_HANDLED;

        // In the following code, do not cache blood volumes.
        // They are repeadetly updated and caching can introduce bugs.

        // Blood regen thresholds use your real amount of blood.
        if (Dm.num(slime.get_blood_volume()) <= 0)
        {
            slime.adjust_blood_volume(JELLY_REGEN_RATE_EMPTY * Dm.num(GET_PHYSIOLOGY(slime, PHYS_COEFF_BLOOD_REGEN)) * Dm.num(seconds_per_tick));
            Waiting.NoSleep(slime.adjust_brute_loss(2.5f * Dm.num(seconds_per_tick)));
            to_chat(slime, span_danger(I18n.T("You feel empty!")));
        }

        // Same logic applies here.
        if (Dm.num(slime.get_blood_volume()) < BLOOD_VOLUME_NORMAL)
        {
            if (Dm.num(slime.nutrition) >= NUTRITION_LEVEL_STARVING)
            {
                slime.adjust_blood_volume(JELLY_REGEN_RATE * Dm.num(GET_PHYSIOLOGY(slime, PHYS_COEFF_BLOOD_REGEN)) * Dm.num(seconds_per_tick));
                if (Dm.num(slime.get_blood_volume()) <= BLOOD_VOLUME_LOSE_NUTRITION) // don't lose nutrition if we are above a certain threshold, otherwise slimes on IV drips will still lose nutrition
                    Waiting.NoSleep(slime.adjust_nutrition(-1.25f * Dm.num(seconds_per_tick)));
            }
        }

        // If you're on saline, you don't feel the effects of bloodloss.
        if (Dm.num(slime.get_blood_volume(apply_modifiers: TRUE)) < BLOOD_VOLUME_OKAY)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                to_chat(slime, span_danger(I18n.T("You feel drained!")));
        }

        // Saline can prevent you from cannibalizing yourself.
        if (Dm.num(slime.get_blood_volume(apply_modifiers: TRUE)) < BLOOD_VOLUME_BAD)
            cannibalize_body(slime);

        if (regenerate_limbs != null)
            Waiting.NoSleep(regenerate_limbs.build_all_button_icons(UPDATE_BUTTON_STATUS));
        return HANDLE_BLOOD_NO_NUTRITION_DRAIN | HANDLE_BLOOD_NO_OXYLOSS;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:94
    public virtual void cannibalize_body(mob_living_carbon_human target = null)
    {
        DmList limbs_to_consume = Dm.list();

        foreach ((object body_zone, object untyped_limb) in Dm.pairs(target.get_bodyparts_by_zones() as DmList))
        {
            obj_item_bodypart limb = untyped_limb as obj_item_bodypart;
            if (limb == null)
                continue;

            object limb_zone = limb.body_zone;

            if (Dm.truthy(IS_STUMP(limb)) || Dm.eq(limb_zone, BODY_ZONE_CHEST) || Dm.eq(limb_zone, BODY_ZONE_HEAD))
                continue;

            if (Dm.truthy(Dm.bitand(limb.biological_state, BIO_JELLY))) // can only cannibalize limbs that are comprised of jelly (this is a last gasp to keep going, let's assume we can't digest out non-jelly limbs)
                Dm.append(ref limbs_to_consume, limb_zone);
        }

        if (length(limbs_to_consume) == 0)
        {
            target.losebreath++;
            return;
        }

        if (target.num_legs > 0) //Legs go before arms
            Dm.remove(ref limbs_to_consume, Dm.list(BODY_ZONE_R_ARM, BODY_ZONE_L_ARM));

        obj_item_bodypart consumed_limb = target.get_bodypart(Dm.str(pick(limbs_to_consume))) as obj_item_bodypart;
        Waiting.NoSleep(consumed_limb.drop_limb());

        to_chat(target, span_userdanger(I18n.T("Your [] is drawn back into your body, unable to maintain its shape!", consumed_limb.name)));
        qdel(consumed_limb);

        target.adjust_blood_volume(20 * Dm.num(GET_PHYSIOLOGY(target, PHYS_COEFF_BLOOD_REGEN)));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:125
    public override object get_species_description()
    {
        return I18n.T("Jellypeople are a strange and alien species with three eyes, made entirely out of gel.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:128
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Jellypeople are actively being experimented on my Nanotrasen scientists, who are trying to unlock the secrets of their unique biology."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:133
    public override async DmTask prepare_human_for_preview(mob_living_carbon_human human = null)
    {
        human.dna.features[FEATURE_MUTANT_COLOR] = COLOR_PINK;
        human.hairstyle = "Bob Hair 2";
        human.hair_color = COLOR_PINK;
        await human.update_body(TRUE); // update_body(is_creating = TRUE)
    }

    // Unique handling for slime blood here, it's got some unique properties that warrant a more detailed desc.
    // They may not be roundstart but in the unlikely event they become one might as well not leave a glaring issue open.
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:141
    public override object create_pref_blood_perks()
    {
        DmList to_add = Dm.list();
        datum_blood_type blood_type = get_blood_type(exotic_bloodtype) as datum_blood_type;

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
            Dm.kv(SPECIES_PERK_ICON, "tint"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Jelly Blood")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("[] don't have blood, but instead have toxic []! Jelly is extremely important, as losing it will cause you to lose limbs. Having low jelly will make medical treatment very difficult.", plural_form, Dm.initial(blood_type.reagent_type, "name"))))));

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:155
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_PERSON_RUNNING),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Jelly Body")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("[] have bodies of malleable Jelly. You can squeeze through small cracks like grilles, provided you're not wearing clothes.", plural_form)))));

        return to_add;
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:168
public partial class datum_action_innate_regenerate_limbs : datum_action_innate
{
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:178
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon_human H = owner as mob_living_carbon_human;
        DmList limbs_to_heal = H.get_missing_limbs() as DmList;
        if (length(limbs_to_heal) == 0)
            return FALSE;
        if (Dm.num(H.get_blood_volume()) >= BLOOD_VOLUME_OKAY + blood_per_limb)
            return TRUE;
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:189
    public override async DmTask<object> Activate()
    {
        mob_living_carbon_human H = owner as mob_living_carbon_human;
        DmList limbs_to_heal = H.get_missing_limbs() as DmList;
        if (length(limbs_to_heal) == 0)
        {
            to_chat(H, span_notice(I18n.T("You feel intact enough as it is.")));
            return null;
        }
        to_chat(H, span_notice(I18n.T("You focus intently on your missing []...", length(limbs_to_heal) >= 2 ? I18n.T("limbs") : I18n.T("limb"))));
        if (Dm.num(H.get_blood_volume()) >= blood_per_limb * length(limbs_to_heal) + BLOOD_VOLUME_OKAY)
        {
            await H.regenerate_limbs();
            H.adjust_blood_volume(-blood_per_limb * length(limbs_to_heal));
            to_chat(H, span_notice(I18n.T("...and after a moment you finish reforming!")));
            return null;
        }
        else if (Dm.num(H.get_blood_volume()) >= blood_per_limb)//We can partially heal some limbs
        {
            while (Dm.num(H.get_blood_volume()) >= BLOOD_VOLUME_OKAY + blood_per_limb)
            {
                object healed_limb = pick(limbs_to_heal);
                await H.regenerate_limb(healed_limb);
                Dm.remove(ref limbs_to_heal, healed_limb);
                H.adjust_blood_volume(-blood_per_limb);
            }
            to_chat(H, span_warning(I18n.T("...but there is not enough of you to fix everything! You must attain more mass to heal completely!")));
            return null;
        }
        to_chat(H, span_warning(I18n.T("...but there is not enough of you to go around! You must attain more mass to heal!")));
        return null;
    }
}

////////////////////////////////////////////////////////SLIMEPEOPLE///////////////////////////////////////////////////////////////////

//Slime people are able to split like slimes, retaining a single mind that can swap between bodies at will, even after death.

// tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:215
public partial class datum_species_jelly_slime : datum_species_jelly
{
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:234
    public override object get_physical_attributes()
    {
        return I18n.T("Slimepeople have jelly for blood and their vacuoles can extremely quickly convert plasma to it if they're breathing it in. They can then use the excess blood to split off an excess body, which their consciousness can transfer to at will or on death. Most things that are toxic heal them, but most things that prevent toxicity damage them!");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:239
    [DmParams("C")]
    public override async DmTask on_species_loss(mob_living_carbon_human C = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        if (slime_split != null)
            await slime_split.Remove(C);
        if (swap_body != null)
            await swap_body.Remove(C);
        Dm.remove(ref bodies, C); // This means that the other bodies maintain a link
        // so if someone mindswapped into them, they'd still be shared.
        bodies = null;
        C.set_blood_volume(C.get_blood_volume(), maximum: BLOOD_VOLUME_NORMAL);
        UnregisterSignal(C, Dm.list(
            COMSIG_LIVING_DEATH,
            COMSIG_LIVING_LIFE));
        await base.on_species_loss(C, new_species, pref_load, __extra);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:254
    [DmParams("C", "old_species", "pref_load", "regenerate_icons")]
    public override async DmTask on_species_gain(mob_living_carbon_human C = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(C, old_species, pref_load, regenerate_icons, replace_missing);
        if (ishuman(C))
        {
            slime_split = Dm.@new<datum_action_innate_split_body>();
            await slime_split.Grant(C);
            swap_body = Dm.@new<datum_action_innate_swap_body>();
            await swap_body.Grant(C);

            if (bodies == null || length(bodies) == 0)
                bodies = Dm.list(C);
            else
                Dm.combine(ref bodies, C);
        }

        RegisterSignal(C, COMSIG_LIVING_DEATH, nameof(on_death_move_body));
        RegisterSignal(C, COMSIG_LIVING_LIFE, nameof(on_life));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:270
    [SignalHandler]
    public virtual void on_death_move_body(mob_living_carbon_human source = null, object gibbed = null)
    {
        if (slime_split == null)
            return;
        if (!Dm.truthy(source.mind?.active))
            return;

        DmList available_bodies = Dm.sub(bodies, source) as DmList;
        foreach (mob_living_carbon_human other_body in Dm.each_cast<mob_living_carbon_human>(available_bodies))
        {
            if (!Dm.truthy(Waiting.NoSleep(swap_body.can_swap(other_body))))
                Dm.remove(ref available_bodies, other_body);
        }

        if (length(available_bodies) == 0)
            return;

        Waiting.NoSleep(swap_body.swap_to_dupe(source.mind, pick(available_bodies) as mob_living_carbon_human));
    }

    //If you're cloned you get your body pool back
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:289
    public override void copy_properties_from(datum_species old_species = null, object pref_load = null, object regenerate_icons = null)
    {
        bodies = (old_species as datum_species_jelly_slime).bodies;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:292
    [SignalHandler]
    public virtual void on_life(mob_living_carbon_human source = null, object seconds_per_tick = null)
    {
        if (Dm.num(source.get_blood_volume()) >= BLOOD_VOLUME_SLIME_SPLIT)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                to_chat(source, span_notice(I18n.T("You feel very bloated!")));
        }

        else if (Dm.num(source.nutrition) >= NUTRITION_LEVEL_WELL_FED)
        {
            source.adjust_blood_volume(1.5f * Dm.num(seconds_per_tick));
            if (Dm.num(source.get_blood_volume()) <= BLOOD_VOLUME_LOSE_NUTRITION)
                Waiting.NoSleep(source.adjust_nutrition(-1.25f * Dm.num(seconds_per_tick)));
        }
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:303
public partial class datum_action_innate_split_body : datum_action_innate
{
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:311
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon_human H = owner as mob_living_carbon_human;
        if (Dm.num(H.get_blood_volume()) >= BLOOD_VOLUME_SLIME_SPLIT)
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:320
    public override async DmTask<object> Activate()
    {
        mob_living_carbon_human H = owner as mob_living_carbon_human;
        if (!Dm.truthy(is_species(H, typeof(datum_species_jelly_slime))))
            return null;
        // CHECK_DNA_AND_SPECIES(H) (__DEFINES/DNA.dm:3, a statement macro that returns)
        if (!Dm.truthy(H.dna?.species))
            return null;
        H.visible_message(
            span_notice(I18n.T("[] gains a look of concentration while standing perfectly still.", owner)),
            span_notice(I18n.T("You focus intently on moving your body while standing perfectly still...")));

        ADD_TRAIT(this, TRAIT_NO_TRANSFORM, REF(this));

        if (Dm.truthy(await do_after(owner, delay: 6 * SECONDS, target: owner, timed_action_flags: IGNORE_HELD_ITEM)))
        {
            if (Dm.num(H.get_blood_volume()) >= BLOOD_VOLUME_SLIME_SPLIT)
                await make_dupe();
            else
                to_chat(H, span_warning(I18n.T("...but there is not enough of you to go around! You must attain more mass to split!")));
        }
        else
            to_chat(H, span_warning(I18n.T("...but fail to stand perfectly still!")));

        REMOVE_TRAIT(this, TRAIT_NO_TRANSFORM, REF(this));
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:342
    public virtual async DmTask make_dupe()
    {
        mob_living_carbon_human H = owner as mob_living_carbon_human;
        // CHECK_DNA_AND_SPECIES(H) (__DEFINES/DNA.dm:3, a statement macro that returns)
        if (!Dm.truthy(H.dna?.species))
            return;

        mob_living_carbon_human spare = Dm.@new<mob_living_carbon_human>(H.loc);

        spare.underwear = "Nude";
        await H.dna.copy_dna(spare.dna, COPY_DNA_SE | COPY_DNA_SPECIES | COPY_DNA_MUTATIONS);
        spare.dna.features[FEATURE_MUTANT_COLOR] = Dm.text("#[][][]", pick("7F", "FF"), pick("7F", "FF"), pick("7F", "FF"));
        spare.dna.update_uf_block(typeof(datum_dna_block_feature_mutant_color));
        spare.real_name = spare.dna.real_name;
        spare.name = Dm.str(spare.dna.real_name);
        await spare.updateappearance(mutcolor_update: 1);
        await spare.Move(get_step(H.loc, Dm.num(pick(NORTH, SOUTH, EAST, WEST))));

        H.set_blood_volume(Dm.num(H.get_blood_volume()) * 0.45f);
        REMOVE_TRAIT(H, TRAIT_NO_TRANSFORM, REF(this));

        datum_species_jelly_slime origin_datum = H.dna.species as datum_species_jelly_slime;
        Dm.combine(ref origin_datum.bodies, spare);

        datum_species_jelly_slime spare_datum = spare.dna.species as datum_species_jelly_slime;
        spare_datum.bodies = origin_datum.bodies;

        await H.transfer_quirk_datums(spare);
        await H.mind.transfer_to(spare);
        spare.visible_message(
            span_warning(I18n.T("[] distorts as a new body \"steps out\" of [].", H, H.p_them())),
            span_notice(I18n.T("...and after a moment of disorentation, you're besides yourself!")));
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:374
public partial class datum_action_innate_swap_body : datum_action_innate
{
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:382
    public override async DmTask<object> Activate()
    {
        if (!Dm.truthy(is_species(owner, typeof(datum_species_jelly_slime))))
        {
            to_chat(owner, span_warning(I18n.T("You are not a slimeperson.")));
            await Remove(owner);
        }
        else
            await ui_interact(owner);
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:389
    public override object ui_host(mob user = null)
    {
        return owner;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:392
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:395
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "SlimeBodySwapper", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:401
    public override async DmTask<object> ui_data(mob user = null)
    {
        mob_living_carbon_human H = owner as mob_living_carbon_human;
        if (!Dm.truthy(is_species(H, typeof(datum_species_jelly_slime))))
            return null;

        datum_species_jelly_slime SS = H.dna.species as datum_species_jelly_slime;

        DmList data = Dm.list();
        data["bodies"] = Dm.list();
        foreach (object b in Dm.each(SS.bodies.Copy()))
        {
            mob_living_carbon_human body = b as mob_living_carbon_human;
            if (body == null || QDELETED(body) || !Dm.truthy(is_species(body, typeof(datum_species_jelly_slime))))
            {
                Dm.remove(ref SS.bodies, b);
                continue;
            }

            DmList L = Dm.list();
            L["htmlcolor"] = body.dna.features[FEATURE_MUTANT_COLOR];
            L["area"] = get_area_name(body, TRUE);
            string stat = "error";
            object sw__1 = body.stat;
            if (Dm.eq(sw__1, STABLE))
                stat = I18n.T("Stable");
            else if (Dm.eq(sw__1, SOFT_CRIT) || Dm.eq(sw__1, HARD_CRIT))
                stat = I18n.T("Critical");
            else if (Dm.eq(sw__1, DEAD))
                stat = I18n.T("Dead");
            string occupied;
            if (body == H)
                occupied = "owner";
            else if (body.mind != null && Dm.truthy(body.mind.active))
                occupied = "stranger";
            else
                occupied = "available";

            L["status"] = stat;
            L["exoticblood"] = body.get_blood_volume();
            L["name"] = body.name;
            L["ref"] = Dm.text("[]", REF(body));
            L["occupied"] = occupied;
            object button;
            if (occupied == "owner")
                button = "selected";
            else if (occupied == "stranger")
                button = "danger";
            else if (Dm.truthy(await can_swap(body)))
                button = null;
            else
                button = "disabled";

            L["swap_button_state"] = button;
            L["swappable"] = occupied == "available" && Dm.truthy(await can_swap(body)) ? TRUE : FALSE;

            DmList bodies_data = data["bodies"] as DmList;
            Dm.append(ref bodies_data, Dm.list(L));
        }

        return data;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:457
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob_living_carbon_human H = owner as mob_living_carbon_human;
        if (!Dm.truthy(is_species(owner, typeof(datum_species_jelly_slime))))
            return null;
        if (H.mind == null || !Dm.truthy(H.mind.active))
            return null;
        if (Dm.eq(action, "swap"))
        {
            datum_species_jelly_slime SS = H.dna.species as datum_species_jelly_slime;
            object located = Dm.locate_ref(@params["ref"]);
            mob_living_carbon_human selected = located != null && Dm.@in(located, SS.bodies) ? located as mob_living_carbon_human : null;
            if (!Dm.truthy(await can_swap(selected)))
                return null;
            SStgui.close_uis(this);
            await swap_to_dupe(H.mind, selected);
        }
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:475
    public virtual async DmTask<object> can_swap(mob_living_carbon_human dupe = null)
    {
        mob_living_carbon_human H = owner as mob_living_carbon_human;
        if (!Dm.truthy(is_species(H, typeof(datum_species_jelly_slime))))
            return FALSE;
        datum_species_jelly_slime SS = H.dna.species as datum_species_jelly_slime;

        if (QDELETED(dupe)) //Is there a body?
        {
            Dm.remove(ref SS.bodies, dupe);
            return FALSE;
        }

        if (!Dm.truthy(is_species(dupe, typeof(datum_species_jelly_slime)))) //Is it a slimeperson?
        {
            Dm.remove(ref SS.bodies, dupe);
            return FALSE;
        }

        if (dupe.stat == DEAD) //Is it alive?
            return FALSE;

        if (IS_UNCONSCIOUS_OR_CRIT(dupe)) //Is it awake?
            return FALSE;

        if (dupe.mind != null && Dm.truthy(dupe.mind.active)) //Is it unoccupied?
            return FALSE;

        if (!Dm.@in(dupe, SS.bodies)) //Do we actually own it?
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:503
    public virtual async DmTask swap_to_dupe(datum_mind M = null, mob_living_carbon_human dupe = null)
    {
        if (!Dm.truthy(await can_swap(dupe))) //sanity check
            return;
        if (!IS_UNCONSCIOUS_OR_CRIT(M.current))
        {
            M.current.visible_message(span_notice(I18n.T("[] stops moving and starts staring vacantly into space.", M.current)),
                span_notice(I18n.T("You stop moving this body...")));
        }
        else
            to_chat(M.current, span_notice(I18n.T("You abandon this body...")));
        await (M.current as mob_living).transfer_quirk_datums(dupe);
        await M.transfer_to(dupe);
        dupe.visible_message(span_notice(I18n.T("[] blinks and looks around.", dupe)), span_notice(I18n.T("...and move this one instead.")));
    }
}

///////////////////////////////////LUMINESCENTS//////////////////////////////////////////

//Luminescents are able to consume and use slime extracts, without them decaying.

// tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:520
public partial class datum_species_jelly_luminescent : datum_species_jelly
{
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:545
    public override object get_physical_attributes()
    {
        return I18n.T("Luminescent are able to integrate slime extracts into themselves for wondrous effects. Most things that are toxic heal them, but most things that prevent toxicity damage them!");
    }

    //Species datums don't normally implement destroy, but JELLIES SUCK ASS OUT OF A STEEL STRAW and have to i guess
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:550
    public override object Destroy(float? force = null)
    {
        current_extract = null;
        QDEL_NULL(ref glow);
        QDEL_LIST(luminescent_actions); luminescent_actions = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:556
    public override async DmTask on_species_gain(mob_living_carbon_human new_jellyperson = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(new_jellyperson, old_species, pref_load, regenerate_icons, replace_missing);
        glow = new_jellyperson.mob_light(light_type: typeof(obj_effect_dummy_lighting_obj_moblight_species)) as obj_effect_dummy_lighting_obj_moblight;
        update_glow(new_jellyperson);

        luminescent_actions = Dm.list();

        datum_action_innate_integrate_extract integrate_extract = Dm.@new<datum_action_innate_integrate_extract>(this);
        await integrate_extract.Grant(new_jellyperson);
        Dm.append(ref luminescent_actions, integrate_extract);

        datum_action_innate_use_extract extract_minor = Dm.@new<datum_action_innate_use_extract>(this);
        await extract_minor.Grant(new_jellyperson);
        Dm.append(ref luminescent_actions, extract_minor);

        datum_action_innate_use_extract_major extract_major = Dm.@new<datum_action_innate_use_extract_major>(this);
        await extract_major.Grant(new_jellyperson);
        Dm.append(ref luminescent_actions, extract_major);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:575
    [DmParams("C")]
    public override async DmTask on_species_loss(mob_living_carbon_human C = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        await base.on_species_loss(C, new_species, pref_load, __extra);
        if (current_extract != null)
        {
            await current_extract.forceMove(C.drop_location() as atom);
            current_extract = null;
        }
        QDEL_NULL(ref glow);
        QDEL_LIST(luminescent_actions); luminescent_actions = null;
    }

    /// Updates the glow of our internal glow object
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:584
    public virtual void update_glow(mob_living_carbon_human glowie = null, object intensity = null)
    {
        if (Dm.truthy(intensity))
            glow_intensity = Dm.num(intensity);
        glow.set_light_range_power_color(glow_intensity, glow_intensity, glowie.dna.features[FEATURE_MUTANT_COLOR]);
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:589
public partial class datum_action_innate_integrate_extract : datum_action_innate
{
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:598
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        await AddComponent(typeof(datum_component_action_item_overlay), Dm.named("item_callback", Dm.@new<datum_callback>(this, nameof(locate_extract))));
    }

    /// Callback for /datum/component/action_item_overlay to find the slime extract from within the species
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:603
    public virtual object locate_extract()
    {
        datum_species_jelly_luminescent species = target as datum_species_jelly_luminescent;
        if (species == null)
            return null;

        return species.current_extract;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:610
    public override void update_button_name(atom_movable_screen_movable_action_button button = null, float? force = null, params object[] __extra)
    {
        datum_species_jelly_luminescent species = target as datum_species_jelly_luminescent;
        if (species == null || species.current_extract == null)
        {
            name = I18n.T("Integrate Extract");
            desc = I18n.T("Eat a slime extract to use its properties.");
        }
        else
        {
            name = I18n.T("Eject Extract");
            desc = I18n.T("Eject your current slime extract.");
        }

        base.update_button_name(button, force, __extra);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:621
    public override void apply_button_icon(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        datum_species_jelly_luminescent species = target as datum_species_jelly_luminescent;
        if (species == null || species.current_extract == null)
            button_icon_state = "slimeconsume";
        else
            button_icon_state = "slimeeject";

        base.apply_button_icon(current_button, force);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:630
    public override async DmTask<object> Activate()
    {
        mob_living_carbon_human human_owner = owner as mob_living_carbon_human;
        datum_species_jelly_luminescent species = target as datum_species_jelly_luminescent;
        if (species == null)
            return null;

        if (species.current_extract != null)
        {
            obj_item_slime_extract to_remove = species.current_extract;
            if (!Dm.truthy(await human_owner.put_in_active_hand(to_remove)))
                await to_remove.forceMove(human_owner.drop_location() as atom);

            species.current_extract = null;
            human_owner.balloon_alert(human_owner, I18n.T("[] ejected", to_remove.name));
        }

        else
        {
            obj_item_slime_extract to_integrate = human_owner.get_active_held_item() as obj_item_slime_extract;
            if (to_integrate == null || to_integrate.extract_uses <= 0)
            {
                human_owner.balloon_alert(human_owner, I18n.T("need an unused slime extract!"));
                return null;
            }
            if (!Dm.truthy(await human_owner.temporarilyRemoveItemFromInventory(to_integrate)))
                return null;
            await to_integrate.forceMove(human_owner);
            species.current_extract = to_integrate;
            human_owner.balloon_alert(human_owner, I18n.T("[] consumed", to_integrate.name));
        }

        foreach (datum_action to_update in Dm.each_cast<datum_action>(species.luminescent_actions))
            await to_update.build_all_button_icons();
        return null;
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:658
public partial class datum_action_innate_use_extract : datum_action_innate
{
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:668
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        await AddComponent(typeof(datum_component_action_item_overlay), Dm.named("item_callback", Dm.@new<datum_callback>(this, nameof(locate_extract))));
    }

    /// Callback for /datum/component/action_item_overlay to find the slime extract from within the species
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:673
    public virtual object locate_extract()
    {
        datum_species_jelly_luminescent species = target as datum_species_jelly_luminescent;
        if (species == null)
            return null;

        return species.current_extract;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:680
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return dot;

        datum_species_jelly_luminescent species = target as datum_species_jelly_luminescent;
        if (species != null && species.current_extract != null && species.extract_cooldown <= world.time) // COOLDOWN_FINISHED(species, extract_cooldown)
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:690
    public override async DmTask<object> Activate()
    {
        mob_living_carbon_human human_owner = owner as mob_living_carbon_human;
        datum_species_jelly_luminescent species = human_owner.dna?.species as datum_species_jelly_luminescent;
        if (species == null || species.current_extract == null)
            return null;

        species.extract_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START(species, extract_cooldown, 10 SECONDS)
        object after_use_cooldown = await species.current_extract.activate(human_owner, species, activation_type);
        species.extract_cooldown = world.time + Dm.num(after_use_cooldown); // COOLDOWN_START(species, extract_cooldown, after_use_cooldown)
        return null;
    }
}

///////////////////////////////////STARGAZERS//////////////////////////////////////////

//Stargazers are the telepathic branch of jellypeople, able to project psychic messages and to link minds with willing participants.

// tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:710
public partial class datum_species_jelly_stargazer : datum_species_jelly
{
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:718
    public override object get_physical_attributes()
    {
        return I18n.T("Stargazers can link others' minds with their own, creating a private communication channel. Most things that are toxic heal them, but most things that prevent toxicity damage them!");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:722
    public override async DmTask on_species_gain(mob_living_carbon_human grant_to = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(grant_to, old_species, pref_load, regenerate_icons, replace_missing);
        project_action = Dm.@new<datum_action_innate_project_thought>(this);
        await project_action.Grant(grant_to);

        await grant_to.AddComponent(
            typeof(datum_component_mind_linker_active_linking),
            Dm.named("network_name", I18n.T("Slime Link")),
            Dm.named("signals_which_destroy_us", Dm.list(COMSIG_SPECIES_LOSS)),
            Dm.named("show_balloon_alert", TRUE),
            Dm.named("linker_action_path", typeof(datum_action_innate_link_minds)));
    }

    //Species datums don't normally implement destroy, but JELLIES SUCK ASS OUT OF A STEEL STRAW
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:736
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref project_action);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:740
    [DmParams("remove_from")]
    public override async DmTask on_species_loss(mob_living_carbon_human remove_from = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        QDEL_NULL(ref project_action);
        await base.on_species_loss(remove_from, new_species, pref_load, __extra);
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:744
public partial class datum_action_innate_project_thought : datum_action_innate
{
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:752
    public override async DmTask<object> Activate()
    {
        mob_living_carbon_human telepath = owner as mob_living_carbon_human;
        if (telepath.stat == DEAD)
            return null;
        if (!Dm.truthy(is_species(telepath, typeof(datum_species_jelly_stargazer))))
            return null;
        DmList recipient_options = Dm.list();
        foreach (mob_living recipient__1 in Dm.each<mob_living>(oview(telepath)))
            Dm.append(ref recipient_options, recipient__1);
        if (length(recipient_options) == 0)
        {
            to_chat(telepath, span_warning(I18n.T("You don't see anyone to send your thought to.")));
            return null;
        }
        mob_living recipient = await tgui_input_list(telepath, I18n.T("Choose a telepathic message recipient"), I18n.T("Telepathy"), sort_names(recipient_options) as DmList) as mob_living;
        if (recipient == null || telepath.stat == DEAD || !Dm.truthy(is_species(telepath, typeof(datum_species_jelly_stargazer))))
            return null;
        object msg = await tgui_input_text(telepath, title: I18n.T("Telepathy"), max_length: MAX_MESSAGE_LEN);
        if (msg == null || telepath.stat == DEAD || !Dm.truthy(is_species(telepath, typeof(datum_species_jelly_stargazer))))
            return null;
        if (!Dm.@in(recipient, oview(telepath)))
        {
            to_chat(telepath, span_warning(I18n.T("You can't see [] anymore!", recipient)));
            return null;
        }
        if (Dm.truthy(recipient.can_block_magic(MAGIC_RESISTANCE_MIND, charge_cost: 0)))
        {
            to_chat(telepath, span_warning(I18n.T("As you reach into []'s mind, you are stopped by a mental blockage. It seems you've been foiled.", recipient)));
            return null;
        }
        log_directed_talk(telepath, recipient, msg, LOG_SAY, "slime telepathy");
        to_chat(recipient, Dm.text("[]<font color=#008CA2>[]</font>", span_notice(I18n.T("You hear an alien voice in your head... ")), msg));
        to_chat(telepath, span_notice(I18n.T("You telepathically said: \"[]\" to []", msg, recipient)));
        foreach (object dead in Dm.each(GLOB.dead_mob_list))
        {
            if (!isobserver(dead))
                continue;
            string follow_link_user = FOLLOW_LINK(dead, telepath);
            string follow_link_target = FOLLOW_LINK(dead, recipient);
            to_chat(dead, Dm.text("[] [] [] [] [] []", follow_link_user, span_name(Dm.text("[]", telepath)), span_alertalien(I18n.T("Slime Telepathy --> ")), follow_link_target, span_name(Dm.text("[]", recipient)), span_noticealien(Dm.text("[]", msg))));
        }
        return null;
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:786
public partial class datum_action_innate_link_minds : datum_action_innate
{
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:798
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        object Target = Dm.arg(__extra, 0);
        await base.New(__extra);
        if (!istype(Target, typeof(datum_component_mind_linker)))
        {
            stack_trace(Dm.text("[] ([]) was instantiated on a non-mind_linker target, this doesn't work.", name, type), "code/modules/mob/living/carbon/human/species_types/jellypeople.dm", 801);
            qdel(this);
        }
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:804
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return dot;
        if (!ishuman(owner) || !Dm.truthy(is_species(owner, req_species)))
            return FALSE;
        if (Dm.truthy(currently_linking))
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:815
    public override async DmTask<object> Activate()
    {
        if (!isliving(owner.pulling) || owner.grab_state < GRAB_AGGRESSIVE)
        {
            to_chat(owner, span_warning(I18n.T("You need to aggressively grab someone to link minds!")));
            return null;
        }

        mob_living living_target = owner.pulling as mob_living;
        if (living_target.stat == DEAD)
        {
            to_chat(owner, span_warning(I18n.T("They're dead!")));
            return null;
        }

        to_chat(owner, span_notice(I18n.T("You begin linking []'s mind to yours...", living_target)));
        to_chat(living_target, span_warning(I18n.T("You feel a foreign presence within your mind...")));
        currently_linking = TRUE;

        if (!Dm.truthy(await do_after(owner, 6 * SECONDS, target: living_target, extra_checks: Dm.@new<datum_callback>(this, nameof(while_link_callback), living_target))))
        {
            to_chat(owner, span_warning(I18n.T("You can't seem to link []'s mind.", living_target)));
            to_chat(living_target, span_warning(I18n.T("The foreign presence leaves your mind.")));
            currently_linking = FALSE;
            return null;
        }

        currently_linking = FALSE;
        if (QDELETED(this) || QDELETED(owner) || QDELETED(living_target))
            return null;

        datum_component_mind_linker linker = target as datum_component_mind_linker;
        if (!Dm.truthy(linker.link_mob(living_target)))
        {
            to_chat(owner, span_warning(I18n.T("You can't seem to link []'s mind.", living_target)));
            to_chat(living_target, span_warning(I18n.T("The foreign presence leaves your mind.")));
        }
        return null;
    }

    /// Callback ran during the do_after of Activate() to see if we can keep linking with someone.
    // tg: code/modules/mob/living/carbon/human/species_types/jellypeople.dm:846
    public virtual object while_link_callback(mob_living linkee = null)
    {
        if (!Dm.truthy(is_species(owner, req_species)))
            return FALSE;
        if (owner.pulling == null)
            return FALSE;
        if (owner.pulling != linkee)
            return FALSE;
        if (owner.grab_state < GRAB_AGGRESSIVE)
            return FALSE;
        if (linkee.stat == DEAD)
            return FALSE;

        return TRUE;
    }
}
