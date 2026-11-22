// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/crafting/tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 8 (crafting): the file's only proc. The recipes themselves are var blocks (generated, D111); the static
// valid_types lives in TgState (rules 10.6). Crayons are not translated (generated-only types, M9): use_charges and
// check_empty are not declared, so the shuttle blueprints never pass their tool check (a NotTranslated leaf).

// tg: code/datums/components/crafting/tools.dm:83
public partial class datum_crafting_recipe_shuttle_blueprints : datum_crafting_recipe
{
    // tg: code/datums/components/crafting/tools.dm:98  var/static/list/valid_types (a type-level static, rules 10.6)
    private static ref DmList valid_types => ref TgState.Current.datum_crafting_recipe_shuttle_blueprints__valid_types;

    // tg: code/datums/components/crafting/tools.dm:104
    public override object check_tools(atom user = null, DmList collected_tools = null, float? final_check = null)
    {
        final_check ??= FALSE;
        foreach (obj_item_toy_crayon crayon in Dm.each<obj_item_toy_crayon>(collected_tools))
        {
            if (!Dm.truthy(is_type_in_typecache(crayon, valid_types)))
                continue;
            // if(final_check ? crayon.use_charges(user, 10) : !crayon.check_empty(user, 10)) return TRUE
            Dm.NotTranslated("code/datums/components/crafting/tools.dm:108", "shuttle_blueprints check_tools: the crayon's use_charges and check_empty (crayons, M10+)");
        }
        return FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/crafting/tools.dm:98
    internal DmList datum_crafting_recipe_shuttle_blueprints__valid_types = typecacheof(Dm.list(
        typeof(obj_item_toy_crayon_blue),
        typeof(obj_item_toy_crayon_rainbow),
        typeof(obj_item_toy_crayon_spraycan)
    )) as DmList;
}
