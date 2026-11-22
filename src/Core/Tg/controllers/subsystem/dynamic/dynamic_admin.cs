// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/dynamic/dynamic_admin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A3 (D316): the whole file. The verb's body is dynamic_panel, which the admin console's dynamic-panel and the
// Check Antagonists page's link run (D275). ruleset_to_data takes a typepath or an instance (Appendix A). Deferred: the VV button
// (the variable editor is not ported, D275) and the verb's blackbox count. DV346: the panel lists every tg ruleset, but an inert one
// (its antagonist not ported) is neither queued nor executed.

public static partial class GlobalProcs
{
    // ADMIN_VERB(dynamic_panel, R_ADMIN, "Dynamic Panel", "Mess with dynamic.", ADMIN_CATEGORY_GAME): the verb's body, dynamic_panel(user.mob),
    // which the admin console's dynamic-panel runs (D275)
    // tg: code/controllers/subsystem/dynamic/dynamic_admin.dm:4
    public static async DmTask dynamic_panel(mob user = null)
    {
        if (!Dm.truthy(check_rights(R_ADMIN)))
            return;
        datum_dynamic_panel tgui = Dm.@new<datum_dynamic_panel>();
        await tgui.ui_interact(user);

        log_admin(Dm.text("[] opened the Dynamic Panel.", key_name(user)));
        if (!isobserver(user))
            message_admins(Dm.text("[] opened the Dynamic Panel.", key_name_admin(user)));
        // BLACKBOX_LOG_ADMIN_VERB("Dynamic Panel"): the blackbox (not translated)
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_admin.dm:15
public partial class datum_dynamic_panel : datum
{
    // tg: code/controllers/subsystem/dynamic/dynamic_admin.dm:17
    public override object ui_state(mob user = null)
    {
        return ADMIN_STATE(R_ADMIN);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_admin.dm:20
    public override void ui_close(mob user = null)
    {
        qdel(this);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_admin.dm:23
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "DynamicAdmin");
            await ui.open();
        }
        return null;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_admin.dm:29
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        if (SSdynamic.current_tier != null)
        {
            data["current_tier"] = Dm.list(
                Dm.kv("number", SSdynamic.current_tier.tier),
                Dm.kv("name", SSdynamic.current_tier.name)
            );
        }

        data["ruleset_count"] = Dm.list();
        foreach (object category in Dm.each(SSdynamic.rulesets_to_spawn))
            (data["ruleset_count"] as DmList)[category] = Dm.max(Dm.num(SSdynamic.rulesets_to_spawn[category]), 0);

        data["full_config"] = SSdynamic.get_config();
        data["config_even_enabled"] = Dm.truthy(config.Get(typeof(datum_config_entry_flag_dynamic_config_enabled))) ? Dm.length(data["full_config"]) : FALSE;

        data["queued_rulesets"] = Dm.list();
        for (int i = 1; i <= Dm.length(SSdynamic.queued_rulesets); i++)
            Dm.append_at(data, "queued_rulesets", Dm.list(Dm.add(ruleset_to_data(SSdynamic.queued_rulesets[i]), Dm.list(Dm.kv("index", i)))));

        data["active_rulesets"] = Dm.list();
        for (int i = 1; i <= Dm.length(SSdynamic.executed_rulesets); i++)
            Dm.append_at(data, "active_rulesets", Dm.list(Dm.add(ruleset_to_data(SSdynamic.executed_rulesets[i]), Dm.list(Dm.kv("index", i)))));

        data["all_rulesets"] = Dm.list();
        DmList all_rulesets = data["all_rulesets"] as DmList;
        foreach (object ruleset_type in Dm.each(subtypesof(typeof(datum_dynamic_ruleset_roundstart))))
            Dm.append_at(all_rulesets, ROUNDSTART, Dm.list(ruleset_to_data(ruleset_type)));
        foreach (object ruleset_type in Dm.each(subtypesof(typeof(datum_dynamic_ruleset_midround))))
        {
            object midround_type = Dm.initial(ruleset_type, "midround_type"); // initial(midround.midround_type)
            if (Dm.eq(midround_type, HEAVY_MIDROUND))
                Dm.append_at(all_rulesets, HEAVY_MIDROUND, Dm.list(ruleset_to_data(ruleset_type)));
            else if (Dm.eq(midround_type, LIGHT_MIDROUND))
                Dm.append_at(all_rulesets, LIGHT_MIDROUND, Dm.list(ruleset_to_data(ruleset_type)));
        }
        foreach (object ruleset_type in Dm.each(subtypesof(typeof(datum_dynamic_ruleset_latejoin))))
            Dm.append_at(all_rulesets, LATEJOIN, Dm.list(ruleset_to_data(ruleset_type)));

        // COOLDOWN_TIMELEFT(SSdynamic, ...) (__DEFINES/cooldowns.dm:119)
        data["time_until_lights"] = Dm.max(0, SSdynamic.light_ruleset_start - world.time);
        data["time_until_heavies"] = Dm.max(0, SSdynamic.heavy_ruleset_start - world.time);
        data["time_until_latejoins"] = Dm.max(0, SSdynamic.latejoin_ruleset_start - world.time);

        data["time_until_next_midround"] = Dm.max(0, SSdynamic.midround_cooldown - world.time);
        data["time_until_next_latejoin"] = Dm.max(0, SSdynamic.latejoin_cooldown - world.time);
        data["failed_latejoins"] = SSdynamic.failed_latejoins;

        data["light_midround_chance"] = SSdynamic.get_midround_chance(LIGHT_MIDROUND);
        data["heavy_midround_chance"] = SSdynamic.get_midround_chance(HEAVY_MIDROUND);
        data["latejoin_chance"] = SSdynamic.get_latejoin_chance();

        data["roundstarted"] = SSticker.HasRoundStarted();

        data["light_chance_maxxed"] = SSdynamic.admin_forcing_next_light;
        data["heavy_chance_maxxed"] = SSdynamic.admin_forcing_next_heavy;
        data["latejoin_chance_maxxed"] = SSdynamic.admin_forcing_next_latejoin;

        data["next_dynamic_tick"] = Dm.truthy(SSdynamic.next_fire) ? SSdynamic.next_fire - world.time : SSticker.GetTimeLeft();

        data["antag_events_enabled"] = SSdynamic.antag_events_enabled;

        return data;
    }

    /// Pass a ruleset typepath or a ruleset instance
    // tg: code/controllers/subsystem/dynamic/dynamic_admin.dm:91
    public virtual object ruleset_to_data(object ruleset = null)
    {
        DmList data = Dm.list();
        object ruleset_path = isdatum(ruleset) ? (ruleset as datum).type : ruleset;
        data["name"] = Dm.initial(ruleset, "name");
        data["id"] = Dm.initial(ruleset, "config_tag");
        data["typepath"] = ruleset_path;
        data["selected_players"] = Dm.list();
        data["admin_disabled"] = Dm.num(Dm.@in(ruleset_path, SSdynamic.admin_disabled_rulesets));
        if (isdatum(ruleset))
        {
            foreach (datum_mind player in Dm.each_cast<datum_mind>((ruleset as datum_dynamic_ruleset).selected_minds))
            {
                Dm.append_at(data, "selected_players", Dm.list(Dm.list(
                    Dm.kv("key", player.key)
                )));
                data["hidden"] = Dm.num(Dm.@in(ruleset, SSdynamic.unreported_rulesets));
            }
        }
        return data;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_admin.dm:107
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (Dm.str(action))
        {
            case "remove_queued_ruleset":
            {
                object index = @params["ruleset_index"];
                if (Dm.lt(Dm.length(SSdynamic.queued_rulesets), index))
                    return dot;
                datum_dynamic_ruleset ruleset = SSdynamic.queued_rulesets[index] as datum_dynamic_ruleset;
                if (ruleset == null)
                    return dot;
                SSdynamic.unqueue_ruleset(ruleset);
                message_admins(Dm.text("[] removed [] from the dynamic ruleset queue.", key_name_admin(ui.user), ruleset.config_tag));
                log_admin(Dm.text("[] removed [] from the dynamic ruleset queue.", key_name_admin(ui.user), ruleset.config_tag));
                qdel(ruleset);
                return TRUE;
            }
            case "add_queued_ruleset":
            {
                Type ruleset_path = Dm.text2path(Dm.str(@params["ruleset_type"]));
                if (ruleset_path == null)
                    return dot;
                // DEVIATION(DV346): an inert ruleset (its antagonist is not ported) is not queued
                if (Attribute.IsDefined(ruleset_path, typeof(DmInertAttribute), inherit: false))
                    return dot;
                await SSdynamic.queue_ruleset(ruleset_path);
                message_admins(Dm.text("[] added [] to the dynamic ruleset queue.", key_name_admin(ui.user), Dm.initial(ruleset_path, "config_tag")));
                log_admin(Dm.text("[] added [] to the dynamic ruleset queue.", key_name_admin(ui.user), Dm.initial(ruleset_path, "config_tag")));
                return TRUE;
            }
            case "dynamic_vv":
                // ui.user?.client?.debug_variables(SSdynamic) (:137): the variable editor is not ported (D275)
                Dm.Deferred("code/controllers/subsystem/dynamic/dynamic_admin.dm:137", "ui_act dynamic_vv: the variable editor (debug_variables, D275)");
                return TRUE;
            case "add_ruleset_category_count":
            {
                object category = @params["ruleset_category"];
                if (!Dm.truthy(category))
                    return dot;
                Dm.append_at(SSdynamic.rulesets_to_spawn, category, 1);
                message_admins(Dm.text("[] added 1 to the [] ruleset category.", key_name_admin(ui.user), category));
                log_admin(Dm.text("[] added 1 to the [] ruleset category.", key_name_admin(ui.user), category));
                return TRUE;
            }
            case "set_ruleset_category_count":
            {
                object category = @params["ruleset_category"];
                object count = @params["ruleset_count"];
                if (!Dm.truthy(category) || !Dm.isnum(count))
                    return dot;
                SSdynamic.rulesets_to_spawn[category] = count;
                message_admins(Dm.text("[] set the [] ruleset category to [].", key_name_admin(ui.user), category, count));
                log_admin(Dm.text("[] set the [] ruleset category to [].", key_name_admin(ui.user), category, count));
                return TRUE;
            }
            case "execute_ruleset":
            {
                Type ruleset_path = Dm.text2path(Dm.str(@params["ruleset_type"]));
                if (ruleset_path == null)
                    return dot;
                // DEVIATION(DV346): an inert ruleset (its antagonist is not ported) is not executed
                if (Attribute.IsDefined(ruleset_path, typeof(DmInertAttribute), inherit: false))
                    return dot;
                message_admins(Dm.text("[] executed the ruleset [].", key_name_admin(ui.user), Dm.initial(ruleset_path, "config_tag")));
                log_admin(Dm.text("[] executed the ruleset [].", key_name_admin(ui.user), Dm.initial(ruleset_path, "config_tag")));
                // ASYNC
                Waiting.Detach(SSdynamic.force_run_midround(ruleset_path, alert_admins_on_fail: TRUE, admin: ui.user));
                return TRUE;
            }
            case "disable_ruleset":
            {
                Type ruleset_path = Dm.text2path(Dm.str(@params["ruleset_type"]));
                if (ruleset_path == null)
                    return dot;
                if (Dm.@in(ruleset_path, SSdynamic.admin_disabled_rulesets))
                {
                    Dm.remove(ref SSdynamic.admin_disabled_rulesets, ruleset_path);
                    message_admins(Dm.text("[] enabled [] to be selected.", key_name_admin(ui.user), ruleset_path));
                    log_admin(Dm.text("[] enabled [] to be selected.", key_name_admin(ui.user), ruleset_path));
                }
                else
                {
                    Dm.append(ref SSdynamic.admin_disabled_rulesets, ruleset_path);
                    message_admins(Dm.text("[] disabled [] from being selected.", key_name_admin(ui.user), ruleset_path));
                    log_admin(Dm.text("[] disabled [] from being selected.", key_name_admin(ui.user), ruleset_path));
                }
                return TRUE;
            }
            case "disable_all":
                Dm.combine(ref SSdynamic.admin_disabled_rulesets, subtypesof(typeof(datum_dynamic_ruleset)));
                message_admins(Dm.text("[] disabled all rulesets from being selected.", key_name_admin(ui.user)));
                log_admin(Dm.text("[] disabled all rulesets from being selected.", key_name_admin(ui.user)));
                break;
            case "enable_all":
                SSdynamic.admin_disabled_rulesets.Cut();
                message_admins(Dm.text("[] re-enabled all rulesets.", key_name_admin(ui.user)));
                log_admin(Dm.text("[] re_enabled all rulesets.", key_name_admin(ui.user)));
                break;
            case "set_tier":
            {
                if (SSdynamic.current_tier != null && Dm.truthy(SSticker.HasRoundStarted()))
                    return TRUE;
                DmList tiers = Dm.list();
                foreach (Type tier in Dm.each_cast<Type>(subtypesof(typeof(datum_dynamic_tier))))
                    tiers[Dm.initial(tier, "config_tag")] = tier; // tier::config_tag
                object picked = await tgui_input_list(ui.user, "Pick a dynamic tier before the game starts", "Pick tier", tiers, ui_state: ADMIN_STATE(R_ADMIN));
                if (Dm.truthy(picked) && !Dm.truthy(SSticker.HasRoundStarted()))
                {
                    SSdynamic.set_tier(tiers[picked]);
                    message_admins(Dm.text("[] set the dynamic tier to [].", key_name_admin(ui.user), picked));
                    log_admin(Dm.text("[] set the dynamic tier to [].", key_name_admin(ui.user), picked));
                }
                return TRUE;
            }
            case "max_light_chance":
                SSdynamic.admin_forcing_next_light = Dm.num(!Dm.truthy(SSdynamic.admin_forcing_next_light));
                message_admins(Dm.text("[] [] the next light ruleset chance.", key_name_admin(ui.user), Dm.truthy(SSdynamic.admin_forcing_next_light) ? "forced" : "reset"));
                log_admin(Dm.text("[] [] the next light ruleset chance.", key_name_admin(ui.user), Dm.truthy(SSdynamic.admin_forcing_next_light) ? "forced" : "reset"));
                return TRUE;
            case "max_heavy_chance":
                SSdynamic.admin_forcing_next_heavy = Dm.num(!Dm.truthy(SSdynamic.admin_forcing_next_heavy));
                message_admins(Dm.text("[] [] the next heavy ruleset chance.", key_name_admin(ui.user), Dm.truthy(SSdynamic.admin_forcing_next_heavy) ? "forced" : "reset"));
                log_admin(Dm.text("[] [] the next heavy ruleset chance.", key_name_admin(ui.user), Dm.truthy(SSdynamic.admin_forcing_next_heavy) ? "forced" : "reset"));
                return TRUE;
            case "max_latejoin_chance":
                SSdynamic.admin_forcing_next_latejoin = Dm.num(!Dm.truthy(SSdynamic.admin_forcing_next_latejoin));
                message_admins(Dm.text("[] [] the next latejoin ruleset chance.", key_name_admin(ui.user), Dm.truthy(SSdynamic.admin_forcing_next_latejoin) ? "forced" : "reset"));
                log_admin(Dm.text("[] [] the next latejoin ruleset chance.", key_name_admin(ui.user), Dm.truthy(SSdynamic.admin_forcing_next_latejoin) ? "forced" : "reset"));
                return TRUE;
            case "light_start_now":
                SSdynamic.light_ruleset_start = 0; // COOLDOWN_RESET(SSdynamic, light_ruleset_start)
                message_admins(Dm.text("[] reset the light ruleset start cooldown.", key_name_admin(ui.user)));
                log_admin(Dm.text("[] reset the light ruleset start cooldown.", key_name_admin(ui.user)));
                return TRUE;
            case "heavy_start_now":
                SSdynamic.heavy_ruleset_start = 0; // COOLDOWN_RESET(SSdynamic, heavy_ruleset_start)
                message_admins(Dm.text("[] reset the heavy ruleset start cooldown.", key_name_admin(ui.user)));
                log_admin(Dm.text("[] reset the heavy ruleset start cooldown.", key_name_admin(ui.user)));
                return TRUE;
            case "latejoin_start_now":
                SSdynamic.latejoin_ruleset_start = 0; // COOLDOWN_RESET(SSdynamic, latejoin_ruleset_start)
                message_admins(Dm.text("[] reset the latejoin ruleset start cooldown.", key_name_admin(ui.user)));
                log_admin(Dm.text("[] reset the latejoin ruleset start cooldown.", key_name_admin(ui.user)));
                return TRUE;
            case "reset_midround_cooldown":
                SSdynamic.midround_cooldown = 0; // COOLDOWN_RESET(SSdynamic, midround_cooldown)
                message_admins(Dm.text("[] reset the midround cooldown.", key_name_admin(ui.user)));
                log_admin(Dm.text("[] reset the midround cooldown.", key_name_admin(ui.user)));
                return TRUE;
            case "reset_latejoin_cooldown":
                SSdynamic.latejoin_cooldown = 0; // COOLDOWN_RESET(SSdynamic, latejoin_cooldown)
                message_admins(Dm.text("[] reset the latejoin cooldown.", key_name_admin(ui.user)));
                log_admin(Dm.text("[] reset the latejoin cooldown.", key_name_admin(ui.user)));
                return TRUE;
            case "hide_ruleset":
            {
                object index = @params["ruleset_index"];
                if (Dm.lt(Dm.length(SSdynamic.executed_rulesets), index))
                    return dot;
                datum_dynamic_ruleset ruleset = SSdynamic.executed_rulesets[index] as datum_dynamic_ruleset;
                if (ruleset == null)
                    return dot;
                if (Dm.@in(ruleset, SSdynamic.unreported_rulesets))
                {
                    Dm.remove(ref SSdynamic.unreported_rulesets, ruleset);
                    message_admins(Dm.text("[] hid [] from the roundend report.", key_name_admin(ui.user), ruleset));
                    log_admin(Dm.text("[] hid [] from the roundend report.", key_name_admin(ui.user), ruleset));
                }
                else
                {
                    Dm.append(ref SSdynamic.unreported_rulesets, ruleset);
                    message_admins(Dm.text("[] unhid [] from the roundend report.", key_name_admin(ui.user), ruleset));
                    log_admin(Dm.text("[] unhid [] from the roundend report.", key_name_admin(ui.user), ruleset));
                }
                return TRUE;
            }
            case "toggle_antag_events":
                SSdynamic.antag_events_enabled = Dm.num(!Dm.truthy(SSdynamic.antag_events_enabled));
                message_admins(Dm.text("[] [] antag events.", key_name_admin(ui.user), Dm.truthy(SSdynamic.antag_events_enabled) ? "enabled" : "disabled"));
                log_admin(Dm.text("[] [] antag events.", key_name_admin(ui.user), Dm.truthy(SSdynamic.antag_events_enabled) ? "enabled" : "disabled"));
                return TRUE;
        }
        return dot;
    }
}
