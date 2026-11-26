// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/human.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the M2 subset translated (M2 P2, scope.md 2e "human": Initialize, setup_mood (its mood since gaps MS1), setup_organless_effects,
// setup_human_dna, Destroy, on_entered, fully_heal and updatehealth's damage slowdown). The health doll's
// update_health_hud (:679) is M2 P3's partial; do_cpr (:503) is the M2 integration's (a help click on a lying human in
// crit reaches it); M6 slice 3b: can_inject, try_inject and Topic (D154) but its examine links (M9); the M7 shocks slice:
// electrocution_animation and end_electrocution_animation; syndicate G4c: crewlike_monkify (the monkey agent); gaps H1: wash_hands and
// clean_face (the sinks); gaps SC2: the HUDs' record photo links and investigate logs; the rest of the file
// (inventory, piggyback, eye colours) comes with M4a, M8 and M9 (its roots below stay Dm.NotTranslated).

// tg: code/modules/mob/living/carbon/human/human.dm:1
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human.dm:1
    [DmParams("mapload", "species")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object species = Dm.arg(__extra, 0); // a monkey cube passes TRUE here (monkeycube.dm:42), a species subtype's create_dna ignores it
        object dot = null;
        SSverbs.assign_verb(this, typeof(datum_verb_metadata_mob_living_mob_sleep)); // ASSIGN_GAME_VERB(src, /mob/living, mob_sleep)
        add_verb(this, "/mob/living/proc/toggle_resting");

        icon_state = ""; //Remove the inherent human icon that is visible on the map editor. We're rendering ourselves limb by limb, having it still be there results in a bug where the basic human icon appears below as south in all directions and generally looks nasty.

        Waiting.NoSleep(setup_mood());
        // This needs to be called very very early in human init (before organs / species are created at the minimum)
        Waiting.NoSleep(setup_organless_effects());


        create_dna(species);
        Waiting.NoSleep(((datum_species)dna.species).create_fresh_body(this));
        Waiting.NoSleep(setup_human_dna());

        create_carbon_reagents();
        set_species(((datum)dna.species).type, icon_update: FALSE); //carbon/Initialize will call update_body()
        //set species enables and disables the flag. Just to be sure, we re-enable it now until it's removed by the parent call.
        Dm.combine(ref living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS);

        prepare_huds(); //Prevents a nasty runtime on human init

        dot = base.Initialize(mapload, Dm.forward(__extra, species));

        // The components and elements of human.dm:24-28: riding (M9) stays deferred; the stripping is M8z's; the
        // footsteps are translated (M4b audio, slice 4), the crafting menu in M5 slice 8, the bloody soles in M6 slice 1.
        // tg: code/modules/mob/living/carbon/human/human.dm:24
        Waiting.NoSleep(AddComponent(typeof(datum_component_personal_crafting), ui_human_crafting));
        // tg: code/modules/mob/living/carbon/human/human.dm:25
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_HUMAN, 1, -6));
        // tg: code/modules/mob/living/carbon/human/human.dm:26
        Waiting.NoSleep(AddComponent(typeof(datum_component_bloodysoles_feet)));
        Dm.Deferred("code/modules/mob/living/carbon/human/human.dm:27", "Initialize: AddElement(/datum/element/ridable, /datum/component/riding/creature/human): riding (M10+)");
        // tg: code/modules/mob/living/carbon/human/human.dm:28
        Waiting.NoSleep(AddElement(typeof(datum_element_strippable), GLOB.strippable_human_items, nameof(should_strip)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        Dm.append(ref GLOB.human_list, this);
        ADD_TRAIT(this, TRAIT_CAN_MOUNT_HUMANS, INNATE_TRAIT);
        ADD_TRAIT(this, TRAIT_CAN_MOUNT_CYBORGS, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:29  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.mob_living_carbon_human__Initialize__loc_connections;

    // tg: code/modules/mob/living/carbon/human/human.dm:74
    public override void prepare_data_huds()
    {
        //Update med hud images...
        base.prepare_data_huds();
        //...sec hud images...
        update_ID_card();
        sec_hud_set_implants();
        sec_hud_set_security_status();
        //...fan gear
        fan_hud_set_fandom();
        //...and display them.
        add_to_all_human_data_huds();
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:43  (gaps MS1)
    public virtual async DmTask setup_mood()
    {
        mob_mood = await Dm.new_async(typeof(datum_mood), this) as datum_mood;
    }

}

// tg: code/modules/mob/living/carbon/human/human.dm:49 (interleaved types, rules 1.9)
public partial class mob_living_carbon_human_dummy : mob_living_carbon_human
{
    // tg: code/modules/mob/living/carbon/human/human.dm:49  (gaps MS1)
    public override async DmTask setup_mood()
    {
        mob_mood = await Dm.new_async(typeof(datum_mood_dummy), this) as datum_mood;
    }
}

// tg: code/modules/mob/living/carbon/human/human.dm:55 (interleaved types, rules 1.9)
public partial class mob_living_carbon_human : mob_living_carbon
{
    /// This proc is for holding effects applied when a mob is missing certain organs
    /// It is called very, very early in human init because all humans innately spawn with no organs and gain them during init
    /// Gaining said organs removes these effects
    // tg: code/modules/mob/living/carbon/human/human.dm:55
    public virtual async DmTask setup_organless_effects()
    {
        // All start without eyes, and get them via set species
        await become_blind(NO_EYES);
        // And no ears, and get them via set species
        ADD_TRAIT(this, TRAIT_DEAF, NO_EARS);
        // Mobs cannot taste anything without a tongue; the tongue organ removes this on Insert
        ADD_TRAIT(this, TRAIT_AGEUSIA, NO_TONGUE_TRAIT);
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:63
    public virtual async DmTask setup_human_dna()
    {
        await randomize_human_normie(this, randomize_mutations: TRUE, update_body: FALSE);
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:66
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.human_list, this);

        if (Dm.truthy(mob_mood))
            QDEL_NULL(ref mob_mood);

        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:92  (D154: a chat link reaches it through ClientSeam.Topic)
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        // The branches answer the links of the human's examine (M9 slice G): its ID card read up close and the HUDs' records.
        if (Dm.truthy(href_list["see_id"]))
        {
            mob viewer = usr as mob;
            bool can_see_still = Dm.@in(viewer, viewers(this, null));

            obj_item_card_id id = (wear_id as obj_item)?.GetID() as obj_item_card_id;
            bool same_id = id != null && (Dm.eq(href_list["id_ref"], REF(id)) || Dm.eq(href_list["id_name"], id.registered_name));
            if (!same_id && can_see_still)
            {
                to_chat(viewer, span_notice(I18n.T("[] [] no longer wearing that ID card.", p_They(), p_are())));
                return null;
            }

            float viable_time = can_see_still ? 3 * MINUTES : 1 * MINUTES; // assuming 3min is the length of a hop line visit - give some leeway if they're still in sight
            if (!same_id || (Dm.num(Dm.text2num(href_list["examine_time"])) + viable_time) < world.time)
            {
                to_chat(viewer, span_notice(I18n.T("You don't have that good of a memory. Examine [] again.", p_them())));
                return null;
            }
            if (!isobserver(viewer) && HAS_TRAIT(this, TRAIT_UNKNOWN_APPEARANCE))
            {
                to_chat(viewer, span_notice(I18n.T("You can't make out that ID anymore.")));
                return null;
            }
            if (!isobserver(viewer) && get_dist(viewer, this) > ID_EXAMINE_DISTANCE + 1) // leeway, ignored if the viewer is a ghost
            {
                to_chat(viewer, span_notice(I18n.T("You can't make out that ID from here.")));
                return null;
            }

            object id_name = id.registered_name;
            object id_age = id.registered_age;
            object id_job = id.assignment;
            // Should probably be recorded on the ID, but this is easier (albiet more restrictive) on chameleon ID users
            datum_record_crew record = find_record(id_name) as datum_record_crew;
            object id_blood_type = record?.blood_type;
            object id_gender = record?.gender;
            object id_species = record?.species;
            object id_icon = jointext(id.get_id_examine_strings(viewer), "");
            // Fill in some blanks for chameleon IDs to maintain the illusion of a real ID
            if (id is obj_item_card_id_advanced_chameleon)
            {
                if (!Dm.truthy(id_gender))
                    id_gender = gender;
                if (!Dm.truthy(id_species))
                    id_species = ((datum_species)dna.species).name;
                if (!Dm.truthy(id_blood_type))
                    id_blood_type = get_bloodtype();
            }
            else if (id is obj_item_card_id_advanced advancedID)
                id_job = Dm.truthy(advancedID.trim_assignment_override) ? advancedID.trim_assignment_override : id_job;

            string id_examine = span_slightly_larger(separator_hr(I18n.T("This is <em>[]'s ID card</em>.", this)));
            id_examine += "<div class='img_by_text_container'>";
            id_examine += Dm.str(id_icon);
            id_examine += "<div class='img_text'>";
            id_examine += Dm.str(jointext(Dm.list(
                I18n.T("&bull; Name: []", Dm.truthy(id_name) ? id_name : "Unknown"),
                I18n.T("&bull; Job: []", Dm.truthy(id_job) ? id_job : "Unassigned"),
                I18n.T("&bull; Age: []", Dm.truthy(id_age) ? id_age : "Unknown"),
                I18n.T("&bull; Gender: []", Dm.truthy(id_gender) ? id_gender : "Unknown"),
                I18n.T("&bull; Blood Type: []", Dm.truthy(id_blood_type) ? id_blood_type : "?"),
                I18n.T("&bull; Species: []", Dm.truthy(id_species) ? id_species : "Unknown")), "<br>"));
            id_examine += "</div>"; // container
            id_examine += "</div>"; // text

            to_chat(viewer, boxed_message(span_info(id_examine)));
        }

        ///////HUDs///////
        if (Dm.truthy(href_list["hud"]))
        {
            if (!ishuman(usr) && !isobserver(usr))
                return null;
            mob human_or_ghost_user = usr as mob;
            object perpname = get_face_name(Dm.str(get_id_name("")));
            if (!HAS_TRAIT(human_or_ghost_user, TRAIT_SECURITY_HUD) && !HAS_TRAIT(human_or_ghost_user, TRAIT_MEDICAL_HUD))
                return null;
            if ((Dm.num(Dm.text2num(href_list["examine_time"])) + 1 * MINUTES) < world.time)
            {
                to_chat(human_or_ghost_user, span_notice(I18n.T("It's too late to use this now!")));
                return null;
            }
            datum_record_crew target_record = find_record(perpname) as datum_record_crew;
            if (Dm.truthy(href_list["photo_front"]) || Dm.truthy(href_list["photo_side"]))
            {
                if (target_record == null)
                    return null;
                if (human_or_ghost_user is mob_living_carbon_human photo_user && !Dm.truthy(photo_user.canUseHUD()))
                    return null;
                if (!HAS_TRAIT(human_or_ghost_user, TRAIT_SECURITY_HUD) && !HAS_TRAIT(human_or_ghost_user, TRAIT_MEDICAL_HUD))
                    return null;
                // var/obj/item/photo/photo_from_record: get_photo's empty /icon of a record with no appearance stays one (rules 3.4), so
                // that its show() fails as in DM
                object photo_from_record = null;
                if (Dm.truthy(href_list["photo_front"]))
                    photo_from_record = target_record.get_front_photo();
                else if (Dm.truthy(href_list["photo_side"]))
                    photo_from_record = target_record.get_side_photo();
                if (Dm.truthy(photo_from_record))
                    ((obj_item_photo)photo_from_record).show(human_or_ghost_user);
                return null;
            }

            if (human_or_ghost_user is mob_living_carbon_human med_user && Dm.eq(href_list["hud"], "m"))
            {
                if (!HAS_TRAIT(med_user, TRAIT_MEDICAL_HUD))
                    return null;
                if (Dm.truthy(href_list["evaluation"]))
                {
                    if (!Dm.truthy(get_brute_loss()) && !Dm.truthy(get_fire_loss()) && !Dm.truthy(get_oxy_loss()) && Dm.num(get_tox_loss()) < 20)
                    {
                        to_chat(med_user, Dm.text("[]<br>", span_notice(I18n.T("No external injuries detected."))));
                        return null;
                    }
                    string span = "notice";
                    string status = "";
                    if (Dm.truthy(get_brute_loss()))
                    {
                        to_chat(med_user, I18n.T("<b>Physical trauma analysis:</b>"));
                        foreach (obj_item_bodypart BP in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
                        {
                            float brutedamage = BP.brute_dam;
                            if (brutedamage > 0)
                            {
                                status = I18n.T("received minor physical injuries.");
                                span = "notice";
                            }
                            if (brutedamage > 20)
                            {
                                status = I18n.T("been seriously damaged.");
                                span = "danger";
                            }
                            if (brutedamage > 40)
                            {
                                status = I18n.T("sustained major trauma!");
                                span = "userdanger";
                            }
                            if (Dm.truthy(brutedamage))
                                to_chat(med_user, I18n.T("<span class='[]'>[] appears to have []</span>", span, BP, status));
                        }
                    }
                    if (Dm.truthy(get_fire_loss()))
                    {
                        to_chat(med_user, I18n.T("<b>Analysis of skin burns:</b>"));
                        foreach (obj_item_bodypart BP in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
                        {
                            float burndamage = BP.burn_dam;
                            if (burndamage > 0)
                            {
                                status = I18n.T("signs of minor burns.");
                                span = "notice";
                            }
                            if (burndamage > 20)
                            {
                                status = I18n.T("serious burns.");
                                span = "danger";
                            }
                            if (burndamage > 40)
                            {
                                status = I18n.T("major burns!");
                                span = "userdanger";
                            }
                            if (Dm.truthy(burndamage))
                                to_chat(med_user, I18n.T("<span class='[]'>[] appears to have []</span>", span, BP, status));
                        }
                    }
                    if (Dm.truthy(get_oxy_loss()))
                        to_chat(med_user, span_danger(I18n.T("Patient has signs of suffocation, emergency treatment may be required!")));
                    if (Dm.num(get_tox_loss()) > 20)
                        to_chat(med_user, span_danger(I18n.T("Gathered data is inconsistent with the analysis, possible cause: poisoning.")));
                }
                if (!Dm.truthy(med_user.wear_id)) //You require access from here on out.
                {
                    to_chat(med_user, span_warning(I18n.T("ERROR: Invalid access")));
                    return null;
                }
                DmList access = ((obj_item)med_user.wear_id).GetAccess() as DmList;
                if (!Dm.@in(ACCESS_MEDICAL, access))
                {
                    to_chat(med_user, span_warning(I18n.T("ERROR: Invalid access")));
                    return null;
                }
                if (Dm.truthy(href_list["physical_status"]))
                {
                    object health_status = await tgui_input_list(med_user, I18n.T("Specify a new physical status for this person."), I18n.T("Medical HUD"), Dm.list(PHYSICAL_ACTIVE, PHYSICAL_DEBILITATED, PHYSICAL_UNCONSCIOUS, PHYSICAL_DECEASED), target_record?.physical_status);
                    if (!Dm.truthy(health_status) || target_record == null || !Dm.truthy(med_user.canUseHUD()) || !HAS_TRAIT(med_user, TRAIT_MEDICAL_HUD))
                        return null;

                    target_record.physical_status = health_status;
                    return null;
                }

                if (Dm.truthy(href_list["mental_status"]))
                {
                    object health_status = await tgui_input_list(med_user, I18n.T("Specify a new mental status for this person."), I18n.T("Medical HUD"), Dm.list(MENTAL_STABLE, MENTAL_WATCH, MENTAL_UNSTABLE, MENTAL_INSANE), target_record?.mental_status);
                    if (!Dm.truthy(health_status) || target_record == null || !Dm.truthy(med_user.canUseHUD()) || !HAS_TRAIT(med_user, TRAIT_MEDICAL_HUD))
                        return null;

                    target_record.mental_status = health_status;
                    return null;
                }

                if (Dm.truthy(href_list["quirk"]))
                {
                    object quirkstring = get_quirk_string(TRUE, CAT_QUIRK_ALL, from_scan: TRUE);
                    if (Dm.truthy(quirkstring))
                        to_chat(med_user, I18n.T("<span class='notice ml-1'>Detected physiological traits:</span>\n<span class='notice ml-2'>[]</span>", quirkstring));
                    else
                        to_chat(med_user, I18n.T("<span class='notice ml-1'>No physiological traits found.</span>"));
                }
                return null; //Medical HUD ends here.
            }

            if (Dm.eq(href_list["hud"], "s"))
            {
                object allowed_access = null;
                if (!HAS_TRAIT(human_or_ghost_user, TRAIT_SECURITY_HUD))
                    return null;
                if (human_or_ghost_user is mob_living_carbon_human sec_user)
                {
                    if (IS_UNCONSCIOUS_OR_CRIT(sec_user) || sec_user == this) //|| !human_user.canmove || human_user.restrained()) Fluff: Sechuds have eye-tracking technology and sets 'arrest' to people that the wearer looks and blinks at.
                        return null; //Non-fluff: This allows sec to set people to arrest as they get disarmed or beaten
                    // Checks the user has security clearence before allowing them to change arrest status via hud, comment out to enable all access
                    if (sec_user.glasses is obj_item_clothing_glasses_hud_security user_glasses && Dm.truthy(Dm.bitand(user_glasses.obj_flags, EMAGGED)))
                        allowed_access = "@%&ERROR_%$*";
                    else //Implant and standard glasses check access
                    {
                        if (Dm.truthy(sec_user.wear_id))
                        {
                            DmList access = ((obj_item)sec_user.wear_id).GetAccess() as DmList;
                            if (Dm.@in(ACCESS_SECURITY, access))
                                allowed_access = sec_user.get_authentification_name();
                        }
                    }

                    if (!Dm.truthy(allowed_access))
                    {
                        to_chat(sec_user, span_warning(I18n.T("ERROR: Invalid access.")));
                        return null;
                    }
                }

                if (!Dm.truthy(perpname))
                {
                    to_chat(human_or_ghost_user, span_warning(I18n.T("ERROR: Can not identify target.")));
                    return null;
                }
                target_record = find_record(perpname) as datum_record_crew;
                if (target_record == null)
                {
                    to_chat(human_or_ghost_user, span_warning(I18n.T("ERROR: Unable to locate data core entry for target.")));
                    return null;
                }
                if (human_or_ghost_user is mob_living_carbon_human status_user && Dm.truthy(href_list["status"]))
                {
                    object new_status = await tgui_input_list(status_user, I18n.T("Specify a new criminal status for this person."), I18n.T("Security HUD"), WANTED_STATUSES(), target_record.wanted_status);
                    if (!Dm.truthy(new_status) || target_record == null || !Dm.truthy(status_user.canUseHUD()) || !HAS_TRAIT(status_user, TRAIT_SECURITY_HUD))
                        return null;

                    if (Dm.eq(new_status, WANTED_ARREST))
                    {
                        datum_crime new_crime = Dm.@new<datum_crime>(Dm.named("author", status_user), Dm.named("details", "Set by SecHUD."));
                        Dm.append(ref target_record.crimes, new_crime);
                        investigate_log(Dm.text("SecHUD auto-crime | Added to [] by []", target_record.name, key_name(status_user)), INVESTIGATE_RECORDS);
                    }

                    investigate_log(Dm.text("has been set from [] to [] via HUD by [].", target_record.wanted_status, new_status, key_name(status_user)), INVESTIGATE_RECORDS);
                    target_record.wanted_status = Dm.str(new_status);
                    update_matching_security_huds(target_record.name);
                    return null;
                }

                if (Dm.truthy(href_list["view"]))
                {
                    if (human_or_ghost_user is mob_living_carbon_human view_user && !Dm.truthy(view_user.canUseHUD()))
                        return null;
                    if (!HAS_TRAIT(human_or_ghost_user, TRAIT_SECURITY_HUD))
                        return null;
                    string sec_record_message = "";
                    sec_record_message += I18n.T("<b>Name:</b> []", target_record.name);
                    sec_record_message += I18n.T("\n<b>Criminal Status:</b> []", target_record.wanted_status);
                    sec_record_message += I18n.T("\n<b>Citations:</b> []", Dm.length(target_record.citations));
                    sec_record_message += I18n.T("\n<b>Note:</b> []", Dm.truthy(target_record.security_note) ? target_record.security_note : "None");
                    sec_record_message += I18n.T("\n<b>Rapsheet:</b> [] incidents", Dm.length(target_record.crimes));
                    if (Dm.truthy(Dm.length(target_record.crimes)))
                    {
                        foreach (datum_crime crime in Dm.each<datum_crime>(target_record.crimes))
                        {
                            if (!Dm.truthy(crime.valid))
                            {
                                sec_record_message += span_notice(I18n.T("\n-- REDACTED --"));
                                continue;
                            }

                            sec_record_message += I18n.T("\n<b>Crime:</b> []", crime.name);
                            sec_record_message += I18n.T("\n<b>Details:</b> []", crime.details);
                            sec_record_message += I18n.T("\nAdded by [] at []", crime.author, crime.time);
                        }
                    }
                    to_chat(human_or_ghost_user, boxed_message(sec_record_message));
                    return null;
                }
                if (human_or_ghost_user is mob_living_carbon_human human_user)
                {
                    if (Dm.truthy(href_list["add_citation"]))
                    {
                        object max_fine = config.Get(typeof(datum_config_entry_number_maxfine));
                        object citation_name = await tgui_input_text(human_user, I18n.T("Citation crime"), I18n.T("Security HUD"), max_length: MAX_MESSAGE_LEN);
                        object fine = await tgui_input_number(human_user, I18n.T("Citation fine"), I18n.T("Security HUD"), 50, max_fine, 5);
                        if (!Dm.truthy(fine) || target_record == null || !Dm.truthy(citation_name) || !Dm.truthy(allowed_access) || !isnum(fine) || Dm.num(fine) > Dm.num(max_fine) || Dm.num(fine) <= 0 || !Dm.truthy(human_user.canUseHUD()) || !HAS_TRAIT(human_user, TRAIT_SECURITY_HUD))
                            return null;

                        datum_crime_citation new_citation = Dm.@new<datum_crime_citation>(Dm.named("name", citation_name), Dm.named("author", allowed_access), Dm.named("fine", fine));

                        Dm.append(ref target_record.citations, new_citation);
                        new_citation.alert_owner(usr as mob, this, target_record.name, I18n.T("You have been fined [] [] for '[]'. Fines may be paid at security.", fine, MONEY_NAME, citation_name));
                        investigate_log(Dm.text("New Citation: <strong>[]</strong> Fine: [] | Added to [] by []", citation_name, fine, target_record.name, key_name(human_user)), INVESTIGATE_RECORDS);
                        // SSblackbox.ReportCitation(REF(new_citation), human_user.ckey, human_user.real_name, target_record.name, citation_name, null, fine):
                        // the database's citation table (rules 13, not-translated.md 198)

                        return null;
                    }

                    if (Dm.truthy(href_list["add_crime"]))
                    {
                        object crime_name = await tgui_input_text(human_user, I18n.T("Crime name"), I18n.T("Security HUD"), max_length: MAX_MESSAGE_LEN);
                        if (target_record == null || !Dm.truthy(crime_name) || !Dm.truthy(allowed_access) || !Dm.truthy(human_user.canUseHUD()) || !HAS_TRAIT(human_user, TRAIT_SECURITY_HUD))
                            return null;

                        datum_crime new_crime = Dm.@new<datum_crime>(Dm.named("name", crime_name), Dm.named("author", allowed_access));

                        Dm.append(ref target_record.crimes, new_crime);
                        investigate_log(Dm.text("New Crime: <strong>[]</strong> | Added to [] by []", crime_name, target_record.name, key_name(human_user)), INVESTIGATE_RECORDS);
                        // SSblackbox.ReportCitation(REF(new_crime), human_user.ckey, human_user.real_name, target_record.name, crime_name, null): the
                        // database's citation table (rules 13, not-translated.md 198)
                        to_chat(human_user, span_notice(I18n.T("Successfully added a crime.")));

                        return null;
                    }

                    if (Dm.truthy(href_list["add_note"]))
                    {
                        object new_note = await tgui_input_text(human_user, I18n.T("Security note"), I18n.T("Security Records"), max_length: MAX_MESSAGE_LEN, multiline: TRUE);
                        if (target_record == null || !Dm.truthy(new_note) || !Dm.truthy(allowed_access) || !Dm.truthy(human_user.canUseHUD()) || !HAS_TRAIT(human_user, TRAIT_SECURITY_HUD))
                            return null;

                        target_record.security_note = new_note;

                        return null;
                    }
                }
            }
        }

        return await base.Topic(href, href_list); //end of this massive fucking chain. TODO: make the hud chain not spooky. - Yeah, great job doing that.
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:370
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        Waiting.NoSleep(spreadFire(AM as mob_living));
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:374
    public virtual object canUseHUD()
    {
        return Dm.bitand(mobility_flags, MOBILITY_USE);
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:377
    public override object can_inject(mob user = null, object target_zone = null, object injection_flags = null)
    {
        object dot = TRUE; // Default to returning true.
        // we may choose to ignore species trait pierce immunity in case we still want to check skellies for thick clothing without insta failing them (wounds)
        if (Dm.truthy(Dm.bitand(injection_flags, INJECT_CHECK_IGNORE_SPECIES)))
        {
            if (HAS_TRAIT_NOT_FROM(this, TRAIT_PIERCEIMMUNE, SPECIES_TRAIT))
                dot = FALSE;
        }
        else if (HAS_TRAIT(this, TRAIT_PIERCEIMMUNE))
            dot = FALSE;
        if (user != null && !Dm.truthy(target_zone))
            target_zone = get_bodypart(check_zone(user.zone_selected) as string); //try to find a bodypart. if there isn't one, target_zone will be null, and check_zone in the next line will default to the chest.
        obj_item_bodypart the_part = (isbodypart(target_zone) ? target_zone : get_bodypart(check_zone(target_zone) as string)) as obj_item_bodypart; //keep these synced
        // Loop through the clothing covering this bodypart and see if there's any thiccmaterials
        if (!Dm.truthy(Dm.bitand(injection_flags, INJECT_CHECK_PENETRATE_THICK)))
        {
            foreach (obj_item_clothing iter_clothing in Dm.each<obj_item_clothing>(get_clothing_on_part(the_part)))
            {
                if (Dm.truthy(Dm.bitand(iter_clothing.clothing_flags, THICKMATERIAL)))
                {
                    dot = FALSE;
                    break;
                }
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:395
    public override object try_inject(mob user = null, object target_zone = null, object injection_flags = null)
    {
        object dot = base.try_inject(user, target_zone, injection_flags);
        if (!Dm.truthy(dot) && Dm.truthy(Dm.bitand(injection_flags, INJECT_TRY_SHOW_ERROR_MESSAGE)) && user != null)
            balloon_alert(user, I18n.T("no exposed skin on []!", parse_zone(Dm.truthy(target_zone) ? target_zone : check_zone(user.zone_selected))));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:483
    public virtual async DmTask set_cloned_appearance()
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/human.dm:483", "set_cloned_appearance: not in M2 (M4a/M8/M10+ rest)");
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:500  #define CPR_PANIC_SPEED (0.8 SECONDS) (file-local, #undef at :572)
    private const float CPR_PANIC_SPEED = 0.8f * SECONDS;

    /// Performs CPR on the target after a delay.
    // tg: code/modules/mob/living/carbon/human/human.dm:503
    public virtual async DmTask<object> do_cpr(mob_living_carbon target = null)
    {
        if (target == this)
            return null;

        float panicking = FALSE;

        do
        {
            // CHECK_DNA_AND_SPECIES(target) (__DEFINES/DNA.dm:3, a statement macro inline, rules 11.1)
            if (!Dm.truthy(target.dna?.species))
                return null;

            if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(this, target)))
                return FALSE;

            if (IS_DEAD_OR_FAKING(target))
            {
                balloon_alert(this, I18n.T("[] [] dead!", target.p_they(), target.p_are()));
                return FALSE;
            }

            if (Dm.truthy(is_mouth_covered()))
            {
                balloon_alert(this, "remove your mask first!");
                return FALSE;
            }

            if (Dm.truthy(target.is_mouth_covered()))
            {
                balloon_alert(this, I18n.T("remove [] mask first!", target.p_their()));
                return FALSE;
            }

            if (HAS_TRAIT_FROM(this, TRAIT_NOBREATH, DISEASE_TRAIT))
            {
                to_chat(this, span_warning("you can't breathe!"));
                return FALSE;
            }

            obj_item_organ_lungs human_lungs = get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
            if (human_lungs == null)
            {
                balloon_alert(this, "you don't have lungs!");
                return FALSE;
            }
            if (Dm.truthy(Dm.bitand(human_lungs.organ_flags, ORGAN_FAILING)))
            {
                balloon_alert(this, "your lungs are too damaged!");
                return FALSE;
            }

            visible_message(span_notice(I18n.T("[] is trying to perform CPR on []!", this, target.name)),
                            span_notice(I18n.T("You try to perform CPR on []... Hold still!", target.name)));

            if (!Dm.truthy(await do_after(this, delay: Dm.truthy(panicking) ? CPR_PANIC_SPEED : (3 * SECONDS), target: target)))
            {
                balloon_alert(this, "you fail to perform CPR!");
                return FALSE;
            }

            if (target.health > target.crit_threshold)
                return FALSE;

            visible_message(span_notice(I18n.T("[] performs CPR on []!", this, target.name)), span_notice(I18n.T("You perform CPR on [].", target.name)));
            if (HAS_MIND_TRAIT(this, TRAIT_MORBID))
                await add_mood_event("morbid_saved_life", typeof(datum_mood_event_morbid_saved_life));
            else
                await add_mood_event("saved_life", typeof(datum_mood_event_saved_life));
            log_combat(this, target, "CPRed");

            if (HAS_TRAIT(target, TRAIT_NOBREATH))
                to_chat(target, span_unconscious("You feel a breath of fresh air... which is a sensation you don't recognise..."));
            else if (!Dm.truthy(target.get_organ_slot(ORGAN_SLOT_LUNGS)))
                to_chat(target, span_unconscious("You feel a breath of fresh air... but you don't feel any better..."));
            else
            {
                await target.adjust_oxy_loss(Dm.neg(Dm.min(target.get_oxy_loss(), 7)));
                to_chat(target, span_unconscious("You feel a breath of fresh air enter your lungs... It feels good..."));
            }

            if (target.health <= target.crit_threshold)
            {
                if (!Dm.truthy(panicking))
                    to_chat(this, span_warning(I18n.T("[] still isn't up! You try harder!", target)));
                panicking = TRUE;
            }
            else
                panicking = FALSE;
        }
        while (Dm.truthy(panicking));
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:591
    // (gaps H1: the sinks)
    public virtual async DmTask<object> wash_hands(object clean_types = null)
    {
        if (Dm.truthy(Dm.bitand(covered_slots, HIDEGLOVES)))
            return FALSE;

        if (gloves != null)
            await gloves.wash(clean_types);
        else if (Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_BLOOD)) && blood_in_hands > 0)
        {
            blood_in_hands = 0;
            await update_worn_gloves();
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:607
    public virtual object clean_face(datum source = null, object clean_types = null)
    {
        // SIGNAL_HANDLER (gaps H1: the sinks)
        object dot = null;
        if (!Dm.truthy(is_mouth_covered()) && Dm.truthy(Waiting.NoSleep(clean_lips())))
            dot = TRUE;

        if (glasses != null && !Dm.truthy(is_eyes_covered(Dm.bitor(ITEM_SLOT_MASK, ITEM_SLOT_HEAD))) && Dm.truthy(Waiting.NoSleep(glasses.wash(clean_types))))
            dot = TRUE;

        if (wear_mask != null && !Dm.truthy(Dm.bitand(covered_slots, HIDEMASK)) && Dm.truthy(Waiting.NoSleep(wear_mask.wash(clean_types))))
            dot = TRUE;
        return dot;
    }

    //Turns a mob black, flashes a skeleton overlay
    //Just like a cartoon!
    // tg: code/modules/mob/living/carbon/human/human.dm:634  (the M7 shocks slice)
    public virtual async DmTask electrocution_animation(object anim_duration = null)
    {
        mutable_appearance zap_appearance = null;

        // If we have a species, we need to handle mutant parts and stuff
        if (Dm.truthy(dna?.species))
        {
            await add_atom_colour(COLOR_BLACK, TEMPORARY_COLOUR_PRIORITY);
            mutable_appearance shock_animation_dna = mutable_appearance(icon, "electrocuted_base", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
            apply_height(shock_animation_dna, ENTIRE_BODY);
            zap_appearance = shock_animation_dna;
        }

        // Otherwise do a generic animation
        else
        {
            if (!Dm.truthy(electrocution_animation__shock_animation_generic))
            {
                electrocution_animation__shock_animation_generic = mutable_appearance(icon, "electrocuted_generic") as mutable_appearance;
                electrocution_animation__shock_animation_generic.appearance_flags = Dm.bitor(electrocution_animation__shock_animation_generic.appearance_flags, Dm.bitor(RESET_COLOR, KEEP_APART));
            }
            zap_appearance = electrocution_animation__shock_animation_generic;
        }

        add_overlay(zap_appearance);
        _addtimer(Dm.@new<datum_callback>(this, nameof(end_electrocution_animation), zap_appearance), Dm.num_or_null(anim_duration), file: "code/modules/mob/living/carbon/human/human.dm", line: 653);
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:645  var/static/mutable_appearance/shock_animation_generic (rules 10.6)
    private static ref mutable_appearance electrocution_animation__shock_animation_generic => ref TgState.Current.mob_living_carbon_human__electrocution_animation__shock_animation_generic;

    // tg: code/modules/mob/living/carbon/human/human.dm:655  (the M7 shocks slice)
    public virtual async DmTask end_electrocution_animation(mutable_appearance MA = null)
    {
        await remove_atom_colour(TEMPORARY_COLOUR_PRIORITY, COLOR_BLACK);
        cut_overlay(MA);
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:688
    public override async DmTask fully_heal(float? heal_flags = null)
    {
        heal_flags ??= HEAL_ALL;
        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_NEGATIVE_MUTATIONS)))
        {
            foreach (datum_mutation existing_mutation in Dm.each<datum_mutation>(dna.mutations))
            {
                if (!Dm.eq(existing_mutation.quality, POSITIVE) && Dm.truthy(existing_mutation.remove_on_aheal))
                    await dna.remove_mutation(existing_mutation, GLOB.standard_mutation_sources);
            }
        }

        if (Dm.truthy(Dm.bitand(heal_flags, HEAL_TEMP)))
        {
            set_coretemperature(get_body_temp_normal(apply_change: FALSE));
            heat_exposure_stacks = 0;
            seconds_in_low_pressure = 0;
        }

        await base.fully_heal(heal_flags);
    }

    // gaps package P6: the empty hand's use (Z) reaches the arm, whose implants open their tools' radial (augments_arms.dm:110)
    // tg: code/modules/mob/living/carbon/human/human.dm:864
    public override async DmTask limb_attack_self()
    {
        obj_item_bodypart arm = hand_bodyparts[active_hand_index] as obj_item_bodypart;
        if (arm != null)
            await arm.attack_self(this);
        await base.limb_attack_self();
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:885
    public virtual object can_piggyback(mob_living_carbon target = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/human.dm:885", "can_piggyback: not in M2 (M4a/M8/M10+ rest)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:888
    public virtual object can_be_firemanned(mob_living_carbon target = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/human.dm:888", "can_be_firemanned: not in M2 (M4a/M8/M10+ rest)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:891
    public virtual async DmTask<object> fireman_carry(mob_living_carbon target = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/human.dm:891", "fireman_carry: not in M2 (M4a/M8/M10+ rest)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:935
    public virtual async DmTask<object> piggyback(mob_living_carbon target = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/human.dm:935", "piggyback: not in M2 (M4a/M8/M10+ rest)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:960
    public override async DmTask updatehealth()
    {
        await base.updatehealth();
        float health_deficiency = max((maxHealth - health), staminaloss);
        if (health_deficiency >= 40)
            await add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_damage_slowdown), TRUE, multiplicative_slowdown: health_deficiency / 75);
        else
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_damage_slowdown));
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:973
    public virtual async DmTask add_eye_color_left(object color = null, object color_priority = null, float? update_body = null)
    {
        update_body ??= TRUE;
        LAZYSET(ref eye_color_left_overrides, Dm.text("[]", color_priority), color);
        if (Dm.truthy(update_body))
        {
            await update_eyes();
        }
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:978
    public virtual async DmTask add_eye_color_right(object color = null, object color_priority = null, float? update_body = null)
    {
        update_body ??= TRUE;
        LAZYSET(ref eye_color_right_overrides, Dm.text("[]", color_priority), color);
        if (Dm.truthy(update_body))
        {
            await update_eyes();
        }
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:983
    public virtual async DmTask add_eye_color(object color = null, object color_priority = null, float? update_body = null)
    {
        update_body ??= TRUE;
        await add_eye_color_left(color, color_priority, update_body: FALSE);
        await add_eye_color_right(color, color_priority, update_body: update_body);
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:987
    public virtual async DmTask remove_eye_color(object color_priority = null, float? update_body = null)
    {
        update_body ??= TRUE;
        LAZYREMOVE(ref eye_color_left_overrides, Dm.text("[]", color_priority));
        LAZYREMOVE(ref eye_color_right_overrides, Dm.text("[]", color_priority));
        if (Dm.truthy(update_body))
            await update_eyes();
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:1042
    ///Proc used to make monkey roles able to function like crew, but not be able to shift into humans easily.
    public virtual async DmTask crewlike_monkify()
    {
        if (!ismonkey(this))
            set_species(typeof(datum_species_monkey));
        // Can't make them human or nonclever. At least not with the easy and boring way out.
        await dna.add_mutation(typeof(datum_mutation_clever), MUTATION_SOURCE_CREW_MONKEY);
        await dna.add_mutation(typeof(datum_mutation_race), MUTATION_SOURCE_CREW_MONKEY);

        add_traits(Dm.list(TRAIT_NO_DNA_SCRAMBLE, TRAIT_BADDNA, TRAIT_BORN_MONKEY), SPECIES_TRAIT);
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:1051
    public virtual object is_atmos_sealed(object additional_flags = null, float? check_hands = null)
    {
        check_hands ??= FALSE;
        bool chest_covered = false;
        bool head_covered = false;
        bool hands_covered = false;
        foreach (obj_item_clothing equipped in Dm.each<obj_item_clothing>(get_equipped_items(INCLUDE_ABSTRACT)))
        {
            // We don't really have space-proof gloves, so even if we're checking them we ignore the flags
            if (Dm.truthy(Dm.bitand(equipped.body_parts_covered, HANDS)) && num_hands >= default_num_hands)
                hands_covered = true;
            if (Dm.truthy(Dm.bitand(equipped.clothing_flags, Dm.bitor(STOPSPRESSUREDAMAGE, additional_flags))) && Dm.truthy(Dm.bitand(equipped.body_parts_covered, CHEST)))
                chest_covered = true;
            if (Dm.truthy(Dm.bitand(equipped.clothing_flags, Dm.bitor(STOPSPRESSUREDAMAGE, additional_flags))) && Dm.truthy(Dm.bitand(equipped.body_parts_covered, HEAD)))
                head_covered = true;
        }
        if (!chest_covered)
            return FALSE;
        if (!hands_covered && Dm.truthy(check_hands))
            return FALSE;
        return (head_covered || HAS_TRAIT(this, TRAIT_HEAD_ATMOS_SEALED)) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:1069  (the M7 shocks slice)
    public override object should_electrocute(object power_source = null)
    {
        if (Dm.eq(gloves?.siemens_coefficient, 0))
            return FALSE;
        return base.should_electrocute(power_source);
    }
}

// tg: code/modules/mob/living/carbon/human/human.dm:993
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/human/human.dm:993
    public virtual object get_right_eye_color()
    {
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:1009
    public virtual object get_left_eye_color()
    {
        return null;
    }
}

// tg: code/modules/mob/living/carbon/human/human.dm:996 (M10x slice D2: the eyes' body overlay)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human.dm:996
    public override object get_right_eye_color()
    {
        if (LAZYLEN(eye_color_right_overrides) == 0)
            return eye_color_right;

        object eye_color = eye_color_right;
        object priority = null;
        foreach (object override_priority in Dm.each(eye_color_right_overrides))
        {
            float? new_priority = Dm.text2num(override_priority);
            if (Dm.gt(new_priority, priority))
            {
                priority = new_priority;
                eye_color = eye_color_right_overrides[override_priority];
            }
        }
        return eye_color;
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:1012
    public override object get_left_eye_color()
    {
        if (LAZYLEN(eye_color_left_overrides) == 0)
            return eye_color_left;

        object eye_color = eye_color_left;
        object priority = null;
        foreach (object override_priority in Dm.each(eye_color_left_overrides))
        {
            float? new_priority = Dm.text2num(override_priority);
            if (Dm.gt(new_priority, priority))
            {
                priority = new_priority;
                eye_color = eye_color_left_overrides[override_priority];
            }
        }
        return eye_color;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/human/human.dm:29  var/static/list/loc_connections = list(...)
    internal DmList mob_living_carbon_human__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(mob_living_carbon_human.on_entered)),
        Dm.kv(COMSIG_LIVING_DISARM_PRESHOVE, nameof(mob_living_carbon_human.disarm_precollide)),
        Dm.kv(COMSIG_LIVING_DISARM_COLLIDE, nameof(mob_living_carbon_human.disarm_collision)));
    // tg: code/modules/mob/living/carbon/human/human.dm:645  the proc-level var/static of electrocution_animation
    internal mutable_appearance mob_living_carbon_human__electrocution_animation__shock_animation_generic;
}

// tg: code/modules/mob/living/carbon/human/human.dm:870 (drag and drop, protocol version 17: a mob dragged onto a human)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human.dm:870
    public override async DmTask<object> mouse_buckle_handling(mob_living M = null, mob_living user = null)
    {
        if (pulling != M || grab_state != GRAB_AGGRESSIVE || IS_UNCONSCIOUS_OR_CRIT(this))
            return FALSE;

        // :874-882: the piggyback (user == M && can_piggyback(M): piggyback(M)) and the fireman carry (can_be_firemanned(M):
        // fireman_carry(M)) of a mob this human holds in an aggressive grab; grabbing and pulling are M9, so no mob is pulled here yet
        Dm.NotTranslated("code/modules/mob/living/carbon/human/human.dm:874", "mouse_buckle_handling: piggyback and fireman_carry of an aggressively grabbed mob (grabbing, pulling and riding, M10+)");
        return null;
    }
}

// tg: code/modules/mob/living/carbon/human/human.dm:492 (interleaved types, rules 1.9; M5x slice 5)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human.dm:492
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (Dm.num(current_size) >= STAGE_THREE)
        {
            foreach (obj_item hand in Dm.each<obj_item>(held_items))
            {
                if (prob(Dm.num(current_size) * 5) && hand.w_class >= ((11 - Dm.num(current_size)) / 2) && Dm.truthy(await dropItemToGround(hand)))
                {
                    await step_towards(hand, this);
                    to_chat(this, span_warning(I18n.T("\\The [] pulls \\the [] from your grip!", singularity, hand)));
                }
            }
        }
        return null;
    }
}

// tg: code/modules/mob/living/carbon/human/human.dm:1029 (interleaved types, rules 1.9; gaps package C5)
public partial class mob_living_carbon_human_species : mob_living_carbon_human
{
    // tg: code/modules/mob/living/carbon/human/human.dm:1033
    public override void create_dna(object species = null)
    {
        base.create_dna(race); //Kind of shit but I'm brainfarting how to do this better right now.
    }

    // tg: code/modules/mob/living/carbon/human/human.dm:1036
    [DmParams("mrace", "icon_update", "pref_load", "replace_missing")]
    public override void set_species(object mrace = null, float? icon_update = null, params object[] __extra)
    {
        base.set_species(mrace, icon_update, __extra);
        if (Dm.truthy(use_random_name))
            Waiting.NoSleep(fully_replace_character_name(newname: generate_random_mob_name()));
    }
}

// tg: code/modules/mob/living/carbon/human/human.dm:400 (gaps S3: the photocopier's copy of a butt)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human.dm:400
    public override object get_butt_sprite()
    {
        obj_item_bodypart_chest chest = get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
        return chest?.get_butt_sprite();
    }
}
