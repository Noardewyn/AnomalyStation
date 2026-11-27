// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/adjust_fishing_difficulty.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: what is worn, held or buckled to makes the fishing minigame easier or harder, and says so to those who know fish.
// The var block is generated (D111).

///Influences the difficulty of the minigame when worn or if buckled to.
// tg: code/datums/elements/adjust_fishing_difficulty.dm:2
public partial class datum_element_adjust_fishing_difficulty : datum_element
{
    // tg: code/datums/elements/adjust_fishing_difficulty.dm:10
    [DmParams("target", "modifier", "slots")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object modifier = Dm.arg(__extra, 0);
        object slots = Dm.arg(__extra, 1) ?? NONE;
        object dot = await base.Attach(target, __extra);
        if (!ismovable(target) || !Dm.truthy(modifier))
            return COMPONENT_INCOMPATIBLE;

        if (!isitem(target))
        {
            atom_movable movable_target = (atom_movable)target;
            if (!Dm.truthy(movable_target.can_buckle))
                return COMPONENT_INCOMPATIBLE;

            RegisterSignal(target, COMSIG_MOVABLE_BUCKLE, nameof(on_buckle), @override: TRUE);
            RegisterSignal(target, COMSIG_MOVABLE_UNBUCKLE, nameof(on_unbuckle), @override: TRUE);
            RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_buckle_examine), @override: TRUE);
        }
        else
        {
            RegisterSignal(target, COMSIG_ITEM_EQUIPPED, nameof(on_equipped), @override: TRUE);
            RegisterSignal(target, COMSIG_ITEM_DROPPED, nameof(on_dropped), @override: TRUE);
            RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_item_examine), @override: TRUE);
        }

        this.modifier = modifier;
        this.slots = slots;

        await update_check((atom_movable)target);
        return dot;
    }

    // tg: code/datums/elements/adjust_fishing_difficulty.dm:34
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(
            COMSIG_ATOM_EXAMINE,
            COMSIG_MOVABLE_BUCKLE,
            COMSIG_MOVABLE_UNBUCKLE,
            COMSIG_ITEM_EQUIPPED,
            COMSIG_ITEM_DROPPED));

        Waiting.NoSleep(update_check((atom_movable)source, TRUE));
        return dot;
    }

    // tg: code/datums/elements/adjust_fishing_difficulty.dm:46
    public virtual async DmTask update_check(atom_movable movable_source = null, float? removing = null)
    {
        removing ??= FALSE;
        foreach (mob_living buckled_mob in Dm.each<mob_living>(movable_source.buckled_mobs))
            await update_user(buckled_mob, movable_source as obj, removing);
        if (!isitem(movable_source) || !isliving(movable_source.loc))
            return;
        mob_living holder = (mob_living)movable_source.loc;
        obj_item item = (obj_item)movable_source;
        if (Dm.truthy(Dm.bitand(holder.get_slot_by_item(item), Dm.truthy(slots) ? slots : item.slot_flags)))
            await update_user(holder, item, removing);
    }

    // tg: code/datums/elements/adjust_fishing_difficulty.dm:56
    [SignalHandler]
    public virtual void on_item_examine(obj_item item = null, mob user = null, DmList examine_text = null)
    {
        if (!HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISH))
            return;
        object method = Dm.truthy(Dm.bitand(Dm.truthy(slots) ? slots : item.slot_flags, ITEM_SLOT_HANDS))
            ? I18n.T("Holding []", item.p_them())
            : I18n.T("Wearing []", item.p_them());
        add_examine_line(user, examine_text, method);
    }

    // tg: code/datums/elements/adjust_fishing_difficulty.dm:63
    [SignalHandler]
    public virtual void on_buckle_examine(atom_movable source = null, mob user = null, DmList examine_text = null)
    {
        if (!HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISH))
            return;
        add_examine_line(user, examine_text, I18n.T("Buckling to []", source.p_them()));
    }

    // tg: code/datums/elements/adjust_fishing_difficulty.dm:69
    public virtual void add_examine_line(mob user = null, DmList examine_text = null, object method = null)
    {
        string percent = HAS_MIND_TRAIT(user, TRAIT_EXAMINE_DEEPER_FISH) ? Dm.text("[]% ", abs(Dm.num(modifier))) : "";
        string text = Dm.num(modifier) < 0
            ? I18n.T("[] will make fishing []easier.", method, percent)
            : I18n.T("[] will make fishing []harder.", method, percent);
        if (Dm.num(modifier) < 0)
            Dm.append(examine_text, span_nicegreen(text));
        else
            Dm.append(examine_text, span_danger(text));
    }

    // tg: code/datums/elements/adjust_fishing_difficulty.dm:77
    [SignalHandler]
    public virtual void on_buckle(atom_movable source = null, mob_living buckled_mob = null, object forced = null)
    {
        Waiting.NoSleep(update_user(buckled_mob, source as obj));
    }

    // tg: code/datums/elements/adjust_fishing_difficulty.dm:81
    [SignalHandler]
    public virtual void on_unbuckle(atom_movable source = null, mob_living buckled_mob = null, object forced = null)
    {
        Waiting.NoSleep(update_user(buckled_mob, source as obj, TRUE));
    }

    // tg: code/datums/elements/adjust_fishing_difficulty.dm:85
    [SignalHandler]
    public virtual void on_equipped(obj_item source = null, mob_living wearer = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, Dm.truthy(slots) ? slots : source.slot_flags)))
            Waiting.NoSleep(update_user(wearer, source));
    }

    // tg: code/datums/elements/adjust_fishing_difficulty.dm:90
    [SignalHandler]
    public virtual void on_dropped(obj_item source = null, mob_living dropper = null)
    {
        Waiting.NoSleep(update_user(dropper, source, TRUE));
    }

    /// Updates the user's tracked current fishing modifiers
    // tg: code/datums/elements/adjust_fishing_difficulty.dm:95
    public virtual async DmTask update_user(mob_living user = null, obj source_item = null, float? removing = null)
    {
        removing ??= FALSE;
        if (Dm.truthy(removing))
            LAZYREMOVE(ref user.fishing_difficulty_mods_by_source, source_item.type);
        else
            LAZYSET(ref user.fishing_difficulty_mods_by_source, source_item.type, modifier);

        // Are we currently in a fishing challenge? If so, update that challenge as well
        datum_fishing_challenge challenge = GLOB.fishing_challenges_by_user[user] as datum_fishing_challenge;
        if (challenge != null)
            await challenge.update_difficulty();
    }
}
