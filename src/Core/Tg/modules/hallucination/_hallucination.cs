// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/_hallucination.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The images reach the hallucinator through client.images, which the replicator sends to that session
// alone (D40); an image that hangs on an invisible holder is drawn on the holder's tile (the replicator, DV112). The var blocks are generated (D111).

/**
 * # Hallucination datum.
 *
 * Handles effects of a hallucination on a living mob.
 * Created and triggered via the [cause hallucination proc][/mob/living/proc/cause_hallucination].
 *
 * See also: [the hallucination status effect][/datum/status_effect/hallucination].
 */
// tg: code/modules/hallucination/_hallucination.dm:10
public partial class datum_hallucination : datum
{
    // tg: code/modules/hallucination/_hallucination.dm:21
    [DmParams("hallucinator")]
    public override async DmTask New(params object[] __extra)
    {
        object hallucinator = Dm.arg(__extra, 0);
        if (!isliving(hallucinator))
        {
            stack_trace(Dm.text("[] was created without a hallucinating mob.", type), "code/modules/hallucination/_hallucination.dm", 22);
            qdel(this);
            return;
        }

        this.hallucinator = (mob_living)hallucinator;
        RegisterSignal(this.hallucinator, COMSIG_QDELETING, nameof(target_deleting));
        Dm.append(GLOB.all_ongoing_hallucinations, this);
    }

    /// Signal proc for [COMSIG_QDELETING], if the mob hallucinating us is deletes, we should delete too.
    // tg: code/modules/hallucination/_hallucination.dm:32
    [SignalHandler]
    public virtual void target_deleting()
    {
        qdel(this);
    }

    // QDEL_IN(item, time) and QDEL_IN_STOPPABLE(item, time) (__DEFINES/qdel.dm:58-61) expanded once for the many uses of this file's hallucinations
    // (rules 11.1 (f) wants them inline, each with its use site's file and line; the lines named here are those of the first use).
    // tg: code/__DEFINES/qdel.dm:58
    internal static object QDEL_IN_HALLUCINATION(datum item, float time, string file, int line, bool stoppable = false)
        => _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), time > GC_FILTER_QUEUE ? WEAKREF(item) : item), time, stoppable ? TIMER_STOPPABLE : null, file: file, line: line);

    /// Starts the hallucination.
    // tg: code/modules/hallucination/_hallucination.dm:38  SHOULD_CALL_PARENT(FALSE)
    public virtual async DmTask<object> start()
    {
        stack_trace(Dm.text("[] didn't implement any hallucination effects in start.", type), "code/modules/hallucination/_hallucination.dm", 40);
        return null;
    }

    // tg: code/modules/hallucination/_hallucination.dm:43
    public override object Destroy(float? force = null)
    {
        if (hallucinator != null)
        {
            UnregisterSignal(hallucinator, COMSIG_QDELETING);
            hallucinator = null;
        }

        Dm.remove(GLOB.all_ongoing_hallucinations, this);
        return base.Destroy(force);
    }

    /// Returns a random turf in a ring around the hallucinator mob.
    /// Useful for sound hallucinations.
    // tg: code/modules/hallucination/_hallucination.dm:52
    public virtual object random_far_turf()
    {
        float first_offset = Dm.num(pick(-8, -7, -6, -5, 5, 6, 7, 8));
        float second_offset = rand(-8, 8);
        float x_offset;
        float y_offset;
        if (prob(50))
        {
            x_offset = first_offset;
            y_offset = second_offset;
        }
        else
        {
            x_offset = second_offset;
            y_offset = first_offset;
        }

        return locate(hallucinator.x + x_offset, hallucinator.y + y_offset, hallucinator.z);
    }

    /// Gets a random non-security member of the crew that is at least 8 tiles away.
    // tg: code/modules/hallucination/_hallucination.dm:67
    public virtual object random_non_sec_crewmember()
    {
        DmList possible_fakes = Dm.list();
        foreach (datum_mind possible_fake in Dm.each_cast<datum_mind>(get_crewmember_minds()))
        {
            // Sec won't make sense. (Neither will cap but we'll just let it slide)
            if (Dm.truthy(Dm.bitand(possible_fake.assigned_role.departments_bitflags, DEPARTMENT_BITFLAG_SECURITY)))
                continue;
            // Look for minds on the manifest in control of humans
            mob_living_carbon_human fake_body = possible_fake.current as mob_living_carbon_human;
            if (fake_body == null || fake_body == hallucinator)
                continue;
            // This also won't make sense in most cases
            if (get_dist(fake_body, hallucinator) < 8)
                continue;
            Dm.append(possible_fakes, fake_body);
        }

        return length(possible_fakes) != 0 ? pick(possible_fakes) : null;
    }
}

/**
 * Simple effect that holds an image
 * to be shown to one or multiple clients only.
 *
 * Pass a list of mobs in initialize() that corresponds to all mobs that can see it.
 */
// tg: code/modules/hallucination/_hallucination.dm:90
public partial class obj_effect_client_image_holder : obj_effect
{
    // tg: code/modules/hallucination/_hallucination.dm:115
    [DmParams("mapload", "mobs_which_see_us")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object mobs_which_see_us = Dm.arg(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (mobs_which_see_us == null)
        {
            stack_trace("Client image holder was created with no mobs to see it.", "code/modules/hallucination/_hallucination.dm", 117);
            return INITIALIZE_HINT_QDEL;
        }

        shown_image = generate_image() as image;

        if (!islist(mobs_which_see_us))
            mobs_which_see_us = Dm.list(mobs_which_see_us);

        who_sees_us = Dm.list();
        foreach (mob seer in Dm.each_cast<mob>(mobs_which_see_us))
            add_seer(seer);
        return dot;
    }

    // tg: code/modules/hallucination/_hallucination.dm:131
    public override object Destroy(float? force = null)
    {
        if (shown_image != null)
        {
            foreach (mob seer in Dm.each_cast<mob>(who_sees_us.Copy()))
                remove_seer(seer);
            shown_image = null;
        }

        who_sees_us.Cut(); // probably not needed but who knows
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/_hallucination.dm:140
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (QDELETED(this) || Dm.truthy(same_z_layer))
            return;
        SET_PLANE(shown_image, Dm.num(PLANE_TO_TRUE(shown_image.plane)), new_turf);
    }

    // tg: code/modules/hallucination/_hallucination.dm:146
    public virtual void add_seer(mob new_seer = null)
    {
        RegisterSignal(new_seer, COMSIG_MOB_LOGIN, nameof(show_image_to));
        RegisterSignal(new_seer, COMSIG_QDELETING, nameof(remove_seer));
        Dm.append(who_sees_us, new_seer);
        show_image_to(new_seer);
    }

    /// Signal proc to clean up references if people who see us are deleted.
    // tg: code/modules/hallucination/_hallucination.dm:153
    [SignalHandler]
    public virtual void remove_seer(mob source = null)
    {
        UnregisterSignal(source, Dm.list(COMSIG_MOB_LOGIN, COMSIG_QDELETING));
        hide_image_from(source);
        Dm.remove(who_sees_us, source);

        // No reason to exist, anymore
        if (!QDELETED(this) && length(who_sees_us) == 0 && !Dm.truthy(persist_without_seers))
            qdel(this);
    }

    /// Generates the image which we take on.
    // tg: code/modules/hallucination/_hallucination.dm:165
    public virtual object generate_image()
    {
        image created = image(image_icon, this, image_state, image_layer, dir);
        SET_PLANE_EXPLICIT(created, image_plane, this);
        created.pixel_w = image_pixel_x;
        created.pixel_z = image_pixel_y;
        if (Dm.truthy(image_color))
            created.color = image_color;
        return created;
    }

    /// Shows the image we generated to the passed mob
    // tg: code/modules/hallucination/_hallucination.dm:174
    [SignalHandler]
    public virtual void show_image_to(mob show_to = null)
    {
        if (show_to.client != null)
            Dm.combine(show_to.client.images, shown_image);
    }

    /// Hides the image we generated from the passed mob
    // tg: code/modules/hallucination/_hallucination.dm:180
    public virtual void hide_image_from(mob hide_from = null)
    {
        if (hide_from.client != null)
            Dm.remove(hide_from.client.images, shown_image);
    }

    /// Simple helper for refreshing / showing the image to everyone in our list.
    // tg: code/modules/hallucination/_hallucination.dm:186
    public virtual void regenerate_image()
    {
        foreach (mob seer in Dm.each_cast<mob>(who_sees_us))
            hide_image_from(seer);

        shown_image = generate_image() as image;

        foreach (mob seer in Dm.each_cast<mob>(who_sees_us))
            show_image_to(seer);
    }

    // Whenever we perform icon updates, regenerate our image
    // tg: code/modules/hallucination/_hallucination.dm:197
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon(updates, __extra);
        regenerate_image();
        return dot;
    }

    // If we move for some reason, regenerate our image
    // tg: code/modules/hallucination/_hallucination.dm:203
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = null;
        dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (loc == null)
            return dot;
        regenerate_image();
        return dot;
    }

    // tg: code/modules/hallucination/_hallucination.dm:209
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }

    // tg: code/modules/hallucination/_hallucination.dm:212
    public override async DmTask<object> singularity_act()
    {
        return null;
    }
}

/**
 * A client-side image effect tied to the existence of a hallucination.
 */
// tg: code/modules/hallucination/_hallucination.dm:219
public partial class obj_effect_client_image_holder_hallucination : obj_effect_client_image_holder
{
    // tg: code/modules/hallucination/_hallucination.dm:224
    [DmParams("mapload", "mobs_which_see_us", "parent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_hallucination parent = Dm.arg<datum_hallucination>(__extra, 1);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (parent == null)
        {
            stack_trace(Dm.text("[] was created without a parent hallucination.", type), "code/modules/hallucination/_hallucination.dm", 226);
            return INITIALIZE_HINT_QDEL;
        }

        RegisterSignal(parent, COMSIG_QDELETING, nameof(parent_deleting));
        this.parent = parent;
        return dot;
    }

    // tg: code/modules/hallucination/_hallucination.dm:233
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(parent, COMSIG_QDELETING);
        parent = null;
        return base.Destroy(force);
    }

    /// Signal proc for [COMSIG_QDELETING], if our associated hallucination deletes, we should too
    // tg: code/modules/hallucination/_hallucination.dm:238
    [SignalHandler]
    public virtual void parent_deleting(datum source = null)
    {
        qdel(this);
    }
}
