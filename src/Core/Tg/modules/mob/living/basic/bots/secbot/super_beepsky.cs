// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/secbot/super_beepsky.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:1
public partial class mob_living_basic_bot_secbot_grievous : mob_living_basic_bot_secbot
{
    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:17
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(weapon, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_weapon_transform));
        grant_actions_by_list(Dm.list(Dm.kv(typeof(datum_action_cooldown_mob_cooldown_bot_sword), null)));
        Waiting.Detach(weapon.attack_self(this));
        RegisterSignal(this, COMSIG_ATOM_PRE_BULLET_ACT, nameof(block_bullets));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:28
    public override async DmTask<object> check_block(atom hit_by = null, object damage = null, string attack_text = null, float? attack_type = null, float? armour_penetration = null, string damage_type = null, params object[] __extra)
    {
        object dot = await base.check_block(hit_by, damage, attack_text, attack_type, armour_penetration, damage_type, __extra);
        if (Dm.truthy(Dm.bitand(dot, FAILED_BLOCK)))
            return dot;

        return Dm.truthy(sword_active) && prob(block_chance) ? SUCCESSFUL_BLOCK : FAILED_BLOCK;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:35
    [SignalHandler]
    public virtual void on_weapon_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (Dm.truthy(active))
            visible_message(span_warning(Dm.text("[] ignites his energy swords!", this)));
        sword_active = Dm.num(active);
        Waiting.NoSleep(update_icon_state());
    }

    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:42
    public override async DmTask add_arrest_component() //i dont think we'll be arresting people...
    {
    }

    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:45
    [SignalHandler]
    public virtual object block_bullets(datum source = null, obj_projectile hitting_projectile = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(this))
            return NONE;

        if (!Dm.truthy(sword_active))
            return NONE;

        visible_message(span_warning(Dm.text("[] deflects [] with its energy swords!", source, hitting_projectile)));
        playsound(source as atom, Dm.resource("sound/items/weapons/blade1.ogg"), 50, TRUE);
        return COMPONENT_BULLET_BLOCKED;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:58
    public override void on_entered(datum source = null, atom_movable movable_target = null)
    {
        base.on_entered(source, movable_target);
        if (!ismob(movable_target) || Dm.eq(Dm.num(!Dm.truthy((ai_controller as datum_ai_controller).blackboard[BB_CURRENT_TARGET])), movable_target))
            return;
        visible_message(span_warning(Dm.text("[] flails his swords and cuts []!", this, movable_target)));
        playsound(this, Dm.resource("sound/mobs/non-humanoids/beepsky/beepskyspinsabre.ogg"), 100, TRUE, -1);
        Waiting.Detach(ClickOn(movable_target));
    }

    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:67
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);

        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(sword_active) ? "-c" : "");
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:72
    public override object Destroy(float? force = null)
    {
        qdel(weapon);
        weapon = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:76
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;
        Waiting.Detach(weapon.melee_attack_chain(this, target));
        playsound(this, Dm.resource("sound/items/weapons/blade1.ogg"), 50, TRUE, -1);
        return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:84
    public override async DmTask explode()
    {
        atom drop_location = this.drop_location() as atom;
        //Parent is dropping the weapon, so let's drop 3 more to make up for it.
        for (int i = 1; i <= 3; i++)
            await drop_part(baton_type, drop_location);

        await base.explode();
    }
}

// tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:93
public partial class mob_living_basic_bot_secbot_grievous_toy : mob_living_basic_bot_secbot_grievous
{
    // tg: code/modules/mob/living/basic/bots/secbot/super_beepsky.dm:101
    public override object block_bullets(datum source = null, obj_projectile hitting_projectile = null)
    {
        return NONE;
    }
}
