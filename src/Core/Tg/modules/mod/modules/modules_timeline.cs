// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_timeline.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole. The chrono suit's (an admin's) timeline MOD modules: the eradication lock (a stranger who activates
// the suit is erased), the rewinder (back to where and how the wearer was ten seconds ago, with the dejavu component,
// datums/components/dejavu.dm), the timestopper (a channelled time stop around the wearer, datums/proximity_monitor/fields/timestop.dm),
// the timeline jumper (phasing out of and back into the timeline) and the timestream eradication module (a beam that holds its target
// in an eradication field until they are erased), with the eradication beam and field. DV540: the eradication field draws its victim
// fading out (tg multiplies the victim's flattened icon by an erasing mask, frame by frame, on the server). The var blocks are
// generated (D111).

///Eradication lock - Prevents people who aren't the owner of the suit from existing on the timeline via eradicating the suit with the intruder inside
// tg: code/modules/mod/modules/modules_timeline.dm:7
public partial class obj_item_mod_module_eradication_lock : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_timeline.dm:22
    public override async DmTask on_install()
    {
        await base.on_install();
        RegisterSignal(mod, COMSIG_MOD_ACTIVATE, nameof(on_mod_activation));
        RegisterSignal(mod, COMSIG_MOD_MODULE_REMOVAL, nameof(on_mod_removal));
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:27
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        UnregisterSignal(mod, COMSIG_MOD_ACTIVATE);
        UnregisterSignal(mod, COMSIG_MOD_MODULE_REMOVAL);
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:32
    public override async DmTask<object> on_use(mob activator = null)
    {
        true_owner_ckey = mod.wearer.ckey;
        balloon_alert(activator, I18n.T("user remembered"));
        playsound(this, Dm.resource("sound/items/pshoom/pshoom.ogg"), 25, TRUE);
        await drain_power(use_energy_cost);
        return null;
    }

    ///Signal fired when the modsuit tries activating
    // tg: code/modules/mod/modules/modules_timeline.dm:39
    [SignalHandler]
    public virtual object on_mod_activation(datum source = null, mob user = null)
    {
        if (!Dm.truthy(true_owner_ckey) || Dm.eq(user.ckey, true_owner_ckey))
            return NONE;
        if (user == mod.wearer)
        {
            to_chat(mod.wearer, span_userdanger(I18n.T("\"MODsuit compromised by timeline inhabitant! Eradicating...\"")));
            Dm.@new<obj_structure_chrono_field>(user.loc, user);
        }
        return MOD_CANCEL_ACTIVATE;
    }

    ///Signal fired when the modsuit tries removing a module.
    // tg: code/modules/mod/modules/modules_timeline.dm:50
    [SignalHandler]
    public virtual object on_mod_removal(datum source = null, mob user = null)
    {
        if (Dm.truthy(true_owner_ckey) && !Dm.eq(user.ckey, true_owner_ckey))
        {
            to_chat(mod.wearer, span_userdanger(I18n.T("\"Timeline inhabitant tampering detected! Eradicating...\"")));
            Dm.@new<obj_structure_chrono_field>(user.loc, user);
            return MOD_CANCEL_REMOVAL;
        }
        return null;
    }
}

///Rewinder - Activating saves a point in time, after 10 seconds you will jump back to that state.
// tg: code/modules/mod/modules/modules_timeline.dm:59
public partial class obj_item_mod_module_rewinder : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_timeline.dm:72
    public override async DmTask<object> on_use(mob activator = null)
    {
        balloon_alert(activator, I18n.T("anchor point set"));
        playsound(this, Dm.resource("sound/items/modsuit/time_anchor_set.ogg"), 50, TRUE);
        //stops all mods from triggering during rewinding
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(mod.modules))
            RegisterSignal(module, COMSIG_MODULE_TRIGGERED, nameof(on_module_triggered));
        await mod.wearer.AddComponent(typeof(datum_component_dejavu_timeline), 1, 10 * SECONDS);
        RegisterSignal(mod, COMSIG_MOD_ACTIVATE, nameof(on_activate_block));
        _addtimer(Dm.@new<datum_callback>(this, nameof(unblock_suit_activation)), 10 * SECONDS, file: "code/modules/mod/modules/modules_timeline.dm", line: 80);
        return null;
    }

    ///Unregisters the modsuit deactivation blocking signal, after dejavu functionality finishes.
    // tg: code/modules/mod/modules/modules_timeline.dm:83
    public virtual void unblock_suit_activation()
    {
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(mod.modules))
            UnregisterSignal(module, COMSIG_MODULE_TRIGGERED);
        UnregisterSignal(mod, COMSIG_MOD_ACTIVATE);
    }

    ///Signal fired when wearer attempts to activate/deactivate suits
    // tg: code/modules/mod/modules/modules_timeline.dm:89
    [SignalHandler]
    public virtual object on_activate_block(datum source = null, object user = null)
    {
        balloon_alert(user as mob, I18n.T("not while rewinding!"));
        return MOD_CANCEL_ACTIVATE;
    }

    ///Signal fired when wearer attempts to trigger modules, if attempting while time is stopped
    // tg: code/modules/mod/modules/modules_timeline.dm:95
    [SignalHandler]
    public virtual object on_module_triggered(datum source = null, mob user = null)
    {
        balloon_alert(user, I18n.T("not while rewinding!"));
        return MOD_ABORT_USE;
    }
}

///Timestopper - Need I really explain? It's the wizard's time stop, but the user channels it by not moving instead of a duration.
// tg: code/modules/mod/modules/modules_timeline.dm:101
public partial class obj_item_mod_module_timestopper : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_timeline.dm:116
    public override async DmTask<object> used(mob activator = null)
    {
        if (timestop != null)
        {
            mod.balloon_alert(activator, I18n.T("already freezing time!"));
            return FALSE;
        }
        return await base.used(activator);
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:122
    public override async DmTask<object> on_use(mob activator = null)
    {
        //stops all mods from triggering during timestop- including timestop itself
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(mod.modules))
            RegisterSignal(module, COMSIG_MODULE_TRIGGERED, nameof(on_module_triggered));
        timestop = Dm.@new<obj_effect_timestop_channelled>(get_turf(mod.wearer), 2, INFINITY, Dm.list(mod.wearer));
        RegisterSignal(timestop, COMSIG_QDELETING, nameof(unblock_suit_activation));
        return null;
    }

    ///Unregisters the modsuit deactivation blocking signal, after timestop functionality finishes.
    // tg: code/modules/mod/modules/modules_timeline.dm:130
    [SignalHandler]
    public virtual void unblock_suit_activation(datum source = null)
    {
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(mod.modules))
            UnregisterSignal(module, COMSIG_MODULE_TRIGGERED);
        UnregisterSignal(source, COMSIG_QDELETING);
        UnregisterSignal(mod, COMSIG_MOD_ACTIVATE);
        timestop = null;
    }

    ///Signal fired when wearer attempts to trigger modules, if attempting while time is stopped
    // tg: code/modules/mod/modules/modules_timeline.dm:139
    [SignalHandler]
    public virtual object on_module_triggered(datum source = null)
    {
        balloon_alert(mod.wearer, I18n.T("not while stopping time!"));
        return MOD_ABORT_USE;
    }

    ///Signal fired when wearer attempts to activate/deactivate suits, if attempting while time is stopped
    // tg: code/modules/mod/modules/modules_timeline.dm:145
    [SignalHandler]
    public virtual object on_activate_block(datum source = null, object user = null)
    {
        balloon_alert(user as mob, I18n.T("not while stopping time!"));
        return MOD_CANCEL_ACTIVATE;
    }
}

///Timeline Jumper - Infinite phasing. needs some special effects
// tg: code/modules/mod/modules/modules_timeline.dm:151
public partial class obj_item_mod_module_timeline_jumper : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_timeline.dm:165
    public override async DmTask<object> used(mob activator = null)
    {
        area noteleport_check = get_area(mod.wearer) as area;
        if (noteleport_check != null && !Dm.truthy(check_teleport_valid(mod.wearer, get_turf(mod.wearer))))
        {
            to_chat(activator, span_danger(I18n.T("Some dull, universal force is between you and the [].", phased_mob != null ? I18n.T("current timeline") : I18n.T("stream between timelines"))));
            return FALSE;
        }
        return await base.used(activator);
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:172
    public override async DmTask<object> on_use(mob activator = null)
    {
        if (phased_mob == null)
        {
            //phasing out
            mod.visible_message(span_warning(I18n.T("[] leaps out of the timeline!", mod.wearer)));
            await mod.wearer.SetAllImmobility(0);
            await mod.wearer.set_stamina_loss(0);
            phased_mob = Dm.@new<obj_effect_dummy_phased_mob_chrono>(get_turf(mod.wearer.loc), mod.wearer);
            RegisterSignal(mod, COMSIG_MOD_ACTIVATE, nameof(on_activate_block));
        }
        else
        {
            //phasing in
            await phased_mob.eject_jaunter();
            phased_mob = null;
            UnregisterSignal(mod, COMSIG_MOD_ACTIVATE);
            mod.visible_message(span_warning(I18n.T("[] drops into the timeline!", mod.wearer)));
        }

        //probably justifies its own sound but whatever
        playsound(this, Dm.resource("sound/items/modsuit/time_anchor_set.ogg"), 50, TRUE);
        return null;
    }

    ///Signal fired when wearer attempts to activate/deactivate suits while phased out
    // tg: code/modules/mod/modules/modules_timeline.dm:191
    [SignalHandler]
    public virtual object on_activate_block(datum source = null, object user = null)
    {
        //has to be a to_chat because you're phased out.
        to_chat(user, span_warning(I18n.T("Deactivating your suit while inbetween timelines would be a very bad idea.")));
        return MOD_CANCEL_ACTIVATE;
    }
}

///TEM - Lets you eradicate people.
// tg: code/modules/mod/modules/modules_timeline.dm:203
public partial class obj_item_mod_module_tem : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_timeline.dm:2 (file-local, #undef at :438)
    private const float CHRONO_BEAM_RANGE = 3;

    // tg: code/modules/mod/modules/modules_timeline.dm:220
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        //trying to fire again, so disconnect what we have
        if (field != null)
            field_disconnect(field);
        //fire projectile
        obj_projectile_energy_chrono_beam chrono_beam = Dm.@new<obj_projectile_energy_chrono_beam>(get_turf(this));
        chrono_beam.tem_weakref = WEAKREF(this) as datum_weakref;
        await chrono_beam.aim_projectile(target, mod.wearer);
        chrono_beam.firer = mod.wearer;
        playsound(this, Dm.resource("sound/items/modsuit/time_anchor_set.ogg"), 50, TRUE);
        Waiting.Detach(chrono_beam.fire()); // INVOKE_ASYNC(chrono_beam, TYPE_PROC_REF(/obj/projectile, fire))
        return dot;
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:235
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        if (field == null)
            return;
        field_disconnect(field);
    }

    /**
     * ### field_connect
     *
     * Links a chrono field to this module. The chrono field will keep track of the eradication process.
     * Unlinks a chrono field if it is connected to a tem already.
     *
     * Arguments:
     * * field: chronofield we are attempting to link to this module.
     */
    // tg: code/modules/mod/modules/modules_timeline.dm:250
    public virtual void field_connect(obj_structure_chrono_field field = null)
    {
        if (field.tem != null)
        {
            if (field.captured != null)
                balloon_alert(mod.wearer, I18n.T("already has connection!"));
            field_disconnect(field);
            return;
        }
        startpos = get_turf(mod.wearer);
        this.field = field;
        field.tem = this;
        if (field.captured != null)
            balloon_alert(mod.wearer, I18n.T("connection estabilished"));
    }

    /**
     * ### field_disconnect
     *
     * Unlinks a chrono field from this module.
     *
     * Arguments:
     * * field: chronofield we are attempting to unlink from this module.
     */
    // tg: code/modules/mod/modules/modules_timeline.dm:270
    public virtual void field_disconnect(obj_structure_chrono_field field = null)
    {
        if (field != null)
        {
            if (field.tem == this)
                field.tem = null;
            if (field.captured != null)
                balloon_alert(mod.wearer, I18n.T("connection lost!"));
        }
        this.field = null;
        startpos = null;
    }

    /**
     * ### field_check
     *
     * Checks to see if  our field can still be linked to the tem. If it isn't, it will unlink the field.
     *
     * Arguments:
     * * field: chronofield we're checking the connection's validity on.
     */
    // tg: code/modules/mod/modules/modules_timeline.dm:287
    public virtual object field_check(obj_structure_chrono_field field = null)
    {
        if (field == null)
            return FALSE;
        turf currentpos = get_turf(this);
        if (currentpos == startpos && Dm.eq(mod.wearer.body_position, STANDING_UP) && !HAS_TRAIT(mod.wearer, TRAIT_INCAPACITATED) && Dm.@in(field, view(CHRONO_BEAM_RANGE, currentpos)))
            return TRUE;
        field_disconnect(field);
        return FALSE;
    }
}

// tg: code/modules/mod/modules/modules_timeline.dm:296
public partial class obj_projectile_energy_chrono_beam : obj_projectile_energy
{
    // tg: code/modules/mod/modules/modules_timeline.dm:303
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        obj_item_mod_module_tem tem = tem_weakref.resolve() as obj_item_mod_module_tem;
        if (target != null && tem != null && isliving(target))
        {
            obj_structure_chrono_field field = Dm.@new<obj_structure_chrono_field>(target.loc, target, tem);
            tem.field_connect(field);
            return BULLET_ACT_HIT;
        }

        return await base.on_hit(target, blocked, pierce_hit);
    }
}

// tg: code/modules/mod/modules/modules_timeline.dm:312
public partial class obj_structure_chrono_field : obj_structure
{
    // tg: code/modules/mod/modules/modules_timeline.dm:4 (file-local, #undef at :439)
    private const float CHRONO_FRAME_COUNT = 22;

    // tg: code/modules/mod/modules/modules_timeline.dm:335
    [DmParams("mapload", "target", "tem")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living target = Dm.arg(__extra, 0) as mob_living;
        obj_item_mod_module_tem tem = Dm.arg(__extra, 1) as obj_item_mod_module_tem;
        if (isliving(target))
        {
            if (tem == null)
                attached = FALSE;
            Waiting.NoSleep(target.forceMove(this));
            captured = target;
            // DEVIATION(DV540): tg flattens the victim's icon (getFlatIcon) and multiplies it by each of the CHRONO_FRAME_COUNT frames of
            // icons/obj/chronos.dmi "erasing" into a cached icon of states frame1..frame22, on the server (icon Blend and Insert); the
            // server renders no icons (D104), so the underlay is the victim's own appearance, faded out frame by frame (update_overlays).
            mob_underlay = Dm.@new<mutable_appearance>(target);
            mob_underlay.plane = FLOAT_PLANE;
            mob_underlay.layer = FLOAT_LAYER;
            update_appearance();

            desc = Dm.text("[]<br>[]", Dm.initial(this, (obj_structure_chrono_field p) => p.desc), span_info(I18n.T("It appears to contain [].", target.name)));
        }

        START_PROCESSING(SSobj, this);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:357
    public override object Destroy(float? force = null)
    {
        if (tem != null)
            tem.field_disconnect(this);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:362
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        float ttk_frame = 1 - (timetokill / Dm.initial(this, (obj_structure_chrono_field p) => p.timetokill));
        ttk_frame = clamp(ceil(ttk_frame * CHRONO_FRAME_COUNT), 1, CHRONO_FRAME_COUNT);
        if (!Dm.eq(ttk_frame, RPpos))
        {
            RPpos = ttk_frame;
            Dm.remove(underlays, mob_underlay);
            // DEVIATION(DV540): tg sets mob_underlay.icon_state = "frame[RPpos]" (its cached icon's frame); the port fades the victim
            // out instead, from whole at frame 1 to nearly gone at the last frame
            mob_underlay.alpha = round(255 * (1 - (ttk_frame - 1) / CHRONO_FRAME_COUNT));
            Dm.append(underlays, mob_underlay);
        }
        return dot;
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:372
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (captured == null)
        {
            qdel(this);
            return null;
        }

        if (timetokill > Dm.initial(this, (obj_structure_chrono_field p) => p.timetokill))
        {
            foreach (atom_movable freed_movable in Dm.each<atom_movable>(contents))
                Waiting.NoSleep(freed_movable.forceMove(drop_location() as atom));
            qdel(this);
        }
        else if (timetokill <= 0)
        {
            to_chat(captured, span_notice(I18n.T("As the last essence of your being is erased from time, you are taken back to your most enjoyable memory. You feel happy...")));
            mob_dead_observer ghost = captured.ghostize(can_reenter_corpse: TRUE) as mob_dead_observer;
            if (captured.mind != null)
            {
                if (ghost != null)
                    ghost.mind = null;
            }
            qdel(captured);
            qdel(this);
        }
        else
        {
            Waiting.NoSleep(captured.Unconscious(8 * SECONDS));
            if (captured.loc != this)
                Waiting.NoSleep(captured.forceMove(this));
            update_appearance();
            if (tem != null)
            {
                if (Dm.truthy(tem.field_check(this)))
                    timetokill -= Dm.num(seconds_per_tick);
                else
                {
                    tem = null;
                    return null;
                }
            }
            else if (!Dm.truthy(attached))
                timetokill -= Dm.num(seconds_per_tick);
            else
                timetokill += Dm.num(seconds_per_tick);
        }
        return null;
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:406
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        if (!istype(hitting_projectile, typeof(obj_projectile_energy_chrono_beam)))
            return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);

        obj_projectile_energy_chrono_beam beam = hitting_projectile as obj_projectile_energy_chrono_beam;
        obj_item_mod_module_tem linked_tem = beam.tem_weakref.resolve() as obj_item_mod_module_tem;
        if (linked_tem != null && istype(linked_tem, typeof(obj_item_mod_module_tem)))
            linked_tem.field_connect(this);
        return BULLET_ACT_HIT;
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:416
    public override async DmTask<object> assume_air(datum_gas_mixture giver = null)
    {
        return FALSE;
    }

    //we always have nominal air and temperature
    // tg: code/modules/mod/modules/modules_timeline.dm:419
    public override async DmTask<object> return_air()
    {
        datum_gas_mixture fresh_air = Dm.@new<datum_gas_mixture>();
        fresh_air.set_gas(typeof(datum_gas_oxygen), MOLES_O2STANDARD);
        fresh_air.set_gas(typeof(datum_gas_nitrogen), MOLES_N2STANDARD);
        fresh_air.temperature = T20C;
        return fresh_air;
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:426
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:429
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:432
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/modules/mod/modules/modules_timeline.dm:435
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        return null;
    }
}
