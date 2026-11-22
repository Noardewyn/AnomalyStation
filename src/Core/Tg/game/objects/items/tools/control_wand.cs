// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/control_wand.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298): the warden's and the heads' door remotes.

// tg: code/game/objects/items/tools/control_wand.dm:7
public partial class obj_item_door_remote : obj_item
{
    private const string WAND_OPEN = "open";
    private const string WAND_BOLT = "bolt";
    private const string WAND_EMERGENCY = "emergency";
    private const string WAND_SHOCK = "shock";
    private const string WAND_DEPOWER = "depower";

    private static DmList restricted_areas => Dm.list(
        typeof(area_station_command_bridge), /*so Captain's remote isn't totally useless*/
        typeof(area_station_security), /*so antag RD/HoP/QM/CMO can't easily screw up the brig doors*/
        typeof(area_station_command_vault), /*aka Vault since it's QM's special thing*/
        typeof(area_station_ai_satellite_chamber), // these are areas exclusive to RD
        typeof(area_station_ai_upload)); // but sometimes mappers might misconfig their doors with our several dozen access helpers

    // tg: code/game/objects/items/tools/control_wand.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        Waiting.NoSleep(update_icon_state());
        // initialize late to make sure job accesses are fully configured
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/objects/items/tools/control_wand.dm:51
    public override void LateInitialize(params object[] __extra)
    {
        // a room with no SSid_access (a toy room's sweep; in tg it always initializes first): the remote simply opens nothing
        if (SSid_access?.accesses_by_region == null || SSid_access.trim_singletons_by_path == null)
            return;
        access_list = SSid_access.get_region_access_list(Dm.list(region_access)) as DmList;
        if (owner_trim != null)
        {
            datum_id_trim_job trim_singlet = SSid_access.trim_singletons_by_path[owner_trim] as datum_id_trim_job;
            access_list = Dm.bitor(access_list, trim_singlet.access) as DmList;
        }
    }

    // tg: code/game/objects/items/tools/control_wand.dm:57
    public virtual bool is_my_domain(area restricted_area = null)
    {
        foreach (object dominion in Dm.each(our_domain))
        {
            if (istype(restricted_area, dominion as Type))
                return true;
        }
        return false;
    }

    // tg: code/game/objects/items/tools/control_wand.dm:63
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        balloon_alert(user, I18n.T("restricted functions unlocked"));
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        await update_icon_state();
        return TRUE;
    }

    // tg: code/game/objects/items/tools/control_wand.dm:72
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        string op = mode switch
        {
            WAND_OPEN => "Open Door",
            WAND_BOLT => "Toggle Bolts",
            WAND_EMERGENCY => "Toggle Emergency Access",
            WAND_SHOCK => "Shock Door",
            _ => "Depower Door",
        };
        switch (mode)
        {
            case WAND_OPEN:
                mode = WAND_BOLT;
                break;
            case WAND_BOLT:
                mode = WAND_EMERGENCY;
                break;
            case WAND_EMERGENCY:
                if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                    mode = WAND_OPEN;
                else
                    mode = WAND_SHOCK;
                break;
            case WAND_SHOCK:
                mode = WAND_DEPOWER;
                break;
            case WAND_DEPOWER:
                mode = WAND_OPEN;
                break;
        }
        await update_icon_state();
        op = mode switch
        {
            WAND_OPEN => "Open Door",
            WAND_BOLT => "Toggle Bolts",
            WAND_EMERGENCY => "Toggle Emergency Access",
            WAND_SHOCK => "Shock Door",
            _ => "Depower Door",
        };
        balloon_alert(user, I18n.T("mode: []", I18n.T(op)));
        if (mode_switch_sound != null)
            playsound(this, mode_switch_sound, 50, TRUE);
        return null;
    }

    // tg: code/game/objects/items/tools/control_wand.dm:93
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!istype(interacting_with, typeof(obj_machinery_door)) && !isturf(interacting_with))
            return NONE;
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/tools/control_wand.dm:168
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        obj_machinery_door door = null;
        if (action_sound != null)
            playsound(this, action_sound, 50, TRUE);

        if (interacting_with is obj_machinery_door clicked_door)
        {
            door = clicked_door;
            if (!Dm.truthy(door.opens_with_door_remote))
                return ITEM_INTERACT_BLOCKING;
        }
        else
        {
            foreach (obj_machinery_door door_on_turf in Dm.each<obj_machinery_door>(get_turf(interacting_with) as atom))
            {
                if (Dm.truthy(door_on_turf.opens_with_door_remote))
                {
                    door = door_on_turf;
                    break;
                }
            }

            if (door == null)
                return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(door.check_access_list(access_list)) || !Dm.truthy(door.requiresID()))
        {
            interacting_with.balloon_alert(user, I18n.T("can't access!"));
            return ITEM_INTERACT_BLOCKING;
        }

        area door_area = get_area(door) as area;
        if (Dm.truthy(is_type_in_list(door_area, restricted_areas)) && !is_my_domain(door_area))
        {
            interacting_with.balloon_alert(user, I18n.T("can't access!"));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_machinery_door_airlock airlock = door as obj_machinery_door_airlock;

        if (!Dm.truthy(door.hasPower()) || (airlock != null && !Dm.truthy(airlock.canAIControl())))
        {
            interacting_with.balloon_alert(user, mode == WAND_OPEN ? I18n.T("it won't budge!") : I18n.T("nothing happens!"));
            return ITEM_INTERACT_BLOCKING;
        }

        switch (mode)
        {
            case WAND_OPEN:
                if (Dm.truthy(door.density))
                    await door.open();
                else
                    await door.close();
                break;

            case WAND_BOLT:
                if (airlock == null)
                {
                    interacting_with.balloon_alert(user, I18n.T("only airlocks!"));
                    return ITEM_INTERACT_BLOCKING;
                }

                if (Dm.truthy(airlock.locked))
                {
                    airlock.unbolt();
                    log_combat(user, airlock, "unbolted", this);
                }
                else
                {
                    airlock.bolt();
                    log_combat(user, airlock, "bolted", this);
                }
                break;

            case WAND_EMERGENCY:
                if (airlock == null)
                {
                    interacting_with.balloon_alert(user, I18n.T("only airlocks!"));
                    return ITEM_INTERACT_BLOCKING;
                }

                airlock.emergency = Dm.truthy(airlock.emergency) ? FALSE : TRUE;
                airlock.update_appearance(UPDATE_ICON);
                break;

            case WAND_SHOCK:
                if (airlock == null)
                {
                    interacting_with.balloon_alert(user, I18n.T("only airlocks!"));
                    return ITEM_INTERACT_BLOCKING;
                }
                if (shock_cooldown > Dm.num(world.time)) // COOLDOWN_FINISHED, inline
                {
                    interacting_with.balloon_alert(user, I18n.T("shock pulse resetting!"));
                    return ITEM_INTERACT_BLOCKING;
                }
                if (Dm.truthy(airlock.isElectrified()))
                {
                    interacting_with.balloon_alert(user, I18n.T("already electrified!"));
                }
                else
                {
                    await airlock.set_electrified(MACHINE_DEFAULT_ELECTRIFY_TIME, user);
                    shock_cooldown = Dm.num(world.time) + 10 * SECONDS; // COOLDOWN_START, inline
                }
                break;

            case WAND_DEPOWER:
                if (airlock == null)
                {
                    interacting_with.balloon_alert(user, I18n.T("only airlocks!"));
                    return ITEM_INTERACT_BLOCKING;
                }
                // First hit disrupts main power, backup comes back in ten seconds, if you stick around you can hit backup for 60 more seconds of downtime.
                if (!Dm.truthy(airlock.main_power_timer))
                    airlock.loseMainPower();
                else if (!Dm.truthy(airlock.backup_power_time))
                    airlock.loseBackupPower();
                break;
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tools/control_wand.dm:254
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        string icon_state_mode = null;
        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            switch (mode)
            {
                case WAND_OPEN:
                    icon_state_mode = "open";
                    break;
                case WAND_BOLT:
                    icon_state_mode = "bolt";
                    break;
                case WAND_EMERGENCY:
                    icon_state_mode = "emergency";
                    break;
            }
        }
        else
        {
            icon_state_mode = "emergency";
        }

        icon_state = Dm.text("[]_[]_[]", base_icon_state, department, icon_state_mode);
        return await base.update_icon_state(__extra);
    }
}
