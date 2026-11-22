// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/robot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the cyborg HUD's buttons. The var blocks are generated (D111).

// tg: code/_onclick/hud/screen_objects/robot.dm:1
public partial class atom_movable_screen_robot : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/robot.dm:9
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (isobserver(usr))
            return TRUE;
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/robot.dm:5
public partial class atom_movable_screen_robot_module : atom_movable_screen_robot
{
    // tg: code/_onclick/hud/screen_objects/robot.dm:13
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        mob_living_silicon_robot robot_owner = hud.mymob as mob_living_silicon_robot;
        if (robot_owner.model.GetType() == typeof(obj_item_robot_model))
        {
            if (Dm.truthy(dot))
                return dot;
            await robot_owner.pick_model();
            return dot;
        }

        DmList modifiers = params2list(@params);
        if (robot_owner.module_active != null && !Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) && !Dm.truthy(dot))
        {
            await robot_owner.uneq_active();
            return dot;
        }

        // Observers can look at borg's inventories, so we ignore parent return value here, unlike everywhere else.
        if (usr.active_storage == robot_owner.model.atom_storage)
            robot_owner.model.atom_storage.hide_contents(usr);
        else
            await robot_owner.model.atom_storage.open_storage(usr);
        return TRUE;
    }
}

// tg: code/_onclick/hud/screen_objects/robot.dm:34
public partial class atom_movable_screen_robot_module_slot : atom_movable_screen_robot
{
    // tg: code/_onclick/hud/screen_objects/robot.dm:41
    public virtual void set_slot(object slot_num = null)
    {
        this.slot_num = Dm.num(slot_num);
        name = Dm.text("module[]", slot_num);
        base_icon_state = Dm.text("inv[]", slot_num);
        icon_state = base_icon_state as string;

        switch ((int)Dm.num(slot_num))
        {
            case BORG_CHOOSE_MODULE_ONE:
                screen_loc = ui_inv1;
                break;
            case BORG_CHOOSE_MODULE_TWO:
                screen_loc = ui_inv2;
                break;
            case BORG_CHOOSE_MODULE_THREE:
                screen_loc = ui_inv3;
                break;
        }
    }

    // tg: code/_onclick/hud/screen_objects/robot.dm:55
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_robot R = usr as mob_living_silicon_robot;
        R.toggle_module(slot_num);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/robot.dm:61
public partial class atom_movable_screen_robot_radio : atom_movable_screen_robot
{
    // tg: code/_onclick/hud/screen_objects/robot.dm:66
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(await base.Click(location, control, @params)))
            return null;
        mob_living_silicon_robot R = usr as mob_living_silicon_robot;
        await (R.radio as obj_item_radio).interact(R);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/robot.dm:72
public partial class atom_movable_screen_robot_lamp : atom_movable_screen_robot
{
    // tg: code/_onclick/hud/screen_objects/robot.dm:78
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (Dm.truthy(dot))
            return dot;
        mob_living_silicon_robot user = usr as mob_living_silicon_robot;
        await user.toggle_headlamp();
        update_appearance();
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/robot.dm:86
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[]", base_icon_state, Dm.truthy((hud?.mymob as mob_living_silicon_robot)?.lamp_enabled) ? "on" : "off");
        return await base.update_icon_state(__extra);
    }
}

// tg: code/_onclick/hud/screen_objects/robot.dm:90
public partial class atom_movable_screen_robot_modpc : atom_movable_screen_robot
{
    // tg: code/_onclick/hud/screen_objects/robot.dm:95
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (Dm.truthy(dot))
            return dot;
        mob_living_silicon_robot user = usr as mob_living_silicon_robot;
        if (user.modularInterface != null)
            await user.modularInterface.interact(user);
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/robot.dm:102
public partial class atom_movable_screen_robot_alerts : atom_movable_screen_robot
{
    // tg: code/_onclick/hud/screen_objects/robot.dm:108
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (Dm.truthy(dot))
            return dot;
        mob_living_silicon_robot borgo = usr as mob_living_silicon_robot;
        await borgo.alert_control.ui_interact(borgo);
        return dot;
    }
}
