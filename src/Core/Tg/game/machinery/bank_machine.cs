// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/bank_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4a (D327): the whole file: the vault's bank machine with its BankMachine window: money fed to it goes to the cargo budget, and
// a siphon draws the budget out into space cash, warning the station over the radio. The class declares radio (an instance, where the
// generator's name-based V3 would make it a Type), so its var block and type overrides are hand-written here.

// tg: code/game/machinery/bank_machine.dm:1
[DmPath("/obj/machinery/computer/bank_machine")]
public partial class obj_machinery_computer_bank_machine : obj_machinery_computer
{
    ///Whether the machine is currently being siphoned
    // tg: code/game/machinery/bank_machine.dm:10
    public float siphoning = FALSE;
    ///While siphoning, how much money do we have? Will drop this once siphon is complete.
    // tg: code/game/machinery/bank_machine.dm:12
    public float syphoning_credits = 0;
    ///Whether siphoning is authorized or not (has access)
    // tg: code/game/machinery/bank_machine.dm:14
    public float unauthorized = FALSE;
    ///Amount of time before the next warning over the radio is announced.
    // tg: code/game/machinery/bank_machine.dm:16
    public float next_warning = 0;
    ///The amount of time we have between warnings
    // tg: code/game/machinery/bank_machine.dm:18
    public float minimum_time_between_warnings = 40 * SECONDS;

    ///The machine's internal radio, used to broadcast alerts.
    // tg: code/game/machinery/bank_machine.dm:21
    public obj_item_radio radio;
    ///The channel we announce a siphon over.
    // tg: code/game/machinery/bank_machine.dm:23
    public string radio_channel = RADIO_CHANNEL_COMMON;

    ///What department to check to link our bank account to.
    // tg: code/game/machinery/bank_machine.dm:26
    public string account_department = ACCOUNT_CAR;
    ///Bank account we're connected to.
    // tg: code/game/machinery/bank_machine.dm:28
    public datum_bank_account synced_bank_account;

    // The type's overrides of inherited vars (the Hand mode, types-converter.md 3.2: the class declares radio)
    // tg: code/game/machinery/bank_machine.dm:1
    public obj_machinery_computer_bank_machine()
    {
        name = "bank machine";
        desc = "A machine used to deposit and withdraw station funds.";
        circuit = typeof(obj_item_circuitboard_computer_bankmachine);
        icon_state = "computer"; // MAP_SWITCH("computer", ...): the game's state
        icon_screen = "vault";
        icon_keyboard = "security_key";
    }

    // tg: code/game/machinery/bank_machine.dm:8  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        req_access = Dm.list(ACCESS_VAULT);
    }

    // tg: code/game/machinery/bank_machine.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        radio = Dm.@new<obj_item_radio>(this);
        radio.subspace_transmission = TRUE;
        radio.canhear_range = 0;
        radio.set_listening(FALSE);
        radio.recalculateChannels();
        synced_bank_account = SSeconomy?.get_dep_account(account_department); // SSeconomy?: a test harness world that makes machines without Master has none (rules 10.6)

        if (!Dm.truthy(mapload))
            Waiting.NoSleep(AddComponent(typeof(datum_component_gps), "Forbidden Cash Signal"));
        return dot;
    }

    // tg: code/game/machinery/bank_machine.dm:42
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref radio);
        synced_bank_account = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/bank_machine.dm:47
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        float value = 0;
        if (tool is obj_item_stack_spacecash inserted_cash)
            value = Dm.num(inserted_cash.value) * Dm.num(inserted_cash.amount);
        else if (tool is obj_item_holochip inserted_holochip)
            value = Dm.num(inserted_holochip.credits);
        else if (tool is obj_item_coin inserted_coin)
            value = Dm.num(inserted_coin.value);
        else if (tool is obj_item_poker_chip inserted_chip)
            value = Dm.num(inserted_chip.get_item_credit_value());
        if (!Dm.truthy(value))
            return NONE;
        if (synced_bank_account != null)
        {
            synced_bank_account.adjust_money(value);
            await say(I18n.T("[] deposited! The [] is now [] [].", MONEY_NAME_CAPITALIZED, synced_bank_account.account_holder, synced_bank_account.account_balance, MONEY_SYMBOL));
        }
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/bank_machine.dm:70
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);
        if (!Dm.truthy(siphoning) || synced_bank_account == null)
            return dot;
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
        {
            Waiting.NoSleep(say(I18n.T("Insufficient power. Halting siphon.")));
            end_siphon();
            return dot;
        }
        float siphon_am = 100 * Dm.num(seconds_per_tick);
        if (!synced_bank_account.has_money(siphon_am))
        {
            Waiting.NoSleep(say(I18n.T("[] depleted. Halting siphon.", synced_bank_account.account_holder)));
            end_siphon();
            return dot;
        }

        playsound(this, "sound/items/poster/poster_being_created.ogg", 100, TRUE);
        syphoning_credits += siphon_am;
        synced_bank_account.adjust_money(-siphon_am);
        if (next_warning < world.time && prob(15))
        {
            var A = get_area(loc as atom) as area;
            string message = I18n.T("[]redit withdrawal underway in [][]", Dm.truthy(unauthorized) ? I18n.T("Unauthorized c") : "C", Dm.initial(A, (area p) => p.name), Dm.truthy(unauthorized) ? "!!" : "...");
            radio.talk_into(this, message, radio_channel);
            next_warning = world.time + minimum_time_between_warnings;
        }
        return dot;
    }

    // tg: code/game/machinery/bank_machine.dm:93
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "BankMachine", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/bank_machine.dm:100
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["current_balance"] = synced_bank_account?.account_balance ?? 0;
        data["siphoning"] = siphoning;
        data["station_name"] = station_name();

        return data;
    }

    // tg: code/game/machinery/bank_machine.dm:109
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "siphon":
                if (Dm.truthy(is_station_level(this.z)) || Dm.truthy(is_centcom_level(this.z)))
                {
                    await say(I18n.T("Siphon of station [] has begun!", MONEY_NAME));
                    start_siphon(ui.user as mob_living_carbon);
                }
                else
                    await say(I18n.T("Error: Console not in reach of station, withdrawal cannot begin."));
                dot = TRUE;
                break;
            case "halt":
                await say(I18n.T("Station [] withdrawal halted.", MONEY_NAME_SINGULAR));
                end_siphon();
                dot = TRUE;
                break;
        }
        return dot;
    }

    // tg: code/game/machinery/bank_machine.dm:127
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (Dm.truthy(siphoning) && !(Dm.truthy(is_station_level(this.z)) || Dm.truthy(is_centcom_level(this.z))))
        {
            await say(I18n.T("Error: Console not in reach of station. Siphon halted."));
            end_siphon();
        }
    }

    // tg: code/game/machinery/bank_machine.dm:133
    public virtual void end_siphon()
    {
        siphoning = FALSE;
        unauthorized = FALSE;
        atom droploc = drop_location() as atom;
        foreach (object cash_typepath in Dm.each(credits_to_spacecash(syphoning_credits)))
            Dm.@new(cash_typepath as Type, droploc);
        syphoning_credits = 0;
    }

    // tg: code/game/machinery/bank_machine.dm:141
    public virtual void start_siphon(mob_living_carbon user = null)
    {
        var card = user.get_idcard(hand_first: TRUE) as obj_item_card_id;
        if (card == null || !Dm.truthy(check_access(card)))
            unauthorized = TRUE;
        else
            unauthorized = FALSE;
        siphoning = TRUE;
    }
}
