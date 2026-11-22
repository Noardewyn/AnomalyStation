// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/networks/modular_computers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: NtOS): the network log, the NTNet store's software lists, the chat channels' lookup and the photos'
// names; the Coupon Master's discount drops with gaps P4b (D298). The var block is generated (D111).

// tg: code/controllers/subsystem/networks/modular_computers.dm:2  #define MAX_LOG_COUNT 300 (file-local)
// tg: code/controllers/subsystem/networks/modular_computers.dm:4 SUBSYSTEM_DEF(modular_computers)
public partial class datum_controller_subsystem_modular_computers : datum_controller_subsystem
{
    private const float MAX_LOG_COUNT = 300;

    // The New() that SUBSYSTEM_DEF(modular_computers) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSmodular_computers)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/networks/modular_computers.dm:4
    public override async DmTask New(params object[] __extra)
    {
        if (SSmodular_computers != this)
        {
            if (SSmodular_computers is datum_controller_subsystem_modular_computers)
            {
                Recover();
                qdel(SSmodular_computers);
            }
            SSmodular_computers = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/networks/modular_computers.dm:32
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        build_software_lists();
        initialized = TRUE;
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/networks/modular_computers.dm:37
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (Dm.truthy(discount_coupons) && Dm.num(world.time) >= next_discount)
            announce_coupon();
    }

    ///Generate new coupon codes that can be redeemed with the Coupon Master App
    // tg: code/controllers/subsystem/networks/modular_computers.dm:42
    public virtual void announce_coupon()
    {
        //If there's no way to announce the coupon, we may as well skip it.
        obj_machinery_announcement_system announcement_system = get_announcement_system();
        if (announcement_system == null)
            return;

        DmList discounts = TgState.Current.datum_controller_subsystem_modular_computers__announce_coupon__discounts ??= Dm.list(Dm.kv("0.10", 7), Dm.kv("0.15", 16), Dm.kv("0.20", 20), Dm.kv("0.25", 16), Dm.kv("0.50", 8), Dm.kv("0.66", 1));
        DmList flash_discounts = TgState.Current.datum_controller_subsystem_modular_computers__announce_coupon__flash_discounts ??= Dm.list(Dm.kv("0.30", 3), Dm.kv("0.40", 8), Dm.kv("0.50", 8), Dm.kv("0.66", 2), Dm.kv("0.75", 1));
        ///Eliminates non-alphanumeric characters, as well as the word "Single-Pack" or "Pack" or "Crate" from the coupon code
        regex strip_pack_name = TgState.Current.datum_controller_subsystem_modular_computers__announce_coupon__strip_pack_name ??= regex("[^a-zA-Z0-9]|(Single-)?Pack|Crate", "g");

        var discounted_pack = (Type)pick(GLOB.discountable_packs[pick_weight(GLOB.pack_discount_odds)] as DmList);
        object pack_name = Dm.initial(discounted_pack, (datum_supply_pack p) => p.name);
        float chosen_discount;
        float expires_in = 0;
        if (prob(75))
        {
            chosen_discount = Dm.num(text2num(pick_weight(discounts)));
            if (prob(20))
                expires_in = rand(8, 10) * MINUTES;
        }
        else
        {
            chosen_discount = Dm.num(text2num(pick_weight(flash_discounts)));
            expires_in = rand(2, 4) * MINUTES;
        }
        string coupon_code = Dm.text("[][]", uppertext(strip_pack_name.Replace(pack_name, "")), chosen_discount * 100);

        DmList targets = Dm.list();
        foreach (object messenger_ref in Dm.each(GLOB.pda_messengers))
        {
            var messenger = GLOB.pda_messengers[messenger_ref] as datum_computer_file_program_messenger;
            if (Dm.locate<datum_computer_file_program_coupon>(messenger?.computer.stored_files) != null)
                Dm.append(ref targets, messenger);
        }

        ///Don't go any further if the same coupon code has been done alrady or if there's no recipient for the 'promo'.
        if (Dm.@in(coupon_code, discount_coupons) || !Dm.truthy(length(targets)))
            return;

        var coupon = Dm.@new<datum_coupon_code>(chosen_discount, discounted_pack, expires_in);

        discount_coupons[coupon_code] = coupon;

        ///pda message code here
        DmList promo_messages = TgState.Current.datum_controller_subsystem_modular_computers__announce_coupon__promo_messages ??= Dm.list(
            "A new discount has dropped for %GOODY: %DISCOUNT.",
            "Check this new offer out: %GOODY, now %DISCOUNT off.",
            "Now on sales: %GOODY, at %DISCOUNT discount!",
            "This item is now on sale (%DISCOUNT off): %GOODY.",
            "Would you look at that! A %DISCOUNT discount on %GOODY!",
            "Exclusive offer for %GOODY. Only %DISCOUNT! Get it now:",
            "%GOODY is now %DISCOUNT off.",
            "*RING* A new discount has dropped: %GOODY, %DISCOUNT off.",
            "%GOODY - %DISCOUNT off.");
        DmList code_messages = TgState.Current.datum_controller_subsystem_modular_computers__announce_coupon__code_messages ??= Dm.list(
            "Here's the code",
            "Use this code to redeem it",
            "Open the app to redeem it",
            "Code",
            "Redeem it now",
            "Buy it now");

        object chosen_promo_message = replacetext(replacetext(I18n.T(Dm.str(pick(promo_messages))), "%GOODY", pack_name), "%DISCOUNT", Dm.text("[]%", chosen_discount * 100));
        var signal = Dm.@new<datum_signal_subspace_messaging_tablet_message>(announcement_system, Dm.list(
            Dm.kv("fakename", "Coupon Master"),
            Dm.kv("fakejob", "Goodies Promotion"),
            Dm.kv("message", Dm.text("[] []: [][].", chosen_promo_message, I18n.T(Dm.str(pick(code_messages))), coupon_code, expires_in > 0 ? I18n.T(" (EXPIRES IN [])", uppertext(DisplayTimeText(expires_in))) : "")),
            Dm.kv("targets", targets),
            Dm.kv("automated", TRUE)));

        signal.send_to_receivers();

        next_discount = Dm.num(world.time) + rand(3, 5) * MINUTES;
    }

    ///Finds all downloadable programs and adds them to their respective downloadable list.
    // tg: code/controllers/subsystem/networks/modular_computers.dm:116
    public virtual void build_software_lists()
    {
        foreach (Type prog_type in Dm.each_cast<Type>(subtypesof(typeof(datum_computer_file_program))))
        {
            // Has no TGUI file so is not meant to be a downloadable thing.
            if (!Dm.truthy(Dm.initial(prog_type, (datum_computer_file_program p) => p.tgui_id)) || !Dm.truthy(Dm.initial(prog_type, (datum_computer_file_program p) => p.filename)))
                continue;
            datum_computer_file_program prog = Dm.@new(prog_type) as datum_computer_file_program;

            if (Dm.truthy(Dm.bitand(prog.program_flags, PROGRAM_ON_NTNET_STORE)))
                available_station_software.Add(prog);
            if (Dm.truthy(Dm.bitand(prog.program_flags, PROGRAM_ON_SYNDINET_STORE)))
                available_antag_software.Add(prog);
        }
    }

    ///Attempts to find a new file through searching the available stores with its name.
    // tg: code/controllers/subsystem/networks/modular_computers.dm:129
    public virtual datum_computer_file_program find_ntnet_file_by_name(object filename = null)
    {
        foreach (datum_computer_file_program programs in Dm.each<datum_computer_file_program>(Dm.add(available_station_software, available_antag_software)))
        {
            if (Dm.eq(filename, programs.filename))
                return programs;
        }
        return null;
    }

    ///Attempts to find a chatorom using the ID of the channel.
    // tg: code/controllers/subsystem/networks/modular_computers.dm:136
    public virtual datum_ntnet_conversation get_chat_channel_by_id(object id = null)
    {
        foreach (datum_ntnet_conversation chan in Dm.each<datum_ntnet_conversation>(chat_channels))
        {
            if (Dm.eq(chan.id, id))
                return chan;
        }
        return null;
    }

    /**
     * Records a message into the station logging system for the network
     * Arguments:
     * * log_string - The message being logged
     */
    // tg: code/controllers/subsystem/networks/modular_computers.dm:147
    public virtual void add_log(object log_string = null)
    {
        DmList log_text = Dm.list();
        Dm.append(ref log_text, Dm.text("\\[[]\\]", round_timestamp()));
        Dm.append(ref log_text, "*SYSTEM* - ");
        Dm.append(ref log_text, log_string);
        log_string = log_text.Join("");

        modpc_logs.Add(log_string);

        // We have too many logs, remove the oldest entries until we get into the limit
        if (modpc_logs.len > MAX_LOG_COUNT)
            modpc_logs = modpc_logs.Copy(modpc_logs.len - MAX_LOG_COUNT, 0);
    }

    /**
     * Removes all station logs and leaves it with an alert that it's been wiped.
     */
    // tg: code/controllers/subsystem/networks/modular_computers.dm:163
    public virtual void purge_logs()
    {
        modpc_logs = Dm.list();
        add_log("-!- LOGS DELETED BY SYSTEM OPERATOR -!-");
    }

    /**
     * Returns a name which a /datum/picture can be assigned to.
     * Use this function to get asset names and to avoid cache duplicates/overwriting.
     */
    // tg: code/controllers/subsystem/networks/modular_computers.dm:171
    public virtual string get_next_picture_name()
    {
        float next_uid = next_picture_id;
        next_picture_id++;
        return Dm.text("ntos_picture_[].png", next_uid);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/networks/modular_computers.dm:4  SUBSYSTEM_DEF(modular_computers): GLOBAL_REAL(SSmodular_computers, /datum/controller/subsystem/modular_computers)
    internal datum_controller_subsystem_modular_computers SSmodular_computers;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_modular_computers SSmodular_computers => ref TgState.Current.SSmodular_computers;
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/networks/modular_computers.dm:48-51, :82, :93  the static lists and regex of announce_coupon
    internal DmList datum_controller_subsystem_modular_computers__announce_coupon__discounts;
    internal DmList datum_controller_subsystem_modular_computers__announce_coupon__flash_discounts;
    internal regex datum_controller_subsystem_modular_computers__announce_coupon__strip_pack_name;
    internal DmList datum_controller_subsystem_modular_computers__announce_coupon__promo_messages;
    internal DmList datum_controller_subsystem_modular_computers__announce_coupon__code_messages;
}
