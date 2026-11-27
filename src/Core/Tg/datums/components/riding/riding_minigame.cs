// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/riding/riding_minigame.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The game's arrows and hearts are tg's images in the rider's client.images, at the rider's own screen position (ABOVE_HUD_PLANE).
// The first-person client draws no private image on its viewer's own body, so the rider does not see them yet: how the game
// shows in first person is still open (a row in not-translated.md).

//a simple minigame players must win to mount and tame a mob
// tg: code/datums/components/riding/riding_minigame.dm:9
public partial class datum_riding_minigame : datum
{
    // tg: code/datums/components/riding/riding_minigame.dm:1-6 (file-local defines, #undef at :244-249)
    private const float STARTING_ARROW_POSITION = -50;
    private const float ENDING_ARROW_POSITION = 20;
    private const float VERTICAL_ARROW_HEIGHT = 13;
    private const float HORIZONTAL_ARROW_HEIGHT = 9;
    private const float FADE_AWAY_TIME = 0.1f * SECONDS;
    private const float PING_GRACE = 0.25f * SECONDS;

    // tg: code/datums/components/riding/riding_minigame.dm:43
    [DmParams("ridden", "rider")]
    public override async DmTask New(params object[] __extra)
    {
        mob_living ridden = Dm.arg<mob_living>(__extra, 0);
        mob_living rider = Dm.arg<mob_living>(__extra, 1);
        await base.New(__extra);
        host = WEAKREF(ridden) as datum_weakref;
        mounter = WEAKREF(rider) as datum_weakref;
        set_difficulty(ridden, rider);
        RegisterSignal(rider, COMSIG_MOB_UNBUCKLED, nameof(lose_minigame));
        RegisterSignal(ridden, COMSIG_MOVABLE_ATTEMPTED_MOVE, nameof(on_ridden_moved));
        minigame_holder = image(icon: Dm.resource("icons/effects/effects.dmi"), loc: rider, icon_state: "nothing", layer: 0);
        minigame_holder.pixel_w = 32;
        heart_counter = image(icon: Dm.resource("icons/effects/effects.dmi"), loc: rider, icon_state: "nothing", layer: 0);
        heart_counter.pixel_z = -32;
        SET_PLANE_EXPLICIT(minigame_holder, ABOVE_HUD_PLANE, rider);
        SET_PLANE_EXPLICIT(heart_counter, ABOVE_HUD_PLANE, rider);
        generate_heart_counter();
        generate_visuals();
        DmList client_images = rider.client.images;
        Dm.combine(ref client_images, Dm.list(minigame_holder, heart_counter));
        START_PROCESSING(SSprocessing, this);
    }

    // tg: code/datums/components/riding/riding_minigame.dm:61
    public virtual void set_difficulty(mob_living ridden = null, mob_living rider = null)
    {
        if (HAS_TRAIT(rider, TRAIT_BEAST_EMPATHY) || HAS_TRAIT(ridden, TRAIT_MOB_EASY_TO_MOUNT))
            linger_time *= easy_difficulty_multiplier;
        if (HAS_TRAIT(ridden, TRAIT_MOB_DIFFICULT_TO_MOUNT))
            linger_time *= hard_difficulty_multiplier;
    }

    // tg: code/datums/components/riding/riding_minigame.dm:67
    public virtual void generate_visuals()
    {
        DmList void_arrow_order = TgState.Current.datum_riding_minigame__generate_visuals__void_arrow_order ??= Dm.list("north", "west", "south", "east");
        float x_offset = 0;
        foreach (object direction in Dm.each(void_arrow_order))
        {
            obj_effect_overlay_vis_ride_minigame new_arrow = Dm.@new<obj_effect_overlay_vis_ride_minigame>();
            new_arrow.icon = Dm.resource("icons/effects/riding_minigame.dmi");
            new_arrow.icon_state = "blank_arrow";
            Waiting.NoSleep(new_arrow.setDir(text2dir(direction)));
            new_arrow.pixel_x = x_offset;
            new_arrow.pixel_y = ENDING_ARROW_POSITION;
            new_arrow.layer = ABOVE_ALL_MOB_LAYER;
            Dm.append(minigame_holder.vis_contents, new_arrow);
            cached_arrows[direction] = Dm.list(Dm.kv("visual_object", new_arrow), Dm.kv("is_active", null));
            x_offset += 16;
        }
    }

    // tg: code/datums/components/riding/riding_minigame.dm:87
    public virtual void generate_heart_counter()
    {
        float x_offset = -32;
        for (float i = 1; i <= required_successes; i++)
        {
            obj_effect_overlay_vis_ride_minigame heart = Dm.@new<obj_effect_overlay_vis_ride_minigame>();
            heart.icon = Dm.resource("icons/effects/effects.dmi");
            heart.icon_state = "empty_heart";
            heart.pixel_x = x_offset;
            x_offset += 8;
            Dm.append(ref hearts_list, heart);
            Dm.append(heart_counter.vis_contents, heart);
        }
    }

    // tg: code/datums/components/riding/riding_minigame.dm:98
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (current_attempts >= maximum_attempts)
        {
            lose_minigame();
            return null;
        }
        if (prob(30)) //we shake and move uncontrollably!
        {
            mob_living living_host = host.resolve() as mob_living;
            living_host.Shake(pixelshiftx: 1, pixelshifty: 0, duration: 0.75f * SECONDS);
            living_host.spin(spintime: 0.75f * SECONDS, speed: 1);
        }

        generate_arrow();
        return null;
    }

    // tg: code/datums/components/riding/riding_minigame.dm:109
    public virtual void generate_arrow()
    {
        if (current_attempts >= maximum_attempts)
            return;
        DmList possible_arrows = TgState.Current.datum_riding_minigame__generate_arrow__possible_arrows ??= Dm.list(
            Dm.kv("north", 0),
            Dm.kv("west", 16),
            Dm.kv("south", 32),
            Dm.kv("east", 48));

        current_attempts++;
        object picked_arrow = pick(possible_arrows);
        obj_effect_overlay_vis_ride_minigame new_arrow = Dm.@new<obj_effect_overlay_vis_ride_minigame>();
        new_arrow.icon = Dm.resource("icons/effects/riding_minigame.dmi");
        new_arrow.icon_state = Dm.text("[]_arrow", picked_arrow);
        new_arrow.alpha = 0;
        new_arrow.layer = ABOVE_ALL_MOB_LAYER + 0.1f;
        new_arrow.pixel_x = Dm.num(possible_arrows[picked_arrow]);
        new_arrow.pixel_y = STARTING_ARROW_POSITION;
        Dm.append(minigame_holder.vis_contents, new_arrow);

        animate(new_arrow, Dm.named("alpha", 255), Dm.named("time", 0.15f * SECONDS));
        animate(new_arrow, Dm.named("pixel_y", ENDING_ARROW_POSITION), Dm.named("time", ((ENDING_ARROW_POSITION - STARTING_ARROW_POSITION) / arrow_speed) * SECONDS));

        _addtimer(Dm.@new<datum_callback>(this, nameof(add_active_arrow), new_arrow, picked_arrow), ((ENDING_ARROW_POSITION - (STARTING_ARROW_POSITION + get_arrow_height(picked_arrow))) / arrow_speed) * SECONDS, file: "code/datums/components/riding/riding_minigame.dm", line: 137);
    }

    // tg: code/datums/components/riding/riding_minigame.dm:135
    public virtual float get_arrow_height(object text_direction = null)
    {
        float direction = Dm.num(text2dir(text_direction));
        return Dm.truthy(NSCOMPONENT(direction)) ? VERTICAL_ARROW_HEIGHT : HORIZONTAL_ARROW_HEIGHT;
    }

    // tg: code/datums/components/riding/riding_minigame.dm:139
    public virtual void add_active_arrow(atom arrow = null, object direction = null)
    {
        if (QDELETED(arrow))
            return;

        if (!(failure_cooldown <= world.time)) // !COOLDOWN_FINISHED(src, failure_cooldown)
        {
            remove_active_arrow(arrow, direction);
            return;
        }

        mob_living rider = mounter?.resolve() as mob_living;
        if (rider == null)
            return;
        client rider_client = rider.client;
        if (rider_client == null)
            return;
        (cached_arrows[direction] as DmList)["is_active"] = arrow;
        RegisterSignal(arrow, COMSIG_QDELETING, nameof(on_arrow_delete));
        float distance_to_travel = get_arrow_height(direction) * 2;
        float time_of_grace = (distance_to_travel / arrow_speed) * SECONDS + linger_time + PING_GRACE - FADE_AWAY_TIME;
        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_active_arrow), arrow, direction), time_of_grace, file: "code/datums/components/riding/riding_minigame.dm", line: 162);
    }

    // tg: code/datums/components/riding/riding_minigame.dm:159
    public virtual void remove_active_arrow(atom arrow = null, object direction = null)
    {
        if (QDELETED(arrow))
            return;
        animate(arrow, Dm.named("alpha", 0), Dm.named("time", FADE_AWAY_TIME));
        // QDEL_IN(arrow, FADE_AWAY_TIME), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), arrow), FADE_AWAY_TIME, file: "code/datums/components/riding/riding_minigame.dm", line: 168);
    }

    // tg: code/datums/components/riding/riding_minigame.dm:165
    [SignalHandler]
    public virtual void on_arrow_delete(datum source = null)
    {
        foreach (object arrow in Dm.each(cached_arrows))
        {
            DmList arrow_details = cached_arrows[arrow] as DmList;
            if (!Dm.eq(arrow_details["is_active"], source))
                continue;
            arrow_details["is_active"] = null;
        }
    }

    // tg: code/datums/components/riding/riding_minigame.dm:173
    [SignalHandler]
    public virtual object on_ridden_moved(atom_movable source = null, atom new_loc = null, object direction = null)
    {
        object dot = NONE;
        if (!Dm.eq(new_loc.z, source.z) || !(failure_cooldown <= world.time))
            return dot;
        DmList arrow_data = cached_arrows[dir2text(direction)] as DmList;
        atom existing_arrow = arrow_data?["is_active"] as atom;

        if (!QDELETED(existing_arrow))
        {
            qdel(existing_arrow);
            atom arrow_object = arrow_data["visual_object"] as atom;
            flick("blank_arrow_win", arrow_object);
            increment_counter();
            return dot;
        }

        foreach (object arrow_direction in Dm.each(cached_arrows))
        {
            obj my_arrow = (cached_arrows[arrow_direction] as DmList)["visual_object"] as obj;
            if (my_arrow == null)
                continue;
            atom clickable_arrow = (cached_arrows[arrow_direction] as DmList)["is_active"] as atom;
            if (!QDELETED(clickable_arrow))
                qdel(clickable_arrow);
            my_arrow.layer = ABOVE_ALL_MOB_LAYER + 0.2f;
            flick("blank_arrow_lose", my_arrow);
            my_arrow.Shake(duration: 2 * SECONDS);
            // VARSET_CALLBACK(my_arrow, layer, ABOVE_ALL_MOB_LAYER): the timer calls layer__varset, ___callbackvarset's datum branch
            _addtimer(Dm.@new<datum_callback>(this, nameof(layer__varset), my_arrow, ABOVE_ALL_MOB_LAYER), 2 * SECONDS, file: "code/datums/components/riding/riding_minigame.dm", line: 203);
        }

        increment_failure();
        return dot;
    }

    /// VARSET_CALLBACK(my_arrow, layer, ABOVE_ALL_MOB_LAYER)'s target: ___callbackvarset's datum branch (varset_callback.dm:8-23).
    public virtual void layer__varset(atom_movable arrow, object var_value)
    {
        if (!QDELETED(arrow))
            arrow.layer = Dm.num(var_value);
    }

    // tg: code/datums/components/riding/riding_minigame.dm:202
    public virtual void increment_counter()
    {
        current_succeeded++;
        obj new_heart = hearts_list[current_succeeded] as obj;
        new_heart.icon_state = "full_heart";
        new_heart.transform = (new_heart.transform as matrix).Scale(2, 2);
        animate(new_heart, Dm.named("transform", Dm.matrix()), Dm.named("time", 0.3f * SECONDS));
        if (current_succeeded >= required_successes)
            win_minigame();
    }

    // tg: code/datums/components/riding/riding_minigame.dm:211
    public virtual void increment_failure()
    {
        current_failures++;
        failure_cooldown = world.time + 2 * SECONDS; // COOLDOWN_START(src, failure_cooldown, 2 SECONDS)
        if (current_failures > (maximum_attempts - required_successes))
            lose_minigame();
    }

    // tg: code/datums/components/riding/riding_minigame.dm:217
    [SignalHandler]
    public virtual void lose_minigame()
    {
        mob_living living_host = host?.resolve() as mob_living;
        mob_living living_rider = mounter?.resolve() as mob_living;
        if (living_host == null || living_rider == null)
        {
            qdel(this);
            return;
        }
        if (Dm.truthy(LAZYFIND(living_host.buckled_mobs, living_rider)))
        {
            UnregisterSignal(living_rider, COMSIG_MOB_UNBUCKLED); //we're about to knock them down!
            living_host.spin(spintime: 2 * SECONDS, speed: 1);
            Waiting.NoSleep(living_rider.Knockdown(4 * SECONDS));
            Waiting.NoSleep(living_host.unbuckle_mob(living_rider));
            living_host.balloon_alert(living_rider, "knocks you down!");
        }
        qdel(this);
    }

    // tg: code/datums/components/riding/riding_minigame.dm:232
    public virtual void win_minigame()
    {
        mob_living living_host = host?.resolve() as mob_living;
        mob_living living_rider = mounter?.resolve() as mob_living;
        if (living_host == null || living_rider == null)
        {
            qdel(this);
            return;
        }
        Waiting.NoSleep(living_host.befriend(living_rider));
        living_host.balloon_alert(living_rider, "calms down...");
        qdel(this);
    }

    // tg: code/datums/components/riding/riding_minigame.dm:242
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSprocessing, this);

        mob_living living_mounter = mounter?.resolve() as mob_living;
        if (living_mounter?.client != null)
        {
            DmList client_images = living_mounter.client.images;
            Dm.remove(ref client_images, minigame_holder);
            Dm.remove(ref client_images, heart_counter);
        }

        mounter = null;
        host = null;
        hearts_list = null;
        cached_arrows = null;
        minigame_holder = null;
        heart_counter = null;
        return base.Destroy(force);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/riding/riding_minigame.dm:70 (var/static/list/void_arrow_order)
    internal DmList datum_riding_minigame__generate_visuals__void_arrow_order;
    // tg: code/datums/components/riding/riding_minigame.dm:115 (var/static/list/possible_arrows)
    internal DmList datum_riding_minigame__generate_arrow__possible_arrows;
}
