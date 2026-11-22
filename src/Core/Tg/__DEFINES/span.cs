// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/span.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// PARTIAL: the function-like span macros the generator does not emit (M8 slice D: the penlight's and the stethoscope's
// readouts; M9 slice D: span_class, the headset's channel list); the object-like spans are generated (Generated/Defines/__DEFINES/span.g.cs).

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/span.dm:2  #define span_class(class, str) ("<span class='[class]'>" + str + "</span>")
    public static string span_class(object @class, object str) => Dm.text("<span class='[]'>", @class) + Dm.str(str) + "</span>";

    // tg: code/__DEFINES/span.dm:158  #define span_notice_ml(text) "<span class='notice ml-1'>[text]</span>"
    public static string span_notice_ml(object text) => Dm.text("<span class='notice ml-1'>[]</span>", text);

    // tg: code/__DEFINES/span.dm:159  #define span_danger_ml(text) "<span class='danger ml-1'>[text]</span>"
    public static string span_danger_ml(object text) => Dm.text("<span class='danger ml-1'>[]</span>", text);

    // tg: code/__DEFINES/span.dm:160  #define span_info_ml(text) "<span class='info ml-1'>[text]</span>"
    public static string span_info_ml(object text) => Dm.text("<span class='info ml-1'>[]</span>", text);
}
