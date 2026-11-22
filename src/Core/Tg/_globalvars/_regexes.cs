// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/_regexes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 2: SANITIZE_FILENAME reads filename_forbidden_chars, and the scar texts are read through
// sanitize_filepath). GLOBAL_PROTECT is an admin guard (rules: not translated, the vars are not edited at run time).

//These are a bunch of regex datums for use /((any|every|no|some|head|foot)where(wolf)?\sand\s)+(\.[\.\s]+\s?where\?)?/i
public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/_regexes.dm:2  GLOBAL_DATUM_INIT(is_http_protocol, /regex, regex("^https?://"))
    public regex is_http_protocol;

    // tg: code/_globalvars/_regexes.dm:2  (GLOBAL_MANAGED(is_http_protocol, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalis_http_protocol()
    {
        is_http_protocol = Dm.regex("^https?://");
        Dm.append(ref gvars_datum_init_order, "is_http_protocol");
    }

    // tg: code/_globalvars/_regexes.dm:4  GLOBAL_DATUM_INIT(is_website, /regex, regex("http|www.|\[a-z0-9_-]+.(com|org|net|mil|edu)+", "i"))
    public regex is_website;

    // tg: code/_globalvars/_regexes.dm:4  (GLOBAL_MANAGED(is_website, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalis_website()
    {
        is_website = Dm.regex("http|www.|[a-z0-9_-]+.(com|org|net|mil|edu)+", "i");
        Dm.append(ref gvars_datum_init_order, "is_website");
    }

    // tg: code/_globalvars/_regexes.dm:5  GLOBAL_DATUM_INIT(is_email, /regex, regex("\[a-z0-9_-]+@\[a-z0-9_-]+.\[a-z0-9_-]+", "i"))
    public regex is_email;

    // tg: code/_globalvars/_regexes.dm:5  (GLOBAL_MANAGED(is_email, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalis_email()
    {
        is_email = Dm.regex("[a-z0-9_-]+@[a-z0-9_-]+.[a-z0-9_-]+", "i");
        Dm.append(ref gvars_datum_init_order, "is_email");
    }

    // tg: code/_globalvars/_regexes.dm:6  GLOBAL_DATUM_INIT(is_alphanumeric, /regex, regex("\[a-z0-9]+", "i"))
    public regex is_alphanumeric;

    // tg: code/_globalvars/_regexes.dm:6  (GLOBAL_MANAGED(is_alphanumeric, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalis_alphanumeric()
    {
        is_alphanumeric = Dm.regex("[a-z0-9]+", "i");
        Dm.append(ref gvars_datum_init_order, "is_alphanumeric");
    }

    // tg: code/_globalvars/_regexes.dm:7  GLOBAL_DATUM_INIT(is_punctuation, /regex, regex("\[.!?]+", "i"))
    public regex is_punctuation;

    // tg: code/_globalvars/_regexes.dm:7  (GLOBAL_MANAGED(is_punctuation, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalis_punctuation()
    {
        is_punctuation = Dm.regex("[.!?]+", "i");
        Dm.append(ref gvars_datum_init_order, "is_punctuation");
    }

    // tg: code/_globalvars/_regexes.dm:8  GLOBAL_DATUM_INIT(is_color, /regex, regex("^#\[0-9a-fA-F]{6}$"))
    public regex is_color;

    // tg: code/_globalvars/_regexes.dm:8  (GLOBAL_MANAGED(is_color, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalis_color()
    {
        is_color = Dm.regex("^#[0-9a-fA-F]{6}$");
        Dm.append(ref gvars_datum_init_order, "is_color");
    }

    // tg: code/_globalvars/_regexes.dm:9  GLOBAL_DATUM_INIT(is_alpha_color, /regex, regex("^#\[0-9a-fA-F]{8}$"))
    public regex is_alpha_color;

    // tg: code/_globalvars/_regexes.dm:9  (GLOBAL_MANAGED(is_alpha_color, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalis_alpha_color()
    {
        is_alpha_color = Dm.regex("^#[0-9a-fA-F]{8}$");
        Dm.append(ref gvars_datum_init_order, "is_alpha_color");
    }

    // tg: code/_globalvars/_regexes.dm:10  GLOBAL_DATUM_INIT(is_greyscale_colors, /regex, regex("^(#\[0-9a-fA-F]{6})+$"))
    public regex is_greyscale_colors;

    // tg: code/_globalvars/_regexes.dm:10  (GLOBAL_MANAGED(is_greyscale_colors, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalis_greyscale_colors()
    {
        is_greyscale_colors = Dm.regex("^(#[0-9a-fA-F]{6})+$");
        Dm.append(ref gvars_datum_init_order, "is_greyscale_colors");
    }

    //finds text strings recognized as links on discord. Mainly used to stop embedding.
    // tg: code/_globalvars/_regexes.dm:13  GLOBAL_DATUM_INIT(has_discord_embeddable_links, /regex, regex("(https?://\[^\\s|<\]{2,})"))
    public regex has_discord_embeddable_links;

    // tg: code/_globalvars/_regexes.dm:13  (GLOBAL_MANAGED(has_discord_embeddable_links, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhas_discord_embeddable_links()
    {
        has_discord_embeddable_links = Dm.regex("(https?://[^\\s|<]{2,})");
        Dm.append(ref gvars_datum_init_order, "has_discord_embeddable_links");
    }

    //All < and > characters
    // tg: code/_globalvars/_regexes.dm:16  GLOBAL_DATUM_INIT(angular_brackets, /regex, regex(@"[<>]", "g"))
    public regex angular_brackets;

    // tg: code/_globalvars/_regexes.dm:16  (GLOBAL_MANAGED(angular_brackets, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalangular_brackets()
    {
        angular_brackets = Dm.regex(@"[<>]", "g");
        Dm.append(ref gvars_datum_init_order, "angular_brackets");
    }

    //All characters between < a > inclusive of the bracket
    // tg: code/_globalvars/_regexes.dm:19  GLOBAL_DATUM_INIT(html_tags, /regex, regex(@"<.*?>", "g"))
    public regex html_tags;

    // tg: code/_globalvars/_regexes.dm:19  (GLOBAL_MANAGED(html_tags, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhtml_tags()
    {
        html_tags = Dm.regex(@"<.*?>", "g");
        Dm.append(ref gvars_datum_init_order, "html_tags");
    }

    //All characters forbidden by filenames: ", \, \n, \t, /, ?, %, *, :, |, <, >, ..
    // tg: code/_globalvars/_regexes.dm:22  GLOBAL_DATUM_INIT(filename_forbidden_chars, /regex, regex(@{""|[\\\n\t/?%*:|<>]|\.\."}, "g"))
    public regex filename_forbidden_chars;

    // tg: code/_globalvars/_regexes.dm:22  (GLOBAL_MANAGED(filename_forbidden_chars, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfilename_forbidden_chars()
    {
        filename_forbidden_chars = Dm.regex(@"""|[\\\n\t/?%*:|<>]|\.\.", "g");
        Dm.append(ref gvars_datum_init_order, "filename_forbidden_chars");
    }
    // had to use the OR operator for quotes instead of putting them in the character class because it breaks the syntax highlighting otherwise.

    //Find AREACOORD() in log_mapping messages
    // tg: code/_globalvars/_regexes.dm:27  GLOBAL_DATUM_INIT(test_areacoord_regex, /regex, regex(@"\(-?\d+,-?\d+,(-?\d+)\)"))
    public regex test_areacoord_regex;

    // tg: code/_globalvars/_regexes.dm:27  (GLOBAL_MANAGED(test_areacoord_regex, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltest_areacoord_regex()
    {
        test_areacoord_regex = Dm.regex(@"\(-?\d+,-?\d+,(-?\d+)\)");
        Dm.append(ref gvars_datum_init_order, "test_areacoord_regex");
    }
}
