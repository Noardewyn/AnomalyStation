// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/scanners/health_analyzer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: healthscan (M2 P3, the dev analyzer; M8 slice D: its organ table), the analyzer's item procs (modes, the scan with
// its flick and sound, the wound scan, the chemical scan), chemscan, woundscan and the simple (wound) analyzer (M8 slice D).
// The readout report's printing came with the paperwork (gaps S3); diseasescan and the disease analyzer with gaps DV2 (D333);
// the report's disease lines with the diseases (gaps VI1). The report's lines of later milestones are Dm.Deferred at the statement
// (husks M6, quirks M9, the death time's DisplayTimeText).

// tg: code/game/objects/items/devices/scanners/health_analyzer.dm:6
[DmPath("/obj/item/healthanalyzer")]
public partial class obj_item_healthanalyzer : obj_item
{
    // Describes the three modes of scanning available for health analyzers
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:2  #define SCANMODE_HEALTH 0 (file-local, #undef at :846)
    private const int SCANMODE_HEALTH = 0;
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:3  #define SCANMODE_WOUND 1 (file-local, #undef at :847)
    private const int SCANMODE_WOUND = 1;
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:4  #define SCANMODE_COUNT 2 (file-local, #undef at :848)
    private const int SCANMODE_COUNT = 2; // Update this to be the number of scan modes if you add more

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:635  #define AID_EMOTION_NEUTRAL "neutral" (file-local, #undef at :850; the simple analyzer and woundscan use them)
    internal const string AID_EMOTION_NEUTRAL = "neutral";
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:636  #define AID_EMOTION_HAPPY "happy"
    internal const string AID_EMOTION_HAPPY = "happy";
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:637  #define AID_EMOTION_WARN "cautious"
    internal const string AID_EMOTION_WARN = "cautious";
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:638  #define AID_EMOTION_ANGRY "angery"
    internal const string AID_EMOTION_ANGRY = "angery";
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:639  #define AID_EMOTION_SAD "sad"
    internal const string AID_EMOTION_SAD = "sad";

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_item_context();
        return dot;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:45
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.eq(mode, SCANNER_NO_MODE))
            Dm.append(ref dot, span_notice(I18n.T("Alt-click [] to toggle the limb damage readout. Ctrl-shift-click to print readout report.", this)));
        return dot;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:50
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins to analyze []self with []! The display shows that [] dead!", user, user.p_them(), this, user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:54
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(user.can_read(this)) || Dm.truthy(user.is_blind()))
            return null;

        scanmode = (scanmode + 1) % SCANMODE_COUNT;
        if (scanmode == SCANMODE_HEALTH)
            to_chat(user, span_notice(I18n.T("You switch the health analyzer to check physical health.")));
        else if (scanmode == SCANMODE_WOUND)
            to_chat(user, span_notice(I18n.T("You switch the health analyzer to report extra info on wounds.")));
        return null;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:65
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;

        mob_living M = interacting_with as mob_living;

        object dot = ITEM_INTERACT_SUCCESS;

        flick(Dm.text("[]-scan", icon_state), this); //makes it so that it plays the scan animation upon scanning, including clumsy scanning

        // Clumsiness/brain damage check
        if ((HAS_TRAIT(user, TRAIT_CLUMSY) || HAS_TRAIT(user, TRAIT_DUMB)) && prob(50))
        {
            turf scan_turf = get_turf(user);
            user.visible_message(
                span_warning(I18n.T("[] analyzes []'s vitals!", user, scan_turf)),
                span_notice(I18n.T("You stupidly try to analyze []'s vitals!", scan_turf)));

            string floor_text = I18n.T("<span class='info'>Analyzing results for <b>[]</b> ([]):</span><br>", scan_turf, round_timestamp());
            floor_text += I18n.T("<span class='info ml-1'>Overall status: <i>Unknown</i></span><br>");
            floor_text += I18n.T("<span class='alert ml-1'>Subject lacks a brain.</span><br>");
            object temperature = scan_turf == null ? null : (await scan_turf.return_air() as datum_gas_mixture)?.return_temperature();
            floor_text += I18n.T("<span class='info ml-1'>Body temperature: []</span><br>", Dm.truthy(temperature) ? temperature : "???");

            if (Dm.truthy(user.can_read(this)) && !Dm.truthy(user.is_blind()))
                to_chat(user, custom_boxed_message("blue_box", floor_text));
            last_scan_text = floor_text;
            return dot;
        }

        if (HAS_TRAIT(M, TRAIT_REQUIRED_ADV_HEALTH_SCANNER) && scanpower < SCANPOWER_ADVANCED)
        {
            to_chat(user, span_info(I18n.T("[]'s biological structure is too complex for the health analyzer.", M)));
            return dot;
        }

        user.visible_message(span_notice(I18n.T("[] analyzes []'s vitals.", user, M)));
        balloon_alert(user, I18n.T("analyzing vitals"));
        playsound(user.loc, Dm.resource("sound/items/healthanalyzer.ogg"), 50);

        bool readability_check = Dm.truthy(user.can_read(this)) && !Dm.truthy(user.is_blind());
        if (scanmode == SCANMODE_HEALTH)
        {
            last_scan_text = healthscan(user, M, mode, scanpower, tochat: Dm.num(readability_check));
            if (Dm.num(M.health) / Dm.num(M.maxHealth) > CLEAN_BILL_OF_HEALTH_RATIO)
                last_healthy_scanned = WEAKREF(M) as datum_weakref;
            else
                last_healthy_scanned = null;
        }
        else if (scanmode == SCANMODE_WOUND)
        {
            if (readability_check)
                woundscan(user, M as mob_living_carbon, this);
        }

        add_fingerprint(user);
        return dot;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:115
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;
        if (Dm.truthy(user.can_read(this)) && !Dm.truthy(user.is_blind()))
            chemscan(user, interacting_with as mob_living);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:122
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (!isliving(target))
            return NONE;

        if (scanmode == SCANMODE_HEALTH)
            context[SCREENTIP_CONTEXT_LMB] = "Scan health";
        else if (scanmode == SCANMODE_WOUND)
            context[SCREENTIP_CONTEXT_LMB] = "Scan wounds";

        context[SCREENTIP_CONTEXT_RMB] = "Scan chemicals";

        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:614
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.eq(mode, SCANNER_NO_MODE))
            return CLICK_ACTION_BLOCKING;

        mode = Dm.truthy(mode) ? 0 : 1;
        to_chat(user, Dm.eq(mode, SCANNER_VERBOSE) ? I18n.T("The scanner now shows specific limb damage.") : I18n.T("The scanner no longer shows limb damage."));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:480  (gaps S3: the printed report)
    public override async DmTask<object> click_ctrl_shift(mob user = null)
    {
        object dot = await base.click_ctrl_shift(user);
        if (!Dm.truthy(LAZYLEN(last_scan_text)))
        {
            balloon_alert(user, I18n.T("no scans!"));
            return dot;
        }
        if (Dm.truthy(scanner_busy))
        {
            balloon_alert(user, I18n.T("analyzer busy!"));
            return dot;
        }
        scanner_busy = TRUE;
        balloon_alert(user, I18n.T("printing report..."));
        _addtimer(Dm.@new<datum_callback>(this, nameof(print_report), user), 2 * SECONDS, file: "code/game/objects/items/devices/scanners/health_analyzer.dm", line: 490);
        return dot;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:492
    public virtual async DmTask print_report(mob user = null)
    {
        obj_item_paper_medical_report report_paper = Dm.@new<obj_item_paper_medical_report>(get_turf(this));

        report_paper.color = "#99ccff";
        report_paper.name = I18n.T("health scan report - []", server_timestamp(format: "hh:mm", ic_time: TRUE));
        object report_text = Dm.text("<center><B>Health scan report</br>Time of retrieval: []</B></center><HR>", UNDERLINED_HTML_TEXT(Dm.text("[]", server_timestamp(format: "hh:mm", ic_time: TRUE)), Dm.text("Shift Time: []", round_timestamp(format: "hh:mm"))));
        Dm.append(ref report_text, last_scan_text);

        report_paper.add_raw_text(report_text, advanced_html: TRUE);
        report_paper.update_appearance();

        await user.put_in_hands(report_paper);
        balloon_alert(user, I18n.T("logs cleared"));

        resolve_patient_eligibility(report_paper, user);
        scanner_busy = FALSE;
    }

    /**
     * Checks the mob and the medical report that the scanner is trying to print, checks the traits and statuses of the mob, and then resolves by true or false.
     * Applies traits to the patient if the scanning is eligable to turn in for a bounty, with callbacks to remove after a cooldown.
     */
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:514
    public virtual object resolve_patient_eligibility(obj_item_paper_medical_report report_paper = null, mob scanner = null)
    {
        mob_living patient = last_healthy_scanned?.resolve() as mob_living;
        if (patient == null)
            return FALSE;

        if (scanner == patient)
            return FALSE; //You can't just scan yourself.

        if (HAS_TRAIT(patient, TRAIT_RECENTLY_TREATED))
            return FALSE;

        report_paper.last_healthy_scanned_mob = last_healthy_scanned;
        ADD_TRAIT(patient, TRAIT_RECENTLY_TREATED, ANALYZER_TRAIT);
        // tg's own: the callback removes the trait named by the cooldown, so the recently treated trait stays
        _addtimer(TRAIT_CALLBACK_REMOVE(patient, RECENTLY_HEALED_COOLDOWN, ANALYZER_TRAIT), RECENTLY_HEALED_COOLDOWN, file: "code/game/objects/items/devices/scanners/health_analyzer.dm", line: 528);
        return TRUE;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:531
    public virtual object clear_treatment(mob_living target = null)
    {
        if (target == null)
            return null;
        if (QDELETED(target))
            return null;
        REMOVE_TRAIT(target, TRAIT_RECENTLY_TREATED, ANALYZER_TRAIT);
        return TRUE;
    }
}

// tg: code/game/objects/items/devices/scanners/health_analyzer.dm:835  (gaps S3)
public partial class obj_item_paper_medical_report : obj_item_paper
{
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:841
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (last_healthy_scanned_mob != null)
            Dm.append(ref dot, span_notice(I18n.T("This medical report is applicable for medical bounties.")));
        return dot;
    }
}

// tg: code/game/objects/items/devices/scanners/health_analyzer.dm:151 (global procs)
public static partial class GlobalProcs
{
    /**
     * healthscan
     * returns a list of everything a health scan should give to a player.
     * Examples of where this is used is Health Analyzer and the Physical Scanner tablet app.
     * Args:
     * user - The person with the scanner
     * target - The person being scanned
     * mode - Uses SCANNER_CONDENSED or SCANNER_VERBOSE to decide whether to give a list of all individual limb damage
     * advanced - Whether it will give more advanced details, such as husk source.
     * tochat - Whether to immediately post the result into the chat of the user, otherwise it will return the results.
     */
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:151
    public static object healthscan(mob user = null, mob_living target = null, float? mode = null, float? scanpower = null, float? tochat = null)
    {
        mode ??= SCANNER_VERBOSE;
        scanpower ??= SCANPOWER_BASIC;
        tochat ??= TRUE;
        object dot = null;
        if (Dm.truthy(user.incapacitated))
            return null;

        // the final list of strings to render
        DmList render_list = Dm.list();

        // Damage specifics
        float oxy_loss = Dm.num(target.get_oxy_loss());
        float tox_loss = Dm.num(target.get_tox_loss());
        float fire_loss = Dm.num(target.get_fire_loss());
        float brute_loss = Dm.num(target.get_brute_loss());
        // IS_DEAD_OR_FAKING(target) (__DEFINES/mobs.dm:1127), inline
        string mob_status = ((target.stat == DEAD || HAS_TRAIT(target, TRAIT_FAKEDEATH)) ? span_alert(I18n.T("<b>Deceased</b>")) : I18n.T("<b>[]% healthy</b>", round(Dm.num(Dm.div(target.health, target.maxHealth)), 0.01f) * 100));

        if (HAS_TRAIT(target, TRAIT_FAKEDEATH) && target.stat != DEAD)
        {
            // if we don't appear to actually be in a "dead state", add fake oxyloss
            if (oxy_loss + tox_loss + fire_loss + brute_loss < 200)
            {
                oxy_loss += 200 - (oxy_loss + tox_loss + fire_loss + brute_loss);
                oxy_loss = clamp(oxy_loss, 0, 200);
            }
        }

        Dm.append(ref render_list, I18n.T("[]<br><span class='info ml-1'>Overall status: []</span><br>", span_info(I18n.T("Analyzing results for <b>[]</b> ([]):", target, round_timestamp())), mob_status));

        // tg: if(!scanpower == SCANPOWER_BASIC && ...) (:174): DM parse: (!scanpower) == SCANPOWER_BASIC
        // && target.has_reagent(/datum/reagent/inverse/technetium): GAP(P2/M8): the reagent type is not declared (reagents,
        // M8), so no target has it and scanpower stays as given.
        if (Dm.num(!Dm.truthy(scanpower)) == SCANPOWER_BASIC && false)
            scanpower = SCANPOWER_ADVANCED;

        if (Dm.truthy(target._listen_lookup?[COMSIG_LIVING_HEALTHSCAN]))
            target._SendSignal(COMSIG_LIVING_HEALTHSCAN, Dm.list(target, render_list, scanpower, user, mode, tochat));

        // Husk detection
        if (HAS_TRAIT(target, TRAIT_HUSK))
        {
            // the husk lines (:181-195): their causes need /datum/status_effect/zombie and the husking sources (husks M6,
            // zombies M10); no M2 body is husked.
            Dm.Deferred("code/game/objects/items/devices/scanners/health_analyzer.dm:181", "healthscan: the husk lines (husks, M6)");
        }

        if (Dm.truthy(target.get_stamina_loss()))
        {
            if (Dm.num(scanpower) >= SCANPOWER_ADVANCED)
                Dm.append(ref render_list, I18n.T("<span class='alert ml-1'>Fatigue level: []%.</span><br>", target.get_stamina_loss()));
            else
                Dm.append(ref render_list, I18n.T("<span class='alert ml-1'>Subject appears to be suffering from fatigue.</span><br>"));
        }

        // Check for brain - both organic (carbon) and synthetic (cyborg MMI)
        float has_brain = FALSE;
        if (Dm.truthy(target.get_organ_slot(ORGAN_SLOT_BRAIN)))
            has_brain = TRUE;
        else if (iscyborg(target)) // the cyborg's MMI (silicons SI2)
        {
            mob_living_silicon_robot cyborg_target = (mob_living_silicon_robot)target;
            if ((cyborg_target.mmi as obj_item_brain_processor_organic)?.brain != null)
                has_brain = TRUE;
        }

        if (!Dm.truthy(has_brain)) // kept exclusively for soul purposes
            Dm.append(ref render_list, I18n.T("<span class='alert ml-1'>Subject lacks a brain.</span><br>"));

        if (target is mob_living_carbon) // iscarbon(target) (__DEFINES/is_helpers.dm): no member yet, inline
        {
            mob_living_carbon carbontarget = target as mob_living_carbon;
            if (Dm.truthy(LAZYLEN(carbontarget.quirks)))
            {
                // the quirk lines (:215-218, get_quirk_string): quirks are M9
                Dm.Deferred("code/game/objects/items/devices/scanners/health_analyzer.dm:215", "healthscan: the quirk lines (quirks, M10+)");
            }
        }

        // Body part damage report
        if (target is mob_living_carbon) // iscarbon(target) (__DEFINES/is_helpers.dm): no member yet, inline
        {
            mob_living_carbon carbontarget = target as mob_living_carbon;
            float any_damage = Dm.num(brute_loss > 0 || fire_loss > 0 || oxy_loss > 0 || tox_loss > 0 || fire_loss > 0);
            float any_missing = length(carbontarget.get_missing_limbs());
            float any_wounded = length(carbontarget.all_wounds);
            object any_embeds = carbontarget.has_embedded_objects();
            if (Dm.truthy(any_damage) || (Dm.num(mode) == SCANNER_VERBOSE && (Dm.truthy(any_missing) || Dm.truthy(any_wounded) || Dm.truthy(any_embeds))))
            {
                Dm.append(ref render_list, "<hr>");
                string dmgreport = I18n.T("<span class='info ml-1'>Body status:</span><font face='Verdana'><table class='ml-2'><tr><td style='width:7em;'><font color='#ff0000'><b>Damage:</b></font></td><td style='width:5em;'><font color='#ff3333'><b>Brute</b></font></td><td style='width:4em;'><font color='#ff9933'><b>Burn</b></font></td><td style='width:4em;'><font color='#00cc66'><b>Toxin</b></font></td><td style='width:8em;'><font color='#00cccc'><b>Suffocation</b></font></td></tr><tr><td><font color='#ff3333'><b>Overall:</b></font></td><td><font color='#ff3333'><b>[]</b></font></td><td><font color='#ff9933'><b>[]</b></font></td><td><font color='#00cc66'><b>[]</b></font></td><td><font color='#33ccff'><b>[]</b></font></td></tr>", ceil(brute_loss), ceil(fire_loss), ceil(tox_loss), ceil(oxy_loss));

                if (Dm.num(mode) == SCANNER_VERBOSE)
                {
                    // Follow same body zone list every time so it's consistent across all humans
                    foreach (object zone in Dm.each(carbontarget.get_all_limbs()))
                    {
                        obj_item_bodypart limb = carbontarget.get_bodypart((string)zone) as obj_item_bodypart;
                        if (limb == null)
                        {
                            dmgreport += "<tr>";
                            dmgreport += I18n.T("<td><font color='#cc3333'>[]:</font></td>", capitalize(parse_zone(zone)));
                            dmgreport += "<td><font color='#cc3333'>-</font></td>";
                            dmgreport += "<td><font color='#ff9933'>-</font></td>";
                            dmgreport += "</tr>";
                            // conditional_tooltip(normal_text, tooltip_text, condition) (__DEFINES/chat.dm:58), inline
                            dmgreport += I18n.T("<tr><td colspan=6><span class='alert ml-2'>&rdsh; Physical trauma: []</span></td></tr>", Dm.truthy(tochat) ? span_tooltip(I18n.T("Reattach or replace surgically."), I18n.T("Dismembered")) : I18n.T("Dismembered"));
                            continue;
                        }
                        float has_any_embeds = Dm.num(LAZYLEN(limb.embedded_objects) >= 1);
                        float has_any_wounds = Dm.num(length(limb.wounds) >= 1);
                        float is_damaged = Dm.num(limb.burn_dam > 0 || limb.brute_dam > 0);
                        if (!Dm.truthy(is_damaged) && (!Dm.eq(zone, BODY_ZONE_CHEST) || (tox_loss <= 0 && oxy_loss <= 0)) && !Dm.truthy(has_any_embeds) && !Dm.truthy(has_any_wounds))
                            continue;
                        dmgreport += "<tr>";
                        // capitalize(t) (__HELPERS/text.dm:374): GAP(lead, __HELPERS/text.cs) no mirror yet; its body
                        // (uppertext of the first character and the rest) written inline
                        string part_name = (Dm.truthy(Dm.bitand(limb.bodytype, BODYTYPE_ROBOTIC)) ? limb.name : Dm.text("[]", limb.plaintext_zone));
                        string capitalized = Dm.truthy(part_name) ? uppertext(part_name.Substring(0, 1)) + copytext(part_name, 2) : part_name;
                        dmgreport += I18n.T("<td><font color='#cc3333'>[]:</font></td>", capitalized);
                        dmgreport += I18n.T("<td><font color='#cc3333'>[]</font></td>", limb.brute_dam > 0 ? (object)ceil(limb.brute_dam) : "0");
                        dmgreport += I18n.T("<td><font color='#ff9933'>[]</font></td>", limb.burn_dam > 0 ? (object)ceil(limb.burn_dam) : "0");
                        if (Dm.eq(zone, BODY_ZONE_CHEST)) // tox/oxy is stored in the chest
                        {
                            dmgreport += I18n.T("<td><font color='#00cc66'>[]</font></td>", tox_loss > 0 ? (object)ceil(tox_loss) : "0");
                            dmgreport += I18n.T("<td><font color='#33ccff'>[]</font></td>", oxy_loss > 0 ? (object)ceil(oxy_loss) : "0");
                        }
                        dmgreport += "</tr>";
                        if (Dm.truthy(has_any_embeds))
                        {
                            DmList embedded_names = Dm.list();
                            foreach (obj_item embed in Dm.each_cast<obj_item>(limb.embedded_objects))
                            {
                                // capitalize(embed.name), inline as above
                                string embed_name = Dm.text("[]", embed.name);
                                string embed_key = Dm.truthy(embed_name) ? uppertext(embed_name.Substring(0, 1)) + copytext(embed_name, 2) : embed_name;
                                embedded_names[embed_key] = Dm.num(embedded_names[embed_key]) + 1;
                            }
                            foreach (object embedded_name in Dm.each(embedded_names))
                            {
                                string displayed = Dm.text("[]", embedded_name);
                                float embedded_amt = Dm.num(embedded_names[embedded_name]);
                                if (embedded_amt > 1)
                                    displayed = I18n.T("[]x []", embedded_amt, embedded_name);
                                // conditional_tooltip(normal_text, tooltip_text, condition) (__DEFINES/chat.dm:58), inline
                                dmgreport += I18n.T("<tr><td colspan=6><span class='alert ml-2'>&rdsh; Foreign object(s): []</span></td></tr>", Dm.truthy(tochat) ? span_tooltip(I18n.T("Use a hemostat to remove."), displayed) : displayed);
                            }
                        }
                        if (Dm.truthy(has_any_wounds))
                        {
                            foreach (datum_wound wound in Dm.each_cast<datum_wound>(limb.wounds))
                            {
                                // conditional_tooltip(normal_text, tooltip_text, condition) (__DEFINES/chat.dm:58), inline
                                string wound_text = I18n.T("[] ([])", wound.name, wound.severity_text());
                                dmgreport += I18n.T("<tr><td colspan=6><span class='alert ml-2'>&rdsh; Physical trauma: []</span></td></tr>", Dm.truthy(tochat) ? span_tooltip(wound.treat_text_short, wound_text) : wound_text);
                            }
                        }
                    }
                }

                dmgreport += "</table></font>";
                Dm.append(ref render_list, dmgreport); // tables do not need extra linebreak
            }
        }

        if (ishuman(target))
        {
            mob_living_carbon_human humantarget = target as mob_living_carbon_human;

            // Organ damage, missing organs
            bool render = false;
            string toReport = I18n.T("<span class='info ml-1'>Organ status:</span><font face='Verdana'><table class='ml-2'><tr><td style='width:8em;'><font color='#ff0000'><b>Organ:</b></font></td>[]<td style='width:30em;'><font color='#ff0000'><b>Status</b></font></td></tr>",
                Dm.num(scanpower) >= SCANPOWER_ADVANCED ? I18n.T("<td style='width:4em;'><font color='#ff0000'><b>Dmg</b></font></td>") : "");

            DmList missing_organs = humantarget.get_missing_organs() as DmList;
            // Follow same order as in the organ_process_order so it's consistent across all humans
            foreach (object sorted_slot in Dm.each(GLOB.organ_process_order))
            {
                obj_item_organ organ = humantarget.get_organ_slot(sorted_slot) as obj_item_organ;
                if (organ == null)
                {
                    if (Dm.truthy(missing_organs[sorted_slot]))
                    {
                        render = true;
                        toReport += I18n.T("<tr><td><font color='#cc3333'>[]:</font></td>[]<td><font color='#cc3333'>Missing</font></td></tr>",
                            missing_organs[sorted_slot], Dm.num(scanpower) >= SCANPOWER_ADVANCED ? "<td><font color='#ff3333'>-</font></td>" : "");
                    }
                    continue;
                }
                if (!Dm.eq(mode, SCANNER_VERBOSE) && !Dm.truthy(organ.show_on_condensed_scans()))
                    continue;
                object status = organ.get_status_text(scanpower, tochat);
                object appendix = organ.get_status_appendix(scanpower, tochat);
                if (Dm.truthy(status) || Dm.truthy(appendix))
                {
                    if (!Dm.truthy(status))
                        status = "<font color='#ffcc33'>OK</font>"; // otherwise flawless organs have no status reported by default
                    render = true;
                    toReport += Dm.text("<tr><td><font color='#cc3333'>[]:</font></td>[]<td>[]</td></tr>",
                        capitalize(organ.name), Dm.num(scanpower) >= SCANPOWER_ADVANCED ? Dm.text("<td><font color='#ff3333'>[]</font></td>", Dm.num(organ.damage) > 0 ? ceil(Dm.num(organ.damage)) : "0") : "", status);
                    if (Dm.truthy(appendix))
                        toReport += Dm.text("<tr><td colspan=4><span class='alert ml-2'>&rdsh; []</span></td></tr>", appendix);
                }
            }

            if (render)
            {
                Dm.append(ref render_list, "<hr>");
                Dm.append(ref render_list, toReport + "</table></font>"); // tables do not need extra linebreak
            }

            // Cybernetics & mutant
            float mutant = Dm.num(HAS_TRAIT(humantarget, TRAIT_HULK));
            foreach (obj_item_organ target_organ in Dm.each_cast<obj_item_organ>(humantarget.organs))
            {
                // IS_ROBOTIC_ORGAN(target_organ) && !(organ_flags & ORGAN_HIDDEN): LAZYADD(cyberimps, target_organ.examine_title(user))
                // (:330-331): cybernetic organs and examine_title are M8/M9; M2's organs are organic
                if (Dm.truthy(Dm.bitand(target_organ.organ_flags, ORGAN_MUTANT)))
                    mutant = TRUE;
            }

            Dm.append(ref render_list, "<hr>");

            //Genetic stability
            if (Dm.num(scanpower) >= SCANPOWER_ADVANCED && Dm.truthy(humantarget.has_dna()) && humantarget.dna.stability != Dm.initial(humantarget.dna, (datum_dna p) => p.stability))
                Dm.append(ref render_list, I18n.T("<span class='info ml-1'>Genetic Stability: []%.</span><br>", humantarget.dna.stability));

            //body temperature
            datum_species targetspecies = (datum_species)humantarget.dna.species;
            // !ishumanbasic(humantarget) && HAS_TRAIT(humantarget, TRAIT_HUMAN_DISGUISE): M2's humans are basic humans (Q7)
            float disguised = Dm.num(!(targetspecies is datum_species_human) && HAS_TRAIT(humantarget, TRAIT_HUMAN_DISGUISE));
            string species_name = I18n.T("[][]", Dm.truthy(disguised) ? I18n.T("\"[]\"", Dm.initial(typeof(datum_species_human), (datum_species_human p) => p.name)) : (object)targetspecies.name, Dm.truthy(mutant) ? I18n.T("-derived mutant") : "");

            Dm.append(ref render_list, I18n.T("<span class='info ml-1'>Species: []</span><br>", species_name));
            string core_temperature_message = I18n.T("Core temperature: [] &deg;C ([] &deg;F)", round(humantarget.coretemperature - T0C, 0.1f), round(humantarget.coretemperature * 1.8f - 459.67f, 0.1f));
            if (Dm.ge(humantarget.coretemperature, humantarget.get_body_temp_heat_damage_limit()))
                Dm.append(ref render_list, I18n.T("<span class='alert ml-1'>☼ [] ☼</span><br>", core_temperature_message));
            else if (Dm.le(humantarget.coretemperature, humantarget.get_body_temp_cold_damage_limit()))
                Dm.append(ref render_list, I18n.T("<span class='alert ml-1'>❄ [] ❄</span><br>", core_temperature_message));
            else
                Dm.append(ref render_list, I18n.T("<span class='info ml-1'>[]</span><br>", core_temperature_message));
        }

        string body_temperature_message = I18n.T("Body temperature: [] &deg;C ([] &deg;F)", round(target.bodytemperature - T0C, 0.1f), round(target.bodytemperature * 1.8f - 459.67f, 0.1f));
        if (Dm.ge(target.bodytemperature, target.get_body_temp_heat_damage_limit()))
            Dm.append(ref render_list, I18n.T("<span class='alert ml-1'>☼ [] ☼</span><br>", body_temperature_message));
        else if (Dm.le(target.bodytemperature, target.get_body_temp_cold_damage_limit()))
            Dm.append(ref render_list, I18n.T("<span class='alert ml-1'>❄ [] ❄</span><br>", body_temperature_message));
        else
            Dm.append(ref render_list, I18n.T("<span class='info ml-1'>[]</span><br>", body_temperature_message));

        // Blood Level
        datum_blood_type blood_type = target.get_bloodtype() as datum_blood_type;
        if (Dm.truthy(blood_type))
        {
            float cached_blood_volume = Dm.num(target.get_blood_volume(apply_modifiers: TRUE));
            float blood_percent = round((cached_blood_volume / BLOOD_VOLUME_NORMAL) * 100);
            object blood_type_format = null;
            string level_format = null;
            if (cached_blood_volume <= BLOOD_VOLUME_SAFE && cached_blood_volume > BLOOD_VOLUME_OKAY)
            {
                level_format = I18n.T("LOW []%, [] cl", blood_percent, cached_blood_volume);
                if (Dm.truthy(blood_type.restoration_chem))
                {
                    // conditional_tooltip(normal_text, tooltip_text, condition) (__DEFINES/chat.dm:58), inline
                    string tooltip__1 = I18n.T("Recommendation: [] supplement.", Dm.initial(blood_type.restoration_chem, (datum_reagent p) => p.name));
                    level_format = Dm.truthy(tochat) ? span_tooltip(tooltip__1, level_format) : level_format;
                }
            }
            else if (cached_blood_volume <= BLOOD_VOLUME_OKAY)
            {
                level_format = I18n.T("<b>CRITICAL []%</b>, [] cl", blood_percent, cached_blood_volume);
                DmList recommendation = Dm.list();
                if (Dm.truthy(blood_type.restoration_chem))
                    Dm.append(ref recommendation, I18n.T("[] supplement", Dm.initial(blood_type.restoration_chem, (datum_reagent p) => p.name)));
                if (blood_type.restoration_chem == typeof(datum_reagent_iron))
                {
                    // "[/datum/reagent/medicine/salglu_solution::name]": GAP(P2/M8): the reagent type is not declared (reagents,
                    // M8); its name is written as tg's var default (reagents/medicine_reagents.dm: name = "Saline-Glucose Solution").
                    Dm.append(ref recommendation, "Saline-Glucose Solution");
                }
                if (Dm.truthy(length(recommendation)))
                    Dm.append(ref recommendation, I18n.T("[] transfusion", blood_type.get_blood_name()));
                else
                    Dm.append(ref recommendation, I18n.T("immediate [] transfusion", blood_type.get_blood_name()));
                string tooltip__2 = I18n.T("Recommendation: [].", english_list(recommendation, and_text: " or "));
                level_format = Dm.truthy(tochat) ? span_tooltip(tooltip__2, level_format) : level_format; // conditional_tooltip, inline
            }
            else
                level_format = I18n.T("[]%, [] cl", blood_percent, cached_blood_volume);

            if (Dm.truthy(blood_type.get_type()))
            {
                blood_type_format = Dm.text("[]", blood_type.get_type());
                if (Dm.truthy(tochat) && Dm.truthy(length(blood_type.compatible_types)))
                {
                    DmList compatible_types_readable = Dm.list();
                    foreach (object comp_blood_type in Dm.each(blood_type.compatible_types))
                        Dm.combine(ref compatible_types_readable, Dm.initial(comp_blood_type, "name"));
                    blood_type_format = span_tooltip(I18n.T("Can receive from types [].", english_list(compatible_types_readable)), (string)blood_type_format);
                }
            }

            Dm.append(ref render_list, I18n.T("<span class='[] ml-1'>[] level: []</span> <span class='info'>[]</span><br>", cached_blood_volume < BLOOD_VOLUME_SAFE ? "alert" : "info", blood_type.get_blood_name(), level_format, blood_type_format));
        }

        object blood_alcohol_content = target.get_blood_alcohol_content();
        if (Dm.num(blood_alcohol_content) > 0)
        {
            object blood_name = (blood_type?.get_blood_name() is var or__1 && Dm.truthy(or__1) ? or__1 : "Blood");
            if (Dm.num(blood_alcohol_content) >= 0.24f)
            {
                // "Oil alcohol content" is kinda funny if you think about it from a technical standpoint
                Dm.append(ref render_list, I18n.T("<span class='alert ml-1'>[] alcohol content: <b>CRITICAL []%</b></span><br>", blood_name, blood_alcohol_content));
            }
            else
                Dm.append(ref render_list, I18n.T("<span class='info ml-1'>[] alcohol content: []%</span><br>", blood_name, blood_alcohol_content));
        }

        // Ethereal Charge (:419-438): /obj/item/organ/stomach/ethereal is not declared (M2's species are narrowed to the
        // human, plan Q7); a human's stomach is never one.
        object stomach__1 = target.get_organ_slot(ORGAN_SLOT_STOMACH);
        if (stomach__1 != null && DmTypes.PathOf(stomach__1.GetType()) is string stomach_path && stomach_path.StartsWith("/obj/item/organ/stomach/ethereal", StringComparison.Ordinal))
            Dm.NotTranslated("code/game/objects/items/devices/scanners/health_analyzer.dm:420", "healthscan: an ethereal's electrical charge (other species, later)");

        //Diseases
        bool disease_hr = false;
        foreach (datum_disease disease in Dm.each_cast<datum_disease>(target.diseases)) // gaps VI1
        {
            if (Dm.truthy(Dm.bitand(disease.visibility_flags, HIDDEN_SCANNER)))
                continue;
            if (!disease_hr)
            {
                Dm.append(ref render_list, "<hr>");
                disease_hr = true;
            }
            object cure_text;
            if (disease is datum_disease_advance)
            {
                datum_disease_advance advanced_disease = (datum_disease_advance)disease;
                float cure_count = 2;
                if (scanpower == SCANPOWER_ADVANCED)
                    cure_count = 3;
                else if (scanpower == SCANPOWER_SUPER)
                    cure_count = INFINITY;
                cure_text = advanced_disease.generate_cure_text(cure_count);
            }
            else
                cure_text = disease.cure_text;
            Dm.append(ref render_list, I18n.T("<span class='alert ml-1'>[]<br><div class='ml-2'>Name: [].<br>Type: [].<br>Stage: []/[].<br>Cure: []</div></span>",
                conditional_tooltip(I18n.T("<b>Warning: [] detected</b>", disease.form), I18n.T("Supply listed cure or [], or treat with food and rest.", Dm.initial(typeof(datum_reagent_medicine_spaceacillin), (datum_reagent r) => r.name)), tochat),
                disease.name, disease.spread_text, disease.stage, disease.max_stages, cure_text));
        }

        // Time of death
        if (Dm.truthy(target.station_timestamp_timeofdeath) && (target.stat == DEAD || HAS_TRAIT(target, TRAIT_FAKEDEATH)))
        {
            Dm.append(ref render_list, "<hr>");
            Dm.append(ref render_list, I18n.T("<span class='info ml-1'>Time of Death: []</span><br>", target.station_timestamp_timeofdeath));
            // "Subject died [DisplayTimeText(round(world.time - target.timeofdeath))] ago." (:472): GAP(lead, __HELPERS/time.cs):
            // DisplayTimeText (__HELPERS/time.dm:79) has no mirror yet.
            Dm.Deferred("code/game/objects/items/devices/scanners/health_analyzer.dm:472", "healthscan: \"Subject died ... ago\" (DisplayTimeText has no mirror yet; examine texts, M10+)");
        }

        dot = jointext(render_list, "");
        if (Dm.truthy(tochat))
        {
            // custom_boxed_message("blue_box", .) (__DEFINES/chat.dm:50), inline
            to_chat(user, "<div class='boxed_message " + "blue_box" + "'>" + (string)dot + "</div>", trailing_newline: FALSE, type: MESSAGE_TYPE_INFO);
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:540
    public static object chemscan(mob user = null, mob_living target = null, object reagent_types_to_check = null, float? tochat = null)
    {
        tochat ??= TRUE;
        if (Dm.truthy(user.incapacitated))
            return null;

        if (target == null || target.reagents is not datum_reagents blood)
            return null;

        DmList render_list = Dm.list(); //The master list of readouts, including reagents in the blood/stomach, addictions, quirks, etc.
        DmList render_block = Dm.list(); //A second block of readout strings. If this ends up empty after checking stomach/blood contents, we give the "empty" header.

        // Blood reagents
        if (Dm.truthy(length(blood.reagent_list)))
        {
            foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(blood.reagent_list))
            {
                if (Dm.truthy(Dm.bitand(reagent.chemical_flags, REAGENT_INVISIBLE))) //Don't show hidden chems on scanners
                    continue;
                if (Dm.truthy(reagent_types_to_check) && !istype(reagent, (Type)reagent_types_to_check))
                    continue;
                Dm.append(ref render_block, I18n.T("<span class='notice ml-2'>[] units of [][]<br>", round(Dm.num(reagent.volume), 0.001f), reagent.name, Dm.truthy(reagent.overdosed) ? "</span> - " + span_bolddanger(I18n.T("OVERDOSING")) : ".</span>"));
            }
        }

        object or__1 = LOWER_TEXT((target.get_bloodtype() as datum_blood_type)?.get_blood_name());
        string blood_word = Dm.truthy(or__1) ? Dm.str(or__1) : I18n.T("blood");
        if (!Dm.truthy(length(render_block))) //If no VISIBLY DISPLAYED reagents are present, we report as if there is nothing.
        {
            Dm.append(ref render_list, I18n.T("<span class='notice ml-1'>Subject contains no reagents in their []stream.</span><br>", blood_word));
        }
        else
        {
            Dm.append(ref render_list, I18n.T("<span class='notice ml-1'>Subject contains the following reagents in their []stream:</span><br>", blood_word));
            Dm.append(ref render_list, render_block); //Otherwise, we add the header, reagent readouts, and clear the readout block for use on the stomach.
            render_block.Cut();
        }

        // Stomach reagents
        obj_item_organ_stomach belly = target.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach;
        if (belly != null)
        {
            datum_reagents belly_reagents = belly.reagents as datum_reagents;
            if (Dm.truthy(length(belly_reagents.reagent_list)))
            {
                foreach (datum_reagent bit in Dm.each_cast<datum_reagent>(belly_reagents.reagent_list))
                {
                    if (Dm.truthy(Dm.bitand(bit.chemical_flags, REAGENT_INVISIBLE)))
                        continue;
                    if (Dm.truthy(reagent_types_to_check) && !istype(bit, (Type)reagent_types_to_check))
                        continue;
                    if (!Dm.truthy(belly.food_reagents?[bit.type]))
                    {
                        Dm.append(ref render_block, I18n.T("<span class='notice ml-2'>[] units of [][]<br>", round(Dm.num(bit.volume), 0.001f), bit.name, Dm.truthy(bit.overdosed) ? "</span> - " + span_bolddanger(I18n.T("OVERDOSING")) : ".</span>"));
                    }
                    else
                    {
                        float bit_vol = Dm.num(bit.volume) - Dm.num(belly.food_reagents[bit.type]);
                        if (bit_vol > 0)
                            Dm.append(ref render_block, I18n.T("<span class='notice ml-2'>[] units of [][]<br>", round(bit_vol, 0.001f), bit.name, Dm.truthy(bit.overdosed) ? "</span> - " + span_bolddanger(I18n.T("OVERDOSING")) : ".</span>"));
                    }
                }
            }

            if (!Dm.truthy(length(render_block)))
            {
                Dm.append(ref render_list, I18n.T("<span class='notice ml-1'>Subject contains no reagents in their stomach.</span><br>"));
            }
            else
            {
                Dm.append(ref render_list, I18n.T("<span class='notice ml-1'>Subject contains the following reagents in their stomach:</span><br>"));
                Dm.append(ref render_list, render_block);
            }
        }

        // Addictions
        if (Dm.truthy(LAZYLEN(target.mind?.active_addictions)))
        {
            Dm.append(ref render_list, I18n.T("<span class='boldannounce ml-1'>Subject is addicted to the following types of drug:</span><br>"));
            foreach (object addiction_type in Dm.each(target.mind.active_addictions))
                Dm.append(ref render_list, I18n.T("<span class='alert ml-2'>[]</span><br>", Dm.initial(addiction_type, (datum_addiction a) => a.name)));
        }

        // Special eigenstasium addiction
        if (Dm.truthy(target.has_status_effect(typeof(datum_status_effect_eigenstasium))))
            Dm.append(ref render_list, I18n.T("<span class='notice ml-1'>Subject is temporally unstable. Stabilising agent is recommended to reduce disturbances.</span><br>"));

        // Allergies
        foreach (datum_quirk quirky in Dm.each_cast<datum_quirk>(target.quirks))
        {
            if (quirky is datum_quirk_item_quirk_allergic allergies_quirk)
            {
                object allergies = allergies_quirk.allergy_string;
                Dm.append(ref render_list, I18n.T("<span class='alert ml-1'>Subject is extremely allergic to the following chemicals:</span><br>"));
                Dm.append(ref render_list, I18n.T("<span class='alert ml-2'>[]</span><br>", allergies));
            }
        }

        // we handled the last <br> so we don't need handholding
        if (Dm.truthy(tochat))
        {
            to_chat(user, custom_boxed_message("blue_box", jointext(render_list, "")), trailing_newline: FALSE, type: MESSAGE_TYPE_INFO);
            return null;
        }
        return jointext(render_list, "");
    }

    /// Displays wounds with extended information on their status vs medscanners
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:642
    public static void woundscan(mob user = null, mob_living_carbon patient = null, obj_item_healthanalyzer scanner = null, float? simple_scan = null)
    {
        simple_scan ??= FALSE;
        if (patient == null || Dm.truthy(user.incapacitated))
            return;

        string render_list = "";
        bool advised = false;
        foreach (obj_item_bodypart wounded_part in Dm.each_cast<obj_item_bodypart>(patient.get_wounded_bodyparts()))
        {
            render_list += I18n.T("<span class='alert ml-1'><b>Warning: Physical trauma[] detected in []</b>", LAZYLEN(wounded_part.wounds) > 1 ? "s" : "", wounded_part.name);
            foreach (datum_wound current_wound in Dm.each_cast<datum_wound>(wounded_part.wounds))
            {
                render_list += Dm.text("<div class='ml-2'>[]</div><br>", Dm.truthy(simple_scan) ? current_wound.get_simple_scanner_description() : current_wound.get_scanner_description());
                if (Dm.truthy(scanner.give_wound_treatment_bonus))
                {
                    ADD_TRAIT(current_wound, TRAIT_WOUND_SCANNED, ANALYZER_TRAIT);
                    if (!advised)
                    {
                        to_chat(user, span_notice(I18n.T("You notice how bright holo-images appear over your []. They seem to be filled with helpful information, this should make treatment easier!", (Dm.truthy(length(wounded_part.wounds)) ? Dm.num(length(wounded_part.wounds)) : Dm.num(length(patient.get_wounded_bodyparts()))) > 1 ? I18n.T("various wounds") : I18n.T("wound"))));
                        advised = true;
                    }
                }
            }
            render_list += "</span>";
        }

        if (render_list == "")
        {
            if (Dm.truthy(simple_scan))
            {
                obj_item_healthanalyzer_simple simple_scanner = scanner as obj_item_healthanalyzer_simple;
                // Only emit the cheerful scanner message if this scan came from a scanner
                playsound(simple_scanner, Dm.resource("sound/machines/ping.ogg"), 50, FALSE);
                to_chat(user, span_notice(I18n.T("\\The [] makes a happy ping and briefly displays a smiley face with several exclamation points! It's really excited to report that [] has no wounds!", simple_scanner, patient)));
                simple_scanner.show_emotion(obj_item_healthanalyzer.AID_EMOTION_HAPPY);
            }
            to_chat(user, I18n.T("<span class='notice ml-1'>No wounds detected in subject.</span>"));
        }
        else
        {
            to_chat(user, custom_boxed_message("blue_box", render_list), type: MESSAGE_TYPE_INFO);
            if (Dm.truthy(simple_scan))
            {
                obj_item_healthanalyzer_simple simple_scanner = scanner as obj_item_healthanalyzer_simple;
                simple_scanner.show_emotion(obj_item_healthanalyzer.AID_EMOTION_WARN);
                playsound(simple_scanner, Dm.resource("sound/machines/beep/twobeep.ogg"), 50, FALSE);
            }
        }
    }

    /// Checks the individual for any diseases that are visible to the scanner, and displays the diseases in the attacked to the attacker.
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:811  (gaps DV2, with the disease analyzer)
    public static void diseasescan(mob user = null, mob_living_carbon patient = null, obj_item_healthanalyzer_simple scanner = null)
    {
        if (patient is not mob_living_carbon || Dm.truthy(user.incapacitated))
            return;

        DmList render = Dm.list();
        foreach (datum_disease disease in Dm.each_cast<datum_disease>(patient.diseases))
        {
            if (!Dm.truthy(Dm.bitand(disease.visibility_flags, HIDDEN_SCANNER)))
            {
                object disease_cure = disease.cure_text;
                if (disease is datum_disease_advance advanced_disease)
                    disease_cure = advanced_disease.generate_cure_text(1);
                Dm.append(ref render, I18n.T("<span class='alert ml-1'><b>Warning: [] detected</b><br><div class='ml-2'>Name: [].<br>Type: [].<br>Stage: []/[].<br>Cure: []</div></span>",
                    disease.form, disease.name, disease.spread_text, disease.stage, disease.max_stages, disease_cure));
            }
        }

        if (!Dm.truthy(length(render)))
        {
            playsound(scanner, Dm.resource("sound/machines/ping.ogg"), 50, FALSE);
            to_chat(user, span_notice(I18n.T("\\The [] makes a happy ping and briefly displays a smiley face with several exclamation points! It's really excited to report that [] has no diseases!", scanner, patient)));
            scanner.emotion = obj_item_healthanalyzer.AID_EMOTION_HAPPY;
        }
        else
        {
            to_chat(user, span_notice(render.Join("")));
            scanner.emotion = obj_item_healthanalyzer.AID_EMOTION_WARN;
            playsound(scanner, Dm.resource("sound/machines/beep/twobeep.ogg"), 50, FALSE);
        }
    }
}

// tg: code/game/objects/items/devices/scanners/health_analyzer.dm:677
[DmPath("/obj/item/healthanalyzer/simple")]
public partial class obj_item_healthanalyzer_simple : obj_item_healthanalyzer
{
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:696
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.num(next_encouragement) < world.time)
        {
            playsound(this, Dm.resource("sound/machines/ping.ogg"), 50, FALSE);
            to_chat(user, span_notice(I18n.T("[] makes a happy ping and []!", this, Dm.pick(encouragements))));
            next_encouragement = world.time + 10 * SECONDS;
            show_emotion(AID_EMOTION_HAPPY);
        }
        else if (emotion != AID_EMOTION_ANGRY)
        {
            greed_warning(user);
        }
        else
        {
            await violence(user);
        }
        return null;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:707
    public virtual void greed_warning(mob user = null)
    {
        to_chat(user, span_warning(I18n.T("[] displays an eerily high-definition frowny face, chastizing you for asking it for too much encouragement.", this)));
        show_emotion(AID_EMOTION_ANGRY);
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:711
    public virtual async DmTask violence(mob user = null)
    {
        playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
        if (user is mob_living L)
        {
            to_chat(L, span_warning(I18n.T("[] makes a disappointed buzz and pricks your finger for being greedy. Ow!", this)));
            flick(Dm.text("[]_pinprick", icon_state), this);
            await violence_damage(L);
            await user.dropItemToGround(this);
            show_emotion(AID_EMOTION_HAPPY);
        }
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:721
    public virtual async DmTask violence_damage(mob_living user = null)
    {
        await user.adjust_brute_loss(4);
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:724
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;
        if (!Dm.truthy(user.can_read(this)) || Dm.truthy(user.is_blind()))
            return ITEM_INTERACT_BLOCKING;

        add_fingerprint(user);
        user.visible_message(
            span_notice(I18n.T("[] scans [] for [].", user, interacting_with, scan_for_what)),
            span_notice(I18n.T("You scan [] for [].", interacting_with, scan_for_what)));

        if (!iscarbon(interacting_with))
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
            to_chat(user, span_notice(I18n.T("[] makes a sad buzz and briefly displays an unhappy face, indicating it can't scan [].", this, interacting_with)));
            show_emotion(AI_EMOTION_SAD); // tg passes the AI's "Sad", which no overlay matches
            return ITEM_INTERACT_BLOCKING;
        }

        do_the_scan(interacting_with as mob_living_carbon, user);
        flick(Dm.text("[]_pinprick", icon_state), this);
        update_appearance(UPDATE_OVERLAYS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:747
    public virtual void do_the_scan(mob_living_carbon scanning = null, mob_living user = null)
    {
        woundscan(user, scanning, this, simple_scan: TRUE);
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:750
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        switch (emotion)
        {
            case AID_EMOTION_HAPPY:
                Dm.append(ref dot, mutable_appearance(icon, "+no_wounds"));
                break;
            case AID_EMOTION_WARN:
                Dm.append(ref dot, mutable_appearance(icon, "+wound_warn"));
                break;
            case AID_EMOTION_ANGRY:
                Dm.append(ref dot, mutable_appearance(icon, "+angry"));
                break;
            case AID_EMOTION_SAD:
                Dm.append(ref dot, mutable_appearance(icon, "+fail_scan"));
                break;
        }
        return dot;
    }

    /// Sets a new emotion display on the scanner, and resets back to neutral in a moment
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:763
    public virtual void show_emotion(string new_emotion = null)
    {
        emotion = new_emotion;
        update_appearance(UPDATE_OVERLAYS);
        if (emotion != AID_EMOTION_NEUTRAL)
            _addtimer(Dm.@new<datum_callback>(this, nameof(reset_emotions), AID_EMOTION_NEUTRAL), 2 * SECONDS, file: "code/game/objects/items/devices/scanners/health_analyzer.dm", line: 767);
    }

    // Resets visible emotion back to neutral
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:770
    public virtual void reset_emotions()
    {
        emotion = AID_EMOTION_NEUTRAL;
        update_appearance(UPDATE_OVERLAYS);
    }
}

// tg: code/game/objects/items/devices/scanners/health_analyzer.dm:779  (gaps DV2, D333)
public partial class obj_item_healthanalyzer_simple_disease : obj_item_healthanalyzer_simple
{
    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:789
    public override async DmTask violence_damage(mob_living user = null)
    {
        await user.adjust_brute_loss(1);
        await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_toxin), rand(1, 3));
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:793
    public override void do_the_scan(mob_living_carbon scanning = null, mob_living user = null)
    {
        diseasescan(user, scanning, this);
    }

    // tg: code/game/objects/items/devices/scanners/health_analyzer.dm:796
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        switch (emotion)
        {
            case AID_EMOTION_HAPPY:
                Dm.append(ref dot, mutable_appearance(icon, "+not_infected"));
                break;
            case AID_EMOTION_WARN:
                Dm.append(ref dot, mutable_appearance(icon, "+infected"));
                break;
            case AID_EMOTION_ANGRY:
                Dm.append(ref dot, mutable_appearance(icon, "+rancurous"));
                break;
            case AID_EMOTION_SAD:
                Dm.append(ref dot, mutable_appearance(icon, "+unknown_scan"));
                break;
        }
        if (emotion != AID_EMOTION_NEUTRAL)
            _addtimer(Dm.@new<datum_callback>(this, nameof(reset_emotions)), 4 * SECONDS, file: "code/game/objects/items/devices/scanners/health_analyzer.dm", line: 808); // longer on purpose
        return dot;
    }
}
