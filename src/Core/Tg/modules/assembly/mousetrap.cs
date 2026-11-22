// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/mousetrap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps AS1, D333): the mousetrap, armed and disarmed in hand with its click, its host (the item it is in: its
// wires' holder, its assembly holder's master, the holder, or itself) whose turf it watches, so that a running human (barefoot:
// paralysed with a leg's scratch), a mouse (splat) or a regal rat (too big) stepping onto it, or anything dense, snaps it; the
// clumsy hand that sets it off, the hand that finds it in a bag or wires, the thing thrown at it, the pulse it sends, the snap
// sound and its armed sprite; the armed mousetrap. The var block is generated (D111).

// tg: code/modules/assembly/mousetrap.dm:1
public partial class obj_item_assembly_mousetrap : obj_item_assembly
{
    /**
     * update_host: automatically setup host and host_turf
     *
     * Arguments:
     * * force: Re-register signals even if the host or loc is unchanged
     */
    // tg: code/modules/assembly/mousetrap.dm:20
    public virtual void update_host(float? force = null)
    {
        force ??= FALSE;
        atom newhost;
        // Pick the first valid object in this list:
        // Wiring datum's owner
        // assembly holder's attached object
        // assembly holder itself
        // us
        newhost = connected?.holder ?? (atom)holder?.master ?? holder ?? (atom)this;

        // ok look
        // previously this wasn't working and thus no concern, but I made mousetraps work with wires
        // specifically in step-on-the-mousetrap mode, ie, when you enter its turf
        // and as a consequence, you can put a mousetrap in door wires and it will be set off
        // the first time someone walks through a door (enters the door's loc)
        // that's an interesting mechanic (bolt open a door for example) but it's not appropriate for a mousetrap
        // similarly if used on say an apc's wires it would go into effect when someone walked by it.  Not appropriate.
        // other assemblies could be made to do something similar instead.
        // mousetrap assemblies will still receive on-found notifications when you open a wiring panel
        // and (whether reasonable or not) mousetraps that do this do still trigger wires
        // the point is for now step-on-mousetrap mode should only work on items
        // maybe it should never have been an assembly in the first place.

        // tl;dr only trigger step-on mode if the host is an item
        if (!istype(newhost, typeof(obj_item)))
        {
            if (host != null)
            {
                UnregisterSignal(host, COMSIG_MOVABLE_MOVED);
                host = this;
            }
            if (isturf(host_turf))
            {
                UnregisterSignal(host_turf, COMSIG_ATOM_ENTERED);
                host_turf = null;
            }
            return;
        }

        // If host changed
        if (newhost != host || Dm.truthy(force))
        {
            if (host != null)
                UnregisterSignal(host, COMSIG_MOVABLE_MOVED);
            host = (obj_item)newhost;
            RegisterSignal(host, COMSIG_MOVABLE_MOVED, nameof(holder_movement));
        }

        // If host moved
        if (host_turf != host.loc || Dm.truthy(force))
        {
            if (isturf(host_turf))
            {
                UnregisterSignal(host_turf, COMSIG_ATOM_ENTERED);
                host_turf = null;
            }
            if (isturf(host.loc))
            {
                host_turf = (turf)host.loc;
                RegisterSignal(host_turf, COMSIG_ATOM_ENTERED, nameof(on_entered));
            }
            else
                host_turf = null;
        }
    }

    // tg: code/modules/assembly/mousetrap.dm:70
    public override object holder_movement()
    {
        object dot = base.holder_movement();
        update_host();
        return dot;
    }

    // tg: code/modules/assembly/mousetrap.dm:74
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_host(force: TRUE);
        return dot;
    }

    // tg: code/modules/assembly/mousetrap.dm:78
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The pressure plate is [].", Dm.truthy(armed) ? I18n.T("primed") : I18n.T("safe"))));
        return dot;
    }

    // tg: code/modules/assembly/mousetrap.dm:82
    public override async DmTask<object> activate(mob activator = null)
    {
        if (Dm.truthy(await base.activate(activator)))
        {
            armed = Dm.num(!Dm.truthy(armed));
            if (!Dm.truthy(armed))
            {
                if (ishuman(usr))
                {
                    mob_living_carbon_human user = (mob_living_carbon_human)usr;
                    if ((HAS_TRAIT(user, TRAIT_DUMB) || HAS_TRAIT(user, TRAIT_CLUMSY)) && prob(50))
                    {
                        to_chat(user, span_warning(I18n.T("Your hand slips, setting off the trigger!")));
                        await pulse();
                    }
                }
            }
            update_appearance();
            playsound(loc, Dm.resource("sound/items/weapons/handcuffs.ogg"), 30, TRUE, -3);
        }
        return null;
    }

    // tg: code/modules/assembly/mousetrap.dm:94
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("mousetrap[]", Dm.truthy(armed) ? "armed" : "");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/assembly/mousetrap.dm:98
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        object dot = await base.update_icon(updates, __extra);
        if (holder != null)
            await holder.update_icon(updates);
        return dot;
    }

    // tg: code/modules/assembly/mousetrap.dm:102
    public override async DmTask on_attach()
    {
        await base.on_attach();
        update_host();
    }

    // tg: code/modules/assembly/mousetrap.dm:106
    public override async DmTask<object> on_detach()
    {
        object dot = await base.on_detach();
        update_host();
        return dot;
    }

    // tg: code/modules/assembly/mousetrap.dm:110
    public virtual async DmTask<object> triggered(atom target = null, string type = null)
    {
        type ??= "feet";
        if (!Dm.truthy(armed))
            return null;
        armed = FALSE; // moved to the top because you could trigger it more than once under some circumstances
        update_appearance();
        obj_item_bodypart affecting = null;
        if (ishuman(target))
        {
            mob_living_carbon_human victim = (mob_living_carbon_human)target;
            if (HAS_TRAIT(victim, TRAIT_PIERCEIMMUNE))
            {
                playsound(this, Dm.resource("sound/effects/snap.ogg"), 50, TRUE);
                await pulse();
                return FALSE;
            }
            if (type == "feet")
            {
                if (victim.shoes == null)
                {
                    affecting = victim.get_bodypart((string)pick(GLOB.leg_zones)) as obj_item_bodypart;
                    await victim.Paralyze(6 * SECONDS);
                }
                else
                    to_chat(victim, span_notice(I18n.T("Your [] protects you from [].", victim.shoes.name, this)));
            }
            else if (type == BODY_ZONE_PRECISE_L_HAND || type == BODY_ZONE_PRECISE_R_HAND)
            {
                if (victim.gloves == null)
                {
                    affecting = victim.get_bodypart(type) as obj_item_bodypart;
                    await victim.Stun(6 * SECONDS);
                }
                else
                    to_chat(victim, span_notice(I18n.T("Your [] protects you from [].", victim.gloves.name, this)));
            }
            if (affecting != null)
                await victim.apply_damage(1, BRUTE, affecting, wound_bonus: CANT_WOUND);
        }
        else if (ismouse(target))
        {
            mob_living_basic_mouse splatted = (mob_living_basic_mouse)target;
            visible_message(span_bolddanger(I18n.T("SPLAT!")));
            splatted.splat(); // mousetraps are instadeath for mice
        }
        else if (isregalrat(target))
            visible_message(span_bolddanger(I18n.T("Skreeeee!"))); //He's simply too large to be affected by a tiny mouse trap.

        playsound(this, Dm.resource("sound/effects/snap.ogg"), 50, TRUE);
        await pulse();
        return null;
    }

    /**
     * clumsy_check: Sets off the mousetrap if handled by a clown (with some probability)
     *
     * Arguments:
     * * user: The mob handling the trap
     */
    // tg: code/modules/assembly/mousetrap.dm:154
    public virtual async DmTask<object> clumsy_check(mob user = null)
    {
        if (!Dm.truthy(armed) || user == null)
            return FALSE;
        if ((HAS_TRAIT(user, TRAIT_DUMB) || HAS_TRAIT(user, TRAIT_CLUMSY)) && prob(50))
        {
            string which_hand = BODY_ZONE_PRECISE_L_HAND;
            if (IS_RIGHT_INDEX(user.active_hand_index))
                which_hand = BODY_ZONE_PRECISE_R_HAND;
            await triggered(user, which_hand);
            user.visible_message(span_warning(I18n.T("[] accidentally sets off [], breaking their fingers.", user, this)),
                span_warning(I18n.T("You accidentally trigger []!", this)));
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/assembly/mousetrap.dm:167
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(armed))
            to_chat(user, span_notice(I18n.T("You arm [].", this)));
        else
        {
            if (Dm.truthy(await clumsy_check(user)))
                return null;
            to_chat(user, span_notice(I18n.T("You disarm [].", this)));
        }
        armed = Dm.num(!Dm.truthy(armed));
        update_appearance();
        playsound(this, Dm.resource("sound/items/weapons/handcuffs.ogg"), 30, TRUE, -3);
        return null;
    }

    // Clumsy check only
    // tg: code/modules/assembly/mousetrap.dm:180
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(await clumsy_check(user)))
            return null;
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/assembly/mousetrap.dm:186
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null, params object[] __extra)
    {
        if (Dm.truthy(armed))
        {
            if (ismob(AM))
            {
                mob MM = (mob)AM;
                if (!Dm.truthy(Dm.bitand(MM.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
                {
                    if (ishuman(AM))
                    {
                        mob_living_carbon H = (mob_living_carbon)AM;
                        if (Dm.eq(H.move_intent, MOVE_INTENT_RUN))
                        {
                            Waiting.Detach(triggered(H));
                            H.visible_message(span_warning(I18n.T("[] accidentally steps on [].", H, this)),
                                span_warning(I18n.T("You accidentally step on [].", this)));
                        }
                    }
                    else if (ismouse(MM) || isregalrat(MM))
                        Waiting.Detach(triggered(MM));
                }
            }
            else if (Dm.truthy(AM.density)) // For mousetrap grenades, set off by anything heavy
                Waiting.Detach(triggered(AM));
        }
    }

    // tg: code/modules/assembly/mousetrap.dm:203
    public override async DmTask<object> on_found(mob finder = null)
    {
        if (Dm.truthy(armed))
        {
            if (finder != null)
            {
                finder.visible_message(span_warning(I18n.T("[] accidentally sets off [], breaking their fingers.", finder, this)),
                    span_warning(I18n.T("You accidentally trigger []!", this)));
                await triggered(finder, IS_RIGHT_INDEX(finder.active_hand_index) ? BODY_ZONE_PRECISE_R_HAND : BODY_ZONE_PRECISE_L_HAND);
                return TRUE; //end the search!
            }
            else
            {
                visible_message(span_warning(I18n.T("[] snaps shut!", this)));
                await triggered(loc);
                return FALSE;
            }
        }
        return FALSE;
    }

    // tg: code/modules/assembly/mousetrap.dm:217
    public override async DmTask<object> hitby(atom_movable AM = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        if (!Dm.truthy(armed))
            return await base.hitby(AM, skipcatch, hitpush, blocked, throwingdatum);
        visible_message(span_warning(I18n.T("[] is triggered by [].", this, AM)));
        await triggered(null);
        return null;
    }

    // tg: code/modules/assembly/mousetrap.dm:224
    public override object Destroy(float? force = null)
    {
        if (host != null)
        {
            UnregisterSignal(host, COMSIG_MOVABLE_MOVED);
            host = null;
        }
        if (isturf(host_turf))
        {
            UnregisterSignal(host_turf, COMSIG_ATOM_ENTERED);
            host_turf = null;
        }
        return base.Destroy(force);
    }
}
