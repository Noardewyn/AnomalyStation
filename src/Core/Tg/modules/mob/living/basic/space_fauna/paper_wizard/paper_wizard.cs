// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:2
public partial class mob_living_basic_paper_wizard : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        apply_dynamic_human_appearance(this, mob_spawn_path: typeof(obj_effect_mob_spawn_corpse_human_wizard_paper));
        grant_abilities();
        grant_loot();
        Waiting.NoSleep(AddElement(typeof(datum_element_effect_trail), typeof(obj_effect_temp_visual_paper_scatter)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:39
    public override object get_unconscious_appearance()
    {
        return get_generic_humanoid_static_appearance();
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:42
    public virtual void grant_abilities()
    {
        grant_actions_by_list(Dm.list(
            Dm.kv(typeof(datum_action_cooldown_spell_conjure_wizard_summon_minions), BB_WIZARD_SUMMON_MINIONS),
            Dm.kv(typeof(datum_action_cooldown_spell_pointed_wizard_mimic), BB_WIZARD_MIMICS)));
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:50
    public virtual void grant_loot()
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_effect_temp_visual_paperwiz_dying)));
    }
}

// tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:66
public partial class mob_living_basic_paper_wizard_copy : mob_living_basic_paper_wizard
{
    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:75
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_relay_attackers)));
        RegisterSignal(this, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:80
    public override void grant_abilities()
    {
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:83
    public override void grant_loot()
    {
    }

    //Hit a fake? eat pain!
    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:87
    [SignalHandler]
    public virtual void on_attacked(mob source = null, mob_living attacker = null, object attack_flags = null)
    {
        if (!Dm.truthy(Dm.bitand(attack_flags, Dm.bitor(ATTACKER_STAMINA_ATTACK, ATTACKER_SHOVING))))
        {
            Waiting.NoSleep(attacker.adjust_brute_loss(20));
            to_chat(attacker, span_warning("The clone casts a spell to damage you before he dies!"));
        }
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:95
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (isobserver(user))
            Dm.append(ref dot, span_notice("It's an illusion - what is it hiding?"));
        else
        {
            Dm.@new<obj_effect_temp_visual_small_smoke_halfsecond>(get_turf(this));
            qdel(this); //I see through your ruse!
        }
        return dot;
    }
}

// tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:116
public partial class obj_effect_temp_visual_paperwiz_dying : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:127
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        visible_message(span_bolddanger("The wizard cries out in pain as a gate appears behind him, sucking him in!"));
        playsound(get_turf(this), Dm.resource("sound/effects/magic/mandswap.ogg"), 50, vary: TRUE, pressure_affected: TRUE);
        playsound(get_turf(this), Dm.resource("sound/effects/hallucinations/wail.ogg"), 50, vary: TRUE, pressure_affected: TRUE);
        RegisterSignal(this, COMSIG_PREQDELETED, nameof(on_delete));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/paper_wizard/paper_wizard.dm:134
    [SignalHandler]
    public virtual void on_delete()
    {
        foreach (mob nearby in Dm.each<mob>(range(7, this)))
            shake_camera(nearby, 7 * SECONDS, 1);
        turf current_turf = get_turf(this);
        playsound(current_turf, Dm.resource("sound/effects/magic/summon_magic.ogg"), 50, vary: TRUE);
        Dm.@new<obj_effect_temp_visual_paper_scatter>(current_turf);
        Dm.@new<obj_item_clothing_suit_wizrobe_paper>(current_turf);
        Dm.@new<obj_item_clothing_head_collectable_paper>(current_turf);
    }
}
