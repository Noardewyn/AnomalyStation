// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/clients.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: unvalidated_client_error (M9 slice A, the OOC verb) and is_guest_key (silicons SI6, D326: the pAI personality's
// savefile). The port's keys are never BYOND guest keys, so is_guest_key is FALSE for them.

public static partial class GlobalProcs
{
    ///Returns whether or not a player is a guest using their ckey as an input
    // tg: code/__HELPERS/clients.dm:2
    public static bool is_guest_key(object key = null)
    {
        string k = Dm.str(key);
        if (!k.StartsWith("Guest-", StringComparison.OrdinalIgnoreCase)) //was findtextEx
            return false;
        for (int i = 6; i < k.Length; i++) //we know the first 6 chars are Guest-
        {
            char ch = k[i];
            if (ch < '0' || ch > '9') //0-9
                return false;
        }
        return true;
    }

    /// Proc that just logs whenever an uninitialized client tries to do something before they have fully gone through New().
    /// Intended to be used in conjunction with the `VALIDATE_CLIENT_INITIALIZATION()` macro, but can be dropped anywhere when we look at the `fully_created` var on /client.
    // tg: code/__HELPERS/clients.dm:16
    public static void unvalidated_client_error(client target = null)
    {
        to_chat(target, span_warning(I18n.T("You are not fully initialized yet! Please wait a moment.")));
        log_access(Dm.text("Client [] attempted to execute a verb before being fully initialized.", key_name(target)));
    }
}
