// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/festival/instrument_rites.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the festival god's rites (the holy violin, the portable tuning of an instrument, the five tuned songs with their
// effects on the listeners while the song plays and at its end). The var blocks are generated (D111).

// tg: code/modules/religion/festival/instrument_rites.dm:1
public partial class datum_religion_rites_holy_violin : datum_religion_rites
{
    // tg: code/modules/religion/festival/instrument_rites.dm:9
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        object dot = null;
        dot = await base.invoke_effect(user, religious_tool);
        turf tool_turf = get_turf(religious_tool);
        obj_item_instrument_violin fidis = Dm.@new<obj_item_instrument_violin_festival>(get_turf(religious_tool));
        fidis.visible_message(span_notice(I18n.T("[] appears!", fidis)));
        playsound(tool_turf, Dm.resource("sound/effects/pray.ogg"), 50, TRUE);
        return dot;
    }
}

// tg: code/modules/religion/festival/instrument_rites.dm:16
public partial class datum_religion_rites_portable_song_tuning : datum_religion_rites
{
    // tg: code/modules/religion/festival/instrument_rites.dm:26
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        foreach (obj_item_instrument could_empower in Dm.each<obj_item_instrument>(get_turf(religious_tool)))
        {
            instrument_target = could_empower;
            return await base.perform_rite(user, religious_tool);
        }
        to_chat(user, span_warning(I18n.T("You need to place an instrument on [] to do this!", religious_tool)));
        return FALSE;
    }

    // tg: code/modules/religion/festival/instrument_rites.dm:33
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        obj_item_instrument empower_target = instrument_target;
        turf tool_turf = get_turf(religious_tool);
        instrument_target = null;
        if (QDELETED(empower_target) || !(tool_turf == empower_target.loc)) //check if the instrument is still there
        {
            to_chat(user, span_warning(I18n.T("Your target left the altar!")));
            return FALSE;
        }
        empower_target.visible_message(span_notice(I18n.T("[] glows for a moment.", empower_target)));
        playsound(tool_turf, Dm.resource("sound/effects/pray.ogg"), 50, TRUE);
        DmList allowed_rites_from_bible = subtypesof(typeof(datum_religion_rites_song_tuner));
        await empower_target.AddComponent(
            typeof(datum_component_religious_tool),
            Dm.named("operation_flags", RELIGION_TOOL_INVOKE),
            Dm.named("force_catalyst_afterattack", FALSE),
            Dm.named("after_sect_select_cb", null),
            Dm.named("catalyst_type", typeof(obj_item_book_bible)),
            Dm.named("charges", 5),
            Dm.named("rite_types_allowlist", allowed_rites_from_bible));
        return TRUE;
    }
}

///prototype for rites that tune a song.
// tg: code/modules/religion/festival/instrument_rites.dm:56
public partial class datum_religion_rites_song_tuner : datum_religion_rites
{
    // tg: code/modules/religion/festival/instrument_rites.dm:73
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        object dot = null;
        dot = await base.invoke_effect(user, religious_tool);
        to_chat(user, span_notice(I18n.T("[]", song_invocation_message)));
        await user.AddComponent(typeof(datum_component_smooth_tunes), this, repeats_okay, particles_path, glow_color);
        return dot;
    }

    /**
     * Song effect applied when the performer starts playing.
     *
     * Arguments:
     * * performer - A human starting the song
     * * song_source - parent of the smooth_tunes component. This is limited to the compatible items of said component, which currently includes mobs and objects so we'll have to type appropriately.
     */
    // tg: code/modules/religion/festival/instrument_rites.dm:85
    public virtual void performer_start_effect(mob_living_carbon_human performer = null, atom song_source = null)
    {
        return;
    }

    /**
     * Perform the song effect.
     *
     * Arguments:
     * * listener - A mob, listening to the song
     * * song_source - parent of the smooth_tunes component. This is limited to the compatible items of said component, which currently includes mobs and objects so we'll have to type appropriately.
     */
    // tg: code/modules/religion/festival/instrument_rites.dm:95
    public virtual async DmTask song_effect(mob_living_carbon_human listener = null, atom song_source = null)
    {
        await DmTask.CompletedTask;
    }

    /**
     * When the song is long enough, it will have a special effect when it ends.
     *
     * If you want something that ALWAYS goes off regardless of song length, affix it to the Destroy proc. The rite is destroyed when smooth tunes is done.
     *
     * Arguments:
     * * listener - A mob, listening to the song
     * * song_source - parent of the smooth_tunes component. This is limited to the compatible items of said component, which currently includes mobs and objects so we'll have to type appropriately.
     */
    // tg: code/modules/religion/festival/instrument_rites.dm:107
    public virtual async DmTask finish_effect(mob_living_carbon_human listener = null, atom song_source = null)
    {
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/religion/festival/instrument_rites.dm:110
public partial class datum_religion_rites_song_tuner_evangelism : datum_religion_rites_song_tuner
{
    // tg: code/modules/religion/festival/instrument_rites.dm:119
    public override async DmTask song_effect(mob_living_carbon_human listener = null, atom song_source = null)
    {
        // A ckey requirement is good to have for gaining favor, to stop monkey farms and such.
        if (GLOB.religious_sect == null || Dm.truthy(listener.mind?.holy_role) || !Dm.truthy(listener.ckey))
            return;
        GLOB.religious_sect.adjust_favor(0.2f);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/religion/festival/instrument_rites.dm:125
    public override async DmTask finish_effect(mob_living_carbon_human listener = null, atom song_source = null)
    {
        await listener.add_mood_event("blessing", typeof(datum_mood_event_blessing));
    }
}

// tg: code/modules/religion/festival/instrument_rites.dm:128
public partial class datum_religion_rites_song_tuner_light : datum_religion_rites_song_tuner
{
    // tg: code/modules/religion/festival/instrument_rites.dm:140
    public override void performer_start_effect(mob_living_carbon_human performer = null, atom song_source = null)
    {
        performer_light_obj = performer.mob_light(8, 1.5f, color: LIGHT_COLOR_DIM_YELLOW) as obj_effect_dummy_lighting_obj_moblight;
    }

    // tg: code/modules/religion/festival/instrument_rites.dm:143
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref performer_light_obj);
        return base.Destroy(force);
    }

    // tg: code/modules/religion/festival/instrument_rites.dm:147
    public override async DmTask finish_effect(mob_living_carbon_human listener = null, atom song_source = null)
    {
        await listener.apply_status_effect(typeof(datum_status_effect_song_light));
    }
}

// tg: code/modules/religion/festival/instrument_rites.dm:150
public partial class datum_religion_rites_song_tuner_nullwave : datum_religion_rites_song_tuner
{
    // tg: code/modules/religion/festival/instrument_rites.dm:159
    public override async DmTask song_effect(mob_living_carbon_human listener = null, atom song_source = null)
    {
        await listener.apply_status_effect(typeof(datum_status_effect_song_antimagic));
    }
}

// tg: code/modules/religion/festival/instrument_rites.dm:162
public partial class datum_religion_rites_song_tuner_pain : datum_religion_rites_song_tuner
{
    // tg: code/modules/religion/festival/instrument_rites.dm:171
    public override async DmTask song_effect(mob_living_carbon_human listener = null, atom song_source = null)
    {
        float damage_dealt = 1;
        if (Dm.truthy(listener.mind?.holy_role))
            damage_dealt *= 0.5f;

        await listener.adjust_brute_loss(damage_dealt);
    }

    // tg: code/modules/religion/festival/instrument_rites.dm:178
    public override async DmTask finish_effect(mob_living_carbon_human listener = null, atom song_source = null)
    {
        obj_item_bodypart sliced_limb = pick(listener.get_bodyparts()) as obj_item_bodypart;
        await sliced_limb.force_wound_upwards(typeof(datum_wound_slash_flesh_moderate_many_cuts));
    }
}

// tg: code/modules/religion/festival/instrument_rites.dm:182
public partial class datum_religion_rites_song_tuner_lullaby : datum_religion_rites_song_tuner
{
    // tg: code/modules/religion/festival/instrument_rites.dm:201  var/static/list/sleepy_messages (proc-level static, rules 10.6)
    private static DmList song_effect__sleepy_messages => TgState.Current.datum_religion_rites_song_tuner_lullaby__song_effect__sleepy_messages ??= Dm.list(
        "The music is putting you to sleep...",
        "The music makes you nod off for a moment.",
        "You try to focus on staying awake through the song.");

    // tg: code/modules/religion/festival/instrument_rites.dm:194
    public override object Destroy(float? force = null)
    {
        listener_counter.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/religion/festival/instrument_rites.dm:198
    public override async DmTask song_effect(mob_living_carbon_human listener = null, atom song_source = null)
    {
        if (Dm.truthy(listener.mind?.holy_role))
            return;

        if (prob(20))
        {
            to_chat(listener, span_warning(I18n.T("[]", pick(song_effect__sleepy_messages))));
            await listener.emote("yawn");
        }
        await listener.set_eye_blur_if_lower(4 * SECONDS);
    }

    // tg: code/modules/religion/festival/instrument_rites.dm:213
    public override async DmTask finish_effect(mob_living_carbon_human listener = null, atom song_source = null)
    {
        to_chat(listener, span_danger(I18n.T("Wow, the ending of that song was... pretty...")));
        await listener.AdjustSleeping(5 * SECONDS);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/religion/festival/instrument_rites.dm:201  /datum/religion_rites/song_tuner/lullaby/song_effect's var/static/list/sleepy_messages
    internal DmList datum_religion_rites_song_tuner_lullaby__song_effect__sleepy_messages;
}
