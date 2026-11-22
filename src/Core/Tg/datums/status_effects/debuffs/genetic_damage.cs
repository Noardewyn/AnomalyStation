// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/genetic_damage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the genetic damage the DNA console's pulses and injectors give, its toxin damage, the genetics
// gorilla and the health scanner's line. The var block is generated (D111).

// tg: code/datums/status_effects/debuffs/genetic_damage.dm:4
public partial class datum_status_effect_genetic_damage : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/genetic_damage.dm:1  #define GORILLA_MUTATION_CHANCE_PER_SECOND 0.25 (file-local)
    private const float GORILLA_MUTATION_CHANCE_PER_SECOND = 0.25f;
    // tg: code/datums/status_effects/debuffs/genetic_damage.dm:2  #define GORILLA_MUTATION_MINIMUM_DAMAGE 2500 (file-local)
    private const float GORILLA_MUTATION_MINIMUM_DAMAGE = 2500;

    // tg: code/datums/status_effects/debuffs/genetic_damage.dm:21
    [DmParams("new_owner", "total_damage")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object total_damage = Dm.arg(__extra, 0);
        object dot = await base.on_creation(new_owner, __extra);
        this.total_damage = Dm.num(total_damage);
        RegisterSignal(new_owner, COMSIG_LIVING_HEALTHSCAN, nameof(on_healthscan));
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/genetic_damage.dm:26
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        UnregisterSignal(owner, COMSIG_LIVING_HEALTHSCAN);
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/genetic_damage.dm:30
    [DmParams("effect", "total_damage")]
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        object total_damage = Dm.arg(__extra, 0);
        await base.refresh(effect, __extra);
        this.total_damage += Dm.num(total_damage);
    }

    // tg: code/datums/status_effects/debuffs/genetic_damage.dm:34
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (iscarbon(owner) && HAS_TRAIT(owner, TRAIT_SIMIAN) && total_damage >= GORILLA_MUTATION_MINIMUM_DAMAGE && SPT_PROB(GORILLA_MUTATION_CHANCE_PER_SECOND, seconds_between_ticks))
        {
            mob_living_carbon carbon_owner = owner as mob_living_carbon;
            await carbon_owner.gorillize(genetics_gorilla: TRUE);
            qdel(this);
            return null;
        }

        if (total_damage >= minimum_before_tox_damage)
            await owner.adjust_tox_loss(toxin_damage_per_second * Dm.num(seconds_between_ticks));

        total_damage -= remove_per_second * Dm.num(seconds_between_ticks);
        if (total_damage <= 0)
        {
            qdel(this);
            return null;
        }
        return null;
    }

    // tg: code/datums/status_effects/debuffs/genetic_damage.dm:50
    [SignalHandler]
    public virtual void on_healthscan(datum source = null, DmList render_list = null, object scanpower = null, mob user = null, object mode = null, object tochat = null)
    {
        string message = "";
        if (Dm.num(scanpower) >= SCANPOWER_ADVANCED)
            message = I18n.T("Genetic damage: []%", round(total_damage / minimum_before_tox_damage * 100, 0.1f));
        else if (total_damage >= minimum_before_tox_damage)
            message = I18n.T("Severe genetic damage detected.");
        else
            message = I18n.T("Minor genetic damage detected.");

        if (Dm.truthy(message))
        {
            Dm.append(ref render_list, "<span class='alert ml-1'>");
            Dm.append(ref render_list, conditional_tooltip(message, I18n.T("Irreparable under normal circumstances - will decay over time."), tochat));
            Dm.append(ref render_list, "</span><br>");
        }
    }
}
