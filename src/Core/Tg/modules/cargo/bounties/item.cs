// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file, the item bounty base and the debug bounty vouchers. The var blocks are generated (D111).

// tg: code/modules/cargo/bounties/item.dm:1
public partial class datum_bounty_item : datum_bounty
{
    // tg: code/modules/cargo/bounties/item.dm:11
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        wanted_types = string_assoc_list(zebra_typecacheof(wanted_types, only_root_path: Dm.truthy(include_subtypes) ? FALSE : TRUE) as DmList) as DmList;
    }

    // tg: code/modules/cargo/bounties/item.dm:15
    public override object print_required() => Dm.text("[]/[]", shipped_count, required_count);

    // tg: code/modules/cargo/bounties/item.dm:18
    public override object can_claim() => shipped_count >= required_count ? TRUE : FALSE;

    // tg: code/modules/cargo/bounties/item.dm:21
    public override object applies_to(obj shipped = null)
    {
        if (!Dm.truthy(is_type_in_typecache(shipped, wanted_types)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(shipped.flags_1, HOLOGRAM_1)))
            return FALSE;
        return shipped_count < required_count ? TRUE : FALSE;
    }

    // tg: code/modules/cargo/bounties/item.dm:28
    public override async DmTask<object> ship(obj shipped = null)
    {
        if (!Dm.truthy(applies_to(shipped)))
            return FALSE;
        if (shipped is obj_item_stack shipped_is_a_stack)
            shipped_count += Dm.num(shipped_is_a_stack.amount);
        else
            shipped_count += 1;
        return TRUE;
    }

    // tg: code/modules/cargo/bounties/item.dm:38
    public override object get_total() => shipped_count;

    // tg: code/modules/cargo/bounties/item.dm:41
    public override object get_max() => required_count;
}

/**
 * Debug item because it took less time to code this than it did to roll ONE toolbox bounty.
 */
// tg: code/modules/cargo/bounties/item.dm:48
public partial class obj_item_bounty_voucher : obj_item
{
    // tg: code/modules/cargo/bounties/item.dm:54
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (user is not mob_living living_user)
            return dot;
        var id = living_user.get_idcard() as obj_item_card_id;
        if (id?.registered_account == null)
            return dot;
        object choice = await tgui_input_list(living_user, I18n.T("Choose a bounty."), I18n.T("New Bounty"), subtypesof(typeof(datum_bounty)) as DmList);
        Type new_chore = text2path(Dm.text("[]", choice));
        id.registered_account.set_bounty(Dm.@new(new_chore) as datum_bounty, id);
        balloon_alert(user, I18n.T("new bounty acquired!"));
        playsound(this, Dm.resource("sound/effects/coin2.ogg"), 30, TRUE);
        qdel(this);
        return dot;
    }
}

/// As above, but it spawns a global bounty for testing.
// tg: code/modules/cargo/bounties/item.dm:72
public partial class obj_item_bounty_voucher_stationwide : obj_item_bounty_voucher
{
    // tg: code/modules/cargo/bounties/item.dm:77
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (user is not mob_living living_user)
            return dot;
        object choice = await tgui_input_list(living_user, I18n.T("Choose a bounty."), I18n.T("New Bounty"), subtypesof(typeof(datum_bounty)) as DmList);
        Type new_chore = text2path(Dm.text("[]", choice));
        if (Dm.truthy(Dm.initial(new_chore, (datum_bounty p) => p.global_exempt)))
        {
            to_chat(user, span_warning(I18n.T("Can't use that one, try another!")));
            return dot;
        }
        Dm.append(ref GLOB.shared_crew_bounties, new_chore);
        balloon_alert(user, I18n.T("new bounty provided to the crew!"));
        playsound(this, Dm.resource("sound/effects/coin2.ogg"), 30, TRUE);
        qdel(this);
        return dot;
    }
}
