// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/files.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// PARTIAL (Gaps H3, the jukebox's config tracks): IS_SOUND_FILE_SAFE. The other file-type macros come with their users.

// None of these should be trusted for the purpose of user input
// They do not check the actual type of the file, a user could just rename it
public static partial class GlobalProcs
{
    /// File types we can sniff the duration from using rustg.
    // tg: code/__DEFINES/files.dm:5  #define IS_SOUND_FILE_SAFE(file) is_file_type_in_list(##file, SSsounds.safe_formats)
    public static object IS_SOUND_FILE_SAFE(object file) => is_file_type_in_list(file, datum_controller_subsystem_sounds.safe_formats);
}
