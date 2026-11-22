// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/laser_gatling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the laser gatling gun and its backpack power source. The pack worn on the back hands out its
// gun when clicked ("notholstered"), takes it back when the gun is dropped or used on it; the gun fires on full auto only while its
// pack is on the shooter, each shot heating the pack (the heat sensor locks the trigger at the cap, the heat diffuses over time)
// and refilling the gun's cell from the pack's fusion core. The fusion core's cell is var-only. The pack's `battery` holds the core
// itself (the converter typed it as a path): hand-declared here, which puts the class in hand mode (types-converter.md 3.7), so its
// generated constructor is kept here too. The other var blocks are generated (D111).

//The ammo/gun is stored in a back slot item
// tg: code/modules/projectiles/guns/energy/laser_gatling.dm:4
public partial class obj_item_minigunpack : obj_item
{
    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:16  var/obj/item/stock_parts/power_store/cell/minigun/battery
    public obj_item_stock_parts_power_store_cell_minigun battery;

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:5  (the generated constructor, kept here: hand mode, see the header)
    public obj_item_minigunpack()
    {
        name = "backpack power source";
        desc = "The massive external power source for the laser gatling gun.";
        icon = Dm.resource("icons/obj/weapons/guns/minigun.dmi");
        icon_state = "holstered";
        inhand_icon_state = "backpack";
        lefthand_file = Dm.resource("icons/mob/inhands/equipment/backpack_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/equipment/backpack_righthand.dmi");
        slot_flags = 1024;
        w_class = 5;
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        gun = Dm.@new<obj_item_gun_energy_minigun>(this);
        battery = Dm.@new<obj_item_stock_parts_power_store_cell_minigun>(this);
        START_PROCESSING(SSobj, this);
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:29
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(gun))
            qdel(gun);
        gun = null;
        QDEL_NULL(ref battery);
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:37
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        overheat = max(0, overheat - heat_diffusion * Dm.num(seconds_per_tick));
        return null;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:41
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc == user)
        {
            if (!Dm.truthy(armed))
            {
                if (Dm.eq(user.get_item_by_slot(ITEM_SLOT_BACK), this))
                {
                    armed = TRUE;
                    if (!Dm.truthy(await user.put_in_hands(gun)))
                    {
                        armed = FALSE;
                        to_chat(user, span_warning(I18n.T("You need a free hand to hold the gun!")));
                        return null;
                    }
                    update_appearance();
                    await user.update_worn_back();
                }
            }
            else
                to_chat(user, span_warning(I18n.T("You are already holding the gun!")));
        }
        else
            await base.attack_hand(user, modifiers, __extra);
        return null;
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:57
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool != gun) //Don't need armed check, because if you have the gun assume its armed.
            return NONE;
        await user.dropItemToGround(gun, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:64
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (Dm.truthy(armed))
            await user.dropItemToGround(gun, TRUE);
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:69
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(armed) ? "notholstered" : "holstered";
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:73
    public virtual async DmTask attach_gun(mob user = null)
    {
        if (gun == null)
            gun = Dm.@new<obj_item_gun_energy_minigun>(this);
        await gun.forceMove(this);
        armed = FALSE;
        if (user != null)
            to_chat(user, span_notice(I18n.T("You attach \\the [] to \\the [].", gun, this)));
        else
            visible_message(span_warning(I18n.T("\\The [] snaps back onto \\the []!", gun, this)));
        update_appearance();
        await user.update_worn_back();
    }
}

// tg: code/modules/projectiles/guns/energy/laser_gatling.dm:86
public partial class obj_item_gun_energy_minigun : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:104
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!istype(loc, typeof(obj_item_minigunpack))) //We should spawn inside an ammo pack so let's use that one.
            return INITIALIZE_HINT_QDEL; //No pack, no gun
        ammo_pack = loc as obj_item_minigunpack;
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_automatic_fire), 0.2f * SECONDS));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:112
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(ammo_pack))
            qdel(ammo_pack);
        ammo_pack = null;
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:118
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        return null;
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:121  SHOULD_CALL_PARENT(FALSE)
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        if (ammo_pack != null)
            await ammo_pack.attach_gun(user);
        else
            qdel(this);
        return null;
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:128
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        message ??= TRUE;
        zone_override ??= "";
        bonus_spread ??= 0;
        object dot = null;
        if (ammo_pack != null && ammo_pack.overheat >= ammo_pack.overheat_max)
        {
            to_chat(user, span_warning(I18n.T("The gun's heat sensor locked the trigger to prevent lens damage!")));
            return dot;
        }
        dot = await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
        if (!Dm.truthy(dot))
            return dot;
        ammo_pack.overheat++;
        if (ammo_pack.battery != null)
        {
            object transferred = await ammo_pack.battery.use(cell.maxcharge - cell.charge, Dm.named("force", TRUE));
            await cell.give(transferred);
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/laser_gatling.dm:141
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        if (ammo_pack == null || ammo_pack.loc != user)
        {
            to_chat(user, span_warning(I18n.T("You need the backpack power source to fire the gun!")));
            return FALSE;
        }
        return await base.try_fire_gun(target, user, @params);
    }
}
