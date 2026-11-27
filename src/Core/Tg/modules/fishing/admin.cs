// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/admin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the admins' fishing calculator (the console's fishing-calculator, D275), which rigs a rod with the chosen hook,
// line and bait and lists each catch of a fishing spot with its weight, difficulty and count. The var block is generated (D111).

public partial class client : datum
{
    // ADMIN_VERB(fishing_calculator, R_DEBUG, "Fishing Calculator", "A calculator... for fishes?", ADMIN_CATEGORY_DEBUG): the verb's
    // body, which the admin console's fishing-calculator runs (D275; the verb's rights are the console's)
    // tg: code/modules/fishing/admin.dm:1
    public async DmTask fishing_calculator()
    {
        datum_fishing_calculator tgui = Dm.@new<datum_fishing_calculator>();
        await tgui.ui_interact(mob);
    }
}

// tg: code/modules/fishing/admin.dm:5
public partial class datum_fishing_calculator : datum
{
    // tg: code/modules/fishing/admin.dm:8
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "FishingCalculator");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/fishing/admin.dm:14
    public override object ui_state(mob user = null) => ADMIN_STATE(R_DEBUG);

    // tg: code/modules/fishing/admin.dm:17
    public override void ui_close(mob user = null)
    {
        qdel(this);
    }

    // tg: code/modules/fishing/admin.dm:20
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        object dot = await base.ui_static_data(user);
        DmList data = dot as DmList ?? Dm.list();
        data["rod_types"] = typesof(typeof(obj_item_fishing_rod));
        data["hook_types"] = typesof(typeof(obj_item_fishing_hook));
        data["line_types"] = typesof(typeof(obj_item_fishing_line));
        data["spot_types"] = subtypesof(typeof(datum_fish_source));
        return data;
    }

    // tg: code/modules/fishing/admin.dm:27
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv("info", current_table));
    }

    // tg: code/modules/fishing/admin.dm:30
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = usr as mob;
        switch (action)
        {
            case "recalc":
            {
                Type rod_type = text2path(Dm.str(@params["rod"])) as Type;
                if (rod_type == null)
                {
                    to_chat(user, span_warning(I18n.T("A fishing rod is needed in order to fish.")));
                    return null;
                }
                Type hook_type = text2path(Dm.str(@params["hook"])) as Type;
                if (hook_type == null)
                {
                    to_chat(user, span_warning(I18n.T("A fishing hook is needed in order to fish.")));
                    return null;
                }
                Type bait_type = text2path(Dm.str(@params["bait"])) as Type;
                Type line_type = text2path(Dm.str(@params["line"])) as Type;
                datum_fish_source spot = GLOB.preset_fish_sources[text2path(Dm.str(@params["spot"]))] as datum_fish_source;

                obj_item_fishing_rod temporary_rod = await Dm.new_async(rod_type) as obj_item_fishing_rod;
                qdel(temporary_rod.bait_value);
                qdel(temporary_rod.line_value);
                qdel(temporary_rod.hook_value);

                if (bait_type != null)
                    temporary_rod.set_slot(await Dm.new_async(bait_type, temporary_rod) as obj_item, ROD_SLOT_BAIT);
                temporary_rod.set_slot(await Dm.new_async(hook_type, temporary_rod) as obj_item, ROD_SLOT_HOOK);
                if (line_type != null)
                    temporary_rod.set_slot(await Dm.new_async(line_type, temporary_rod) as obj_item, ROD_SLOT_LINE);

                DmList result_table = Dm.list();
                DmList modified_table = await spot.get_modified_fish_table(temporary_rod, user, null) as DmList;
                foreach (object result_type in Dm.each<object>(spot.fish_table)) // through this not modified to display 0 chance ones too
                {
                    DmList info = Dm.list();
                    info["result"] = result_type;
                    object weight = modified_table?[result_type];
                    info["weight"] = Dm.truthy(weight) ? weight : 0;
                    info["difficulty"] = Dm.num(spot.calculate_difficulty(result_type, temporary_rod, user)) + Dm.num(Dm.initial(typeof(datum_fishing_challenge), (datum_fishing_challenge p) => p.difficulty));
                    object count = spot.fish_counts?[result_type];
                    info["count"] = Dm.truthy(count) ? count : "Infinite";
                    Dm.append(ref result_table, Dm.list(info));
                }
                current_table = result_table;

                qdel(temporary_rod);
                return TRUE;
            }
        }
        return dot;
    }
}
