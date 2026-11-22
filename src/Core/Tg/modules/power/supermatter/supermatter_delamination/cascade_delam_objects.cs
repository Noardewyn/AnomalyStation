// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 delamination slice (G6, D163): the whole file, the resonance cascade's crystal mass and its evacuation rift. The logs
// and admin messages are deferred (M10); the rift's arrival at CentCom waits for the CentCom level (D108, M10). The var blocks
// are generated (D111).

// tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:1
public partial class obj_crystal_mass : obj
{
    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:23
    [DmParams("mapload", "dir_to_remove")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dir_to_remove = Dm.arg(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("crystal_cascade_[]", rand(1, 6));
        START_PROCESSING(SSsupermatter_cascade, this);

        sm_comp = Waiting.NoSleep(AddComponent(typeof(datum_component_supermatter_crystal), null, null)) as datum_component_supermatter_crystal;

        playsound(this, Dm.resource("sound/misc/cracking_crystal.ogg"), 45, TRUE);

        Dm.remove(ref available_dirs, dir_to_remove);

        turf our_turf = get_turf(this);

        if (Dm.truthy(our_turf))
            our_turf.opacity = FALSE;

        // Ideally this'd be part of the SM component, but the SM itself snowflakes bullets (emitters are bullets).
        RegisterSignal(this, COMSIG_ATOM_PRE_BULLET_ACT, nameof(eat_bullets));
        return dot;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:43  (set waitfor = FALSE, inherited from
    // /datum/process: the wrapper of waiting-model.md 5.4)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null);
    }

    private async DmTask<object> process__body()
    {
        if (!(sm_wall_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return null;

        if (!Dm.truthy(available_dirs) || available_dirs.len <= 0)
            return PROCESS_KILL;

        sm_wall_cooldown = world.time + rand(0, 3 * SECONDS); // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))

        object picked_dir = pick_n_take(available_dirs);
        turf next_turf = get_step_multiz(this, picked_dir) as turf;

        icon_state = Dm.text("crystal_cascade_[]", rand(1, 6));

        if (!Dm.truthy(next_turf) || Dm.truthy(Dm.locate<obj_crystal_mass>(next_turf)))
            return null;

        foreach (atom_movable checked_atom in Dm.each_cast<atom_movable>(next_turf))
        {
            if (isliving(checked_atom))
            {
                await sm_comp.dust_mob(this, (mob_living)checked_atom, span_danger(I18n.T("\\The [] lunges out on [], touching []... [] body begins to shine with a brilliant light before crystallizing from the inside out and joining \\the []!", this, checked_atom, checked_atom.p_them(), checked_atom.p_their(), this)),
                    span_userdanger(I18n.T("The crystal mass lunges on you and hits you in the chest. As your vision is filled with a blinding light, you think to yourself \"Damn it.\"")));
            }
            else if (checked_atom is obj_cascade_portal)
            {
                checked_atom.visible_message(span_userdanger(I18n.T("\\The [] screeches and closes away as it is hit by \\a []! Too late!", checked_atom, this)));
                playsound(get_turf(checked_atom), Dm.resource("sound/effects/magic/charge.ogg"), 50, TRUE);
                playsound(get_turf(checked_atom), Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
                qdel(checked_atom);
            }
            else if (isitem(checked_atom))
            {
                playsound(get_turf(checked_atom), Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
                qdel(checked_atom);
            }
        }

        Dm.@new<obj_crystal_mass>(next_turf, get_dir(next_turf, this));
        return null;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:78
    [SignalHandler]
    public virtual object eat_bullets(datum source = null, obj_projectile hitting_projectile = null)
    {
        visible_message(
            span_warning(I18n.T("[] flies into [] with a loud crack, before rapidly flashing into ash.", hitting_projectile, this)),
            null,
            span_hear(I18n.T("You hear a loud crack as you are washed with a wave of heat."))
        );

        playsound(this, Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
        qdel(hitting_projectile);
        return COMPONENT_BULLET_BLOCKED;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:91
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:94  (the /atom root, atom_act.dm, is not
    // translated: the slot's first declaration on this branch of the tree, as the crystal's own, supermatter_hit_procs.cs)
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (!iscarbon(user))
            return null;
        mob_living_carbon jedi = (mob_living_carbon)user;
        to_chat(jedi, span_userdanger(I18n.T("That was a really dense idea.")));
        jedi.ghostize();
        obj_item_organ_brain rip_u = Dm.locate<obj_item_organ_brain>(jedi.organs);
        if (Dm.truthy(rip_u))
        {
            await rip_u.Remove(jedi);
            qdel(rip_u);
        }
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:105
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSsupermatter_cascade, this);
        sm_comp = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:110
public partial class obj_cascade_portal : obj
{
    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:126
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        var location = get_turf(this) as turf;
        object area_name = get_area_name(this);
        message_admins(Dm.text("Exit rift created at []. []", area_name, ADMIN_VERBOSEJMP(location)));
        log_game(Dm.text("Bluespace Exit Rift was created at [].", area_name));
        investigate_log(Dm.text("created at [].", area_name), INVESTIGATE_ENGINE);
        return dot;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:134
    public override object Destroy(float? force = null)
    {
        var location = get_turf(this) as turf;
        object area_name = get_area_name(this);
        message_admins(Dm.text("Exit rift at [] deleted. []", area_name, ADMIN_VERBOSEJMP(location)));
        log_game(Dm.text("Bluespace Exit Rift at [] was deleted.", area_name));
        investigate_log("was deleted.", INVESTIGATE_ENGINE);
        return base.Destroy(force);
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:142
    public override object Bumped(atom_movable hit_object = null)
    {
        Waiting.NoSleep(consume(hit_object));
        Dm.@new<obj_effect_particle_effect_sparks>(loc);
        playsound(loc, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        return null;
    }

    /**
     * Proc to consume the objects colliding with the portal
     *
     * Arguments: atom/movable/consumed_object is the object hitting the portal
     */
    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:152
    public virtual async DmTask consume(atom_movable consumed_object = null)
    {
        if (isliving(consumed_object))
        {
            consumed_object.visible_message(span_danger(I18n.T("\\The [] walks into \\the []... A blinding light covers [] body before disappearing completely!", consumed_object, this, consumed_object.p_their())),
                span_userdanger(I18n.T("You walk into \\the [] as your body is washed with a powerful blue light. You contemplate about this decision before landing face first onto the cold, hard floor.", this)),
                span_hear(I18n.T("You hear a loud crack as a distortion passes through you.")));

            DmList arrival_turfs = get_area_turfs(typeof(area_centcom_central_command_areas_evacuation)) as DmList;
            // GAP(lead): D108 loads no CentCom level (DV190: its docks are stand-ins in reservations), so its evacuation area has no
            // turf, where tg's do-while below would pick forever: the living one stays where the rift is (the CentCom level, M10+)
            if (!Dm.truthy(length(arrival_turfs)))
            {
                Dm.Deferred("code/modules/power/supermatter/supermatter_delamination/cascade_delam_objects.dm:160", "consume: the rift's arrival at CentCom's evacuation area (the CentCom level is not loaded, D108, DV190; M10+)");
                return;
            }
            turf arrival_turf = null;
            do
            {
                arrival_turf = pick_n_take(arrival_turfs) as turf;
            }
            while (!Dm.truthy(is_safe_turf(arrival_turf)));

            mob_living consumed_mob = (mob_living)consumed_object;
            message_admins(Dm.text("[] has entered [] [].", key_name_admin(consumed_mob), this, ADMIN_VERBOSEJMP(this)));
            investigate_log(Dm.text("was entered by [].", key_name(consumed_mob)), INVESTIGATE_ENGINE);
            await consumed_mob.forceMove(arrival_turf);
            await consumed_mob.Paralyze(100);
            await consumed_mob.adjust_brute_loss(30);
            await consumed_mob.flash_act(1, TRUE, TRUE);

            Dm.@new<obj_effect_particle_effect_sparks>(consumed_object);
            playsound(consumed_object, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        }
        else if (isitem(consumed_object))
        {
            consumed_object.visible_message(span_danger(I18n.T("\\The [] smacks into \\the [] and disappears out of sight.", consumed_object, this)), null,
                span_hear(I18n.T("You hear a loud crack as a small distortion passes through you.")));

            qdel(consumed_object);
        }
    }
}
