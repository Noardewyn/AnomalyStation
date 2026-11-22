// Translated from tgstation (snapshot 1d99bd7f): code/datums/browser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10x slice G6: the player's ticket panel): /datum/browser's page and its open and close through the browse seam
// (ClientSeam.Browse, DV216). Not translated: the stylesheets, scripts and common.css of the asset cache (add_stylesheet, add_script:
// the client's window has the game's look), the window scaling, setup_onclose (BYOND's winexists and onclose), the modal browsers and
// tg_alert (deprecated in tg for tgui_alert), the preference browser and the number input below them.

// tg: code/datums/browser.dm:1
public partial class datum_browser : datum
{
    // tg: code/datums/browser.dm:18
    public override async DmTask New(params object[] __extra)
    {
        object user = __extra.Length > 0 ? __extra[0] : null;
        object window_id = __extra.Length > 1 ? __extra[1] : null;
        object title = __extra.Length > 2 ? __extra[2] : "";
        object width = __extra.Length > 3 ? __extra[3] : 0;
        object height = __extra.Length > 4 ? __extra[4] : 0;
        atom source = __extra.Length > 5 ? __extra[5] as atom : null;
        if (user is client client_user) // IS_CLIENT_OR_MOCK(user)
            user = client_user.mob;
        this.user = user as mob;
        RegisterSignal(this.user, COMSIG_QDELETING, nameof(user_deleted));
        this.window_id = window_id;
        if (Dm.truthy(title))
            this.title = Dm.str(format_text(title));
        if (Dm.truthy(width))
            this.width = Dm.num(width);
        if (Dm.truthy(height))
            this.height = Dm.num(height);
        if (source != null)
            source_ref = WEAKREF(source) as datum_weakref;
        await DmTask.CompletedTask;
    }

    // tg: code/datums/browser.dm:34
    public virtual void user_deleted(datum source = null)
    {
        user = null;
    }

    // tg: code/datums/browser.dm:38
    public virtual void add_head_content(object head_content = null) => this.head_content += Dm.str(head_content);

    // tg: code/datums/browser.dm:41
    public virtual void set_head_content(object head_content = null) => this.head_content = Dm.str(head_content);

    // tg: code/datums/browser.dm:44
    public virtual void set_window_options(object window_options = null) => this.window_options = Dm.str(window_options);

    // tg: code/datums/browser.dm:66
    public virtual void set_content(object content = null) => this.content = Dm.str(content);

    // tg: code/datums/browser.dm:69
    public virtual void add_content(object content = null) => this.content += Dm.str(content);

    // tg: code/datums/browser.dm:72
    public virtual string get_header()
    {
        // the common.css link, the stylesheets, the window scaling's zoom and the scripts: the asset cache (not translated)
        return Dm.text("<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01 Transitional//EN\" \"http://www.w3.org/TR/html4/loose.dtd\">\n<html>\n\t<head>\n"
            + "\t\t<meta http-equiv='Content-Type' content='text/html; charset=UTF-8'>\n\t\t<meta http-equiv='X-UA-Compatible' content='IE=edge'>\n"
            + "\t\t[]\n\t</head>\n\t<body scroll=auto>\n\t\t<div class='uiWrapper'>\n\t\t\t[]\n\t\t\t<div class='uiContent'>\n\t",
            head_content, Dm.truthy(title) ? Dm.text("<div class='uiTitleWrapper'><div class='uiTitle'><tt>[]</tt></div></div>", title) : "");
    }

    // tg: code/datums/browser.dm:107
    public virtual string get_footer() => "\n\t\t\t</div>\n\t\t</div>\n\t</body>\n</html>";

    // tg: code/datums/browser.dm:114
    public virtual string get_content() => Dm.text("\n\t\t[]\n\t\t[]\n\t\t[]\n\t", get_header(), content, get_footer());

    // tg: code/datums/browser.dm:120
    public virtual void open(object use_on_close = null)
    {
        if (window_id == null) //null check because this can potentially nuke goonchat
        {
            warning(Dm.text("Browser [] tried to open with a null ID in code/datums/browser.dm at line 123 src: [] usr: [].", title, this, usr)); // WARNING(MSG)
            to_chat(user, span_userdanger(Dm.text("The [] browser you tried to open failed a sanity check! Please report this on GitHub!", title)));
            return;
        }

        string window_size = "";
        if (Dm.truthy(width) && Dm.truthy(height))
            window_size = Dm.text("size=[]x[];", width, height); // the ui_scale preference's window scaling: not translated

        // common_asset.send(user) and the stylesheets' and scripts' send_assets: the asset cache (not translated)
        ClientSeam.Browse(user, get_content(), Dm.text("window=[];[][]", window_id, window_size, window_options)); // DIRECT_OUTPUT(user, browse(...))
        // if (use_on_close) setup_onclose(): BYOND's winexists and onclose (not translated)
    }

    // tg: code/datums/browser.dm:155
    public virtual void close()
    {
        if (window_id != null) //null check because this can potentially nuke goonchat
            ClientSeam.Browse(user, null, Dm.text("window=[]", window_id)); // user << browse(null, ...)
        else
            warning(Dm.text("Browser [] tried to close with a null ID in code/datums/browser.dm at line 159 src: [] usr: [].", title, this, usr)); // WARNING(MSG)
    }
}
