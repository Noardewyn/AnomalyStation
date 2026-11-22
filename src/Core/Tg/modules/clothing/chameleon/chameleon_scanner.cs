// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/chameleon/chameleon_scanner.dm and
// code/modules/clothing/chameleon/chameleon_action_subtypes.dm (the scanner's action)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G1b (D316): the chameleon scanner (a disguised pen or gadget that scans a person's outfit into a custom
// chameleon outfit, and with the right click puts it on at once) and its action, both whole. The var blocks are generated (D111).

/// Small handheld chameleon item that allows a user to mimic the outfit of another person quickly.
public partial class obj_item_chameleon_scanner : obj_item
{
    // tg: code/modules/clothing/chameleon/chameleon_scanner.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_item_context();
        return dot;
    }

    // tg: code/modules/clothing/chameleon/chameleon_scanner.dm:21
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (target is mob_living_carbon_human && Dm.truthy(IS_TRAITOR(user)))
        {
            // probably don't want to give out the context to any old crewmember -
            // even though anyone can use it, it kind of reveals the fact that it's disguised
            // (though it's trivial to find that out anyway)
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Scan target outfit");
            context[SCREENTIP_CONTEXT_RMB] = I18n.T("Scan target outfit and equip");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return NONE;
    }

    // tg: code/modules/clothing/chameleon/chameleon_scanner.dm:37
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(IS_TRAITOR(user)))
            return dot;
        // similar to context, we don't want a bunch of text revealing "THIS IS A DISGUISED ITEM" to everyone on examine.
        // despite the fact that anyone can use it, we'll only show it to traitors, everyone else just has to figure it out.
        Dm.append(dot, span_red(I18n.T("There's a small button on the bottom side of it. You recognize this as a hidden <i>Chameleon Scanner 6000</i>.")));
        Dm.append(dot, span_red(I18n.T("<b>Left click</b> will stealthily scan a target up to [] meters away and upload their getup as a custom outfit for you to use.", scan_range)));
        Dm.append(dot, span_red(I18n.T("<b>Right click</b> will do the same, but instantly equip the outfit you obtain.")));
        return dot;
    }

    // tg: code/modules/clothing/chameleon/chameleon_scanner.dm:47
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return Dm.truthy(await scan_target(interacting_with, user)) ? ITEM_INTERACT_SUCCESS : NONE;
    }

    // tg: code/modules/clothing/chameleon/chameleon_scanner.dm:50
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/modules/clothing/chameleon/chameleon_scanner.dm:53
    public override async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        DmList scanned_outfit = await scan_target(interacting_with, user) as DmList;
        if (Dm.length(scanned_outfit) == 0)
            return NONE;
        var empty_outfit = Dm.@new<datum_outfit>();
        var outfit_action = Dm.locate<datum_action_chameleon_outfit>(user.actions) as datum_action_chameleon_outfit;
        if (outfit_action != null)
            await outfit_action.apply_outfit(empty_outfit, scanned_outfit.Copy());
        qdel(empty_outfit);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/clothing/chameleon/chameleon_scanner.dm:63
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await ranged_interact_with_atom_secondary(interacting_with, user, modifiers);
    }

    /**
     * Attempts to scan a human's outfit
     *
     * * scanned - the atom being scanned.
     * * scanner - the mob doing the scanning
     *
     * Returns null or a list of paths scanned. Will not return an empty list.
     */
    // tg: code/modules/clothing/chameleon/chameleon_scanner.dm:74
    public virtual async DmTask<object> scan_target(atom scanned = null, mob scanner = null)
    {
        mob_living_carbon_human mob_copying = null;
        if (scanned is mob_living_carbon_human scanned_human)
            mob_copying = scanned_human;
        else if (scanned is turf)
            mob_copying = Dm.locate<mob_living_carbon_human>(scanned.contents) as mob_living_carbon_human; // Aim assist

        if (mob_copying == null)
            return null;

        if (scan_cooldown > Dm.world.time) // COOLDOWN_FINISHED, inline
        {
            balloon_alert(scanner, "not ready yet!");
            return null;
        }
        if (Dm.num(get_dist(scanner, mob_copying)) > scan_range)
        {
            balloon_alert(scanner, "too far away!");
            return null;
        }
        // Very short scan timer, keep you on your toes
        if (!Dm.truthy(await do_after(scanner, 0.5f * SECONDS, scanned, cog_icon: null)))
            return null;

        DmList all_scanned_items = Dm.list();
        foreach (obj_item thing in Dm.each<obj_item>(mob_copying.get_equipped_items()))
        {
            var counter_chameleon = Dm.locate<datum_action_item_action_chameleon_change>(thing.actions) as datum_action_item_action_chameleon_change;
            if (counter_chameleon?.active_type != null)
                // Prevent counter spying
                Dm.combine(ref all_scanned_items, counter_chameleon.active_type);
            else
                // I guess this technically serves as a way to discover changling clothes at a distance...??
                // Not sure if that's a good thing or not.
                Dm.combine(ref all_scanned_items, thing.type);
        }

        if (Dm.length(all_scanned_items) == 0)
        {
            scanned.balloon_alert(scanner, "nothing to scan!");
            return null;
        }

        playsound(this, "sound/machines/ping.ogg", vol: 30, vary: TRUE, extrarange: SILENCED_SOUND_EXTRARANGE);
        scan_cooldown = Dm.world.time + 5 * SECONDS; // COOLDOWN_START, inline

        var outfit_action = Dm.locate<datum_action_chameleon_outfit>(scanner.actions) as datum_action_chameleon_outfit;
        outfit_action?.save_outfit(scanner, all_scanned_items);

        return all_scanned_items;
    }
}

public partial class datum_action_item_action_chameleon_change_scanner : datum_action_item_action_chameleon_change
{
    /// Other types the chameleon scanner can swap into in addition to the chameleon type
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:322  var/static/list/other_cham_types (a constant list: built where it is read)
    private static DmList other_cham_types() => Dm.list(
        typeof(obj_item_analyzer),
        typeof(obj_item_assembly_flash_handheld),
        typeof(obj_item_assembly_signaler),
        typeof(obj_item_autopsy_scanner),
        typeof(obj_item_camera),
        typeof(obj_item_cane),
        typeof(obj_item_detective_scanner),
        typeof(obj_item_door_remote),
        typeof(obj_item_flashlight),
        typeof(obj_item_geiger_counter),
        typeof(obj_item_healthanalyzer),
        typeof(obj_item_mining_scanner),
        typeof(obj_item_multitool),
        typeof(obj_item_plant_analyzer),
        typeof(obj_item_sensor_device),
        typeof(obj_item_storage_fancy_cigarettes), // Fanservice
        typeof(obj_item_taperecorder),
        typeof(obj_item_toy_crayon));
    // (Other ideas include: GPSs, PDAs, station bounced radios, holosign creators, etc. But this is good enough)

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:343
    public override async DmTask initialize_blacklist()
    {
        await base.initialize_blacklist();
        Dm.combine(ref chameleon_blacklist, typecacheof(Dm.list(
            typeof(obj_item_assembly_signaler_anomaly),
            typeof(obj_item_camera_siliconcam),
            typeof(obj_item_door_remote_omni),
            typeof(obj_item_flashlight_emp_debug),
            typeof(obj_item_flashlight_flare),
            typeof(obj_item_flashlight_lamp),
            typeof(obj_item_healthanalyzer_rad_laser),
            typeof(obj_item_multitool_ai_detect),
            typeof(obj_item_multitool_cyborg),
            typeof(obj_item_multitool_drone),
            typeof(obj_item_multitool_field_debug),
            typeof(obj_item_storage_fancy_cigarettes_cigars))));
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:360
    public override async DmTask initialize_disguises()
    {
        await base.initialize_disguises();
        foreach (Type other_type in Dm.each<Type>(other_cham_types()))
            add_chameleon_items(other_type);
    }
}
