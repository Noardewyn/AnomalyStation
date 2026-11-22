// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/dept_order.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the Departmental Orders app (its NtosDeptOrder window) on the departments' cargo interfacing
// consoles: a free order from the department's cargo groups, the cooldown its cost sets, the heads' override. The app declares
// linked_department as a type (tg's `datum/job_department/linked_department` holds the department's typepath, rules 3.2), so its var
// block is hand-written; a department's name is read off its type (DV409).

/// Stores an override value for the order cooldown to be used by the Dpt. Order Cooldown button in the secrets menu. When null, the override is not active.
// tg: code/modules/modular_computers/file_system/programs/dept_order.dm:2 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:2  GLOBAL_VAR(department_cd_override)
    public object department_cd_override;

    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:2  (GLOBAL_MANAGED(department_cd_override, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldepartment_cd_override()
    {
        Dm.append(ref gvars_datum_init_order, "department_cd_override");
    }
}

// tg: code/modules/modular_computers/file_system/programs/dept_order.dm:4
public partial class datum_computer_file_program_department_order : datum_computer_file_program
{
    /// Filled via set_linked_department. Serves as "who can place orders".
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:21
    public DmList use_access;
    /// The department we are linked to, typepath.
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:24  a typepath (rules 3.2)
    public Type linked_department;
    /// Stores the time when we can next place an order for each department.
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:26  var/static/list/department_cooldowns (a type static, rules 10.6)
    private static DmList department_cooldowns => TgState.Current.datum_computer_file_program_department_order__department_cooldowns ??= Dm.list(
        Dm.kv(typeof(datum_job_department_engineering), 0f),
        Dm.kv(typeof(datum_job_department_medical), 0f),
        Dm.kv(typeof(datum_job_department_science), 0f),
        Dm.kv(typeof(datum_job_department_security), 0f),
        Dm.kv(typeof(datum_job_department_service), 0f));
    /// Reference to the order we've made UNTIL it gets sent on the supply shuttle. this is so heads can cancel it
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:34
    public datum_supply_order department_order;
    /// The radio channel we will speak into by default.
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:36
    public object radio_channel;
    /// Maps what department should report to what radio channel
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:39  var/static/list/dept_to_radio_channel (a type static, rules 10.6)
    private static DmList dept_to_radio_channel => TgState.Current.datum_computer_file_program_department_order__dept_to_radio_channel ??= Dm.list(
        Dm.kv(typeof(datum_job_department_engineering), RADIO_CHANNEL_ENGINEERING),
        Dm.kv(typeof(datum_job_department_medical), RADIO_CHANNEL_MEDICAL),
        Dm.kv(typeof(datum_job_department_science), RADIO_CHANNEL_SCIENCE),
        Dm.kv(typeof(datum_job_department_security), RADIO_CHANNEL_SECURITY),
        Dm.kv(typeof(datum_job_department_service), RADIO_CHANNEL_SERVICE));

    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:4
    public datum_computer_file_program_department_order()
    {
        filename = "dept_order";
        filedesc = "Departmental Orders";
        can_run_on_flags = PROGRAM_CONSOLE;
        downloader_category = PROGRAM_CATEGORY_SUPPLY;
        program_open_overlay = "request";
        extended_desc = "Allows for departments to order supplied from Cargo for free, with a cooldown between orders.";
        size = 10;
        tgui_id = "NtosDeptOrder";
        program_icon = FA_ICON_CART_FLATBED;
        alert_able = TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:17
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // Filled via set_linked_department. Also serves as "who can cancel the order".
        download_access = Dm.list(ACCESS_COMMAND);
        // Anyone can open, not everyone can use.
        run_access = Dm.list();
        use_access = Dm.list();
    }

    /// Sets the passed department type as the active department for this computer file.
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:48
    public virtual void set_linked_department(Type department = null)
    {
        linked_department = department;
        var linked_department_real = SSjob.get_department_type(linked_department) as datum_job_department;
        if (linked_department_real == null)
            return;
        // Heads of staff can download
        LAZYOR(ref download_access, linked_department_real.head_of_staff_access);
        // Heads of staff + anyone in the dept can run it
        Dm.combine(ref use_access, linked_department_real.head_of_staff_access);
        Dm.combine(ref use_access, linked_department_real.department_access);
        // Also set up the radio
        if (Dm.truthy(dept_to_radio_channel[linked_department]))
            radio_channel = dept_to_radio_channel[linked_department] ?? RADIO_CHANNEL_SUPPLY;
        Waiting.NoSleep(computer.update_static_data_for_all_viewers());
    }

    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:63
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        check_cooldown();
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:66
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["no_link"] = linked_department == null ? TRUE : FALSE;
        data["id_inside"] = computer.stored_id != null ? TRUE : FALSE;
        data["time_left"] = Dm.truthy(department_cooldowns[linked_department]) ? DisplayTimeText(Math.Max(Dm.num(department_cooldowns[linked_department]) - world.time, 0), 1) : null;
        data["can_override"] = department_order != null ? TRUE : FALSE;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:74
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        var linked_department_real = SSjob.get_department_type(linked_department) as datum_job_department;
        if (linked_department_real == null)
            return Dm.list(Dm.kv("supplies", Dm.list()));

        DmList data = Dm.list();

        DmList supply_data = Dm.list();
        foreach (object group in Dm.each(linked_department_real.associated_cargo_groups))
            supply_data[group] = Dm.list();

        foreach (object pack_key in Dm.each(SSshuttle.supply_packs))
        {
            var pack = (datum_supply_pack)SSshuttle.supply_packs[pack_key];
            if (supply_data[pack.group] is not DmList group_list || !can_see_pack(pack))
                continue;

            Dm.append(ref group_list, Dm.list(Dm.list( // UNTYPED_LIST_ADD
                Dm.kv("name", pack.name),
                Dm.kv("cost", pack.get_cost()),
                Dm.kv("id", Dm.text("[]", pack.id)),
                Dm.kv("desc", Dm.truthy(pack.desc) ? pack.desc : pack.name)))); // If there is a description, use it. Otherwise use the pack's name.
        }

        DmList supply_data_flattened = Dm.list();
        foreach (object group in Dm.each(supply_data))
        {
            Dm.append(ref supply_data_flattened, Dm.list(Dm.list( // UNTYPED_LIST_ADD
                Dm.kv("name", group),
                Dm.kv("packs", supply_data[group]))));
        }

        data["supplies"] = supply_data_flattened;
        return data;
    }

    /// Checks if we can "see" the passed supply pack
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:108
    protected virtual bool can_see_pack(datum_supply_pack to_check)
    {
        if (Dm.truthy(Dm.bitand(to_check.order_flags, ORDER_EMAG_ONLY)) && !Dm.truthy(Dm.bitand(computer.obj_flags, EMAGGED)))
            return false;
        if (Dm.truthy(Dm.bitand(to_check.order_flags, ORDER_SPECIAL)) && !Dm.truthy(Dm.bitand(to_check.order_flags, ORDER_SPECIAL_ENABLED)))
            return false;
        if (Dm.truthy(Dm.bitand(to_check.order_flags, ORDER_INVISIBLE | ORDER_POD_ONLY | ORDER_GOODY | ORDER_NOT_DEPARTMENTAL)))
            return false;
        return true;
    }

    /// Looks through all possible departments and finds one this ID card "corresponds" to.
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:119
    protected virtual Type find_department_to_link(obj_item_card_id id_card)
    {
        if (id_card?.GetType() != typeof(obj_item_card_id_advanced_silver))
        {
            // I don't want to introduce weird "access order" behavior with Captain's ID / Chameleon ids / etc, so only silver IDs work
            return null;
        }
        DmList access_to_depts = Dm.list();
        foreach (Type department in Dm.each_cast<Type>(department_cooldowns))
            access_to_depts[Dm.initial(department, (datum_job_department p) => p.head_of_staff_access)] = department;
        foreach (object access_key in Dm.each(id_card.GetAccess() as DmList))
        {
            if (Dm.truthy(access_to_depts[access_key]))
                return access_to_depts[access_key] as Type;
        }
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:132
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (ui.user is not mob_living orderer)
            return dot;

        if (Dm.eq(action, "link"))
        {
            if (linked_department != null)
                return TRUE;

            Type new_dept_type = find_department_to_link(computer.stored_id);
            if (new_dept_type == null)
            {
                computer.physical.balloon_alert(orderer, I18n.T("no department found!"));
                playsound(computer, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
            }
            else
            {
                computer.physical.balloon_alert(orderer, I18n.T("linked"));
                playsound(computer, Dm.resource("sound/machines/ping.ogg"), 30, TRUE);
                set_linked_department(new_dept_type);
            }
            return TRUE;
        }

        if (linked_department == null)
            return TRUE;

        var id_card = (computer.stored_id ?? orderer.get_idcard(hand_first: TRUE)) as obj_item_card_id;
        DmList id_card_access = (id_card?.GetAccess() as DmList) ?? Dm.list();

        if (length(Dm.bitand(use_access, id_card_access)) <= 0)
        {
            computer.physical.balloon_alert(orderer, I18n.T("access denied!"));
            playsound(computer, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
            return TRUE;
        }

        if (Dm.eq(action, "override_order"))
        {
            if (department_order == null || !Dm.@in(department_order, SSshuttle.shopping_list))
                return TRUE;
            if (LAZYLEN(Dm.bitand(download_access, id_card_access) as DmList) <= 0)
            {
                computer.physical.balloon_alert(orderer, I18n.T("requires head of staff access!"));
                playsound(computer, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
                return TRUE;
            }

            department_cooldowns[linked_department] = 0f;
            Dm.remove(ref SSshuttle.shopping_list, department_order);
            department_order = null;
            UnregisterSignal(SSshuttle, COMSIG_SUPPLY_SHUTTLE_BUY);
            return TRUE;
        }

        if (Dm.num(department_cooldowns[linked_department]) > world.time)
            return TRUE;

        await submit_order(orderer, @params["id"]);
        return TRUE;
    }

    /// Submits the order with the specified supply pack id as the specified orderer
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:187
    public virtual async DmTask submit_order(mob_living orderer = null, object id = null)
    {
        id = (object)text2path(Dm.str(id)) ?? id;

        var linked_department_real = SSjob.get_department_type(linked_department) as datum_job_department;
        var pack = SSshuttle.supply_packs[id] as datum_supply_pack;
        if (pack == null)
        {
            await computer.physical.say(I18n.T("Something went wrong!"));
            throw Dm.CRASH(Dm.text("requested supply pack id \"[]\" not found!", id), "code/modules/modular_computers/file_system/programs/dept_order.dm", 184);
        }
        if (!can_see_pack(pack) || !Dm.@in(pack.group, linked_department_real.associated_cargo_groups))
            return;
        object name = "*None Provided*";
        object rank = "*None Provided*";
        object ckey = orderer.ckey;
        if (orderer is mob_living_carbon_human human_orderer)
        {
            name = human_orderer.get_authentification_name();
            rank = human_orderer.get_assignment(hand_first: TRUE);
        }
        else if (HAS_SILICON_ACCESS(orderer))
        {
            name = orderer.real_name;
            rank = "Silicon";
        }
        bool already_signalled = department_order != null;
        object chosen_delivery_area = null;
        foreach (object delivery_area_type in Dm.each(linked_department_real.department_delivery_areas))
        {
            if (Dm.truthy(GLOB.areas_by_type[delivery_area_type]))
            {
                chosen_delivery_area = delivery_area_type;
                break;
            }
        }

        if (Dm.eq(SSshuttle.supply.get_order_count(pack), OVER_ORDER_LIMIT))
        {
            playsound(computer, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
            await computer.physical.say(I18n.T("ERROR: No more then [] of any pack may be ordered at once!", CARGO_MAX_ORDER));
            return;
        }

        department_order = Dm.@new<datum_supply_order>(pack, name, rank, ckey, "Departmental Order", null, chosen_delivery_area, null, null, FALSE);
        Dm.append(ref SSshuttle.shopping_list, department_order);
        if (!already_signalled)
            RegisterSignal(SSshuttle, COMSIG_SUPPLY_SHUTTLE_BUY, nameof(finalize_department_order));
        if (!Dm.truthy(alert_silenced) && Dm.truthy(alert_able))
        {
            await aas_config_announce(typeof(datum_aas_config_entry_department_orders), Dm.list(Dm.kv("ORDER", pack.name), Dm.kv("PERSON", name)), computer.physical, Dm.list(radio_channel), "Order Placed");
            // DEVIATION(DV409): tg reads linked_department.department_name off the typepath
            await aas_config_announce(typeof(datum_aas_config_entry_department_orders_cargo), Dm.list(Dm.kv("DEPARTMENT", Dm.initial(linked_department, (datum_job_department p) => p.department_name))), computer.physical, Dm.list(RADIO_CHANNEL_SUPPLY));
        }
        await computer.physical.say(I18n.T("Order processed. Cargo will deliver the crate when it comes in on their shuttle. NOTICE: Heads of staff may override the order."));
        calculate_cooldown(pack.cost);
    }

    /// Signal when the supply shuttle begins to spawn orders. We forget the current order preventing it from being overridden (since it's already past the point of no return on undoing the order)
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:240
    public virtual void finalize_department_order(datum subsystem = null)
    {
        if (department_order != null && Dm.@in(department_order, SSshuttle.shopping_list))
            department_order = null;
        UnregisterSignal(subsystem, COMSIG_SUPPLY_SHUTTLE_BUY);
    }

    /// Calculates the cooldown it will take for this department's free order, based on its credit cost
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:247
    public virtual void calculate_cooldown(float credits = 0)
    {
        if (GLOB.department_cd_override == null)
        {
            float time_y = DEPARTMENTAL_ORDER_COOLDOWN_COEFFICIENT * (float)Math.Pow(log(10, credits), DEPARTMENTAL_ORDER_COOLDOWN_EXPONENT) * (1 * SECONDS);
            department_cooldowns[linked_department] = world.time + time_y;
        }
        else
        {
            float time_y = Dm.num(GLOB.department_cd_override) * SECONDS;
            department_cooldowns[linked_department] = world.time + time_y;
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:255
    public override object process_tick(object seconds_per_tick = null)
    {
        if (!check_cooldown() || Dm.truthy(alert_silenced) || !Dm.truthy(alert_able))
            return null;
        Waiting.NoSleep(aas_config_announce(typeof(datum_aas_config_entry_department_orders), Dm.list(), computer.physical, Dm.list(radio_channel), "Cooldown Reset"));
        computer.alert_call(this, I18n.T("Order cooldown expired!"), Dm.resource("sound/machines/ping.ogg"));
        return null;
    }

    /// Checks if the cooldown is up and resets it if so.
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:262
    public virtual bool check_cooldown()
    {
        if (Dm.num(department_cooldowns[linked_department]) > 0 && Dm.num(department_cooldowns[linked_department]) <= world.time)
        {
            department_cooldowns[linked_department] = 0f;
            return true;
        }
        return false;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/file_system/programs/dept_order.dm:26, :39  the static lists
    internal DmList datum_computer_file_program_department_order__department_cooldowns;
    internal DmList datum_computer_file_program_department_order__dept_to_radio_channel;
}
