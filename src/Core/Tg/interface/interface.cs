// Translated from tgstation (snapshot 1d99bd7f): interface/interface.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the hidden verb emote_panel (M9 slice B: the emote wheel's "More...", ClientSeam's verb code 13) and the changelog verb
// (M10x slice H: the lobby's Changelog button); the wiki, forum, rules, GitHub, report-issue and hotkeys-help verbs open web pages and
// windows the port does not have yet.

public partial class client : datum
{
    // tg: interface/interface.dm:95  GAME_VERB(/client, changelog, "Changelog", "OOC"): the body
    public virtual async DmTask __gvb_changelog(DmList structured_args = null)
    {
        if (GLOB.changelog_tgui == null)
            GLOB.changelog_tgui = Dm.@new<datum_changelog>();

        await GLOB.changelog_tgui.ui_interact(mob);
        if (!Dm.eq(prefs.lastchangelog, GLOB.changelog_hash))
        {
            prefs.lastchangelog = Dm.str(GLOB.changelog_hash);
            prefs.save_preferences();
        }
    }

    // tg: interface/interface.dm:95  the verb proc
    public virtual void changelog(params object[] __extra)
    {
        Waiting.Detach(__gvb_changelog(Dm.list()));
    }

    // tg: interface/interface.dm:112  GAME_VERB_HIDDEN(/client, emote_panel, "Emote Panel"): the body
    public virtual async DmTask __gvb_emote_panel(DmList structured_args = null)
    {
        if (!isliving(mob))
        {
            to_chat(mob, span_notice(I18n.T("You can only use this while you're alive!")));
            return;
        }

        if (GLOB.emote_panel == null)
            GLOB.emote_panel = Dm.@new<datum_emote_panel>();
        await GLOB.emote_panel.ui_interact(mob);
    }

    // tg: interface/interface.dm:112  the verb proc
    public virtual void emote_panel(params object[] __extra)
    {
        Waiting.Detach(__gvb_emote_panel(Dm.list()));
    }
}
