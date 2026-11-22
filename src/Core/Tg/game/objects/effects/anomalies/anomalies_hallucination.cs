// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/anomalies_hallucination.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice, part 2 (D167): the whole file, the hallucination anomaly, its decoys and the supermatter's (no
// decoys). Its pulses are the hallucination system's (M8 slice G).
// The var blocks are generated (D111).

// tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:2
public partial class obj_effect_anomaly_hallucination : obj_effect_anomaly
{
    /// Messages sent to people feeling the pulses
    // tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:11  var/static/list/messages (a type-level static, rules 10.6)
    public static ref DmList messages => ref TgState.Current.obj_effect_anomaly_hallucination__messages;

    // tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:20
    [DmParams("mapload", "new_lifespan")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        apply_wibbly_filters(this);
        generate_decoys();
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:25
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.anomalyEffect(seconds_per_tick);
        ticks += Dm.num(seconds_per_tick);
        if (ticks < release_delay)
            return dot;
        ticks -= release_delay;
        if (!isturf(loc))
            return dot;

        await visible_hallucination_pulse(
            center: get_turf(this),
            radius: 5,
            hallucination_duration: 50 * SECONDS,
            hallucination_max_duration: 300 * SECONDS,
            optional_messages: messages);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:42
    public override async DmTask detonate()
    {
        if (!isturf(loc))
            return;

        await hallucination_pulse(
            center: get_turf(this),
            radius: 15,
            hallucination_duration: 50 * SECONDS,
            hallucination_max_duration: 300 * SECONDS,
            optional_messages: messages);
    }

    // tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:54
    public virtual void generate_decoys()
    {
        if (!Dm.truthy(spawn_decoys))
            return;

        foreach (turf floor in Dm.each<turf>(orange(1, this)))
        {
            if (prob(35))
                Dm.@new<obj_effect_anomaly_hallucination_decoy>(floor);
        }
    }
}

// tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:62
public partial class obj_effect_anomaly_hallucination_decoy : obj_effect_anomaly_hallucination
{
    // tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:67
    [DmParams("mapload", "new_lifespan")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_ILLUSORY_EFFECT, INNATE_TRAIT);
        report_text = pick(
            I18n.T("[]'s unstable field is fluctuating along frequency 9999999.99999, code 9999999.99999. No, no, that can't be right?", this),
            I18n.T("It doesn't detect anything. It awaits an input, as if you're pointing it towards nothing at all. What?"),
            I18n.T("The interface displays []. What the hell?", pick(I18n.T("a bad memory from your past"), I18n.T("the frequency numbers in a language you cannot read"), I18n.T("the first 15 digits of Pi"), I18n.T("yourself, from behind, angled at a 3/4ths isometric perspective"))),
            I18n.T("Nothing happens?"),
            I18n.T("It reports that you are a []. Huh?", pick("moron", "idiot", "cretin", "lowlife", "worthless denthead", "gump")),
            I18n.T("It tells you to try again, because you're doing it all wrong. What?"),
            I18n.T("It occurs to you that the anomaly you're scanning isn't actually there."),
            I18n.T("It's not working. You activate %TOOL% again. Still broken. You activate %TOOL%. You activate %TOOL%. Why isn't this working??"),
            I18n.T("Something happens. You can't tell what. The interface on %TOOL% remains blank."),
            I18n.T("What are you even trying to accomplish here? Did you really think that was going to work?"),
            I18n.T("Someone behind you whispers the frequency code to you, but you can't quite hear them. The interface on %TOOL% remains blank."),
            I18n.T("For a brief moment, you see yourself traversing a frozen forest, before snapping back to reality. The interface on %TOOL% remains blank."),
            I18n.T("Nothing interesting happens. Are you sure you're actually using it on anything?"),
            I18n.T("For a moment you can feel your skin falling off, then blink as the sensation vanishes. What the hell did that mean?"),
            I18n.T("The interface reports that you are a complete failure, and have screwed everything up again. Great work."),
            I18n.T("You realize that the formatting of this message is completely wrong, and get confused. Now why would that be?"),
            I18n.T("%TOOL% stares back at you. It looks dissapointed, its screen practically saying 'You missed the anomaly, you dolt. There's nothing there!'"),
            I18n.T("Nothing. Weird, maybe %TOOL% must be broken or something?"),
            I18n.T("You activate %TOOL%. You activate %TOOL%. You activate %TOOL%. You activate %TOOL%. You activate %TOOL%. You activate %TOOL%. You activate %TOOL%. Why isn't it working??")
        );
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:92
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        // tg: #ifndef UNIT_TESTS: the production branch (rules 1.7)
        if (SPT_PROB(move_chance, seconds_per_tick))
            await move_anomaly();
        return null;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:98
    public override object analyzer_act(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(I18n.T("You activate []. []", tool, replacetext(report_text, "%TOOL%", Dm.text("[]", tool)))));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:102
    public override async DmTask detonate()
    {
        do_sparks(3, source: this);
        return;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:106
    public override void generate_decoys()
    {
        return;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/anomalies/anomalies_hallucination.dm:11  var/static/list/messages = list(span_warning(...), ...)
    internal DmList obj_effect_anomaly_hallucination__messages = Dm.list(
        span_warning("You feel your conscious mind fall apart!"),
        span_warning("Reality warps around you!"),
        span_warning("Something's whispering around you!"),
        span_warning("You are going insane!"));
}
