// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/song_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the festival sect's song auras (the dull antimagic one and the bright one). The var blocks are generated (D111).

///Type of status effect applied by music played by the festival sect. Stacks upon itself, and removes all other song subtypes other than itself.
// tg: code/datums/status_effects/song_effects.dm:2
public partial class datum_status_effect_song : datum_status_effect
{
    // tg: code/datums/status_effects/song_effects.dm:7
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        owner.visible_message(span_notice(I18n.T("[] is coated with a [] aura!", owner, aura_desc)));
        //removes every other song subtype except itself
        foreach (object overridden_song_type in Dm.each(Dm.sub(subtypesof(typeof(datum_status_effect_song)), type)))
            owner.remove_status_effect(overridden_song_type);
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/song_effects.dm:14
    public override async DmTask<object> on_remove()
    {
        owner.visible_message(span_warning(I18n.T("[]'s [] aura fades away...", owner, aura_desc)));
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/status_effects/song_effects.dm:17
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        duration += Dm.initial(this, (datum_status_effect_song p) => p.duration); //slowly builds up, so the more times you get this status effect, the longer it lasts until it's gone.
        await DmTask.CompletedTask;
    }
}

// tg: code/datums/status_effects/song_effects.dm:20
public partial class datum_status_effect_song_antimagic : datum_status_effect_song
{
    // tg: code/datums/status_effects/song_effects.dm:26
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        ADD_TRAIT(owner, TRAIT_ANTIMAGIC, TRAIT_STATUS_EFFECT(id));
        playsound(owner, Dm.resource("sound/items/weapons/fwoosh.ogg"), 75, FALSE);
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/song_effects.dm:31
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_ANTIMAGIC, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }

    // tg: code/datums/status_effects/song_effects.dm:35
    public override object get_examine_text(mob examiner = null)
    {
        return span_notice(I18n.T("[] seem[] to be covered in a dull, grey aura.", owner.p_They(), owner.p_s()));
    }
}

// tg: code/datums/status_effects/song_effects.dm:38
public partial class datum_status_effect_song_light : datum_status_effect_song
{
    // tg: code/datums/status_effects/song_effects.dm:46
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        mob_light_obj = owner.mob_light(3, 1.5f, color: LIGHT_COLOR_DIM_YELLOW) as obj_effect_dummy_lighting_obj_moblight;
        playsound(owner, Dm.resource("sound/items/weapons/fwoosh.ogg"), 75, FALSE);
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/datums/status_effects/song_effects.dm:51
    public override async DmTask<object> on_remove()
    {
        QDEL_NULL(ref mob_light_obj);
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/datums/status_effects/song_effects.dm:54
public partial class datum_status_effect_song_light_song : datum_status_effect_song
{
    // tg: code/datums/status_effects/song_effects.dm:54  (tg's own path: the light song's examine text was declared on a new type)
    public override object get_examine_text(mob examiner = null)
    {
        return span_notice(I18n.T("[] seem[] to be covered in a glowing aura.", owner.p_They(), owner.p_s()));
    }
}
