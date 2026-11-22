// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/hulk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file but its tail swing (gaps P3, D327): the hulk, its green limbs, shouting speech, smashing punches, cold weakness and loss in
// crit, and its wizardly, superhuman and ork kinds. The tail swing (check_swing, setup_swing, swing_loop and finish_swing, :97-251)
// is "based in part on the wrestling swing code ported from goon" (hulk.dm:157): Goonstation code is not taken (D80), so its four procs are
// declared with not-translated bodies (the type stays live) and on_acquiring does not register it (not-translated section 165). The var blocks are generated (D111).

//Hulk turns your skin green, makes you strong, and allows you to shrug off stun effect.
// tg: code/datums/mutations/hulk.dm:2
public partial class datum_mutation_hulk : datum_mutation
{
    // tg: code/datums/mutations/hulk.dm:26
    [DmParams("copymut")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        await add_speechmod();
    }

    // tg: code/datums/mutations/hulk.dm:30
    public virtual async DmTask add_speechmod()
    {
        await AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", Dm.list(Dm.kv(".", "!"))), Dm.named("end_string", "!!"), Dm.named("uppercase", TRUE));
    }

    // tg: code/datums/mutations/hulk.dm:33
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(owner.get_bodyparts()))
        {
            if (Dm.truthy(Dm.bitand(part.bodytype, BODYTYPE_ORGANIC)))
                part.add_color_override(bodypart_color, LIMB_COLOR_HULK);
        }
        await owner.update_body_parts();
        await owner.add_mood_event("hulk", typeof(datum_mood_event_hulk));
        RegisterSignal(owner, COMSIG_LIVING_EARLY_UNARMED_ATTACK, nameof(on_attack_hand));
        // RegisterSignal(owner, COMSIG_MOB_CLICKON, PROC_REF(check_swing)): the Goon-derived tail swing, not taken (D80)
        RegisterSignal(owner, COMSIG_CARBON_ATTACH_LIMB, nameof(texture_limb));
        RegisterSignal(owner, COMSIG_CARBON_REMOVE_LIMB, nameof(untexture_limb));
        owner.add_movespeed_mod_immunities("hulk", typeof(datum_movespeed_modifier_damage_slowdown));
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_COLD, 2); //hulks are vulnerable to cold temperatures for some reason, idk low body fat?
        return dot;
    }

    // tg: code/datums/mutations/hulk.dm:49
    [SignalHandler]
    public virtual object on_attack_hand(mob_living_carbon_human source = null, atom target = null, object proximity = null, object modifiers = null)
    {
        if (!Dm.truthy(source.combat_mode) || !Dm.truthy(proximity) || Dm.truthy(LAZYACCESS(modifiers as DmList, RIGHT_CLICK)))
            return NONE;
        if (!Dm.truthy(source.can_unarmed_attack()))
            return COMPONENT_SKIP_ATTACK;
        if (!Dm.truthy(Waiting.NoSleep(target.attack_hulk(owner))))
            return NONE;

        if (world.time > (last_scream + scream_delay))
        {
            last_scream = world.time;
            Waiting.Detach(scream_attack(source)); // INVOKE_ASYNC
        }
        log_combat(source, target, "punched", "hulk powers");
        Waiting.NoSleep(source.do_attack_animation(target, ATTACK_EFFECT_SMASH));
        source.changeNext_move(CLICK_CD_MELEE);
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/mutations/hulk.dm:67
    public virtual async DmTask scream_attack(mob_living_carbon_human source = null)
    {
        await source.say("WAAAAAAAAAAAAAAGH!", forced: "hulk");
    }

    // tg: code/datums/mutations/hulk.dm:70
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (Dm.num(owner.health) < Dm.num(owner.crit_threshold))
        {
            await on_losing(owner);
            to_chat(owner, span_danger(I18n.T("You suddenly feel very weak.")));
            qdel(this);
        }
    }

    // tg: code/datums/mutations/hulk.dm:76
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(owner.get_bodyparts()))
            part.remove_color_override(LIMB_COLOR_HULK);
        await owner.update_body_parts();
        owner.clear_mood_event("hulk");
        UnregisterSignal(owner, COMSIG_LIVING_EARLY_UNARMED_ATTACK);
        UnregisterSignal(owner, COMSIG_MOB_CLICKON);
        owner.remove_movespeed_mod_immunities("hulk", typeof(datum_movespeed_modifier_damage_slowdown));
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_COLD, 0.5f);
        return dot;
    }

    // tg: code/datums/mutations/hulk.dm:88
    [SignalHandler]
    public virtual void texture_limb(atom source = null, obj_item_bodypart limb = null)
    {
        if (Dm.truthy(Dm.bitand(limb.bodytype, BODYTYPE_ORGANIC)))
            limb.add_color_override(bodypart_color, LIMB_COLOR_HULK);
    }

    // tg: code/datums/mutations/hulk.dm:93
    [SignalHandler]
    public virtual void untexture_limb(atom source = null, obj_item_bodypart limb = null)
    {
        limb.remove_color_override(LIMB_COLOR_HULK);
    }

    // The Goon-derived tail swing (hulk.dm:97-251, D80): declared so the hulk's type is live, never registered or called; the
    // bodies are not taken.
    // tg: code/datums/mutations/hulk.dm:101
    [SignalHandler]
    public virtual object check_swing(mob_living_carbon_human user = null, atom clicked_atom = null, DmList modifiers = null)
    {
        Dm.NotTranslated("code/datums/mutations/hulk.dm:101", "check_swing (Goonstation-derived, D80)");
        return null;
    }

    // tg: code/datums/mutations/hulk.dm:127
    public virtual void setup_swing(mob_living_carbon_human the_hulk = null, mob_living_carbon yeeted_person = null)
    {
        Dm.NotTranslated("code/datums/mutations/hulk.dm:127", "setup_swing (Goonstation-derived, D80)");
    }

    // tg: code/datums/mutations/hulk.dm:161
    public virtual void swing_loop(mob_living_carbon_human the_hulk = null, mob_living_carbon yeeted_person = null, object step = null, object original_dir = null)
    {
        Dm.NotTranslated("code/datums/mutations/hulk.dm:161", "swing_loop (Goonstation-derived, D80)");
    }

    // tg: code/datums/mutations/hulk.dm:232
    public virtual void finish_swing(mob_living_carbon_human the_hulk = null, mob_living_carbon yeeted_person = null, object original_dir = null)
    {
        Dm.NotTranslated("code/datums/mutations/hulk.dm:232", "finish_swing (Goonstation-derived, D80)");
    }
}

// tg: code/datums/mutations/hulk.dm:266
public partial class datum_mutation_hulk_superhuman : datum_mutation_hulk
{
    // tg: code/datums/mutations/hulk.dm:282
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await DmTask.CompletedTask;
    }
}

// tg: code/datums/mutations/hulk.dm:285
public partial class datum_mutation_hulk_ork : datum_mutation_hulk
{
    // tg: code/datums/mutations/hulk.dm:292
    public override async DmTask add_speechmod()
    {
        await AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", strings("ork_replacement.json", "ork")), Dm.named("end_string", "!!"), Dm.named("uppercase", TRUE));
    }
}
