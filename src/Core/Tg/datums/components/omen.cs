// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/omen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H1 (the broken mirror's bad luck): the whole file (the omen: the airlock that crushes, the vending machine that tips, the lights
// that zap, the spontaneous combustion, the fall over an edge, the evil mirror, the head-smashing slip, the bible's blessing that lifts
// it, the death that lifts it, its vessel; the bible's omen with its red aura). The var blocks are generated (D111).

/**
 * omen.dm: For when you want someone to have a really bad day
 *
 * When you attach an omen component to someone, they start running the risk of all sorts of bad environmental injuries, like nearby vending machines randomly falling on you,
 * or hitting your head really hard when you slip and fall, or you get shocked by the tram rails at an unfortunate moment.
 *
 * Omens are removed once the victim is either maimed by one of the possible injuries, or if they receive a blessing (read: bashing with a bible) from the chaplain.
 */
// tg: code/datums/components/omen.dm:9
public partial class datum_component_omen : datum_component
{
    // tg: code/datums/components/omen.dm:26
    [DmParams("vessel", "incidents_left", "luck_mod", "damage_mod", "bless_fixable", "on_death")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object vessel = Dm.arg(__extra, 0);
        object incidents_left = Dm.arg(__extra, 1) ?? INFINITY;
        object luck_mod = Dm.arg(__extra, 2) ?? 1;
        object damage_mod = Dm.arg(__extra, 3) ?? 1;
        object bless_fixable = Dm.arg(__extra, 4) ?? TRUE;
        var on_death = Dm.arg(__extra, 5) as datum_callback;
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        if (vessel is obj vessel_obj)
        {
            this.vessel = vessel_obj;
            RegisterSignal(vessel_obj, COMSIG_QDELETING, nameof(vessel_qdeleting));
        }

        this.incidents_left = Dm.num(incidents_left);
        this.luck_mod = Dm.num(luck_mod);
        this.damage_mod = Dm.num(damage_mod);
        this.bless_fixable = Dm.num(bless_fixable);
        this.on_death = on_death;

        ADD_TRAIT(parent, TRAIT_CURSED, REF(this));
        await DmTask.CompletedTask;
        return null;
    }

    /**
     * This is a omen eat omen world! The stronger omen survives.
     */
    // tg: code/datums/components/omen.dm:45
    [DmParams("new_comp", "i_am_original", "vessel", "incidents_left", "luck_mod", "damage_mod")]
    public override async DmTask InheritComponent(datum_component new_comp = null, object i_am_original = null, params object[] __extra)
    {
        // COMPONENT_DUPE_UNIQUE_PASSARGS: the new omen's Initialize arguments
        float incidents_left = Dm.num(Dm.arg(__extra, 1) ?? INFINITY);
        float luck_mod = Dm.num(Dm.arg(__extra, 2) ?? 1);
        float damage_mod = Dm.num(Dm.arg(__extra, 3) ?? 1);
        await DmTask.CompletedTask;
        // If we have more incidents left the new one gets deleted.
        if (this.incidents_left > incidents_left)
            return; // make slimes get nurtiton from plasmer
        // Otherwise we set our incidents remaining to the higher, newer value.
        this.incidents_left = incidents_left;
        // The new omen is weaker than our current omen? Let's split the difference.
        if (this.luck_mod > luck_mod)
            this.luck_mod += luck_mod * 0.5f;
        if (this.damage_mod > damage_mod)
            this.damage_mod += damage_mod * 0.5f;
        // This means that if you had a strong temporary omen and it was replaced by a weaker but permanent omen, the latter is made worse.
        // Feature!
    }

    // tg: code/datums/components/omen.dm:59
    public override object Destroy(float? force = null)
    {
        var person = (mob_living)parent;
        REMOVE_TRAIT(person, TRAIT_CURSED, REF(this));
        REMOVE_TRAIT(person, TRAIT_NO_MIRROR_REFLECTION, REF(this));
        to_chat(person, span_nicegreen(I18n.T("You feel a horrible omen lifted off your shoulders!")));

        if (vessel != null)
        {
            vessel.visible_message(span_warning(I18n.T("[] burns up in a sinister flash, taking an evil energy with it...", vessel)));
            UnregisterSignal(vessel, COMSIG_QDELETING);
            Waiting.NoSleep(vessel.burn__proc());
            vessel = null;
        }

        foreach (obj_machinery_light to_untrack in Dm.each<obj_machinery_light>(tracked_lights.Copy()))
            untrack_light(to_untrack);

        return base.Destroy(force);
    }

    // tg: code/datums/components/omen.dm:76
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(check_accident));
        RegisterSignal(parent, COMSIG_ON_CARBON_SLIP, nameof(check_slip));
        RegisterSignal(parent, COMSIG_LIVING_BLESSED, nameof(check_bless));
        RegisterSignal(parent, COMSIG_LIVING_DEATH, nameof(check_death));
        RegisterSignal(parent, COMSIG_MOVABLE_BLOCKING_AIRLOCK, nameof(check_airlock_crush));
        RegisterSignal(parent, COMSIG_MOB_VENDING_PURCHASE, nameof(check_vending));
        RegisterSignal(parent, COMSIG_LIVING_LIFE, nameof(on_life));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/omen.dm:85
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ON_CARBON_SLIP,
            COMSIG_MOVABLE_MOVED,
            COMSIG_LIVING_BLESSED,
            COMSIG_LIVING_DEATH,
            COMSIG_MOVABLE_BLOCKING_AIRLOCK,
            COMSIG_MOB_VENDING_PURCHASE,
            COMSIG_LIVING_LIFE));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/omen.dm:96
    public virtual void consume_omen()
    {
        if (incidents_left == INFINITY)
            return;

        incidents_left--;
        if (incidents_left < 1)
            qdel(this);
    }

    /// Roll an accident happening, factoring in a few things, based on some base change.
    // tg: code/datums/components/omen.dm:105
    public virtual bool roll_for_accident(float base_chance = 4)
    {
        float chance = base_chance * luck_mod;
        foreach (mob viewer in Dm.each<mob>(viewers(parent)))
        {
            if (!Dm.truthy(viewer.client?.is_afk()))
            {
                chance *= 2;
                break;
            }
        }

        return prob(chance);
    }

    /// When we obstruct an airlock, there's a chance it will crush us instead of stopping like it should
    // tg: code/datums/components/omen.dm:115
    [SignalHandler]
    public virtual object check_airlock_crush(mob_living source = null, obj_machinery_door_airlock darth_airlock = null, object forced = null, object force_crush = null)
    {
        if (Dm.truthy(force_crush) || !roll_for_accident(25))
            return NONE;

        consume_omen();
        to_chat(parent, span_warning(I18n.T("As you stand in [], it doesn't stop closing like it should...", darth_airlock)));
        return AIRLOCK_BLOCK_FORCE_CRUSH;
    }

    /// When we vend an item from a vending machine, there's a chance the machine will tip
    // tg: code/datums/components/omen.dm:126
    [SignalHandler]
    public virtual object check_vending(mob_living source = null, obj_machinery_vending darth_vendor = null, obj_item vended_item = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(source.Adjacent(darth_vendor))) || !roll_for_accident(10))
            return NONE;

        consume_omen();
        to_chat(source, span_warning(I18n.T("As you grab [] from the slot, [] wobbles ominously...", vended_item, darth_vendor)));
        Waiting.Detach(darth_vendor.tilt(source)); // INVOKE_ASYNC
        return VENDING_NO_PICKUP;
    }

    /// On life tick, run a few generic checks for accidents, and track nearby lights
    // tg: code/datums/components/omen.dm:138
    [SignalHandler]
    public virtual void on_life(mob_living source = null, object seconds_per_tick = null)
    {
        if (source.stat == DEAD || HAS_TRAIT(source, TRAIT_STASIS))
            return;

        if (roll_for_accident(0.001f))
        {
            spontaneous_combustion();
            return;
        }

        if (HAS_TRAIT(source, TRAIT_SHOCKIMMUNE))
        {
            foreach (obj_machinery_light to_untrack in Dm.each<obj_machinery_light>(tracked_lights.Copy()))
                untrack_light(to_untrack);
            return;
        }

        DmList light_list = Dm.list();
        foreach (obj_machinery_light evil_light in Dm.each<obj_machinery_light>(view(2, source)))
        {
            if (Dm.eq(evil_light.status, LIGHT_OK))
                Dm.append(ref light_list, evil_light);
        }
        foreach (obj_machinery_light to_track in Dm.each<obj_machinery_light>(Dm.sub(light_list, tracked_lights)))
            track_light(to_track);
        foreach (obj_machinery_light to_untrack in Dm.each<obj_machinery_light>(Dm.sub(tracked_lights, light_list)))
            untrack_light(to_untrack);
    }

    /// Start tracking a light, because it's near us and could be a threat
    // tg: code/datums/components/omen.dm:163
    public virtual void track_light(obj_machinery_light evil_light = null)
    {
        Dm.append(ref tracked_lights, evil_light);
        RegisterSignal(evil_light, COMSIG_LIGHT_FIXTURE_BROKEN, nameof(check_break_zap));
        RegisterSignal(evil_light, COMSIG_LIGHT_FIXTURE_TOGGLED, nameof(check_toggle_zap));
        RegisterSignal(evil_light, COMSIG_QDELETING, nameof(untrack_light));
    }

    /// Stop tracking a light, either because it broke or we moved away from it
    // tg: code/datums/components/omen.dm:170
    [SignalHandler]
    public virtual void untrack_light(obj_machinery_light evil_light = null)
    {
        Dm.remove(ref tracked_lights, evil_light);
        UnregisterSignal(evil_light, Dm.list(COMSIG_LIGHT_FIXTURE_BROKEN, COMSIG_LIGHT_FIXTURE_TOGGLED, COMSIG_QDELETING));
    }

    /// When a light we track breaks, there's a chance of zapping us
    // tg: code/datums/components/omen.dm:177
    [SignalHandler]
    public virtual void check_break_zap(obj_machinery_light evil_light = null, object was_ok = null)
    {
        if (Dm.truthy(was_ok) && !HAS_TRAIT(parent, TRAIT_SHOCKIMMUNE) && roll_for_accident(25))
        {
            evil_light.visible_message(span_boldwarning(I18n.T("A bolt of electricity jumps from [] to [] as it breaks!", evil_light, parent)));
            light_zap(evil_light);
            consume_omen();
        }
        // always untrack because it's broken now
        untrack_light(evil_light);
    }

    /// When a light we track is toggled on or off, there's a chance of zapping us
    // tg: code/datums/components/omen.dm:188
    [SignalHandler]
    public virtual void check_toggle_zap(obj_machinery_light evil_light = null, object new_status = null)
    {
        if (HAS_TRAIT(parent, TRAIT_SHOCKIMMUNE) || !roll_for_accident(10))
            return;

        evil_light.visible_message(span_boldwarning(Dm.truthy(new_status)
            ? I18n.T("A bolt of electricity jumps from [] to [] as it turns on!", evil_light, parent)
            : I18n.T("A bolt of electricity jumps from [] to [] as it turns off!", evil_light, parent)));
        light_zap(evil_light);
        consume_omen();
        // we're about to break it, so untrack to avoid a double zap
        untrack_light(evil_light);
        // and then actually break the thing
        Waiting.NoSleep(evil_light.break_light_tube());
    }

    /// Zap the target with electricity from a light fixture
    // tg: code/datums/components/omen.dm:203
    private void light_zap(obj_machinery_light evil_light = null)
    {
        var target = (mob_living)parent;
        evil_light.Beam(target, icon_state: Dm.text("lightning[]", rand(1, 12)), time: 0.5f * SECONDS);
        Waiting.NoSleep(target.electrocute_act(35 * damage_mod, evil_light, flags: SHOCK_NOGLOVES));
        Waiting.Detach(target.emote("scream")); // INVOKE_ASYNC
        consume_omen();
    }

    /// Randomly burst into flames
    // tg: code/datums/components/omen.dm:213
    public virtual object spontaneous_combustion()
    {
        var target = (mob_living)parent;
        Waiting.NoSleep(target.adjust_fire_stacks(20));
        if (!Dm.truthy(target.ignite_mob(silent: TRUE)))
            return FALSE;

        target.visible_message(
            span_danger(I18n.T("[] suddenly bursts into flames!", target)),
            span_userdanger(I18n.T("You suddenly burst into flames!")));
        Waiting.Detach(target.emote("scream")); // INVOKE_ASYNC
        consume_omen();
        return TRUE;
    }

    /// Every time we move we need to check a few things for potential incidents
    // tg: code/datums/components/omen.dm:228
    [SignalHandler]
    public virtual void check_accident(mob_living source = null)
    {
        if (mirror_interaction())
            return;

        if (fall_down())
            return;
    }

    /// Attempts to throw us down a nearby open space
    // tg: code/datums/components/omen.dm:238
    public virtual bool fall_down()
    {
        var our_guy = (mob_living)parent;
        var mob_turf = get_turf(our_guy) as turf_open;
        if (isgroundlessturf(mob_turf) || mob_turf is turf_open_floor_glass_reinforced_tram) // snowflake check is to increase likelihood of being hit with the tram
            return false;

        foreach (turf adjacent_turf in Dm.each<turf>(get_adjacent_open_turfs(mob_turf)))
        {
            if (!Dm.truthy(our_guy.can_z_move(DOWN, adjacent_turf, z_move_flags: ZMOVE_FALL_FLAGS)))
                continue;
            if (!roll_for_accident(4))
                return false;

            var rail = Dm.locate<obj_structure_railing>(mob_turf);
            to_chat(our_guy, span_warning(rail != null
                ? I18n.T("As you step on [], you lose footing and fall over the railing and off the edge!", mob_turf)
                : I18n.T("As you step on [], you lose footing and fall off the edge!", mob_turf)));
            Waiting.NoSleep(our_guy.throw_at(adjacent_turf, 1, 10, force: MOVE_FORCE_EXTREMELY_STRONG));
            consume_omen();
            return true;
        }

        return false;
    }

    /// Gaze into a mirror and see if something bad happens
    // tg: code/datums/components/omen.dm:259
    public virtual bool mirror_interaction()
    {
        var our_guy = (mob_living)parent;
        var evil_mirror = Dm.locate<obj_structure_mirror>(get_turf(our_guy));
        if (evil_mirror == null || !roll_for_accident(10))
        {
            REMOVE_TRAIT(our_guy, TRAIT_NO_MIRROR_REFLECTION, REF(this));
            return false;
        }

        to_chat(our_guy, span_warning(I18n.T("You pass by the mirror and glance at it...")));
        if (Dm.truthy(evil_mirror.broken))
        {
            to_chat(our_guy, span_notice(I18n.T("...You feel lucky, somehow.")));
            return true;
        }

        switch ((int)rand(1, 5))
        {
            case 1:
                to_chat(our_guy, span_warning(I18n.T("...The mirror explodes into a million pieces! Wait, does that mean you're even more unlucky?")));
                Waiting.NoSleep(evil_mirror.take_damage(evil_mirror.max_integrity, BRUTE, MELEE, FALSE));
                if (roll_for_accident(20))
                {
                    luck_mod += 0.25f;
                    damage_mod += 0.25f;
                }
                break;

            case 2:
            case 3:
                if (HAS_TRAIT(our_guy, TRAIT_NO_MIRROR_REFLECTION)) // not so living i suppose
                {
                    to_chat(our_guy, span_green(I18n.T("...Oh god, you can't see your reflection - wait, that's normal.")));
                    return true;
                }
                to_chat(our_guy, span_big(span_hypnophrase(I18n.T("...Oh god, you can't see your reflection!!"))));
                Waiting.Detach(our_guy.emote("scream")); // INVOKE_ASYNC
                ADD_TRAIT(our_guy, TRAIT_NO_MIRROR_REFLECTION, REF(this));
                break;

            case 4:
            case 5:
                if (HAS_TRAIT(our_guy, TRAIT_NO_MIRROR_REFLECTION))
                {
                    to_chat(our_guy, span_warning(I18n.T("...but you don't see anything of notice.")));
                    return true;
                }
                to_chat(our_guy, span_userdanger(I18n.T("You see your reflection, but it is grinning malevolently and staring directly at you!")));
                Waiting.Detach(our_guy.emote("scream")); // INVOKE_ASYNC
                break;
        }

        Waiting.NoSleep(our_guy.set_jitter_if_lower(25 * SECONDS));
        if (roll_for_accident(2))
        {
            to_chat(our_guy, span_warning(I18n.T("You are completely shocked by this turn of events!")));
            to_chat(our_guy, span_userdanger(I18n.T("You clutch at your heart!")));
            if (our_guy is mob_living_carbon carbon_guy)
                carbon_guy.set_heartattack(status: TRUE);
        }

        consume_omen();
        return true;
    }

    /// If we get knocked down, see if we have a really bad slip and bash our head hard
    // tg: code/datums/components/omen.dm:306
    [SignalHandler]
    public virtual void check_slip(mob_living our_guy = null, object amount = null)
    {
        if ((our_guy as mob_living_carbon)?.get_bodypart(BODY_ZONE_HEAD) == null || !roll_for_accident(15)) // Bonk!
            return;

        playsound(our_guy, Dm.resource("sound/effects/tableheadsmash.ogg"), 90, TRUE);
        our_guy.visible_message(
            span_danger(I18n.T("[] hits [] head really badly falling down!", our_guy, our_guy.p_their())),
            span_userdanger(I18n.T("You hit your head really badly falling down!")));
        Waiting.NoSleep(our_guy.apply_damage(75 * damage_mod, BRUTE, BODY_ZONE_HEAD, attacking_item: "slipping"));
        Waiting.NoSleep(our_guy.apply_damage(100 * damage_mod, BRAIN));
        consume_omen();
    }

    /// Hijack the mood system to see if we get the blessing mood event to cancel the omen
    // tg: code/datums/components/omen.dm:322
    [SignalHandler]
    public virtual void check_bless(mob_living our_guy = null, mob_living priest = null, obj_item_book_bible bible = null, object bless_result = null)
    {
        if (incidents_left == INFINITY || !Dm.eq(bless_result, BLESSING_SUCCESS) || !Dm.truthy(bless_fixable))
            return;

        playsound(our_guy, Dm.resource("sound/effects/pray_chaplain.ogg"), 40, TRUE);
        to_chat(our_guy, span_green(I18n.T("You feel fantastic!")));
        qdel(this);
    }

    /// Severe deaths. Normally lifts the curse.
    // tg: code/datums/components/omen.dm:334
    [SignalHandler]
    public virtual void check_death(mob_living our_guy = null)
    {
        if (on_death != null)
            Waiting.NoSleep(on_death.Invoke(this));
        if (incidents_left == INFINITY)
            return;

        qdel(this);
    }

    /// Creates a localized explosion that shakes the camera
    // tg: code/datums/components/omen.dm:343
    public virtual async DmTask death_explode(mob_living our_guy = null)
    {
        await explosion(our_guy, explosion_cause: our_guy); // tg passes the omen as the cause; the port's explosion_cause is an atom (the log names its victim)

        foreach (mob witness in Dm.each<mob>(view(2, our_guy)))
            shake_camera(witness, 1 * SECONDS, 2);
    }

    /// Vessel got deleted, set it to null
    // tg: code/datums/components/omen.dm:350
    [SignalHandler]
    public virtual void vessel_qdeleting(atom source = null)
    {
        UnregisterSignal(vessel, COMSIG_QDELETING);
        vessel = null;
    }
}

/**
 * The bible omen.
 * While it lasts, parent gets a cursed aura filter.
 */
// tg: code/datums/components/omen.dm:360
public partial class datum_component_omen_bible : datum_component_omen
{
    // tg: code/datums/components/omen.dm:363
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        var living_parent = (mob_living)parent;
        living_parent.add_filter("omen", 2, Dm.list(Dm.kv("type", "drop_shadow"), Dm.kv("color", COLOR_DARK_RED), Dm.kv("alpha", 0), Dm.kv("size", 2)));
        object filter = living_parent.get_filter("omen");
        Dm.animate(filter, Dm.named("alpha", 255), Dm.named("time", 2 * SECONDS), Dm.named("loop", -1));
        Dm.animate(Dm.named("alpha", 0), Dm.named("time", 2 * SECONDS));
    }

    // tg: code/datums/components/omen.dm:371
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        var living_parent = (mob_living)parent;
        living_parent.remove_filter("omen");
    }
}
