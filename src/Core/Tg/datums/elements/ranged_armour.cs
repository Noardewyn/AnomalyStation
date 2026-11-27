// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/ranged_armour.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Reduces or nullifies damage from ranged weaponry with force below a certain value
// tg: code/datums/elements/ranged_armour.dm:2
public partial class datum_element_ranged_armour : datum_element
{
    // tg: code/datums/elements/ranged_armour.dm:16
    [DmParams("target", "minimum_projectile_force", "below_projectile_multiplier", "vulnerable_projectile_types", "minimum_thrown_force", "throw_blocked_message")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        float? minimum_projectile_force = Dm.arg<float?>(__extra, 0);
        float? below_projectile_multiplier = Dm.arg<float?>(__extra, 1);
        DmList vulnerable_projectile_types = Dm.arg<DmList>(__extra, 2);
        float? minimum_thrown_force = Dm.arg<float?>(__extra, 3);
        string throw_blocked_message = Dm.arg<string>(__extra, 4);
        minimum_projectile_force ??= 0;
        below_projectile_multiplier ??= 0;
        vulnerable_projectile_types ??= Dm.list();
        minimum_thrown_force ??= 0;
        throw_blocked_message ??= "bounces off";
        object dot = await base.Attach(target, Dm.forward(__extra, minimum_projectile_force, below_projectile_multiplier, vulnerable_projectile_types, minimum_thrown_force, throw_blocked_message));
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        this.minimum_projectile_force = minimum_projectile_force;
        this.below_projectile_multiplier = below_projectile_multiplier;
        this.vulnerable_projectile_types = vulnerable_projectile_types;
        this.minimum_thrown_force = minimum_thrown_force;
        this.throw_blocked_message = throw_blocked_message;

        if (Dm.num(minimum_projectile_force) > 0)
            RegisterSignal(target, COMSIG_PROJECTILE_PREHIT, nameof(pre_bullet_impact));
        if (Dm.num(minimum_thrown_force) > 0)
            RegisterSignal(target, COMSIG_ATOM_PREHITBY, nameof(pre_thrown_impact));
        return dot;
    }

    // tg: code/datums/elements/ranged_armour.dm:38
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, Dm.list(COMSIG_PROJECTILE_PREHIT, COMSIG_ATOM_PREHITBY));
        return base.Detach(target, __extra);
    }

    /// Modify or ignore bullet damage based on projectile properties
    // tg: code/datums/elements/ranged_armour.dm:43
    [SignalHandler]
    public virtual object pre_bullet_impact(atom parent = null, obj_projectile bullet = null)
    {
        if (Dm.num(bullet.damage) >= Dm.num(minimum_projectile_force) || Dm.@in(bullet.damage_type, vulnerable_projectile_types))
            return null;
        if (Dm.num(below_projectile_multiplier) == 0)
        {
            parent.visible_message(span_danger(Dm.text("[] seems unharmed by []!", parent, bullet)));
            return PROJECTILE_INTERRUPT_HIT;
        }
        bullet.damage = Dm.num(bullet.damage) * Dm.num(below_projectile_multiplier);
        parent.visible_message(span_danger(Dm.text("[] seems resistant to []!", parent, bullet)));
        return null;
    }

    /// Ignore thrown damage based on projectile properties. There's no elegant way to multiply the damage because throwforce is persistent.
    // tg: code/datums/elements/ranged_armour.dm:54
    [SignalHandler]
    public virtual object pre_thrown_impact(atom parent = null, atom hit_atom = null, datum_thrownthing throwingdatum = null)
    {
        if (!isitem(hit_atom) || HAS_TRAIT(hit_atom, TRAIT_BYPASS_RANGED_ARMOR))
            return null;
        if (Dm.num(((obj_item)hit_atom).throwforce) >= Dm.num(minimum_thrown_force))
            return null;
        parent.visible_message(span_danger(Dm.text("[] [] []!", hit_atom, throw_blocked_message, parent)));
        return COMSIG_HIT_PREVENTED;
    }
}
