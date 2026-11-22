// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/crafting/_recipes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;


namespace Anomaly.Core.Tg;

// M5 slice 2 (New, stack/New: init_crafting_recipes' stack recipes) and slice 8 (the checks and the UI data: the crafting
// menu): the whole file. The var blocks are generated (D111).

// tg: code/datums/components/crafting/_recipes.dm:1
[DmPath("/datum/crafting_recipe")]
public partial class datum_crafting_recipe : datum
{
    // tg: code/datums/components/crafting/_recipes.dm:61
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        if (!Dm.truthy(name) && result != null)
        {
            Type atom_result = result;
            name = Dm.initial(atom_result, (atom a) => a.name);
        }
        if (result != null && blacklist_result == null && !Dm.@in(result, reqs))
            blacklist_result = ALWAYS_BLACKLIST_RESULT;
        if (Dm.truthy(tool_behaviors))
            tool_behaviors = string_list(tool_behaviors) as DmList;
        if (Dm.truthy(tool_paths))
            tool_paths = string_list(tool_paths) as DmList;
        foreach (object key in Dm.each(parts))
        {
            object part = parts[key];
            if (!Dm.truthy(part))
            {
                //ensure every single, same-type part used for the recipe will be transferred if the value is otherwise not specified
                part = INFINITY;
            }
        }
    }

    /**
     * Run custom pre-craft checks for this recipe, don't add feedback messages in this because it will spam the client
     *
     * user: The /mob that initiated the crafting
     * collected_requirements: A list of lists of /obj/item instances that satisfy reqs. Top level list is keyed by requirement path.
     */
    // tg: code/datums/components/crafting/_recipes.dm:102
    public virtual object check_requirements(mob user = null, DmList collected_requirements = null)
    {
        return TRUE;
    }

    ///Run custom pre-craft checks for this recipe for tools, rather than consumed requirements.
    // tg: code/datums/components/crafting/_recipes.dm:106
    public virtual object check_tools(atom source = null, DmList collected_tools = null, float? final_check = null)
    {
        return TRUE;
    }

    /// Additional UI data to be passed to the crafting UI for this recipe
    // tg: code/datums/components/crafting/_recipes.dm:110
    public virtual object crafting_ui_data()
    {
        return Dm.list();
    }
}

// tg: code/datums/components/crafting/_recipes.dm:76
public partial class datum_crafting_recipe_stack : datum_crafting_recipe
{
    // tg: code/datums/components/crafting/_recipes.dm:79
    [DmParams("material", "stack_recipe")]
    public override async DmTask New(params object[] __extra)
    {
        Type material = Dm.arg<Type>(__extra, 0);
        datum_stack_recipe stack_recipe = Dm.arg<datum_stack_recipe>(__extra, 1);
        if (material == null || !Dm.truthy(stack_recipe) || stack_recipe.result_type == null)
        {
            stack_trace(Dm.text("Invalid stack recipe []", stack_recipe), "code/datums/components/crafting/_recipes.dm", 81);
            return;
        }
        await base.New(__extra);

        this.name = stack_recipe.title;
        this.time = stack_recipe.time;
        this.result = stack_recipe.result_type;
        this.result_amount = stack_recipe.res_amount;
        this.reqs[material] = stack_recipe.req_amount;
        object or__1 = stack_recipe.category;
        this.category = Dm.truthy(or__1) ? or__1 : CAT_MISC;
        this.placement_checks = stack_recipe.placement_checks;
        this.crafting_flags = stack_recipe.crafting_flags;

        this.removed_mats = stack_recipe.removed_mats;
    }
}
