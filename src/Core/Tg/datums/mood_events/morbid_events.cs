// Translated from tgstation (snapshot 1d99bd7f): code/datums/mood_events/morbid_events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file. Its mood events define no procs; their var blocks are generated (D111). morbid_saved_life
// keeps the [DmPath] of its M2 skeleton.

// tg: code/datums/mood_events/morbid_events.dm:52
[DmPath("/datum/mood_event/morbid_saved_life")]
public partial class datum_mood_event_morbid_saved_life : datum_mood_event
{
}
