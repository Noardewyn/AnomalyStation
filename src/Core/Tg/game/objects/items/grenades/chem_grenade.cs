// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/chem_grenade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps F1): the casing's stages, the beakers, the trigger assembly on its wire, the detonation through
// chem_splash, the large, cryo, pyro and advanced release casings and the prefilled grenades. The fantasy bonuses are
// live-partial rows (fantasy affixes are not ported); the logs are leaves (logs, M10+).

// tg: code/game/objects/items/grenades/chem_grenade.dm:1
public partial class obj_item_grenade_chem_grenade : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_WIRES));
        create_reagents(casing_holder_volume);
        set_wires(Dm.@new<datum_wires_explosive_chem_grenade>(this));
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:38
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref landminemode);
        QDEL_LIST(beakers);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:49
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        display_timer = Dm.num(stage == GRENADE_READY); //show/hide the timer based on assembly state
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(user.can_see_reagents()))
        {
            if (stage == GRENADE_READY || !Dm.truthy(length(beakers)))
                return dot;
            if (length(beakers) == 2 && Dm.eq(((obj_item)beakers[1]).name, ((obj_item)beakers[2]).name))
            {
                Dm.append(dot, span_notice(I18n.T("You see two []s inside the grenade.", ((obj_item)beakers[1]).name)));
                return dot;
            }

            foreach (obj_item beaker in Dm.each_cast<obj_item>(beakers))
                Dm.append(dot, span_notice(I18n.T("You see a [] inside the grenade.", beaker.name)));
        }

        if (!Dm.truthy(length(beakers)))
        {
            Dm.append(dot, span_notice(I18n.T("You scan the grenade, but detect nothing.")));
            return dot;
        }

        Dm.append(dot, span_notice(I18n.T("You scan the grenade and detect the following reagents:")));

        foreach (obj_item beaker in Dm.each_cast<obj_item>(beakers))
        {
            foreach (datum_reagent reagent in Dm.each<datum_reagent>((beaker.reagents as datum_reagents)?.reagent_list))
                Dm.append(dot, span_notice(I18n.T("[] units of [] in \\the [].", reagent.volume, reagent.name, beaker)));
        }

        if (length(beakers) == 1)
            Dm.append(dot, span_notice(I18n.T("You detect no second beaker in the grenade.")));
        return dot;
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:75
    public override object update_name(float? updates = null)
    {
        string initial_name = Dm.initial(GetType(), (obj_item_grenade_chem_grenade g) => g.name);
        if (stage == GRENADE_EMPTY)
            name = Dm.text("[] casing", initial_name);
        else if (stage == GRENADE_WIRED)
            name = Dm.text("unsecured []", initial_name);
        else if (stage == GRENADE_READY)
            name = initial_name;
        return base.update_name(updates);
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:85
    public override object update_desc(float? updates = null)
    {
        string initial_name = Dm.initial(GetType(), (obj_item_grenade_chem_grenade g) => g.name);
        if (stage == GRENADE_EMPTY)
            desc = Dm.text("A do it yourself []! []", initial_name, Dm.initial(GetType(), (obj_item_grenade_chem_grenade g) => g.casedesc));
        else if (stage == GRENADE_WIRED)
            desc = Dm.text("An unsecured [] assembly.", initial_name);
        else if (stage == GRENADE_READY)
            desc = Dm.initial(GetType(), (obj_item_grenade_chem_grenade g) => g.desc);
        return base.update_desc(updates);
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:96
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(active))
        {
            icon_state = Dm.text("[]_active", base_icon_state);
            return await base.update_icon_state(__extra);
        }

        if (stage == GRENADE_EMPTY)
            icon_state = base_icon_state as string;
        else if (stage == GRENADE_WIRED)
            icon_state = Dm.text("[]_ass", base_icon_state);
        else if (stage == GRENADE_READY)
            icon_state = Dm.text("[]_locked", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:111
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (stage == GRENADE_READY && !Dm.truthy(active))
            return await base.attack_self(user, modifiers, __extra);

        if (stage == GRENADE_WIRED)
            await wires.interact(user);
        return null;
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:118
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(Dm.bitand(dud_flags, GRENADE_USED)))
        {
            balloon_alert(user, I18n.T("resetting trigger..."));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
                return ITEM_INTERACT_BLOCKING;

            balloon_alert(user, I18n.T("trigger reset"));
            dud_flags = Dm.bitand(dud_flags, ~GRENADE_USED);
            return ITEM_INTERACT_SUCCESS;
        }

        string initial_name = Dm.initial(GetType(), (obj_item_grenade_chem_grenade g) => g.name);
        if (stage == GRENADE_WIRED)
        {
            if (Dm.truthy(length(beakers)))
            {
                stage_change(GRENADE_READY);
                to_chat(user, span_notice(I18n.T("You lock the [] assembly.", initial_name)));
                tool.play_tool_sound(this, 25);
                return ITEM_INTERACT_SUCCESS;
            }

            if (landminemode == null || !(Dm.truthy(landminemode.scanning) || Dm.truthy(landminemode.timing)))
            {
                to_chat(user, span_warning(I18n.T("You need to add at least one beaker before locking the [] assembly!", initial_name)));
                return ITEM_INTERACT_BLOCKING;
            }

            landminemode.timing = FALSE;
            await landminemode.toggle_scan(FALSE);
            to_chat(user, span_notice(I18n.T("You disarm \\the [].", landminemode)));
            tool.play_tool_sound(this, 25);
            return ITEM_INTERACT_SUCCESS;
        }

        if (stage != GRENADE_READY)
        {
            to_chat(user, span_warning(I18n.T("You need to add a wire!")));
            return ITEM_INTERACT_BLOCKING;
        }

        det_time = det_time == 5 * SECONDS ? 3 * SECONDS : 5 * SECONDS;
        if (landminemode != null)
            landminemode.time = det_time * 0.1f; //overwrites the proxy sensor activation timer

        tool.play_tool_sound(this, 25);
        to_chat(user, span_notice(I18n.T("You modify the time delay. It's set for [].", DisplayTimeText(det_time))));
        return TRUE;
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:157
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (stage != GRENADE_READY || Dm.truthy(active))
            return NONE;

        tool.play_tool_sound(this);
        stage_change(GRENADE_WIRED);
        to_chat(user, span_notice(I18n.T("You unlock the [] assembly.", Dm.initial(GetType(), (obj_item_grenade_chem_grenade g) => g.name))));
        return TRUE;
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:166
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (stage != GRENADE_WIRED)
            return NONE;

        string initial_name = Dm.initial(GetType(), (obj_item_grenade_chem_grenade g) => g.name);
        if (!Dm.truthy(length(beakers)))
        {
            tool.play_tool_sound(this);
            await (wires as datum_wires_explosive_chem_grenade).detach_assembly(wires.get_wire(1));
            Dm.@new<obj_item_stack_cable_coil>(get_turf(this), 1);
            stage_change(GRENADE_EMPTY);
            to_chat(user, span_notice(I18n.T("You remove the activation mechanism from the [] assembly.", initial_name)));
            return ITEM_INTERACT_SUCCESS;
        }

        to_chat(user, span_notice(I18n.T("You open the [] assembly and remove the payload.", initial_name)));
        foreach (obj_item beaker in Dm.each_cast<obj_item>(beakers?.Copy()))
        {
            await beaker.forceMove(drop_location() as atom);
            if (beaker.reagents == null)
                continue;
            object reagent_list = pretty_string_from_reagent_list((beaker.reagents as datum_reagents).reagent_list);
            user.log_message(Dm.text("removed [] ([]) from []", beaker, reagent_list, this), LOG_GAME);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:187
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item item = tool;
        if (isassembly(item) && stage == GRENADE_WIRED)
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        string initial_name = Dm.initial(GetType(), (obj_item_grenade_chem_grenade g) => g.name);
        if (stage == GRENADE_EMPTY && istype(item, typeof(obj_item_stack_cable_coil)))
        {
            obj_item_stack_cable_coil coil = item as obj_item_stack_cable_coil;
            if (!Dm.truthy(await coil.use(1)))
            {
                to_chat(user, span_warning(I18n.T("You need one length of coil to wire the assembly!")));
                return ITEM_INTERACT_BLOCKING;
            }

            det_time = 5 * SECONDS; // In case the cable_coil was removed and readded.
            stage_change(GRENADE_WIRED);
            to_chat(user, span_notice(I18n.T("You rig the [] assembly.", initial_name)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (stage != GRENADE_WIRED)
            return NONE;

        if (!Dm.truthy(is_type_in_list(item, allowed_containers)))
            return NONE;

        if (Dm.truthy(is_type_in_list(item, banned_containers)))
        {
            to_chat(user, span_warning(I18n.T("[] is too small to fit []!", this, item))); // this one hits home huh anon?
            return ITEM_INTERACT_BLOCKING;
        }

        if (length(beakers) == 2)
        {
            to_chat(user, span_warning(I18n.T("[] can not hold more containers!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(item, this)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You add [] to the [] assembly.", item, initial_name)));
        Dm.append(ref beakers, item);
        object reagent_list = pretty_string_from_reagent_list((item.reagents as datum_reagents).reagent_list);
        user.log_message(Dm.text("inserted [] ([]) into []", item, reagent_list, this), LOG_GAME);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:226
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        Dm.remove(ref beakers, gone);
        return dot;
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:230
    public virtual void stage_change(float new_stage)
    {
        stage = new_stage;
        update_appearance();
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:234
    public override async DmTask<object> on_found(mob finder = null)
    {
        obj_item_assembly assembly = wires.get_attached(wires.get_wire(1)) as obj_item_assembly;
        if (assembly != null)
            await assembly.on_found(finder);
        return null;
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:238
    public override void log_grenade(mob user = null)
    {
        string reagent_string = "";
        float beaker_number = 1;
        foreach (obj_item exploded_beaker in Dm.each_cast<obj_item>(beakers))
        {
            if (exploded_beaker.reagents is datum_reagents beaker_reagents)
                reagent_string += Dm.text(" ([] [] : ", exploded_beaker.name, beaker_number++) + Dm.str(pretty_string_from_reagent_list(beaker_reagents.reagent_list)) + ");";
        }

        if (Dm.truthy(landminemode))
            log_bomber(user, "activated a proxy", this, Dm.text("containing:[]", reagent_string), message_admins: !Dm.truthy(dud_flags) ? TRUE : FALSE);
        else
            log_bomber(user, "primed a", this, Dm.text("containing:[]", reagent_string), message_admins: !Dm.truthy(dud_flags) ? TRUE : FALSE);
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:250
    public override async DmTask arm_grenade(mob user = null, object delayoverride = null, float? msg = null, float? volume = null)
    {
        msg ??= TRUE;
        volume ??= 60;
        log_grenade(user); //Inbuilt admin procs already handle null users
        if (user != null)
        {
            add_fingerprint(user);
            if (Dm.truthy(msg))
            {
                if (landminemode != null)
                    to_chat(user, span_warning(I18n.T("You prime [], activating its proximity sensor.", this)));
                else
                    to_chat(user, span_warning(I18n.T("You prime []! []!", this, DisplayTimeText(det_time))));
            }
        }

        active = TRUE;
        await update_icon_state();
        playsound(this, grenade_arm_sound, volume, grenade_sound_vary);
        if (landminemode != null)
        {
            await landminemode.toggle_scan(FALSE); // Ensures that if it was turned on before for some reason, it doesn't get turned off
            await landminemode.activate();
        }
        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(detonate)), delayoverride == null ? det_time : Dm.num(delayoverride), file: "code/game/objects/items/grenades/chem_grenade.dm", line: 267);
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:269
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        if (stage != GRENADE_READY)
            return null;

        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        DmList reactants = Dm.list();
        foreach (obj_item beaker in Dm.each_cast<obj_item>(beakers))
            Dm.append(ref reactants, beaker.reagents);

        turf detonation_turf = get_turf(this);
        if (Dm.truthy(await chem_splash(detonation_turf, reagents as datum_reagents, affected_area, reactants, ignition_temp, threatscale)))
        {
            // logs from custom assemblies priming are handled by the wire component
            log_game(Dm.text("A grenade detonated at []", AREACOORD(detonation_turf)));
        }

        active = FALSE;
        update_appearance();
        return dot;
    }
}

//Large chem grenades accept slime cores and use the appropriately.
// tg: code/game/objects/items/grenades/chem_grenade.dm:290
public partial class obj_item_grenade_chem_grenade_large : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:303
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        if (stage != GRENADE_READY || Dm.truthy(dud_flags))
        {
            active = FALSE;
            update_appearance();
            return FALSE;
        }

        float extract_total_volume = 0;
        float extract_maximum_volume = 0;
        DmList extracts = Dm.list();

        float beaker_total_volume = 0;
        DmList other_containers = Dm.list();

        foreach (obj_item thing in Dm.each_cast<obj_item>(beakers))
        {
            if (thing.reagents == null)
                continue;

            if (!istype(thing, typeof(obj_item_slime_extract)))
            {
                beaker_total_volume += (thing.reagents as datum_reagents).total_volume;
                Dm.append(ref other_containers, thing);
                continue;
            }

            obj_item_slime_extract extract = thing as obj_item_slime_extract;
            if (!Dm.truthy(extract.extract_uses))
                continue;

            extract_total_volume += (extract.reagents as datum_reagents).total_volume;
            extract_maximum_volume += (extract.reagents as datum_reagents).maximum_volume;
            Dm.append(ref extracts, extract);
        }

        float available_extract_volume = extract_maximum_volume - extract_total_volume;
        if (beaker_total_volume <= 0 || available_extract_volume <= 0)
            return await base.detonate(lanced_by);

        float container_ratio = available_extract_volume / beaker_total_volume;
        datum_reagents tmp_holder = Dm.@new<datum_reagents>(beaker_total_volume);
        foreach (obj_item container in Dm.each_cast<obj_item>(other_containers))
            await (container.reagents as datum_reagents).trans_to(tmp_holder, (container.reagents as datum_reagents).total_volume * container_ratio, no_react: TRUE);

        foreach (obj_item_slime_extract extract in Dm.each_cast<obj_item_slime_extract>(extracts))
        {
            float available_volume = (extract.reagents as datum_reagents).maximum_volume - (extract.reagents as datum_reagents).total_volume;
            await tmp_holder.trans_to(extract, beaker_total_volume * (available_volume / available_extract_volume), no_react: TRUE);

            await (extract.reagents as datum_reagents).handle_reactions(); // Reaction handling in the transfer proc is reciprocal and we don't want to blow up the tmp holder early.
            if (QDELETED(extract))
            {
                Dm.remove(ref beakers, extract);
                Dm.remove(ref extracts, extract);
            }
        }

        return await base.detonate(lanced_by);
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:372
public partial class obj_item_grenade_chem_grenade_adv_release : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:381
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(active))
            return ITEM_INTERACT_BLOCKING;

        object newspread = await tgui_input_number(user, "Please enter a new spread amount", "Grenade Spread", 5, 100, 5);
        if (!Dm.truthy(newspread) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return ITEM_INTERACT_BLOCKING;

        unit_spread = Dm.num(newspread);
        to_chat(user, span_notice(I18n.T("You set the time release to [] units per detonation.", unit_spread)));
        return await base.multitool_act(user, tool);
    }

    // tg: code/game/objects/items/grenades/chem_grenade.dm:393
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        if (stage != GRENADE_READY || Dm.truthy(dud_flags))
        {
            active = FALSE;
            update_appearance();
            return null;
        }

        float total_volume = 0;
        foreach (obj_item_reagent_containers reagent_container in Dm.each<obj_item_reagent_containers>(beakers))
            total_volume += (reagent_container.reagents as datum_reagents).total_volume;

        if (total_volume == 0)
        {
            active = FALSE;
            update_appearance();
            return null;
        }

        float fraction = unit_spread / total_volume;
        datum_reagents reactants = Dm.@new<datum_reagents>(unit_spread);
        reactants.my_atom = this;
        foreach (obj_item_reagent_containers reagent_container in Dm.each<obj_item_reagent_containers>(beakers))
        {
            await (reagent_container.reagents as datum_reagents).trans_to(
                reactants,
                (reagent_container.reagents as datum_reagents).total_volume * fraction,
                threatscale,
                no_react: TRUE
            );
        }

        turf detonated_turf = get_turf(this);
        await chem_splash(detonated_turf, reagents as datum_reagents, affected_area, Dm.list(reactants), ignition_temp, threatscale);
        _addtimer(Dm.@new<datum_callback>(this, nameof(detonate)), det_time, file: "code/game/objects/items/grenades/chem_grenade.dm", line: 421);
        log_game(Dm.text("A grenade detonated at []", AREACOORD(detonated_turf)));
        return null;
    }
}

// Premade grenades

// tg: code/game/objects/items/grenades/chem_grenade.dm:426
public partial class obj_item_grenade_chem_grenade_metalfoam : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:431
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_aluminium), 30));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_foaming_agent), 10));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_toxin_acid_fluacid), 10));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:444
public partial class obj_item_grenade_chem_grenade_smart_metal_foam : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:449
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_aluminium), 75));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_smart_foaming_agent), 25));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_toxin_acid_fluacid), 25));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:462
public partial class obj_item_grenade_chem_grenade_incendiary : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:467
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_phosphorus), 25));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_stable_plasma), 25));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_toxin_acid), 25));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:480
public partial class obj_item_grenade_chem_grenade_antiweed : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:485
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_toxin_plantbgone_weedkiller), 25));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_potassium), 25));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_phosphorus), 25));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_sugar), 25));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:499
public partial class obj_item_grenade_chem_grenade_cleaner : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:504
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_fluorosurfactant), 40));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_water), 40));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_space_cleaner), 10));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:517
public partial class obj_item_grenade_chem_grenade_ez_clean : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:522
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_fluorosurfactant), 40));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_water), 40));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_space_cleaner_ez_clean), 60)); //ensures a  t h i c c  distribution

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:536
public partial class obj_item_grenade_chem_grenade_teargas : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:541
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_condensedcapsaicin), 60));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_potassium), 40));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_phosphorus), 40));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_sugar), 40));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:554
public partial class obj_item_grenade_chem_grenade_teargas_instant : obj_item_grenade_chem_grenade_teargas
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:554
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(Waiting.NoSleep(detonate())))
            return INITIALIZE_HINT_QDEL;
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:559
public partial class obj_item_grenade_chem_grenade_facid : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:564
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_toxin_acid_fluacid), 290));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_potassium), 10));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_phosphorus), 10));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_sugar), 10));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_toxin_acid_fluacid), 280));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:579
public partial class obj_item_grenade_chem_grenade_colorful : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:584
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_colorful_reagent), 25));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_potassium), 25));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_phosphorus), 25));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_sugar), 25));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:597
public partial class obj_item_grenade_chem_grenade_glitter : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:603
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_glitter), 25, data: Dm.list(Dm.kv("colors", glitter_colors))));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_potassium), 25));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_phosphorus), 25));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_sugar), 25));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:626
public partial class obj_item_grenade_chem_grenade_clf3 : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:631
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_fluorosurfactant), 250));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_clf3), 50));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_water), 250));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_clf3), 50));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:644
public partial class obj_item_grenade_chem_grenade_bioterrorfoam : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:649
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_cryptobiolin), 75));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_water), 50));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_toxin_mutetoxin), 50));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_toxin_spore), 75));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_toxin_itching_powder), 50));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_fluorosurfactant), 150));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_toxin_mutagen), 150));
        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:664
public partial class obj_item_grenade_chem_grenade_tuberculosis : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:669
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_potassium), 50));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_phosphorus), 50));
        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_fungalspores), 200));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_blood), 250));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_sugar), 50));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/chem_grenade.dm:683
public partial class obj_item_grenade_chem_grenade_holy : obj_item_grenade_chem_grenade
{
    // tg: code/game/objects/items/grenades/chem_grenade.dm:690
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var beaker_one = Dm.@new<obj_item_reagent_containers_cup_beaker_meta>(this);
        var beaker_two = Dm.@new<obj_item_reagent_containers_cup_beaker_meta>(this);

        Waiting.NoSleep((beaker_one.reagents as datum_reagents).add_reagent(typeof(datum_reagent_potassium), 150));
        Waiting.NoSleep((beaker_two.reagents as datum_reagents).add_reagent(typeof(datum_reagent_water_holywater), 150));

        Dm.append(ref beakers, beaker_one);
        Dm.append(ref beakers, beaker_two);
        return dot;
    }
}
