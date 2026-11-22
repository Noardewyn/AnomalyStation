// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/robot/items/food.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the treat fabricator (lollipops, gumballs, ice cream) with its candy projectiles, and the
// service cyborg's cookbook. The var blocks are generated (D111).

// tg: code/game/objects/items/robot/items/food.dm:6
public partial class obj_item_borg_lollipop : obj_item_borg
{
    internal const int DISPENSE_LOLLIPOP_MODE = 1;
    internal const int THROW_LOLLIPOP_MODE = 2;
    internal const int THROW_GUMBALL_MODE = 3;
    internal const int DISPENSE_ICECREAM_MODE = 4;

    // tg: code/game/objects/items/robot/items/food.dm:25
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        check_amount();
        await base.equipped__proc(user, slot, initial);
    }

    // tg: code/game/objects/items/robot/items/food.dm:29
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        check_amount();
        return await base.dropped(user, silent, __extra);
    }

    ///Queues another lollipop to be fabricated if there is enough room for one
    // tg: code/game/objects/items/robot/items/food.dm:34
    public virtual void check_amount()
    {
        if (!Dm.truthy(charging) && candy < candymax)
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(charge_lollipops)), charge_delay, file: "code/game/objects/items/robot/items/food.dm", line: 36);
            charging = TRUE;
        }
    }

    ///Increases the amount of lollipops
    // tg: code/game/objects/items/robot/items/food.dm:40
    public virtual void charge_lollipops()
    {
        candy++;
        charging = FALSE;
        check_amount();
    }

    ///Dispenses a lollipop
    // tg: code/game/objects/items/robot/items/food.dm:46
    public virtual async DmTask<object> dispense(atom atom_dispensed_to = null, mob user = null)
    {
        if (candy <= 0)
        {
            to_chat(user, span_warning(I18n.T("No treats left in storage!")));
            return FALSE;
        }
        turf turf_to_dispense_to = get_turf(atom_dispensed_to);
        if (turf_to_dispense_to == null || !isopenturf(turf_to_dispense_to))
            return FALSE;
        if (atom_dispensed_to is obj obj_dispensed_to)
        {
            if (Dm.truthy(obj_dispensed_to.density))
                return FALSE;
        }

        obj_item food_item = null;
        switch ((int)mode)
        {
            case DISPENSE_LOLLIPOP_MODE:
                food_item = Dm.@new<obj_item_food_lollipop_cyborg>(turf_to_dispense_to);
                break;
            case DISPENSE_ICECREAM_MODE:
                food_item = Dm.@new<obj_item_food_icecream>(turf_to_dispense_to, Dm.list(ICE_CREAM_VANILLA));
                food_item.desc = I18n.T("Eat the ice cream.");
                break;
        }

        object into_hands = FALSE;
        if (atom_dispensed_to is mob mob_dispensed_to)
            into_hands = await mob_dispensed_to.put_in_hands(food_item);

        candy--;
        check_amount();

        if (Dm.truthy(into_hands))
            user.visible_message(span_notice(I18n.T("[] dispenses a treat into the hands of [].", user, atom_dispensed_to)), span_notice(I18n.T("You dispense a treat into the hands of [].", atom_dispensed_to)), span_hear(I18n.T("You hear a click.")));
        else
            user.visible_message(span_notice(I18n.T("[] dispenses a treat.", user)), span_notice(I18n.T("You dispense a treat.")), span_hear(I18n.T("You hear a click.")));

        playsound(loc, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        return TRUE;
    }

    /// Shoot a lollipop
    // tg: code/game/objects/items/robot/items/food.dm:83
    public virtual async DmTask<object> shootL(atom target = null, mob_living user = null, object @params = null)
    {
        if (candy <= 0)
        {
            to_chat(user, span_warning(I18n.T("Not enough lollipops left!")));
            return FALSE;
        }
        candy--;

        obj_item_ammo_casing_lollipop lollipop;
        if (user is mob_living_silicon_robot robot_user && Dm.truthy(robot_user.emagged))
            lollipop = Dm.@new<obj_item_ammo_casing_lollipop_harmful>(this);
        else
            lollipop = Dm.@new<obj_item_ammo_casing_lollipop>(this);

        playsound(loc, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        await lollipop.fire_casing(target, user, @params, 0, 0, null, 0, this);
        user.visible_message(span_warning(I18n.T("[] blasts a flying lollipop at []!", user, target)));
        check_amount();
        return null;
    }

    /// Shoot a gumball
    // tg: code/game/objects/items/robot/items/food.dm:102
    public virtual async DmTask<object> shootG(atom target = null, mob_living user = null, object @params = null)
    {
        if (candy <= 0)
        {
            to_chat(user, span_warning(I18n.T("Not enough gumballs left!")));
            return FALSE;
        }
        candy--;
        obj_item_ammo_casing_gumball gumball;
        if (user is mob_living_silicon_robot robot_user && Dm.truthy(robot_user.emagged))
            gumball = Dm.@new<obj_item_ammo_casing_gumball_harmful>(this);
        else
            gumball = Dm.@new<obj_item_ammo_casing_gumball>(this);

        gumball.loaded_projectile.color = rgb(rand(0, 255), rand(0, 255), rand(0, 255));
        playsound(loc, Dm.resource("sound/items/weapons/bulletflyby3.ogg"), 50, TRUE);
        await gumball.fire_casing(target, user, @params, 0, 0, null, 0, this);
        user.visible_message(span_warning(I18n.T("[] shoots a high-velocity gumball at []!", user, target)));
        check_amount();
        return null;
    }

    // tg: code/game/objects/items/robot/items/food.dm:120
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        check_amount();
        if (iscyborg(user))
        {
            mob_living_silicon_robot robot_user = user as mob_living_silicon_robot;
            obj_item_stock_parts_power_store robot_cell = robot_user.cell as obj_item_stock_parts_power_store;
            if (robot_cell == null || !Dm.truthy(await robot_cell.use(0.012f * STANDARD_CELL_CHARGE)))
            {
                to_chat(user, span_warning(I18n.T("Not enough power.")));
                return ITEM_INTERACT_BLOCKING;
            }
        }

        switch ((int)mode)
        {
            case THROW_LOLLIPOP_MODE:
                await shootL(interacting_with, user, list2params(modifiers));
                return ITEM_INTERACT_SUCCESS;

            case THROW_GUMBALL_MODE:
                await shootG(interacting_with, user, list2params(modifiers));
                return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/robot/items/food.dm:139
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        check_amount();
        if (iscyborg(user))
        {
            mob_living_silicon_robot robot_user = user as mob_living_silicon_robot;
            obj_item_stock_parts_power_store robot_cell = robot_user.cell as obj_item_stock_parts_power_store;
            if (robot_cell == null || !Dm.truthy(await robot_cell.use(0.012f * STANDARD_CELL_CHARGE)))
            {
                to_chat(user, span_warning(I18n.T("Not enough power.")));
                return ITEM_INTERACT_BLOCKING;
            }
        }

        switch ((int)mode)
        {
            case DISPENSE_LOLLIPOP_MODE:
            case DISPENSE_ICECREAM_MODE:
                await dispense(interacting_with, user);
                return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/robot/items/food.dm:154
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        switch_mode(user as mob_living);
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/robot/items/food.dm:158
    public virtual void switch_mode(mob_living user = null)
    {
        switch ((int)mode)
        {
            case DISPENSE_LOLLIPOP_MODE:
                mode = THROW_LOLLIPOP_MODE;
                to_chat(user, span_notice(I18n.T("Module is now throwing lollipops.")));
                break;
            case THROW_LOLLIPOP_MODE:
                mode = THROW_GUMBALL_MODE;
                to_chat(user, span_notice(I18n.T("Module is now blasting gumballs.")));
                break;
            case THROW_GUMBALL_MODE:
                mode = DISPENSE_ICECREAM_MODE;
                to_chat(user, span_notice(I18n.T("Module is now dispensing ice cream.")));
                break;
            case DISPENSE_ICECREAM_MODE:
                mode = DISPENSE_LOLLIPOP_MODE;
                to_chat(user, span_notice(I18n.T("Module is now dispensing lollipops.")));
                break;
        }
    }
}

// tg: code/game/objects/items/robot/items/food.dm:173
public partial class obj_item_borg_lollipop_ice_cream : obj_item_borg_lollipop
{
    // tg: code/game/objects/items/robot/items/food.dm:181
    public override void switch_mode(mob_living user = null)
    {
        return;
    }
}

// tg: code/game/objects/items/robot/items/food.dm:184
public partial class obj_item_ammo_casing_gumball : obj_item_ammo_casing
{
    // tg: code/game/objects/items/robot/items/food.dm:190
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_caseless)));
        return dot;
    }
}

// tg: code/game/objects/items/robot/items/food.dm:197
public partial class obj_projectile_bullet_gumball : obj_projectile_bullet
{
    // tg: code/game/objects/items/robot/items/food.dm:205
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_projectile_drop), typeof(obj_item_food_gumball)));
        RegisterSignal(this, COMSIG_PROJECTILE_ON_SPAWN_DROP, nameof(handle_drop));
        return dot;
    }

    // tg: code/game/objects/items/robot/items/food.dm:213
    [SignalHandler]
    public virtual void handle_drop(datum source = null, obj_item_food_gumball gumball = null)
    {
        gumball.color = color;
    }
}

// tg: code/game/objects/items/robot/items/food.dm:215
public partial class obj_item_ammo_casing_lollipop : obj_item_ammo_casing //NEEDS RANDOMIZED COLOR LOGIC.
{
    // tg: code/game/objects/items/robot/items/food.dm:223
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_caseless)));
        return dot;
    }
}

// tg: code/game/objects/items/robot/items/food.dm:230
public partial class obj_projectile_bullet_lollipop : obj_projectile_bullet
{
    // tg: code/game/objects/items/robot/items/food.dm:254
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        mutable_appearance head = mutable_appearance(Dm.resource("icons/obj/weapons/guns/projectiles.dmi"), "lollipop_2") as mutable_appearance;
        head.color = head_color = rgb(rand(0, 255), rand(0, 255), rand(0, 255));
        add_overlay(head);
        if (embed_type == null)
            Waiting.NoSleep(AddElement(typeof(datum_element_projectile_drop), typeof(obj_item_food_lollipop_cyborg)));
        RegisterSignals(this, Dm.list(COMSIG_PROJECTILE_ON_SPAWN_DROP, COMSIG_PROJECTILE_ON_SPAWN_EMBEDDED), nameof(handle_drop));
        return dot;
    }

    // tg: code/game/objects/items/robot/items/food.dm:263
    [SignalHandler]
    public virtual void handle_drop(datum source = null, obj_item_food_lollipop lollipop = null)
    {
        lollipop.change_head_color(head_color);
    }
}

// tg: code/game/objects/items/robot/items/food.dm:272
public partial class obj_item_borg_cookbook : obj_item_borg
{
    // tg: code/game/objects/items/robot/items/food.dm:280
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cooking = Waiting.NoSleep(AddComponent(typeof(datum_component_personal_crafting))) as datum_component_personal_crafting;
        cooking.forced_mode = TRUE;
        cooking.mode = TRUE;
        return dot;
    }

    // tg: code/game/objects/items/robot/items/food.dm:286
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        await cooking.ui_interact(user);
        return dot;
    }

    // tg: code/game/objects/items/robot/items/food.dm:290
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        SStgui.close_uis(cooking);
        return await base.dropped(user, silent, __extra);
    }
}
