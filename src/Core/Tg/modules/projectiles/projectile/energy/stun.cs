// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/energy/stun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice B: the electrode projectile (stun.dm:1-66). M7x slice D: the /datum/status_effect/tased it applies (:72-322: the stamina drain
// tethered to the taser), its alert and its reacting electrode beam (beam.dm:468). The mecha branch of
// the effect is a deferred leaf (docs/ai/specs/not-translated.md section 49); the turret branches came with syndicate G4d. The var blocks
// are generated (D111).

// tg: code/modules/projectiles/projectile/energy/stun.dm:1
public partial class obj_projectile_energy_electrode : obj_projectile_energy
{
    // tg: code/modules/projectiles/projectile/energy/stun.dm:18
    public override object is_hostile_projectile()
    {
        return TRUE;
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:21
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref beam_weakref);
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:25
    public override async DmTask fire(object fire_angle = null, atom direct_target = null)
    {
        if (firer != null)
        {
            beam_weakref = WEAKREF(firer.Beam(
                BeamTarget: this,
                icon: Dm.resource("icons/effects/beam.dmi"),
                icon_state: "electrodes_nozap",
                maxdistance: Dm.num(maximum_range) + 1,
                beam_type: typeof(obj_effect_ebeam_electrodes_nozap)) as datum) as datum_weakref;
            initial_firer_weakref = WEAKREF(firer) as datum_weakref;
        }
        await base.fire(fire_angle, direct_target);
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:37
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (Dm.truthy(pierce_hit))
            return dot;
        if (Dm.eq(dot, BULLET_ACT_BLOCK) || blocked >= 100 || !isliving(target))
            return dot;
        // we need a "from", otherwise, where does the electricity come from?
        if (fired_from == null)
        {
            target.visible_message(
                span_warning(I18n.T("[]\\s collide with [] harmlessly[].", this, target, isfloorturf(target.loc) ? Dm.text(", before falling to []", target.loc) : "")),
                span_notice(I18n.T("[] collide with you harmlessly[].", this, isfloorturf(target.loc) ? Dm.text(", before falling to []", target.loc) : ""))
            );
            return dot;
        }

        do_sparks(1, TRUE, this);
        do_sparks(1, TRUE, fired_from as atom);
        await (target as mob_living).apply_status_effect(
            /*type = */typeof(datum_status_effect_tased),
            /*taser = */fired_from,
            /*firer = */initial_firer_weakref?.resolve() ?? firer,
            /*tase_stamina = */tase_stamina,
            /*energy_drain = */STANDARD_CELL_CHARGE * 0.05f,
            /*electrode_name = */Dm.text("\\the []\\s", this),
            /*tase_range = */Dm.num(maximum_range) + 1
        );
        return dot;
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:63
    public override async DmTask on_range() //to ensure the bolt sparks when it reaches the end of its range if it didn't hit a target yet
    {
        do_sparks(1, TRUE, this);
        await base.on_range();
    }
}

/// Status effect tracking being tased by someone!
// tg: code/modules/projectiles/projectile/energy/stun.dm:72
public partial class datum_status_effect_tased : datum_status_effect
{
    // tg: code/modules/projectiles/projectile/energy/stun.dm:93
    [DmParams("new_owner", "fired_from", "firer", "tase_stamina", "energy_drain", "electrode_name", "tase_range")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        datum fired_from = Dm.arg<datum>(__extra, 0);
        atom_movable firer = Dm.arg<atom_movable>(__extra, 1);
        object tase_stamina = Dm.arg(__extra, 2) ?? 80;
        object energy_drain = Dm.arg(__extra, 3) ?? STANDARD_CELL_CHARGE * 0.05f;
        object electrode_name = Dm.arg(__extra, 4) ?? "the electrodes";
        object tase_range = Dm.arg(__extra, 5) ?? 6;
        if (fired_from == null || firer == null || !can_tase_with(fired_from))
        {
            qdel(this);
            return null;
        }

        stamina_per_second = Dm.num(tase_stamina);
        this.energy_drain = Dm.num(energy_drain);
        this.electrode_name = electrode_name;
        this.tase_range = Dm.num(tase_range);

        object dot = await base.on_creation(new_owner, Dm.forward(__extra, fired_from, firer, tase_stamina, energy_drain, electrode_name, tase_range));
        if (!Dm.truthy(dot))
            return dot;

        set_taser(fired_from);
        set_firer(firer);
        return dot;
    }

    /// Checks if the passed atom is captable of being used to tase someone
    // tg: code/modules/projectiles/projectile/energy/stun.dm:118
    public virtual bool can_tase_with(datum with_what = null)
    {
        if (istype(with_what, typeof(obj_item_gun_energy)))
        {
            obj_item_gun_energy taser_gun = with_what as obj_item_gun_energy;
            if (taser_gun.cell == null)
                return false;
        }

        else if (istype(with_what, typeof(obj_machinery)))
        {
            obj_machinery taser_machine = with_what as obj_machinery;
            if (!Dm.truthy(taser_machine.is_operational))
                return false;
        }

        return true;
    }

    /// Actually does the tasing with the passed atom
    /// Returns TRUE if the tasing was successful, FALSE if it failed
    // tg: code/modules/projectiles/projectile/energy/stun.dm:133
    public virtual async DmTask<bool> do_tase_with(atom with_what = null, object seconds_between_ticks = null)
    {
        if (!Dm.truthy(can_see(taser as atom, owner, 5)))
            return false;
        if (istype(with_what, typeof(obj_item_gun_energy)))
        {
            obj_item_gun_energy taser_gun = with_what as obj_item_gun_energy;
            if (taser_gun.cell == null || !Dm.truthy(await taser_gun.cell.use(energy_drain * Dm.num(seconds_between_ticks))))
                return false;
            taser_gun.update_appearance();
            return true;
        }

        if (istype(taser, typeof(obj_machinery)))
        {
            obj_machinery taser_machine = taser as obj_machinery;
            if (!Dm.truthy(taser_machine.is_operational))
                return false;
            if (!Dm.truthy(await taser_machine.use_energy(energy_drain * Dm.num(seconds_between_ticks), force: FALSE)))
                return false;
            return true;
        }

        if (istype(taser, typeof(obj_item_mecha_parts_mecha_equipment)))
        {
            Dm.Deferred("code/modules/projectiles/projectile/energy/stun.dm:155", "do_tase_with: a mecha's taser equipment (chassis power, ejecting, disabled equipment; mechs, M10+)");
            return false;
        }

        return true;
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:171
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (issilicon(owner) || istype(owner, typeof(mob_living_basic_bot)) /* isbot (is_helpers.dm:208) */ || isdrone(owner) || HAS_TRAIT(owner, TRAIT_PIERCEIMMUNE))
        {
            owner.visible_message(span_warning(Dm.text("[] fail to catch [][]!", capitalize(electrode_name), owner, isfloorturf(owner.loc) ? Dm.text(", falling to []", owner.loc) : "")));
            return FALSE;
        }

        RegisterSignal(owner, COMSIG_LIVING_RESIST, nameof(try_remove_taser));
        RegisterSignal(owner, COMSIG_CARBON_PRE_MISC_HELP, nameof(someome_removing_taser));
        if (Dm.truthy(owner._listen_lookup?[COMSIG_LIVING_MINOR_SHOCK]))
            owner._SendSignal(COMSIG_LIVING_MINOR_SHOCK, Dm.list(owner));
        if (!Dm.truthy(owner.has_status_effect(type)))
        {
            // does not use the status effect api because we snowflake it a bit
            owner.throw_alert(type, typeof(atom_movable_screen_alert_tazed));
            await owner.add_mood_event("tased", typeof(datum_mood_event_tased));
            await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_being_tased));
            if (!HAS_TRAIT(owner, TRAIT_ANALGESIA))
                await owner.emote("scream");
            if (HAS_TRAIT(owner, TRAIT_HULK))
                await owner.say(pick(Dm.list(
                    ";RAAAAAAAARGH!",
                    ";HNNNNNNNNNGGGGGGH!",
                    ";GWAAAAAAAARRRHHH!",
                    "NNNNNNNNGGGGGGGGHH!",
                    ";AAAAAAARRRGH!"
                )), forced: "hulk");
        }
        if (ishuman(owner))
        {
            mob_living_carbon_human human_owner = owner as mob_living_carbon_human;
            human_owner.force_say();
        }
        return TRUE;
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:199
    public override async DmTask<object> on_remove()
    {
        if (istype(taser, typeof(obj_machinery_porta_turret)))
        {
            obj_machinery_porta_turret taser_turret = taser as obj_machinery_porta_turret;
            taser_turret.manual_control = Dm.initial(taser_turret, (obj_machinery_porta_turret p) => p.manual_control);
            taser_turret.always_up = Dm.initial(taser_turret, (obj_machinery_porta_turret p) => p.always_up);
            taser_turret.check_should_process();
        }
        else if (istype(taser, typeof(obj_machinery_power_emitter)))
        {
            obj_machinery_power_emitter taser_emitter = taser as obj_machinery_power_emitter;
            taser_emitter.manual = Dm.initial(taser_emitter, (obj_machinery_power_emitter p) => p.manual);
        }

        mob_living mob_firer = firer as mob_living;
        if (mob_firer != null)
            mob_firer.remove_movespeed_modifier(typeof(datum_movespeed_modifier_tasing_someone));

        if (!QDELING(owner) && !Dm.truthy(owner.has_status_effect(type)))
        {
            await owner.adjust_jitter_up_to(10 * SECONDS, 1 * MINUTES);
            owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_being_tased));
            owner.clear_alert(type);
        }

        taser = null;
        firer = null;
        if (!QDELETED(tase_line))
            QDEL_NULL(ref tase_line);
        else
            tase_line = null;
        return null;
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:224
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (!await do_tase_with(taser as atom, seconds_between_ticks))
        {
            end_tase();
            return null;
        }

        await owner.adjust_stutter_up_to(10 * SECONDS, 20 * SECONDS);
        await owner.adjust_jitter_up_to(20 * SECONDS, 30 * SECONDS);
        if (!IS_UNCONSCIOUS(owner))
            owner.do_jitter_animation(INFINITY); // maximum POWER

        // You are damp, that's bad when you're being tased
        if (Dm.truthy(owner.fire_stacks < 0))
        {
            await owner.apply_damage(max(1, owner.fire_stacks * -0.5f * Dm.num(seconds_between_ticks)), FIRE, spread_damage: TRUE);
            if (SPT_PROB(25, Dm.num(seconds_between_ticks)))
                do_sparks(1, FALSE, owner);
        }

        // clumsy people might hit their head while being tased
        if (HAS_TRAIT(owner, TRAIT_CLUMSY) && owner.body_position == LYING_DOWN && SPT_PROB(20, Dm.num(seconds_between_ticks)))
        {
            await owner.apply_damage(10, BRUTE, BODY_ZONE_HEAD);
            playsound(owner, Dm.resource("sound/effects/tableheadsmash.ogg"), 75, TRUE);
        }

        // the actual stunning is here
        if (!Dm.truthy(owner.check_stun_immunity(CANSTUN | CANKNOCKDOWN)))
            await owner.apply_damage(stamina_per_second * Dm.num(seconds_between_ticks), STAMINA);
        return null;
    }

    /// Sets the passed atom as the "taser"
    // tg: code/modules/projectiles/projectile/energy/stun.dm:252
    public virtual void set_taser(datum new_taser = null)
    {
        taser = new_taser;
        RegisterSignals(taser, Dm.list(COMSIG_QDELETING, COMSIG_ITEM_DROPPED, COMSIG_ITEM_EQUIPPED), nameof(end_tase));
        RegisterSignal(taser, COMSIG_GUN_TRY_FIRE, nameof(block_firing));
        // snowflake cases! yay!
        if (istype(taser, typeof(obj_machinery_porta_turret)))
        {
            obj_machinery_porta_turret taser_turret = taser as obj_machinery_porta_turret;
            taser_turret.manual_control = TRUE;
            taser_turret.always_up = TRUE;
        }
        else if (istype(taser, typeof(obj_machinery_power_emitter)))
        {
            obj_machinery_power_emitter taser_emitter = taser as obj_machinery_power_emitter;
            taser_emitter.manual = TRUE;
        }
    }

    /// Sets the passed atom as the person operating the taser, the "firer"
    // tg: code/modules/projectiles/projectile/energy/stun.dm:268
    public virtual void set_firer(atom new_firer = null)
    {
        firer = new_firer as atom_movable;
        if (taser != firer) // Turrets, notably, are both
            RegisterSignal(firer, COMSIG_QDELETING, nameof(end_tase));

        RegisterSignal(firer, COMSIG_MOB_CLICKON, nameof(user_cancel_tase));

        // Ensures AI mobs or turrets don't tase players until they run out of power
        mob_living mob_firer = new_firer as mob_living;
        if (mob_firer == null || mob_firer.client == null)
        {
            // If multiple things are tasing the same mob, give up sooner, so they can select a new target potentially
            _addtimer(Dm.@new<datum_callback>(this, nameof(end_tase)), (owner.has_status_effect(type) != this) ? 2 * SECONDS : 8 * SECONDS, file: "code/modules/projectiles/projectile/energy/stun.dm", line: 278);
        }
        if (mob_firer != null)
            Waiting.NoSleep(mob_firer.add_movespeed_modifier(typeof(datum_movespeed_modifier_tasing_someone)));

        if (firer == owner)
            return;

        tase_line = firer.Beam(
            BeamTarget: owner,
            icon: Dm.resource("icons/effects/beam.dmi"),
            icon_state: "electrodes",
            maxdistance: tase_range,
            beam_type: typeof(obj_effect_ebeam_reacting_electrodes)) as datum_beam;
        RegisterSignal(tase_line, COMSIG_BEAM_ENTERED, nameof(disrupt_tase));
        RegisterSignal(tase_line, COMSIG_QDELETING, nameof(end_tase));
        // moves the tase beam up or down if the target moves up or down
        tase_line.RegisterSignal(owner, COMSIG_LIVING_SET_BODY_POSITION, nameof(datum_beam.redrawing));
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:294
    [SignalHandler]
    public virtual object block_firing(params object[] __extra)
    {
        return COMPONENT_CANCEL_GUN_FIRE;
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:298
    [SignalHandler]
    public virtual object user_cancel_tase(mob_living source = null, atom clicked_on = null, object modifiers = null)
    {
        if (clicked_on != owner)
            return NONE;
        if (Dm.truthy(LAZYACCESS(modifiers as DmList, SHIFT_CLICK)))
            return NONE;
        end_tase();
        source.changeNext_move(CLICK_CD_GRABBING);
        return COMSIG_MOB_CANCEL_CLICKON;
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:307
    [SignalHandler]
    public virtual void end_tase(params object[] __extra)
    {
        if (QDELING(this))
            return;
        owner.visible_message(
            span_warning(Dm.text("[] stop shocking [][].", capitalize(electrode_name), owner, isfloorturf(owner.loc) ? Dm.text(", falling to []", owner.loc) : "")),
            span_notice(Dm.text("[] stop shocking you[].", capitalize(electrode_name), isfloorturf(owner.loc) ? Dm.text(", falling to []", owner.loc) : ""))
        );
        qdel(this);
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:315
    [SignalHandler]
    public virtual void try_remove_taser(datum source = null)
    {
        Waiting.Detach(try_remove_taser_async(owner));
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:320
    [SignalHandler]
    public virtual object someome_removing_taser(datum source = null, mob_living helper = null)
    {
        Waiting.Detach(try_remove_taser_async(helper));
        return COMPONENT_BLOCK_MISC_HELP;
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:325
    public virtual async DmTask try_remove_taser_async(mob_living remover = null)
    {
        if (Dm.truthy(DOING_INTERACTION(remover, id)))
            return;
        owner.shake_up_animation();
        playsound(owner, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 50, TRUE, -1);
        remover.visible_message(
            span_warning(Dm.text("[] tries to remove [][]!", owner, electrode_name, remover == owner ? "" : Dm.text(" from []", owner))),
            span_notice(Dm.text("You try to remove [][]!", electrode_name, remover == owner ? "" : Dm.text(" from []", owner)))
        );
        // If embedding was less... difficult to work with, I would make tasers rely on an embedded object to handle this
        if (!Dm.truthy(await do_after(remover, 5 * SECONDS, owner, extra_checks: Dm.@new<datum_callback>(this, nameof(try_remove_taser_checks)), interaction_key: id)))
            return;
        remover.visible_message(
            span_warning(Dm.text("[] removes [] from [] body!", owner, electrode_name, remover == owner ? owner.p_their() : Dm.text("[]'s", owner))),
            span_notice(Dm.text("You remove [][]!", electrode_name, remover == owner ? "" : Dm.text(" from []'s body", owner)))
        );
        end_tase();
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:342
    public virtual bool try_remove_taser_checks()
    {
        return !QDELETED(this);
    }

    // tg: code/modules/projectiles/projectile/energy/stun.dm:345
    [SignalHandler]
    public virtual void disrupt_tase(datum_beam source = null, obj_effect_ebeam beam_effect = null, atom_movable entering = null)
    {
        if (!isliving(entering) || entering == taser || entering == firer || entering == owner)
            return;
        if (Dm.truthy(Dm.bitand(entering.pass_flags, PASSMOB | PASSGRILLE | PASSTABLE)))
            return;
        mob_living disruptor = entering as mob_living;
        if (!HAS_TRAIT(entering, TRAIT_CLUMSY) || prob(50))
        {
            if (isliving(firer))
            {
                // taser firer can lie down so people can cross over it!
                mob_living firer_living = firer as mob_living;
                if (firer_living.body_position != disruptor.body_position)
                    return;
            }
            else
            {
                // otherwise you can limbo under it
                if (disruptor.body_position == LYING_DOWN)
                    return;
            }
        }
        disruptor.visible_message(
            span_warning(Dm.text("[] gets tangled in []!", disruptor, electrode_name)),
            span_warning(Dm.text("You get tangled in []!", electrode_name))
        );
        if (!Dm.truthy(disruptor.check_stun_immunity(CANSTUN | CANKNOCKDOWN)))
        {
            Waiting.NoSleep(disruptor.apply_damage(90, STAMINA));
            Waiting.NoSleep(disruptor.Knockdown(5 * SECONDS));
        }
        Waiting.NoSleep(disruptor.adjust_jitter_up_to(10 * SECONDS, 30 * SECONDS));
        qdel(this);
    }
}

/// Screen alert for being tased, clicking does a resist
// tg: code/modules/projectiles/projectile/energy/stun.dm:372
public partial class atom_movable_screen_alert_tazed : atom_movable_screen_alert
{
    // tg: code/modules/projectiles/projectile/energy/stun.dm:380
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;
        mob_living clicker = usr as mob_living;
        clicker.resist();
        return dot;
    }
}

/// A beam subtype used for advanced beams, to react to atoms entering the beam
// tg: code/datums/beam.dm:468
public partial class obj_effect_ebeam_reacting : obj_effect_ebeam
{
    // tg: code/datums/beam.dm:472
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));

        if (!isturf(loc) || owner == null || Dm.truthy(mapload) || !Dm.truthy(react_on_init))
            return dot;

        foreach (atom_movable existing in Dm.each<atom_movable>(loc.contents))
            beam_entered(existing);
        return dot;
    }

    // tg: code/datums/beam.dm:473  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_effect_ebeam_reacting__Initialize__loc_connections;

    // tg: code/datums/beam.dm:487
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable entered = null)
    {
        if (owner == null)
            return;

        beam_entered(entered);
    }

    // tg: code/datums/beam.dm:495
    [SignalHandler]
    public virtual void on_exited(datum source = null, atom_movable exited = null)
    {
        if (owner == null)
            return;

        beam_exited(exited);
    }

    // tg: code/datums/beam.dm:503
    [SignalHandler]
    public virtual void on_turf_change(datum source = null, object path = null, object new_baseturfs = null, object flags = null, DmList post_change_callbacks = null)
    {
        if (owner == null)
            return;

        beam_turfs_changed(post_change_callbacks);
    }

    /// Some atom entered the beam's line
    // tg: code/datums/beam.dm:512
    [ShouldCallParent]
    public virtual void beam_entered(atom_movable entered = null)
    {
        if (Dm.truthy(owner._listen_lookup?[COMSIG_BEAM_ENTERED]))
            owner._SendSignal(COMSIG_BEAM_ENTERED, Dm.list(owner, this, entered));
    }

    /// Some atom exited the beam's line
    // tg: code/datums/beam.dm:517
    [ShouldCallParent]
    public virtual void beam_exited(atom_movable exited = null)
    {
        if (Dm.truthy(owner._listen_lookup?[COMSIG_BEAM_EXITED]))
            owner._SendSignal(COMSIG_BEAM_EXITED, Dm.list(owner, this, exited));
    }

    /// Some turf the beam covers has changed to a new turf type
    // tg: code/datums/beam.dm:522
    [ShouldCallParent]
    public virtual void beam_turfs_changed(DmList post_change_callbacks = null)
    {
        if (Dm.truthy(owner._listen_lookup?[COMSIG_BEAM_TURFS_CHANGED]))
            owner._SendSignal(COMSIG_BEAM_TURFS_CHANGED, Dm.list(owner, post_change_callbacks));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/beam.dm:473  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = on_entered, COMSIG_ATOM_EXITED = on_exited, COMSIG_TURF_CHANGE = on_turf_change)
    internal DmList obj_effect_ebeam_reacting__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_ebeam_reacting.on_entered)),
        Dm.kv(COMSIG_ATOM_EXITED, nameof(obj_effect_ebeam_reacting.on_exited)),
        Dm.kv(COMSIG_TURF_CHANGE, nameof(obj_effect_ebeam_reacting.on_turf_change)));
}
