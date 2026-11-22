// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/burning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M5 slice 1: a flammable object burning down through atom integrity). The var block is generated (D111).
// The fire particles (add_shared_particles and the particle holder: particles, a client visual) are a deferred leaf (the scream of a
// burnt hand: M9 slice B); the fire overlay (GLOB.fire_overlay) is drawn.

// tg: code/datums/components/burning.dm:1  GLOBAL_DATUM_INIT(fire_overlay, /mutable_appearance, mutable_appearance('icons/effects/fire.dmi', "fire", appearance_flags = RESET_COLOR|KEEP_APART))
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/datums/components/burning.dm:1  (GLOBAL_RAW(/mutable_appearance/fire_overlay), __DEFINES/__globals.dm:32)
    public object fire_overlay;

    // tg: code/datums/components/burning.dm:1  (GLOBAL_MANAGED(fire_overlay, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfire_overlay()
    {
        fire_overlay = mutable_appearance(Dm.resource("icons/effects/fire.dmi"), "fire", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART));
        Dm.append(ref gvars_datum_init_order, "fire_overlay");
    }
}

/**
 * Component representing an atom being on fire.
 * Should not be used on mobs, they use the fire stacks status effects.
 * Can only be used on atoms that use the integrity system.
 */
// tg: code/datums/components/burning.dm:8
public partial class datum_component_burning : datum_component
{
    // tg: code/datums/components/burning.dm:16
    [DmParams("fire_overlay", "fire_particles")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object fire_overlay = Dm.arg(__extra, 0) ?? GLOB.fire_overlay;
        Type fire_particles = Dm.arg(__extra, 1) as Type ?? typeof(particles_smoke_burning);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        atom atom_parent = parent as atom;
        if (!Dm.truthy(atom_parent.uses_integrity))
        {
            stack_trace(Dm.text("Tried to add /datum/component/burning to an atom ([]) that does not use atom_integrity!", atom_parent.GetType()), "code/datums/components/burning.dm", 21);
            return COMPONENT_INCOMPATIBLE;
        }

        // only flammable atoms should have this component, but it's not really an error if we try to apply this to a non flammable one
        if (!Dm.truthy(Dm.bitand(atom_parent.resistance_flags, FLAMMABLE)) || Dm.truthy(Dm.bitand(atom_parent.resistance_flags, FIRE_PROOF)))
        {
            qdel(this);
            return null;
        }

        this.fire_overlay = fire_overlay;
        if (fire_particles != null)
        {
            if (ismovable(parent))
            {
                // movable_parent.add_shared_particles(fire_particles, "[fire_particles]_[isitem(parent)]", isitem(parent) ? NONE :
                // PARTICLE_ATTACH_MOB): particles (a client visual) are not drawn yet; the overlay shows the fire.
                Dm.Deferred("code/datums/components/burning.dm:34", "Initialize: the burning smoke particles (add_shared_particles, a client visual) (M10+)");
                particle_type = fire_particles;
            }
            else
            {
                // particle_effect = new(atom_parent, fire_particles): the particle holder (game/objects/effects/particle_holder.dm) is
                // a skeleton (particles, a client visual).
                Dm.Deferred("code/datums/components/burning.dm:37", "Initialize: the burning particle holder of a turf (particles, a client visual) (M10+)");
            }
        }
        START_PROCESSING(SSburning, this);
        return null;
    }

    // tg: code/datums/components/burning.dm:40
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSburning, this);
        fire_overlay = null;
        if (particle_effect != null)
            QDEL_NULL(ref particle_effect);
        if (ismovable(parent) && particle_type != null)
        {
            // movable_parent.remove_shared_particles("[particle_type]_[isitem(parent)]"): see Initialize's deferred leaf.
            particle_type = null;
        }
        return base.Destroy(force);
    }

    // tg: code/datums/components/burning.dm:50
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(on_attack_hand));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_EXTINGUISH, nameof(on_extinguish));
        atom atom_parent = parent as atom;
        Dm.combine(ref atom_parent.resistance_flags, ON_FIRE);
        atom_parent.update_appearance();
    }

    // tg: code/datums/components/burning.dm:59
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_ATTACK_HAND,
            COMSIG_ATOM_UPDATE_OVERLAYS,
            COMSIG_ATOM_EXAMINE,
            COMSIG_ATOM_EXTINGUISH
        ));
        atom atom_parent = parent as atom;
        if (!QDELETED(atom_parent))
        {
            Dm.mask(ref atom_parent.resistance_flags, Dm.bitnot(ON_FIRE));
            atom_parent.update_appearance();
        }
    }

    // tg: code/datums/components/burning.dm:71
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        atom atom_parent = parent as atom;
        // Check if the parent somehow became fireproof, remove component if so
        if (Dm.truthy(Dm.bitand(atom_parent.resistance_flags, FIRE_PROOF)))
        {
            await atom_parent.extinguish();
            return null;
        }
        await atom_parent.take_damage(10 * Dm.num(seconds_per_tick), BURN, FIRE, FALSE);
        return null;
    }

    /// Alerts any examiners that the parent is on fire (even though it should be rather obvious)
    // tg: code/datums/components/burning.dm:80
    [SignalHandler]
    public virtual void on_examine(atom source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_danger(I18n.T("[] burning!", source.p_Theyre())));
    }

    /// Handles searing the hand of anyone who tries to touch parent without protection.
    // tg: code/datums/components/burning.dm:86
    [SignalHandler]
    public virtual object on_attack_hand(atom source = null, mob_living_carbon user = null)
    {
        if (!iscarbon(user) || Dm.truthy(user.can_touch_burning(source)))
        {
            to_chat(user, span_notice(I18n.T("You put out the fire on [].", source)));
            Waiting.NoSleep(source.extinguish());
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        Waiting.NoSleep(user.apply_damage(5, BURN, user.get_active_hand()));
        to_chat(user, span_userdanger(I18n.T("You burn your hand on []!", source)));
        Waiting.Detach(user.emote("scream")); // INVOKE_ASYNC
        playsound(source, SFX_SEAR, 50, TRUE);
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// Maintains the burning overlay on the parent atom
    // tg: code/datums/components/burning.dm:101
    [SignalHandler]
    public virtual void on_update_overlays(atom source = null, DmList overlays = null)
    {
        //most likely means the component is being removed
        if (!Dm.truthy(Dm.bitand(source.resistance_flags, ON_FIRE)))
            return;

        if (Dm.truthy(fire_overlay))
            Dm.append(ref overlays, fire_overlay);
    }

    /// Deletes the component when the atom gets extinguished
    // tg: code/datums/components/burning.dm:112
    [SignalHandler]
    public virtual void on_extinguish(atom source = null, DmList overlays = null)
    {
        qdel(this);
    }
}
