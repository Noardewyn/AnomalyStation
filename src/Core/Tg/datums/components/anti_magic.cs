// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/anti_magic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// This provides different types of magic resistance on an object
// tg: code/datums/components/anti_magic.dm:2
public partial class datum_component_anti_magic : datum_component
{
    // tg: code/datums/components/anti_magic.dm:39
    [DmParams("antimagic_flags", "charges", "inventory_flags", "block_magic", "expiration", "check_blocking")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object antimagic_flags = Dm.arg(__extra, 0) ?? MAGIC_RESISTANCE;
        object charges = Dm.arg(__extra, 1) ?? INFINITY;
        object inventory_flags = Dm.arg(__extra, 2) ?? ALL;
        object block_magic = Dm.arg(__extra, 3);
        object expiration = Dm.arg(__extra, 4);
        object check_blocking = Dm.arg(__extra, 5);

        atom_movable movable = parent as atom_movable;
        if (movable == null)
            return COMPONENT_INCOMPATIBLE;

        bool compatible = false;
        if (isitem(movable))
        {
            RegisterSignal(movable, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
            RegisterSignal(movable, COMSIG_ITEM_DROPPED, nameof(on_drop));
            RegisterSignal(movable, COMSIG_ATOM_EXAMINE_TAGS, nameof(get_examine_tags));
            RegisterSignals(movable, Dm.list(COMSIG_ITEM_ATTACK, COMSIG_ITEM_ATTACK_ATOM), nameof(on_attack));
            compatible = true;
        }
        else if (ismob(movable))
        {
            register_antimagic_signals(movable);
            compatible = true;
        }

        if (Dm.truthy(movable.can_buckle))
        {
            RegisterSignal(movable, COMSIG_MOVABLE_BUCKLE, nameof(on_buckle));
            RegisterSignal(movable, COMSIG_MOVABLE_UNBUCKLE, nameof(on_unbuckle));
            compatible = true;
        }

        if (!compatible)
            return COMPONENT_INCOMPATIBLE;

        this.antimagic_flags = Dm.num(antimagic_flags);
        this.charges = Dm.num(charges);
        this.inventory_flags = Dm.num(inventory_flags);
        this.block_magic = block_magic as datum_callback;
        this.expiration = expiration as datum_callback;
        this.check_blocking = check_blocking as datum_callback;
        return null;
    }

    // tg: code/datums/components/anti_magic.dm:78
    public override object Destroy(float? force = null)
    {
        block_magic = null;
        expiration = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/anti_magic.dm:83
    public virtual void register_antimagic_signals(datum on_what = null)
    {
        RegisterSignal(on_what, COMSIG_MOB_RECEIVE_MAGIC, nameof(block_receiving_magic), @override: TRUE);
        RegisterSignal(on_what, COMSIG_MOB_RESTRICT_MAGIC, nameof(restrict_casting_magic), @override: TRUE);
    }

    // tg: code/datums/components/anti_magic.dm:87
    public virtual void unregister_antimagic_signals(datum on_what = null)
    {
        UnregisterSignal(on_what, Dm.list(COMSIG_MOB_RECEIVE_MAGIC, COMSIG_MOB_RESTRICT_MAGIC));
    }

    // tg: code/datums/components/anti_magic.dm:90
    [SignalHandler]
    public virtual void on_buckle(atom_movable source = null, mob_living bucklee = null)
    {
        register_antimagic_signals(bucklee);
    }

    // tg: code/datums/components/anti_magic.dm:94
    [SignalHandler]
    public virtual void on_unbuckle(atom_movable source = null, mob_living bucklee = null)
    {
        unregister_antimagic_signals(bucklee);
    }

    // tg: code/datums/components/anti_magic.dm:98
    [SignalHandler]
    public virtual void get_examine_tags(atom source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.eq(antimagic_flags, ALL_MAGIC_RESISTANCE))
        {
            examine_list["magic-proof"] = I18n.T("It is thoroughly shielded against all known forms of magic.");
            return;
        }

        if (Dm.truthy(Dm.bitand(antimagic_flags, MAGIC_RESISTANCE)))
            examine_list["warded"] = I18n.T("It possesses a general resistance to regular spells and magic.");
        if (Dm.truthy(Dm.bitand(antimagic_flags, MAGIC_RESISTANCE_MIND)))
            examine_list["telepathy-proof"] = I18n.T("It appears to be insulated against telepathic or mental influence.");
        if (Dm.truthy(Dm.bitand(antimagic_flags, MAGIC_RESISTANCE_HOLY)))
            examine_list["blessed"] = I18n.T("It is protected by a divine shield against unholy and dark forms of magic.");
    }

    // tg: code/datums/components/anti_magic.dm:112
    [SignalHandler]
    public virtual void on_equip(atom_movable source = null, mob equipper = null, object slot = null)
    {
        if (!Dm.truthy(Dm.bitand(inventory_flags, slot))) //Check that the slot is valid for antimagic
        {
            unregister_antimagic_signals(equipper);
            return;
        }

        register_antimagic_signals(equipper);
        if (!Dm.truthy(alert_caster_on_equip))
            return;

        // Check to see if we have any spells that are blocked due to antimagic
        foreach (datum_action_cooldown_spell magic_spell in Dm.each<datum_action_cooldown_spell>(equipper.actions))
        {
            if (!Dm.truthy(Dm.bitand(magic_spell.spell_requirements, SPELL_REQUIRES_NO_ANTIMAGIC)))
                continue;

            if (!Dm.truthy(Dm.bitand(antimagic_flags, magic_spell.antimagic_flags)))
                continue;

            to_chat(equipper, span_warning(I18n.T("[] is interfering with your ability to cast magic!", parent)));
            alert_caster_on_equip = FALSE;
            break;
        }
    }

    // tg: code/datums/components/anti_magic.dm:135
    [SignalHandler]
    public virtual void on_drop(atom_movable source = null, mob user = null)
    {
        // Reset alert
        if (source.loc != user)
            alert_caster_on_equip = TRUE;
        unregister_antimagic_signals(user);
    }

    // tg: code/datums/components/anti_magic.dm:143
    [SignalHandler]
    public virtual object block_receiving_magic(mob_living_carbon source = null, object casted_magic_flags = null, object charge_cost = null, DmList antimagic_sources = null)
    {
        if (check_blocking != null && !Dm.truthy(Waiting.NoSleep(check_blocking.Invoke())))
            return NONE;

        // We do not block this type of magic, good day
        if (!Dm.truthy(Dm.bitand(casted_magic_flags, antimagic_flags)))
            return NONE;

        // We have already blocked this spell
        if (Dm.@in(parent, antimagic_sources))
            return NONE;

        // Block success! Add this parent to the list of antimagic sources
        Dm.append(ref antimagic_sources, parent);

        if (block_magic != null)
            Waiting.NoSleep(block_magic.Invoke(source, parent));
        if (Dm.num(charges) != INFINITY && Dm.num(charge_cost) > 0)
        {
            charges = Dm.num(charges) - Dm.num(charge_cost);
            if (Dm.num(charges) <= 0)
            {
                if (expiration != null)
                    Waiting.NoSleep(expiration.Invoke(source, parent));
                qdel(this); // no more antimagic
            }
        }

        return COMPONENT_MAGIC_BLOCKED;
    }

    /// cannot cast magic with the same type of antimagic present
    // tg: code/datums/components/anti_magic.dm:170
    [SignalHandler]
    public virtual object restrict_casting_magic(mob user = null, object magic_flags = null)
    {
        if (Dm.truthy(Dm.bitand(magic_flags, antimagic_flags)))
        {
            if (HAS_TRAIT(user, TRAIT_ANTIMAGIC_NO_SELFBLOCK)) // this trait bypasses magic casting restrictions
                return NONE;
            return COMPONENT_MAGIC_BLOCKED;
        }

        return NONE;
    }

    // tg: code/datums/components/anti_magic.dm:180
    [SignalHandler]
    public virtual void on_attack(atom_movable source = null, atom target = null, mob user = null)
    {
        if (Dm.truthy(target?._listen_lookup?[COMSIG_ATOM_HOLYATTACK]))
            target._SendSignal(COMSIG_ATOM_HOLYATTACK, Dm.list(target, source, user, antimagic_flags));
    }
}
