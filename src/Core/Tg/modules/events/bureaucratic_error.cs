// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/bureaucratic_error.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the bureaucratic error (the jobs' late-join slots shuffle, or only one job stays open, unlimited).
// The var blocks are generated (D111).

// tg: code/modules/events/bureaucratic_error.dm:9
public partial class datum_round_event_bureaucratic_error : datum_round_event
{
    // tg: code/modules/events/bureaucratic_error.dm:12
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("A recent bureaucratic error in the Organic Resources Department may result in personnel shortages in some departments and redundant staffing in others.", "Paperwork Mishap Alert");
    }

    // tg: code/modules/events/bureaucratic_error.dm:15
    public override async DmTask<object> start()
    {
        DmList jobs = (SSjob.get_valid_overflow_jobs() as DmList).Copy(); // tg takes from the static list itself; the copy keeps it whole
        if (prob(33)) // Only allows latejoining as a single role.
        {
            var overflow = pick_n_take(jobs) as datum_job;
            overflow.spawn_positions = -1;
            overflow.total_positions = -1; // Ensures infinite slots as this role. Assistant will still be open for those that cant play it.
            foreach (datum_job current in Dm.each_cast<datum_job>(jobs))
                current.total_positions = 0;
            return null;
        }
        // Adds/removes a random amount of job slots from all jobs.
        foreach (datum_job current in Dm.each_cast<datum_job>(jobs))
            current.total_positions = Math.Max(current.total_positions + rand(-2, 4), 0);
        return null;
    }
}
