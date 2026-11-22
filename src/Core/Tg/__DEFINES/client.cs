// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/client.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: IS_FAKE_KEY (wave 4, package C: /mob/Destroy and /mob/proc/ghostize use it) and VALIDATE_CLIENT_INITIALIZATION (M9: the
// OOC verb), IS_CLIENT_OR_MOCK (syndicate N1c, D316: the preferences of randomize_human_appearance's mock client).

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/client.dm:2  #define IS_CLIENT_OR_MOCK(target) (istype(target, /client) || istype(target, /datum/client_interface))
    public static bool IS_CLIENT_OR_MOCK(object target) => target is client || target is datum_client_interface;

    /// Checks to see if a /client has fully gone through New() as a safeguard against certain operations.
    /// Should return the boolean value of the fully_created var, which should be TRUE if New() has finished running. FALSE otherwise.
    // tg: code/__DEFINES/client.dm:6  #define VALIDATE_CLIENT_INITIALIZATION(target) (target.fully_created)
    public static object VALIDATE_CLIENT_INITIALIZATION(client target) => target.fully_created;

    /// Aghosting AND APPERANTLY STEALTHMINNING sets your ckey/key == "@[old_key]" because it hates me. this detects that
    // tg: code/__DEFINES/client.dm:9  #define IS_FAKE_KEY(key) (key && key[1] == "@")
    public static bool IS_FAKE_KEY(object key)
    {
        // SHIM-GAP: indexing text (key[1], BYOND 515: the character at that position) is not in the shim;
        // copytext_char(key, 1, 2) reads the same first character.
        return Dm.truthy(key) && Dm.eq(copytext_char(key, 1, 2), "@");
    }
}
