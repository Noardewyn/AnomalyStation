// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// Divine vocal cords

// tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:3
public partial class obj_item_organ_vocal_cords_colossus : obj_item_organ_vocal_cords
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:56
    public override object can_speak_with()
    {
        if (owner == null)
            return FALSE;

        if (world.time < next_command)
        {
            to_chat(owner, span_notice(I18n.T("You must wait [] before Speaking again.", DisplayTimeText(next_command - world.time))));
            return FALSE;
        }

        return owner.can_speak__proc();
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:66
    public override async DmTask handle_speech(object message = null)
    {
        playsound(get_turf(owner), Dm.resource("sound/effects/magic/clockwork/invoke_general.ogg"), 300, TRUE, 5);
        return; //voice of god speaks for us
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:70
    public override async DmTask speak_with(object message = null)
    {
        object cooldown = await voice_of_god(uppertext(message), owner, spans, base_multiplier);
        next_command = world.time + (Dm.num(cooldown) * cooldown_mod);
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:13
public partial class datum_action_item_action_organ_action_colossus : datum_action_item_action_organ_action
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:17
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        cords = target as obj_item_organ_vocal_cords_colossus;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:21
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        if (owner == null)
            return FALSE;
        if (world.time < cords.next_command)
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, Dm.text("wait []!", DisplayTimeText(cords.next_command - world.time)));
            return FALSE;
        }
        if (isliving(owner))
        {
            mob_living living = owner as mob_living;
            if (!Dm.truthy(living.can_speak__proc()))
            {
                if (Dm.truthy(feedback))
                    owner.balloon_alert(owner, "can't speak!");
                return FALSE;
            }
        }
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_CONSCIOUS)) && IS_UNCONSCIOUS_OR_CRIT(owner))
        {
            if (Dm.truthy(feedback))
            {
                if (owner.stat == DEAD)
                    owner.balloon_alert(owner, "dead!");
                else if (IS_UNCONSCIOUS(owner))
                    owner.balloon_alert(owner, "unconscious!");
                else
                    owner.balloon_alert(owner, "in critical!");
            }
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:45
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        object command = await tgui_input_text(owner, "Speak with the Voice of God", "Command", max_length: MAX_MESSAGE_LEN);
        if (!Dm.truthy(command))
            return FALSE;
        if (QDELETED(this) || QDELETED(owner))
            return FALSE;
        await owner.say(Dm.text(".x[]", command));
        return TRUE;
    }
}

// Anomalous crystal

// tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:84
public partial class obj_machinery_anomalous_crystal : obj_machinery
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:75-83 (file-local defines)
    protected const string ACTIVATE_TOUCH = "touch";
    protected const string ACTIVATE_SPEECH = "speech";
    protected const string ACTIVATE_HEAT = "heat";
    protected const string ACTIVATE_BULLET = "bullet";
    protected const string ACTIVATE_ENERGY = "energy";
    protected const string ACTIVATE_BOMB = "bomb";
    protected const string ACTIVATE_MOB_BUMP = "bumping";
    protected const string ACTIVATE_WEAPON = "weapon";
    protected const string ACTIVATE_MAGIC = "magic";

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:107
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(activation_method))
            activation_method = pick(possible_methods);
        become_hearing_sensitive(trait_source: ROUNDSTART_TRAIT);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:113
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (isobserver(user))
        {
            Dm.append(ref dot, observer_desc);
            Dm.append(ref dot, I18n.T("It is activated by [].", activation_method));
        }
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:119
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        object dot = await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (isliving(speaker))
            await ActivationReaction(speaker as mob, ACTIVATE_SPEECH);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:124
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        await ActivationReaction(user, ACTIVATE_TOUCH);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:130
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(attacking_item.get_temperature()))
            await ActivationReaction(user, ACTIVATE_HEAT);
        else
            await ActivationReaction(user, ACTIVATE_WEAPON);
        await base.attackby(attacking_item, user, modifiers, attack_modifiers);
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:137
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        obj_projectile proj = hitting_projectile;
        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (proj is obj_projectile_magic)
        {
            await ActivationReaction(proj.firer as mob, ACTIVATE_MAGIC, proj.damage_type);
            return dot;
        }
        await ActivationReaction(proj.firer as mob, proj.armor_flag, proj.damage_type);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:144
    public virtual async DmTask<object> ActivationReaction(mob user = null, object method = null, object damtype = null)
    {
        if (!(cooldown_timer <= world.time)) // COOLDOWN_FINISHED(src, cooldown_timer), inline
            return FALSE;
        if (Dm.truthy(activation_damage_type) && !Dm.eq(activation_damage_type, damtype))
            return FALSE;
        if (!Dm.eq(method, activation_method))
            return FALSE;
        if (Dm.truthy(active))
            return FALSE;
        if (Dm.truthy(use_time))
            await charge_animation();
        cooldown_timer = world.time + cooldown_add; // COOLDOWN_START(src, cooldown_timer, cooldown_add)
        playsound(user, activation_sound, 100, TRUE);
        log_game(Dm.text("[] activated by [] in []. The last fingerprints on the [] was [].", this, key_name(user), AREACOORD(this), this, fingerprintslast));
        return TRUE;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:159
    public virtual async DmTask<object> charge_animation()
    {
        icon_state = "anomaly_crystal_charging";
        active = TRUE;
        await set_anchored(TRUE);
        balloon_alert_to_viewers("charging...");
        playsound(this, Dm.resource("sound/effects/magic/disable_tech.ogg"), 50, TRUE);
        await sleep(use_time);
        icon_state = Dm.initial(this, (atom p) => p.icon_state);
        active = FALSE;
        await set_anchored(FALSE);
        return TRUE;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:171
    public override object Bumped(atom_movable bumped_atom = null)
    {
        base.Bumped(bumped_atom);
        if (ismob(bumped_atom))
            Waiting.NoSleep(ActivationReaction(bumped_atom as mob, ACTIVATE_MOB_BUMP));
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:176
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        Waiting.NoSleep(ActivationReaction(null, ACTIVATE_BOMB));
        return TRUE;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:180
public partial class obj_machinery_anomalous_crystal_honk : obj_machinery_anomalous_crystal //Revives the dead, but strips and equips them as a clown
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:189
    public override async DmTask<object> ActivationReaction(mob user = null, object method = null, object damtype = null)
    {
        object dot = await base.ActivationReaction(user, method, damtype);
        if (!Dm.truthy(dot))
            return FALSE;

        foreach (object thing__it in Dm.each(range(1, this)))
        {
            atom thing = (atom)thing__it;
            if (isturf(thing))
            {
                Dm.@new<obj_effect_decal_cleanable_confetti>(thing);
                continue;
            }

            if (!ishuman(thing))
                continue;

            mob_living_carbon_human new_clown = thing as mob_living_carbon_human;

            if (new_clown.stat != DEAD)
                continue;

            await new_clown.revive(ADMIN_HEAL_ALL, force_grab_ghost: TRUE);

            string clown_ref = Dm.str(REF(new_clown));
            if (Dm.@in(clown_ref, clowned_mob_refs)) //one clowning per person
                continue;

            foreach (obj_item to_strip in Dm.each<obj_item>(new_clown.get_equipped_items()))
                await new_clown.dropItemToGround(to_strip);
            await new_clown.dress_up_as_job(SSjob.get_job_type(typeof(datum_job_clown)) as datum_job);
            Dm.append(ref clowned_mob_refs, clown_ref);
        }

        return TRUE;
    }
}

/// Transforms the area to look like a new one
// tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:222
public partial class obj_machinery_anomalous_crystal_theme_warp : obj_machinery_anomalous_crystal
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:231
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        terrain_theme = SSmaterials.dimensional_themes[pick(subtypesof(typeof(datum_dimension_theme)))] as datum_dimension_theme;
        observer_desc = I18n.T("This crystal changes the area around it to match the theme of \"[]\".", terrain_theme.name);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:236
    public override async DmTask<object> ActivationReaction(mob user = null, object method = null, object damtype = null)
    {
        object dot = await base.ActivationReaction(user, method, damtype);
        if (!Dm.truthy(dot))
            return FALSE;
        area current_area = get_area(this) as area;
        if (Dm.@in(current_area, converted_areas))
            return FALSE;
        await terrain_theme.apply_theme_to_list_of_turfs(current_area.get_turfs_from_all_zlevels() as DmList);
        Dm.append(ref converted_areas, current_area);
        return TRUE;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:246
    public override object Destroy(float? force = null)
    {
        terrain_theme = null;
        converted_areas.Cut();
        return base.Destroy(force);
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:251
public partial class obj_machinery_anomalous_crystal_emitter : obj_machinery_anomalous_crystal //Generates a projectile when interacted with
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:258
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        observer_desc = I18n.T("This crystal generates a [] when activated.", Dm.initial(generated_projectile, (atom p) => p.name));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:262
    public override async DmTask<object> ActivationReaction(mob user = null, object method = null, object damtype = null)
    {
        if (Dm.truthy(await base.ActivationReaction(user, method, damtype)))
        {
            obj_projectile proj = Dm.@new(generated_projectile, get_turf(this)) as obj_projectile;
            proj.firer = this;
            await proj.fire(dir2angle(dir));
        }
        return null;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:268
public partial class obj_machinery_anomalous_crystal_dark_reprise : obj_machinery_anomalous_crystal //Revives anyone nearby, but turns them into shadowpeople and renders them uncloneable, so the crystal is your only hope of getting up again if you go down.
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:274
    public override async DmTask<object> ActivationReaction(mob user = null, object method = null, object damtype = null)
    {
        object dot = await base.ActivationReaction(user, method, damtype);
        if (!Dm.truthy(dot))
            return FALSE;

        foreach (object thing__it in Dm.each(range(1, this)))
        {
            atom thing = (atom)thing__it;
            if (isturf(thing))
            {
                Dm.@new<obj_effect_temp_visual_cult_sparks>(thing);
                continue;
            }

            if (!ishuman(thing))
                continue;

            mob_living_carbon_human to_revive = thing as mob_living_carbon_human;

            if (to_revive.stat != DEAD)
                continue;

            to_revive.set_species(typeof(datum_species_shadow), TRUE);
            await to_revive.revive(ADMIN_HEAL_ALL, force_grab_ghost: TRUE);
            //Free revives, but significantly limits your options for reviving except via the crystal
            //except JK who cares about BADDNA anymore. this even heals suicides.
            ADD_TRAIT(to_revive, TRAIT_BADDNA, MAGIC_TRAIT);
        }

        return TRUE;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:300
public partial class obj_machinery_anomalous_crystal_helpers : obj_machinery_anomalous_crystal //Lets ghost spawn as helpful creatures that can only heal people slightly. Incredibly fragile and they can't converse with humans
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:307
    public override async DmTask<object> ActivationReaction(mob user = null, object method = null, object damtype = null)
    {
        if (Dm.truthy(await base.ActivationReaction(user, method, damtype)) && !Dm.truthy(ready_to_deploy))
        {
            Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
            ready_to_deploy = TRUE;
            notify_ghosts(
                I18n.T("An anomalous crystal has been activated in []! This crystal can always be used by ghosts hereafter.", get_area(this)),
                source: this,
                header: "Anomalous crystal activated",
                click_interact: TRUE,
                ghost_sound: Dm.resource("sound/effects/ghost2.ogg")
            );
        }
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:319
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        object dot = await base.attack_ghost(user);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(ready_to_deploy))
        {
            object be_helper = await tgui_alert(usr, I18n.T("Become a Lightgeist? (Warning, You can no longer be revived!)"), I18n.T("Lightgeist Deployment"), Dm.list("Yes", "No"));
            if (Dm.eq(be_helper, "Yes") && !QDELETED(this) && isobserver(user))
            {
                mob_living_basic_lightgeist deployable = Dm.@new<mob_living_basic_lightgeist>(get_turf(loc));
                deployable.PossessByPlayer(user.key);
            }
        }
        return dot;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:329
public partial class obj_machinery_anomalous_crystal_possessor : obj_machinery_anomalous_crystal //Allows you to bodyjack small animals, then exit them at your leisure, but you can only do this once per activation. Because they blow up. Also, if the bodyjacked animal dies, SO DO YOU.
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:334
    public override async DmTask<object> ActivationReaction(mob user = null, object method = null, object damtype = null)
    {
        object dot = await base.ActivationReaction(user, method, damtype);
        if (!Dm.truthy(dot) || !ishuman(user))
            return FALSE;

        DmList valid_mobs = Dm.list();
        DmList nearby_things = range(1, this);
        foreach (mob_living_basic possible_mob in Dm.each<mob_living_basic>(nearby_things))
        {
            if (!Dm.truthy(is_valid_animal(possible_mob)))
                continue;
            Dm.append(ref valid_mobs, possible_mob);
        }
        foreach (mob_living_simple_animal possible_mob in Dm.each<mob_living_simple_animal>(nearby_things))
        {
            if (!Dm.truthy(is_valid_animal(possible_mob)))
                continue;
            Dm.append(ref valid_mobs, possible_mob);
        }

        if (!Dm.truthy(length(valid_mobs))) //Just in case there aren't any animals on the station, this will leave you with a terrible option to possess if you feel like it //i found it funny that in the file for a giant angel beast theres a cockroach
        {
            Dm.@new<mob_living_basic_cockroach_bloodroach>(get_step(this, dir));
            return null;
        }

        mob_living picked_mob = pick(valid_mobs) as mob_living;
        obj_structure_closet_stasis possessor_container = Dm.@new<obj_structure_closet_stasis>(picked_mob);
        await user.forceMove(possessor_container);
        return dot;
    }

    /// Returns true if this is a mob you're allowed to possess
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:361
    public virtual object is_valid_animal(mob_living check_mob = null)
    {
        return Dm.num(check_mob.stat != DEAD && !Dm.truthy(check_mob.ckey) && check_mob.mob_size < MOB_SIZE_LARGE && Dm.num(check_mob.melee_damage_upper) <= 5);
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:365
public partial class obj_structure_closet_stasis : obj_structure_closet
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:375
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (isanimal_or_basicmob(loc))
        {
            holder_animal = loc as mob_living;
            RegisterSignal(holder_animal, COMSIG_LIVING_DEATH, nameof(on_holder_animal_death));
        }
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_ALL, EMP_NO_EXAMINE)));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:382
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (isliving(arrived) && holder_animal != null)
        {
            mob_living possessor = arrived as mob_living;
            possessor.add_traits(Dm.list(TRAIT_UNDENSE, TRAIT_NO_TRANSFORM, TRAIT_GODMODE), STASIS_MUTE);
            await possessor.mind.transfer_to(holder_animal);
            datum_action_exit_possession escape = Dm.@new<datum_action_exit_possession>(holder_animal);
            await escape.Grant(holder_animal);
        }
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:391
    [DmParams("kill")]
    public override async DmTask dump_contents(params object[] __extra)
    {
        object kill = Dm.arg(__extra, 0) ?? TRUE;
        foreach (mob_living possessor in Dm.each<mob_living>(this))
        {
            possessor.remove_traits(Dm.list(TRAIT_UNDENSE, TRAIT_NO_TRANSFORM, TRAIT_GODMODE), STASIS_MUTE);
            if (Dm.truthy(kill) || !isanimal_or_basicmob(loc))
            {
                possessor.investigate_log(Dm.text("has died from [].", this), INVESTIGATE_DEATHS);
                await possessor.death(FALSE);
            }
            if (holder_animal != null)
            {
                await holder_animal.mind.transfer_to(possessor);
                possessor.mind.grab_ghost(force: TRUE);
                await possessor.forceMove(get_turf(holder_animal));
                holder_animal.investigate_log(Dm.text("has been gibbed by [].", this), INVESTIGATE_DEATHS);
                await holder_animal.gib(DROP_ALL_REMAINS);
                await base.dump_contents(__extra);
                return;
            }
        }
        await base.dump_contents(__extra);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:407
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }

    ///When our host animal dies in any way, we empty the stasis closet out.
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:411
    [SignalHandler]
    public virtual void on_holder_animal_death()
    {
        Waiting.NoSleep(dump_contents());
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:415
public partial class datum_action_exit_possession : datum_action
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:421
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        return Dm.num(Dm.truthy(await base.IsAvailable(feedback)) && isfloorturf(owner.loc));
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm:424
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return FALSE;

        obj_structure_closet_stasis stasis = Dm.locate<obj_structure_closet_stasis>(owner);
        if (stasis == null)
            throw Dm.CRASH(Dm.text("[] did not find a stasis closet thing in the owner.", type), "code/modules/mining/lavaland/mining_loot/megafauna/colossus.dm", 431);

        await stasis.dump_contents(FALSE);
        qdel(stasis);
        qdel(this);
        return dot;
    }
}
