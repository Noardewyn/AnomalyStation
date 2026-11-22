// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_types/support.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file. The var blocks are generated (the beacon structure has no procs).

/// Quick-moving mob which can teleport things to a beacon and heal its allies
// tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:2
public partial class mob_living_basic_guardian_support : mob_living_basic_guardian
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_healing_touch),
            Dm.named("heal_brute", healing_amount),
            Dm.named("heal_burn", healing_amount),
            Dm.named("heal_tox", healing_amount),
            Dm.named("heal_oxy", healing_amount),
            Dm.named("heal_time", 0),
            Dm.named("action_text", ""),
            Dm.named("complete_text", ""),
            Dm.named("required_modifier", RIGHT_CLICK),
            Dm.named("extra_checks", Dm.@new<datum_callback>(this, nameof(is_deployed))),
            Dm.named("after_healed", Dm.@new<datum_callback>(this, nameof(after_healed)))
        ));

        ADD_TRAIT(this, TRAIT_MEDICAL_HUD, INNATE_TRAIT);

        datum_action_cooldown_mob_cooldown_guardian_bluespace_beacon teleport = Dm.@new<datum_action_cooldown_mob_cooldown_guardian_bluespace_beacon>(this);
        Waiting.NoSleep(teleport.Grant(this));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:36
    public override async DmTask set_guardian_colour(object colour = null)
    {
        await base.set_guardian_colour(colour);
        await AddComponent(typeof(datum_component_healing_touch), Dm.named("heal_color", guardian_colour));
    }

    /// Called after we heal someone, show some visuals
    // tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:41
    public virtual void after_healed(mob_living healed = null)
    {
        Waiting.NoSleep(do_attack_animation(healed, ATTACK_EFFECT_PUNCH));
        healed.visible_message(
            message: span_notice(Dm.text("[] heals []!", this, healed)),
            self_message: span_userdanger(Dm.text("[] heals you!", this)),
            vision_distance: COMBAT_MESSAGE_RANGE,
            ignored_mobs: this
        );
        to_chat(this, span_notice(Dm.text("You heal []!", healed)));
        playsound(healed, attack_sound, 50, TRUE, TRUE, frequency: -1); // play punch sound in REVERSE
    }
}


/// Place a beacon and then listen for clicks to teleport people to it
// tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:54
public partial class datum_action_cooldown_mob_cooldown_guardian_bluespace_beacon : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:70
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        RegisterSignal(owner, COMSIG_MOB_ALTCLICKON, nameof(try_teleporting));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:74
    public override async DmTask Remove(mob removed_from = null)
    {
        UnregisterSignal(owner, COMSIG_MOB_ALTCLICKON);
        await base.Remove(removed_from);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:78
    public override async DmTask<object> Activate(atom target = null)
    {
        turf beacon_loc = owner.loc as turf;
        if (!isfloorturf(beacon_loc))
        {
            owner.balloon_alert(owner, "no room!");
            return FALSE;
        }

        if (beacon != null)
        {
            beacon.visible_message(Dm.text("[] vanishes!", beacon));
            Dm.@new<obj_effect_temp_visual_guardian_phase_out>(beacon.loc);
            qdel(beacon);
        }

        beacon = Dm.@new<obj_structure_guardian_beacon>(beacon_loc, this);
        if (isguardian(owner))
        {
            mob_living_basic_guardian guardian_owner = (mob_living_basic_guardian)owner;
            await beacon.add_atom_colour(guardian_owner.guardian_colour, FIXED_COLOUR_PRIORITY);
        }
        RegisterSignal(beacon, COMSIG_QDELETING, nameof(on_beacon_deleted));
        to_chat(this, span_bolddanger("Beacon placed! You may now warp targets and objects to it, including your user, via Alt+Click.")); // tg sends it to the action (src): nobody reads it
        StartCooldown();
        return TRUE;
    }

    /// Don't hold a reference to a deleted beacon
    // tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:99
    [SignalHandler]
    public virtual void on_beacon_deleted()
    {
        beacon = null;
    }

    /// Try and teleport something to our beacon
    // tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:104
    [SignalHandler]
    public virtual object try_teleporting(mob_living source = null, atom target = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(can_teleport(source, target as atom_movable))))
            return null;

        Waiting.Detach(perform_teleport(source, target)); // INVOKE_ASYNC
        return COMSIG_MOB_CANCEL_CLICKON;
    }

    /// Validate whether we can teleport this object
    // tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:114
    public virtual async DmTask<object> can_teleport(mob_living source = null, atom_movable target = null)
    {
        if (target == null) // Turfs
            return FALSE;
        if (beacon == null)
        {
            source.balloon_alert(source, "no beacon!");
            return FALSE;
        }
        if (isguardian(source))
        {
            mob_living_basic_guardian guardian_mob = (mob_living_basic_guardian)source;
            if (!Dm.truthy(guardian_mob.is_deployed()))
            {
                source.balloon_alert(source, "manifest yourself!");
                return FALSE;
            }
        }
        if (!Dm.truthy(await source.can_perform_action(target)))
        {
            target.balloon_alert(source, "too far!");
            return FALSE;
        }
        if (Dm.truthy(target.anchored))
        {
            target.balloon_alert(source, "it won't budge!");
            return FALSE;
        }
        if ((beacon.z != target.z) && !Dm.@in(target.z, SSmapping.get_connected_levels(beacon.z)))
        {
            target.balloon_alert(source, "too far from beacon!");
            return FALSE;
        }
        return TRUE;
    }

    /// Start teleporting
    // tg: code/modules/mob/living/basic/guardian/guardian_types/support.dm:137
    public virtual async DmTask perform_teleport(mob_living source = null, atom target = null)
    {
        await source.do_attack_animation(target);
        playsound(target, Dm.resource("sound/items/weapons/punch1.ogg"), 50, TRUE, TRUE, frequency: -1);
        source.balloon_alert(source, "teleporting...");
        target.visible_message(
            span_danger(Dm.text("[] starts to glow faintly!", target)),
            span_userdanger("You start to faintly glow, and you feel strangely weightless!"));
        if (!Dm.truthy(await do_after(source, teleport_time, target)))
            return;
        Dm.@new<obj_effect_temp_visual_guardian_phase_out>(target.loc);
        if (isliving(target))
        {
            mob_living living_target = (mob_living)target;
            await living_target.flash_act();
        }
        target.visible_message(
            span_danger(Dm.text("[] disappears in a flash of light!", target)),
            span_userdanger("Your vision is obscured by a flash of light!"));
        await do_teleport(target as atom_movable, beacon, precision: 0, channel: TELEPORT_CHANNEL_BLUESPACE);
        Dm.@new<obj_effect_temp_visual_guardian_phase>(get_turf(target));
    }
}
