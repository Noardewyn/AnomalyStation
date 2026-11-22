// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/fake_virus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4 (D327): the whole file: one to three crewmen get a fake virus (hypochondria: coughs, sneezes and worrying messages for
// three minutes, the status effect in debuffs.cs) and a few more cough or feel their head hurt. The var blocks are generated (D111).

// tg: code/modules/events/fake_virus.dm:8
public partial class datum_round_event_fake_virus : datum_round_event
{
    // tg: code/modules/events/fake_virus.dm:8
    public override async DmTask<object> start()
    {
        var fake_virus_victims = Dm.list();
        foreach (mob_living_carbon_human victim in Dm.each<mob_living_carbon_human>(GLOB.player_list))
        {
            if (IS_UNCONSCIOUS_OR_CRIT(victim) || HAS_TRAIT(victim, TRAIT_VIRUSIMMUNE))
                continue;
            if (!Dm.truthy(Dm.bitand(victim.mind?.assigned_role?.job_flags, JOB_CREW_MEMBER)))
                continue;
            Dm.append(fake_virus_victims, victim);
        }

        float defacto_min = min(3, Dm.length(fake_virus_victims));
        if (defacto_min <= 0)// event will hit 1-3 people by default, but will do 1-2 or just 1 if only those many candidates are available
            return null;
        float count = rand(1, defacto_min);
        for (int i = 1; i <= count; i++)
        {
            var hypochondriac = (mob_living_carbon_human)pick_n_take(fake_virus_victims);
            await hypochondriac.apply_status_effect(typeof(datum_status_effect_fake_virus));
            announce_to_ghosts(hypochondriac);
        }

        defacto_min = min(5, Dm.length(fake_virus_victims));
        if (defacto_min <= 0)
            return null;
        count = rand(1, defacto_min);
        for (int i = 1; i <= count; i++)
        {
            var onecoughman = (mob_living_carbon_human)pick_n_take(fake_virus_victims);
            if (prob(25))//1/4 odds to get a spooky message instead of coughing out loud
                _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.to_chat), onecoughman, span_warning((string)pick(I18n.T("Your head hurts."), I18n.T("Your head pounds.")))),
                    rand(3 * SECONDS, 15 * SECONDS), file: "code/modules/events/fake_virus.dm", line: 32);
            else
                _addtimer(Dm.@new<datum_callback>(onecoughman, nameof(mob.emote), pick("cough", "sniff")), rand(3 * SECONDS, 15 * SECONDS),
                    file: "code/modules/events/fake_virus.dm", line: 34);//deliver the message with a slightly randomized time interval so there is no instant flood
        }
        return null;
    }
}
