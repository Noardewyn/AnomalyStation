// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/irradiated.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 radiation slice: the whole file. The var blocks of the component and of /atom/movable/screen/alert/irradiated are
// generated (D111). The green glow is tg's outline filter, drawn and animated by the client (D155, D156). The geiger counter's
// chat icon (icon2html) is a deferred leaf (chat icons, M9).

/// This atom is irradiated, and will glow green.
/// Humans will take toxin damage until all their toxin damage is cleared.
// tg: code/datums/components/irradiated.dm:15
public partial class datum_component_irradiated : datum_component
{
    // tg: code/datums/components/irradiated.dm:1
    private const float RADIATION_IMMEDIATE_TOX_DAMAGE = 10;
    // tg: code/datums/components/irradiated.dm:3
    private const float RADIATION_TOX_DAMAGE_PER_INTERVAL = 2;
    // tg: code/datums/components/irradiated.dm:4
    private const float RADIATION_TOX_INTERVAL = 25 * SECONDS;
    // tg: code/datums/components/irradiated.dm:6
    private const float RADIATION_BURN_SPLOTCH_DAMAGE = 11;
    // tg: code/datums/components/irradiated.dm:7
    private const float RADIATION_BURN_INTERVAL_MIN = 30 * SECONDS;
    // tg: code/datums/components/irradiated.dm:8
    private const float RADIATION_BURN_INTERVAL_MAX = 60 * SECONDS;
    // Showers process on SSmachines
    // tg: code/datums/components/irradiated.dm:11  #define RADIATION_CLEAN_IMMUNITY_TIME (SSMACHINES_DT + (1 SECONDS)), with SSMACHINES_DT
    // (SSmachines.wait/10, __DEFINES/subsystems.dm:276) inline; rules 10.6 for a test harness world without SSmachines
    private static float RADIATION_CLEAN_IMMUNITY_TIME => (Dm.num(SSmachines?.wait) / 10) + (1 * SECONDS);

    // tg: code/datums/components/irradiated.dm:25
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!CAN_IRRADIATE(parent))
            return COMPONENT_INCOMPATIBLE;

        // This isn't incompatible, it's just wrong
        if (HAS_TRAIT(parent, TRAIT_RADIMMUNE))
        {
            qdel(this);
            return null;
        }

        ADD_TRAIT(parent, TRAIT_IRRADIATED, REF(this));

        create_glow();

        beginning_of_irradiation = world.time;

        if (ishuman(parent))
        {
            mob_living_carbon_human human_parent = (mob_living_carbon_human)parent;
            await human_parent.apply_damage(RADIATION_IMMEDIATE_TOX_DAMAGE, TOX);
            START_PROCESSING(SSobj, this);

            last_tox_damage = world.time + RADIATION_TOX_INTERVAL; // COOLDOWN_START (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))

            start_burn_splotch_timer();

            human_parent.throw_alert(ALERT_IRRADIATED, typeof(atom_movable_screen_alert_irradiated));
        }
        return null;
    }

    // tg: code/datums/components/irradiated.dm:51
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_clean));
        RegisterSignal(parent, COMSIG_GEIGER_COUNTER_SCAN, nameof(on_geiger_counter_scan));
        RegisterSignal(parent, COMSIG_LIVING_HEALTHSCAN, nameof(on_healthscan));
    }

    // tg: code/datums/components/irradiated.dm:56
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_COMPONENT_CLEAN_ACT,
            COMSIG_GEIGER_COUNTER_SCAN,
            COMSIG_LIVING_HEALTHSCAN
        ));
    }

    // tg: code/datums/components/irradiated.dm:63
    public override object Destroy(float? force = null)
    {
        atom_movable parent_movable = parent as atom_movable;
        if (parent_movable != null)
            parent_movable.remove_filter("rad_glow");

        mob_living_carbon_human human_parent = parent as mob_living_carbon_human;
        if (human_parent != null)
            human_parent.clear_alert(ALERT_IRRADIATED);

        REMOVE_TRAIT(parent, TRAIT_IRRADIATED, REF(this));

        deltimer(burn_splotch_timer_id);
        STOP_PROCESSING(SSobj, this);

        return base.Destroy(force);
    }

    // tg: code/datums/components/irradiated.dm:79
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        if (!ishuman(parent))
            return PROCESS_KILL;

        if (HAS_TRAIT(parent, TRAIT_RADIMMUNE))
        {
            qdel(this);
            return PROCESS_KILL;
        }

        mob_living_carbon_human human_parent = (mob_living_carbon_human)parent;
        if (Dm.num(human_parent.get_tox_loss()) == 0)
        {
            qdel(this);
            return PROCESS_KILL;
        }

        if (Dm.truthy(should_halt_effects(human_parent)))
            return null;

        if (human_parent.stat != DEAD)
        {
            datum_species species = human_parent.dna?.species as datum_species;
            if (species != null)
                await species.handle_radiation(human_parent, world.time - Dm.num(beginning_of_irradiation), seconds_per_tick);
        }

        await process_tox_damage(human_parent, seconds_per_tick);
        return null;
    }

    // tg: code/datums/components/irradiated.dm:100
    public virtual object should_halt_effects(mob_living_carbon_human target = null)
    {
        if (HAS_TRAIT(target, TRAIT_STASIS))
            return TRUE;

        if (HAS_TRAIT(target, TRAIT_HALT_RADIATION_EFFECTS))
            return TRUE;

        if (!(clean_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return TRUE;

        return FALSE;
    }

    // tg: code/datums/components/irradiated.dm:112
    public virtual async DmTask process_tox_damage(mob_living_carbon_human target = null, object seconds_per_tick = null)
    {
        if (!(last_tox_damage <= world.time)) // COOLDOWN_FINISHED, inline
            return;

        await target.apply_damage(RADIATION_TOX_DAMAGE_PER_INTERVAL, TOX);
        last_tox_damage = world.time + RADIATION_TOX_INTERVAL; // COOLDOWN_START, inline
    }

    // tg: code/datums/components/irradiated.dm:119
    public virtual void start_burn_splotch_timer()
    {
        // tg keeps no id of this timer (burn_splotch_timer_id stays null); give_burn_splotches checks QDELETED(src)
        _addtimer(Dm.@new<datum_callback>(this, nameof(give_burn_splotches)), rand(RADIATION_BURN_INTERVAL_MIN, RADIATION_BURN_INTERVAL_MAX), TIMER_STOPPABLE, file: "code/datums/components/irradiated.dm", line: 120);
    }

    // tg: code/datums/components/irradiated.dm:122
    public virtual async DmTask give_burn_splotches()
    {
        // This shouldn't be possible, but just in case.
        if (QDELETED(this))
            return;

        start_burn_splotch_timer();

        mob_living_carbon_human human_parent = (mob_living_carbon_human)parent;

        if (Dm.truthy(should_halt_effects(human_parent)))
            return;

        obj_item_bodypart affected_limb = human_parent.get_bodypart(human_parent.get_random_valid_zone() as string) as obj_item_bodypart;
        human_parent.visible_message(
            span_boldwarning(I18n.T("[]'s [] bubbles unnaturally, then bursts into blisters!", human_parent, affected_limb.plaintext_zone)),
            span_boldwarning(I18n.T("Your [] bubbles unnaturally, then bursts into blisters!", affected_limb.plaintext_zone))
        );

        if (Dm.truthy(human_parent.is_blind()))
            to_chat(human_parent, span_boldwarning(I18n.T("Your [] feels like it's bubbling, then burns like hell!", affected_limb.plaintext_zone)));

        await human_parent.apply_damage(RADIATION_BURN_SPLOTCH_DAMAGE, BURN, affected_limb, wound_clothing: FALSE);
        playsound(
            human_parent,
            SFX_SIZZLE,
            50,
            vary: TRUE
        );
    }

    // tg: code/datums/components/irradiated.dm:151
    public virtual void create_glow()
    {
        atom_movable parent_movable = parent as atom_movable;
        if (parent_movable == null)
            return;

        parent_movable.add_filter("rad_glow", 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", "#39ff1430"), Dm.kv("size", 2)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(start_glow_loop), parent_movable), rand(0.1f * SECONDS, 1.9f * SECONDS), file: "code/datums/components/irradiated.dm", line: 157); // Things should look uneven
    }

    // tg: code/datums/components/irradiated.dm:159
    public virtual void start_glow_loop(atom_movable parent_movable = null)
    {
        object filter = parent_movable.get_filter("rad_glow");
        if (!Dm.truthy(filter))
            return;

        Dm.animate(filter, Dm.named("alpha", 110), Dm.named("time", 1.5f * SECONDS), Dm.named("loop", -1));
        Dm.animate(Dm.named("alpha", 40), Dm.named("time", 2.5f * SECONDS));
    }

    // tg: code/datums/components/irradiated.dm:167
    [SignalHandler]
    public virtual object on_clean(datum source = null, object clean_types = null)
    {
        // SIGNAL_HANDLER

        if (!Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_RADIATION)))
            return NONE;

        if (isitem(parent))
        {
            qdel(this);
            return Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP);
        }

        clean_cooldown = world.time + RADIATION_CLEAN_IMMUNITY_TIME; // COOLDOWN_START, inline
        return null;
    }

    // tg: code/datums/components/irradiated.dm:179
    [SignalHandler]
    public virtual object on_geiger_counter_scan(datum source = null, mob user = null, obj_item_geiger_counter geiger_counter = null)
    {
        // SIGNAL_HANDLER

        // [icon2html(geiger_counter, user)] before the text: the device's picture in the chat line
        Dm.Deferred("code/datums/components/irradiated.dm:184", "on_geiger_counter_scan: icon2html(geiger_counter, user), the counter's icon in the chat line (chat icons, M10+)");
        if (isliving(source))
        {
            mob_living living_source = (mob_living)source;
            to_chat(user, span_bolddanger(I18n.T("Subject is irradiated. Contamination traces back to roughly [] ago. Current toxin levels: [].", DisplayTimeText(world.time - Dm.num(beginning_of_irradiation), 5), living_source.get_tox_loss())));
        }
        else
        {
            // In case the green wasn't obvious enough...
            to_chat(user, span_bolddanger(I18n.T("Target is irradiated.")));
        }

        return COMSIG_GEIGER_COUNTER_SCAN_SUCCESSFUL;
    }

    // tg: code/datums/components/irradiated.dm:191
    [SignalHandler]
    public virtual void on_healthscan(datum source = null, DmList render_list = null, object scanpower = null, mob user = null, object mode = null, object tochat = null)
    {
        // SIGNAL_HANDLER

        Dm.append(ref render_list, "<span class='alert ml-1'>");
        // conditional_tooltip(normal_text, tooltip_text, condition) (__DEFINES/chat.dm:58), inline
        string tooltip = I18n.T("Supply antiradiation or antitoxin, such as [] or [].", Dm.initial(typeof(datum_reagent_medicine_potass_iodide), (datum_reagent_medicine_potass_iodide p) => p.name), Dm.initial(typeof(datum_reagent_medicine_pen_acid), (datum_reagent_medicine_pen_acid p) => p.name));
        Dm.append(ref render_list, Dm.truthy(tochat) ? span_tooltip(tooltip, I18n.T("Subject is irradiated.")) : I18n.T("Subject is irradiated."));
        Dm.append(ref render_list, "</span><br>");
    }
}
