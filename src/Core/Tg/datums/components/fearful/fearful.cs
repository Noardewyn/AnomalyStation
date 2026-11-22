// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/fearful/fearful.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3b, D316: the fear system the phobias run on). The var block is generated (D111).

/// Fearful component: provides optional handling of fears and phobias for mob's mood
/// Can be applied from multiple sources, and essentially serves as a central controller for fear datums described below
// tg: code/datums/components/fearful/fearful.dm:4
public partial class datum_component_fearful : datum_component
{
    /*
     * handler_types - terror_handler(s) to add to the mob
     * initial_buildup - amount of fear to add to mob from getting scared shitless by whatever added the component
     * add_defaults - should terror handlers marked as "default" be added to the mob?
     */
    // tg: code/datums/components/fearful/fearful.dm:21
    [DmParams("handler_types", "initial_buildup", "add_defaults")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object initial_buildup = Dm.arg(__extra, 1);
        object add_defaults = Dm.arg(__extra, 2) ?? TRUE;
        object dot = await base.Initialize(__extra);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        START_PROCESSING(SSdcs, this);
        terror_buildup = Dm.num(initial_buildup);

        if (!Dm.truthy(add_defaults))
            return dot;

        foreach (Type handler in Dm.each_cast<Type>(subtypesof(typeof(datum_terror_handler))))
        {
            if (!Dm.truthy(Dm.initial(handler, (datum_terror_handler p) => p.@default)))
                continue;
            add_handler(handler, "default");
        }
        return dot;
    }

    // tg: code/datums/components/fearful/fearful.dm:37
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSdcs, this);
        QDEL_LIST(terror_handlers);
        return base.Destroy(force);
    }

    // tg: code/datums/components/fearful/fearful.dm:42
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_CARBON_PRE_MISC_HELP, nameof(comfort_owner));
        RegisterSignal(parent, SIGNAL_ADDTRAIT(TRAIT_FEARLESS), nameof(fearless_added));
        RegisterSignal(parent, COMSIG_CARBON_MOOD_CHECK, nameof(on_mood_check));
    }

    // tg: code/datums/components/fearful/fearful.dm:49
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_CARBON_PRE_MISC_HELP, SIGNAL_ADDTRAIT(TRAIT_FEARLESS), COMSIG_CARBON_MOOD_CHECK));
    }

    // tg: code/datums/components/fearful/fearful.dm:53
    [DmParams("source", "handler_types", "initial_buildup", "add_defaults")]
    public override async DmTask<object> on_source_add(object source = null, params object[] __extra)
    {
        object handler_types = Dm.arg(__extra, 0);
        object initial_buildup = Dm.arg(__extra, 1);
        object dot = await base.on_source_add(source, __extra);
        terror_buildup = clamp(terror_buildup + Dm.num(initial_buildup), 0, TERROR_BUILDUP_MAXIMUM);
        foreach (object handler_type in Dm.each(handler_types))
            add_handler(handler_type, source);
        return dot;
    }

    // tg: code/datums/components/fearful/fearful.dm:59
    public override void on_source_remove(object source = null)
    {
        foreach (datum_terror_handler handler in Dm.each_cast<datum_terror_handler>(terror_handlers))
        {
            Dm.remove(terror_handlers[handler] as DmList, source);
            if (Dm.truthy(length(terror_handlers[handler])))
                continue;
            Dm.remove(terror_handlers, handler);
            foreach (object override_type in Dm.each(handler.overrides))
            {
                if (!Dm.truthy(overriden_handlers[override_type]))
                    continue;
                Dm.remove(overriden_handlers[override_type] as DmList, handler.type);
                if (!Dm.truthy(length(overriden_handlers[override_type])))
                    Dm.remove(overriden_handlers, override_type);
            }
            qdel(handler);
        }
        base.on_source_remove(source);
    }

    // tg: code/datums/components/fearful/fearful.dm:74
    public virtual object add_handler(object handler_type = null, object source = null)
    {
        foreach (datum_terror_handler existing in Dm.each_cast<datum_terror_handler>(terror_handlers))
        {
            if (Dm.eq(existing.type, handler_type) && !Dm.truthy(existing.bespoke))
            {
                Dm.append(terror_handlers[existing] as DmList, source);
                return null;
            }
        }

        var handler = Dm.@new((Type)handler_type, parent, this) as datum_terror_handler;
        terror_handlers[handler] = Dm.list(source);
        foreach (object override_type in Dm.each(handler.overrides))
        {
            if (!Dm.truthy(overriden_handlers[override_type]))
                overriden_handlers[override_type] = Dm.list();
            ((DmList)overriden_handlers[override_type])[handler_type] = TRUE;
        }
        return handler;
    }

    // tg: code/datums/components/fearful/fearful.dm:88
    public virtual object get_fear_multiplier()
    {
        float multiplier = 1;
        // HAS_PERSONALITY(parent_mob, cowardly / paranoid / brave): x1.25, x1.10, x0.75; the personalities are not ported (M10+, with the
        // moods), so no mob has one
        return multiplier;
    }

    // tg: code/datums/components/fearful/fearful.dm:99
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        float fear_modifier = Dm.num(get_fear_multiplier());
        float terror_adjustment = 0;
        DmList tick_later = Dm.list();
        foreach (datum_terror_handler handler in Dm.each_cast<datum_terror_handler>(terror_handlers))
        {
            if (Dm.truthy(overriden_handlers[handler.type]))
                continue;
            if (Dm.eq(handler.handler_type, TERROR_HANDLER_EFFECT))
            {
                Dm.append(tick_later, handler);
                continue;
            }
            float adjustment = Dm.num(await handler.tick(seconds_per_tick, terror_buildup));
            terror_buildup = clamp(terror_buildup + (adjustment * fear_modifier), 0, TERROR_BUILDUP_MAXIMUM);
            terror_adjustment += adjustment;
        }

        foreach (datum_terror_handler handler in Dm.each_cast<datum_terror_handler>(tick_later))
        {
            float adjustment = Dm.num(await handler.tick(seconds_per_tick, terror_buildup));
            terror_buildup = clamp(terror_buildup + (adjustment * fear_modifier), 0, TERROR_BUILDUP_MAXIMUM);
            terror_adjustment += adjustment;
        }

        // If we gained terror in any way, don't tick it down
        if (terror_adjustment > 0 || terror_buildup > last_tick_buildup)
        {
            last_tick_buildup = terror_buildup;
            return null;
        }

        // Tick terror down while we're not being actively spooked
        if (terror_buildup > 0)
            terror_buildup = max(terror_buildup - (TERROR_BUILDUP_PASSIVE_DECREASE * (1 / fear_modifier)) * Dm.num(seconds_per_tick), 0);
        last_tick_buildup = terror_buildup;
        return null;
    }

    // tg: code/datums/components/fearful/fearful.dm:128
    [SignalHandler]
    public virtual void on_examine(mob_living source = null, mob user = null, DmList examine_list = null)
    {
        if (IS_UNCONSCIOUS(source))
            return;

        if (terror_buildup >= TERROR_BUILDUP_HEART_ATTACK)
            Dm.append(examine_list, span_danger(I18n.T("[] [] seizing up, about to collapse in fear!", source.p_They(), source.p_are())));
        else if (terror_buildup > TERROR_BUILDUP_PANIC)
            Dm.append(examine_list, span_boldwarning(I18n.T("[] [] trembling and shaking, barely standing upright!", source.p_They(), source.p_are())));
        else if (terror_buildup >= TERROR_BUILDUP_TERROR)
            Dm.append(examine_list, span_boldwarning(I18n.T("[] is visibly trembling and twitching. [] [] clearly in distress!", source, source.p_They(), source.p_are())));
        else if (terror_buildup >= TERROR_BUILDUP_FEAR)
            Dm.append(examine_list, span_warning(I18n.T("[] looks very worried about something. [] [] alright?", source, capitalize(source.p_are()), source.p_they())));
        else if (Dm.truthy(terror_buildup))
            Dm.append(examine_list, span_smallnotice(I18n.T("[] looks rather anxious. [] could probably use a hug...", source, source.p_They())));
    }

    // tg: code/datums/components/fearful/fearful.dm:145
    [SignalHandler]
    public virtual object comfort_owner(mob_living_carbon source = null, mob_living hugger = null)
    {
        if (hugger == parent)
            return null;

        if (HAS_TRAIT(hugger, TRAIT_NIGHTMARISH))
        {
            float lit_tiles = 0;
            float unlit_tiles = 0;

            foreach (turf_open turf_to_check in Dm.each<turf_open>(Builtins.range(1, source)))
            {
                if (Dm.truthy(turf_to_check.check_lumcount_above(LIGHTING_TILE_IS_DARK)))
                    lit_tiles++;
                else
                    unlit_tiles++;
            }

            if (lit_tiles < unlit_tiles)
            {
                Waiting.NoSleep(source.Knockdown(0.5f * SECONDS));
                terror_buildup += HUG_TERROR_AMOUNT;
                source.visible_message(
                    span_warning(I18n.T("[] recoils in fear as [] waves [] arms and shrieks at []!", source, hugger, hugger.p_their(), source.p_them())),
                    span_boldwarning(I18n.T("The shadows lash out at you, and you drop to the ground in fear!")),
                    span_hear(I18n.T("You hear someone shriek in fear. How embarassing!")));
                return COMPONENT_BLOCK_MISC_HELP;
            }
        }

        float hug_buildup = 0;
        foreach (datum_terror_handler handler in Dm.each_cast<datum_terror_handler>(terror_handlers))
            hug_buildup += Dm.num(handler.on_hug(hugger));

        if (hug_buildup > 0)
        {
            terror_buildup += hug_buildup;
            source.visible_message(
                span_warning(I18n.T("[] recoils in fear as [] attempts to hug []!", source, hugger, source.p_them())),
                span_boldwarning(I18n.T("You recoil in terror as [] attempts to hug you!", hugger)),
                span_hear(I18n.T("You hear someone shriek in fear. How embarassing!")));
            return COMPONENT_BLOCK_MISC_HELP;
        }

        if (terror_buildup >= TERROR_BUILDUP_TERROR)
        {
            source.visible_message(
                span_notice(I18n.T("[] seems to relax as [] gives [] a comforting hug.", source, hugger, source.p_them())),
                span_nicegreen(I18n.T("You feel yourself calm down as [] gives you a reassuring hug.", hugger)),
                span_hear(I18n.T("You hear shuffling and a sigh of relief.")));
        }
        terror_buildup -= HUG_TERROR_AMOUNT;
        return null;
    }

    /// Remove all terror buildup when we become fearless
    // tg: code/datums/components/fearful/fearful.dm:193
    [SignalHandler]
    public virtual void fearless_added(datum source = null, params object[] __extra)
    {
        terror_buildup = 0;
    }

    // tg: code/datums/components/fearful/fearful.dm:197
    [SignalHandler]
    public virtual void on_mood_check(mob_living source = null, DmList mood_list = null)
    {
        if (terror_buildup >= TERROR_BUILDUP_HEART_ATTACK)
            Dm.append(mood_list, span_boldwarning(I18n.T("You are about to collapse in fear!")));
        else if (terror_buildup > TERROR_BUILDUP_PANIC)
            Dm.append(mood_list, span_boldwarning(I18n.T("You are shaking in fear!")));
        else if (terror_buildup >= TERROR_BUILDUP_TERROR)
            Dm.append(mood_list, span_warning(I18n.T("You are trembling in fear.")));
        else if (terror_buildup >= TERROR_BUILDUP_FEAR)
            Dm.append(mood_list, span_warning(I18n.T("You feel scared.")));
        else if (Dm.truthy(terror_buildup))
            Dm.append(mood_list, span_notice(I18n.T("You feel on the edge.")));
    }
}
