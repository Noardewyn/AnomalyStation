// Translated from tgstation (snapshot 1d99bd7f): code/modules/manufactorio/machines/crafter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the manufacturing assembling (and cooking) machine. It keeps what it receives on its own tile, crafts the recipe set
// with a multitool (tg's crafting component of a machine) from what lies there once per manufacturing cycle while its cable has the
// power, flicks its printing overlay for the recipe's time and sends the results forward. The examine is translated (X1). The var blocks are generated.

// tg: code/modules/manufactorio/machines/crafter.dm:1
public partial class obj_machinery_power_manufacturing_crafter : obj_machinery_power_manufacturing
{
    // tg: code/modules/manufactorio/machines/crafter.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        craftsman = Waiting.NoSleep(AddComponent(typeof(datum_component_personal_crafting_machine))) as datum_component_personal_crafting_machine;
        if (recipe is Type recipe_path)
        {
            DmList recipes = Dm.truthy(cooking) ? GLOB.cooking_recipes : GLOB.crafting_recipes;
            recipe = null;
            foreach (datum_crafting_recipe candidate in Dm.each<datum_crafting_recipe>(recipes))
            {
                if (Dm.istype(candidate, recipe_path))
                {
                    recipe = candidate;
                    break;
                }
            }
        }
        START_PROCESSING(SSmanufacturing, this);
        return dot;
    }

    // tg: code/modules/manufactorio/machines/crafter.dm:27
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        datum_crafting_recipe recipe = this.recipe as datum_crafting_recipe;
        Dm.append(ref dot, span_notice(I18n.T("It is currently manufacturing <b>[]</b>.", recipe == null ? I18n.T("nothing. Use a multitool to set it") : recipe.name)));
        if (recipe == null)
            return dot;
        Dm.append(ref dot, span_notice(I18n.T("It needs:")));
        foreach (object valid_type in Dm.each(recipe.reqs))
        {
            // Check if they're datums, specifically reagents.
            // istype(reagent_ingredient) of a typepath is false in the port (Dm.istype: a typepath is no instance), as in OpenDream
            if (Dm.istype(valid_type, typeof(datum_reagent)))
            {
                object reagent_amount = recipe.reqs[valid_type];
                Dm.append(ref dot, I18n.T("[] unit[] of []", reagent_amount, Dm.num(reagent_amount) > 1 ? "s" : "", Dm.initial(valid_type, "name")));
            }

            object amount = recipe.reqs[valid_type];

            Dm.append(ref dot, I18n.T("[] []", Dm.num(amount) > 1 ? I18n.T("[] of", amount) : I18n.T("a"), Dm.initial(valid_type, "name")));
        }
        return dot;
    }

    // tg: code/modules/manufactorio/machines/crafter.dm:47
    public override async DmTask<object> receive_resource(atom_movable receiving = null, atom from = null, object receive_dir = null)
    {
        turf machine_turf = get_turf(this);
        if (Dm.length(machine_turf.contents) >= MANUFACTURING_TURF_LAG_LIMIT)
            return MANUFACTURING_FAIL;
        await receiving.forceMove(machine_turf);
        return MANUFACTURING_SUCCESS;
    }

    // tg: code/modules/manufactorio/machines/crafter.dm:54
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        DmList available = Dm.list();
        foreach (datum_crafting_recipe potential_recipe in Dm.each_cast<datum_crafting_recipe>(craftsman.get_visible_recipes(user)))
        {
            Type as_obj = potential_recipe.result;
            if (Dm.ispath(as_obj, typeof(obj)) && !Dm.ispath(as_obj, typeof(obj_effect)) && !Dm.truthy(Dm.initial(as_obj, (obj p) => p.anchored)))
                Dm.append(available, potential_recipe);
        }
        object result = await tgui_input_list(user, "Recipe", "Select Recipe", available);
        if (result == null || result == recipe || !Dm.truthy(await user.can_perform_action(this)))
            return ITEM_INTERACT_FAILURE;
        recipe = result;
        balloon_alert(user, "set");
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/manufactorio/machines/crafter.dm:67
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        recipe = null;
        craftsman = null;
        withheld.Cut();
        return dot;
    }

    // tg: code/modules/manufactorio/machines/crafter.dm:73
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        Waiting.NoSleep(send_withheld()); // try send any pending stuff
        if (craft_timer != null)
        {
            if (Dm.num(surplus()) >= power_cost)
                add_load(); // tg's own call, with no amount: a null load adds nothing to the powernet
            else
            {
                deltimer(craft_timer);
                craft_timer = null;
                Waiting.NoSleep(say("Power failure!"));
            }
            return null;
        }
        datum_crafting_recipe the_recipe = recipe as datum_crafting_recipe;
        if (the_recipe == null || !Dm.truthy(craftsman.check_contents(this, the_recipe, craftsman.get_surroundings(this))))
            return null;
        flick_overlay_view(mutable_appearance(icon, "crafter_printing"), the_recipe.time);
        craft_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(craft), the_recipe), the_recipe.time, TIMER_STOPPABLE, file: "code/modules/manufactorio/machines/crafter.dm", line: 86);
        return null;
    }

    // tg: code/modules/manufactorio/machines/crafter.dm:88
    public virtual async DmTask<object> send_withheld()
    {
        if (!Dm.truthy(Dm.length(withheld)))
            return FALSE;
        foreach (datum_weakref weakref in Dm.each_cast<datum_weakref>(withheld.Copy()))
        {
            atom_movable resolved = weakref?.resolve() as atom_movable;
            if (resolved == null)
            {
                Dm.remove(withheld, weakref);
                continue;
            }
            if (resolved.loc != loc || Dm.truthy(await send_resource(resolved, dir)))
                Dm.remove(withheld, weakref);
        }
        return Dm.length(withheld);
    }

    // tg: code/modules/manufactorio/machines/crafter.dm:100
    public virtual async DmTask craft(datum_crafting_recipe recipe = null)
    {
        if (QDELETED(this))
            return;
        craft_timer = null;
        DmList prediff = (DmList)get_overfloor_objects();
        object result = await craftsman.construct_item(this, recipe);
        if (result is string failure)
        {
            await say(Dm.text("Crafting failed[]", failure));
            return;
        }
        if (isstack(result)) //it doesn't have hands to pick up stacks so let's try to merge them instead
        {
            obj_item_stack stack = (obj_item_stack)result;
            await stack.merge_with_loc();
        }
        DmList diff = (DmList)Dm.sub(get_overfloor_objects(), prediff);
        foreach (atom_movable diff_result in Dm.each_cast<atom_movable>(diff))
        {
            if (iseffect(diff_result) || ismob(diff_result)) // PLEASE dont stuff cats (or other mobs) into the cat grinder 9000
                continue;
            if (isitem(diff_result))
            {
                diff_result.pixel_x += rand(-4, 4);
                diff_result.pixel_y += rand(-4, 4);
            }
            Dm.append(withheld, WEAKREF(diff_result));
        }
        await send_withheld();
    }
}
