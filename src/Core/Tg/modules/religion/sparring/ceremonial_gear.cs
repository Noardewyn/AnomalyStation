// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/sparring/ceremonial_gear.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the ceremonial blade, weak outside a sparring match and ten times stronger (and blocking ten times better) in one.
// The var block is generated (D111).

///ritual weapons. they're really bad, but they become normal weapons when sparring.
// tg: code/modules/religion/sparring/ceremonial_gear.dm:2
public partial class obj_item_ceremonial_blade : obj_item
{
    // tg: code/modules/religion/sparring/ceremonial_gear.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple));
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering),
            Dm.named("speed", 4 * SECONDS),
            Dm.named("effectiveness", 105)));
        RegisterSignal(this, COMSIG_ITEM_SHARPEN_ACT, nameof(block_sharpening));
        return dot;
    }

    // tg: code/modules/religion/sparring/ceremonial_gear.dm:50
    public override async DmTask<object> melee_attack_chain(mob user = null, atom target = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!HAS_TRAIT(target, TRAIT_SPARRING))
            return await base.melee_attack_chain(user, target, modifiers, attack_modifiers);
        object dot = null;
        float old_force = force;
        float old_throwforce = throwforce;
        force *= 10;
        throwforce *= 10;
        dot = await base.melee_attack_chain(user, target, modifiers, attack_modifiers);
        force = old_force;
        throwforce = old_throwforce;
        return dot;
    }

    // tg: code/modules/religion/sparring/ceremonial_gear.dm:61
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (!Dm.eq(attack_type, MELEE_ATTACK) || !ishuman(hitby.loc))
            return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
        if (HAS_TRAIT(hitby.loc, TRAIT_SPARRING))
        {
            //becomes 30 block
            final_block_chance *= 10;
        }
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }

    // tg: code/modules/religion/sparring/ceremonial_gear.dm:69
    [SignalHandler]
    public virtual object block_sharpening(datum source = null, object increment = null, object max = null)
    {
        //this breaks it
        return COMPONENT_BLOCK_SHARPEN_BLOCKED;
    }
}
