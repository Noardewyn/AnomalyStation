// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/stray_bullet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The fake projectile is a real zero-damage projectile of the gun core (M7x) that the hallucinator's client alone
// sees as an image and hears; the blood and hit effects are short-lived private images. The var blocks are generated (D111).

/// Shoots a random, fake projectile to the hallucinator
// tg: code/modules/hallucination/stray_bullet.dm:2
public partial class datum_hallucination_stray_bullet : datum_hallucination
{
    // tg: code/modules/hallucination/stray_bullet.dm:7
    public override async DmTask<object> start()
    {
        DmList starting_locations = Dm.list();
        // world.view is the text "15x15" in the port; tg's number is the view's radius (7)
        float view_radius = floor(Dm.num((getviewsize(world.view) as DmList)[1]) / 2);
        DmList outside = Dm.sub(view(view_radius + 1, hallucinator), view(view_radius, hallucinator)) as DmList;
        foreach (turf_open open_out_of_view in Dm.each<turf_open>(outside))
            Dm.append(starting_locations, open_out_of_view);
        if (length(starting_locations) == 0)
            return FALSE;

        turf start = pick(starting_locations) as turf;
        Type fake_type = pick(subtypesof(typeof(obj_projectile_hallucination))) as Type;

        feedback_details += Dm.text("Type: [], Source: ([], [], [])", fake_type, start.x, start.y, start.z);

        obj_projectile_hallucination fake_projectile = (obj_projectile_hallucination)Dm.@new(fake_type, start, this);

        await fake_projectile.aim_projectile(hallucinator, start);
        await fake_projectile.fire();

        QDEL_IN_HALLUCINATION(this, 10 * SECONDS, "code/modules/hallucination/stray_bullet.dm", 24); // Should clean up the projectile if it somehow gets stuck.
        return TRUE;
    }
}

/// Hallucinated projectiles.
// tg: code/modules/hallucination/stray_bullet.dm:27
public partial class obj_projectile_hallucination : obj_projectile
{
    // tg: code/modules/hallucination/stray_bullet.dm:55
    [DmParams("mapload", "parent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_hallucination parent = Dm.arg<datum_hallucination>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, parent));
        if (parent == null)
        {
            stack_trace(Dm.text("[] was created without a parent hallucination.", type), "code/modules/hallucination/stray_bullet.dm", 58);
            return INITIALIZE_HINT_QDEL;
        }

        this.parent = parent;
        RegisterSignal(parent, COMSIG_QDELETING, nameof(parent_deleting));
        return dot;
    }

    // tg: code/modules/hallucination/stray_bullet.dm:65
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(parent.hallucinator) && parent.hallucinator.client != null)
            Dm.remove(parent.hallucinator.client.images, fake_bullet);
        fake_bullet = null;

        UnregisterSignal(parent, COMSIG_QDELETING);
        parent = null;
        return base.Destroy(force);
    }

    /// Signal proc for [COMSIG_QDELETING], if our associated hallucination deletes, we need to clean up
    // tg: code/modules/hallucination/stray_bullet.dm:75
    [SignalHandler]
    public virtual void parent_deleting(datum source = null)
    {
        qdel(this);
    }

    // tg: code/modules/hallucination/stray_bullet.dm:80
    public override async DmTask fire(object fire_angle = null, atom direct_target = null)
    {
        if (Dm.truthy(hal_fire_sound))
            parent.hallucinator.playsound_local(get_turf(this), hal_fire_sound, 60, TRUE);

        fake_bullet = image(hal_icon, this, hal_icon_state, ABOVE_MOB_LAYER);
        if (parent.hallucinator.client != null)
            Dm.append(parent.hallucinator.client.images, fake_bullet);
        await base.fire(fire_angle, direct_target);
    }

    // tg: code/modules/hallucination/stray_bullet.dm:87
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!Dm.eq(dot, BULLET_ACT_HIT))
            return dot;

        if (ismob(target))
        {
            if (Dm.truthy(hal_hitsound))
                parent.hallucinator.playsound_local(target, hal_hitsound, 100, 1);
            await on_mob_hit(target as mob_living);
        }

        else
        {
            if (Dm.truthy(hal_hitsound_wall))
                parent.hallucinator.playsound_local(loc, hal_hitsound_wall, 40, 1);
            if (Dm.truthy(hal_impact_effect_wall))
                spawn_hit(target, TRUE);
        }

        qdel(this);
        return dot;
    }

    /// Called when a mob is hit by the fake projectile
    // tg: code/modules/hallucination/stray_bullet.dm:105
    public virtual async DmTask on_mob_hit(mob_living hit_mob = null)
    {
        if (hit_mob == parent.hallucinator)
        {
            to_chat(parent.hallucinator, span_userdanger(Dm.text("[] is hit by \\a [] in the chest!", hit_mob, this)));
            await apply_effect_to_hallucinator(parent.hallucinator);
        }

        else if (Dm.@in(hit_mob, view(parent.hallucinator)))
            to_chat(parent.hallucinator, span_danger(Dm.text("[] is hit by \\a [] in the chest!", hit_mob, this)));

        if (Dm.eq(damage_type, BRUTE))
        {
            float splatter_dir = dir;
            if (starting != null)
                splatter_dir = Dm.num(get_dir(starting, get_turf(hit_mob)));
            spawn_blood(hit_mob, splatter_dir);
        }

        else if (Dm.truthy(hal_impact_effect))
            spawn_hit(hit_mob, FALSE);
    }

    /// Called when the hallucinator themselves are hit by the fake projectile
    // tg: code/modules/hallucination/stray_bullet.dm:123
    public virtual async DmTask apply_effect_to_hallucinator(mob_living afflicted = null)
    {
        return;
    }

    /// Called after a mob is hit by the fake projectile, and our fake projectile is of brute type, to create fake blood
    // tg: code/modules/hallucination/stray_bullet.dm:127
    public virtual void spawn_blood(mob_living bleeding = null, object set_dir = null)
    {
        if (parent.hallucinator.client == null) // Purely visual, don't need to do this for clientless mobs
            return;

        string splatter_icon_state;
        if (Dm.truthy(ISDIAGONALDIR(Dm.num(set_dir))))
            splatter_icon_state = Dm.text("splatter[]", pick(1, 2, 6));
        else
            splatter_icon_state = Dm.text("splatter[]", pick(3, 4, 5));

        image blood = image(Dm.resource("icons/effects/blood.dmi"), bleeding, splatter_icon_state, ABOVE_MOB_LAYER);
        float target_pixel_x = 0;
        float target_pixel_y = 0;
        float sdir = Dm.num(set_dir);
        if (sdir == NORTH)
            target_pixel_y = 16;
        else if (sdir == SOUTH)
        {
            target_pixel_y = -16;
            layer = ABOVE_MOB_LAYER;
        }
        else if (sdir == EAST)
            target_pixel_x = 16;
        else if (sdir == WEST)
            target_pixel_x = -16;
        else if (sdir == NORTHEAST)
        {
            target_pixel_x = 16;
            target_pixel_y = 16;
        }
        else if (sdir == NORTHWEST)
        {
            target_pixel_x = -16;
            target_pixel_y = 16;
        }
        else if (sdir == SOUTHEAST)
        {
            target_pixel_x = 16;
            target_pixel_y = -16;
            layer = ABOVE_MOB_LAYER;
        }
        else if (sdir == SOUTHWEST)
        {
            target_pixel_x = -16;
            target_pixel_y = -16;
            layer = ABOVE_MOB_LAYER;
        }

        Dm.combine(parent.hallucinator.client.images, blood);
        animate(blood, Dm.named("pixel_x", target_pixel_x), Dm.named("pixel_y", target_pixel_y), Dm.named("alpha", 0), Dm.named("time", 5));
        _addtimer(Dm.@new<datum_callback>(this, nameof(clean_up_blood), blood), 0.5f * SECONDS, file: "code/modules/hallucination/stray_bullet.dm", line: 170);
    }

    // tg: code/modules/hallucination/stray_bullet.dm:172
    public virtual void clean_up_blood(image blood = null)
    {
        if (parent?.hallucinator?.client != null)
            Dm.remove(parent.hallucinator.client.images, blood);
    }

    /// Called with a non-mob atom was hit by our fake projectile, or a mob was hit and our damge type is not brute
    // tg: code/modules/hallucination/stray_bullet.dm:176
    public virtual void spawn_hit(atom hit_atom = null, object is_wall = null)
    {
        if (parent.hallucinator.client == null) // Purely visual, don't need to do this for clientless mobs
            return;

        image hit_effect = image(Dm.resource("icons/effects/blood.dmi"), hit_atom, Dm.str(Dm.truthy(is_wall) ? hal_impact_effect_wall : hal_impact_effect), ABOVE_MOB_LAYER);
        hit_effect.pixel_w = hit_atom.pixel_x + rand(-4, 4);
        hit_effect.pixel_z = hit_atom.pixel_y + rand(-4, 4);
        Dm.combine(parent.hallucinator.client.images, hit_effect);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clean_up_hit), hit_effect), Dm.num(Dm.truthy(is_wall) ? hit_duration_wall : hit_duration), file: "code/modules/hallucination/stray_bullet.dm", line: 184);
    }

    // tg: code/modules/hallucination/stray_bullet.dm:186
    public virtual void clean_up_hit(image hit_effect = null)
    {
        if (parent?.hallucinator?.client != null)
            Dm.remove(parent.hallucinator.client.images, hit_effect);
    }
}

// tg: code/modules/hallucination/stray_bullet.dm:199
public partial class obj_projectile_hallucination_bullet : obj_projectile_hallucination
{
    // tg: code/modules/hallucination/stray_bullet.dm:208
    public override async DmTask apply_effect_to_hallucinator(mob_living afflicted = null)
    {
        await afflicted.adjust_stamina_loss(60);
    }
}

// tg: code/modules/hallucination/stray_bullet.dm:211
public partial class obj_projectile_hallucination_laser : obj_projectile_hallucination
{
    // tg: code/modules/hallucination/stray_bullet.dm:226
    public override async DmTask apply_effect_to_hallucinator(mob_living afflicted = null)
    {
        await afflicted.adjust_stamina_loss(20);
        await afflicted.adjust_eye_blur(4 * SECONDS);
    }
}

// tg: code/modules/hallucination/stray_bullet.dm:229
public partial class obj_projectile_hallucination_disabler : obj_projectile_hallucination
{
    // tg: code/modules/hallucination/stray_bullet.dm:243
    public override async DmTask apply_effect_to_hallucinator(mob_living afflicted = null)
    {
        await afflicted.adjust_stamina_loss(30);
    }
}

// tg: code/modules/hallucination/stray_bullet.dm:246
public partial class obj_projectile_hallucination_ebow : obj_projectile_hallucination
{
    // tg: code/modules/hallucination/stray_bullet.dm:256
    public override async DmTask apply_effect_to_hallucinator(mob_living afflicted = null)
    {
        await afflicted.adjust_slurring(10 * SECONDS);
        await afflicted.Knockdown(1 * SECONDS);
        await afflicted.adjust_stamina_loss(75); // 60 stam + 15 tox
        await afflicted.adjust_eye_blur(20 * SECONDS);
    }
}

// tg: code/modules/hallucination/stray_bullet.dm:262
public partial class obj_projectile_hallucination_change : obj_projectile_hallucination
{
    // tg: code/modules/hallucination/stray_bullet.dm:272
    public override async DmTask apply_effect_to_hallucinator(mob_living afflicted = null)
    {
        // Future idea: Make it so any other mob hit appear to be polymorphed to the hallucinator
        await afflicted.cause_hallucination(
            get_random_valid_hallucination_subtype(typeof(datum_hallucination_delusion_preset)),
            Dm.text("fake []", name),
            Dm.named("duration", 30 * SECONDS),
            Dm.named("affects_us", TRUE),
            Dm.named("affects_others", FALSE),
            Dm.named("skip_nearby", FALSE),
            Dm.named("play_wabbajack", TRUE));
    }
}

// tg: code/modules/hallucination/stray_bullet.dm:285
public partial class obj_projectile_hallucination_death : obj_projectile_hallucination
{
    // tg: code/modules/hallucination/stray_bullet.dm:294
    public override async DmTask apply_effect_to_hallucinator(mob_living afflicted = null)
    {
        await afflicted.cause_hallucination(typeof(datum_hallucination_death), Dm.text("fake []", name));
    }
}
