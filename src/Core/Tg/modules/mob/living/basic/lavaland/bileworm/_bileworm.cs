// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/bileworm/_bileworm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/basic/lavaland/bileworm/_bileworm.dm:1
public partial class mob_living_basic_mining_bileworm : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/lavaland/bileworm/_bileworm.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_IMMOBILIZED, INNATE_TRAIT);

        if (ispath(evolve_path))
            Waiting.NoSleep(AddComponent(typeof(datum_component_evolutionary_leap), 30 * MINUTES, evolve_path));
        Waiting.NoSleep(AddElement(typeof(datum_element_content_barfer)));
        datum_action_cooldown_mob_cooldown spew_bile = Dm.@new(attack_action_path, this) as datum_action_cooldown_mob_cooldown;
        Waiting.NoSleep(spew_bile.Grant(this));
        spew_bile.StartCooldownSelf(INFINITY);
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BILEWORM_SPEW_BILE, spew_bile);

        DmList other_innate_actions = TgState.Current.mob_living_basic_mining_bileworm__Initialize__other_innate_actions ??= Dm.list(
            Dm.kv(typeof(datum_action_adjust_vision_bileworm), null),
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_devour), BB_BILEWORM_DEVOUR),
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_resurface), BB_BILEWORM_RESURFACE));
        grant_actions_by_list(other_innate_actions);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/_bileworm.dm:62
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (stat != DEAD && !Dm.truthy(jumping))
            Dm.Deferred("code/modules/mob/living/basic/lavaland/bileworm/_bileworm.dm:65", "update_overlays: emissive_appearance(icon, emissive_state, src), the bileworm's glow in the dark (DEVIATION DV12)");
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/bileworm/_bileworm.dm:67
    [SignalHandler]
    public override void on_attacked(datum source = null, atom attacker = null, object attack_flags = null)
    {
        base.on_attacked(source, attacker, attack_flags);
        if (!Dm.truthy(Dm.bitand(attack_flags, Dm.bitor(ATTACKER_STAMINA_ATTACK, ATTACKER_SHOVING))))
            (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BILEWORM_SCARED, TRUE);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/lavaland/bileworm/_bileworm.dm:54 (var/static/list/other_innate_actions)
    internal DmList mob_living_basic_mining_bileworm__Initialize__other_innate_actions;
}
