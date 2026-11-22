// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/coupon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D298): the whole file, the Coupon Master app and its NtosCouponMaster window. The var block is generated (D111).

///A program that enables the user to redeem randomly generated coupons for several cargo packs (mostly goodies).
// tg: code/modules/modular_computers/file_system/programs/coupon.dm:5
public partial class datum_computer_file_program_coupon : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/coupon.dm:1-2  (file-local, #undef at :126-127)
    private const float COUPON_PAPER_USE = 1;
    private const float COUPON_TONER_USE = 0.250f;

    // tg: code/modules/modular_computers/file_system/programs/coupon.dm:17
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        ///set the discount_coupons list, which means SSmodular_computers will now begin to periodically produce new coupon codes.
        SSmodular_computers.discount_coupons ??= Dm.list(); // LAZYINITLIST
        ADD_TRAIT(computer, TRAIT_MODPC_HALVED_DOWNLOAD_SPEED, REF(this)); //All that glitters is not gold
    }

    // tg: code/modules/modular_computers/file_system/programs/coupon.dm:23
    public override object Destroy(float? force = null)
    {
        if (computer != null)
            REMOVE_TRAIT(computer, TRAIT_MODPC_HALVED_DOWNLOAD_SPEED, REF(this));
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/coupon.dm:28
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList printed_coupons = Dm.list();
        DmList redeemed_coupons = Dm.list();
        data["valid_id"] = FALSE;
        obj_item_card_id user_id = computer.stored_id;
        if (Dm.truthy(user_id?.registered_account?.add_to_accounts))
        {
            foreach (datum_coupon_code coupon in Dm.each_cast<datum_coupon_code>(user_id.registered_account.redeemed_coupons))
            {
                DmList coupon_data = Dm.list(
                    Dm.kv("goody", Dm.initial(coupon.discounted_pack, (datum_supply_pack p) => p.name)),
                    Dm.kv("discount", Dm.num(coupon.discount) * 100));
                if (Dm.truthy(coupon.printed))
                    Dm.append(ref printed_coupons, Dm.list(coupon_data));
                else
                    Dm.append(ref redeemed_coupons, Dm.list(coupon_data));
            }
            data["valid_id"] = TRUE;
        }
        data["printed_coupons"] = printed_coupons;
        data["redeemed_coupons"] = redeemed_coupons;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/coupon.dm:48
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        obj_item_card_id user_id = computer.stored_id;
        if (!Dm.truthy(user_id?.registered_account?.add_to_accounts))
            return TRUE;
        switch (Dm.str(action))
        {
            case "redeem":
            {
                object code = @params["code"];
                if (!Dm.truthy(length(code)))
                    return TRUE;
                var coupon = SSmodular_computers.discount_coupons?[code] as datum_coupon_code;
                if (coupon == null)
                {
                    await user_id.registered_account.bank_card_talk(I18n.T("Invalid coupon code."), TRUE);
                    return TRUE;
                }
                if (Dm.truthy(coupon.expires_in) && Dm.num(coupon.expires_in) < Dm.world.time)
                {
                    await user_id.registered_account.bank_card_talk(I18n.T("Expired coupon code."), TRUE);
                    return TRUE;
                }
                if (Dm.@in(coupon, user_id.registered_account.redeemed_coupons))
                {
                    await user_id.registered_account.bank_card_talk(I18n.T("Coupon [] already redeemed.", code), TRUE);
                    return TRUE;
                }
                coupon.copy(user_id.registered_account);
                DmList goodbye = TgState.Current.datum_computer_file_program_coupon__ui_act__goodbye ??= Dm.list(
                    "Have a wonderful day.",
                    "Don't forget to print it.",
                    "Time to get shopping!",
                    "Enjoy your discount!",
                    "Congratulations!",
                    "Bye Bye~.");
                await user_id.registered_account.bank_card_talk(I18n.T("Coupon [] redeemed. []", code, I18n.T(Dm.str(pick(goodbye)))), TRUE);
                //Well, guess you're redeeming something else too.
                if (prob(40) && computer.used_capacity < computer.max_capacity)
                {
                    var warez = Dm.@new<datum_computer_file>();
                    warez.filename = Dm.str(random_string(rand(6, 12), Dm.add(Dm.add(GLOB.alphabet, GLOB.alphabet_upper), GLOB.numerals) as DmList));
                    warez.filetype = Dm.str(pick("DAT", "XXX", "TMP", "FILE", "MNT", "MINER", "SYS", "PNG.EXE"));
                    warez.size = Math.Min(rand(1, 4), computer.max_capacity - computer.used_capacity);
                    if (prob(25))
                        warez.undeletable = TRUE;
                    computer.store_file(warez);
                }
                break;
            }
        }
        return dot;
    }

    /**
     * Normally, modular PCs can be print paper already, but I find this additional step
     * to be less lazy and fitting to the "I gotta go print it before it expires" aspect of it.
     */
    // tg: code/modules/modular_computers/file_system/programs/coupon.dm:94
    public override async DmTask<object> tap(atom tapped_atom = null, mob_living user = null, DmList modifiers = null)
    {
        if (tapped_atom is not obj_machinery_photocopier copier)
            return FALSE;
        obj_item_card_id user_id = computer.stored_id;
        if (user_id?.registered_account == null)
        {
            computer.balloon_alert(user, I18n.T("no bank account found!"));
            return TRUE;
        }
        if (Dm.truthy(copier.check_busy(user)))
            return TRUE;
        int num_coupons = 0;
        foreach (datum_coupon_code coupon in Dm.each_cast<datum_coupon_code>(user_id.registered_account.redeemed_coupons))
        {
            if (!Dm.truthy(coupon.printed))
                num_coupons++;
        }
        if (num_coupons == 0)
        {
            computer.balloon_alert(user, I18n.T("no coupon available!"));
            return TRUE;
        }
        copier.do_copies(Dm.@new<datum_callback>(this, nameof(print_coupon), user_id.registered_account), user, COUPON_PAPER_USE, COUPON_TONER_USE, num_coupons);
        return TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/coupon.dm:116
    public virtual object print_coupon(datum_bank_account account = null)
    {
        datum_coupon_code coupon = null;
        foreach (datum_coupon_code possible_coupon in Dm.each_cast<datum_coupon_code>(account.redeemed_coupons))
        {
            if (!Dm.truthy(possible_coupon.printed))
            {
                coupon = possible_coupon;
                break;
            }
        }
        return coupon?.generate();
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/file_system/programs/coupon.dm:72  var/static/list/goodbye
    internal DmList datum_computer_file_program_coupon__ui_act__goodbye;
}
