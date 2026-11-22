// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/on_hit_effect.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4b (D327): the whole file (the venus human trap's lifesteal relies on it; the venomous element's poison, gaps C4, now reaches
// its victims too). The var block is generated (D111).

/**
 * ## On Hit Effect Component!
 *
 * Component for other elements/components to rely on for on-hit effects without duplicating the on-hit code.
 * See Lifesteal, or bane for examples.
 */
// tg: code/datums/elements/on_hit_effect.dm:7
public partial class datum_element_on_hit_effect : datum_element
{
    // tg: code/datums/elements/on_hit_effect.dm:9
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!HAS_TRAIT(target, TRAIT_ON_HIT_EFFECT))
            stack_trace(Dm.text("[] added to [] without adding TRAIT_ON_HIT_EFFECT first. Please use AddElementTrait instead.", type, target), "code/datums/elements/on_hit_effect.dm", 12);
        if (ismachinery(target) || isstructure(target) || isgun(target) || isprojectilespell(target))
            RegisterSignal(target, COMSIG_PROJECTILE_ON_HIT, nameof(on_projectile_hit));
        else if (isitem(target))
            RegisterSignal(target, COMSIG_ITEM_AFTERATTACK, nameof(item_afterattack));
        else if (isanimal_or_basicmob(target))
            RegisterSignal(target, COMSIG_HOSTILE_POST_ATTACKINGTARGET, nameof(hostile_attackingtarget));
        else if (ishuman(target))
            RegisterSignal(target, COMSIG_HUMAN_PUNCHED, nameof(human_punched));
        else if (isprojectile(target))
            RegisterSignal(target, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(on_projectile_self_hit));
        else
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_MOVABLE_IMPACT, nameof(on_thrown_hit));
        return dot;
    }

    // tg: code/datums/elements/on_hit_effect.dm:28
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(
            COMSIG_PROJECTILE_ON_HIT,
            COMSIG_ITEM_AFTERATTACK,
            COMSIG_HOSTILE_POST_ATTACKINGTARGET,
            COMSIG_PROJECTILE_SELF_ON_HIT,
            COMSIG_MOVABLE_IMPACT
        ));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/on_hit_effect.dm:38
    [SignalHandler]
    public virtual void item_afterattack(obj_item source = null, atom target = null, mob user = null, DmList modifiers = null)
    {
        on_hit(source, user, target, user.zone_selected);
    }

    // tg: code/datums/elements/on_hit_effect.dm:43
    [SignalHandler]
    public virtual void hostile_attackingtarget(mob_living attacker = null, atom target = null, object success = null)
    {
        if (!Dm.truthy(success))
            return;

        on_hit(attacker, attacker, target, attacker.zone_selected);
    }

    // tg: code/datums/elements/on_hit_effect.dm:52
    [SignalHandler]
    public virtual void human_punched(mob_living_carbon_human attacker = null, mob_living_carbon_human target = null, object damage = null, object damage_type = null, object atk_effect = null, obj_item_bodypart hit = null, params object[] __extra)
    {
        on_hit(attacker, attacker, target, hit.body_zone);
    }

    // tg: code/datums/elements/on_hit_effect.dm:57
    [SignalHandler]
    public virtual void on_projectile_hit(datum fired_from = null, atom_movable firer = null, atom target = null, object angle = null, object body_zone = null)
    {
        // on_hit's slot types its source as an atom, but a projectile spell (a datum) fires too: what on_hit does with it, inline
        if (fired_from is not atom)
        {
            fired_from.SEND_SIGNAL(COMSIG_ON_HIT_EFFECT, Dm.list(fired_from, firer, target, body_zone, FALSE));
            return;
        }
        on_hit(fired_from as atom, firer, target, body_zone);
    }

    // tg: code/datums/elements/on_hit_effect.dm:61
    [SignalHandler]
    public virtual void on_projectile_self_hit(datum source = null, mob firer = null, atom target = null, object angle = null, object body_zone = null)
    {
        on_hit(source as atom, firer, target, body_zone);
    }

    // tg: code/datums/elements/on_hit_effect.dm:65
    [SignalHandler]
    public virtual void on_thrown_hit(datum source = null, atom hit_atom = null, datum_thrownthing throwingdatum = null)
    {
        on_hit(source as atom, source as atom_movable, hit_atom, null, TRUE);
    }

    // tg: code/datums/elements/on_hit_effect.dm:69
    public virtual void on_hit(atom source = null, atom_movable attacker = null, atom target = null, object body_zone = null, float? throw_hit = null)
    {
        throw_hit ??= FALSE;
        source.SEND_SIGNAL(COMSIG_ON_HIT_EFFECT, Dm.list(source, attacker, target, body_zone, throw_hit));
    }
}
