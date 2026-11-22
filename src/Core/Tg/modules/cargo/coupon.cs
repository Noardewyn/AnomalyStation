// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/coupon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D298): the whole file. The coupon declares discount_pct_off as object (it holds a fraction or COUPON_OMEN, rules 3.2), so its
// var block is hand-written. A cursed coupon lays the omen on its holder (gaps H1 ported the omen); the heart attack of a second
// cursed coupon comes as tg's.

///datum used by the Coupon Master PDA app to generate coupon items redeemed by a bank account.
// tg: code/modules/cargo/coupon.dm:2
public partial class datum_coupon_code : datum
{
    // tg: code/modules/cargo/coupon.dm:19
    [DmParams("discount", "discounted_pack", "expires_in")]
    public override async DmTask New(params object[] __extra)
    {
        object discount = Dm.arg(__extra, 0);
        object discounted_pack = Dm.arg(__extra, 1);
        object expires_in = Dm.arg(__extra, 2);
        await base.New();
        this.discounted_pack = discounted_pack as Type;
        this.discount = discount;
        if (Dm.truthy(expires_in))
            this.expires_in = Dm.world.time + Dm.num(expires_in);
    }

    // tg: code/modules/cargo/coupon.dm:26
    public override object Destroy(float? force = null)
    {
        if (associated_account != null)
        {
            Dm.remove(ref associated_account.redeemed_coupons, this);
            associated_account = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/cargo/coupon.dm:32
    public virtual void copy(datum_bank_account account = null)
    {
        var copy = Dm.@new<datum_coupon_code>(discount, discounted_pack, expires_in);
        copy.associated_account = account;
        if (account != null)
            LAZYADD(ref account.redeemed_coupons, this);
        if (Dm.truthy(expires_in))
        {
            // QDEL_IN_STOPPABLE(copy, expires_in - world.time) (__DEFINES/qdel.dm), expanded inline
            float time = Dm.num(expires_in) - Dm.world.time;
            copy.timerid = _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), time > GC_FILTER_QUEUE ? WEAKREF(copy) : copy), time, TIMER_STOPPABLE, file: "code/modules/cargo/coupon.dm", line: 38);
        }
    }

    // tg: code/modules/cargo/coupon.dm:40
    public virtual obj_item_coupon generate()
    {
        var coupon = Dm.@new<obj_item_coupon>();
        Waiting.NoSleep(coupon.generate(discount, discounted_pack));
        printed = TRUE;
        deltimer(timerid);
        timerid = null;
        return coupon;
    }
}

// tg: code/modules/cargo/coupon.dm:48
public partial class obj_item_coupon : obj_item
{
    // tg: code/modules/cargo/coupon.dm:55
    public Type discounted_pack;
    // tg: code/modules/cargo/coupon.dm:56  object (rules 3.2): a fraction, or COUPON_OMEN
    public object discount_pct_off = 0.05f;
    // tg: code/modules/cargo/coupon.dm:57
    public obj_machinery_computer_cargo inserted_console;

    // tg: code/modules/cargo/coupon.dm:49
    public obj_item_coupon()
    {
        name = "coupon";
        desc = "It doesn't matter if you didn't want it before, what matters now is that you've got a coupon for it!";
        icon_state = "data_1";
        icon = Dm.resource("icons/obj/card.dmi");
        item_flags = NOBLUDGEON;
        w_class = WEIGHT_CLASS_TINY;
    }

    // tg: code/modules/cargo/coupon.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (discounted_pack != null)
            update_name();
        return dot;
    }

    /// Choose what our prize is :D
    // tg: code/modules/cargo/coupon.dm:66
    public virtual async DmTask<object> generate(object discount = null, object discounted_pack = null, mob user = null)
    {
        this.discounted_pack = (discounted_pack ?? pick(GLOB.discountable_packs[pick_weight(GLOB.pack_discount_odds)] as DmList)) as Type;
        DmList chances = TgState.Current.obj_item_coupon__generate__chances ??= Dm.list(Dm.kv("0.10", 4), Dm.kv("0.15", 8), Dm.kv("0.20", 10), Dm.kv("0.25", 8), Dm.kv("0.50", 4), Dm.kv(COUPON_OMEN, 1));
        discount_pct_off = discount ?? pick_weight(chances);

        if (!Dm.eq(discount_pct_off, COUPON_OMEN))
        {
            if (discount == null) // the discount arg should be a number already, while the keys in the chances list cannot be numbers
            {
                discount_pct_off = text2num(discount_pct_off);
                update_name();
            }
            return null;
        }

        name = I18n.T("coupon - fuck you");
        desc = I18n.T("The small text reads, 'You will be slaughtered'... That doesn't sound right, does it?");

        object cursed = (object)user ?? loc;
        if (cursed is not mob cursed_mob)
            return FALSE;

        to_chat(cursed_mob, span_warning(I18n.T("The coupon reads '<b>fuck you</b>' in large, bold text... is- is that a prize, or?")));

        if (cursed_mob.GetComponent(typeof(datum_component_omen)) == null)
        {
            Waiting.NoSleep(cursed_mob.AddComponent(typeof(datum_component_omen), this, 1));
            return TRUE;
        }
        if (HAS_TRAIT(cursed_mob, TRAIT_CURSED))
            to_chat(cursed_mob, span_warning(I18n.T("What a horrible night... To have a curse!")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(curse_heart), cursed_mob), 5 * SECONDS, TIMER_UNIQUE | TIMER_STOPPABLE, file: "code/modules/cargo/coupon.dm", line: 88);
        return null;
    }

    // tg: code/modules/cargo/coupon.dm:91
    public override object update_name(float? updates = null)
    {
        name = I18n.T("coupon - []% off []", round(Dm.num(discount_pct_off) * 100), Dm.initial(discounted_pack, (datum_supply_pack p) => p.name));
        return base.update_name(updates);
    }

    /// Play stupid games, win stupid prizes
    // tg: code/modules/cargo/coupon.dm:96
    public virtual async DmTask<object> curse_heart(mob_living cursed = null)
    {
        if (!iscarbon(cursed))
        {
            await cursed.gib(DROP_ALL_REMAINS);
            await burn_evilly();
            return TRUE;
        }

        var player = (mob_living_carbon)cursed;
        Waiting.Detach(player.emote("scream")); // INVOKE_ASYNC
        to_chat(player, span_mind_control(I18n.T("What could that coupon mean?")));
        to_chat(player, span_userdanger(I18n.T("...The suspense is killing you!")));
        player.set_heartattack(status: TRUE);
        await burn_evilly();
        return null;
    }

    // tg: code/modules/cargo/coupon.dm:110
    public virtual async DmTask burn_evilly()
    {
        visible_message(span_warning(I18n.T("[] burns up in a sinister flash, taking an evil energy with it...", this)));
        await burn__proc();
    }

    // tg: code/modules/cargo/coupon.dm:114
    public override async DmTask<object> attack_atom(atom attacked_atom = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (attacked_atom is not obj_machinery_computer_cargo console)
            return await base.attack_atom(attacked_atom, user, modifiers, attack_modifiers);
        if (Dm.eq(discount_pct_off, COUPON_OMEN))
        {
            to_chat(user, span_warning(I18n.T("\\The [] validates the coupon as authentic, but refuses to accept it...", attacked_atom)));
            await console.say(I18n.T("Coupon fulfillment already in progress..."));
            return null;
        }

        inserted_console = console;
        LAZYADD(ref inserted_console.loaded_coupons, this);
        await inserted_console.say(I18n.T("Coupon for [] applied!", Dm.initial(discounted_pack, (datum_supply_pack p) => p.name)));
        await forceMove(inserted_console);
        return null;
    }

    // tg: code/modules/cargo/coupon.dm:127
    public override object Destroy(float? force = null)
    {
        if (inserted_console != null)
        {
            LAZYREMOVE(ref inserted_console.loaded_coupons, this);
            inserted_console = null;
        }
        return base.Destroy(force);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/cargo/coupon.dm:68  var/static/list/chances
    internal DmList obj_item_coupon__generate__chances;
}
