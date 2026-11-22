// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_ui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. The MODsuit window (tgui MODsuit): the suit's core charge and its bar, its activity, its AI
// or pAI assistant, its MODlink id, frequency and call, its wires' state, its lock and complexity; the wearer's name and job; each
// module with its powers, cooldown, configuration and pin; each part with its slot and whether it is deployed. Its acts lock and
// unlock the suit, call or hang up, turn the suit on, select, configure and pin a module, deploy or retract a part and eject the
// pAI. An AI assistant uses the window from inside the suit (contained state).

public partial class obj_item_mod_control
{
    // tg: code/modules/mod/mod_ui.dm:1
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "MODsuit", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mod/mod_ui.dm:7
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        // Suit information
        DmList suit_status = Dm.list(
            Dm.kv("core_name", core?.name),
            Dm.kv("charge_current", get_charge()),
            Dm.kv("charge_max", get_max_charge()),
            Dm.kv("chargebar_color", get_chargebar_color()),
            Dm.kv("chargebar_string", get_chargebar_string()),
            Dm.kv("active", active),
            Dm.kv("ai_name", ai_assistant?.name),
            Dm.kv("has_pai", Dm.num(ispAI(ai_assistant))),
            Dm.kv("is_ai", Dm.num(ai_assistant != null && ai_assistant == user)),
            Dm.kv("link_id", mod_link.id),
            Dm.kv("link_freq", mod_link.frequency),
            Dm.kv("link_call", (mod_link.get_other() as datum_mod_link)?.id),
            // Wires
            Dm.kv("open", open),
            Dm.kv("seconds_electrified", seconds_electrified),
            Dm.kv("malfunctioning", malfunctioning),
            Dm.kv("locked", locked),
            Dm.kv("interface_break", interface_break),
            // Modules
            Dm.kv("complexity", complexity)
        );
        data["suit_status"] = suit_status;
        // User information
        DmList user_status = Dm.list(
            Dm.kv("user_name", wearer != null ? (wearer.get_authentification_name(I18n.T("Unknown")) is var or__1 && Dm.truthy(or__1) ? or__1 : I18n.T("Unknown")) : ""),
            Dm.kv("user_assignment", wearer != null ? wearer.get_assignment(I18n.T("Unknown"), I18n.T("Unknown"), FALSE) : "")
        );
        data["user_status"] = user_status;
        // Module information
        DmList module_custom_status = Dm.list();
        DmList module_info = Dm.list();
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
        {
            Dm.append(ref module_custom_status, module.add_ui_data());
            Dm.append(ref module_info, Dm.list(Dm.list(
                Dm.kv("module_name", module.name),
                Dm.kv("description", module.desc),
                Dm.kv("module_type", module.module_type),
                Dm.kv("module_active", module.active),
                Dm.kv("pinned", module.pinned_to[REF(user)]),
                Dm.kv("idle_power", module.idle_power_cost),
                Dm.kv("active_power", module.active_power_cost),
                Dm.kv("use_energy", module.use_energy_cost),
                Dm.kv("module_complexity", module.complexity),
                Dm.kv("cooldown_time", module.cooldown_time),
                Dm.kv("cooldown", round(max(0, module.cooldown_timer - world.time), 1 * SECONDS)), // COOLDOWN_TIMELEFT(module, cooldown_timer)
                Dm.kv("id", module.tgui_id),
                Dm.kv("ref", REF(module)),
                Dm.kv("configuration_data", module.get_configuration(user))
            )));
        }
        data["module_custom_status"] = module_custom_status;
        data["control"] = name;
        data["module_info"] = module_info;
        DmList part_info = Dm.list();
        foreach (obj_item part in Dm.each_cast<obj_item>(get_parts()))
        {
            Dm.append(ref part_info, Dm.list(Dm.list(
                Dm.kv("slot", english_list(parse_slot_flags(part.slot_flags) as DmList)),
                Dm.kv("name", part.name),
                Dm.kv("deployed", Dm.num(part.loc != this)),
                Dm.kv("ref", REF(part))
            )));
        }
        data["parts"] = part_info;
        return data;
    }

    // tg: code/modules/mod/mod_ui.dm:74
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["ui_theme"] = ui_theme;
        data["complexity_max"] = complexity_max;
        return data;
    }

    // tg: code/modules/mod/mod_ui.dm:80
    public override object ui_state(mob user = null)
    {
        if (user == ai_assistant)
            return GLOB.contained_state;
        return base.ui_state(user);
    }

    // tg: code/modules/mod/mod_ui.dm:85
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(malfunctioning) && prob(MOD_MALFUNCTION_PROB))
        {
            balloon_alert(ui.user, I18n.T("button malfunctions!"));
            return dot;
        }
        switch (action as string)
        {
            case "lock":
                if (!Dm.truthy(locked) || Dm.truthy(await allowed__proc(ui.user)))
                {
                    locked = Dm.num(!Dm.truthy(locked));
                    balloon_alert(ui.user, Dm.truthy(locked) ? I18n.T("locked") : I18n.T("unlocked"));
                }
                else
                {
                    balloon_alert(ui.user, I18n.T("access insufficent!"));
                    playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
                }
                break;
            case "call":
                if (mod_link.link_call == null)
                    await call_link(ui.user, mod_link);
                else
                    mod_link.end_call();
                break;
            case "activate":
                await toggle_activate(ui.user);
                break;
            case "select":
            {
                obj_item_mod_module module = Dm.locate_ref(@params["ref"]) is obj_item_mod_module found && Dm.@in(found, modules) ? found : null;
                if (module == null)
                    return dot;
                await module.on_select(ui.user); // We can now
                break;
            }
            case "configure":
            {
                obj_item_mod_module module = Dm.locate_ref(@params["ref"]) is obj_item_mod_module found && Dm.@in(found, modules) ? found : null;
                if (module == null)
                    return dot;
                await module.configure_edit(@params["key"], @params["value"]);
                break;
            }
            case "pin":
            {
                obj_item_mod_module module = Dm.locate_ref(@params["ref"]) is obj_item_mod_module found && Dm.@in(found, modules) ? found : null;
                if (module == null)
                    return dot;
                await module.pin(ui.user);
                break;
            }
            case "deploy":
            {
                obj_item mod_part = Dm.locate_ref(@params["ref"]) is obj_item found && Dm.@in(found, get_parts()) ? found : null;
                if (mod_part == null)
                    return dot;
                if (mod_part.loc == this)
                    await deploy(ui.user, mod_part);
                else
                    await retract(ui.user, mod_part);
                break;
            }
            case "eject_pai":
                if (!ishuman(ui.user))
                    return dot;
                await remove_pai(ui.user);
                break;
        }
        return TRUE;
    }
}
