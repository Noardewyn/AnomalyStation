// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

//Elite mining mobs
// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:9
public partial class mob_living_simple_animal_hostile_asteroid_elite : mob_living_simple_animal_hostile_asteroid
{
    //Gives player-controlled variants the ability to swap attacks
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_seethrough_mob)));
        grant_actions_by_list(attack_action_types);
        ADD_TRAIT(this, TRAIT_UNCONVERTABLE, INNATE_TRAIT); //You cannot convert them while they're battling.
        return dot;
    }

    //Prevents elites from attacking members of their faction (can't hurt themselves either) and lets them mine rock with an attack despite not being able to smash walls.
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:39
    public override async DmTask<object> AttackingTarget(atom attacked_target = null)
    {
        if (ishostile(target))
        {
            mob_living_simple_animal_hostile M = target as mob_living_simple_animal_hostile;
            if (Dm.truthy(faction_check_atom(M)))
                return FALSE;
        }
        if (target is obj_structure_elite_tumor T)
        {
            if (T.mychild == this && T.activity == obj_structure_elite_tumor.TUMOR_PASSIVE)
            {
                object elite_remove = await tgui_alert(usr, "Re-enter the tumor?", "Despawn yourself?", Dm.list("Yes", "No"));
                if (Dm.eq(elite_remove, "No") || QDELETED(this) || !Dm.truthy(await Adjacent(T)))
                    return null;
                T.mychild = null;
                T.activity = obj_structure_elite_tumor.TUMOR_INACTIVE;
                T.icon_state = "advanced_tumor";
                qdel(this);
                return FALSE;
            }
        }
        object dot = await base.AttackingTarget(attacked_target);
        if (ismineralturf(target))
        {
            turf_closed_mineral M = target as turf_closed_mineral;
            await M.gets_drilled();
        }
        if (ismecha(target))
        {
            obj_vehicle_sealed_mecha M = target as obj_vehicle_sealed_mecha;
            await M.take_damage(50, BRUTE, MELEE, 1);
        }
        return dot;
    }

    //Elites can't talk (normally)!
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:64
    public override object can_speak__proc(float? allow_mimes = null)
    {
        return Dm.truthy(can_talk) ? base.can_speak__proc(allow_mimes) : can_talk;
    }
}

/*Basic setup for elite attacks, based on Whoneedspace's megafauna attack setup.
While using this makes the system rely on OnFire, it still gives options for timers not tied to OnFire, and it makes using attacks consistent accross the board for player-controlled elites.*/

// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:70
public partial class datum_action_innate_elite_attack : datum_action_innate
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:81
    public override async DmTask<object> create_button(mob viewer = null)
    {
        atom_movable_screen_movable_action_button button = await base.create_button(viewer) as atom_movable_screen_movable_action_button;
        button.maptext = "";
        button.maptext_x = 6;
        button.maptext_y = 2;
        button.maptext_width = 24;
        button.maptext_height = 12;
        return button;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:90
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (owner == null)
        {
            STOP_PROCESSING(SSfastprocess, this);
            qdel(this);
            return null;
        }

        Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_STATUS));
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:98
    public override async DmTask update_button_status(atom_movable_screen_movable_action_button button = null, float? force = null)
    {
        mob_living_simple_animal_hostile_asteroid_elite elite_owner = owner as mob_living_simple_animal_hostile_asteroid_elite;
        if (!(owner is mob))
        {
            button.maptext = "";
            return;
        }

        float timeleft = max(elite_owner.ranged_cooldown - world.time, 0);
        if (timeleft == 0)
            button.maptext = "";
        else
            button.maptext = MAPTEXT(Dm.text("<b>[]</b>", round(timeleft / 10, 0.1f)));
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:110
    public override async DmTask<object> Grant(mob L = null, params object[] __extra)
    {
        if (L is mob_living_simple_animal_hostile_asteroid_elite)
        {
            START_PROCESSING(SSfastprocess, this);
            return await base.Grant(L, __extra);
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:116
    public override async DmTask<object> Activate()
    {
        mob_living_simple_animal_hostile_asteroid_elite elite_owner = owner as mob_living_simple_animal_hostile_asteroid_elite;
        elite_owner.chosen_attack = chosen_attack_num;
        to_chat(elite_owner, chosen_message);
        await DmTask.CompletedTask;
        return null;
    }
}

//The Pulsing Tumor, the actual "spawn-point" of elites, handles the spawning, arena, and procs for dealing with basic scenarios.

// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:123
public partial class obj_structure_elite_tumor : obj_structure
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:1-3 (file-local #defines)
    public const float TUMOR_INACTIVE = 0;
    public const float TUMOR_ACTIVE = 1;
    public const float TUMOR_PASSIVE = 2;

    // Makes the elite mob weaker on victory so they don't just go and trash the station with a dangerous, big health pool
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:6
    internal const float ELITE_POST_BATTLE_HEALTH_MULTIPLIER = 0.4f;

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:159
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!ishuman(user))
            return dot;
        if (activity == TUMOR_PASSIVE)
        {
            // Prevents the user from being forcemoved back and forth between two elite arenas.
            if (HAS_TRAIT(user, TRAIT_ELITE_CHALLENGER))
            {
                user.visible_message(span_warning(I18n.T("[] reaches for [] with [] arm, but nothing happens.", user, this, user.p_their())),
                    span_warning(I18n.T("You reach for [] with your arm... but nothing happens.", this)));
                return dot;
            }
            activity = TUMOR_ACTIVE;
            user.visible_message(span_boldwarning(I18n.T("[] convulses as []'s arm enters its radius.  Uh-oh...", this, user)),
                span_boldwarning(I18n.T("[] convulses as your arm enters its radius.  Your instincts tell you to step back.", this)));
            make_activator(user);
            if (Dm.truthy(boosted))
            {
                mychild.playsound_local(get_turf(mychild), Dm.resource("sound/effects/magic.ogg"), 40, 0);
                to_chat(mychild, I18n.T("<b>Someone has activated your tumor.  You will be returned to fight shortly, get ready!</b>"));
            }
            _addtimer(Dm.@new<datum_callback>(this, nameof(return_elite)), 3 * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm", line: 175);
            Waiting.Detach(arena_checks());
        }
        else if (activity == TUMOR_INACTIVE)
        {
            if (HAS_TRAIT(user, TRAIT_ELITE_CHALLENGER))
            {
                user.visible_message(span_warning(I18n.T("[] reaches for [] with [] arm, but nothing happens.", user, this, user.p_their())),
                    span_warning(I18n.T("You reach for [] with your arm... but nothing happens.", this)));
                return dot;
            }
            activity = TUMOR_ACTIVE;
            mob_dead_observer elitemind = null;
            visible_message(span_boldwarning(I18n.T("[] begins to convulse.  Your instincts tell you to step back.", this)));
            make_activator(user);
            if (!Dm.truthy(boosted))
            {
                _addtimer(Dm.@new<datum_callback>(this, nameof(spawn_elite)), 3 * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm", line: 187);
                return dot;
            }
            visible_message(span_boldwarning(I18n.T("Something within [] stirs...", this)));
            mob chosen_one = await SSpolling.poll_ghosts_for_target(check_jobban: ROLE_SENTIENCE, role: ROLE_SENTIENCE, poll_time: 5 * SECONDS, checked_target: this, ignore_category: POLL_IGNORE_LAVALAND_ELITE, alert_pic: this, role_name_text: "lavaland elite") as mob;
            if (chosen_one != null)
            {
                audible_message(span_boldwarning(I18n.T("The stirring sounds increase in volume!")));
                elitemind = chosen_one as mob_dead_observer;
                elitemind.playsound_local(get_turf(elitemind), Dm.resource("sound/effects/magic.ogg"), 40, 0);
                to_chat(elitemind, I18n.T("<b>You have been chosen to play as a Lavaland Elite.\nIn a few seconds, you will be summoned on Lavaland as a monster to fight your activator, in a fight to the death.\nYour attacks can be switched using the buttons on the top left of the HUD, and used by clicking on targets or tiles similar to a gun.\nWhile the opponent might have an upper hand with  powerful mining equipment and tools, you have great power normally limited by AI mobs.\nIf you want to win, you'll have to use your powers in creative ways to ensure the kill. It's suggested you try using them all as soon as possible.\nShould you win, you'll receive extra information regarding what to do after. Good luck!</b>"));
                _addtimer(Dm.@new<datum_callback>(this, nameof(spawn_elite), elitemind), 10 * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm", line: 202);
            }
            else
            {
                visible_message(span_boldwarning(I18n.T("The stirring stops, and nothing emerges.  Perhaps try again later.")));
                activity = TUMOR_INACTIVE;
                clear_activator(user);
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:208
    public virtual void spawn_elite(mob_dead_observer elitemind = null)
    {
        object selectedspawn = pick(potentialspawns as DmList);
        mychild = Dm.@new((Type)selectedspawn, loc) as mob_living_simple_animal_hostile_asteroid_elite;
        visible_message(span_boldwarning(I18n.T("[] emerges from []!", mychild, this)));
        playsound(loc, Dm.resource("sound/effects/phasein.ogg"), 200, 0, 50, TRUE, TRUE);
        if (Dm.truthy(boosted))
        {
            mychild.PossessByPlayer(elitemind.key);
            mychild.sentience_act();
            notify_ghosts(
                I18n.T("\\A [] has been awakened in \\the []!", mychild, get_area(this)),
                source: mychild,
                header: "Lavaland Elite awakened",
                notify_flags: NOTIFY_CATEGORY_NOFLASH);
        }
        mychild.log_message(Dm.text("has been awakened by []!", key_name(activator)), LOG_GAME, color: "#960000");
        icon_state = "tumor_popped";
        RegisterSignal(mychild, COMSIG_QDELETING, nameof(onEliteLoss));
        Waiting.Detach(arena_checks());
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:227
    public virtual async DmTask return_elite()
    {
        await mychild.forceMove(loc);
        visible_message(span_boldwarning(I18n.T("[] emerges from []!", mychild, this)));
        playsound(loc, Dm.resource("sound/effects/phasein.ogg"), 200, 0, 50, TRUE, TRUE);
        await mychild.revive(HEAL_ALL);
        if (Dm.truthy(boosted))
        {
            mychild.maxHealth = Dm.num(mychild.maxHealth) * (1 / ELITE_POST_BATTLE_HEALTH_MULTIPLIER); //we multiply it back to its original value
            mychild.health = mychild.maxHealth;
            notify_ghosts(
                I18n.T("\\A [] has been challenged in \\the []!", mychild, get_area(this)),
                source: mychild,
                header: "Lavaland Elite challenged",
                notify_flags: NOTIFY_CATEGORY_NOFLASH);
        }
        mychild.log_message(Dm.text("has been challenged by []!", key_name(activator)), LOG_GAME, color: "#960000");
        ADD_TRAIT(mychild, TRAIT_UNCONVERTABLE, INNATE_TRAIT);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:244
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_gps), "Menacing Signal"));
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:249
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        mychild = null;
        clear_activator(activator);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:255
    public virtual void make_activator(mob user = null)
    {
        if (activator != null)
            return;
        activator = user as mob_living_carbon_human;
        ADD_TRAIT(user, TRAIT_ELITE_CHALLENGER, REF(this));
        RegisterSignal(user, COMSIG_QDELETING, nameof(clear_activator));
        user.log_message("has activated an elite tumor!", LOG_GAME, color: "#960000");
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:263
    [SignalHandler]
    public virtual void clear_activator(mob source = null)
    {
        if (activator == null)
            return;
        activator = null;
        REMOVE_TRAIT(source, TRAIT_ELITE_CHALLENGER, REF(this));
        UnregisterSignal(source, COMSIG_QDELETING);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:271
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!isturf(loc))
            return null;

        foreach (mob_living_simple_animal_hostile_asteroid_elite elitehere in Dm.each<mob_living_simple_animal_hostile_asteroid_elite>(loc))
        {
            if (elitehere == mychild && activity == TUMOR_PASSIVE)
            {
                Waiting.NoSleep(mychild.adjust_brute_loss(-Dm.num(mychild.maxHealth) * 0.025f * Dm.num(seconds_per_tick)));
                obj_effect_temp_visual_heal H = Dm.@new<obj_effect_temp_visual_heal>(get_turf(mychild));
                H.color = COLOR_RED;
            }
        }
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:281
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item attacking_item = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = NONE;
        if (attacking_item is obj_item_organ_monster_core_regenerative_core && activity == TUMOR_INACTIVE && !Dm.truthy(boosted))
        {
            obj_item_organ_monster_core_regenerative_core core = attacking_item as obj_item_organ_monster_core_regenerative_core;
            visible_message(span_boldwarning(I18n.T("As [] drops the core into [], [] appears to swell.", user, this, this)));
            icon_state = "advanced_tumor";
            boosted = TRUE;
            set_light_range(6);
            desc = I18n.T("[]  This one seems to glow with a strong intensity.", desc);
            qdel(core);
            return ITEM_INTERACT_SUCCESS;
        }
        await DmTask.CompletedTask;
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:293
    public virtual async DmTask arena_checks()
    {
        if (activity != TUMOR_ACTIVE || QDELETED(this))
            return;
        Waiting.Detach(fighters_check()); //Checks to see if our fighters died.
        Waiting.DetachSync(() => arena_trap()); //Gets another arena trap queued up for when this one runs out.
        Waiting.Detach(border_check()); //Checks to see if our fighters got out of the arena somehow.
        if (!QDELETED(this))
            _addtimer(Dm.@new<datum_callback>(this, nameof(arena_checks)), 5 * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm", line: 303);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:302
    public virtual async DmTask fighters_check()
    {
        if (QDELETED(mychild) || mychild.stat == DEAD)
        {
            onEliteLoss();
            return;
        }
        if (QDELETED(activator) || activator.stat == DEAD || (Dm.num(activator.health) <= Dm.num(activator.dead_threshold) && HAS_TRAIT(activator, TRAIT_NODEATH)))
        {
            if (!QDELETED(activator) && HAS_TRAIT(activator, TRAIT_NODEATH)) // dust the unkillable activator
                await activator.dust(drop_items: TRUE);
            await onEliteWon();
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:311
    public virtual void arena_trap()
    {
        turf tumor_turf = get_turf(this);
        if (loc == null)
            return;
        datum_weakref activator_ref = WEAKREF(activator) as datum_weakref;
        datum_weakref mychild_ref = WEAKREF(mychild) as datum_weakref;
        foreach (object tumor_range_turfs in Dm.each(RANGE_TURFS(12, tumor_turf)))
        {
            if (Dm.num(get_dist(tumor_range_turfs, tumor_turf)) == 12)
            {
                obj_effect_temp_visual_elite_tumor_wall newwall;
                newwall = Dm.@new<obj_effect_temp_visual_elite_tumor_wall>(tumor_range_turfs, this);
                newwall.activator_ref = activator_ref;
                newwall.ourelite_ref = mychild_ref;
            }
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:324
    public virtual async DmTask border_check()
    {
        if (activator != null && Dm.num(get_dist(this, activator)) >= 12)
        {
            await activator.forceMove(loc);
            visible_message(span_boldwarning(I18n.T("[] suddenly reappears above []!", activator, this)));
            playsound(loc, Dm.resource("sound/effects/phasein.ogg"), 200, 0, 50, TRUE, TRUE);
        }
        if (mychild != null && Dm.num(get_dist(this, mychild)) >= 12)
        {
            await mychild.forceMove(loc);
            visible_message(span_boldwarning(I18n.T("[] suddenly reappears above []!", mychild, this)));
            playsound(loc, Dm.resource("sound/effects/phasein.ogg"), 200, 0, 50, TRUE, TRUE);
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:334
    public virtual void onEliteLoss()
    {
        playsound(loc, Dm.resource("sound/effects/tendril_destroyed.ogg"), 200, 0, 50, TRUE, TRUE);
        visible_message(span_boldwarning(I18n.T("[] begins to convulse violently before beginning to dissipate.", this)));
        visible_message(span_boldwarning(I18n.T("As [] closes, something is forced up from down below.", this)));
        obj_structure_closet_crate_necropolis_tendril lootbox = Dm.@new<obj_structure_closet_crate_necropolis_tendril>(loc);
        if (Dm.truthy(boosted))
        {
            if (mychild.loot_drop != null && prob(50))
                Dm.@new(mychild.loot_drop, lootbox);
            else
                Dm.@new<obj_item_tumor_shard>(lootbox);
        }
        qdel(this);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:346
    public virtual async DmTask onEliteWon()
    {
        activity = TUMOR_PASSIVE;
        clear_activator(activator);
        await mychild.revive(HEAL_ALL);
        if (Dm.truthy(boosted))
        {
            times_won++;
            mychild.maxHealth = Dm.num(mychild.maxHealth) * ELITE_POST_BATTLE_HEALTH_MULTIPLIER;
            mychild.health = mychild.maxHealth;
        }
        if (times_won == 1)
        {
            mychild.playsound_local(get_turf(mychild), Dm.resource("sound/effects/magic.ogg"), 40, 0);
            to_chat(mychild, span_boldwarning(I18n.T("As the life in the activator's eyes fade, the forcefield around you dies out and you feel your power subside.\nDespite this inferno being your home, you feel as if you aren't welcome here anymore.\nWithout any guidance, your purpose is now for you to decide.")));
            to_chat(mychild, I18n.T("<b>Your max health has been halved, but can now heal by standing on your tumor. Note, it's your only way to heal.\nBear in mind, if anyone interacts with your tumor, you'll be resummoned here to carry out another fight. In such a case, you will regain your full max health.\nAlso, be weary of your fellow inhabitants, they likely won't be happy to see you!</b>"));
            to_chat(mychild, span_boldbig(I18n.T("Note that you are a lavaland monster, and thus not allied to the station. You should not cooperate or act friendly with any station crew unless under extreme circumstances!")));
        }

        REMOVE_TRAIT(mychild, TRAIT_UNCONVERTABLE, INNATE_TRAIT);
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:366
public partial class obj_item_tumor_shard : obj_item
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:379
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!(interacting_with is mob_living_simple_animal_hostile_asteroid_elite))
            return NONE;

        mob_living_simple_animal_hostile_asteroid_elite elite = interacting_with as mob_living_simple_animal_hostile_asteroid_elite;
        if (elite.stat != DEAD || !Dm.eq(elite.sentience_type, SENTIENCE_BOSS) || !Dm.truthy(elite.key))
        {
            user.visible_message(span_notice(I18n.T("It appears [] is unable to be revived right now. Perhaps try again later.", elite)));
            return ITEM_INTERACT_BLOCKING;
        }
        elite.set_allies(Dm.list(Dm.text("[]", REF(user))));
        elite.set_faction(null);
        await elite.revive(HEAL_ALL);
        user.visible_message(span_notice(I18n.T("[] stabs [] with [], reviving it.", user, elite, this)));
        elite.playsound_local(get_turf(elite), Dm.resource("sound/effects/magic.ogg"), 40, 0);
        to_chat(elite, span_userdanger(I18n.T("You have been revived by []. While you can't speak to them, you owe [] a great debt.  Assist [] in achieving [] goals, regardless of risk.", user, user, user.p_them(), user.p_their())));
        to_chat(elite, span_boldbig(I18n.T("Note that you now share the loyalties of [].  You are expected not to intentionally sabotage their faction unless commanded to!", user)));
        elite.maxHealth = Dm.num(elite.maxHealth) * obj_structure_elite_tumor.ELITE_POST_BATTLE_HEALTH_MULTIPLIER;
        elite.health = elite.maxHealth;
        elite.desc = I18n.T("[] However, this one appears to be less wild in nature, and calmer around people.", elite.desc);
        elite.sentience_type = SENTIENCE_ORGANIC;
        REMOVE_TRAIT(elite, TRAIT_UNCONVERTABLE, INNATE_TRAIT);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:402
public partial class obj_effect_temp_visual_elite_tumor_wall : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:419
    [DmParams("mapload", "new_caster")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
        {
            QUEUE_SMOOTH_NEIGHBORS(this);
            QUEUE_SMOOTH(this);
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:425
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH_NEIGHBORS(this);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/elite.dm:430
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (mover == ourelite_ref.resolve() || mover == activator_ref.resolve())
            return FALSE;
        return dot;
    }
}
