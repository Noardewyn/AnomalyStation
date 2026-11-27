// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/aura_healing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/// Applies healing to those in the area.
/// Will provide them with an alert while they're in range, as well as
/// give them a healing particle.
/// Can be applied to those only with a trait conditionally.
// tg: code/datums/components/aura_healing.dm:7
public partial class datum_component_aura_healing : datum_component
{
    private const float HEAL_EFFECT_COOLDOWN = 1 * SECONDS; // tg: code/datums/components/aura_healing.dm:1

    // tg: code/datums/components/aura_healing.dm:56
    [DmParams("range", "requires_visibility", "brute_heal", "burn_heal", "toxin_heal", "suffocation_heal", "stamina_heal", "blood_heal", "wound_clotting", "organ_healing", "simple_heal", "limit_to_trait", "healing_color", "self_heal")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        START_PROCESSING(SSaura, this);

        this.range = Dm.num_or_null(Dm.arg(__extra, 0) ?? 5f);
        this.requires_visibility = Dm.num(Dm.arg(__extra, 1) ?? TRUE);
        this.brute_heal = Dm.num(Dm.arg(__extra, 2) ?? 0f);
        this.burn_heal = Dm.num(Dm.arg(__extra, 3) ?? 0f);
        this.toxin_heal = Dm.num(Dm.arg(__extra, 4) ?? 0f);
        this.suffocation_heal = Dm.num(Dm.arg(__extra, 5) ?? 0f);
        this.stamina_heal = Dm.num(Dm.arg(__extra, 6) ?? 0f);
        this.blood_heal = Dm.num(Dm.arg(__extra, 7) ?? 0f);
        this.wound_clotting = Dm.num(Dm.arg(__extra, 8) ?? 0f);
        this.organ_healing = Dm.arg(__extra, 9) as DmList;
        this.simple_heal = Dm.num(Dm.arg(__extra, 10) ?? 0f);
        this.limit_to_trait = Dm.arg(__extra, 11);
        this.healing_color = (string)(Dm.arg(__extra, 12) ?? COLOR_GREEN);
        this.self_heal = Dm.num(Dm.arg(__extra, 13) ?? TRUE);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/aura_healing.dm:92
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSaura, this);
        string alert_category = Dm.text("aura_healing_[]", REF(this));

        foreach (object alert_holder in Dm.each(current_alerts))
            ((mob_living)alert_holder).clear_alert(alert_category);
        current_alerts.Cut();

        return base.Destroy(force);
    }

    // tg: code/datums/components/aura_healing.dm:102
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        bool should_show_effect = last_heal_effect_time <= world.time; // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
        if (should_show_effect)
            last_heal_effect_time = world.time + HEAL_EFFECT_COOLDOWN; // COOLDOWN_START, inline

        float spt = Dm.num(seconds_per_tick);
        DmList to_heal = Dm.list();
        string alert_category = Dm.text("aura_healing_[]", REF(this));

        if (Dm.truthy(requires_visibility))
        {
            foreach (mob_living candidate in Dm.each<mob_living>(view(range, parent)))
            {
                if (limit_to_trait != null && !HAS_TRAIT(candidate, limit_to_trait))
                    continue;
                to_heal[candidate] = TRUE;
            }
        }
        else
        {
            foreach (mob_living candidate in Dm.each<mob_living>(Builtins.range(range, parent)))
            {
                if (limit_to_trait != null && !HAS_TRAIT(candidate, limit_to_trait))
                    continue;
                to_heal[candidate] = TRUE;
            }
        }

        foreach (object candidate__it in Dm.each(to_heal))
        {
            mob_living candidate = (mob_living)candidate__it;
            if (!Dm.truthy(current_alerts[candidate]))
            {
                candidate.throw_alert(alert_category, typeof(atom_movable_screen_alert_aura_healing), new_master: parent as atom);
                current_alerts[candidate] = TRUE;
            }

            if (should_show_effect && Dm.num(candidate.health) < Dm.num(candidate.maxHealth))
                Dm.@new<obj_effect_temp_visual_heal>(get_turf(candidate), healing_color);

            if (iscarbon(candidate) || issilicon(candidate) || isbasicmob(candidate))
            {
                Waiting.NoSleep(candidate.adjust_brute_loss(-brute_heal * spt, updating_health: FALSE));
                Waiting.NoSleep(candidate.adjust_fire_loss(-burn_heal * spt, updating_health: FALSE));
            }

            if (iscarbon(candidate))
            {
                // Toxin healing is forced for slime people
                Waiting.NoSleep(candidate.adjust_tox_loss(-toxin_heal * spt, updating_health: FALSE, forced: TRUE));

                Waiting.NoSleep(candidate.adjust_oxy_loss(-suffocation_heal * spt, updating_health: FALSE));
                Waiting.NoSleep(candidate.adjust_stamina_loss(-stamina_heal * spt, updating_stamina: FALSE));

                foreach (object organ in Dm.each(organ_healing))
                    Waiting.NoSleep(candidate.adjust_organ_loss(organ, -Dm.num(organ_healing[organ]) * spt));
                mob_living_carbon carbidate = candidate as mob_living_carbon;
                foreach (datum_wound iter_wound in Dm.each<datum_wound>(carbidate.all_wounds))
                    Waiting.NoSleep(iter_wound.adjust_blood_flow(-wound_clotting * spt));
            }
            else if (isanimal_or_basicmob(candidate))
                Waiting.NoSleep(candidate.adjust_brute_loss(-simple_heal * spt, updating_health: FALSE));

            candidate.adjust_blood_volume(blood_heal * spt, maximum: BLOOD_VOLUME_NORMAL);

            Waiting.NoSleep(candidate.updatehealth());
        }

        foreach (object remove_alert_from in Dm.each(Dm.sub(current_alerts, to_heal)))
        {
            ((mob_living)remove_alert_from).clear_alert(alert_category);
            Dm.remove(current_alerts, remove_alert_from);
        }
        return null;
    }
}

// tg: code/datums/components/aura_healing.dm:157
public partial class atom_movable_screen_alert_aura_healing : atom_movable_screen_alert
{
    // tg: code/datums/components/aura_healing.dm:164
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = I18n.T("You are being healed by [].", master_ref?.resolve());
        return dot;
    }
}
