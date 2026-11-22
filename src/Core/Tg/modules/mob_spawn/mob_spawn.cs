// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob_spawn/mob_spawn.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the spawner and its ghost roles (the drone shell is a ghost role spawner). Gaps E2c (D327): the corpses (the corpse
// spawner, the human corpse and the cockroach spawner), so the troopers and the other mobs that drop one leave a body. The whole file. The
// var blocks are generated (D111).

// tg: code/modules/mob_spawn/mob_spawn.dm:1
public partial class obj_effect_mob_spawn : obj_effect
{
    // tg: code/modules/mob_spawn/mob_spawn.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(faction))
            faction = string_list(faction) as DmList;
        return dot;
    }

    // tg: code/modules/mob_spawn/mob_spawn.dm:42
    public override object Destroy(float? force = null)
    {
        spawned_mob_ref = null;
        // if(istype(outfit)) QDEL_NULL(outfit): the port's outfit var is a type until equip makes one (outfit_override), never kept
        return base.Destroy(force);
    }

    /**
     * Creates whatever mob the spawner makes.
     *
     * * mob_possessor - The ghost/mob that is possessing this mob, if applicable
     * * newname - A forced name for the mob, if applicable
     * * apply_prefs - Whether we should apply the possessor's preferences to the mob, if applicable
     *
     * Returns
     * - the created mob
     * - CANCEL_SPAWN if the spawn process should be stopped
     * - null if the spawn failed (and something went wrong)
     */
    // tg: code/modules/mob_spawn/mob_spawn.dm:61
    public virtual object create(mob mob_possessor = null, object newname = null, object apply_prefs = null)
    {
        mob_living spawned_mob;
        if (ispath(mob_type, typeof(mob_living_carbon_human)))
            spawned_mob = Dm.@new(mob_type, get_turf(this), get_mob_species(mob_possessor, apply_prefs)) as mob_living;
        else
            spawned_mob = Dm.@new(mob_type, get_turf(this)) as mob_living; //living mobs only
        special(spawned_mob, mob_possessor, apply_prefs);
        name_mob(spawned_mob, newname);
        equip(spawned_mob);
        spawned_mob_ref = WEAKREF(spawned_mob) as datum_weakref;
        return spawned_mob;
    }

    /// Returns the species typepath a human spawned by this spawner should be initialized with.
    // tg: code/modules/mob_spawn/mob_spawn.dm:76
    public virtual object get_mob_species(mob mob_possessor = null, object apply_prefs = null) => mob_species;

    /**
     * Any special behavior that needs to be done to the mob after it's created but before it's equipped.
     */
    // tg: code/modules/mob_spawn/mob_spawn.dm:86
    public virtual void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        if (Dm.truthy(faction))
            spawned_mob.set_faction(faction);
        if (spawned_mob is not mob_living_carbon_human spawned_human)
            return;
        Waiting.NoSleep((spawned_human.dna.species as datum_species).give_important_for_life(spawned_human)); // for preventing plasmamen from combusting immediately upon spawning
        spawned_human.underwear = "Nude";
        spawned_human.undershirt = "Nude";
        spawned_human.socks = "Nude";
        Waiting.NoSleep(randomize_human_normie(spawned_human));
        if (Dm.truthy(hairstyle))
            spawned_human.set_hairstyle(hairstyle, update: FALSE);
        if (Dm.truthy(facial_hairstyle))
            spawned_human.set_facial_hairstyle(facial_hairstyle, update: FALSE);
        if (Dm.truthy(haircolor))
            spawned_human.set_haircolor(haircolor, update: FALSE);
        if (Dm.truthy(facial_haircolor))
            spawned_human.set_facial_haircolor(facial_haircolor, update: FALSE);
        if (Dm.truthy(skin_tone))
            spawned_human.skin_tone = Dm.str(skin_tone);
        Waiting.NoSleep(spawned_human.update_body(TRUE)); // update_body(is_creating = TRUE)
    }

    // tg: code/modules/mob_spawn/mob_spawn.dm:110
    public virtual void name_mob(mob_living spawned_mob = null, object forced_name = null)
    {
        object chosen_name = null;
        //passed arguments on mob spawns are number one priority
        if (Dm.truthy(forced_name))
            chosen_name = forced_name;
        //then the mob name var
        else if (Dm.truthy(mob_name))
            chosen_name = mob_name;
        //then if no name was chosen the one the mob has by default works great
        if (!Dm.truthy(chosen_name))
            return;
        //not using an old name doesn't update records- but ghost roles don't have records so who cares
        Waiting.NoSleep(spawned_mob.fully_replace_character_name(null, chosen_name));
    }

    // tg: code/modules/mob_spawn/mob_spawn.dm:124
    public virtual void equip(mob_living spawned_mob = null)
    {
        if (outfit == null)
            return;
        var spawned_human = (mob_living_carbon_human)spawned_mob;
        object to_equip = outfit;
        if (Dm.truthy(outfit_override))
        {
            var made = (datum_outfit)Dm.@new(outfit); //create it now to apply vars
            foreach (object outfit_var in Dm.each(outfit_override))
            {
                object value = outfit_override[outfit_var];
                if (!ispath(value) && value != null)
                    throw Dm.CRASH(Dm.text("outfit_override var on [] spawner has incorrect values! it must be an assoc list with outfit \"var\" = path | null", mob_name), "code/modules/mob_spawn/mob_spawn.dm", 132);
                Dm.set_var(made, Dm.str(outfit_var), value);
            }
            to_equip = made;
        }
        Waiting.NoSleep(spawned_human.equipOutfit(to_equip));
    }
}

///these mob spawn subtypes do not trigger until attacked by a ghost.
// tg: code/modules/mob_spawn/mob_spawn.dm:135
public partial class obj_effect_mob_spawn_ghost_role : obj_effect_mob_spawn
{
    // tg: code/modules/mob_spawn/mob_spawn.dm:183  var/static/list/ckeys_trying_to_spawn
    private static DmList ckeys_trying_to_spawn
    {
        get => TgState.Current.obj_effect_mob_spawn_ghost_role__ckeys_trying_to_spawn;
        set => TgState.Current.obj_effect_mob_spawn_ghost_role__ckeys_trying_to_spawn = value;
    }

    // tg: code/modules/mob_spawn/mob_spawn.dm:170
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
        object key = format_text(name);
        if (GLOB.mob_spawners[key] is not DmList spawners)
            GLOB.mob_spawners[key] = spawners = Dm.list();
        spawners.Add(this);
        return dot;
    }

    // tg: code/modules/mob_spawn/mob_spawn.dm:175
    public override object Destroy(float? force = null)
    {
        object key = format_text(name);
        var spawners = GLOB.mob_spawners[key] as DmList;
        spawners?.Remove(this);
        if (Dm.length(spawners) == 0)
            GLOB.mob_spawners.Remove(key);
        return base.Destroy(force);
    }

    //ATTACK GHOST IGNORING PARENT RETURN VALUE
    // tg: code/modules/mob_spawn/mob_spawn.dm:183
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        if (!Dm.truthy(SSticker.HasRoundStarted()) || loc == null || QDELETED(this))
            return null;

        // Lazylist of the ckeys that currently are trying to access any spawner, so that they can't try to spawn more than once (in case there's sleeps).
        if (ckeys_trying_to_spawn != null && Dm.@in(user.ckey, ckeys_trying_to_spawn))
            return null;
        if (uses <= 0 && !Dm.truthy(infinite_use))
        {
            to_chat(user, span_warning(I18n.T("This spawner is out of charges!")));
            return FALSE;
        }
        if (!Dm.truthy(can_ghost_take(user)))
            return FALSE;

        uses -= 1; // Remove a use EARLY to account for sleep / inputs
        object user_ckey = user.ckey; // Just in case shenanigans happen, we always want to remove it from the list.
        (ckeys_trying_to_spawn ??= Dm.list()).Add(user_ckey);
        bool prompt_fail = false;
        object apply_prefs = FALSE;
        if (Dm.truthy(prompt_ghost))
        {
            string prompt = Dm.text("Become []?", prompt_name);
            if (!Dm.truthy(temp_body) && Dm.truthy(user.can_reenter_corpse) && user.mind != null)
                prompt += " (Warning, You can no longer be revived!)";
            prompt_fail = !Dm.eq(await tgui_alert(user, I18n.T(prompt), buttons: Dm.list("Yes", "No"), timeout: 10 * SECONDS), "Yes");
        }

        object species_pref = user.client?.prefs.read_preference(typeof(datum_preference_choiced_species));
        if (!Dm.truthy(species_pref))
            species_pref = typeof(datum_species_human);
        if (!prompt_fail && Dm.truthy(user.started_as_observer) && Dm.truthy(allow_custom_character)
            && Dm.eq((GLOB.species_prototypes[species_pref] as datum_species)?.get_breath_type(), GAS_O2))
        {
            string static_prompt = Dm.text("Because you haven't taken a role so far, you may spawn in as [] your customized character with a random name. Would you like to?",
                Dm.truthy(Dm.bitand(allow_custom_character, GHOSTROLE_TAKE_PREFS_SPECIES)) || Dm.eq(species_pref, typeof(datum_species_human)) ? "" : "a human version of");
            apply_prefs = Dm.eq(await tgui_alert(user, I18n.T(static_prompt), I18n.T("Custom Character"), Dm.list("Yes", "No"), 10 * SECONDS), "Yes") ? TRUE : FALSE;
        }

        if (!prompt_fail && !Dm.truthy(await pre_ghost_take(user)))
            prompt_fail = true;

        if (prompt_fail || !Dm.truthy(can_ghost_take(user)) || !Dm.truthy(create_from_ghost(user, apply_prefs, subtract_uses: FALSE)))
            uses += 1;
        ckeys_trying_to_spawn?.Remove(user_ckey);
        return null;
    }

    /// Allows for modifications before the ghost is turned into a mob.
    /// You can put sleeps or inputs in here, sanity checking is done for you after this proc returns.
    /// Returning FALSE will cancel the spawn process.
    // tg: code/modules/mob_spawn/mob_spawn.dm:224
    public virtual async DmTask<object> pre_ghost_take(mob_dead_observer user = null)
    {
        await DmTask.CompletedTask;
        return TRUE;
    }

    /// Checks if a ghost can take this ghost role.
    // tg: code/modules/mob_spawn/mob_spawn.dm:228
    public virtual object can_ghost_take(mob_dead_observer user = null)
    {
        if (Dm.truthy(is_banned_from(user.ckey, role_ban)))
        {
            to_chat(user, span_warning(I18n.T("You are banned from this role!")));
            return FALSE;
        }
        if (!Dm.truthy(Dm.bitand(GLOB.ghost_role_flags, GHOSTROLE_SPAWNER)) && !Dm.truthy(Dm.bitand(flags_1, ADMIN_SPAWNED_1)))
        {
            to_chat(user, span_warning(I18n.T("An admin has temporarily disabled non-admin ghost roles!")));
            return FALSE;
        }
        if (QDELETED(this) || QDELETED(user))
            return FALSE;
        if (!Dm.truthy(allow_spawn(user, silent: FALSE)))
            return FALSE;
        return TRUE;
    }

    /**
     * Uses a use and creates a mob from a passed ghost
     *
     * Does NOT validate that the spawn is possible or valid - assumes this has been done already!
     */
    // tg: code/modules/mob_spawn/mob_spawn.dm:256
    public object create_from_ghost(mob_dead_observer user = null, object apply_prefs = null, object subtract_uses = null)
    {
        subtract_uses ??= TRUE;
        if (user == null)
            throw Dm.CRASH("assertion failed: istype(user)", "code/modules/mob_spawn/mob_spawn.dm", 259);

        user.log_message(Dm.text("became a [].", prompt_name), LOG_GAME);
        if (!Dm.truthy(temp_body))
            user.mind = null; // dissassociate mind, don't let it follow us to the next life

        object created = create(user, apply_prefs: apply_prefs);
        if (ismob(created))
        {
            if (Dm.truthy(_listen_lookup?[COMSIG_GHOSTROLE_SPAWNED]))
                _SendSignal(COMSIG_GHOSTROLE_SPAWNED, Dm.list(this, created));
            if (Dm.truthy(subtract_uses))
                uses -= 1;
            check_uses();
        }
        else if (created == null) // null instead of explicit CANCEL_SPAWN means something went wrong
            throw Dm.CRASH(Dm.text("An instance of [] didn't return anything when creating a mob, this might be broken!", type), "code/modules/mob_spawn/mob_spawn.dm", 273);

        return created;
    }

    // tg: code/modules/mob_spawn/mob_spawn.dm:277
    public override object create(mob mob_possessor = null, object newname = null, object apply_prefs = null)
    {
        if (!Dm.truthy(mob_possessor.key)) // This is in the scenario that the server is somehow lagging, or someone fucked up their code, and we try to spawn the same person in twice. We'll simply not spawn anything and CRASH(), so that we report what happened.
            throw Dm.CRASH(Dm.text("Attempted to create an instance of [] with a mob that had no ckey attached to it, which isn't supported by ghost role spawners!", type), "code/modules/mob_spawn/mob_spawn.dm", 279);
        return base.create(mob_possessor, newname, apply_prefs);
    }

    // tg: code/modules/mob_spawn/mob_spawn.dm:283
    public override object get_mob_species(mob mob_possessor = null, object apply_prefs = null)
    {
        if (mob_possessor?.client != null && Dm.truthy(apply_prefs) && Dm.truthy(Dm.bitand(allow_custom_character, GHOSTROLE_TAKE_PREFS_SPECIES)))
            return mob_possessor.client.prefs.read_preference(typeof(datum_preference_choiced_species));
        return base.get_mob_species(mob_possessor, apply_prefs);
    }

    // tg: code/modules/mob_spawn/mob_spawn.dm:288
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        if (mob_possessor != null)
        {
            if (mob_possessor.client != null && Dm.truthy(apply_prefs) && Dm.truthy(allow_custom_character) && spawned_mob is mob_living_carbon_human spawned_human)
            {
                if (Dm.truthy(Dm.bitand(allow_custom_character, GHOSTROLE_TAKE_PREFS_APPEARANCE)))
                    mob_possessor.client.prefs.apply_prefs_to(spawned_human, icon_updates: TRUE, do_not_apply: Dm.add(typesof(typeof(datum_preference_name)), typesof(typeof(datum_preference_choiced_species))) as DmList);
                if (Dm.truthy(Dm.bitand(allow_custom_character, GHOSTROLE_TAKE_PREFS_SPECIES)))
                    Waiting.NoSleep(spawned_human.fully_replace_character_name(spawned_human.real_name, spawned_human.generate_random_mob_name()));
            }
            if (mob_possessor.mind != null)
                Waiting.NoSleep(mob_possessor.mind.transfer_to(spawned_mob, force_key_move: TRUE));
            else
                spawned_mob.PossessByPlayer(mob_possessor.key);
        }

        datum_mind spawned_mind = spawned_mob.mind;
        if (spawned_mind != null)
        {
            spawned_mob.mind.set_assigned_role_with_greeting(SSjob.get_job_type(spawner_job_path) as datum_job);
            spawned_mind.name = spawned_mob.real_name;
        }
        if (Dm.truthy(show_flavor))
        {
            string output_message = span_infoplain(Dm.text("<span class='big bold'>[]</span>", you_are_text));
            if (flavour_text != "")
                output_message += Dm.text("\n<span class='infoplain'><b>[]</b></span>", flavour_text);
            if (important_text != "")
                output_message += "\n" + span_userdanger(important_text);

            to_chat(spawned_mob, boxed_message(output_message), type: MESSAGE_TYPE_INFO);
        }
    }

    /// Checks if the spawner has zero uses left, if so, delete yourself... NOW!
    // tg: code/modules/mob_spawn/mob_spawn.dm:317
    public virtual void check_uses()
    {
        if (uses == 0 && Dm.truthy(deletes_on_zero_uses_left))
            qdel(this);
    }

    ///override this to add special spawn conditions to a ghost role
    // tg: code/modules/mob_spawn/mob_spawn.dm:322
    public virtual object allow_spawn(mob user = null, object silent = null) => TRUE;
}

// tg: code/modules/mob_spawn/mob_spawn.dm:324
public partial class obj_effect_mob_spawn_corpse : obj_effect_mob_spawn
{
    // tg: code/modules/mob_spawn/mob_spawn.dm:343
    [DmParams("mapload", "no_spawn")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(Dm.arg(__extra, 0))) // no_spawn
            return dot;
        // INVOKE_ASYNC(src, PROC_REF(create)): create runs at once and does not sleep
        if (spawn_when == CORPSE_INSTANT)
            create();
        else if (spawn_when == CORPSE_ROUNDSTART)
        {
            if (Dm.truthy(mapload) || (SSticker != null && Dm.num(SSticker.current_state) > GAME_STATE_SETTING_UP))
                create();
        }
        return dot;
    }

    // tg: code/modules/mob_spawn/mob_spawn.dm:354
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        Waiting.NoSleep(spawned_mob.death(TRUE));
        Waiting.NoSleep(spawned_mob.adjust_oxy_loss(oxy_damage));
        Waiting.NoSleep(spawned_mob.adjust_brute_loss(brute_damage));
        Waiting.NoSleep(spawned_mob.adjust_fire_loss(burn_damage));
        if (Dm.truthy(corpse_description))
            Waiting.NoSleep(spawned_mob.AddComponent(typeof(datum_component_temporary_description), corpse_description, naive_corpse_description));
    }

    // tg: code/modules/mob_spawn/mob_spawn.dm:363
    public override object create(mob mob_possessor = null, object newname = null, object apply_prefs = null)
    {
        object dot = base.create(mob_possessor, newname, apply_prefs);
        qdel(this);
        return dot;
    }
}

//almost all mob spawns in this game, dead or living, are human. so voila
// tg: code/modules/mob_spawn/mob_spawn.dm:375
public partial class obj_effect_mob_spawn_corpse_human : obj_effect_mob_spawn_corpse
{
    // tg: code/modules/mob_spawn/mob_spawn.dm:383
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        var spawned_human = (mob_living_carbon_human)spawned_mob;
        if (Dm.truthy(husk))
            spawned_human.Drain();
        else //Because for some reason I can't track down, things are getting turned into husks even if husk = false. It's in some damage proc somewhere.
            Waiting.NoSleep(spawned_human.cure_husk());
        spawned_human.job = name;
    }

    // tg: code/modules/mob_spawn/mob_spawn.dm:391
    public override void equip(mob_living spawned_mob = null)
    {
        base.equip(spawned_mob);
        var spawned_human = (mob_living_carbon_human)spawned_mob;
        if (Dm.truthy(conceal_presence))
        {
            // We don't want corpse PDAs to show up in the messenger list.
            var messenger = Dm.locate<obj_item_modular_computer_pda>(spawned_human);
            if (messenger != null)
            {
                var message_app = Dm.locate<datum_computer_file_program_messenger>(messenger.stored_files);
                if (message_app != null)
                    message_app.invisible = TRUE;
            }
            // Or on crew monitors
            if (spawned_human.w_uniform is obj_item_clothing_under sensor_clothes)
                sensor_clothes.set_sensor_mode(SENSOR_OFF);
        }
    }
}

// tg: code/modules/mob_spawn/mob_spawn.dm:409
public partial class obj_effect_mob_spawn_cockroach : obj_effect_mob_spawn
{
    // tg: code/modules/mob_spawn/mob_spawn.dm:415
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (prob(bloodroach_chance))
            mob_type = typeof(mob_living_basic_cockroach_bloodroach);
        object dot = base.Initialize(mapload, __extra);
        create(); // INVOKE_ASYNC(src, PROC_REF(create)): create does not sleep
        qdel(this);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob_spawn/mob_spawn.dm:183  /obj/effect/mob_spawn/ghost_role/attack_ghost's var/static/list/ckeys_trying_to_spawn
    internal DmList obj_effect_mob_spawn_ghost_role__ckeys_trying_to_spawn;
}
