// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/locked_to_account.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D298): the whole file. The var block is generated (D111).

/// locks a crate so only the person (or department) who bought it can open it
// tg: code/datums/components/locked_to_account.dm:4
public partial class datum_component_locked_to_account : datum_component
{
    // tg: code/datums/components/locked_to_account.dm:1
    private const float DENY_SOUND_COOLDOWN = 2 * SECONDS;

    // tg: code/datums/components/locked_to_account.dm:12
    [DmParams("buyer_account")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!(parent is obj_structure_closet))
            return COMPONENT_INCOMPATIBLE;
        this.buyer_account = Dm.arg(__extra, 0) as datum_bank_account;
        return null;
    }

    /// if AddComponent is called again on this crate, update buyer_account instead of making a duplicate
    // tg: code/datums/components/locked_to_account.dm:19
    public override async DmTask InheritComponent(datum_component new_comp = null, object i_am_original = null, params object[] __extra)
    {
        this.buyer_account = Dm.arg(__extra, 0) as datum_bank_account;
    }

    // tg: code/datums/components/locked_to_account.dm:22
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_EMAG_ACT, nameof(on_emag));
        RegisterSignal(parent, COMSIG_CLOSET_PRE_OPEN, nameof(on_pre_open));
    }

    // tg: code/datums/components/locked_to_account.dm:27
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_ATOM_EMAG_ACT, COMSIG_CLOSET_PRE_OPEN));
        await base.UnregisterFromParent(__extra);
    }

    /// adds a note to the examine text saying its account-locked
    // tg: code/datums/components/locked_to_account.dm:35
    public virtual void on_examine(obj_structure_closet source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_warning(I18n.T("An account lock prevents this from opening except by the purchasing account holder.")));
    }

    /**
     * runs when someone tries to open the crate, checks their ID against buyer_account
     * * force: if TRUE, the open bypasses this lock entirely (e.g. admin or code-forced opens)
     */
    // tg: code/datums/components/locked_to_account.dm:44
    public virtual object on_pre_open(obj_structure_closet source = null, mob_living user = null, object force = null)
    {
        if (Dm.truthy(force))
            return NONE;

        if (user == null)
            return BLOCK_OPEN;

        var id_card = user.get_idcard(TRUE) as obj_item_card_id; // the ID card the user is holding/wearing, used to check their linked bank account
        if (id_card == null)
        {
            deny(source, user, I18n.T("No ID detected!"));
            return BLOCK_OPEN;
        }

        if (id_card.registered_account == null)
        {
            deny(source, user, I18n.T("No linked bank account detected!"));
            return BLOCK_OPEN;
        }

        bool is_department = buyer_account is datum_bank_account_department; // department orders check the user's paycheck department instead of a personal account match.  TRUE if this user is allowed to open the crate
        var dept_account = buyer_account as datum_bank_account_department;
        bool account_matches = is_department
            ? Dm.eq(id_card.registered_account?.account_job?.paycheck_department, dept_account.department_id)
            : id_card.registered_account == buyer_account;

        if (!account_matches)
        {
            deny(source, user, I18n.T("Bank account does not match with buyer!"));
            return BLOCK_OPEN;
        }

        if (iscarbon(user))
            source.add_fingerprint(user);
        remove_lock(source, user);
        return NONE;
    }

    // tg: code/datums/components/locked_to_account.dm:77
    public virtual void deny(obj_structure_closet source = null, mob_living user = null, object message = null)
    {
        to_chat(user, span_warning(Dm.text("[]", message)));
        if (world.time >= deny_cooldown) // COOLDOWN_FINISHED
        {
            playsound(source, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 30, TRUE);
            deny_cooldown = world.time + DENY_SOUND_COOLDOWN; // COOLDOWN_START
        }
    }

    /// emag skips this whole check and goes straight to remove_lock
    // tg: code/datums/components/locked_to_account.dm:84
    public virtual void on_emag(obj_structure_closet source = null, mob emagger = null)
    {
        emagger.balloon_alert(emagger, I18n.T("account lock bypassed"));
        remove_lock(source, emagger);
    }

    /// unlocks the crate  and removes this component
    // tg: code/datums/components/locked_to_account.dm:90
    public virtual void remove_lock(obj_structure_closet source = null, mob user = null)
    {
        source.visible_message(span_notice(I18n.T("[]'s account lock disengages with a soft click.", source)));
        to_chat(user, span_notice(I18n.T("You unlock [].", source)));
        source.update_appearance();
        qdel(this);
    }
}
