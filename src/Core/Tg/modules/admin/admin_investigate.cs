// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/admin_investigate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the log itself; the admins' Investigate verb, which reads it back, comes with the admin verbs.

// tg: code/modules/admin/admin_investigate.dm:1
public partial class atom : datum
{
    // tg: code/modules/admin/admin_investigate.dm:1
    public virtual void investigate_log(object message = null, object subject = null)
    {
        if (!Dm.truthy(message))
            return;
        if (!Dm.truthy(subject))
            throw Dm.CRASH("No subject provided for investigate_log", "code/modules/admin/admin_investigate.dm", 5);
        // shortcut: a world that never set a log folder (the test rooms) keeps no log, a server always has one
        if (GLOB.log_directory == null)
            return;
        object F = Dm.text("[]/[].html", GLOB.log_directory, subject);
        string source = Dm.text("[]", this);

        if (isliving(this))
        {
            mob_living source_mob = this as mob_living;
            source += Dm.text(" ([])", Dm.truthy(source_mob.ckey) ? source_mob.ckey : "*no key*");
        }

        Dm.text2file(Dm.text("[] [] ([],[],[]) || [] []<br>", server_timestamp(format: "YYYY-MM-DD hh:mm:ss"), REF(this), x, y, z, source, message), F);
    }
}
