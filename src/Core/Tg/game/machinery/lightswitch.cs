// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/lightswitch.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 lighting slice L1 (D168): the whole file, with its circuit component on the USB port. The wall light switch flips its
// area's lightswitch and tells the area's machines through power_change (the fixtures switch on or off); the wall frame it
// leaves is generated. Deferred leaves: the turf's hand redirect (Initialize), the emissive glow (DEVIATION DV12). The var block is generated: tg's `area` is
// an object there (the maps write a path or text; Initialize resolves it to the area instance, read through `the_area`).

// tg: code/game/machinery/lightswitch.dm:2
public partial class obj_machinery_light_switch : obj_machinery
{
    /// tg's `area` (lightswitch.dm:13) after Initialize has resolved it to an area instance.
    private area the_area => area as area;

    // tg: code/game/machinery/lightswitch.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // AddComponent(/datum/component/redirect_attack_hand_from_turf) (clicking the wall below, clicks)
        Dm.Deferred("code/game/machinery/lightswitch.dm:28", "Initialize: redirect_attack_hand_from_turf (clicking the wall below, M4a)");

        Waiting.NoSleep(AddComponent(typeof(datum_component_usb_port), typecacheof(Dm.list(typeof(obj_item_circuit_component_light_switch)), only_root_path: TRUE)));
        if (istext(area))
            area = text2path((string)area);
        if (ispath(area))
            area = GLOB.areas_by_type[area];
        if (!Dm.truthy(area))
            area = get_area(this);
        if (Dm.truthy(autoname))
            name = I18n.T("light switch ([])", the_area.name);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        register_context();
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/lightswitch.dm:44
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(the_area.lightswitch) ? "Flick off" : "Flick on";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/machinery/lightswitch.dm:54
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);
        luminosity = Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) ? 0 : 1;
        return dot;
    }

    // tg: code/game/machinery/lightswitch.dm:58
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        await set_light(Dm.truthy(the_area.lightswitch) ? 0 : light_on_range);
        icon_state = Dm.text("[]", base_icon_state);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            icon_state += "-nopower";
            return await base.update_icon_state(__extra);
        }
        icon_state += Dm.truthy(the_area.lightswitch) ? "-on" : "-off";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/lightswitch.dm:67
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return await base.update_overlays(__extra);
        // DEVIATION(DV12): . += emissive_appearance(icon, "[base_icon_state]-emissive[area.lightswitch ? "-on" : "-off"]", src, alpha = src.alpha)
        Dm.Deferred("code/game/machinery/lightswitch.dm:71", "update_overlays: emissive_appearance of the switch's glow (DEVIATION DV12)");
        return dot;
    }

    // tg: code/game/machinery/lightswitch.dm:73
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("It is [].",
            Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) ? I18n.T("unpowered") : (Dm.truthy(the_area.lightswitch) ? I18n.T("on") : I18n.T("off"))));
        Dm.append(ref dot, span_notice(I18n.T("It's <b>screwed</b> and secured to the wall.")));
        return dot;
    }

    // tg: code/game/machinery/lightswitch.dm:78
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        playsound(this, Dm.truthy(the_area.lightswitch) ? sound_off : sound_on, 40, TRUE);
        set_lights(Dm.truthy(the_area.lightswitch) ? FALSE : TRUE);
        return dot;
    }

    // tg: code/game/machinery/lightswitch.dm:83
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        user.visible_message(span_notice(I18n.T("[] starts unscrewing []...", user, this)), span_notice(I18n.T("You start unscrewing []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        user.visible_message(span_notice(I18n.T("[] unscrews []!", user, this)), span_notice(I18n.T("You detach [] from the wall.", this)));
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/machinery/lightswitch.dm:88
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/lightswitch.dm:92
    public virtual void set_lights(object status = null)
    {
        area own_area = the_area;
        if (Dm.eq(own_area.lightswitch, status))
            return;
        own_area.lightswitch = Dm.num(status);
        own_area.update_appearance();

        foreach (obj_machinery_light_switch light_switch in Dm.each_cast<obj_machinery_light_switch>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_light_switch))))
        {
            if (!ReferenceEquals(light_switch.area, own_area))
                continue;
            light_switch.update_appearance();
            if (Dm.truthy(light_switch._listen_lookup?[COMSIG_LIGHT_SWITCH_SET]))
                light_switch._SendSignal(COMSIG_LIGHT_SWITCH_SET, Dm.list(light_switch, status));
        }

        own_area.power_change();
    }

    // tg: code/game/machinery/lightswitch.dm:106  (SHOULD_CALL_PARENT(FALSE): the parent runs only for the switch's own area)
    public override object power_change()
    {
        if (ReferenceEquals(area, get_area(this)))
            return base.power_change();
        return null;
    }

    // tg: code/game/machinery/lightswitch.dm:111
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (!Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            power_change();
        return dot;
    }

    // tg: code/game/machinery/lightswitch.dm:118
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_wallframe_light_switch>(loc);
        return null;
    }
}

// tg: code/game/machinery/lightswitch.dm:130
public partial class obj_item_circuit_component_light_switch : obj_item_circuit_component
{
    // tg: code/game/machinery/lightswitch.dm:142
    public override async DmTask populate_ports()
    {
        on_setting = add_input_port("On", PORT_TYPE_BOOLEAN) as datum_port_input;
        is_on = add_output_port("Is On", PORT_TYPE_BOOLEAN) as datum_port_output;
    }

    // tg: code/game/machinery/lightswitch.dm:146
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_light_switch)))
        {
            attached_switch = shell as obj_machinery_light_switch;
            RegisterSignal(shell, COMSIG_LIGHT_SWITCH_SET, nameof(on_light_switch_set));
        }
    }

    // tg: code/game/machinery/lightswitch.dm:152
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        attached_switch = null;
        UnregisterSignal(shell, COMSIG_LIGHT_SWITCH_SET);
        return base.unregister_usb_parent(shell);
    }

    // tg: code/game/machinery/lightswitch.dm:157
    [SignalHandler]
    public virtual void on_light_switch_set(datum source = null, object status = null)
    {
        is_on.set_output(status);
    }

    // tg: code/game/machinery/lightswitch.dm:161
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        attached_switch?.set_lights(Dm.truthy(on_setting.value) ? TRUE : FALSE);
        return null;
    }
}
