// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/floor_diseases/carpellosis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111). Dirty food gives it (the germ-sensitive food's floor diseases).

/// Caused by dirty food. Makes you growl at people and bite them spontaneously.
// tg: code/datums/diseases/floor_diseases/carpellosis.dm:4
public partial class datum_disease_carpellosis : datum_disease
{
    // tg: code/datums/diseases/floor_diseases/carpellosis.dm:1
    private const float GNASHING_RANGE = 7;

    // tg: code/datums/diseases/floor_diseases/carpellosis.dm:27
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;


        if (stage == 2)
        {
            if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob) && Dm.truthy(affected_mob.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL)))
            {
                to_chat(affected_mob, span_warning(I18n.T("You want to wag your tail...")));
                await affected_mob.emote("wag");
            }
        }
        else if (stage == 3)
        {
            if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                to_chat(affected_mob, span_warning(I18n.T("You suddenly feel like swimming in space...")));
            else if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                affected_mob.visible_message(I18n.T("gnashes."), visible_message_flags: EMOTE_MESSAGE);
        }
        else if (stage == 4)
        {
            if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                await gnash_someone();
            else if (SPT_PROB(1, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                affected_mob.visible_message(I18n.T("gnashes."), visible_message_flags: EMOTE_MESSAGE);
        }
        else if (stage == 5)
        {
            max_stage_reached = TRUE;
            await grant_ability();
            if (SPT_PROB(2, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                await gnash_someone();
            else if (SPT_PROB(2, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(affected_mob))
                affected_mob.visible_message(I18n.T("gnashes."), visible_message_flags: EMOTE_MESSAGE);
        }
        return dot;
    }

    // tg: code/datums/diseases/floor_diseases/carpellosis.dm:56
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(ability_granted))
            QDEL_NULL(ref rift_ability);
        return base.Destroy(force);
    }

    // tg: code/datums/diseases/floor_diseases/carpellosis.dm:61
    public override async DmTask cure(float? add_resistance = null)
    {
        add_resistance ??= TRUE;
        if (Dm.truthy(ability_granted))
            await rift_ability.Remove(affected_mob);
        if (Dm.truthy(max_stage_reached) && prob(ella_spawn_chance))
        {
            to_chat(affected_mob, span_warning(I18n.T("Something comes out of you!")));
            Dm.@new<mob_living_basic_carp_ella>(affected_mob.loc);
        }
        await base.cure(add_resistance);
    }

    // tg: code/datums/diseases/floor_diseases/carpellosis.dm:69
    public virtual async DmTask grant_ability()
    {
        if (Dm.truthy(ability_granted))
            return;
        rift_ability = await Dm.new_async<datum_action_cooldown_mob_cooldown_lesser_carp_rift>(this);
        await rift_ability.Grant(affected_mob);
        rift_ability.HideFrom(affected_mob);
        ability_granted = TRUE;
    }

    // tg: code/datums/diseases/floor_diseases/carpellosis.dm:77
    public virtual object find_nearby_human()
    {
        DmList surroundings = orange(GNASHING_RANGE, affected_mob);
        foreach (mob_living_carbon_human human in Dm.each_cast<mob_living_carbon_human>(typecache_filter_list(surroundings, typecacheof(typeof(mob_living_carbon_human)) as DmList)))
        {
            if (!IS_DEAD_OR_FAKING(human))
                return human;
        }
        return null;
    }

    // tg: code/datums/diseases/floor_diseases/carpellosis.dm:83
    public virtual async DmTask gnash_someone()
    {
        mob_living_carbon_human target = find_nearby_human() as mob_living_carbon_human;
        if (target == null || !Dm.truthy(affected_mob.get_bodypart(BODY_ZONE_HEAD))) // Need mouth to gnash
        {
            to_chat(affected_mob, span_warning(I18n.T("You want to gnash at someone...")));
            return;
        }
        to_chat(affected_mob, span_warning(I18n.T("[] makes you angry for some reason...", target.name)));
        if (Dm.truthy(ability_granted) && !Dm.truthy(await affected_mob.Adjacent(target)))
            await rift_ability.Trigger(null, null, target);
        affected_mob.face_atom(target);
        if (Dm.truthy(await affected_mob.Adjacent(target)))
        {
            affected_mob.set_combat_mode(TRUE);
            await target.attack_paw(affected_mob);
        }
        else
            affected_mob.visible_message(I18n.T("gnashes at [].", target.name), visible_message_flags: EMOTE_MESSAGE);
    }
}
