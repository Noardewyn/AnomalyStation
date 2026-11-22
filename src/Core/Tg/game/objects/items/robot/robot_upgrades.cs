// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/robot/robot_upgrades.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the cyborg upgrade modules and the emergency reboot board. The var blocks are generated
// (D111).

// robot_upgrades.dm
// Contains various borg upgrades.

// tg: code/game/objects/items/robot/robot_upgrades.dm:4
public partial class obj_item_borg_upgrade : obj_item_borg
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:27
    public virtual async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        if (borg.stat == DEAD)
        {
            to_chat(user, span_warning(I18n.T("[] will not function on a deceased cyborg!", this)));
            return FALSE;
        }
        if (model_type != null && !Dm.truthy(is_type_in_list(borg.model, model_type)))
        {
            to_chat(borg, span_alert(I18n.T("Upgrade mounting error! No suitable hardpoint detected.")));
            to_chat(user, span_warning(I18n.T("There's no mounting point for the module!")));
            return FALSE;
        }
        if (!Dm.truthy(allow_duplicates) && Dm.truthy(Dm.locate_type(GetType(), borg.upgrades)))
        {
            to_chat(borg, span_alert(I18n.T("Upgrade mounting error! Hardpoint already occupied!")));
            to_chat(user, span_warning(I18n.T("The mounting point for the module is already occupied!")));
            return FALSE;
        }
        // Handles adding/removing items.
        if (Dm.truthy(length(items_to_add)))
            await install_items(borg, user, items_to_add);
        if (Dm.truthy(length(items_to_remove)))
            await remove_items(borg, user, items_to_remove);
        return TRUE;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:46
    public virtual async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        if (!Dm.@in(this, borg.upgrades))
            return FALSE;

        // Handles reverting the items back
        if (Dm.truthy(length(items_to_add)))
            await remove_items(borg, user, items_to_add);
        if (Dm.truthy(length(items_to_remove)))
            await install_items(borg, user, items_to_remove);
        return TRUE;
    }

    // Handles adding items with the module
    // tg: code/game/objects/items/robot/robot_upgrades.dm:58
    public virtual async DmTask<object> install_items(mob_living_silicon_robot borg = null, mob_living user = null, DmList items = null)
    {
        foreach (object item_to_add in Dm.each(items))
        {
            obj_item module_item = Dm.@new((Type)item_to_add, borg.model) as obj_item;
            Dm.append(ref borg.model.basic_modules, module_item);
            await borg.model.add_module(module_item, FALSE, TRUE);
        }
        return TRUE;
    }

    // Handles removing some items as the module is installed
    // tg: code/game/objects/items/robot/robot_upgrades.dm:66
    public virtual async DmTask<object> remove_items(mob_living_silicon_robot borg = null, mob_living user = null, DmList items = null)
    {
        foreach (object item_to_remove in Dm.each(items))
        {
            obj_item module_item = Dm.locate_type((Type)item_to_remove, borg.model.modules) as obj_item;
            if (module_item != null)
                await borg.model.remove_module(module_item);
        }
        return TRUE;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:73
public partial class obj_item_borg_upgrade_rename : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:82
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object new_heldname = await sanitize_name(await tgui_input_text(user, I18n.T("Enter new robot name"), I18n.T("Cyborg Reclassification"), heldname, MAX_NAME_LEN), allow_numbers: TRUE);
        if (!Dm.truthy(new_heldname) || !Dm.truthy(user.is_holding(this)))
            return null;
        heldname = Dm.str(new_heldname);
        user.log_message(Dm.text("set \"[]\" as a name in a cyborg reclassification board at []", heldname, loc_name(user)), LOG_GAME);
        return null;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:89
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        object oldname = borg.real_name;
        object oldkeyname = key_name(borg);
        borg.custom_name = heldname;
        await borg.updatename();
        if (Dm.eq(oldname, borg.real_name))
            borg.notify_ai(AI_NOTIFICATION_CYBORG_RENAMED, oldname, borg.real_name);
        user.log_message(Dm.text("used a cyborg reclassification board to rename [] to []", oldkeyname, key_name(borg)), LOG_GAME);
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:101
public partial class obj_item_borg_upgrade_disablercooler : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:112
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot))
            return dot;

        obj_item_gun_energy_disabler_cyborg disabler = Dm.locate<obj_item_gun_energy_disabler_cyborg>(borg.model.modules);
        if (disabler == null)
        {
            to_chat(user, span_warning(I18n.T("There's no disabler in this unit!")));
            return FALSE;
        }
        if (Dm.num(disabler.charge_delay) <= 2)
        {
            to_chat(borg, span_warning(I18n.T("A cooling unit is already installed!")));
            to_chat(user, span_warning(I18n.T("There's no room for another cooling unit!")));
            return FALSE;
        }

        disabler.charge_delay = max(2, Dm.num(disabler.charge_delay) - 4);
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:128
    public override async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_gun_energy_disabler_cyborg disabler = Dm.locate<obj_item_gun_energy_disabler_cyborg>(borg.model.modules);
        if (disabler == null)
            return FALSE;
        disabler.charge_delay = Dm.num(Dm.initial(disabler, "charge_delay"));
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:137
public partial class obj_item_borg_upgrade_thrusters : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:143
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(borg.ionpulse))
        {
            to_chat(user, span_warning(I18n.T("This unit already has ion thrusters installed!")));
            return FALSE;
        }

        borg.ionpulse = TRUE;
        borg.toggle_ionpulse(); //Enabled by default
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:154
    public override async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        borg.ionpulse = FALSE;
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:242
public partial class obj_item_borg_upgrade_syndicate : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:249
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:253
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(borg.emagged))
            return FALSE;

        await borg.SetEmagged(TRUE);
        await borg.logevent("WARN: hardware installed with missing security certificate!"); //A bit of fluff to hint it was an illegal tech item
        await borg.logevent(Dm.text("WARN: root privleges granted to PID [][].", num2hex(rand(1, 65535), -1), num2hex(rand(1, 65535), -1))); //random eight digit hex value. Two are used because rand(1,4294967295) throws an error

        return TRUE;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:266
    public override async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        await borg.SetEmagged(FALSE);
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:272
public partial class obj_item_borg_upgrade_lavaproof : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:282
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        borg.add_traits(Dm.list(TRAIT_LAVA_IMMUNE, TRAIT_SNOWSTORM_IMMUNE), GetType());
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:288
    public override async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        borg.remove_traits(Dm.list(TRAIT_LAVA_IMMUNE, TRAIT_SNOWSTORM_IMMUNE), GetType());
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:294
public partial class obj_item_borg_upgrade_selfrepair : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:309
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        icon_state = "selfrepair_off";
        toggle_action = Dm.@new<datum_action_item_action_toggle>(this);
        await toggle_action.Grant(borg);
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:317
    public override async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        await toggle_action.Remove(borg);
        QDEL_NULL(ref toggle_action);
        deactivate_sr();
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:325
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (Dm.truthy(on))
        {
            to_chat(toggle_action.owner, span_notice(I18n.T("You deactivate the self-repair module.")));
            deactivate_sr();
        }
        else
        {
            to_chat(toggle_action.owner, span_notice(I18n.T("You activate the self-repair module.")));
            activate_sr();
        }
        return null;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:334
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (toggle_action != null)
            icon_state = Dm.text("selfrepair_[]", Dm.truthy(on) ? "on" : "off");
        else
            icon_state = "cyborg_upgrade5";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:341
    public virtual void activate_sr()
    {
        START_PROCESSING(SSobj, this);
        on = TRUE;
        update_appearance();
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:346
    public virtual void deactivate_sr()
    {
        STOP_PROCESSING(SSobj, this);
        on = FALSE;
        update_appearance();
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:351
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (world.time < next_repair)
            return null;

        mob_living_silicon_robot cyborg = toggle_action.owner as mob_living_silicon_robot;

        if (cyborg is mob_living_silicon_robot && (cyborg.stat != DEAD) && Dm.truthy(on))
        {
            if (cyborg.cell is not obj_item_stock_parts_power_store cyborg_cell)
            {
                to_chat(cyborg, span_alert(I18n.T("Self-repair module deactivated. Please insert power cell.")));
                deactivate_sr();
                return null;
            }

            if (Dm.num(cyborg_cell.charge) < energy_cost * 2)
            {
                to_chat(cyborg, span_alert(I18n.T("Self-repair module deactivated. Please recharge.")));
                deactivate_sr();
                return null;
            }

            if (Dm.num(cyborg.health) < Dm.num(cyborg.maxHealth))
            {
                if (Dm.num(cyborg.health) < 0)
                {
                    repair_amount = -2.5f;
                    energy_cost = 0.03f * STANDARD_CELL_CHARGE;
                }
                else
                {
                    repair_amount = -1;
                    energy_cost = 0.01f * STANDARD_CELL_CHARGE;
                }
                Waiting.NoSleep(cyborg.adjust_brute_loss(repair_amount));
                Waiting.NoSleep(cyborg.adjust_fire_loss(repair_amount));
                Waiting.NoSleep(cyborg.updatehealth());
                Waiting.NoSleep(cyborg_cell.use(energy_cost));
            }
            else
                Waiting.NoSleep(cyborg_cell.use(0.005f * STANDARD_CELL_CHARGE));
            next_repair = world.time + repair_cooldown * 10; // Multiply by 10 since world.time is in deciseconds

            if (!Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_BORG_SELF_REPAIR))) // TIMER_COOLDOWN_FINISHED
            {
                // TIMER_COOLDOWN_START(src, COOLDOWN_BORG_SELF_REPAIR, 200 SECONDS) (inline, rules 11.1 (f))
                LAZYSET(ref cooldowns, COOLDOWN_BORG_SELF_REPAIR, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), this, COOLDOWN_BORG_SELF_REPAIR), 200 * SECONDS, file: "code/game/objects/items/robot/robot_upgrades.dm", line: 384));
                string msgmode = "standby";
                if (Dm.num(cyborg.health) < 0)
                    msgmode = "critical";
                else if (Dm.num(cyborg.health) < Dm.num(cyborg.maxHealth))
                    msgmode = "normal";
                to_chat(cyborg, span_notice(I18n.T("Self-repair is active in [] mode.", span_boldnotice(msgmode))));
            }
        }
        else
            deactivate_sr();
        return null;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:394
public partial class obj_item_borg_upgrade_hypospray : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:404
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        foreach (obj_item_reagent_containers_borghypo_medical hypo in Dm.each<obj_item_reagent_containers_borghypo_medical>(borg.model.modules))
            await hypo.upgrade_hypo();
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:411
    public override async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        foreach (obj_item_reagent_containers_borghypo_medical hypo in Dm.each<obj_item_reagent_containers_borghypo_medical>(borg.model.modules))
            await hypo.remove_hypo_upgrade();
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:424
public partial class obj_item_borg_upgrade_piercing_hypospray : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:431
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        bool found_hypo = false;
        foreach (obj_item_reagent_containers_borghypo hypo in Dm.each<obj_item_reagent_containers_borghypo>(borg.model.modules))
        {
            if (!Dm.truthy(hypo.allow_piercing))
                continue;
            hypo.bypass_protection = TRUE;
            found_hypo = true;
        }

        if (!found_hypo)
        {
            to_chat(user, span_warning(I18n.T("There are no installed hypospray modules to upgrade with piercing!"))); //check to see if any hyposprays were upgraded
            return FALSE;
        }

        // If we are actually going to install the upgrade due to the presence of compatible modules, make sure their emagged counterparts get upgraded too.
        foreach (obj_item_reagent_containers_borghypo hypo in Dm.each<obj_item_reagent_containers_borghypo>(borg.model.emag_modules))
            hypo.bypass_protection = TRUE;
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:450
    public override async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        foreach (obj_item_reagent_containers_borghypo hypo in Dm.each<obj_item_reagent_containers_borghypo>(borg.model.modules))
            hypo.bypass_protection = Dm.num(Dm.initial(hypo, "bypass_protection"));
        foreach (obj_item_reagent_containers_borghypo hypo in Dm.each<obj_item_reagent_containers_borghypo>(borg.model.emag_modules))
            hypo.bypass_protection = Dm.num(Dm.initial(hypo, "bypass_protection"));
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:459
public partial class obj_item_borg_upgrade_surgery_omnitool : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:473
    public override async DmTask<object> action(mob_living_silicon_robot cyborg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(cyborg, user);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(cyborg, TRAIT_FASTMED, REF(this));
        foreach (obj_item_borg_cyborg_omnitool_medical omnitool in Dm.each<obj_item_borg_cyborg_omnitool_medical>(cyborg.model.modules))
            omnitool.set_upgraded(TRUE);
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:481
    public override async DmTask<object> deactivate(mob_living_silicon_robot cyborg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(cyborg, user);
        if (!Dm.truthy(dot))
            return dot;
        REMOVE_TRAIT(cyborg, TRAIT_FASTMED, REF(this));
        foreach (obj_item_borg_cyborg_omnitool_medical omnitool in Dm.each<obj_item_borg_cyborg_omnitool_medical>(cyborg.model.modules))
            omnitool.set_upgraded(FALSE);
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:489
public partial class obj_item_borg_upgrade_engineering_omnitool : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:499
    public override async DmTask<object> action(mob_living_silicon_robot cyborg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(cyborg, user);
        if (!Dm.truthy(dot))
            return null;
        foreach (obj_item_borg_cyborg_omnitool_engineering omnitool in Dm.each<obj_item_borg_cyborg_omnitool_engineering>(cyborg.model.modules))
            omnitool.set_upgraded(TRUE);
        foreach (obj_item_weldingtool_largetank_cyborg welder in Dm.each<obj_item_weldingtool_largetank_cyborg>(cyborg.model.modules))
            welder.toolspeed = Dm.num(Dm.initial(welder, "toolspeed")) - 0.3f;
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:508
    public override async DmTask<object> deactivate(mob_living_silicon_robot cyborg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(cyborg, user);
        if (!Dm.truthy(dot))
            return null;
        foreach (obj_item_borg_cyborg_omnitool_engineering omnitool in Dm.each<obj_item_borg_cyborg_omnitool_engineering>(cyborg.model.modules))
            omnitool.set_upgraded(FALSE);
        foreach (obj_item_weldingtool_largetank_cyborg welder in Dm.each<obj_item_weldingtool_largetank_cyborg>(cyborg.model.modules))
            welder.toolspeed = Dm.num(Dm.initial(welder, "toolspeed"));
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:517
public partial class obj_item_borg_upgrade_defib : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:529
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        obj_item_borg_upgrade_defib_backpack defib_pack = Dm.locate<obj_item_borg_upgrade_defib_backpack>(borg); //If a full defib unit was used to upgrade prior, we can just pop it out now and replace
        if (defib_pack != null)
        {
            await defib_pack.deactivate(borg, user);
            to_chat(user, span_notice(I18n.T("The defibrillator pops out of the chassis as the compact upgrade installs.")));
        }
        return await base.action(borg, user);
    }
}

///A version of the above that also acts as a holder of an actual defibrillator item used in place of the upgrade chip.
// tg: code/game/objects/items/robot/robot_upgrades.dm:537
public partial class obj_item_borg_upgrade_defib_backpack : obj_item_borg_upgrade_defib
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:540
    [DmParams("mapload", "defib")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_defibrillator defib = Dm.arg<obj_item_defibrillator>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (defib == null)
            defib = Dm.@new<obj_item_defibrillator>();
        defib_instance = defib;
        name = defib_instance.name;
        Waiting.NoSleep(defib_instance.moveToNullspace());
        RegisterSignals(defib_instance, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED), nameof(on_defib_instance_qdel_or_moved));
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:549
    [SignalHandler]
    public virtual void on_defib_instance_qdel_or_moved(obj_item_defibrillator defib = null)
    {
        defib_instance = null;
        if (!QDELETED(this))
            qdel(this);
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:555
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(defib_instance))
            QDEL_NULL(ref defib_instance);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:560
    public override async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        if (defib_instance != null)
            await defib_instance.forceMove(borg.drop_location() as atom); // [on_defib_instance_qdel_or_moved()] handles the rest.
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:579
public partial class obj_item_borg_upgrade_ai : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:586
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(borg.key)) //You cannot replace a player unless the key is completely removed.
        {
            to_chat(user, span_warning(I18n.T("Intelligence patterns detected in this []. Aborting.", borg.braintype)));
            return FALSE;
        }

        await borg.make_shell(this);
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:596
    public override async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(borg, user);
        if (!Dm.truthy(dot) || !Dm.truthy(borg.shell))
            return dot;

        await borg.undeploy();
        borg.notify_ai(AI_NOTIFICATION_AI_SHELL);
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:604
public partial class obj_item_borg_upgrade_expand : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:610
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot) || HAS_TRAIT(borg, TRAIT_NO_TRANSFORM))
            return FALSE;

        if (Dm.truthy(borg.hasExpanded))
        {
            to_chat(usr, span_warning(I18n.T("This unit already has an expand module installed!")));
            return FALSE;
        }

        ADD_TRAIT(borg, TRAIT_NO_TRANSFORM, REF(this));
        float prev_lockcharge = borg.lockcharge;
        await borg.SetLockdown(TRUE);
        await borg.set_anchored(TRUE);
        await do_smoke(1, borg, borg.loc);
        await sleep(0.2f * SECONDS);
        for (int i = 1; i <= 4; i++)
        {
            playsound(borg, pick(
                Dm.resource("sound/items/tools/drill_use.ogg"),
                Dm.resource("sound/items/tools/jaws_cut.ogg"),
                Dm.resource("sound/items/tools/jaws_pry.ogg"),
                Dm.resource("sound/items/tools/welder.ogg"),
                Dm.resource("sound/items/tools/ratchet.ogg")
                ), 80, TRUE, -1);
            await sleep(1.2f * SECONDS);
        }
        if (!Dm.truthy(prev_lockcharge))
            await borg.SetLockdown(FALSE);
        await borg.set_anchored(FALSE);
        REMOVE_TRAIT(borg, TRAIT_NO_TRANSFORM, REF(this));
        borg.hasExpanded = TRUE;
        await borg.update_transform(2);
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:641
    public override async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(borg.hasExpanded))
        {
            borg.hasExpanded = FALSE;
            await borg.update_transform(0.5f);
        }
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:649
public partial class obj_item_borg_upgrade_rped : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:669
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_borg_upgrade_smallrped upgrade = Dm.locate<obj_item_borg_upgrade_smallrped>(borg);
        obj_item_storage_part_replacer_cyborg_small replacer = Dm.locate<obj_item_storage_part_replacer_cyborg_small>(borg.model.modules);
        if (upgrade != null)
        {
            to_chat(user, span_notice(I18n.T("The old RPED module is now expanded and gets more space")));
            await replacer.emptyStorage();
            await replacer.forceMove(get_turf(borg));
            qdel(upgrade);
        }
        return dot;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:691
public partial class obj_item_borg_upgrade_pinpointer : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:703
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        crew_monitor = Dm.@new<datum_action_item_action_crew_monitor>(this);
        await crew_monitor.Grant(borg);
        icon_state = "crew_monitor";
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:712
    public override async DmTask<object> deactivate(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.deactivate(borg, user);
        if (!Dm.truthy(dot))
            return dot;
        icon_state = "pinpointer_crew";
        await crew_monitor.Remove(borg);
        QDEL_NULL(ref crew_monitor);
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_upgrades.dm:720
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (Dm.truthy(await base.ui_action_click(user, actiontype)))
            return null;
        mob_living_silicon_robot borg = usr as mob_living_silicon_robot;
        await GLOB.crewmonitor.show(borg, borg);
        return null;
    }
}

// tg: code/game/objects/items/robot/robot_upgrades.dm:729
public partial class obj_item_borg_upgrade_transform : obj_item_borg_upgrade
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:735
    public override async DmTask<object> action(mob_living_silicon_robot borg = null, mob_living user = null)
    {
        user ??= usr as mob_living;
        object dot = await base.action(borg, user);
        if (Dm.truthy(dot) && new_model != null)
            await borg.model.transform_to(new_model);
        return dot;
    }
}

///This isn't an upgrade or part of the same path, but I'm gonna just stick it here because it's a tool used on cyborgs.
//A reusable tool that can bring borgs back to life. They gotta be repaired first, though.
// tg: code/game/objects/items/robot/robot_upgrades.dm:883
public partial class obj_item_borg_restart_board : obj_item
{
    // tg: code/game/objects/items/robot/robot_upgrades.dm:891
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (target is not mob_living_silicon_robot borgo)
            return await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (!Dm.truthy(borgo.opened))
        {
            to_chat(user, span_warning(I18n.T("You must access the cyborg's internals!")));
            return await base.pre_attack(target, user, modifiers, attack_modifiers);
        }
        if (Dm.num(borgo.health) < 0)
        {
            to_chat(user, span_warning(I18n.T("You have to repair the cyborg before using this module!")));
            return await base.pre_attack(target, user, modifiers, attack_modifiers);
        }
        if (!Dm.truthy(Dm.bitand(borgo.stat, DEAD)))
        {
            to_chat(user, span_warning(I18n.T("This cyborg is already operational!")));
            return await base.pre_attack(target, user, modifiers, attack_modifiers);
        }

        if (borgo.mind != null)
        {
            borgo.mind.grab_ghost();
            playsound(loc, Dm.resource("sound/mobs/non-humanoids/cyborg/liveagain.ogg"), 75, TRUE);
        }
        else
            playsound(loc, Dm.resource("sound/machines/ping.ogg"), 75, TRUE);

        await borgo.revive();
        await borgo.logevent("WARN -- System recovered from unexpected shutdown.");
        await borgo.logevent("System brought online.");
        return await base.pre_attack(target, user, modifiers, attack_modifiers);
    }
}
