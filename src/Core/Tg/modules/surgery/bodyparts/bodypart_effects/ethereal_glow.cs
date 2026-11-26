// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Species package SP2b (D349): the whole file, the ethereal's glow. The light follows the owner's colour and health; an EMP, a saboteur
// or a light eater puts it out for a while, an emag turns the ethereal into a disco ball. The var block is generated (D111).

/// Causes the owner to glow based on their bodypart/mutant color, turns them into a disco ball when emagged
// tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:2
public partial class datum_status_effect_grouped_bodypart_effect_ethereal_glow : datum_status_effect_grouped_bodypart_effect
{
    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:22
    [DmParams("source", "bodypart", "special", "lazy")]
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object lazy = Dm.arg(__extra, 3);
        if (!ishuman(owner))
            return FALSE;
        mob_living_carbon_human ethereal = owner as mob_living_carbon_human;
        RegisterSignal(ethereal, COMSIG_ATOM_EMAG_ACT, nameof(on_emag_act));
        RegisterSignal(ethereal, COMSIG_ATOM_EMP_ACT, nameof(on_emp_act));
        RegisterSignal(ethereal, COMSIG_ATOM_SABOTEUR_ACT, nameof(hit_by_saboteur));
        RegisterSignal(ethereal, COMSIG_LIGHT_EATER_ACT, nameof(on_light_eater));
        RegisterSignal(ethereal, COMSIG_LIVING_HEALTH_UPDATE, nameof(refresh_light_color));
        ethereal_light = ethereal.mob_light(light_type: typeof(obj_effect_dummy_lighting_obj_moblight_species)) as obj_effect_dummy_lighting_obj;
        if (!Dm.truthy(lazy))
            refresh_light_color();
        return TRUE;
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:36
    public override void add_bodypart(obj_item_bodypart bodypart = null, object special = null, object lazy = null)
    {
        base.add_bodypart(bodypart, special, lazy);
        if (!Dm.truthy(lazy))
            refresh_light_color();
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:41
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, Dm.list(
            COMSIG_ATOM_EMAG_ACT,
            COMSIG_ATOM_EMP_ACT,
            COMSIG_ATOM_SABOTEUR_ACT,
            COMSIG_LIGHT_EATER_ACT,
            COMSIG_LIVING_HEALTH_UPDATE));
        QDEL_NULL(ref ethereal_light);
        return await base.on_remove();
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:51
    [SignalHandler]
    public virtual void refresh_light_color()
    {
        if (ethereal_light == null || !ishuman(owner))
            return;

        mob_living_carbon_human ethereal = owner as mob_living_carbon_human;
        // Requires a crystal core or emissive blood to actually glow
        bool can_glow = owner.get_organ_slot(ORGAN_SLOT_HEART) is obj_item_organ_heart_ethereal
            || Dm.truthy((owner.get_bloodtype() as datum_blood_type)?.get_emissive_alpha());
        if (Dm.num(ethereal.stat) == DEAD || Dm.truthy(disrupted) || !can_glow)
        {
            Waiting.NoSleep(ethereal_light.set_light_on(FALSE));
            bool had_head = false;
            foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(bodyparts))
            {
                part.add_color_override(COLOR_GRAY, LIMB_COLOR_ETHEREAL);
                if (part is obj_item_bodypart_head)
                    had_head = true;
            }
            if (had_head)
            {
                ethereal.set_facial_haircolor(COLOR_GRAY, @override: TRUE, update: FALSE);
                ethereal.set_haircolor(COLOR_GRAY, @override: TRUE, update: FALSE);
            }
            Waiting.NoSleep(ethereal.update_body());
            return;
        }

        float healthmod = 1;
        if (Dm.num(ethereal.health) >= 0)
            healthmod = 0.25f + Dm.num(ethereal.health) / 100 * 0.75f;
        else
            healthmod = (100 - Dm.num(ethereal.health)) / 100 * 0.25f;

        object used_color = ethereal.dna.features[FEATURE_MUTANT_COLOR];
        // If we don't have a mutant color but do have an ethereal heart (humans with transplanted limbs & organs), use the heart's color
        if (!HAS_TRAIT(owner, TRAIT_MUTANT_COLORS) && owner.get_organ_slot(ORGAN_SLOT_HEART) is obj_item_organ_heart_ethereal heart)
            used_color = heart.ethereal_color;

        if (!Dm.truthy(emageffect))
        {
            DmList skin_color = rgb2num("#eda495");
            DmList colors = rgb2num(Dm.str(used_color));
            DmList built_color = Dm.list();
            for (int i = 1; i <= 3; i++)
                Dm.append(ref built_color, Dm.num(skin_color[i]) + ((Dm.num(colors[i]) - Dm.num(skin_color[i])) * healthmod));
            current_color = rgb(Dm.num(built_color[1]), Dm.num(built_color[2]), Dm.num(built_color[3]));
        }

        float coefficient = get_bodypart_coefficient();
        ethereal_light.set_light_range_power_color((1 + (2 * healthmod)) * Dm.num(rangemult) * coefficient, (1 + (1 * healthmod) * Dm.num(powermult)) * coefficient, current_color);

        if (Dm.truthy(flickering))
        {
            if (Dm.truthy(currently_flickered))
                Waiting.NoSleep(ethereal_light.set_light_on(FALSE));
            else
                Waiting.NoSleep(ethereal_light.set_light_on(TRUE));
        }
        else
        {
            currently_flickered = FALSE;
            Waiting.NoSleep(ethereal_light.set_light_on(TRUE));
        }

        bool has_head = false;
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(bodyparts))
        {
            part.add_color_override(current_color, LIMB_COLOR_ETHEREAL);
            if (part is obj_item_bodypart_head)
                has_head = true;
        }
        if (has_head)
        {
            ethereal.set_facial_haircolor(current_color, @override: TRUE, update: FALSE);
            ethereal.set_haircolor(current_color, @override: TRUE, update: FALSE);
        }
        Waiting.NoSleep(ethereal.update_body());
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:114
    [SignalHandler]
    public virtual void on_emp_act(mob_living_carbon_human source = null, object severity = null, object protection = null)
    {
        if (Dm.truthy(Dm.bitand(protection, EMP_PROTECT_SELF)))
            return;
        disrupted = TRUE;
        refresh_light_color();
        to_chat(source, span_notice(I18n.T("You feel the light of your body leave you.")));
        object sw__1 = severity;
        if (Dm.eq(sw__1, EMP_LIGHT))
            _addtimer(Dm.@new<datum_callback>(this, nameof(stop_emp), source), 10 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm", line: 121); // We're out for 10 seconds
        else if (Dm.eq(sw__1, EMP_HEAVY))
            _addtimer(Dm.@new<datum_callback>(this, nameof(stop_emp), source), 20 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm", line: 123); // We're out for 20 seconds
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:127
    [SignalHandler]
    public virtual object hit_by_saboteur(mob_living_carbon_human source = null, object disrupt_duration = null)
    {
        disrupted = TRUE;
        refresh_light_color();
        to_chat(source, span_warning(I18n.T("Something inside of you crackles in a bad way.")));
        Waiting.NoSleep(source.take_bodypart_damage(burn: 3, wound_bonus: CANT_WOUND));
        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_emp), source), Dm.num(disrupt_duration), Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm", line: 131);
        return TRUE;
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:135
    [SignalHandler]
    public virtual object on_emag_act(mob_living_carbon_human source = null, mob user = null)
    {
        if (Dm.truthy(emageffect))
            return FALSE;
        emageffect = TRUE;
        if (user != null)
            to_chat(user, span_notice(I18n.T("You tap [] on the back with your card.", source)));
        source.visible_message(span_danger(I18n.T("[] starts flickering in an array of colors!", source)));
        handle_emag();
        // Disco mode for 2 minutes! This doesn't affect the ethereal at all besides either annoying some players, or making someone look badass.
        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_emag), source), 2 * MINUTES, file: "code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm", line: 144);
        return TRUE;
    }

    /// Special handling for getting hit with a light eater
    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:149
    [SignalHandler]
    public virtual object on_light_eater(mob_living_carbon_human source = null, datum light_eater = null)
    {
        Waiting.NoSleep(source.emp_act(EMP_LIGHT));
        return COMPONENT_BLOCK_LIGHT_EATER;
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:154
    public virtual void stop_emp(mob_living_carbon_human ethereal = null)
    {
        disrupted = FALSE;
        refresh_light_color();
        to_chat(ethereal, span_notice(I18n.T("You feel more energized as your shine comes back.")));
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:159
    public virtual void handle_emag(mob_living_carbon_human ethereal = null)
    {
        if (!Dm.truthy(emageffect))
            return;
        current_color = Dm.str(GLOB.color_list_ethereal[pick(GLOB.color_list_ethereal)]);
        refresh_light_color();
        _addtimer(Dm.@new<datum_callback>(this, nameof(handle_emag), ethereal), 0.5f * SECONDS, file: "code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm", line: 164);
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:166
    public virtual void stop_emag(mob_living_carbon_human ethereal = null)
    {
        emageffect = FALSE;
        refresh_light_color();
        ethereal.visible_message(span_danger(I18n.T("[] stops flickering and goes back to their normal state!", ethereal)));
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:171
    public virtual void handle_glow_emote(object power = null, object range = null, object flare = null, object duration = null, object flare_time = null)
    {
        flare ??= FALSE;
        duration ??= 5 * SECONDS;
        flare_time ??= 0;
        powermult = Dm.num(power);
        rangemult = Dm.num(range);
        refresh_light_color();
        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_glow_emote), flare, flare_time), Dm.num(duration), file: "code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm", line: 175);
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:177
    public virtual async DmTask stop_glow_emote(object flare = null, object flare_time = null)
    {
        if (!Dm.truthy(flare))
        {
            powermult = 1;
            rangemult = 1;
            refresh_light_color();
            return;
        }

        mob_living_carbon_human ethereal = owner as mob_living_carbon_human;
        powermult = 0.5f;
        rangemult = 0.75f;
        refresh_light_color();
        start_flicker(duration: 1.5f * SECONDS, min: 1, max: 2);
        await sleep(1.5f * SECONDS);
        powermult = 1;
        rangemult = 1;
        disrupted = TRUE;
        to_chat(ethereal, span_warning(I18n.T("Your shine flickers and fades.")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_emp), ethereal), Dm.num(flare_time), Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm", line: 193);
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:196
    public virtual void handle_flicker(object flickmin = null, object flickmax = null)
    {
        if (!Dm.truthy(flickering))
        {
            currently_flickered = FALSE;
            refresh_light_color();
            return;
        }

        if (Dm.truthy(currently_flickered))
            currently_flickered = FALSE;
        else
            currently_flickered = TRUE;
        refresh_light_color();
        _addtimer(Dm.@new<datum_callback>(this, nameof(handle_flicker)), rand(1, 4), file: "code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm", line: 206);
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:209
    public virtual void stop_flicker()
    {
        flickering = FALSE;
        currently_flickered = FALSE;
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm:213
    public virtual void start_flicker(object duration = null, object min = null, object max = null)
    {
        duration ??= 6 * SECONDS;
        min ??= 1;
        max ??= 4;
        flickering = TRUE;
        handle_flicker(min, max);
        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_flicker)), Dm.num(duration), file: "code/modules/surgery/bodyparts/bodypart_effects/ethereal_glow.dm", line: 217);
    }
}
