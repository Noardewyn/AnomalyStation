// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/pancakes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the pancakes from the griddle, their stacks and the raw pancake's toppings. The var blocks
// are generated (D111).

// tg: code/modules/food_and_drinks/food/pancakes.dm:3
public partial class obj_item_food_pancakes : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/pancakes.dm:1  #define PANCAKE_MAX_STACK 10 (file-local, #undef at :171)
    private const float PANCAKE_MAX_STACK = 10;

    // tg: code/modules/food_and_drinks/food/pancakes.dm:87
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/pancakes.dm:91
    public override object update_name(float? updates = null)
    {
        name = Dm.truthy(contents.len) ? I18n.T("stack of pancakes") : Dm.initial(this, (atom a) => a.name);
        return base.update_name(updates);
    }

    // tg: code/modules/food_and_drinks/food/pancakes.dm:95
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        if (!Dm.truthy(Dm.bitand(updates, UPDATE_OVERLAYS)))
            return await base.update_icon(updates, __extra);

        updates = Dm.bitand(updates.Value, Dm.bitnot(UPDATE_OVERLAYS));
        object dot = await base.update_icon(updates, __extra); // Don't update overlays. We're doing that here

        if (contents.len < LAZYLEN(overlays))
            Dm.remove(overlays, overlays[overlays.len]);
        Dm.combine(ref dot, UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/pancakes.dm:106
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        string ingredients_listed = "";
        float pancakeCount = contents.len;
        float sw__1 = pancakeCount;
        if (sw__1 == 0)
            desc = Dm.initial(this, (atom a) => a.desc);
        else if (Dm.in_to(sw__1, 1, 2))
            desc = I18n.T("A stack of fluffy pancakes.");
        else if (Dm.in_to(sw__1, 3, 6))
            desc = I18n.T("A fat stack of fluffy pancakes!");
        else if (Dm.in_to(sw__1, 7, 9))
            desc = I18n.T("A grand tower of fluffy, delicious pancakes!");
        else if (Dm.in_to(sw__1, PANCAKE_MAX_STACK, INFINITY))
            desc = I18n.T("A massive towering spire of fluffy, delicious pancakes. It looks like it could tumble over!");
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(pancakeCount))
        {
            foreach (obj_item_food_pancakes ING in Dm.each<obj_item_food_pancakes>(contents))
                ingredients_listed += Dm.text("[], ", ING.name);
            Dm.append(ref dot, I18n.T("It contains []on top of a [].", Dm.truthy(contents.len) ? Dm.text("[]", ingredients_listed) : I18n.T("no ingredient, "), Dm.initial(this, (atom a) => a.name)));
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/pancakes.dm:126
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_food_pancakes)
        {
            obj_item_food_pancakes pancake = (obj_item_food_pancakes)tool;
            if ((contents.len >= PANCAKE_MAX_STACK) || ((pancake.contents.len + contents.len) > PANCAKE_MAX_STACK))
            {
                to_chat(user, span_warning(I18n.T("You can't add that many pancakes to []!", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(pancake, this)))
                return ITEM_INTERACT_BLOCKING;

            to_chat(user, span_notice(I18n.T("You add the [] to the [].", pancake, this)));
            pancake.name = Dm.initial(pancake, (atom a) => a.name);
            Dm.append(contents, pancake);
            update_snack_overlays(pancake);
            if (Dm.truthy(pancake.contents.len))
            {
                foreach (object pancake_content in Dm.each(pancake.contents))
                {
                    pancake = pancake_content as obj_item_food_pancakes;
                    pancake.name = Dm.initial(pancake, (atom a) => a.name);
                    Dm.append(contents, pancake);
                    update_snack_overlays(pancake);
                }
            }
            pancake = (obj_item_food_pancakes)tool;
            pancake.contents.Cut();
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(contents.len))
        {
            obj pancake__1 = contents[contents.len] as obj;
            return await pancake__1.item_interaction(user, tool, modifiers);
        }

        return NONE;
    }

    // tg: code/modules/food_and_drinks/food/pancakes.dm:156
    public virtual void update_snack_overlays(obj_item_food_pancakes pancake = null)
    {
        mutable_appearance pancake_visual = mutable_appearance(icon, Dm.text("[]_[]", pancake.stack_name, rand(1, 3))) as mutable_appearance;
        pancake_visual.pixel_w = rand(-1, 1);
        pancake_visual.pixel_z = 3 * contents.len - 1;
        pancake_visual.layer = layer + (contents.len * 0.01f);
        add_overlay(pancake_visual);
        update_appearance();
    }

    // tg: code/modules/food_and_drinks/food/pancakes.dm:164
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList @params = null, object stacked = null, params object[] __extra)
    {
        stacked ??= TRUE;
        if (Dm.truthy(user.combat_mode) || !Dm.truthy(contents.len) || !Dm.truthy(stacked))
            return await base.attack__proc(target, user, @params, stacked, __extra);
        obj_item item = contents[contents.len] as obj_item;
        object dot = await item.attack__proc(target, user, @params, FALSE);
        update_appearance();
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/pancakes.dm:17
public partial class obj_item_food_pancakes_raw : obj_item_food_pancakes
{
    // tg: code/modules/food_and_drinks/food/pancakes.dm:28
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable),
            Dm.named("cook_result", typeof(obj_item_food_pancakes)),
            Dm.named("required_cook_time", rand(30 * SECONDS, 40 * SECONDS)),
            Dm.named("positive_result", TRUE),
            Dm.named("use_large_steam_sprite", TRUE));
    }

    // tg: code/modules/food_and_drinks/food/pancakes.dm:35
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.eq(name, Dm.initial(this, (atom a) => a.name)))// we already have additives
            return await base.item_interaction(user, tool, modifiers, __extra);

        Type newresult = null;
        if (tool is obj_item_food_grown_berries)
        {
            newresult = typeof(obj_item_food_pancakes_blueberry);
            name = I18n.T("raw blueberry pancake");
            icon_state = "rawbbpancakes_1";
            stack_name = "rawbbpancakes";
        }
        else if (tool is obj_item_food_chocolatebar)
        {
            newresult = typeof(obj_item_food_pancakes_chocolatechip);
            name = I18n.T("raw chocolate chip pancake");
            icon_state = "rawccpancakes_1";
            stack_name = "rawccpancakes";
        }
        if (newresult == null)
            return await base.item_interaction(user, tool, modifiers, __extra);

        qdel(tool);
        to_chat(user, span_notice(I18n.T("You add [] to [].", tool, this)));
        await AddComponent(typeof(datum_component_grillable), Dm.named("cook_result", newresult));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/food/pancakes.dm:58
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.eq(name, Dm.initial(this, (atom a) => a.name)))
            Dm.append(ref dot, span_notice(I18n.T("You can modify the pancake by adding <b>blueberries</b> or <b>chocolate</b> before finishing the griddle.")));
        return dot;
    }
}
