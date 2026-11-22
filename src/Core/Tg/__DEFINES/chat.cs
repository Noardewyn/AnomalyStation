// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/chat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the boxed messages and conditional_tooltip (M8 slice D: the health analyzer's, penlight's and
// stethoscope's readouts), the fieldset block and the separator (M9 slice G: the examine); the other macros of chat.dm are translated with the code that uses them.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/chat.dm:48  #define boxed_message(str) ("<div class='boxed_message'>" + str + "</div>")
    public static string boxed_message(object str) => "<div class='boxed_message'>" + Dm.str(str) + "</div>";

    // tg: code/__DEFINES/chat.dm:50  #define custom_boxed_message(classes, str) ("<div class='boxed_message " + classes + "'>" + str + "</div>")
    public static string custom_boxed_message(string classes, object str) => "<div class='boxed_message " + classes + "'>" + Dm.str(str) + "</div>";

    // tg: code/__DEFINES/chat.dm:52  #define fieldset_block(title, content, classes) ("<fieldset class='fieldset " + classes + "'><legend class='fieldset_legend'>" + title + "</legend>" + content + "</fieldset>")
    public static string fieldset_block(object title, object content, object classes) => "<fieldset class='fieldset " + Dm.str(classes) + "'><legend class='fieldset_legend'>" + Dm.str(title) + "</legend>" + Dm.str(content) + "</fieldset>";

    // tg: code/__DEFINES/chat.dm:54  #define separator_hr(str) ("<div class='separator'>" + str + "</div>")
    public static string separator_hr(object str) => "<div class='separator'>" + Dm.str(str) + "</div>";

    // tg: code/__DEFINES/chat.dm:58  #define conditional_tooltip(normal_text, tooltip_text, condition) ((condition) ? (span_tooltip(tooltip_text, normal_text)) : (normal_text))
    public static string conditional_tooltip(string normal_text, string tooltip_text, object condition) => Dm.truthy(condition) ? span_tooltip(tooltip_text, normal_text) : normal_text;
}
