// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fishing_rod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fishing rods with their line, hook and bait slots and their window, the cast (a hook projectile, or
// straight onto an adjacent spot), the fishing line's beam, reeling in a hooked thing, and the telescopic, master, tech,
// bone, rescue and material rods. The context tips are deferred with the other screentips; the slots' pictures in the
// examine line are tg's chat icons (icon2html), which the text chat leaves out. The var blocks are generated (D111).
//
// GAP(types): tg's `var/obj/item/bait`, `var/obj/item/fishing_line/line = /obj/item/fishing_line` and
// `var/obj/item/fishing_hook/hook = /obj/item/fishing_hook` (:27-29) are generated as Types (rules 3.2 V3: two of them default
// to a typepath, and tg assigns typepaths to `bait` in the mob fisher's rod); tg keeps the typepath there until Initialize and
// the part itself after it, as the machines' circuit does. The installed parts are bait_value, line_value and hook_value;
// bait, line and hook keep the types the rod starts with.

// tg: code/modules/fishing/fishing_rod.dm:1
[DmPath("/obj/item/fishing_rod")]
public partial class obj_item_fishing_rod : obj_item
{
    /// GAP(types): tg's `bait` (:27) once a bait is installed.
    public obj_item bait_value;
    /// GAP(types): tg's `line` (:28) once a line is installed.
    public obj_item_fishing_line line_value;
    /// GAP(types): tg's `hook` (:29) once a hook is installed.
    public obj_item_fishing_hook hook_value;

    // tg: code/modules/fishing/fishing_rod.dm:81
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        register_item_context();

        if (bait != null)
            set_slot(Dm.@new(bait, this) as obj_item, ROD_SLOT_BAIT);
        if (hook != null)
            set_slot(Dm.@new(hook, this) as obj_item, ROD_SLOT_HOOK);
        if (line != null)
            set_slot(Dm.@new(line, this) as obj_item, ROD_SLOT_LINE);

        update_appearance();
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane),
            Dm.named("damage_multiplier", 2.25f),
            Dm.named("should_bane_callback", Dm.@new<datum_callback>(this, nameof(should_bane_fish_infusions))),
            Dm.named("on_bane_callback", Dm.@new<datum_callback>(this, nameof(on_bane_fish_infusions))),
            Dm.named("label_text", "fishpeople")));
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:119
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        // tg draws each part's picture before its name (icon2html, chat icons); the text chat names the parts only.
        DmList equipped_stuff = Dm.list();
        if (line_value != null)
            Dm.append(ref equipped_stuff, Dm.text("<b>[]</b>", line_value.name));
        if (hook_value != null)
            Dm.append(ref equipped_stuff, Dm.text("<b>[]</b>", hook_value.name));
        if (bait_value != null)
            Dm.append(ref equipped_stuff, Dm.text("<b>[]</b>", bait_value));
        if (Dm.truthy(length(equipped_stuff)))
            Dm.append(ref dot, span_notice(I18n.T("It has \\a [] equipped.", english_list(equipped_stuff))));
        if (bait_value == null)
            Dm.append(ref dot, span_warning(I18n.T("It doesn't have a bait attached to it. Fishing will be more tedious!")));
        if (HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISH))
        {
            Dm.append(ref dot, ""); //add a new line
            Dm.append(ref dot, span_notice(I18n.T("Thanks to your fishing skills, you can examine it again for more in-depth information.")));
            return dot;
        }
        // IS_HERETIC(user) (__DEFINES/antagonists.dm:293), expanded inline (rules 11.1)
        bool user_is_heretic = Dm.truthy(user.mind?.has_antag_datum(typeof(datum_antagonist_heretic))) || HAS_TRAIT(user, TRAIT_ACT_AS_HERETIC);
        if (HAS_TRAIT(this, TRAIT_ROD_MANSUS_INFUSED))
        {
            if (user_is_heretic)
                Dm.append(ref dot, span_purple(I18n.T("This rod has been <b>infused</b> by a heretic, improving its ability to catch glimpses of the Mansus. And fish.")));
            else
                Dm.append(ref dot, span_purple(I18n.T("It's glowing an eerie purple...")));
        }
        else if (user_is_heretic)
            Dm.append(ref dot, span_purple(I18n.T("As a Heretic, you can infuse this fishing rod with your <b>Mansus Grasp</b> by activating the spell while wielding it, to enhance its fishing power.")));
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:144
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (!HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISH))
            return dot;

        DmList block = Dm.list();
        bool get_percent = HAS_MIND_TRAIT(user, TRAIT_EXAMINE_DEEPER_FISH);
        Dm.append(ref block, span_info(I18n.T("You think you can cast it up to [] tiles away.", get_cast_range())));
        Dm.append(ref block, get_stat_info(get_percent, difficulty_modifier * 0.01f, I18n.T("Fishing will be"), I18n.T("easier"), I18n.T("harder"), I18n.T("with this fishing rod")));
        Dm.append(ref block, get_stat_info(get_percent, experience_multiplier - 1, I18n.T("You will gain experience"), I18n.T("faster"), I18n.T("slower")));
        Dm.append(ref block, get_stat_info(get_percent, completion_speed_mult - 1, I18n.T("The minigame completion speed is"), I18n.T("faster"), I18n.T("slower")));
        Dm.append(ref block, get_stat_info(get_percent, bait_speed_mult - 1, I18n.T("Reeling is"), I18n.T("faster"), I18n.T("slower")));
        Dm.append(ref block, get_stat_info(get_percent, deceleration_mult - 1, I18n.T("Deceleration is"), I18n.T("faster"), I18n.T("slower")));
        Dm.append(ref block, get_stat_info(get_percent, bounciness_mult - 1, I18n.T("This fishing rod is "), I18n.T("bouncier"), I18n.T("less bouncy"), I18n.T("than a normal one"), less_is_better: true));
        Dm.append(ref block, get_stat_info(get_percent, gravity_mult - 1, I18n.T("The lure will sink"), I18n.T("faster"), I18n.T("slower"), span_info: true));

        list_clear_nulls(block);
        Dm.append(ref dot, boxed_message(jointext(block, "\n")));

        block = Dm.list();
        if (HAS_TRAIT(this, TRAIT_ROD_IGNORE_ENVIRONMENT))
            Dm.append(ref block, span_info(I18n.T("Environment and light shouldn't be an issue with this rod.")));
        if (HAS_TRAIT_NOT_FROM(this, TRAIT_ROD_REMOVE_FISHING_DUD, INNATE_TRAIT)) // Duds are innately removed by baits, we all know that.
            Dm.append(ref block, span_info(I18n.T("You won't catch duds with this rod.")));
        if (HAS_TRAIT(this, TRAIT_ROD_LAVA_USABLE))
            Dm.append(ref block, span_info(I18n.T("This fishing rod can be used to fish on lava.")));
        if (Dm.truthy(Dm.length(block)))
            Dm.append(ref dot, boxed_message(jointext(block, "\n")));
        return dot;
    }

    ///Used in examine_more to reduce all the copypasta when getting more information about the various stats of the fishing rod.
    // tg: code/modules/fishing/fishing_rod.dm:174
    public virtual object get_stat_info(bool get_percent, float value, string prefix, string easier, string harder, string suffix = null, bool span_info = false, bool less_is_better = false)
    {
        suffix ??= I18n.T("with this fishing rod");
        if (value == 0)
            return null;
        string percent = get_percent ? Dm.text("[]% ", abs(value * 100)) : "";
        string harder_easier = value > 0 ? easier : harder;
        string dot = Dm.text("[] [][] [].", prefix, percent, harder_easier, suffix);
        if (span_info)
            return GlobalProcs.span_info(dot);
        if (less_is_better ? value < 0 : value > 0)
            return span_nicegreen(dot);
        return span_danger(dot);
    }

    // tg: code/modules/fishing/fishing_rod.dm:186
    public override async DmTask apply_single_mat_effect(datum_material custom_material = null, object amount = null, object multiplier = null)
    {
        await base.apply_single_mat_effect(custom_material, amount, multiplier);
        float mult = Dm.num(multiplier);
        cast_range += round(2 - Dm.num(custom_material.get_property(MATERIAL_DENSITY)) / 2) * mult;
        bait_speed_mult *= GET_MATERIAL_MODIFIER(1 + (Dm.num(custom_material.get_property(MATERIAL_REFLECTIVITY)) - 4) * 0.1f, mult);
        deceleration_mult *= GET_MATERIAL_MODIFIER(1 + (Dm.num(custom_material.get_property(MATERIAL_HARDNESS)) - 4) * 0.1f, mult);
        bounciness_mult *= GET_MATERIAL_MODIFIER(1 + (Dm.num(custom_material.get_property(MATERIAL_FLEXIBILITY)) - 4) * 0.1f, mult);
        gravity_mult *= GET_MATERIAL_MODIFIER(1 + (Dm.num(custom_material.get_property(MATERIAL_DENSITY)) - 4) * 0.1f, mult);
    }

    // tg: code/modules/fishing/fishing_rod.dm:194
    public override async DmTask remove_single_mat_effect(datum_material custom_material = null, object amount = null, object multiplier = null)
    {
        await base.remove_single_mat_effect(custom_material, amount, multiplier);
        float mult = Dm.num(multiplier);
        cast_range -= round(2 - Dm.num(custom_material.get_property(MATERIAL_DENSITY)) / 2) * mult;
        bait_speed_mult /= GET_MATERIAL_MODIFIER(1 + (Dm.num(custom_material.get_property(MATERIAL_REFLECTIVITY)) - 4) * 0.1f, mult);
        deceleration_mult /= GET_MATERIAL_MODIFIER(1 + (Dm.num(custom_material.get_property(MATERIAL_HARDNESS)) - 4) * 0.1f, mult);
        bounciness_mult /= GET_MATERIAL_MODIFIER(1 + (Dm.num(custom_material.get_property(MATERIAL_FLEXIBILITY)) - 4) * 0.1f, mult);
        gravity_mult /= GET_MATERIAL_MODIFIER(1 + (Dm.num(custom_material.get_property(MATERIAL_DENSITY)) - 4) * 0.1f, mult);
    }

    /**
     * Is there a reason why this fishing rod couldn't fish in target_fish_source?
     * If so, return the denial reason as a string, otherwise return `null`.
     */
    // tg: code/modules/fishing/fishing_rod.dm:209
    public virtual object reason_we_cant_fish(datum_fish_source target_fish_source = null)
    {
        return hook_value?.reason_we_cant_fish(target_fish_source);
    }

    ///Called at the end of on_challenge_completed() once the reward has been spawned
    // tg: code/modules/fishing/fishing_rod.dm:213
    public virtual async DmTask on_reward_caught(atom_movable reward = null, mob user = null)
    {
        if (reward == null)
            return;
        // catching things that aren't fish or alive mobs doesn't consume baits.
        if (bait_value == null || HAS_TRAIT(bait_value, TRAIT_BAIT_UNCONSUMABLE))
            return;
        if (isliving(reward))
        {
            mob_living caught_mob = (mob_living)reward;
            if (caught_mob.stat == DEAD)
                return;
        }
        else
        {
            if (!isfish(reward))
                return;
            obj_item_fish fish = (obj_item_fish)reward;
            if (HAS_TRAIT(bait_value, TRAIT_POISONOUS_BAIT) && !HAS_TRAIT(fish, TRAIT_FISH_TOXIN_IMMUNE))
            {
                bool kill_fish = true;
                foreach (object bait_identifer in Dm.each(fish.favorite_bait))
                {
                    if (Dm.truthy(is_matching_bait(bait_value, bait_identifer)))
                    {
                        kill_fish = false;
                        break;
                    }
                }
                if (kill_fish)
                    await fish.set_status(FISH_DEAD, silent: TRUE);
            }
        }

        qdel(bait_value);
        await update_icon();
    }

    // tg: code/modules/fishing/fishing_rod.dm:238
    public virtual object should_bane_fish_infusions(mob_living target = null)
    {
        return force > 0 && HAS_TRAIT(target, TRAIT_WATER_ADAPTATION);
    }

    // tg: code/modules/fishing/fishing_rod.dm:241
    public virtual async DmTask on_bane_fish_infusions(mob_living target = null, mob_living attacker = null)
    {
        await target.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 4 * SECONDS);
        await target.adjust_confusion_up_to(1.5f * SECONDS, 3 * SECONDS);
        await target.adjust_wet_stacks(-4);
    }

    // tg: code/modules/fishing/fishing_rod.dm:246
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (currently_hooked != null)
            await reel(user);
        return null;
    }

    // tg: code/modules/fishing/fishing_rod.dm:250
    public virtual async DmTask reel(mob user = null)
    {
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, currently_hooked)))
            return;

        playsound(this, SFX_REEL, 50, vary: FALSE);
        float time = (0.8f - round(Dm.num(user.mind?.get_skill_level(typeof(datum_skill_fishing))) * 0.04f, 0.1f)) * SECONDS * bait_speed_mult;
        if (!Dm.truthy(await do_after(user, time, currently_hooked, timed_action_flags: Dm.bitor(IGNORE_USER_LOC_CHANGE, IGNORE_TARGET_LOC_CHANGE), extra_checks: Dm.@new<datum_callback>(this, nameof(fishing_line_check)))))
            return;

        if (Dm.truthy(currently_hooked.anchored) || currently_hooked.move_resist >= MOVE_FORCE_STRONG)
        {
            balloon_alert(user, I18n.T("[] won't budge!", currently_hooked.p_they()));
            return;
        }

        //About thirty minutes of non-stop reeling to get from zero to master... not worth it but hey, you do what you do.
        user.mind?.adjust_experience(typeof(datum_skill_fishing), time * 0.13f * experience_multiplier);

        //Try to move it 'till it's under the user's feet, then try to pick it up
        //Not an item, so just delete the line if it's adjacent to the user.
        if (!isitem(currently_hooked) && Dm.num(get_dist(currently_hooked, get_turf(this))) < 1)
        {
            QDEL_NULL(ref fishing_line);
            return;
        }

        if (Dm.truthy(Dm.bitand(line_value?.fishing_line_traits, FISHING_LINE_PHASE)))
            await currently_hooked.forceMove(get_step_towards(currently_hooked, get_turf(this)));
        else
            await step_towards(currently_hooked, get_turf(this));

        if (!isitem(currently_hooked))
        {
            if (Dm.num(get_dist(currently_hooked, get_turf(this))) <= 1)
                QDEL_NULL(ref fishing_line);
            return;
        }

        obj_item item = (obj_item)currently_hooked;
        if (item.loc == user.loc && Dm.truthy(Dm.bitand(item.interaction_flags_item, INTERACT_ITEM_ATTACK_HAND_PICKUP)))
        {
            await user.put_in_inactive_hand(item);
            QDEL_NULL(ref fishing_line);
        }
    }

    // tg: code/modules/fishing/fishing_rod.dm:290
    public virtual object fishing_line_check()
    {
        return !QDELETED(fishing_line);
    }

    // tg: code/modules/fishing/fishing_rod.dm:293
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        await ui_interact(user);
        return dot;
    }

    /// Generates the fishing line visual from the current user to the target and updates inhands
    // tg: code/modules/fishing/fishing_rod.dm:298
    public virtual async DmTask<object> create_fishing_line(atom_movable target = null, mob_living firer = null, object target_py = null)
    {
        if (Dm.truthy(@internal))
            return null;
        if (fishing_line != null)
            QDEL_NULL(ref fishing_line);
        string beam_color = line_value?.line_color is string own_color && Dm.truthy(own_color) ? own_color : default_line_color;
        fishing_line = Dm.@new<datum_beam_held>(firer, target, Dm.named("icon_state", "fishing_line"), Dm.named("beam_color", beam_color), Dm.named("emissive", FALSE), Dm.named("override_target_pixel_y", target_py));
        fishing_line.lefthand = IS_LEFT_INDEX(Dm.num(firer.get_held_index_of_item(this))) ? TRUE : FALSE;
        RegisterSignal(fishing_line, COMSIG_BEAM_BEFORE_DRAW, nameof(check_los));
        RegisterSignal(fishing_line, COMSIG_QDELETING, nameof(clear_line));
        Waiting.Detach(fishing_line.Start()); // INVOKE_ASYNC(fishing_line, TYPE_PROC_REF(/datum/beam, Start))
        if (QDELETED(fishing_line))
            return null;
        await firer.update_held_items();
        return fishing_line;
    }

    // tg: code/modules/fishing/fishing_rod.dm:313
    [SignalHandler]
    public virtual void clear_line(datum source = null)
    {
        if (ismob(loc))
        {
            mob user = (mob)loc;
            Waiting.NoSleep(user.update_held_items());
        }
        fishing_line = null;
        currently_hooked = null;
    }

    // tg: code/modules/fishing/fishing_rod.dm:321
    public virtual object get_cast_range(mob_living user = null)
    {
        float dot = max(cast_range + Dm.num(line_value?.cast_range), 1);
        user ??= loc as mob_living;
        if (!isliving(user) || user.mind == null || !Dm.truthy(user.is_holding(this)))
            return dot;
        dot += round(Dm.num(user.mind.get_skill_level(typeof(datum_skill_fishing))) * 0.3f);
        return max(dot, 1);
    }

    // tg: code/modules/fishing/fishing_rod.dm:329
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        QDEL_NULL(ref fishing_line);
        return dot;
    }

    /// Hooks the item
    // tg: code/modules/fishing/fishing_rod.dm:334
    public virtual async DmTask hook_item(mob user = null, atom target_atom = null)
    {
        if (currently_hooked != null)
            return;
        if (!Dm.truthy(hook_value.can_be_hooked(target_atom)))
            return;
        if (!Dm.truthy(await create_fishing_line(target_atom as atom_movable, user as mob_living)))
            return;
        currently_hooked = target_atom as atom_movable;
        await hook_value.hook_attached(target_atom, this);
        SEND_SIGNAL(COMSIG_FISHING_ROD_HOOKED_ITEM, Dm.list(this, target_atom, user));
    }

    // Checks fishing line for interruptions and range
    // tg: code/modules/fishing/fishing_rod.dm:346
    [SignalHandler]
    public virtual object check_los(datum_beam source = null)
    {
        float cast_range = Dm.num(get_cast_range());
        if (!Dm.truthy(Dm.bitand(line_value?.fishing_line_traits, FISHING_LINE_PHASE)))
        {
            if (!Dm.truthy(Waiting.NoSleep(CheckToolReach(this, source.target as atom_movable, cast_range))))
            {
                qdel(source);
                return BEAM_CANCEL_DRAW;
            }
            return NONE;
        }

        //Bluespace fishing lines are simplier and only check the distance and if there're r-walls in the way.
        if (source.target.z != loc.z || Dm.num(get_dist(loc, source.target)) > cast_range)
        {
            qdel(source);
            return BEAM_CANCEL_DRAW;
        }

        //Check that there are no r-walls in the way of our bluespace fishing line
        DmList bresenham_line = get_line(loc, source.target) as DmList;
        Dm.remove(bresenham_line, get_turf(source.target)); //We don't check if the target is on a r-wall already, just the turfs separating us from it.
        Dm.remove(bresenham_line, get_turf(loc)); //ditto with our own turf.
        foreach (turf turf in Dm.each<turf>(bresenham_line))
        {
            if (isindestructiblewall(turf) || istype(turf, typeof(turf_closed_wall_r_wall)))
            {
                qdel(source);
                return BEAM_CANCEL_DRAW;
            }
        }
        return NONE;
    }

    // tg: code/modules/fishing/fishing_rod.dm:376
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        //this prevent trying to use telekinesis to fish (which would be broken anyway), also whacking people with a rod.
        if (!Dm.truthy(user.contains(this)) || (Dm.truthy(user.combat_mode) && !isturf(interacting_with)) || HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return await base.interact_with_atom(interacting_with, user, modifiers);
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/modules/fishing/fishing_rod.dm:382
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (hook_value == null)
        {
            balloon_alert(user, I18n.T("install a hook first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        // Reel in if able
        if (currently_hooked != null)
        {
            await reel(user);
            return ITEM_INTERACT_BLOCKING;
        }

        await cast_line(interacting_with, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/fishing_rod.dm:395
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await ranged_interact_with_atom_secondary(interacting_with, user, modifiers);
    }

    // tg: code/modules/fishing/fishing_rod.dm:398
    public override async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        //Stop reeling, delete the fishing line
        if (currently_hooked != null)
        {
            QDEL_NULL(ref fishing_line);
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.ranged_interact_with_atom_secondary(interacting_with, user, modifiers);
    }

    /// If the line to whatever that is is clear and we're not already busy, try fishing in it
    // tg: code/modules/fishing/fishing_rod.dm:405
    public virtual async DmTask cast_line(atom target = null, mob user = null, params object[] __extra)
    {
        if (Dm.truthy(casting) || currently_hooked != null)
            return;
        if (hook_value == null)
        {
            balloon_alert(user, I18n.T("install a hook first!"));
            return;
        }
        // COOLDOWN_FINISHED(src, casting_cd) (__DEFINES/cooldowns.dm:113), inline (rules 11.1)
        if (!(casting_cd < world.time))
            return;
        float cast_range = Dm.num(get_cast_range(user as mob_living));
        // Inside of storages, or camera weirdness
        if (target.z != user.z || Dm.num(get_dist(user, target)) > (cast_range + 3))
            return;
        casting_cd = world.time + 1 * SECONDS; // COOLDOWN_START(src, casting_cd, 1 SECONDS), inline
        // skip firing a projectile if the target is adjacent and can be reached (no order windows in the way),
        // otherwise it may end up hitting other things on its turf, which is problematic
        // especially for entities with the profound fisher component, which should only work on
        // proper fishing spots.
        if (Dm.truthy(await target.Adjacent(user, null, null, 0)))
        {
            await hook_hit(target, user);
            return;
        }
        casting = TRUE;
        obj_projectile_fishing_cast cast_projectile = await Dm.new_async<obj_projectile_fishing_cast>(get_turf(this));
        cast_projectile.range = Dm.num(get_cast_range(user as mob_living));
        cast_projectile.maximum_range = Dm.num(get_cast_range(user as mob_living));
        cast_projectile.owner = this;
        cast_projectile.original = target;
        cast_projectile.fired_from = this;
        cast_projectile.firer = user;
        cast_projectile.impacted = Dm.list(Dm.kv(WEAKREF(user), TRUE));
        if (Dm.truthy(Dm.bitand(line_value?.fishing_line_traits, FISHING_LINE_PHASE)))
            cast_projectile.pass_flags = ALL;
        await cast_projectile.aim_projectile(target, user);
        await cast_projectile.fire();
    }

    /// Called by hook projectile when hitting things
    // tg: code/modules/fishing/fishing_rod.dm:438
    public virtual async DmTask hook_hit(atom atom_hit_by_hook_projectile = null, mob user = null)
    {
        if (hook_value == null)
            return;
        if (Dm.truthy(Dm.bitand(atom_hit_by_hook_projectile.SEND_SIGNAL(COMSIG_FISHING_ROD_CAST, Dm.list(atom_hit_by_hook_projectile, this, user)), FISHING_ROD_CAST_HANDLED)))
            return;
        /// If you can't fish in it, try hooking it
        await hook_item(user, atom_hit_by_hook_projectile);
    }

    // tg: code/modules/fishing/fishing_rod.dm:446
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "FishingRod", name);
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/fishing/fishing_rod.dm:452
    public override object ui_state(mob user = null)
    {
        if (Dm.truthy(@internal))
            return GLOB.deep_inventory_state;
        else
            return GLOB.default_state;
    }

    // tg: code/modules/fishing/fishing_rod.dm:458
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, get_fishing_overlays());
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:462
    public virtual object get_fishing_overlays()
    {
        DmList dot = Dm.list();
        string line_color = line_value?.line_color is string own_color && Dm.truthy(own_color) ? own_color : default_line_color;
        /// Line part by the rod.
        if (Dm.truthy(reel_overlay))
        {
            mutable_appearance reel_appearance = mutable_appearance(icon, reel_overlay, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
            reel_appearance.color = line_color;
            Dm.append(ref dot, reel_appearance);
        }

        // Line & hook is also visible when only bait is equipped but it uses default appearances then
        if (hook_value != null || bait_value != null)
        {
            mutable_appearance line_overlay = mutable_appearance(icon, "line_overlay", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
            line_overlay.color = line_color;
            Dm.append(ref dot, line_overlay);
            Dm.append(ref dot, hook_value?.rod_overlay_icon_state ?? "hook_overlay");
        }

        if (bait_value != null)
        {
            string bait_state = "worm_overlay"; //default to worm overlay for anything without specific one
            if (istype(bait_value, typeof(obj_item_food_bait)))
            {
                obj_item_food_bait real_bait = (obj_item_food_bait)bait_value;
                bait_state = real_bait.rod_overlay_icon_state as string;
            }
            if (istype(bait_value, typeof(obj_item_stock_parts_power_store_cell_lead)))
                bait_state = "battery_overlay";
            Dm.append(ref dot, bait_state);
        }
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:487
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        Dm.append(ref dot, get_fishing_worn_overlays(standing, isinhands, icon_file, bodyshape));
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:491
    public virtual object get_fishing_worn_overlays(mutable_appearance standing = null, object isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        DmList dot = Dm.list();
        string line_color = line_value?.line_color is string own_color && Dm.truthy(own_color) ? own_color : default_line_color;
        mutable_appearance reel_overlay = mutable_appearance(icon_file, "reel_overlay", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
        reel_overlay.color = line_color;
        Dm.append(ref dot, reel_overlay);
        /// if we don't have anything hooked show the dangling hook & line
        if (Dm.truthy(isinhands) && fishing_line == null)
        {
            mutable_appearance line_overlay = mutable_appearance(icon_file, "line_overlay", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
            line_overlay.color = line_color;
            Dm.append(ref dot, line_overlay);
            Dm.append(ref dot, mutable_appearance(icon_file, "hook_overlay"));
        }
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:505
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(slot_check(tool, ROD_SLOT_LINE)))
        {
            await use_slot(ROD_SLOT_LINE, user, tool);
            SStgui.update_uis(this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(slot_check(tool, ROD_SLOT_HOOK)))
        {
            await use_slot(ROD_SLOT_HOOK, user, tool);
            SStgui.update_uis(this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(slot_check(tool, ROD_SLOT_BAIT)) || istype(tool, typeof(obj_item_bait_can))) //Can click on the fishing rod with bait can directly
        {
            await use_slot(ROD_SLOT_BAIT, user, tool);
            SStgui.update_uis(this);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // DEVIATION(DV679): tg sends each slot's picture as a PNG of its first frame (icon2base64(icon(...))); the port sends the
    // part's icon and icon state, which the client's FishingRod window draws from tg's sprite (the server reads no .dmi, D104).
    // tg: code/modules/fishing/fishing_rod.dm:522
    public override async DmTask<object> ui_data(mob user = null)
    {
        await base.ui_data(user);
        DmList data = Dm.list();

        data["bait_name"] = format_text(bait_value?.name);
        data["bait_icon"] = bait_value != null ? SlotPicture(bait_value) : null;

        data["line_name"] = format_text(line_value?.name);
        data["line_icon"] = line_value != null ? SlotPicture(line_value) : null;

        data["hook_name"] = format_text(hook_value?.name);
        data["hook_icon"] = hook_value != null ? SlotPicture(hook_value) : null;

        data["busy"] = fishing_line != null ? TRUE : FALSE;

        data["description"] = ui_description;

        return data;
    }

    /// The picture of a slotted part for the window: its icon file and icon state (DV679).
    private static DmList SlotPicture(obj_item part)
        => Dm.list(Dm.kv("icon", Dm.text("[]", part.icon)), Dm.kv("icon_state", part.icon_state));

    /// Checks if the item fits the slot
    // tg: code/modules/fishing/fishing_rod.dm:541
    public virtual object slot_check(obj_item item = null, object slot = null)
    {
        if (item == null)
            return FALSE;
        switch (slot)
        {
            case ROD_SLOT_HOOK:
                if (!istype(item, typeof(obj_item_fishing_hook)))
                    return FALSE;
                break;
            case ROD_SLOT_LINE:
                if (!istype(item, typeof(obj_item_fishing_line)))
                    return FALSE;
                break;
            case ROD_SLOT_BAIT:
                if (!HAS_TRAIT(item, TRAIT_FISHING_BAIT))
                    return FALSE;
                break;
        }
        return TRUE;
    }

    // tg: code/modules/fishing/fishing_rod.dm:556
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob user = usr;
        switch (action)
        {
            case "slot_action":
                // Simple click with empty hand to remove, click with item to insert/switch
                obj_item held_item = user.get_active_held_item() as obj_item;
                await use_slot(@params["slot"], user, held_item == this ? null : held_item);
                return TRUE;
        }
        return dot;
    }

    /// Ideally this will be replaced with generic slotted storage datum + display
    // tg: code/modules/fishing/fishing_rod.dm:568
    public virtual async DmTask use_slot(object slot = null, mob user = null, obj_item new_item = null)
    {
        if (fishing_line != null || Dm.truthy(GLOB.fishing_challenges_by_user[user]))
            return;
        // If the new item is a bait can, try to get bait from it
        if (Dm.eq(slot, ROD_SLOT_BAIT) && istype(new_item, typeof(obj_item_bait_can)))
        {
            obj_item_bait_can can = (obj_item_bait_can)new_item;
            obj_item bait = can.retrieve_bait(user) as obj_item;
            if (bait == null)
                return;
            new_item = bait;
        }
        obj_item current_item = null;
        switch (slot)
        {
            case ROD_SLOT_BAIT:
                current_item = bait_value;
                break;
            case ROD_SLOT_HOOK:
                current_item = hook_value;
                break;
            case ROD_SLOT_LINE:
                current_item = line_value;
                break;
        }
        if (new_item == null && current_item == null)
            return;
        // Trying to remove the item
        if (new_item == null && current_item != null)
        {
            await user.put_in_hands(current_item);
            balloon_alert(user, I18n.T("[] removed", slot));
        }
        // Trying to insert item into empty slot
        else if (new_item != null && current_item == null)
        {
            if (!Dm.truthy(slot_check(new_item, slot)))
                return;
            if (Dm.truthy(await user.transferItemToLoc(new_item, this)))
            {
                set_slot(new_item, slot);
                balloon_alert(user, I18n.T("[] installed", slot));
            }
            else
            {
                balloon_alert(user, I18n.T("stuck to your hands!"));
                return;
            }
        }
        /// Trying to swap item
        else if (new_item != null && current_item != null)
        {
            if (!Dm.truthy(slot_check(new_item, slot)))
                return;
            if (Dm.truthy(await user.transferItemToLoc(new_item, this)))
            {
                await user.put_in_hands(current_item);
                set_slot(new_item, slot);
                balloon_alert(user, I18n.T("[] swapped", slot));
            }
            else
            {
                balloon_alert(user, I18n.T("stuck to your hands!"));
                return;
            }
        }

        await update_icon();
        playsound(this, Dm.resource("sound/items/click.ogg"), 50, TRUE);
    }

    ///assign an item to the given slot and its standard effects, while Exited() should handle unsetting the slot.
    // tg: code/modules/fishing/fishing_rod.dm:619
    public virtual void set_slot(obj_item equipment = null, object slot = null)
    {
        switch (slot)
        {
            case ROD_SLOT_BAIT:
                bait_value = equipment;
                if (!HAS_TRAIT(bait_value, TRAIT_BAIT_ALLOW_FISHING_DUD))
                    ADD_TRAIT(this, TRAIT_ROD_REMOVE_FISHING_DUD, INNATE_TRAIT);
                break;
            case ROD_SLOT_HOOK:
                hook_value = (obj_item_fishing_hook)equipment;
                break;
            case ROD_SLOT_LINE:
                line_value = (obj_item_fishing_line)equipment;
                break;
            default:
                throw Dm.CRASH(Dm.text("set_slot called with an undefined slot: []", slot), "code/modules/fishing/fishing_rod.dm", 630);
        }

        equipment.SEND_SIGNAL(COMSIG_ITEM_FISHING_ROD_SLOTTED, Dm.list(equipment, this, slot));
    }

    // tg: code/modules/fishing/fishing_rod.dm:634
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        string slot = null;
        if (gone == bait_value)
        {
            slot = ROD_SLOT_BAIT;
            bait_value = null;
            REMOVE_TRAIT(this, TRAIT_ROD_REMOVE_FISHING_DUD, INNATE_TRAIT);
        }
        if (gone == line_value)
        {
            slot = ROD_SLOT_LINE;
            line_value = null;
        }
        if (gone == hook_value)
        {
            slot = ROD_SLOT_HOOK;
            QDEL_NULL(ref fishing_line);
            hook_value = null;
        }

        if (slot != null)
            gone.SEND_SIGNAL(COMSIG_ITEM_FISHING_ROD_UNSLOTTED, Dm.list(gone, this, slot));
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:654
    public virtual object get_frame(datum_fishing_challenge challenge = null)
    {
        return mutable_appearance(Dm.resource("icons/hud/fishing_hud.dmi"), frame_state);
    }
}

// tg: code/modules/fishing/fishing_rod.dm:677
public partial class obj_item_fishing_rod_telescopic : obj_item_fishing_rod
{
    // tg: code/modules/fishing/fishing_rod.dm:699
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming), Dm.named("force_on", 8), Dm.named("hitsound_on", hitsound), Dm.named("w_class_on", WEIGHT_CLASS_HUGE), Dm.named("clumsy_check", FALSE)));
        RegisterSignal(this, COMSIG_TRANSFORMING_PRE_TRANSFORM, nameof(pre_transform));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:705
    public override object reason_we_cant_fish(datum_fish_source target_fish_source = null)
    {
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return I18n.T("You need to extend your fishing rod before you can cast the line.");
        return base.reason_we_cant_fish(target_fish_source);
    }

    // tg: code/modules/fishing/fishing_rod.dm:710
    [DmParams("target", "user", "proximity_flag")]
    public override async DmTask cast_line(atom target = null, mob user = null, params object[] __extra)
    {
        object proximity_flag = Dm.arg(__extra, 0);
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
        {
            if (!Dm.truthy(proximity_flag))
                balloon_alert(user, I18n.T("extend the rod first!"));
            return;
        }
        await base.cast_line(target, user, __extra);
    }

    // tg: code/modules/fishing/fishing_rod.dm:717
    public override object get_fishing_overlays()
    {
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return Dm.list();
        return base.get_fishing_overlays();
    }

    // tg: code/modules/fishing/fishing_rod.dm:722
    public override object get_fishing_worn_overlays(mutable_appearance standing = null, object isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return Dm.list();
        return base.get_fishing_worn_overlays(standing, isinhands, icon_file, bodyshape);
    }

    ///Stops the fishing rod from being collapsed while fishing.
    // tg: code/modules/fishing/fishing_rod.dm:728
    [SignalHandler]
    public virtual object pre_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return null;
        //the fishing minigame uses the attack_self signal to let the user end it early without having to drop the rod.
        if (Dm.truthy(GLOB.fishing_challenges_by_user[user]))
            return COMPONENT_BLOCK_TRANSFORM;
        return null;
    }

    ///Gives feedback to the user, makes it show up inhand, toggles whether it can be used for fishing.
    // tg: code/modules/fishing/fishing_rod.dm:737
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        inhand_icon_state = Dm.truthy(active) ? "rod" : null; // When inactive, there is no inhand icon_state.
        if (user != null)
            balloon_alert(user, Dm.truthy(active) ? I18n.T("extended") : I18n.T("collapsed"));
        playsound(this, Dm.resource("sound/items/weapons/batonextend.ogg"), 50, TRUE);
        update_appearance();
        QDEL_NULL(ref fishing_line);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/modules/fishing/fishing_rod.dm:749
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.initial(this, (atom p) => p.icon_state), !HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE) ? "_collapsed" : "");
        return dot;
    }
}

// tg: code/modules/fishing/fishing_rod.dm:772
public partial class obj_item_fishing_rod_tech : obj_item_fishing_rod
{
    // tg: code/modules/fishing/fishing_rod.dm:788
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        DmList fishing_signals = Dm.list(
            Dm.kv(COMSIG_FISHING_ROD_HOOKED_ITEM, "/datum/component/experiment_handler/proc/" + nameof(datum_component_experiment_handler.try_run_handheld_experiment)),
            Dm.kv(COMSIG_FISHING_ROD_CAUGHT_FISH, "/datum/component/experiment_handler/proc/" + nameof(datum_component_experiment_handler.try_run_handheld_experiment)),
            Dm.kv(COMSIG_ITEM_PRE_ATTACK, "/datum/component/experiment_handler/proc/" + nameof(datum_component_experiment_handler.try_run_handheld_experiment)),
            Dm.kv(COMSIG_ITEM_AFTERATTACK, "/datum/component/experiment_handler/proc/" + nameof(datum_component_experiment_handler.ignored_handheld_experiment_attempt)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_experiment_handler),
            Dm.named("config_mode", EXPERIMENT_CONFIG_ALTCLICK),
            Dm.named("allowed_experiments", Dm.list(typeof(datum_experiment_scanning_fish))),
            Dm.named("config_flags", Dm.bitor(EXPERIMENT_CONFIG_SILENT_FAIL, EXPERIMENT_CONFIG_IMMEDIATE_ACTION)),
            Dm.named("experiment_signals", fishing_signals)));
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:805
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("<b>Alt-Click</b> to access the Experiment Configuration UI")));
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:809
    public override async DmTask use_slot(object slot = null, mob user = null, obj_item new_item = null)
    {
        if (Dm.eq(slot, ROD_SLOT_BAIT))
            return;
        await base.use_slot(slot, user, new_item);
    }
}

// tg: code/modules/fishing/fishing_rod.dm:814
public partial class obj_item_fishing_rod_material : obj_item_fishing_rod
{
    // tg: code/modules/fishing/fishing_rod.dm:823
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = I18n.T("fishing rod");
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:827
    public override async DmTask finalize_remove_material_effects(DmList materials = null)
    {
        await base.finalize_remove_material_effects(materials);
        name = I18n.T("fishing rod"); //so it doesn't reset to "material fishing rod"
    }

    // tg: code/modules/fishing/fishing_rod.dm:831
    public override object get_frame(datum_fishing_challenge challenge = null)
    {
        mutable_appearance frame = (mutable_appearance)base.get_frame(challenge);
        frame.color = color;
        return frame;
    }
}

// tg: code/modules/fishing/fishing_rod.dm:839
public partial class obj_projectile_fishing_cast : obj_projectile
{
    // tg: code/modules/fishing/fishing_rod.dm:849
    public override async DmTask fire(object angle = null, atom direct_target = null)
    {
        if (owner.hook_value != null)
            icon_state = owner.hook_value.icon_state;
        await base.fire(angle, direct_target);
        if (!QDELETED(this))
            await owner.create_fishing_line(this, firer as mob_living);
    }

    ///If the fishing line is phasing, we don't care about anything entering our turf
    // tg: code/modules/fishing/fishing_rod.dm:856
    public override void on_entered(datum source = null, atom_movable entered_atom = null)
    {
        if (Dm.truthy(Dm.bitand(owner.line_value?.fishing_line_traits, FISHING_LINE_PHASE)))
            return;
        base.on_entered(source, entered_atom);
    }

    ///Override of the generic scan_moved_turf so we don't scan for mobs when we move. Also cannot phase through r-walls for balance reasons (stealing from the armory *wink wink*)
    // tg: code/modules/fishing/fishing_rod.dm:862
    public override async DmTask scan_moved_turf()
    {
        if (Dm.truthy(await can_hit_target(original, TRUE, FALSE)))
        {
            await impact(original);
            return;
        }
        if (isindestructiblewall(loc) || istype(loc, typeof(turf_closed_wall_r_wall)))
            qdel(this);
    }

    // tg: code/modules/fishing/fishing_rod.dm:869
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        QDEL_NULL(ref owner.fishing_line); //we need to delete the old beam datum, otherwise it won't let you fish.
        owner.casting = FALSE; //set the casting value to false so we don't delete the new fishing line (different target) on Destroy()
        if (blocked >= 100)
            return dot;
        await owner.hook_hit(target, firer as mob);

        if (!isturf(target) || owner.fishing_line != null)
            return dot;

        //So, the target is a turf and we couldn't enstablish a fishing line to it, so let's pick a random thing from that turf.
        DmList possible_picks = Dm.list();
        foreach (atom_movable movable in Dm.each<atom_movable>(get_turf(this)))
        {
            if (movable == this)
                continue;
            if (Dm.truthy(await can_hit_target(movable, direct_target: TRUE, ignore_loc: TRUE)))
                Dm.append(ref possible_picks, movable);
        }
        if (Dm.truthy(length(possible_picks)))
        {
            await impact(pick(possible_picks) as atom);
            return dot;
        }
        return dot;
    }

    // tg: code/modules/fishing/fishing_rod.dm:893
    public override object Destroy(float? force = null)
    {
        if (owner != null && Dm.truthy(owner.casting))
        {
            owner.casting = FALSE;
            QDEL_NULL(ref owner.fishing_line);
        }
        owner = null;
        return base.Destroy(force);
    }
}
