// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/scanner_gate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps SG1 (D333's station order: security and command consoles): the whole file. The scanner gate scans whoever walks under it
// by its mode (mindshield, disease, weapons, wanted, species, nutrition; reversible), beeps, says what it found and flashes its
// scanline (vis_contents, the model's scan field), pulses the assemblies on its accept and deny wires, can be locked with an
// ID, emagged, rotated with a wrench and set in its ScannerGate window. The var blocks are generated.

using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/machinery/scanner_gate.dm:9
public partial class obj_machinery_scanner_gate : obj_machinery
{
    // tg: code/game/machinery/scanner_gate.dm:1-7 (file-local, #undef at :377-383)
    internal const string SCANGATE_NONE = "Off";
    internal const string SCANGATE_MINDSHIELD = "Mindshield";
    internal const string SCANGATE_DISEASE = "Disease";
    internal const string SCANGATE_GUNS = "Guns";
    internal const string SCANGATE_WANTED = "Wanted";
    internal const string SCANGATE_SPECIES = "Species";
    internal const string SCANGATE_NUTRITION = "Nutrition";

    ///List of species that can be scanned by the gate. Supports adding more species' IDs during in-game.
    // tg: code/game/machinery/scanner_gate.dm:43  var/static/list/available_species
    private static DmList available_species => TgState.Current.obj_machinery_scanner_gate__available_species ??= Dm.list(
        SPECIES_HUMAN,
        SPECIES_LIZARD,
        SPECIES_FLYPERSON,
        SPECIES_FELINE,
        SPECIES_PLASMAMAN,
        SPECIES_MOTH,
        SPECIES_JELLYPERSON,
        SPECIES_PODPERSON,
        SPECIES_GOLEM,
        SPECIES_ZOMBIE);

    /// All scan modes available to the scanner
    // tg: code/game/machinery/scanner_gate.dm:56  var/static/list/all_modes
    private static DmList all_modes => TgState.Current.obj_machinery_scanner_gate__all_modes ??= Dm.list(
        SCANGATE_NONE,
        SCANGATE_MINDSHIELD,
        SCANGATE_DISEASE,
        SCANGATE_GUNS,
        SCANGATE_WANTED,
        SCANGATE_SPECIES,
        SCANGATE_NUTRITION);

    /// All disease severity thresholds available to the scanner
    // tg: code/game/machinery/scanner_gate.dm:66  var/static/list/all_disease_thresholds
    private static DmList all_disease_thresholds => TgState.Current.obj_machinery_scanner_gate__all_disease_thresholds ??= Dm.list(
        DISEASE_SEVERITY_POSITIVE,
        DISEASE_SEVERITY_NONTHREAT,
        DISEASE_SEVERITY_MINOR,
        DISEASE_SEVERITY_MEDIUM,
        DISEASE_SEVERITY_HARMFUL,
        DISEASE_SEVERITY_DANGEROUS,
        DISEASE_SEVERITY_BIOHAZARD);

    /// All nutrition levels available to the scanner
    // tg: code/game/machinery/scanner_gate.dm:76  var/static/list/nutrition_modes
    private static DmList nutrition_modes => TgState.Current.obj_machinery_scanner_gate__nutrition_modes ??= Dm.list(
        "Starving",
        "Obese");

    // tg: code/game/machinery/scanner_gate.dm:84  var/static/list/loc_connections
    private static DmList loc_connections => TgState.Current.obj_machinery_scanner_gate__loc_connections ??= Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)));

    // tg: code/game/machinery/scanner_gate.dm:83
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_wires(Dm.@new<datum_wires_scanner_gate>(this));
        set_scanline("passive");
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), ROTATION_IGNORE_ANCHORED | ROTATION_REQUIRE_WRENCH | ROTATION_NEEDS_UNBLOCKED));
        register_context();
        return dot;
    }

    // tg: code/game/machinery/scanner_gate.dm:94
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref scanline);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/scanner_gate.dm:98
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        foreach (datum_stock_part_scanning_module scanning_module in Dm.each<datum_stock_part_scanning_module>(component_parts))
            minus_false_beep = Dm.num(scanning_module.tier); //The better are scanninning modules - the lower is chance of False Positives
        return dot;
    }

    // tg: code/game/machinery/scanner_gate.dm:103
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        if (scanline != null)
            await scanline.setDir(newdir);
    }

    // tg: code/game/machinery/scanner_gate.dm:107
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        Dm.append(ref dot, span_notice(I18n.T("It's set to scan for [].", span_boldnotice(scangate_mode))));
        if (Dm.truthy(locked))
            Dm.append(ref dot, span_notice(I18n.T("The control panel is ID-locked. Swipe a valid ID to unlock it.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The control panel is unlocked. Swipe an ID to lock it.")));
        return dot;
    }

    // tg: code/game/machinery/scanner_gate.dm:116
    [SignalHandler]
    public virtual void on_entered(datum source, atom_movable thing)
    {
        Waiting.Detach(auto_scan(thing)); // INVOKE_ASYNC(src, PROC_REF(auto_scan), thing)
    }

    // tg: code/game/machinery/scanner_gate.dm:120
    public virtual async DmTask auto_scan(atom_movable thing)
    {
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)) && Dm.truthy(anchored) && !Dm.truthy(panel_open))
            await perform_scan(thing);
    }

    // tg: code/game/machinery/scanner_gate.dm:124
    public virtual void set_scanline(object scanline_type, object duration = null)
    {
        if (scanline != null)
            Dm.remove(vis_contents, scanline);
        else
        {
            scanline = Dm.@new<obj_effect_overlay>(this);
            scanline.icon = icon;
            scanline.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
            scanline.layer = layer;
        }
        deltimer(scanline_timer);
        if (scanline_type == null)
        {
            if (Dm.truthy(duration))
                scanline_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(set_scanline), "passive"), Dm.num(duration), TIMER_STOPPABLE, file: "code/game/machinery/scanner_gate.dm", line: 135);
            return;
        }
        scanline.icon_state = Dm.str(scanline_type);
        Dm.append(vis_contents, scanline);
        if (Dm.truthy(duration))
            scanline_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(set_scanline), "passive"), Dm.num(duration), TIMER_STOPPABLE, file: "code/game/machinery/scanner_gate.dm", line: 140);
    }

    // tg: code/game/machinery/scanner_gate.dm:142
    public override object power_change()
    {
        object dot = base.power_change();
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
        {
            set_scanline(null);
            return dot;
        }
        set_scanline("passive");
        return dot;
    }

    // tg: code/game/machinery/scanner_gate.dm:149
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(panel_open) && Dm.truthy(is_wire_tool(tool)))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        var card = tool.GetID() as obj_item_card_id;
        if (card == null)
            return NONE;

        if (Dm.truthy(locked))
        {
            if (!Dm.truthy(await allowed__proc(user)))
                return ITEM_INTERACT_BLOCKING;

            locked = FALSE;
            req_access = Dm.list();
            balloon_alert(user, I18n.T("unlocked"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, I18n.T("nothing happens!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("locked"));
        req_access = tool.GetAccess() as DmList; // returns a copy so this is chill
        locked = TRUE;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/scanner_gate.dm:177
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return Dm.truthy(locked) ? NONE : default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/scanner_gate.dm:180
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]_open", base_icon_state) : (string)base_icon_state;
        return dot;
    }

    // tg: code/game/machinery/scanner_gate.dm:184
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        locked = FALSE;
        req_access = Dm.list();
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        balloon_alert(user, I18n.T("id checker disabled"));
        return TRUE;
    }

    // tg: code/game/machinery/scanner_gate.dm:193
    public virtual async DmTask perform_scan(atom_movable thing)
    {
        bool beep = false;
        object color = null;
        object detected_thing = null;
        bool bypassed = false;
        playsound(this, SFX_INDUSTRIAL_SCAN, 20, TRUE, -2, TRUE, FALSE);
        switch (scangate_mode)
        {
            case SCANGATE_NONE:
                return;
            case SCANGATE_WANTED:
                if (thing is mob_living_carbon_human wanted_human)
                {
                    detected_thing = I18n.T("Warrant");
                    object perpname = wanted_human.get_face_name(Dm.str(wanted_human.get_id_name()));
                    var target = find_record(perpname) as datum_record_crew;
                    if (target == null || Dm.eq(target.wanted_status, WANTED_ARREST))
                        beep = true;
                }
                break;
            case SCANGATE_MINDSHIELD:
                detected_thing = I18n.T("Mindshield");
                if (thing is mob_living_carbon_human shielded_human)
                {
                    if (HAS_TRAIT(shielded_human, TRAIT_MINDSHIELD))
                        beep = true;
                }
                break;
            case SCANGATE_DISEASE:
                detected_thing = I18n.T("[] infection", disease_threshold);
                if (thing is mob_living_carbon scanned_carbon)
                {
                    if (Dm.num(get_disease_severity_value(scanned_carbon.check_virus())) >= Dm.num(get_disease_severity_value(disease_threshold)))
                        beep = true;
                }
                break;
            case SCANGATE_SPECIES:
                if (thing is mob_living_carbon_human species_human)
                {
                    Type scan_species = typeof(datum_species_human);
                    if (Dm.truthy(detect_species_id) && Dm.@in(detect_species_id, available_species))
                    {
                        scan_species = GLOB.species_list[detect_species_id] as Type;
                        // tg reads the path's var (`scan_species.name`): its initial value
                        detected_thing = Dm.initial(scan_species, (datum_species s) => s.name);
                    }
                    if (Dm.truthy(is_species(species_human, scan_species)))
                        beep = true;
                    if (Dm.eq(detect_species_id, SPECIES_ZOMBIE)) //Can detect dormant zombies
                    {
                        detected_thing = I18n.T("Romerol infection");
                        if (Dm.truthy(species_human.get_organ_slot(ORGAN_SLOT_ZOMBIE)))
                            beep = true;
                    }
                }
                break;
            case SCANGATE_GUNS:
                detected_thing = I18n.T("Weapons");
                if (isgun(thing))
                    beep = true;
                else if (thing is mob_living_carbon_human armed_human)
                {
                    var idcard = armed_human.get_idcard(hand_first: FALSE) as obj_item_card_id;
                    foreach (obj_item scanned_item in Dm.each<obj_item>(armed_human.get_all_contents_skipping_traits(TRAIT_CONTRABAND_BLOCKER)))
                    {
                        if (isgun(scanned_item))
                        {
                            if (!HAS_TRAIT(armed_human, TRAIT_MINDSHIELD) && (idcard == null || !Dm.@in(ACCESS_WEAPONS, idcard.access))) // mindshield or ID card with weapons access, like bartender
                            {
                                beep = true;
                                break;
                            }
                            bypassed = true;
                            break;
                        }
                    }
                }
                else
                {
                    foreach (obj_item content in Dm.each<obj_item>(thing.get_all_contents_skipping_traits(TRAIT_CONTRABAND_BLOCKER)))
                    {
                        if (isgun(content))
                        {
                            beep = true;
                            break;
                        }
                    }
                }
                break;
            case SCANGATE_NUTRITION:
                if (thing is mob_living_carbon_human fed_human)
                {
                    if (fed_human.nutrition <= detect_nutrition && detect_nutrition == NUTRITION_LEVEL_STARVING)
                    {
                        beep = true;
                        detected_thing = I18n.T("Starvation");
                    }
                    if (fed_human.nutrition >= detect_nutrition && detect_nutrition == NUTRITION_LEVEL_FAT)
                    {
                        beep = true;
                        detected_thing = I18n.T("Obesity");
                    }
                }
                break;
        }

        if (Dm.truthy(reverse))
            beep = !beep;

        if (prob(base_false_beep - minus_false_beep)) //False positive/negative
            beep = prob(50);

        if (beep)
        {
            await alarm_beep(detected_thing);
            if (Dm.truthy(this._listen_lookup?[COMSIG_SCANGATE_PASS_TRIGGER]))
                this._SendSignal(COMSIG_SCANGATE_PASS_TRIGGER, Dm.list(this, thing));
            if (!Dm.truthy(ignore_signals))
            {
                color = wires.get_color_of_wire(WIRE_ACCEPT);
                var assembly = wires.get_attached(color) as obj_item_assembly;
                if (assembly != null)
                    await assembly.activate();
            }
        }
        else
        {
            if (Dm.truthy(this._listen_lookup?[COMSIG_SCANGATE_PASS_NO_TRIGGER]))
                this._SendSignal(COMSIG_SCANGATE_PASS_NO_TRIGGER, Dm.list(this, thing));
            if (bypassed)
                await say(I18n.T("[] detection bypassed.", detected_thing));
            if (!Dm.truthy(ignore_signals))
            {
                color = wires.get_color_of_wire(WIRE_DENY);
                var assembly = wires.get_attached(color) as obj_item_assembly;
                if (assembly != null)
                    await assembly.activate();
            }
            set_scanline("scanning", 1 * SECONDS);
        }

        await use_energy(active_power_usage);
    }

    // tg: code/game/machinery/scanner_gate.dm:288
    public virtual async DmTask alarm_beep(object detected_thing)
    {
        if (!(next_beep <= world.time)) // COOLDOWN_FINISHED(src, next_beep)
            return;

        if (Dm.truthy(detected_thing))
            await say(Dm.truthy(reverse) ? I18n.T("[] not detected!!", detected_thing) : I18n.T("[] detected!!", detected_thing));

        next_beep = world.time + 2 * SECONDS; // COOLDOWN_START(src, next_beep, 2 SECONDS)
        playsound(source: this, soundin: Dm.resource("sound/machines/scanner/scanbuzz.ogg"), vol: 30, vary: FALSE, extrarange: MEDIUM_RANGE_SOUND_EXTRARANGE, falloff_distance: 4);
        set_scanline("alarm", 2 * SECONDS);
    }

    // tg: code/game/machinery/scanner_gate.dm:299
    public override async DmTask<object> can_interact(mob user = null, float? require_adjacent_turf = null)
    {
        if (Dm.truthy(locked))
            return FALSE;
        return await base.can_interact(user, require_adjacent_turf);
    }

    // tg: code/game/machinery/scanner_gate.dm:304
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "ScannerGate", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/scanner_gate.dm:310
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = await base.ui_static_data(user) as DmList ?? Dm.list();
        foreach (object species_id in Dm.each(available_species))
        {
            // tg reads the path's var (`specie.name`): its initial value
            Type specie = GLOB.species_list[species_id] as Type;
            Dm.append_at(dot, "available_species", Dm.list(Dm.list(
                Dm.kv("specie_name", capitalize(format_text(specie != null ? Dm.initial(specie, (datum_species s) => s.name) : null))),
                Dm.kv("specie_id", species_id))));
        }
        return dot;
    }

    // tg: code/game/machinery/scanner_gate.dm:319
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["locked"] = locked;
        data["scan_mode"] = scangate_mode;
        data["reverse"] = reverse;
        data["disease_threshold"] = disease_threshold;
        data["target_species_id"] = detect_species_id;
        data["target_nutrition"] = detect_nutrition;
        data["target_zombie"] = Dm.eq(detect_species_id, SPECIES_ZOMBIE) ? TRUE : FALSE;
        return data;
    }

    // tg: code/game/machinery/scanner_gate.dm:330
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "set_mode":
            {
                object new_mode = @params["new_mode"];
                if (!Dm.truthy(new_mode) || !Dm.@in(new_mode, all_modes))
                    return dot;
                scangate_mode = Dm.str(new_mode);
                dot = TRUE;
                break;
            }
            case "toggle_reverse":
                reverse = Dm.num(!Dm.truthy(reverse));
                dot = TRUE;
                break;
            case "toggle_lock":
                if (Dm.truthy(await allowed__proc(usr)))
                    locked = Dm.num(!Dm.truthy(locked));
                dot = TRUE;
                break;
            case "set_disease_threshold":
            {
                object new_threshold = @params["new_threshold"];
                if (!Dm.truthy(new_threshold) || !Dm.@in(new_threshold, all_disease_thresholds))
                    return dot;
                disease_threshold = Dm.str(new_threshold);
                dot = TRUE;
                break;
            }
            case "set_target_species":
            {
                object new_specie_id = @params["new_species_id"];
                if (!Dm.truthy(new_specie_id) || !Dm.@in(new_specie_id, available_species))
                    return dot;
                detect_species_id = Dm.str(new_specie_id);
                dot = TRUE;
                break;
            }
            case "set_target_nutrition":
            {
                object new_nutrition = @params["new_nutrition"];
                if (!Dm.truthy(new_nutrition) || !Dm.@in(new_nutrition, nutrition_modes))
                    return dot;
                switch (Dm.str(new_nutrition))
                {
                    case "Starving":
                        detect_nutrition = NUTRITION_LEVEL_STARVING;
                        break;
                    case "Obese":
                        detect_nutrition = NUTRITION_LEVEL_FAT;
                        break;
                }
                dot = TRUE;
                break;
            }
        }
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList obj_machinery_scanner_gate__available_species;
    internal DmList obj_machinery_scanner_gate__all_modes;
    internal DmList obj_machinery_scanner_gate__all_disease_thresholds;
    internal DmList obj_machinery_scanner_gate__nutrition_modes;
    internal DmList obj_machinery_scanner_gate__loc_connections;
}
