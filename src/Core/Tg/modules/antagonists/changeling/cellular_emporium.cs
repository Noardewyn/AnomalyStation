// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/cellular_emporium.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the cellular emporium (the CellularEmporium window's server side: the abilities on sale, the
// owned ones, the genetic points, readapting and evolving) and its action. The var blocks are generated (D111); ui_static_data's
// proc-level static list of abilities is a TgState field (rules 10.6).

// Cellular Emporium -
// The place where Changelings go to purchase biological weaponry.
// tg: code/modules/antagonists/changeling/cellular_emporium.dm:3
public partial class datum_cellular_emporium : datum
{
    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:9
    [DmParams("my_changeling")]
    public override async DmTask New(params object[] __extra)
    {
        object my_changeling = Dm.arg(__extra, 0);
        await base.New(__extra);
        changeling = my_changeling as datum_antagonist_changeling;
    }

    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:13
    public override object Destroy(float? force = null)
    {
        changeling = null;
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:17
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:20
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        await DmTask.CompletedTask;
        if (changeling == null)
            return UI_CLOSE;
        return UI_INTERACTIVE;
    }

    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:25
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "CellularEmporium", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:31  ui_static_data's var/static/list/abilities
    private static DmList ui_static_data__abilities
    {
        get => TgState.Current.datum_cellular_emporium__ui_static_data__abilities;
        set => TgState.Current.datum_cellular_emporium__ui_static_data__abilities = value;
    }

    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:31
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        if (ui_static_data__abilities == null)
        {
            ui_static_data__abilities = Dm.list();
            foreach (Type ability_path in Dm.each_cast<Type>(datum_antagonist_changeling.all_powers))
            {
                float dna_cost = Dm.initial(ability_path, (datum_action_changeling p) => p.dna_cost);
                if (dna_cost < 0) // 0 = free, but negatives are invalid
                    continue;

                DmList ability_data = Dm.list(
                    Dm.kv("name", Dm.initial(ability_path, (datum_action_changeling p) => p.name)),
                    Dm.kv("desc", Dm.initial(ability_path, (datum_action_changeling p) => p.desc)),
                    Dm.kv("icon", Dm.initial(ability_path, (datum_action_changeling p) => p.button_icon_state)),
                    Dm.kv("helptext", Dm.initial(ability_path, (datum_action_changeling p) => p.helptext)),
                    Dm.kv("path", ability_path),
                    Dm.kv("genetic_point_required", dna_cost),
                    Dm.kv("absorbs_required", Dm.initial(ability_path, (datum_action_changeling p) => p.req_absorbs)),
                    Dm.kv("dna_required", Dm.initial(ability_path, (datum_action_changeling p) => p.req_dna)),
                    Dm.kv("category", Dm.initial(ability_path, (datum_action_changeling p) => p.category)));

                DmList abilities__it = ui_static_data__abilities;
                Dm.append(ref abilities__it, Dm.list(ability_data));
            }

            sortTim(ui_static_data__abilities, "/proc/" + nameof(GlobalProcs.cmp_assoc_list_name));
        }

        data["abilities"] = ui_static_data__abilities;
        return data;
    }

    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:59
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        data["can_readapt"] = changeling.can_respec;
        data["owned_abilities"] = assoc_to_keys(changeling.purchased_powers);
        data["genetic_points_count"] = changeling.genetic_points;
        data["absorb_count"] = changeling.true_absorbs;
        data["dna_count"] = changeling.absorbed_count;

        return data;
    }

    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:70
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = null;
        dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        string sw__1 = action as string;
        switch (sw__1)
        {
            case "readapt":
                if (Dm.truthy(changeling.can_respec))
                    changeling.readapt();
                break;

            case "evolve":
                // purchase_power sanity checks stuff like typepath, DNA, and absorbs for us.
                await changeling.purchase_power(text2path(@params["path"] as string));
                break;
        }

        return TRUE;
    }
}

// tg: code/modules/antagonists/changeling/cellular_emporium.dm:88
public partial class datum_action_cellular_emporium : datum_action
{
    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:96
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        object Target = Dm.arg(__extra, 0);
        await base.New(__extra);
        if (Target is not datum_cellular_emporium)
        {
            stack_trace("cellular_emporium action created with non-emporium.", "code/modules/antagonists/changeling/cellular_emporium.dm", 99);
            qdel(this);
        }
    }

    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:102
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        await target.ui_interact(owner);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/antagonists/changeling/cellular_emporium.dm:34  ui_static_data's var/static/list/abilities
    internal DmList datum_cellular_emporium__ui_static_data__abilities;
}
