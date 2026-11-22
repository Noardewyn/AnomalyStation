// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mecha_ui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/mecha_ui.dm:1
public partial class obj_vehicle_sealed_mecha : obj_vehicle_sealed
{
    // tg: code/modules/vehicles/mecha/mecha_ui.dm:30
    // list(get_asset_datum(/datum/asset/spritesheet_batched/mecha_equipment)): the server sends no assets (DV44), the window reads the .dmi pictures
    public override DmTask<object> ui_assets(mob user = null) => DmTask<object>.FromResult(Dm.list());

    // tg: code/modules/vehicles/mecha/mecha_ui.dm:1
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        ui_view.hide_from(user);
    }

    // tg: code/modules/vehicles/mecha/mecha_ui.dm:5
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Mecha", name);
            await ui.open();
            ui_view.display_to(user, ui.window);
        }
        return null;
    }

    // tg: code/modules/vehicles/mecha/mecha_ui.dm:12
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        float common_status = Math.Min(
            Dm.num(ui_status_user_is_abled(user, this)),
            Dm.num(ui_status_only_living(user)));
        float mob_specific_status = UI_INTERACTIVE;
        if (ishuman(user))
        {
            mob_specific_status = Math.Min(Math.Min(
                Dm.num(ui_status_user_inside(user, this)),
                Dm.num(ui_status_user_has_free_hands(user, this, allowed_source: VEHICLE_TRAIT))),
                Dm.num(ui_status_user_is_advanced_tool_user(user)));
        }
        if (isAI(user))
            mob_specific_status = Dm.num(ui_status_silicon_has_access(user, this));
        if (isbrain(user))
            mob_specific_status = Dm.num(ui_status_user_inside(user, this));
        return Math.Min(common_status, mob_specific_status);
    }

    // tg: code/modules/vehicles/mecha/mecha_ui.dm:35
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["ui_theme"] = ui_theme;
        //same thresholds as in air alarm
        data["cabin_pressure_warning_min"] = WARNING_LOW_PRESSURE;
        data["cabin_pressure_hazard_min"] = HAZARD_LOW_PRESSURE;
        data["cabin_pressure_warning_max"] = WARNING_HIGH_PRESSURE;
        data["cabin_pressure_hazard_max"] = HAZARD_HIGH_PRESSURE;
        data["cabin_temp_warning_min"] = BODYTEMP_COLD_WARNING_1 + 10 - T0C;
        data["cabin_temp_hazard_min"] = BODYTEMP_COLD_WARNING_1 - T0C;
        data["cabin_temp_warning_max"] = BODYTEMP_HEAT_WARNING_1 - 27 - T0C;
        data["cabin_temp_hazard_max"] = BODYTEMP_HEAT_WARNING_1 - T0C;
        data["one_atmosphere"] = ONE_ATMOSPHERE;

        data["sheet_material_amount"] = SHEET_MATERIAL_AMOUNT;
        //map of relevant flags to check tgui side, not every flag needs to be here
        data["mechflag_keys"] = Dm.list(
            Dm.kv("ID_LOCK_ON", ID_LOCK_ON),
            Dm.kv("LIGHTS_ON", LIGHTS_ON),
            Dm.kv("HAS_LIGHTS", HAS_LIGHTS));
        data["internal_damage_keys"] = Dm.list(
            Dm.kv("MECHA_INT_FIRE", MECHA_INT_FIRE),
            Dm.kv("MECHA_INT_TEMP_CONTROL", MECHA_INT_TEMP_CONTROL),
            Dm.kv("MECHA_CABIN_AIR_BREACH", MECHA_CABIN_AIR_BREACH),
            Dm.kv("MECHA_INT_CONTROL_LOST", MECHA_INT_CONTROL_LOST),
            Dm.kv("MECHA_INT_SHORT_CIRCUIT", MECHA_INT_SHORT_CIRCUIT));
        data["diagnostic_status"] = HAS_TRAIT(this, TRAIT_MECHA_DIAGNOSTIC_CREATED) ? TRUE : FALSE;

        DmList regions = Dm.list();
        DmList tgui_region_data = SSid_access.all_region_access_tgui;
        foreach (object region in Dm.each<object>(SSid_access.station_regions))
            regions.Add(tgui_region_data[region]);
        data["regions"] = regions;
        return data;
    }

    // tg: code/modules/vehicles/mecha/mecha_ui.dm:72
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        object isoperator = Dm.@in(user, occupants) ? TRUE : FALSE; //maintenance mode outside of mech
        data["isoperator"] = isoperator;
        data["cell"] = cell?.name;
        data["scanning"] = scanmod?.name;
        data["capacitor"] = capacitor?.name;
        data["servo"] = servo?.name;
        ui_view.appearance = appearance;
        data["name"] = name;
        data["integrity"] = atom_integrity;
        data["integrity_max"] = max_integrity;
        data["power_level"] = cell?.charge;
        data["power_max"] = cell?.maxcharge;
        data["mecha_flags"] = mecha_flags;
        data["internal_damage"] = internal_damage;

        data["can_use_overclock"] = can_use_overclock;
        data["overclock_safety_available"] = overclock_safety_available;
        data["overclock_safety"] = overclock_safety;
        data["overclock_mode"] = overclock_mode;
        data["overclock_temp_percentage"] = overclock_temp / overclock_temp_danger;

        data["dna_lock"] = dna_lock;

        data["one_access"] = one_access;
        data["accesses"] = accesses;

        data["servo_rating"] = servo?.rating;
        data["scanmod_rating"] = scanmod?.rating;
        data["capacitor_rating"] = capacitor?.rating;

        data["weapons_safety"] = weapons_safety;
        data["enclosed"] = Dm.bitand(mecha_flags, IS_ENCLOSED);
        data["cabin_sealed"] = cabin_sealed;
        data["cabin_temp"] = round(Dm.num(cabin_air.temperature) - T0C);
        data["cabin_pressure"] = round(Dm.num(cabin_air.return_pressure()));
        data["mech_view"] = ui_view.assigned_map;
        data["modules"] = get_module_ui_data();
        data["selected_module_index"] = ui_selected_module_index;
        return data;
    }

    // tg: code/modules/vehicles/mecha/mecha_ui.dm:114
    public virtual DmList get_module_ui_data()
    {
        DmList data = Dm.list();
        float module_index = 0;
        foreach (object category in Dm.each<object>(max_equip_by_category))
        {
            float max_per_category = Dm.num(max_equip_by_category[category]);
            if (max_per_category != 0)
            {
                for (int i = 1; i <= max_per_category; i++)
                {
                    object equipment = equip_by_category[category];
                    bool is_slot_free = islist(equipment) ? i > Dm.num(length(equipment)) : equipment == null;
                    if (is_slot_free)
                    {
                        data.Add(Dm.list(Dm.list(Dm.kv("slot", category))));
                        if (Dm.eq(ui_selected_module_index, module_index))
                            ui_selected_module_index = null;
                    }
                    else
                    {
                        obj_item_mecha_parts_mecha_equipment module = (islist(equipment) ? (equipment as DmList)[i] : equipment) as obj_item_mecha_parts_mecha_equipment;
                        data.Add(Dm.list(Dm.list(
                            Dm.kv("slot", category),
                            Dm.kv("icon", module.icon_state),
                            Dm.kv("name", module.name),
                            Dm.kv("desc", module.desc),
                            Dm.kv("detachable", module.detachable),
                            Dm.kv("integrity", Dm.num(module.get_integrity()) / Dm.num(module.max_integrity)),
                            Dm.kv("can_be_toggled", module.can_be_toggled),
                            Dm.kv("can_be_triggered", module.can_be_triggered),
                            Dm.kv("active", module.active),
                            Dm.kv("active_label", module.active_label),
                            Dm.kv("equip_cooldown", Dm.truthy(module.equip_cooldown) ? DisplayTimeText(module.equip_cooldown) : module.equip_cooldown),
                            Dm.kv("energy_per_use", module.energy_drain),
                            Dm.kv("snowflake", module.get_snowflake_data()),
                            Dm.kv("ref", REF(module)))));
                        if (ui_selected_module_index == null)
                            ui_selected_module_index = module_index;
                    }
                    module_index++;
                }
            }
        }
        return data;
    }

    // tg: code/modules/vehicles/mecha/mecha_ui.dm:152
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        string act = action as string;
        switch (act)
        {
            case "clear_all":
                accesses = Dm.list();
                one_access = 0;
                update_access();
                break;
            case "grant_all":
                accesses = SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)) as DmList;
                update_access();
                break;
            case "one_access":
                one_access = Dm.truthy(one_access) ? 0 : 1;
                update_access();
                break;
            case "set":
            {
                object access = @params["access"];
                if (!Dm.@in(access, accesses))
                    accesses.Add(access);
                else
                    Dm.remove(accesses, access);
                update_access();
                break;
            }
            case "grant_region":
            {
                object region = @params["region"];
                if (region == null)
                    return null;
                Dm.combine(ref accesses, SSid_access.get_region_access_list(Dm.list(region)));
                update_access();
                break;
            }
            case "deny_region":
            {
                object region = @params["region"];
                if (region == null)
                    return null;
                Dm.remove(accesses, SSid_access.get_region_access_list(Dm.list(region)));
                update_access();
                break;
            }
            case "select_module":
                ui_selected_module_index = text2num(@params["index"]);
                return TRUE;
            case "changename":
            {
                object userinput = await tgui_input_text(usr, I18n.T("Choose a new exosuit name"), I18n.T("Rename exosuit"), max_length: MAX_NAME_LEN, @default: name);
                if (!Dm.truthy(userinput))
                    return null;
                if (Dm.truthy(is_ic_filtered(userinput)) || Dm.truthy(is_soft_ic_filtered(userinput)))
                {
                    await tgui_alert(usr, I18n.T("You cannot set a name that contains a word prohibited in IC chat!"));
                    return null;
                }
                if (Dm.eq(userinput, format_text(name))) //default mecha names may have improper span artefacts in their name, so we format the name
                {
                    to_chat(usr, span_notice(I18n.T("You rename [] to... well, [].", name, userinput)));
                    return null;
                }
                name = Dm.text("\\proper []", userinput);
                chassis_camera?.update_c_tag(this);
                break;
            }
            case "toggle_safety":
                set_safety(usr);
                return null;
            case "dna_lock":
            {
                mob_living_carbon user = usr as mob_living_carbon;
                if (user == null || user.dna == null)
                {
                    to_chat(usr, span_notice(I18n.T("You can't create a DNA lock with no DNA!.")));
                    return null;
                }
                dna_lock = user.dna.unique_enzymes;
                to_chat(user, span_notice(I18n.T("You feel a prick as the needle takes your DNA sample.")));
                break;
            }
            case "reset_dna":
                dna_lock = null;
                break;
            case "toggle_cabin_seal":
                await set_cabin_seal(usr, Dm.truthy(cabin_sealed) ? FALSE : TRUE);
                break;
            case "toggle_id_lock":
                mecha_flags = Dm.bitxor(mecha_flags, ID_LOCK_ON);
                break;
            case "toggle_lights":
                await toggle_lights(user: usr);
                break;
            case "toggle_overclock":
                await toggle_overclock();
                break;
            case "toggle_overclock_safety":
                overclock_safety = Dm.truthy(overclock_safety) ? 0 : 1;
                break;
            case "repair_int_damage":
                await try_repair_int_damage(usr, @params["flag"]);
                return FALSE;
            case "equip_act":
            {
                obj_item_mecha_parts_mecha_equipment gear = Dm.locate_ref(@params["ref"]) is obj_item_mecha_parts_mecha_equipment found && Dm.@in(found, flat_equipment) ? found : null;
                return gear == null ? null : await gear.ui_act(@params["gear_action"], @params, ui, state);
            }
            case "diagnostic":
            {
                if (HAS_TRAIT(this, TRAIT_MECHA_DIAGNOSTIC_CREATED))
                    return FALSE;
                obj_item_mecha_diagnostic diagnostic = Dm.@new<obj_item_mecha_diagnostic>(get_turf(this));
                diagnostic.name = Dm.text("mecha holodiagnostic ([])", name);
                diagnostic.mech_data = this;
                ADD_TRAIT(this, TRAIT_MECHA_DIAGNOSTIC_CREATED, REF(this));
                break;
            }
        }
        return TRUE;
    }
}
