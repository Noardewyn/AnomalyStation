// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only num2hex (M5 slice 2: the material colours), hex2num (M9: colorize_string), subtypesof and text_ref translated (the list, sort and comparator helpers, code/__HELPERS/_lists.dm,
// cmp.dm and ref.dm, use them); the rest of _helpers.dm is translated with the waves that use it (rules 11.1).
public static partial class GlobalProcs
{
    //Returns the hex value of a decimal number
    //len == length of returned string
    // tg: code/__DEFINES/_helpers.dm:18  #define num2hex(X, len) num2text(X, len, 16)
    public static string num2hex(object X, object len) => num2text(Dm.num(X), Dm.num(len), 16);

    // tg: code/__DEFINES/_helpers.dm:22  #define hex2num(X) text2num(X, 16)
    public static object hex2num(object X) => text2num(X, 16);

    // tg: code/__DEFINES/_helpers.dm:28
    /// subtypesof(), typesof() without the parent path
    // Returns the list that typesof() - typepath makes (rules 10.4: "a DmList of Type, in DM type-tree order").
    public static DmList subtypesof(object typepath)
    {
        return Dm.sub(typesof(typepath), typepath) as DmList;
    }

    // tg: code/__DEFINES/_helpers.dm:42
    /// Takes a datum as input, returns its ref string
    public static string text_ref(object datum)
    {
        return @ref(datum);
    }
}
