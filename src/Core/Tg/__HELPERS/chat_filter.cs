// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/chat_filter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M9 slice E: a PDA message's filter): check_pda_message_against_filter, is_ic_filtered_for_pdas,
// is_soft_ic_filtered_for_pdas and censor_ic_filter_for_pdas (a circuit's PDA message). The word filter's regexes are built from config/word_filter.toml by the configuration's
// LoadChatFilter, which the port does not load (the regex is a shim gap, configuration.cs): they stay null, so no word is
// filtered, as with no word filter configured. The IC, OOC and censor filters come with their callers.

public static partial class GlobalProcs
{
    /// Checks a PDA message against the IC/Soft IC filter. Returns TRUE if the message should be sent.
    /// Notifies the user passed in arguments if the message matched either filter.
    // tg: code/__HELPERS/chat_filter.dm:49
    public static async DmTask<object> check_pda_message_against_filter(object message = null, mob user = null)
    {
        // CAN_BYPASS_FILTER(user) (__DEFINES/chat_filter.dm:12): (!isnull(user?.client?.holder))
        bool can_bypass = user?.client?.holder != null;
        DmList filter_result = can_bypass ? null : is_ic_filtered_for_pdas(message);
        if (filter_result != null)
        {
            // REPORT_CHAT_FILTER_TO_USER(user, filter_result) (__DEFINES/chat_filter.dm:8)
            to_chat(user, span_warning(I18n.T("The word <b>[]</b> is prohibited: []", html_encode(filter_result[CHAT_FILTER_INDEX_WORD]), html_encode(filter_result[CHAT_FILTER_INDEX_REASON]))));
            return FALSE;
        }

        DmList soft_filter_result = can_bypass ? null : is_soft_ic_filtered_for_pdas(message);
        if (soft_filter_result != null)
        {
            if (!Dm.eq(await tgui_alert(user, I18n.T("Your message contains \"[]\". \"[]\", Are you sure you want to send it?", soft_filter_result[CHAT_FILTER_INDEX_WORD], soft_filter_result[CHAT_FILTER_INDEX_REASON]), I18n.T("Soft Blocked Word"), Dm.list("Yes", "No")), "Yes"))
                return FALSE;
            message_admins(Dm.text("[] has passed the soft filter for \"[]\" they may be using a disallowed term in PDA messages. Message: \"[]\"", ADMIN_LOOKUPFLW(user), soft_filter_result[CHAT_FILTER_INDEX_WORD], message));
            log_admin_private(Dm.text("[] has passed the soft filter for \"[]\" they may be using a disallowed term in PDA messages. Message: \"[]\"", key_name(user), soft_filter_result[CHAT_FILTER_INDEX_WORD], message));
        }

        return TRUE;
    }

    /// Given a text, will return what word is on the IC filter, ignoring words allowed on the PDA, with the reason.
    /// Returns null if the message is OK.
    // tg: code/__HELPERS/chat_filter.dm:66
    public static DmList is_ic_filtered_for_pdas(object message = null)
    {
        if (datum_controller_configuration.ic_outside_pda_filter_regex != null)
            Dm.NotTranslated("code/__HELPERS/chat_filter.dm:67", "is_ic_filtered_for_pdas: the word filter's regex (/regex, a shim gap; the filter config is not loaded)");
        return null;
    }

    /// Given a text, will return what word is on the soft IC filter, ignoring words allowed on the PDA, with the reason.
    /// Returns null if the message is OK.
    // tg: code/__HELPERS/chat_filter.dm:78
    public static DmList is_soft_ic_filtered_for_pdas(object message = null)
    {
        if (datum_controller_configuration.soft_ic_outside_pda_filter_regex != null)
            Dm.NotTranslated("code/__HELPERS/chat_filter.dm:79", "is_soft_ic_filtered_for_pdas: the word filter's regex (/regex, a shim gap; the filter config is not loaded)");
        return null;
    }

    ///Given a pda message, will replace any match in the message with grawlixs.
    // tg: code/__HELPERS/chat_filter.dm:89
    public static object censor_ic_filter_for_pdas(object message = null)
    {
        if (datum_controller_configuration.ic_outside_pda_filter_regex != null)
            Dm.NotTranslated("code/__HELPERS/chat_filter.dm:91", "censor_ic_filter_for_pdas: the word filter's regex (/regex, a shim gap; the filter config is not loaded)");
        if (datum_controller_configuration.soft_ic_outside_pda_filter_regex != null)
            Dm.NotTranslated("code/__HELPERS/chat_filter.dm:93", "censor_ic_filter_for_pdas: the word filter's regex (/regex, a shim gap; the filter config is not loaded)");
        return message;
    }
}

// tg: code/__HELPERS/chat_filter.dm:5 (M10 slice F: the name filter of reject_bad_name, filter_name_ic)
public static partial class GlobalProcs
{
    /// Given a text, will return what word is on the IC filter, with the reason.
    /// Returns null if the message is OK.
    // tg: code/__HELPERS/chat_filter.dm:7
    public static DmList is_ic_filtered(object message = null)
    {
        if (datum_controller_configuration.ic_filter_regex != null)
            Dm.NotTranslated("code/__HELPERS/chat_filter.dm:8", "is_ic_filtered: the word filter's regex (/regex, a shim gap; the filter config is not loaded)");
        return null;
    }

    /// Given a text, will return what word is on the soft IC filter, with the reason.
    /// Returns null if the message is OK.
    // tg: code/__HELPERS/chat_filter.dm:19
    public static DmList is_soft_ic_filtered(object message = null)
    {
        if (datum_controller_configuration.soft_ic_filter_regex != null)
            Dm.NotTranslated("code/__HELPERS/chat_filter.dm:20", "is_soft_ic_filtered: the word filter's regex (/regex, a shim gap; the filter config is not loaded)");
        return null;
    }
}

public static partial class GlobalProcs
{
    /// Given a text, will return that word is on the soft OOC filter, with the reason.
    /// Returns null if the message is OK.
    // tg: code/__HELPERS/chat_filter.dm:40
    public static DmList is_soft_ooc_filtered(object message = null)
    {
        if (datum_controller_configuration.soft_ooc_filter_regex != null)
            Dm.NotTranslated("code/__HELPERS/chat_filter.dm:41", "is_soft_ooc_filtered: the word filter's regex (/regex, a shim gap; the filter config is not loaded)");
        return null;
    }
}
