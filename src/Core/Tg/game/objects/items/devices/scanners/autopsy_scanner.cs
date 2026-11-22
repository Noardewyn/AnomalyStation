// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/scanners/autopsy_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J5, D298: the scan of a cadaver; gaps autopsy, after P1 and S3: scan_cadaver, the autopsy operation's
// report on paper).

// tg: code/game/objects/items/devices/scanners/autopsy_scanner.dm:1
public partial class obj_item_autopsy_scanner : obj_item
{
    // (the chat's icon2html prefix: no inline icons in the client's chat, as the health analyzer)

    // tg: code/game/objects/items/devices/scanners/autopsy_scanner.dm:20
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;
        if (!Dm.truthy(user.can_read(this)) || Dm.truthy(user.is_blind()))
            return ITEM_INTERACT_BLOCKING;

        mob_living scanned = interacting_with as mob_living;

        if (!Dm.truthy(IS_DEAD_OR_FAKING(scanned))) // good job, you found a loophole
        {
            to_chat(user, span_deadsay(I18n.T("ERROR! CANNOT SCAN LIVE CADAVERS. PROCURE HEALTH ANALYZER OR TERMINATE PATIENT.")));
            return ITEM_INTERACT_BLOCKING;
        }

        // Clumsiness/brain damage check
        if ((HAS_TRAIT(user, TRAIT_CLUMSY) || HAS_TRAIT(user, TRAIT_DUMB)) && prob(50))
        {
            user.visible_message(span_warning(I18n.T("[] analyzes the floor's vitals!", user)),
                span_notice(I18n.T("You stupidly try to analyze the floor's vitals!")));
            to_chat(user, Dm.text("[]\n[]\n[]\n[]",
                span_info("Analyzing results for The floor:\n\tOverall status: <b>Healthy</b>"),
                span_info("Key: <font color='#00cccc'>Suffocation</font>/<font color='#00cc66'>Toxin</font>/<font color='#ffcc33'>Burn</font>/<font color='#ff3333'>Brute</font>"),
                span_info("\tDamage specifics: <font color='#66cccc'>0</font>-<font color='#00cc66'>0</font>-<font color='#ff9933'>0</font>-<font color='#ff3333'>0</font>"),
                span_info("Body temperature: ???")));
            return ITEM_INTERACT_SUCCESS;
        }

        user.visible_message(span_notice(I18n.T("[] scans []'s cadaver.", user, scanned)));
        to_chat(user, span_deadsay(I18n.T("ANALYZING CADAVER.")));

        healthscan(user, scanned, scanpower: SCANPOWER_ADVANCED);

        add_fingerprint(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/scanners/autopsy_scanner.dm:51
    public virtual async DmTask<object> scan_cadaver(mob_living_carbon_human user = null, mob_living_carbon scanned = null)
    {
        if (scanned.stat != DEAD)
            return null;

        obj_item_paper autopsy_report = Dm.@new<obj_item_paper>(get_turf(this));
        autopsy_report.color = "#99ccff";
        autopsy_report.name = I18n.T("autopsy report of [] - []", scanned, server_timestamp(format: "hh:mm", ic_time: TRUE));
        string final_report_text = I18n.T("<center><b>Autopsy report</br>Time of Autopsy: []</b></center>",
            UNDERLINED_HTML_TEXT(Dm.text("[]", server_timestamp(format: "hh:mm", ic_time: TRUE)), I18n.T("Shift Time: []", round_timestamp(format: "hh:mm"))));

        //A lot of this is extremely similar to /proc/healthscan() - but with different formatting, no color, and some added/removed info
        //Does not list quirks/exhaustion/how to repair wounds
        //DOES list wound sources/
        DmList autopsy_information = Dm.list();
        Dm.append(ref autopsy_information, I18n.T("Autopsy Coroner - []<hr>", user.name));

        Dm.append(ref autopsy_information, I18n.T("Analyzing results for <b>[]</b>:</br></br>", scanned.name));
        Dm.append(ref autopsy_information, I18n.T("Time of Death - <b>[]</b></br>", scanned.station_timestamp_timeofdeath));
        Dm.append(ref autopsy_information, I18n.T("Subject has been dead for <b>[]</b>.<hr>", DisplayTimeText(round(world.time - scanned.timeofdeath))));

        float oxy_loss = Dm.num(scanned.get_oxy_loss());
        float tox_loss = Dm.num(scanned.get_tox_loss());
        float fire_loss = Dm.num(scanned.get_fire_loss());
        float brute_loss = Dm.num(scanned.get_brute_loss());
        /// "Body Data" portion of the autopsy - damage, wounds, and limbs
        string dmgreport = I18n.T("<u><b>Body Data:</b></u><table class='ml-2'><tr><td style='width:7em;'><b>Damage:</b></td><td style='width:5em;'><b>Brute</b></td><td style='width:4em;'><b>Burn</b></td><td style='width:4em;'><b>Toxin</b></td><td style='width:8em;'><b>Suffocation</b></td></tr>");
        foreach (object zone in Dm.each(scanned.get_all_limbs())) //Same order every time for consistency across all humans
        {
            obj_item_bodypart limb = scanned.get_bodypart((string)zone) as obj_item_bodypart;
            if (limb == null)
            {
                dmgreport += "<tr>";
                dmgreport += Dm.text("<td><b>[]:</b></td>", capitalize(parse_zone(zone)));
                dmgreport += "<td>-</td>";
                dmgreport += "<td>-</td>";
                dmgreport += "</tr>";
                dmgreport += I18n.T("<tr><td colspan=6>&rdsh; Physical trauma: <u>Dismembered</u></td></tr>");
                continue;
            }
            float has_any_embeds = Dm.num(LAZYLEN(limb.embedded_objects) >= 1);
            float has_any_wounds = Dm.num(length(limb.wounds) >= 1);
            dmgreport += "<tr>";
            dmgreport += Dm.text("<td><b>[]:</b></td>", capitalize(limb.name));
            dmgreport += Dm.text("<td>[]</td>", limb.brute_dam > 0 ? (object)ceil(limb.brute_dam) : "0");
            dmgreport += Dm.text("<td>[]</td>", limb.burn_dam > 0 ? (object)ceil(limb.burn_dam) : "0");
            if (Dm.eq(zone, BODY_ZONE_CHEST)) // tox/oxy is stored in the chest
            {
                dmgreport += Dm.text("<td>[]</td>", tox_loss > 0 ? (object)ceil(tox_loss) : "0");
                dmgreport += Dm.text("<td>[]</td>", oxy_loss > 0 ? (object)ceil(oxy_loss) : "0");
            }
            dmgreport += "</tr>";
            if (Dm.truthy(has_any_embeds))
            {
                DmList embedded_names = Dm.list();
                foreach (obj_item embed in Dm.each_cast<obj_item>(limb.embedded_objects))
                {
                    object embed_key = capitalize(embed.name);
                    embedded_names[embed_key] = Dm.num(embedded_names[embed_key]) + 1;
                }
                foreach (object embedded_name in Dm.each(embedded_names))
                {
                    object displayed = embedded_name;
                    float embedded_amt = Dm.num(embedded_names[embedded_name]);
                    if (embedded_amt > 1)
                        displayed = I18n.T("[]x []", embedded_amt, embedded_name);
                    dmgreport += I18n.T("<tr><td colspan=6><i>&rdsh; Foreign object(s): [].</i></td></tr>", displayed);
                }
            }
            if (Dm.truthy(has_any_wounds))
            {
                foreach (datum_wound wound in Dm.each_cast<datum_wound>(limb.wounds))
                    dmgreport += I18n.T("<tr><td colspan=6><i>&rdsh; Physical trauma: [] ([]) - Caused by <u>[].</u></i></td></tr>", wound.name, wound.severity_text(), wound.wound_source);
            }
        }

        dmgreport += I18n.T("<tr><td><b>Overall:</b></td><td><b>[] Brute</b></td><td><b>[] Burn</b></td><td><b>[] Toxin</b></td><td><b>[] Suffocation</b></td></tr>",
            ceil(brute_loss), ceil(fire_loss), ceil(tox_loss), ceil(oxy_loss));
        dmgreport += "</table><hr>";
        Dm.append(ref autopsy_information, dmgreport);

        // Only humanoids list organs, implants, gene stability, species and core-temp
        if (ishuman(scanned))
        {
            mob_living_carbon_human humantarget = scanned as mob_living_carbon_human;
            /// "Organ Data" portion of the autopsy - damage, functional status (or if it's missing), and implants
            string organreport = I18n.T("<u><b>Organ Data:</b></u><table class='ml-2'><tr><td style='width:10em;'><b>Organ:</b></td><td style='width:6em;'><b>Dmg</b></td><td style='width:30em;'><b>Status</b></td></tr>");

            DmList missing_organs = humantarget.get_missing_organs() as DmList;
            foreach (object sorted_slot in Dm.each(GLOB.organ_process_order)) //Same order every time for consistency across all humans
            {
                obj_item_organ organ = humantarget.get_organ_slot(sorted_slot) as obj_item_organ;
                if (organ == null)
                {
                    if (Dm.truthy(missing_organs[sorted_slot]))
                        organreport += I18n.T("<tr><td><b>[]:</b></td><td>-</td><td><u>Missing</u></td></tr>", missing_organs[sorted_slot]);
                    continue;
                }
                object status = organ.get_status_text(scanpower: SCANPOWER_ADVANCED, add_tooltips: FALSE, colored: FALSE);
                object appendix = organ.get_status_appendix(scanpower: SCANPOWER_ADVANCED, add_tooltips: FALSE);
                if (!Dm.truthy(status))
                    status = I18n.T("OK"); // otherwise flawless organs have no status reported by default
                organreport += Dm.text("<tr><td><b>[]:</b></td><td>[]</td><td>[]</td></tr>",
                    capitalize(organ.name), Dm.num(organ.damage) > 0 ? (object)ceil(Dm.num(organ.damage)) : "0", status);
                if (Dm.truthy(appendix))
                    organreport += Dm.text("<tr><td colspan=4><i>&rdsh; []</i></td></tr>", appendix);
            }
            organreport += "</table>"; //<hr> comes after Cybernetics below
            Dm.append(ref autopsy_information, organreport);

            float mutant = Dm.num(HAS_TRAIT(humantarget, TRAIT_HULK)); //Also applied by genetics infusions
            // Cybernetics
            DmList cyberimps = null;
            foreach (obj_item_organ target_organ in Dm.each_cast<obj_item_organ>(humantarget.organs))
            {
                if (Dm.truthy(IS_ROBOTIC_ORGAN(target_organ)) && !Dm.truthy(Dm.bitand(target_organ.organ_flags, ORGAN_HIDDEN)))
                    LAZYADD(ref cyberimps, target_organ.examine_title(user));
                if (Dm.truthy(Dm.bitand(target_organ.organ_flags, ORGAN_MUTANT)))
                    mutant = TRUE;
            }
            if (Dm.truthy(LAZYLEN(cyberimps)))
            {
                Dm.append(ref autopsy_information, I18n.T("<b>Detected cybernetic modifications:</b></br>"));
                Dm.append(ref autopsy_information, Dm.text("[]</br>", english_list(cyberimps, and_text: I18n.T(", and "))));
            }

            Dm.append(ref autopsy_information, "<hr>");

            // Genetic Stability, Species, and Body Temperature
            if (Dm.truthy(humantarget.has_dna()) && humantarget.dna.stability != Dm.initial(humantarget.dna, (datum_dna p) => p.stability))
                Dm.append(ref autopsy_information, I18n.T("<b>Genetic Stability:</b> []%.</br>", humantarget.dna.stability));
            datum_species targetspecies = (datum_species)humantarget.dna.species;
            float disguised = Dm.num(!ishumanbasic(humantarget) && HAS_TRAIT(humantarget, TRAIT_HUMAN_DISGUISE));
            string species_name = I18n.T("[][]", Dm.truthy(disguised) ? I18n.T("\"[]\"", Dm.initial(typeof(datum_species_human), (datum_species_human p) => p.name)) : (object)targetspecies.name, Dm.truthy(mutant) ? I18n.T("-derived mutant") : "");
            Dm.append(ref autopsy_information, I18n.T("<b>Species:</b> []</br>", species_name));
            Dm.append(ref autopsy_information, I18n.T("<b>Core temperature:</b> [] &deg;C ([] &deg;F)</br>", round(humantarget.coretemperature - T0C, 0.1f), round(humantarget.coretemperature * 1.8f - 459.67f, 0.1f)));
        }
        // (End of humanoid-only information)
        Dm.append(ref autopsy_information, I18n.T("<b>Body temperature:</b> [] &deg;C ([] &deg;F)</br>", round(scanned.bodytemperature - T0C, 0.1f), round(scanned.bodytemperature * 1.8f - 459.67f, 0.1f)));

        // Blood Info
        if (HAS_TRAIT(scanned, TRAIT_HUSK))
        {
            Dm.append(ref autopsy_information, I18n.T("Subject is husked by: "));
            if (HAS_TRAIT_FROM(scanned, TRAIT_HUSK, CHANGELING_DRAIN))
                Dm.append(ref autopsy_information, I18n.T("Desiccation, commonly caused by Changelings.</br>"));
            else if (HAS_TRAIT_FROM(scanned, TRAIT_HUSK, SKELETON_TRAIT))
                Dm.append(ref autopsy_information, I18n.T("Stripped flesh.</br>"));
            else if (HAS_TRAIT_FROM(scanned, TRAIT_HUSK, Dm.initial(typeof(datum_status_effect_zombie), (datum_status_effect_zombie p) => p.id)))
                Dm.append(ref autopsy_information, I18n.T("Zombification.</br>"));
            else if (!HAS_TRAIT_FROM(scanned, TRAIT_HUSK, BURN)) // prioritize showing unknown causes over burns
                Dm.append(ref autopsy_information, I18n.T("Unknown causes.</br>"));
            else
                Dm.append(ref autopsy_information, I18n.T("Severe burns.</br>"));
        }

        datum_blood_type blood_type = scanned.get_bloodtype() as datum_blood_type;
        if (Dm.truthy(blood_type))
        {
            float cached_blood_volume = Dm.num(scanned.get_blood_volume(apply_modifiers: TRUE));
            float blood_percent = round((cached_blood_volume / BLOOD_VOLUME_NORMAL) * 100);
            object blood_type_format = null;
            string level_format = null;
            if (cached_blood_volume <= BLOOD_VOLUME_SAFE && cached_blood_volume > BLOOD_VOLUME_OKAY)
                level_format = I18n.T("LOW []%, [] cl", blood_percent, cached_blood_volume);
            else if (cached_blood_volume <= BLOOD_VOLUME_OKAY)
                level_format = I18n.T("<u>CRITICAL []%</u>, [] cl", blood_percent, cached_blood_volume);
            else
                level_format = I18n.T("[]%, [] cl", blood_percent, cached_blood_volume);
            if (Dm.truthy(blood_type.get_type()))
                blood_type_format = I18n.T("type: []", blood_type.get_type());
            Dm.append(ref autopsy_information, I18n.T("<b>[] level:</b> [], []</br>", blood_type.get_blood_name(), level_format, blood_type_format));
        }
        object blood_alcohol_content = scanned.get_blood_alcohol_content();
        if (Dm.num(blood_alcohol_content) > 0)
            Dm.append(ref autopsy_information, I18n.T("&rdsh; [] alcohol content: []%</br>", blood_type?.get_blood_name() is var or__1 && Dm.truthy(or__1) ? or__1 : I18n.T("Blood"), blood_alcohol_content));
        Dm.append(ref autopsy_information, "<hr>");

        Dm.append(ref autopsy_information, I18n.T("<u>Chemical Data:</u></br>"));
        foreach (datum_reagent scanned_reagents in Dm.each_cast<datum_reagent>(((datum_reagents)scanned.reagents).reagent_list))
        {
            if (Dm.truthy(Dm.bitand(scanned_reagents.chemical_flags, REAGENT_INVISIBLE)))
                continue;
            Dm.append(ref autopsy_information, I18n.T("<b>[]:</b> [] unit\\s in bloodstream.</br>", scanned_reagents.name, round(Dm.num(scanned_reagents.volume), 0.1f)));
            Dm.append(ref autopsy_information, Dm.text("<i>&rdsh; []</i></br>", scanned_reagents.description));
        }
        Dm.append(ref autopsy_information, "<hr>");

        Dm.append(ref autopsy_information, I18n.T("<u>Disease Data:</u></br>"));
        foreach (datum_disease diseases in Dm.each_cast<datum_disease>(scanned.diseases))
        {
            Dm.append(ref autopsy_information, I18n.T("<b>Disease Name:</b> \"[]\"</br>", diseases.name));
            Dm.append(ref autopsy_information, I18n.T("<b>Transmission Type:</b> []</br>", diseases.spread_text));
            if (!(diseases is datum_disease_advance))
                continue;
            Dm.append(ref autopsy_information, I18n.T("<b>Symptoms:</b></br>"));
            datum_disease_advance advanced_disease = diseases as datum_disease_advance;
            foreach (datum_symptom symptom in Dm.each_cast<datum_symptom>(advanced_disease.symptoms))
                Dm.append(ref autopsy_information, Dm.text("&rdsh; [] - <i>[]</i></br>", symptom.name, symptom.desc));
        }
        Dm.append(ref autopsy_information, "<hr>");

        Dm.append(ref autopsy_information, I18n.T("<b>Coroner's Notes:</b>")); //Bottom of the page, anything past here is player-written

        final_report_text += jointext(autopsy_information, "");
        autopsy_report.add_raw_text(final_report_text, advanced_html: TRUE);
        autopsy_report.update_appearance();
        await user.put_in_hands(autopsy_report);
        user.balloon_alert(user, I18n.T("report printed"));
        return TRUE;
    }
}
