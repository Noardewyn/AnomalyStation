// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/alternative_sharpness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Allows items to have different sharpness for right click attacks
// tg: code/datums/components/alternative_sharpness.dm:4
public partial class datum_component_alternative_sharpness : datum_component
{
    // tg: code/datums/components/alternative_sharpness.dm:32
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        obj_item weapon = parent as obj_item;
        this.alt_sharpness = Dm.num(Dm.arg(__extra, 0));
        this.verbs_continuous = Dm.arg(__extra, 1);
        this.verbs_simple = Dm.arg(__extra, 2);
        this.force_mod = Dm.num(Dm.arg(__extra, 3));
        this.required_trait = Dm.arg(__extra, 4);
        this.alt_hitsound = Dm.arg(__extra, 5);
        base_continuous = weapon.attack_verb_continuous;
        base_simple = weapon.attack_verb_simple;
        base_hitsound = weapon.hitsound;
        return null;
    }

    // tg: code/datums/components/alternative_sharpness.dm:47
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_PRE_ATTACK_SECONDARY, nameof(on_secondary_attack));
        RegisterSignal(parent, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
    }

    // tg: code/datums/components/alternative_sharpness.dm:51
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_PRE_ATTACK_SECONDARY, COMSIG_TRANSFORMING_ON_TRANSFORM));
    }

    // tg: code/datums/components/alternative_sharpness.dm:58
    [SignalHandler]
    public virtual void on_secondary_attack(obj_item source = null, atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(alt_attacking) || (Dm.truthy(required_trait) && !HAS_TRAIT(source, required_trait)))
            return;

        alt_attacking = TRUE;
        // MODIFY_ATTACK_FORCE(attack_modifiers, force_mod) (__DEFINES/combat.dm:422)
        if (!islist(attack_modifiers)) { attack_modifiers = Dm.list(); }
        attack_modifiers[FORCE_MODIFIER] = Dm.add(attack_modifiers[FORCE_MODIFIER], force_mod);
        base_sharpness = Dm.num(source.sharpness);
        source.sharpness = alt_sharpness;
        if (verbs_continuous != null)
            source.attack_verb_continuous = verbs_continuous as DmList;
        if (verbs_simple != null)
            source.attack_verb_simple = verbs_simple as DmList;
        if (alt_hitsound != null)
            source.hitsound = alt_hitsound;

        // I absolutely despise this but this is geniunely the best way to do this without creating and hooking up to a dozen signals and still risking failure edge cases
        _addtimer(Dm.@new<datum_callback>(this, nameof(disable_alt_attack)), 1, file: "code/datums/components/alternative_sharpness.dm", line: 72);
    }

    // tg: code/datums/components/alternative_sharpness.dm:74
    public virtual void disable_alt_attack()
    {
        obj_item weapon = parent as obj_item;
        alt_attacking = FALSE;
        weapon.attack_verb_continuous = base_continuous as DmList;
        weapon.attack_verb_simple = base_simple as DmList;
        weapon.sharpness = base_sharpness;
        weapon.hitsound = base_hitsound;
    }

    // If our weapon is transforming, we listen for the transformation to adjust our base_hitsound as needed so we're not caught out by the callback adding inappropriate values.
    // tg: code/datums/components/alternative_sharpness.dm:83
    [SignalHandler]
    public virtual void on_transform(obj_item source = null, mob user = null, object active = null)
    {
        base_continuous = source.attack_verb_continuous;
        base_simple = source.attack_verb_simple;
        base_sharpness = Dm.num(source.sharpness);
        base_hitsound = source.hitsound;
    }
}
