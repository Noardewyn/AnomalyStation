// Translated from tgstation (snapshot 1d99bd7f): code/datums/records/medical_note.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps SC2, D333): the note a medical records console adds to a crew record. The var block is generated (D111).

/**
 * Player-written medical note.
 */
// tg: code/datums/records/medical_note.dm:4
public partial class datum_medical_note : datum
{
    // tg: code/datums/records/medical_note.dm:12
    [DmParams("author", "content", "time")]
    public override async DmTask New(params object[] __extra)
    {
        object author = Dm.arg(__extra, 0);
        object content = Dm.arg(__extra, 1);
        object time = Dm.arg(__extra, 2);
        author ??= "Anonymous";
        content ??= "No details provided.";
        time ??= "--:--:--";
        this.author = author;
        this.content = content;
        this.time = time;
    }
}
