// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/assembly.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. M4a P4b: the assembly base, as a button's device uses it (pulsed, activate with its cooldown, the secured
// state, examine, the screwdriver and the pick-up check); M5 slice 10: get_part_rating (an assembly's rating as a machine part);
// gaps AS1 (D333): the assembly holder's branches (two unsecured assemblies combined into a holder, an assembly added to one,
// the holder's process_activation reached by pulse) and the window's host and state (ui_host, ui_state).
//
// Gaps F1: an assembly attached to wires (the chem grenade's trigger): `connected` is hand-declared as the wires datum (it was
// generated as Type, types-converter.md 3.5, V3), on_attach and pulse reach the wires. GAP(types): tg stores the wires'
// holder (a grenade, a door) in `holder`, typed /obj/item/assembly_holder; the port's slot is an item (the transfer valve, syndicate
// G4b), so a wired assembly takes its host as `holder` only when the host is an item (gaps AS1: a chem grenade, so an infrared
// emitter's beam starts at it); on another host (a door, an APC) `holder` stays null and it reaches its host through `connected`
// (proximity.cs SensorHost).

// tg: code/modules/assembly/assembly.dm:2
public partial class obj_item_assembly : obj_item
{
    ///The wire datum we connect to
    // tg: code/modules/assembly/assembly.dm:27  (hand-declared: generated as a path, see the header)
    public datum_wires connected;

    // tg: code/modules/assembly/assembly.dm:25  (hand-declared as obj_item, wider than tg's /obj/item/assembly_holder: the tank
    // transfer valve sets itself as the holder of its fuse, syndicate G4b, D316)
    public obj_item holder;

    // tg: code/modules/assembly/assembly.dm:3  (the generated constructor, kept with the hand-declared var: types-converter.md 3.2)
    public obj_item_assembly()
    {
        name = "assembly";
        desc = "A small electronic device that should never exist.";
        icon = Dm.resource("icons/obj/devices/new_assemblies.dmi");
        icon_state = "";
        obj_flags = 4096;
        w_class = 2;
        throwforce = 2;
        throw_speed = 3;
        throw_range = 7;
        drop_sound = Dm.resource("sound/items/handling/component_drop.ogg");
        pickup_sound = Dm.resource("sound/items/handling/component_pickup.ogg");
    }

    // tg: code/modules/assembly/assembly.dm:34
    public override object Destroy(float? force = null)
    {
        holder = null;
        return base.Destroy(force);
    }

    // tg: code/modules/assembly/assembly.dm:38
    public override object get_part_rating()
    {
        return 1;
    }

    /**
     * on_attach: Called when attached to a holder, wiring datum, or other special assembly
     *
     * Will also be called if the assembly holder is attached to a plasma (internals) tank or welding fuel (dispenser) tank.
     */
    // tg: code/modules/assembly/assembly.dm:46
    public virtual async DmTask on_attach()
    {
        if (holder == null && connected != null)
            holder = connected.holder as obj_item; // GAP(types), see the header: null for a host that is not an item
        if (Dm.truthy(this._listen_lookup?[COMSIG_ASSEMBLY_ATTACHED]))
            this._SendSignal(COMSIG_ASSEMBLY_ATTACHED, Dm.list(this, holder));
    }

    /**
     * on_detach: Called when removed from an assembly holder or wiring datum
     */
    // tg: code/modules/assembly/assembly.dm:55
    public virtual async DmTask<object> on_detach()
    {
        if (connected != null)
            connected = null;
        if (holder == null)
            return FALSE;
        atom was_holder = holder;
        holder = null;
        await forceMove(was_holder.drop_location() as atom);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ASSEMBLY_DETACHED]))
            this._SendSignal(COMSIG_ASSEMBLY_DETACHED, Dm.list(this, was_holder));
        return TRUE;
    }

    /**
     * holder_movement: Called when the assembly's holder detects movement
     */
    // tg: code/modules/assembly/assembly.dm:70
    public virtual object holder_movement()
    {
        if (holder == null)
            return FALSE;
        Waiting.NoSleep(setDir(holder.dir));
        return TRUE;
    }

    // tg: code/modules/assembly/assembly.dm:76
    public virtual object is_secured(mob user = null)
    {
        if (!Dm.truthy(secured))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is unsecured!", this)));
            return FALSE;
        }
        return TRUE;
    }

    /**
     * Pulsed: This device was pulsed by another device
     *
     * * pulser: Who triggered the pulse
     */
    // tg: code/modules/assembly/assembly.dm:87
    public virtual object pulsed(mob pulser = null)
    {
        Waiting.Detach(activate(pulser));
        if (Dm.truthy(this._listen_lookup?[COMSIG_ASSEMBLY_PULSED]))
            this._SendSignal(COMSIG_ASSEMBLY_PULSED, Dm.list(this));
        return TRUE;
    }

    /**
     * Pulse: This device is emitting a pulse to act on another device
     */
    // tg: code/modules/assembly/assembly.dm:95
    public virtual async DmTask<object> pulse()
    {
        // if we have connected wires and are a pulsing assembly, pulse it
        if (connected != null)
            await connected.pulse_assembly(this);
        // otherwise if we're attached to a holder, process the activation of it with our flags
        else if (holder != null)
        {
            // holder.process_activation(src): a proc of three unrelated types in tg (the transfer valve, the IED, the assembly holder)
            if (holder is obj_item_transfer_valve valve)
                await valve.process_activation(this);
            else if (holder is obj_item_assembly_holder assembly_holder)
                assembly_holder.process_activation(this);
            else if (holder is obj_item_grenade_iedcasing ied) // the IED (grenades/ghettobomb.dm:83, gaps WP1)
                await ied.process_activation(this);
        }
        return TRUE;
    }

    /// What the device does when turned on
    // tg: code/modules/assembly/assembly.dm:105
    public virtual async DmTask<object> activate(mob activator = null)
    {
        // COOLDOWN_FINISHED(src, next_activate) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (QDELETED(this) || !Dm.truthy(secured) || !(next_activate <= world.time))
            return FALSE;
        // COOLDOWN_START(src, next_activate, activation_cooldown) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        next_activate = world.time + Dm.num(activation_cooldown);
        return TRUE;
    }

    // tg: code/modules/assembly/assembly.dm:111
    public virtual async DmTask<object> toggle_secure()
    {
        secured = Dm.num(!Dm.truthy(secured));
        update_appearance();
        return secured;
    }

    // This is overwritten so that clumsy people can set off mousetraps even when in a holder.
    // We are not going deeper than that however (won't set off if in a tank bomb or anything with wires)
    // That would need to be added to all parent objects, or a signal created, whatever.
    // Anyway this return check prevents you from picking up every assembly inside the holder at once.
    // tg: code/modules/assembly/assembly.dm:120
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (holder != null || connected != null)
            return null;
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/assembly/assembly.dm:125
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // isassembly(tool) (__DEFINES/is_helpers.dm), inline (rules 11.1 (a))
        if (tool is obj_item_assembly new_assembly)
        {
            // Check both our's and their's assembly flags to see if either should not duplicate
            // If so, and we match types, don't create a holder - block it
            if (Dm.truthy(Dm.bitand(Dm.bitor(new_assembly.assembly_flags, assembly_flags), ASSEMBLY_NO_DUPLICATES)) && istype(new_assembly, type))
            {
                balloon_alert(user, I18n.T("can't attach another []!", new_assembly.name));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.truthy(new_assembly.secured))
            {
                balloon_alert(user, I18n.T("[] is not attachable!", new_assembly.name));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.truthy(secured))
            {
                balloon_alert(user, I18n.T("[] is not attachable!", name));
                return ITEM_INTERACT_BLOCKING;
            }

            obj_item_assembly_holder new_holder = Dm.@new<obj_item_assembly_holder>(drop_location());
            holder = new_holder;
            await new_holder.assemble(this, new_assembly, user);
            new_holder.balloon_alert(user, I18n.T("parts combined"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_assembly_holder added_to_holder)
        {
            await added_to_holder.try_add_assembly(this, user);
            return ITEM_INTERACT_BLOCKING;
        }

        return NONE;
    }

    // tg: code/modules/assembly/assembly.dm:154
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await toggle_secure()))
            to_chat(user, span_notice(I18n.T("\\The [] is ready!", this)));
        else
            to_chat(user, span_notice(I18n.T("\\The [] can now be attached!", this)));
        // tg: code/modules/assembly/assembly.dm:159
        add_fingerprint(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/assembly/assembly.dm:162
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("\\The [] []", this, Dm.truthy(secured) ? I18n.T("is secured and ready to be used!") : I18n.T("can be attached to other things."))));
        return dot;
    }

    // tg: code/modules/assembly/assembly.dm:166
    public override object ui_host(mob user = null)
    {
        // In order, return:
        // - The conencted wiring datum's owner, or
        // - The thing your assembly holder is attached to, or
        // - the assembly holder itself, or
        // - us
        return connected?.holder ?? (atom)holder?.master ?? holder ?? (atom)this;
    }

    // tg: code/modules/assembly/assembly.dm:174
    public override object ui_state(mob user = null)
    {
        return GLOB.hands_state;
    }
}
