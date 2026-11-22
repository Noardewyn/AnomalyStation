// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/bane.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the file whole, for the serrated shovel's bonus against the organic.

// tg: code/datums/components/bane.dm:11
public partial class datum_component_bane : datum_component
{
    // tg: code/datums/components/bane.dm:31
    [DmParams("damage_multiplier", "added_damage", "affected_biotypes", "should_bane_callback", "pre_bane_callback", "on_bane_callback", "label_text")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isitem(parent) && !isprojectile(parent))
            return COMPONENT_INCOMPATIBLE;

        damage_multiplier = Dm.num(Dm.arg(__extra, 0) ?? 1f);
        added_damage = Dm.num(Dm.arg(__extra, 1) ?? 0f);
        affected_biotypes = Dm.num(Dm.arg(__extra, 2) ?? NONE);
        should_bane_callback = Dm.arg<datum_callback>(__extra, 3);
        pre_bane_callback = Dm.arg<datum_callback>(__extra, 4);
        on_bane_callback = Dm.arg<datum_callback>(__extra, 5);
        label_text = (Dm.arg(__extra, 6) as string) ?? "";
        return null;
    }

    // tg: code/datums/components/bane.dm:51
    public override async DmTask RegisterWithParent()
    {
        if (isitem(parent))
        {
            RegisterSignal(parent, COMSIG_ITEM_WEAPON_LABEL_READOUT, nameof(label_readout));
            RegisterSignal(parent, COMSIG_ITEM_PRE_ATTACK, nameof(pre_attack));
            RegisterSignal(parent, COMSIG_ITEM_AFTERATTACK, nameof(after_attack));
            RegisterSignal(parent, COMSIG_MOVABLE_IMPACT_ZONE, nameof(on_thrown_hit));
        }
        if (isprojectile(parent))
            RegisterSignal(parent, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(projectile_hit));
    }

    // tg: code/datums/components/bane.dm:60
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        if (isitem(parent))
        {
            UnregisterSignal(parent, COMSIG_ITEM_WEAPON_LABEL_READOUT);
            UnregisterSignal(parent, COMSIG_ITEM_PRE_ATTACK);
            UnregisterSignal(parent, COMSIG_ITEM_AFTERATTACK);
            UnregisterSignal(parent, COMSIG_MOVABLE_IMPACT_ZONE);
        }
        if (isprojectile(parent))
            UnregisterSignal(parent, COMSIG_PROJECTILE_SELF_ON_HIT);
    }

    // tg: code/datums/components/bane.dm:69
    public virtual object is_bane_target(atom target = null)
    {
        if (!isliving(target))
            return FALSE;

        mob_living living_target = target as mob_living;
        if (Dm.truthy(affected_biotypes) && !Dm.truthy(Dm.bitand(living_target.mob_biotypes, affected_biotypes)))
            return FALSE;

        return should_bane_callback == null ? TRUE : Waiting.NoSleep(should_bane_callback.Invoke(target));
    }

    // tg: code/datums/components/bane.dm:79
    public virtual void label_readout(obj_item source = null, DmList readout = null)
    {
        if (damage_multiplier == 1 && added_damage == 0 && !Dm.truthy(label_text))
            return;

        string label_line = "";
        if (damage_multiplier > 1 || added_damage > 0)
            label_line += "It is especially effective against";
        else if (damage_multiplier < 1 || added_damage < 0)
            label_line += "It is less effective against";
        else
            label_line += "It interacts uniquely with"; // for custom behaviors?

        label_line += " ";
        if (Dm.truthy(label_text))
            label_line += label_text;
        else if (Dm.truthy(affected_biotypes))
        {
            DmList affected_biotypes_readable = bitfield_to_list(affected_biotypes, TgState.Current.datum_component_bane__MOB_BIOTYPES_READABLE) as DmList;
            label_line += Dm.text("[] enemies", english_list(affected_biotypes_readable));
        }
        else
            label_line += "certain enemies";

        if (damage_multiplier > 1 || added_damage > 0)
        {
            string magnitude = "";
            float biggest = max(damage_multiplier, added_damage / 10f);
            if (biggest >= 3) magnitude = "massively increased";
            else if (biggest >= 2) magnitude = "greatly increased";
            else if (biggest >= 1.5f) magnitude = "significantly increased";
            else if (biggest >= 1) magnitude = "slightly increased";

            label_line += Dm.text(", dealing [] damage per hit", span_warning(magnitude));
        }
        else if (damage_multiplier < 1 || added_damage < 0)
        {
            string magnitude = "";
            float smallest = min(damage_multiplier, 10f / (Dm.truthy(abs(added_damage)) ? abs(added_damage) : 1));
            if (smallest <= 0) magnitude = "no";
            else if (smallest <= 0.3f) magnitude = "massively reduced";
            else if (smallest <= 0.6f) magnitude = "greatly reduced";
            else if (smallest <= 0.9f) magnitude = "significantly reduced";
            else if (smallest <= 1) magnitude = "slightly reduced";

            label_line += Dm.text(", dealing [] damage per hit", span_warning(magnitude));
        }

        label_line += ".";
        Dm.append(ref readout, label_line);
    }

    // Item attack handling
    // tg: code/datums/components/bane.dm:136
    public virtual void pre_attack(datum source = null, atom target = null, mob_living attacker = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!Dm.truthy(is_bane_target(target)))
            return;

        if (added_damage != 0)
            attack_modifiers[FORCE_MODIFIER] = Dm.num(attack_modifiers[FORCE_MODIFIER]) + added_damage; // MODIFY_ATTACK_FORCE
        if (damage_multiplier != 1)
        { // MODIFY_ATTACK_FORCE_MULTIPLIER
            if (!Dm.@in(FORCE_MULTIPLIER, attack_modifiers)) { attack_modifiers[FORCE_MULTIPLIER] = 1; }
            attack_modifiers[FORCE_MULTIPLIER] = Dm.mul(attack_modifiers[FORCE_MULTIPLIER], damage_multiplier);
        }

        if (pre_bane_callback != null) Waiting.NoSleep(pre_bane_callback.Invoke(target, attacker, attack_modifiers));
    }

    // tg: code/datums/components/bane.dm:149
    public virtual void after_attack(datum source = null, atom target = null, mob_living attacker = null, params object[] __extra)
    {
        if (!Dm.truthy(is_bane_target(target)))
            return;

        if (Dm.truthy(target._listen_lookup?[COMSIG_LIVING_BANED]))
            target._SendSignal(COMSIG_LIVING_BANED, Dm.list(target, source, attacker));
        if (on_bane_callback != null) Waiting.NoSleep(on_bane_callback.Invoke(target, attacker));
    }

    // Throw impact handling
    // tg: code/datums/components/bane.dm:159
    public virtual void on_thrown_hit(datum source = null, atom hit_atom = null, object hit_zone = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        if (!Dm.truthy(is_bane_target(hit_atom)))
            return;

        mob thrower = throwingdatum?.get_thrower() as mob;
        DmList damage_modifiers = Dm.list(Dm.kv(Dm.text("[]", FORCE_MULTIPLIER), damage_multiplier), Dm.kv(Dm.text("[]", FORCE_MODIFIER), added_damage));
        if (pre_bane_callback != null) Waiting.NoSleep(pre_bane_callback.Invoke(hit_atom, thrower, damage_modifiers));

        // We're not modifying the throwforce of the item we're just applying more damage as a separate damage event
        // That's why we do damage_multiplier - 1 (so that a 1.5x multiplier would apply 0.5x damage here for a total of 1.5x)
        obj_item throwing_item = parent as obj_item;
        float extra_damage = (Dm.num(throwing_item.throwforce) * max(0, Dm.num(damage_modifiers[FORCE_MULTIPLIER]) - 1)) + Dm.num(damage_modifiers[FORCE_MODIFIER]);
        if (extra_damage > 0)
        {
            mob_living living_target = hit_atom as mob_living; // safe assertion from is_bane_target
            Waiting.Detach(living_target.apply_damage(extra_damage, throwing_item.damtype, hit_zone, Dm.num(blocked)));
        }

        if (Dm.truthy(hit_atom._listen_lookup?[COMSIG_LIVING_BANED]))
            hit_atom._SendSignal(COMSIG_LIVING_BANED, Dm.list(hit_atom, thrower, hit_atom));
        if (on_bane_callback != null) Waiting.NoSleep(on_bane_callback.Invoke(hit_atom, thrower));
    }

    // Projectile hit handling
    // tg: code/datums/components/bane.dm:181
    public virtual void projectile_hit(datum source = null, atom firer = null, atom target = null, object angle = null, object hit_zone = null, object blocked = null, params object[] __extra)
    {
        if (!Dm.truthy(is_bane_target(target)))
            return;

        DmList damage_modifiers = Dm.list(Dm.kv(Dm.text("[]", FORCE_MULTIPLIER), damage_multiplier), Dm.kv(Dm.text("[]", FORCE_MODIFIER), added_damage));
        if (pre_bane_callback != null) Waiting.NoSleep(pre_bane_callback.Invoke(target, firer, damage_modifiers));

        // We're not modifying the projectile damage we're just applying more damage as a separate damage event
        // That's why we do damage_multiplier - 1 (so that a 1.5x multiplier would apply 0.5x damage here for a total of 1.5x)
        obj_projectile projectile_owner = parent as obj_projectile;
        float extra_damage = (Dm.num(projectile_owner.damage) * max(0, Dm.num(damage_modifiers[FORCE_MULTIPLIER]) - 1)) + Dm.num(damage_modifiers[FORCE_MODIFIER]);
        if (extra_damage > 0)
        {
            mob_living living_target = target as mob_living; // safe assertion from is_bane_target
            Waiting.Detach(living_target.apply_damage(extra_damage, projectile_owner.damage_type, hit_zone, Dm.num(blocked)));
        }

        if (Dm.truthy(target._listen_lookup?[COMSIG_LIVING_BANED]))
            target._SendSignal(COMSIG_LIVING_BANED, Dm.list(target, firer, target));
        if (on_bane_callback != null) Waiting.NoSleep(on_bane_callback.Invoke(target, firer));
    }
}

public sealed partial class TgState
{
    // tg: code/__DEFINES/mobs.dm:112  #define MOB_BIOTYPES_READABLE list(...)
    internal DmList datum_component_bane__MOB_BIOTYPES_READABLE = Dm.list("organic", "mineral", "robotic", "undead", "humanoid", "insectoid", "beast", "monstrous", "reptile", "spirit", "plant", "slime", "aquatic", "mining", "crustacean", "skeletal");
}
