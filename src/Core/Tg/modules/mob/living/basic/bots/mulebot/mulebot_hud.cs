// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:1
public partial class mob_living_basic_bot_mulebot : mob_living_basic_bot
{
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm:1
    public virtual void set_cell_hud()
    {
        if (!Dm.truthy(has_power()))
        {
            set_hud_image_state(DIAG_BATT_HUD, hud_state: "hudnobatt");
            return;
        }

        atom_movable_screen_mob_charge charge_hud = hud_used?.screen_objects?[HUD_MULEBOT_CHARGE] as atom_movable_screen_mob_charge;
        charge_hud?.calculate_charge();
        set_hud_image_state(DIAG_BATT_HUD, hud_state: Dm.text("hudbatt[]", RoundDiagBar(Dm.num(cell.charge) / Dm.num(cell.maxcharge))));
    }
}

// tg: code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm:10
public partial class atom_movable_screen_mob_charge : atom_movable_screen
{
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm:22
    public virtual void update_battery_overlay(atom target_battery = null)
    {
        obj_item_stock_parts_power_store_cell my_cell = (target_battery ?? Dm.locate<obj_item_stock_parts_power_store_cell>(get_mob())) as obj_item_stock_parts_power_store_cell;
        if (my_cell == null)
            battery_overlay = null;
        else
            battery_overlay = image(icon: my_cell.icon, icon_state: my_cell.icon_state, loc: this, layer: layer + 0.1f);
        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm:30
    public virtual void calculate_charge()
    {
        obj_item_stock_parts_power_store_cell my_battery = Dm.locate<obj_item_stock_parts_power_store_cell>(get_mob());
        float charge_value = my_battery == null ? 0 : Dm.num(round(Dm.num(my_battery.charge) / Dm.num(my_battery.maxcharge) * 100, 1));
        current_charge_level = Dm.num(round(charge_value * 4 / 100));
        charge_overlay.maptext = MAPTEXT(Dm.text("<div align='center' valign='middle' style='position:relative'>[]%</div>", charge_value));
        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm:37
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        charge_overlay = image(loc: this, layer: layer + 0.2f, pixel_y: -5);
        update_battery_overlay();
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm:42
    public override object Destroy(float? force = null)
    {
        charge_overlay = null;
        battery_overlay = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm:47
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        Dm.append(ref dot, mutable_appearance(icon, Dm.text("ccharger-o[]", current_charge_level)));
        if (battery_overlay != null)
            Dm.combine(ref dot, battery_overlay);
        if (Dm.truthy(hovering))
            Dm.combine(ref dot, charge_overlay);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm:55
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        if (usr != get_mob())
            return;
        await base.MouseEntered(location, control, @params);
        hovering = TRUE;
        calculate_charge();
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm:62
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        if (usr != get_mob())
            return;
        base.MouseExited(location, control, @params);
        hovering = FALSE;
        update_appearance(UPDATE_ICON);
    }
}

// tg: code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm:69
public partial class datum_hud_living_mulebot : datum_hud_living
{
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_hud.dm:71
    public override async DmTask initialize_screen_objects()
    {
        await base.initialize_screen_objects();
        await add_screen_object(typeof(atom_movable_screen_mob_charge), HUD_MULEBOT_CHARGE, HUD_GROUP_INFO);
    }
}
