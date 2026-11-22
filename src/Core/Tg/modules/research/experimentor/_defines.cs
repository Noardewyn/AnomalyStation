// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/experimentor/_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// The whole file (gaps RD4, D333: the experimentor): the experimentor's scan types, its message spans, its effect probabilities and
// its eject wire. The file never #undefs them and the .dme includes it outside code/__DEFINES, so the defines converter does not emit
// them: they are public members of GlobalProcs (rules 11.4).

public static partial class GlobalProcs
{
    // tg: code/modules/research/experimentor/_defines.dm:1
    public const string SCANTYPE_POKE = "Poke";
    // tg: code/modules/research/experimentor/_defines.dm:2
    public const string SCANTYPE_IRRADIATE = "Irradiate";
    // tg: code/modules/research/experimentor/_defines.dm:3
    public const string SCANTYPE_GAS = "Gas";
    // tg: code/modules/research/experimentor/_defines.dm:4
    public const string SCANTYPE_HEAT = "Heat";
    // tg: code/modules/research/experimentor/_defines.dm:5
    public const string SCANTYPE_COLD = "Freeze";
    // tg: code/modules/research/experimentor/_defines.dm:6
    public const string SCANTYPE_OBLITERATE = "Obliterate";
    // tg: code/modules/research/experimentor/_defines.dm:7
    public const string SCANTYPE_DISCOVER = "Discover";
    // tg: code/modules/research/experimentor/_defines.dm:8
    public const string FAIL = "Fail";

    // tg: code/modules/research/experimentor/_defines.dm:10
    public const string MSG_TYPE_NOTICE = "notice";
    // tg: code/modules/research/experimentor/_defines.dm:11
    public const string MSG_TYPE_WARNING = "warning";
    // tg: code/modules/research/experimentor/_defines.dm:12
    public const string MSG_TYPE_DANGER = "danger";

    // tg: code/modules/research/experimentor/_defines.dm:14
    public const int EFFECT_PROBABILITY = 20;
    // tg: code/modules/research/experimentor/_defines.dm:15  #define EXPERIMENT_COOLDOWN_BASE 2 SECONDS
    public const float EXPERIMENT_COOLDOWN_BASE = 2 * SECONDS;

    // tg: code/modules/research/experimentor/_defines.dm:17
    public const string WIRE_EJECT = "Eject";

    // tg: code/modules/research/experimentor/_defines.dm:19
    public const int EFFECT_PROB_VERYLOW = 20;
    // tg: code/modules/research/experimentor/_defines.dm:20
    public const int EFFECT_PROB_LOW = 35;
    // tg: code/modules/research/experimentor/_defines.dm:21
    public const int EFFECT_PROB_MEDIUM = 50;
    // tg: code/modules/research/experimentor/_defines.dm:22
    public const int EFFECT_PROB_HIGH = 75;
    // tg: code/modules/research/experimentor/_defines.dm:23
    public const int EFFECT_PROB_VERYHIGH = 95;
}
