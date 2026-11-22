// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/self/mime_vow.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/spells/spell_types/self/mime_vow.dm:1
public partial class datum_action_cooldown_spell_vow_of_silence : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/self/mime_vow.dm:15
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        ADD_TRAIT(grant_to, TRAIT_MIMING, Dm.text("[]", type));
        return dot;
    }

    // tg: code/modules/spells/spell_types/self/mime_vow.dm:19
    public override async DmTask Remove(mob remove_from = null)
    {
        await base.Remove(remove_from);
        REMOVE_TRAIT(remove_from, TRAIT_MIMING, Dm.text("[]", type));
    }

    // tg: code/modules/spells/spell_types/self/mime_vow.dm:23
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        if (!Dm.eq(await tgui_alert(usr, I18n.T("Are you sure? There's no going back."), I18n.T("Break Vow"), Dm.list("I'm Sure", "Abort")), "I'm Sure"))
            return SPELL_CANCEL_CAST;
        return await base.before_cast(cast_on);
    }

    // tg: code/modules/spells/spell_types/self/mime_vow.dm:28
    public override async DmTask<object> cast(atom cast_on_atom = null)
    {
        mob_living_carbon_human cast_on = cast_on_atom as mob_living_carbon_human;
        object dot = await base.cast(cast_on);
        to_chat(cast_on, span_notice(I18n.T("You break your vow of silence.")));
        cast_on.log_message(Dm.text("broke [] vow of silence.", cast_on.p_their()), LOG_GAME);
        await cast_on.add_mood_event("vow", typeof(datum_mood_event_broken_vow));
        REMOVE_TRAIT(cast_on, TRAIT_MIMING, Dm.text("[]", type));
        datum_job_mime mime_job = SSjob.get_job(JOB_MIME) as datum_job_mime;
        mime_job.total_positions += 1;
        qdel(this);
        return dot;
    }
}
