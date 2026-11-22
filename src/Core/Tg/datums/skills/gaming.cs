// Translated from tgstation (snapshot 1d99bd7f): code/datums/skills/gaming.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The var block is generated (D111).

// tg: code/datums/skills/gaming.dm:1
public partial class datum_skill_gaming : datum_skill
{
    // tg: code/datums/skills/gaming.dm:9
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        levelUpMessages[1] = span_nicegreen(I18n.T("I'm starting to get a hang of the controls of these games..."));
        levelUpMessages[4] = span_nicegreen(I18n.T("I'm starting to pick up the meta of these arcade games. If I were to minmax the optimal strat and accentuate my playstyle around well-refined tech..."));
        levelUpMessages[6] = span_nicegreen(I18n.T("Through incredible determination and effort, I've reached the peak of my [] abilities. I wonder how I can become any more powerful... Maybe gamer fuel would actually help me play better..?", name));
    }
}
