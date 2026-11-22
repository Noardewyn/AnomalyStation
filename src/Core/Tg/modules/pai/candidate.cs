// Translated from tgstation (snapshot 1d99bd7f): code/modules/pai/candidate.dm and personality.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole files. DEVIATION(DV389): the personality is kept in tg's JSON savefile datum (pai.json) instead of a
// BYOND /savefile (pai.sav), which the shim has not got; the keys and the version check are tg's.

// tg: code/modules/pai/candidate.dm:7
public partial class datum_pai_candidate : datum
{
    // tg: code/modules/pai/candidate.dm:19
    [DmParams("ckey")]
    public override async DmTask New(params object[] __extra)
    {
        ckey = Dm.arg(__extra, 0);
        await DmTask.CompletedTask;
    }

    /**
     * Checks if a candidate is ready so that they may be displayed or
     * downloaded. Removes any invalid entries.
     *
     * @returns {boolean} - TRUE if the candidate is ready, FALSE if not
     */
    // tg: code/modules/pai/candidate.dm:28
    public virtual object check_ready()
    {
        mob candidate_mob = get_mob_by_key(ckey);
        if (candidate_mob?.client == null || !isobserver(candidate_mob) || Dm.truthy(is_banned_from(ckey, ROLE_PAI)))
        {
            Dm.remove(SSpai.candidates, ckey);
            return FALSE;
        }
        if (!Dm.truthy(ready))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/pai/personality.dm:10
    public virtual string savefile_path(mob user = null)
    {
        string key = Dm.str(user.ckey);
        return Dm.text("data/player_saves/[]/[]/pai.json", key.Length > 0 ? key[..1] : "", key);
    }

    // tg: code/modules/pai/personality.dm:13
    public virtual object savefile_save(mob user = null)
    {
        mob usr = World.Current.usr as mob;
        if (is_guest_key(user.key))
        {
            to_chat(usr, span_warning(I18n.T("You cannot save pAI information as a guest.")));
            return FALSE;
        }
        var F = Dm.@new<datum_json_savefile>(savefile_path(user));
        F.set_entry("name", name);
        F.set_entry("description", description);
        F.set_entry("comments", comments);
        F.set_entry("version", 1);
        F.save();
        to_chat(usr, span_boldnotice(I18n.T("You have saved pAI information locally.")));
        return TRUE;
    }

    // loads the savefile corresponding to the mob's ckey
    // if silent=true, report incompatible savefiles
    // returns TRUE if loaded (or file was incompatible)
    // returns FALSE if savefile did not exist
    // tg: code/modules/pai/personality.dm:30
    public virtual async DmTask<object> savefile_load(mob user = null, object silent = null)
    {
        silent ??= TRUE;
        if (is_guest_key(user.key))
            return FALSE;

        string path = savefile_path(user);

        if (!Dm.truthy(fexists(path)))
            return FALSE;

        var F = Dm.@new<datum_json_savefile>(path);

        object version = F.get_entry("version");

        if (version == null || !Dm.eq(version, 1))
        {
            fdel(path);
            if (!Dm.truthy(silent))
                await tgui_alert(user, "Your savefile was incompatible with this version and was deleted.");
            return FALSE;
        }

        name = F.get_entry("name");
        description = F.get_entry("description");
        comments = F.get_entry("comments");
        return TRUE;
    }
}
