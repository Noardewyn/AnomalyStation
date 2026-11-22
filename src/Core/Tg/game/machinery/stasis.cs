// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/stasis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8y slice B (D217, D220): the whole file (the bed's fair_market_price and payment_department are tg's machinery vars, which no tg
// code reads). The stasis bed buckles its patient, who gets the stasis status effect (debuffs.cs) and the free_operation component while
// it runs. post_buckle_mob and post_unbuckle_mob are written as tg has them; their base is buckling.dm (slice A, D218). The mattress
// vis overlay fades with a 5 s CUBIC animation (the client draws it, slice C). The var block is generated.

// tg: code/game/machinery/stasis.dm:1  #define STASIS_TOGGLE_COOLDOWN 50 (file-local, #undef at the end)
// tg: code/game/machinery/stasis.dm:2
public partial class obj_machinery_stasis : obj_machinery
{
    private const float STASIS_TOGGLE_COOLDOWN = 50;

    // tg: code/game/machinery/stasis.dm:26
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_elevation), Dm.named("pixel_shift", 6)));
        update_buckle_vars(dir);
        return dot;
    }

    // tg: code/game/machinery/stasis.dm:31
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        float energy_rating = 0;
        foreach (datum_stock_part part in Dm.each<datum_stock_part>(component_parts))
            energy_rating += Dm.num(part.energy_rating());

        foreach (obj_item_stock_parts part in Dm.each<obj_item_stock_parts>(component_parts))
            energy_rating += part.energy_rating;

        idle_power_usage = Dm.div(Dm.initial(this, (obj_machinery_stasis m) => m.idle_power_usage), Dm.div(energy_rating, 2));
        active_power_usage = Dm.div(Dm.initial(this, (obj_machinery_stasis m) => m.active_power_usage), Dm.div(energy_rating, 2));
        update_current_power_usage();
        return dot;
    }

    // tg: code/game/machinery/stasis.dm:45
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Alt-click to [] the machine.", Dm.truthy(stasis_enabled) ? I18n.T("turn off") : I18n.T("turn on"))));
        return dot;
    }

    // tg: code/game/machinery/stasis.dm:49
    public virtual void play_power_sound()
    {
        object _running = stasis_running();
        if (!Dm.eq(last_stasis_sound, _running))
        {
            float sound_freq = rand(5120, 8800);
            if (Dm.truthy(_running))
                playsound(this, Dm.resource("sound/machines/synth/synth_yes.ogg"), 50, TRUE, frequency: sound_freq);
            else
                playsound(this, Dm.resource("sound/machines/synth/synth_no.ogg"), 50, TRUE, frequency: sound_freq);
            last_stasis_sound = Dm.num(_running);
        }
    }

    // tg: code/game/machinery/stasis.dm:59
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (world.time < stasis_can_toggle)
            return CLICK_ACTION_BLOCKING;
        stasis_enabled = Dm.not(stasis_enabled);
        stasis_can_toggle = world.time + STASIS_TOGGLE_COOLDOWN;
        playsound(this, Dm.resource("sound/machines/click.ogg"), 60, TRUE);
        user.visible_message(span_notice(Dm.truthy(stasis_enabled) ? I18n.T("\\The [] powers on.", this) : I18n.T("\\The [] shuts down.", this)),
            span_notice(Dm.truthy(stasis_enabled) ? I18n.T("You power on \\the [].", this) : I18n.T("You shut down \\the [].", this)),
            span_hear(Dm.truthy(stasis_enabled) ? I18n.T("You hear a nearby machine power on.") : I18n.T("You hear a nearby machine shut down.")));
        play_power_sound();
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/machinery/stasis.dm:72
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == occupant)
        {
            mob_living L = gone as mob_living;
            if (HAS_TRAIT(L, TRAIT_STASIS))
                await thaw_them(L);
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/game/machinery/stasis.dm:79
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        update_buckle_vars(newdir);
    }

    // tg: code/game/machinery/stasis.dm:83
    public virtual void update_buckle_vars(object newdir = null)
    {
        buckle_lying = Dm.truthy(Dm.bitand(newdir, NORTHEAST)) ? 270 : 90;
    }

    // tg: code/game/machinery/stasis.dm:86
    public virtual object stasis_running()
    {
        return Dm.truthy(stasis_enabled) ? is_operational : stasis_enabled;
    }

    // tg: code/game/machinery/stasis.dm:89
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            icon_state = Dm.text("[]_broken", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(panel_open) || Dm.truthy(Dm.bitand(machine_stat, MAINT)))
        {
            icon_state = Dm.text("[]_maintenance", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.str(base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/stasis.dm:99
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(mattress_state))
            return dot;
        object _running = stasis_running();
        if (mattress_on == null)
            mattress_on = SSvis_overlays.add_vis_overlay(this, icon as object, mattress_state, BELOW_OBJ_LAYER, plane, dir, alpha: 0, unique: TRUE) as obj_effect_overlay_vis;
        else
        {
            Dm.append(vis_contents, mattress_on);
            if (Dm.truthy(managed_vis_overlays))
                Dm.append(managed_vis_overlays, mattress_on);
            else
                managed_vis_overlays = Dm.list(mattress_on);
        }

        if (Dm.truthy(mattress_on.alpha) ? !Dm.truthy(_running) : Dm.truthy(_running)) //check the inverse of _running compared to truthy alpha, to see if they differ
        {
            float new_alpha = Dm.truthy(_running) ? 255 : 0;
            float easing_direction = Dm.truthy(_running) ? EASE_OUT : EASE_IN;
            animate(mattress_on, Dm.named("alpha", new_alpha), Dm.named("time", 50), Dm.named("easing", Dm.bitor(CUBIC_EASING, easing_direction)));
        }
        return dot;
    }

    // tg: code/game/machinery/stasis.dm:118
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        if (Dm.truthy(same_z_layer))
        {
            await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
            return;
        }
        SET_PLANE(mattress_on, Dm.num(PLANE_TO_TRUE(mattress_on.plane)), new_turf);
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
    }

    // tg: code/game/machinery/stasis.dm:124
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(dot))
            play_power_sound();
        return dot;
    }

    // tg: code/game/machinery/stasis.dm:129
    [SignalHandler]
    public override object power_change()
    {
        object dot = base.power_change();
        play_power_sound();
        return dot;
    }

    // tg: code/game/machinery/stasis.dm:133
    public virtual async DmTask chill_out(mob_living target = null)
    {
        if (target != occupant)
            return;
        float freq = rand(24750, 26550);
        playsound(this, Dm.resource("sound/effects/spray.ogg"), 5, TRUE, 2, frequency: freq);
        await target.apply_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_MACHINE_EFFECT);
        target.extinguish_mob();
        update_use_power(ACTIVE_POWER_USE);
    }

    // tg: code/game/machinery/stasis.dm:142
    public virtual async DmTask thaw_them(mob_living target = null)
    {
        target.remove_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_MACHINE_EFFECT);
        if (target == occupant)
            update_use_power(IDLE_POWER_USE);
    }

    // tg: code/game/machinery/stasis.dm:147  (the base, atom/movable/post_buckle_mob, is buckling.dm: slice A, D218)
    public override async DmTask post_buckle_mob(mob_living L = null)
    {
        if (!Dm.truthy(can_be_occupant(L)))
            return;
        set_occupant(L);
        if (Dm.truthy(stasis_running()))
            await chill_out(L);
        update_appearance();
        await L.AddComponentFrom(type, typeof(datum_component_free_operation));
    }

    // tg: code/game/machinery/stasis.dm:156
    public override async DmTask post_unbuckle_mob(mob_living L = null)
    {
        await thaw_them(L);
        if (L == occupant)
            set_occupant(null);
        update_appearance();
        L.RemoveComponentSource(type, typeof(datum_component_free_operation));
    }

    // tg: code/game/machinery/stasis.dm:163  (set waitfor = FALSE, inherited from /datum/process: the wrapper of waiting-model.md 5.4;
    // applying the status effect may sleep)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process__body(seconds_per_tick, dot), dot);
    }

    private async DmTask<object> process__body(object seconds_per_tick, DotBox<object> dot)
    {
        if (!isliving(occupant))
        {
            update_use_power(IDLE_POWER_USE);
            return dot.Value;
        }
        mob_living L_occupant = (mob_living)occupant;
        if (Dm.truthy(stasis_running()))
        {
            if (!HAS_TRAIT(L_occupant, TRAIT_STASIS))
                await chill_out(L_occupant);
        }

        else if (HAS_TRAIT(L_occupant, TRAIT_STASIS))
            await thaw_them(L_occupant);
        return dot.Value;
    }

    // tg: code/game/machinery/stasis.dm:175
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/stasis.dm:178
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }
}
