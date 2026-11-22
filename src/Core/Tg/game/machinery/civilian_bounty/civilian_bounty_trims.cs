// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/civilian_bounty/civilian_bounty_trims.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file, the ID trim's civilian bounty choices.

// tg: code/game/machinery/civilian_bounty/civilian_bounty_trims.dm:1
public partial class datum_id_trim
{
    /**
     * Generates a list of bounties for use with the civilian bounty pad.
     *
     * @param bounty_types the define taken from a job for selection of a random_bounty() proc.
     * @param bounty_rolls the number of bounties to be selected from.
     * @param assistant_failsafe Do we guarentee one assistant bounty per generated list? Used for non-assistant jobs to give an easier alternative to that job's default bounties.
     */
    // tg: code/game/machinery/civilian_bounty/civilian_bounty_trims.dm:9
    public virtual DmList generate_bounty_list(float bounty_rolls = 3, float assistant_failsafe = TRUE)
    {
        var our_job = find_job() as datum_job;
        object bounty_type = Dm.truthy(our_job?.bounty_types) ? our_job.bounty_types : CIV_JOB_RANDOM;

        DmList rolling_list = Dm.list();
        if (Dm.truthy(assistant_failsafe))
        {
            Type random_assistant = get_random_bounty_type(CIV_JOB_BASIC);
            var assistant_bounty = Dm.@new(random_assistant) as datum_bounty;
            if (Dm.truthy(assistant_bounty.can_get()))
                Dm.append(ref rolling_list, assistant_bounty);
            else
                qdel(assistant_bounty);
        }

        int attempts = 20;
        while (length(rolling_list) < bounty_rolls && attempts > 0)
        {
            Type random_job = get_random_bounty_type(attempts <= 5 ? CIV_JOB_BASIC : bounty_type);
            var job_bounty = Dm.@new(random_job) as datum_bounty;
            attempts -= 1;
            if (!Dm.truthy(job_bounty.can_get()) || has_duplicate_bounty(rolling_list, job_bounty))
            {
                qdel(job_bounty);
                continue;
            }

            Dm.append(ref rolling_list, job_bounty);
        }

        return rolling_list;
    }

    /// Helper to see if there's a duplicate bounty in a list of bounties
    // tg: code/game/machinery/civilian_bounty/civilian_bounty_trims.dm:36
    private bool has_duplicate_bounty(DmList bounty_list, datum_bounty check_bounty)
    {
        foreach (datum_bounty existing in Dm.each_cast<datum_bounty>(bounty_list))
        {
            if (existing.GetType() != check_bounty.GetType())
                continue;
            if (Dm.truthy(existing.allow_duplicate) && Dm.truthy(check_bounty.allow_duplicate))
                continue;
            return true;
        }

        return false;
    }

    /// Returns a /datum/bounty typepath for a given bounty type
    // tg: code/game/machinery/civilian_bounty/civilian_bounty_trims.dm:49
    public virtual Type get_random_bounty_type(object input_bounty_type = null)
    {
        if (!Dm.truthy(input_bounty_type) || Dm.eq(input_bounty_type, CIV_JOB_RANDOM))
            input_bounty_type = rand(1, MAXIMUM_BOUNTY_JOBS);

        switch ((int)Dm.num(input_bounty_type))
        {
            case CIV_JOB_BASIC:
                return pick(subtypesof(typeof(datum_bounty_item_assistant)) as DmList) as Type;
            case CIV_JOB_ROBO:
                return pick(subtypesof(typeof(datum_bounty_item_mech)) as DmList) as Type;
            case CIV_JOB_CHEF:
                return pick(Dm.add(subtypesof(typeof(datum_bounty_item_chef)), subtypesof(typeof(datum_bounty_reagent_chef))) as DmList) as Type;
            case CIV_JOB_SEC:
                if (prob(75))
                    return typeof(datum_bounty_patrol);
                return typeof(datum_bounty_item_contraband);
            case CIV_JOB_DRINK:
                if (prob(50))
                    return typeof(datum_bounty_reagent_simple_drink);
                return typeof(datum_bounty_reagent_complex_drink);
            case CIV_JOB_CHEM:
                if (prob(50))
                    return typeof(datum_bounty_reagent_chemical_simple);
                return typeof(datum_bounty_reagent_chemical_complex);
            case CIV_JOB_MED_VIRO:
                if (prob(75))
                    return pick(subtypesof(typeof(datum_bounty_item_medical)) as DmList) as Type;
                return pick(subtypesof(typeof(datum_bounty_virus)) as DmList) as Type;
            case CIV_JOB_SCI:
                if (prob(50))
                    return pick(subtypesof(typeof(datum_bounty_item_science)) as DmList) as Type;
                return pick(subtypesof(typeof(datum_bounty_item_slime)) as DmList) as Type;
            case CIV_JOB_ENG:
                return pick(subtypesof(typeof(datum_bounty_item_engineering)) as DmList) as Type;
            case CIV_JOB_MINE:
                return pick(subtypesof(typeof(datum_bounty_item_mining)) as DmList) as Type;
            case CIV_JOB_MED:
                return pick(subtypesof(typeof(datum_bounty_item_medical)) as DmList) as Type;
            case CIV_JOB_GROW:
                return pick(subtypesof(typeof(datum_bounty_item_botany)) as DmList) as Type;
            case CIV_JOB_ATMOS:
                return pick(subtypesof(typeof(datum_bounty_item_atmospherics)) as DmList) as Type;
            case CIV_JOB_BITRUN:
                return pick(subtypesof(typeof(datum_bounty_item_bitrunning)) as DmList) as Type;
        }

        stack_trace(Dm.text("Failed to get random bounty type for input type []", input_bounty_type), "code/game/machinery/civilian_bounty/civilian_bounty_trims.dm", 93);
        return null;
    }
}
