// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/dedbot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

///////////////Donk Exenteration Drone - DED////////////
//A patrolling bot that cuts you up if you get close. Use ranged weapons or avoid it.

// tg: code/modules/mob/living/basic/bots/dedbot.dm:6
public partial class mob_living_basic_bot_dedbot : mob_living_basic_bot
{
    // tg: code/modules/mob/living/basic/bots/dedbot.dm:43
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_effect_gibspawner_robot)));
        grant_actions_by_list(Dm.list(Dm.kv(typeof(datum_action_cooldown_mob_cooldown_exenterate), BB_DEDBOT_SLASH)));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/bots/dedbot.dm:66
public partial class datum_action_cooldown_mob_cooldown_exenterate : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/bots/dedbot.dm:82
    private static DmList valid_targets => TgState.Current.datum_action_cooldown_mob_cooldown_exenterate__valid_targets ??= Dm.list(
        Dm.kv(BODY_ZONE_CHEST, 2),
        Dm.kv(BODY_ZONE_R_ARM, 1),
        Dm.kv(BODY_ZONE_L_ARM, 1),
        Dm.kv(BODY_ZONE_R_LEG, 1),
        Dm.kv(BODY_ZONE_L_LEG, 1));

    // tg: code/modules/mob/living/basic/bots/dedbot.dm:90
    public override async DmTask<object> Activate(atom target = null)
    {
        atom caster = target;
        if (!(cooldown_time <= world.time)) // COOLDOWN_FINISHED(src, cooldown_time): tg's own quirk, it reads the duration as an end time
            return FALSE;
        caster.Shake(1.4f, 0.8f, 0.3f * SECONDS);
        caster.visible_message(span_danger(Dm.text("[] shakes violently!", caster)));
        playsound(caster, Dm.resource("sound/items/weapons/drill.ogg"), 120, TRUE);
        await slash_em(caster);
        StartCooldown(cooldown_time);
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/dedbot.dm:99
    public virtual async DmTask slash_em(atom caster = null)
    {
        foreach (mob_living victim in Dm.each<mob_living>(range(ability_range, caster)))
        {
            if (Dm.truthy(victim.has_faction(immune_factions)) && Dm.truthy(await victim.IsReachableBy(owner)))
                continue;
            to_chat(caster, span_warning(Dm.text("You slice []!", victim)));
            to_chat(victim, span_warning(Dm.text("You are cut by []'s blades!", caster)));
            await victim.apply_damage(damage: damage_dealt, damagetype: BRUTE, def_zone: pick(valid_targets), sharpness: SHARP_EDGED);
        }
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList datum_action_cooldown_mob_cooldown_exenterate__valid_targets;
}
