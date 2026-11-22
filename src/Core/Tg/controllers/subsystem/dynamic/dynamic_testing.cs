// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/dynamic/dynamic_testing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A3 (D316): the whole file. The verb's body is open_dynamic_tester, which the admin console's dynamic-tester runs
// (D275); its blackbox count is not translated. The tester weighs every tg ruleset, the inert ones at 0 (DV346).

public static partial class GlobalProcs
{
    /// Verb to open the create command report window and send command reports.
    // ADMIN_VERB(dynamic_tester, R_DEBUG, "Dynamic Tester", "See dynamic probabilities.", ADMIN_CATEGORY_DEBUG): the verb's body, which
    // the admin console's dynamic-tester runs (D275)
    // tg: code/controllers/subsystem/dynamic/dynamic_testing.dm:2
    public static async DmTask open_dynamic_tester(mob user = null)
    {
        // BLACKBOX_LOG_ADMIN_VERB("Dynamic Tester"): the blackbox (not translated)
        datum_dynamic_tester tgui = await Dm.new_async<datum_dynamic_tester>();
        await tgui.ui_interact(user);
    }
}

// tg: code/controllers/subsystem/dynamic/dynamic_testing.dm:7
public partial class datum_dynamic_tester : datum
{
    // tg: code/controllers/subsystem/dynamic/dynamic_testing.dm:26
    public override async DmTask New(params object[] __extra)
    {
        foreach (Type rtype in Dm.each_cast<Type>(subtypesof(typeof(datum_dynamic_ruleset_roundstart))))
        {
            if (!Dm.truthy(Dm.initial(rtype, "config_tag")))
                continue;
            datum_dynamic_ruleset_roundstart created = Dm.@new(rtype, SSdynamic.get_config()) as datum_dynamic_ruleset_roundstart;
            Dm.append(ref roundstart_rulesets, created);
            // snowflake so we can see headrev stats
            if (created is datum_dynamic_ruleset_roundstart_revolution revs)
                revs.heads_necessary = 0;
        }

        foreach (Type rtype in Dm.each_cast<Type>(subtypesof(typeof(datum_dynamic_ruleset_midround))))
        {
            if (!Dm.truthy(Dm.initial(rtype, "config_tag")))
                continue;
            datum_dynamic_ruleset_midround created = Dm.@new(rtype, SSdynamic.get_config()) as datum_dynamic_ruleset_midround;
            Dm.append(ref midround_rulesets, created);
        }

        await update_reports();
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_testing.dm:45
    public override object ui_state(mob user = null)
    {
        return ADMIN_STATE(R_DEBUG);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_testing.dm:48
    public override void ui_close(mob user = null)
    {
        qdel(this);
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_testing.dm:51
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "DynamicTester");
            await ui.open();
        }
        return null;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_testing.dm:57
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["tier"] = tier;
        data["num_players"] = num_players;
        data["roundstart_ruleset_report"] = assoc_to_values(roundstart_ruleset_report);
        data["light_midround_ruleset_report"] = assoc_to_values(light_midround_ruleset_report);
        data["heavy_midround_ruleset_report"] = assoc_to_values(heavy_midround_ruleset_report);

        return data;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_testing.dm:68
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "set_num_players":
            {
                float? old_num = num_players;
                num_players = Dm.max(Dm.num(Dm.text2num(@params["num_players"])), 1);
                if (old_num != num_players)
                    await update_reports();
                return TRUE;
            }

            case "set_tier":
            {
                float? old_tier = tier;
                tier = Dm.max(Dm.num(Dm.text2num(@params["tier"])), 1);
                if (old_tier != tier)
                    await update_reports();
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/controllers/subsystem/dynamic/dynamic_testing.dm:88
    public virtual async DmTask update_reports()
    {
        roundstart_ruleset_report.Cut();
        foreach (datum_dynamic_ruleset_roundstart ruleset in Dm.each_cast<datum_dynamic_ruleset_roundstart>(roundstart_rulesets))
        {
            string comment = "";
            if (ruleset is datum_dynamic_ruleset_roundstart_revolution revs)
                comment = Dm.text(" (Assuming [] heads of staff)", Dm.initial(revs, "heads_necessary"));

            roundstart_ruleset_report[ruleset] = Dm.list(
                Dm.kv("name", ruleset.name),
                Dm.kv("weight", ruleset.get_weight(num_players, tier)),
                Dm.kv("max_candidates", ruleset.get_antag_cap(num_players, Dm.truthy(ruleset.max_antag_cap) ? ruleset.max_antag_cap : ruleset.min_antag_cap)),
                Dm.kv("min_candidates", ruleset.get_antag_cap(num_players, ruleset.min_antag_cap)),
                Dm.kv("comment", comment)
            );
        }

        light_midround_ruleset_report.Cut();
        heavy_midround_ruleset_report.Cut();
        foreach (datum_dynamic_ruleset_midround ruleset in Dm.each_cast<datum_dynamic_ruleset_midround>(midround_rulesets))
        {
            DmList list_to_add = Dm.eq(ruleset.midround_type, LIGHT_MIDROUND) ? light_midround_ruleset_report : heavy_midround_ruleset_report;
            list_to_add[ruleset] = Dm.list(
                Dm.kv("name", ruleset.name),
                Dm.kv("weight", ruleset.get_weight(num_players, tier)),
                Dm.kv("max_candidates", ruleset.get_antag_cap(num_players, Dm.truthy(ruleset.max_antag_cap) ? ruleset.max_antag_cap : ruleset.min_antag_cap)),
                Dm.kv("min_candidates", ruleset.get_antag_cap(num_players, ruleset.min_antag_cap))
            );
        }

        await update_static_data_for_all_viewers();
    }
}
