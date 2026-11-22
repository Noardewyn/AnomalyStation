// Translated from tgstation (snapshot 1d99bd7f): code/datums/records/crime.dm (M9 slice G: the crimes and citations a security HUD adds
// from the human's examine)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Crime data. Used to store information about crimes.
 */
// tg: code/datums/records/crime.dm:4
public partial class datum_crime : datum
{
    // tg: code/datums/records/crime.dm:18
    [DmParams("name", "details", "author")]
    public override async DmTask New(params object[] __extra)
    {
        this.author = Dm.arg(__extra, 2) ?? "Anonymous";
        this.details = Dm.arg(__extra, 1) ?? "No details provided.";
        this.name = Dm.arg(__extra, 0) ?? "Crime";
        this.time = round_timestamp();
    }
}

// tg: code/datums/records/crime.dm:24
public partial class datum_crime_citation : datum_crime
{
    // tg: code/datums/records/crime.dm:30
    [DmParams("name", "details", "author", "fine")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(Dm.arg(__extra, 0) ?? "Citation", Dm.arg(__extra, 1), Dm.arg(__extra, 2));
        this.fine = Dm.arg(__extra, 3) ?? 0;
        this.paid = 0;
    }

    /// Pays off a fine and attempts to fix any weird values.
    // tg: code/datums/records/crime.dm:36
    public virtual object pay_fine(object amount = null)
    {
        if (Dm.num(amount) <= 0)
            return FALSE;

        paid = Dm.num(paid) + Dm.num(amount);
        if (Dm.num(paid) > Dm.num(fine))
            paid = fine;

        fine = Dm.num(fine) - Dm.num(amount);
        if (Dm.num(fine) < 0)
            fine = 0;

        return TRUE;
    }

    /// Sends a citation alert message to the target's PDA.
    // tg: code/datums/records/crime.dm:51
    public virtual object alert_owner(mob sender = null, atom source = null, object target_name = null, object message = null)
    {
        foreach (object messenger_ref in Dm.each(GLOB.pda_messengers))
        {
            datum_computer_file_program_messenger messenger = GLOB.pda_messengers[messenger_ref] as datum_computer_file_program_messenger;
            if (!Dm.eq(messenger.computer.saved_identification, target_name))
                continue;

            datum_signal_subspace_messaging_tablet_message signal = Dm.@new<datum_signal_subspace_messaging_tablet_message>(source, Dm.list(
                Dm.kv("fakename", "Security Citation"),
                Dm.kv("fakejob", "Citation Server"),
                Dm.kv("message", message),
                Dm.kv("targets", Dm.list(messenger)),
                Dm.kv("automated", TRUE)));
            signal.send_to_receivers();
            sender.log_message(Dm.text("(PDA: Citation Server) sent \"[]\" to []", message, signal.format_target()), LOG_PDA);
            break;
        }

        return TRUE;
    }
}
