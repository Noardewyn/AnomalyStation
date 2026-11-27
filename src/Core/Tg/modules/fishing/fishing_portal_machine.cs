// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fishing_portal_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish-porter opens a portal to one of the fishing dimensions its techweb's fish scanning experiments
// unlocked (the syndicate one when emagged), or to a fishing spot linked with a multitool, which its matter bins count and its
// capacitors let reach other z-levels. The var blocks are generated (D111).

// tg: code/modules/fishing/fishing_portal_machine.dm:1
public partial class obj_machinery_fishing_portal_generator : obj_machinery
{
    // tg: code/modules/fishing/fishing_portal_machine.dm:26  Initialize's var/static/list/tool_screentips (rules 10.6)
    private static DmList Initialize__tool_screentips => TgState.Current.obj_machinery_fishing_portal_generator__Initialize__tool_screentips ??= Dm.list(
        Dm.kv(TOOL_MULTITOOL, Dm.list(
            Dm.kv(SCREENTIP_CONTEXT_LMB, "Link"),
            Dm.kv(SCREENTIP_CONTEXT_RMB, "Unlink fishing spots"))));

    // tg: code/modules/fishing/fishing_portal_machine.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_tools), Initialize__tool_screentips));
        ADD_TRAIT(this, TRAIT_UNLINKABLE_FISHING_SPOT, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:35
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(deactivate());
        linked_fishing_spots = null;
        return base.Destroy(force);
    }

    ///Higher tier parts let you link to more fishing spots at once and eventually let you connect through different zlevels.
    // tg: code/modules/fishing/fishing_portal_machine.dm:41
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        max_fishing_spots = 0;
        long_range_link = FALSE;
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            max_fishing_spots += Dm.num(matter_bin.tier) * 0.5f;
        max_fishing_spots = ROUND_UP(max_fishing_spots);
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
        {
            if (Dm.num(capacitor.tier) >= 3)
                long_range_link = TRUE;
        }
        if (!Dm.truthy(long_range_link))
            await check_fishing_spot_z();
        if (length(linked_fishing_spots) > max_fishing_spots)
        {
            if (active != null)
                await deactivate();
            linked_fishing_spots.len = max_fishing_spots;
        }
        return dot;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:57
    public override void on_set_panel_open(object old_value = null)
    {
        update_appearance();
        base.on_set_panel_open(old_value);
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:61
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:66
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            balloon_alert(user, I18n.T("no power!"));
            return ITEM_INTERACT_BLOCKING;
        }
        obj_item_multitool multitool = (obj_item_multitool)tool;
        bool unlink = multitool.buffer == this;
        multitool.set_buffer(unlink ? null : this);
        balloon_alert(user, unlink ? I18n.T("fish-porter unlinked") : I18n.T("fish-porter linked"));
        if (!unlink)
        {
            Dm.combine(ref multitool.item_flags, ITEM_HAS_CONTEXTUAL_SCREENTIPS);
            RegisterSignal(multitool, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET, nameof(multitool_context));
            RegisterSignal(multitool, COMSIG_MULTITOOL_REMOVE_BUFFER, nameof(multitool_unbuffered));
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:80
    public override async DmTask<object> multitool_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            balloon_alert(user, I18n.T("no power!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(length(linked_fishing_spots)))
        {
            balloon_alert(user, I18n.T("nothing to unlink!"));
            return ITEM_INTERACT_BLOCKING;
        }
        DmList fishing_list = Dm.list();
        float id = 1;
        foreach (atom spot in Dm.each<atom>(linked_fishing_spots))
        {
            string choice_name = Dm.text("[] ([])", spot.name, id);
            fishing_list[choice_name] = spot;
            id++;
        }
        DmList choices = Dm.list();
        foreach (object radial_name in Dm.each<object>(fishing_list))
        {
            // DEVIATION(DV682): tg reads the linked spot itself as the fish source here (:95), a runtime on its missing
            // radial_state that stops the menu; the port reads the spot's fish source. tg's
            // `appearance.add_overlay('icons/hud/radial_fishing.dmi', "minus_sign")` (:97) calls /image/proc/add_overlay(x),
            // which takes one argument, so tg overlays the bare icon file; the port draws the state named.
            datum_fish_source source = linked_fishing_spots[fishing_list[radial_name]] as datum_fish_source;
            mutable_appearance appearance = mutable_appearance(Dm.resource("icons/hud/radial_fishing.dmi"), source?.radial_state) as mutable_appearance;
            Dm.combine(appearance.overlays, mutable_appearance(Dm.resource("icons/hud/radial_fishing.dmi"), "minus_sign"));
            choices[radial_name] = appearance;
        }

        object choice = await show_radial_menu(user, this, choices, radius: 38, custom_check: Dm.@new<datum_callback>(this, nameof(can_interact), user), tooltips: TRUE);
        if (!Dm.truthy(choice))
            return null;
        atom chosen_spot = fishing_list[choice] as atom;
        if (QDELETED(chosen_spot) || !Dm.@in(chosen_spot, linked_fishing_spots) || !Dm.truthy(await can_interact(user)))
            return null;
        unlink_fishing_spot(chosen_spot);
        balloon_alert(user, I18n.T("fishing spot unlinked"));
        return null;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:108
    [SignalHandler]
    public virtual object multitool_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (HAS_TRAIT(target, TRAIT_FISHING_SPOT) && !HAS_TRAIT(target, TRAIT_UNLINKABLE_FISHING_SPOT))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Link to fish-porter");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return NONE;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:115
    [SignalHandler]
    public virtual void multitool_unbuffered(datum source = null, datum buffer = null)
    {
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET, COMSIG_MULTITOOL_REMOVE_BUFFER));
    }

    ///Called when using a multitool on any other fishing source.
    // tg: code/modules/fishing/fishing_portal_machine.dm:120
    public virtual object link_fishing_spot(datum_fish_source source = null, atom spot = null, mob_living user = null)
    {
        if (istype(spot, typeof(obj_machinery_fishing_portal_generator))) //Don't link it to itself or other fishing portals.
            return null;
        if (length(linked_fishing_spots) >= max_fishing_spots)
        {
            spot.balloon_alert(user, I18n.T("cannot link more!"));
            return ITEM_INTERACT_BLOCKING;
        }
        foreach (object other_spot in Dm.each<object>(linked_fishing_spots))
        {
            datum_fish_source stored = linked_fishing_spots[other_spot] as datum_fish_source;
            if (stored == source)
            {
                spot.balloon_alert(user, I18n.T("already linked!"));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 15, FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
                return ITEM_INTERACT_BLOCKING;
            }
        }
        if (HAS_TRAIT(spot, TRAIT_UNLINKABLE_FISHING_SPOT))
        {
            spot.balloon_alert(user, I18n.T("unlinkable fishing spot!"));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 15, FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
            return ITEM_INTERACT_BLOCKING;
        }
        LAZYSET(ref linked_fishing_spots, spot, source);
        RegisterSignal(spot, SIGNAL_REMOVETRAIT(TRAIT_FISHING_SPOT), nameof(unlink_fishing_spot));
        spot.balloon_alert(user, I18n.T("fishing spot linked"));
        playsound(spot, Dm.resource("sound/machines/ping.ogg"), 15, TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:142
    [SignalHandler]
    public virtual void unlink_fishing_spot(atom spot = null)
    {
        datum_fish_source source = linked_fishing_spots?[spot] as datum_fish_source;
        if (active != null && active.fish_source == source)
            Waiting.NoSleep(deactivate());
        LAZYREMOVE(ref linked_fishing_spots, spot);
        UnregisterSignal(spot, SIGNAL_REMOVETRAIT(TRAIT_FISHING_SPOT));
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:150
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You can unlock further portal settings by completing fish scanning experiments, or by connecting it to other fishing spots with a multitool.")));
        return dot;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:155
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        Dm.combine(ref obj_flags, EMAGGED);
        balloon_alert(user, I18n.T("syndicate setting loaded"));
        playsound(this, SFX_SPARKS, 25, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        return TRUE;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:163
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (active != null)
            await deactivate();
        else
            await select_fish_source(user);
        return dot;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:170
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, "portal_open");
        if (active == null)
            return dot;
        Dm.append(ref dot, "portal_on");
        datum_fish_source portal = active.fish_source;
        Dm.append(ref dot, portal.overlay_state);
        // DEVIATION(DV12): . += emissive_appearance(icon, "portal_emissive", src)
        return dot;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:181
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        await check_fishing_spot_z();
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:185
    public virtual async DmTask check_fishing_spot_z()
    {
        if (active == null || Dm.truthy(long_range_link) || istype(active.fish_source, typeof(datum_fish_source_portal)))
            return;
        turf new_turf = get_turf(this);
        if (new_turf == null)
        {
            await deactivate();
            return;
        }
        foreach (atom spot in Dm.each<atom>(linked_fishing_spots))
        {
            if (linked_fishing_spots[spot] != active?.fish_source)
                continue;
            turf turf = get_turf(spot);
            if (turf.z != new_turf.z && !(Dm.truthy(is_station_level(turf.z)) && Dm.truthy(is_station_level(new_turf.z))))
                await deactivate();
        }
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:199
    public virtual async DmTask<object> activate(datum_fish_source selected_source = null, mob user = null)
    {
        if (QDELETED(selected_source))
            return null;
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            balloon_alert(user, I18n.T("no power!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(all_destinations) && !istype(selected_source, typeof(datum_fish_source_portal))) //likely from a linked fishing spot
        {
            bool abort = true;
            foreach (atom spot in Dm.each<atom>(linked_fishing_spots))
            {
                if (linked_fishing_spots[spot] != selected_source)
                    continue;
                if (Dm.truthy(long_range_link))
                    abort = false;
                turf spot_turf = get_turf(spot);
                turf turf = get_turf(this);
                if (turf.z == spot_turf.z || (Dm.truthy(is_station_level(turf.z)) && Dm.truthy(is_station_level(spot_turf.z))))
                    abort = false;
                if (!abort)
                {
                    RegisterSignal(spot, COMSIG_MOVABLE_Z_CHANGED, nameof(on_fishing_spot_z_level_changed));
                    current_linked_atom = spot;
                }
                break;
            }
            if (abort && !Dm.truthy(all_destinations))
            {
                balloon_alert(user, I18n.T("cannot reach linked!"));
                return null;
            }
        }

        active = await AddComponent(typeof(datum_component_fishing_spot), selected_source) as datum_component_fishing_spot;
        ADD_TRAIT(this, TRAIT_CATCH_AND_RELEASE, INNATE_TRAIT);
        update_use_power(ACTIVE_POWER_USE);
        await update_icon();
        return null;
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:229
    public virtual async DmTask deactivate()
    {
        if (active == null)
            return;
        if (!istype(active.fish_source, typeof(datum_fish_source_portal)))
        {
            foreach (atom spot in Dm.each<atom>(linked_fishing_spots))
            {
                if (linked_fishing_spots[spot] == active.fish_source)
                    UnregisterSignal(spot, COMSIG_MOVABLE_Z_CHANGED);
            }
            current_linked_atom = null;
        }
        QDEL_NULL(ref active);

        REMOVE_TRAIT(this, TRAIT_CATCH_AND_RELEASE, INNATE_TRAIT);
        if (!QDELETED(this))
        {
            update_use_power(IDLE_POWER_USE);
            await update_icon();
        }
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:244
    [SignalHandler]
    public virtual void on_fishing_spot_z_level_changed(atom spot = null, turf old_turf = null, turf new_turf = null, object same_z_layer = null)
    {
        turf turf = get_turf(this);
        if (turf.z != new_turf.z && !(Dm.truthy(is_station_level(turf.z)) && Dm.truthy(is_station_level(new_turf.z))))
            Waiting.NoSleep(deactivate());
    }

    // tg: code/modules/fishing/fishing_portal_machine.dm:250
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        if (Dm.truthy(old_value))
            await deactivate();
    }

    ///Create a radial menu from a list of available fish sources. If only the default is available, activate it right away.
    // tg: code/modules/fishing/fishing_portal_machine.dm:255
    public virtual async DmTask select_fish_source(mob user = null)
    {
        datum_fish_source_portal @default = GLOB.preset_fish_sources[typeof(datum_fish_source_portal)] as datum_fish_source_portal;
        DmList available_fish_sources = Dm.list(Dm.kv(@default.radial_name, @default));
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            datum_fish_source_portal syndicate = GLOB.preset_fish_sources[typeof(datum_fish_source_portal_syndicate)] as datum_fish_source_portal;
            available_fish_sources[syndicate.radial_name] = syndicate;
        }
        if (Dm.truthy(all_destinations))
            available_fish_sources = GLOB.preset_fish_sources;
        else
        {
            foreach (datum_techweb techweb in Dm.each<datum_techweb>(SSresearch.techwebs))
            {
                bool get_fish_sources = false;
                foreach (obj_machinery_rnd_server server in Dm.each<obj_machinery_rnd_server>(techweb.techweb_servers))
                {
                    if (!Dm.truthy(is_valid_z_level(get_turf(server), get_turf(this))))
                        continue;
                    get_fish_sources = true;
                    break;
                }
                if (!get_fish_sources)
                    continue;
                foreach (Type experiment_type in Dm.each<Type>(typesof(typeof(datum_experiment_scanning_fish))))
                {
                    datum_experiment_scanning_fish experiment = techweb.completed_experiments?[experiment_type] as datum_experiment_scanning_fish;
                    if (experiment == null)
                        continue;
                    datum_fish_source_portal reward = GLOB.preset_fish_sources[experiment.fish_source_reward] as datum_fish_source_portal;
                    available_fish_sources[reward.radial_name] = reward;
                }
            }
        }

        float id = 1;
        foreach (atom spot in Dm.each<atom>(linked_fishing_spots))
        {
            string choice_name = Dm.text("[] - []", id, spot.name);
            available_fish_sources[choice_name] = linked_fishing_spots[spot];
            id++;
        }

        if (length(available_fish_sources) == 1)
        {
            await activate(@default, user);
            return;
        }
        DmList choices = Dm.list();
        foreach (object radial_name in Dm.each<object>(available_fish_sources))
        {
            datum_fish_source source = available_fish_sources[radial_name] as datum_fish_source;
            mutable_appearance radial_icon = mutable_appearance(Dm.resource("icons/hud/radial_fishing.dmi"), source.radial_state) as mutable_appearance;
            if (!istype(source, typeof(datum_fish_source_portal)))
            {
                //a little star on the top-left to distinguishs them from standard portals.
                // DEVIATION(DV682): tg's two-argument add_overlay on an image (:292) overlays the bare icon file; the port draws the state.
                Dm.combine(radial_icon.overlays, mutable_appearance(Dm.resource("icons/hud/radial_fishing.dmi"), "linked_source"));
            }
            choices[radial_name] = radial_icon;
        }

        object choice = await show_radial_menu(user, this, choices, radius: 38, custom_check: Dm.@new<datum_callback>(this, nameof(can_interact), user), tooltips: TRUE);
        if (!Dm.truthy(choice) || !Dm.truthy(await can_interact(user)))
            return;
        await activate(available_fish_sources[choice] as datum_fish_source, user);
    }
}

public sealed partial class TgState
{
    internal DmList obj_machinery_fishing_portal_generator__Initialize__tool_screentips;
}
