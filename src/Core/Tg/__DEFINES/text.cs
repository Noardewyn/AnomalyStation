// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/text.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only LOWER_TEXT translated (the configuration and its entries use it, boot package, wave 3) and SANITIZE_FILENAME
// (M6 slice 2: sanitize_filepath, which the scar texts read through), ALPHABET, VOWELS and CONSONANTS (gaps P3: the speech mutations),
// STRIP_HTML_FULL (gaps SC2: the records consoles); the other macros of
// text.dm are translated with the code that uses them.
// M2 P1 (the living core) added the members of its paths, marked "M2 P1" below.

public static partial class GlobalProcs
{
    /// BYOND's string procs don't support being used on datum references (as in it doesn't look for a name for stringification)
    /// We just use this macro to ensure that we will only pass strings to this BYOND-level function without developers needing to really worry about it.
    // tg: code/__DEFINES/text.dm:69  #define LOWER_TEXT(thing) lowertext(UNLINT("[thing]"))
    public static string LOWER_TEXT(object thing) => lowertext(Dm.text("[]", thing));

    /// Removes characters incompatible with file names.
    // tg: code/__DEFINES/text.dm:53  #define SANITIZE_FILENAME(text) (GLOB.filename_forbidden_chars.Replace(text, ""))
    public static object SANITIZE_FILENAME(object text) => GLOB.filename_forbidden_chars.Replace(text, "");

    /// Removes everything enclose in < and > inclusive of the bracket, and limits the length of the message.
    // tg: code/__DEFINES/text.dm:59  #define STRIP_HTML_FULL(text, limit) (GLOB.html_tags.Replace(copytext(text, 1, limit), ""))  (gaps SC2: the records consoles)
    public static object STRIP_HTML_FULL(object text, object limit) => GLOB.html_tags.Replace(copytext(text, 1, Dm.num(limit)), "");

    // M2 P1 (living core):
    // tg: code/__DEFINES/text.dm:6  #define MAPTEXT(text) {"<span class='maptext'>[##text]</span>"}
    public static string MAPTEXT(object text) => Dm.text("<span class='maptext'>[]</span>", text);

    // tg: code/__DEFINES/text.dm:20  #define MAPTEXT_PIXELLARI(text) {"<span style='font-family: \"Pixellari\"; font-size: 12pt; -dm-text-outline: 1px black'>[##text]</span>"}
    public static string MAPTEXT_PIXELLARI(object text) => Dm.text("<span style='font-family: \"Pixellari\"; font-size: 12pt; -dm-text-outline: 1px black'>[]</span>", text);

    // tg: code/__DEFINES/text.dm:23  #define MAPTEXT_GRAND9K(text) {"<span style='font-family: \"Grand9K Pixel\"; font-size: 6pt; -dm-text-outline: 1px black'>[##text]</span>"}  (gaps ST5: the signboards)
    public static string MAPTEXT_GRAND9K(object text) => Dm.text("<span style='font-family: \"Grand9K Pixel\"; font-size: 6pt; -dm-text-outline: 1px black'>[]</span>", text);

    // tg: code/__DEFINES/text.dm:26  #define MAPTEXT_TINY_UNICODE(text) {"<span style='font-family: \"TinyUnicode\"; font-size: 12pt; line-height: 0.75; -dm-text-outline: 1px black'>[##text]</span>"}
    public static string MAPTEXT_TINY_UNICODE(object text) => Dm.text("<span style='font-family: \"TinyUnicode\"; font-size: 12pt; line-height: 0.75; -dm-text-outline: 1px black'>[]</span>", text);

    // tg: code/__DEFINES/text.dm:29  #define MAPTEXT_SPESSFONT(text) {"<span style='font-family: \"Spess Font\"; font-size: 6pt; line-height: 1.4; -dm-text-outline: 1px black'>[##text]</span>"}  (gaps XB1: the slime console's display)
    public static string MAPTEXT_SPESSFONT(object text) => Dm.text("<span style='font-family: \"Spess Font\"; font-size: 6pt; line-height: 1.4; -dm-text-outline: 1px black'>[]</span>", text);

    // tg: code/__DEFINES/text.dm:114  #define ALPHABET list(...): a new list at every use (gaps P3, D327)
    public static DmList ALPHABET() => Dm.list("a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z");

    // tg: code/__DEFINES/text.dm:115  #define VOWELS list("a", "e", "i", "o", "u")
    public static DmList VOWELS() => Dm.list("a", "e", "i", "o", "u");

    // tg: code/__DEFINES/text.dm:116  #define CONSONANTS (ALPHABET - VOWELS)
    public static DmList CONSONANTS() => Dm.sub(ALPHABET(), VOWELS()) as DmList;
}
