// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/burdened/psyker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the psyker (the head and brain of a burdened chaplain at burden nine, slow_psykerize and psykerize), the null rod's
// transmogrification into the chaplain's revolver with its prayer refill and lucky bullets, and the psychic spells (projection,
// booster, wall). The psyker brain's echolocation waits for the view's own decision (not-translated.md section 244). The var blocks
// are generated (D111).

// tg: code/modules/religion/burdened/psyker.dm:1
public partial class obj_item_organ_brain_psyker : obj_item_organ_brain
{
    // tg: code/modules/religion/burdened/psyker.dm:18
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon inserted_into = organ_owner;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        // echolocation_component = WEAKREF(inserted_into.AddComponent(/datum/component/echolocation, echo_icon = "psyker")): the blind
        // psyker's sight by echoes waits for how a first-person view shows it (not-translated.md section 244)
        Dm.Deferred("code/modules/religion/burdened/psyker.dm:20", "on_mob_insert: the echolocation component (blindness and the echo images), the first-person view's own decision");
        antimagic_component = WEAKREF(await inserted_into.AddComponent(typeof(datum_component_anti_magic), Dm.named("antimagic_flags", MAGIC_RESISTANCE_MIND)) as datum) as datum_weakref;
        return dot;
    }

    // tg: code/modules/religion/burdened/psyker.dm:23
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        qdel(echolocation_component);
        qdel(antimagic_component);
    }

    // tg: code/modules/religion/burdened/psyker.dm:28
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        obj_item_bodypart_head_psyker psyker_head = owner.get_bodypart(zone) as obj_item_bodypart_head_psyker;
        if (psyker_head is obj_item_bodypart_head_psyker)
            return;
        if (!SPT_PROB(2, seconds_per_tick))
            return;
        to_chat(owner, span_userdanger(I18n.T("Your head hurts... It can't fit your brain!")));
        owner.adjust_disgust(33 * Dm.num(seconds_per_tick));
        await apply_organ_damage(5 * Dm.num(seconds_per_tick), 199);
    }
}

// tg: code/modules/religion/burdened/psyker.dm:39
public partial class obj_item_bodypart_head_psyker : obj_item_bodypart_head
{
    // tg: code/modules/religion/burdened/psyker.dm:46
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_DISFIGURED, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/religion/burdened/psyker.dm:51 (interleaved types, rules 1.9)
public partial class mob_living_carbon_human : mob_living_carbon
{
    /// flavorful variant of psykerizing that deals damage and sends messages before calling psykerize()
    // tg: code/modules/religion/burdened/psyker.dm:51
    public virtual async DmTask slow_psykerize()
    {
        if (Dm.eq(stat, DEAD) || get_bodypart(BODY_ZONE_HEAD) == null || get_bodypart(BODY_ZONE_HEAD) is obj_item_bodypart_head_psyker)
            return;
        to_chat(this, span_userdanger(I18n.T("You feel unwell...")));
        await sleep(5 * SECONDS);
        if (Dm.eq(stat, DEAD) || get_bodypart(BODY_ZONE_HEAD) == null)
            return;
        to_chat(this, span_userdanger(I18n.T("You feel your skin ripping off!")));
        await emote("scream");
        await apply_damage(30, BRUTE, BODY_ZONE_HEAD);
        await sleep(5 * SECONDS);
        if (!Dm.truthy(await psykerize()))
        {
            to_chat(this, span_warning(I18n.T("The transformation subsides...")));
            return;
        }
        await apply_damage(50, BRUTE, BODY_ZONE_HEAD);
        to_chat(this, span_userdanger(I18n.T("Your head splits open! Your brain mutates!")));
        Dm.@new<obj_effect_gibspawner_generic>(drop_location(), this, get_blood_dna_list());
        await emote("scream");
    }

    /// Proc with no side effects that turns someone into a psyker. returns FALSE if it could not psykerize.
    // tg: code/modules/religion/burdened/psyker.dm:71
    public virtual async DmTask<object> psykerize()
    {
        obj_item_bodypart_head old_head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        obj_item_organ_brain old_brain = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        obj_item_organ old_eyes = get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ;
        if (Dm.eq(stat, DEAD) || old_head == null || old_brain == null)
            return FALSE;
        obj_item_bodypart_head_psyker psyker_head = Dm.@new<obj_item_bodypart_head_psyker>();
        if (!Dm.truthy(await psyker_head.replace_limb(this)))
            return FALSE;
        psyker_head.species_id = (dna?.species as datum_species)?.id;
        qdel(old_head);
        obj_item_organ_brain_psyker psyker_brain = Dm.@new<obj_item_organ_brain_psyker>(); /// turns out if you make a flashing monochromatic outline against black background that refreshes on inconsistant intervals, it hurts peoples eyes. Who'da thunk.
        await old_brain.before_organ_replacement(psyker_brain);
        await old_brain.Remove(this, special: TRUE, movement_flags: NO_ID_TRANSFER);
        qdel(old_brain);
        await psyker_brain.Insert(this, special: TRUE, movement_flags: DELETE_IF_REPLACED);
        if (old_eyes != null)
            qdel(old_eyes);
        return TRUE;
    }
}

// tg: code/modules/religion/burdened/psyker.dm:91
public partial class datum_religion_rites_nullrod_transformation : datum_religion_rites
{
    // tg: code/modules/religion/burdened/psyker.dm:98
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        if (!ishuman(user))
            return FALSE;
        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        datum_brain_trauma_special_burdened burden = human_user.has_trauma_type(typeof(datum_brain_trauma_special_burdened)) as datum_brain_trauma_special_burdened;
        if (burden == null || burden.burden_level < 9)
        {
            to_chat(human_user, span_warning(I18n.T("You aren't burdened enough.")));
            return FALSE;
        }
        foreach (obj_item possible_rod in Dm.each<obj_item>(get_turf(religious_tool)))
        {
            if (HAS_TRAIT(possible_rod, TRAIT_NULLROD_ITEM))
            {
                transformation_target = possible_rod as obj_item_nullrod;
                return await base.perform_rite(user, religious_tool);
            }
        }
        to_chat(human_user, span_warning(I18n.T("You need to place a null rod on [] to do this!", religious_tool)));
        return FALSE;
    }

    // tg: code/modules/religion/burdened/psyker.dm:113
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        obj_item null_rod = transformation_target;
        transformation_target = null;
        if (QDELETED(null_rod) || null_rod.loc != get_turf(religious_tool))
        {
            to_chat(user, span_warning(I18n.T("Your target left the altar!")));
            return FALSE;
        }
        to_chat(user, span_warning(I18n.T("[] turns into a gun!", null_rod)));
        await user.emote("smile");
        qdel(null_rod);
        Dm.@new<obj_item_gun_ballistic_revolver_chaplain>(get_turf(religious_tool));
        return TRUE;
    }
}

// tg: code/modules/religion/burdened/psyker.dm:126
public partial class obj_item_gun_ballistic_revolver_chaplain : obj_item_gun_ballistic_revolver
{
    /// List of all possible names and descriptions.
    // tg: code/modules/religion/burdened/psyker.dm:140  var/static/list/possible_names
    public static DmList possible_names => TgState.Current.obj_item_gun_ballistic_revolver_chaplain__possible_names ??= Dm.list(
        Dm.kv("Requiescat", "May they rest in peace."),
        Dm.kv("Requiem", "They will never reach truth."),
        Dm.kv("Vade Retro", "Having a gun might make exorcisms more effective, who knows?"),
        Dm.kv("Extra Nos", "Salvation is given externally."),
        Dm.kv("Ordo Salutis", "First step? Fire."),
        Dm.kv("Absolution", "Free of your sins."),
        Dm.kv("Rod of God", "Splitting the red sea again."),
        Dm.kv("Holy Grail", "You found it!"),
        Dm.kv("Burning Bush", "Useful for any burning ambush."),
        Dm.kv("Judgement", "First of all, damn. Alpha much? Dude, so cool, and so are you! Strong, too!"),
        Dm.kv("Paradiso", "A divine end to the comedy of life."),
        Dm.kv("DVNO", "Don't need to ask my name to figure out how cool I am."),
        Dm.kv("Venus Supermax", "Did you know nearly everyone working and living on Venus is involved in sulfur extraction? Quite fitting for this weapon of gunpowder."),
        Dm.kv("Nirvana", "The giver of quietude, freedom, and highest happiness."),
        Dm.kv("Cerebrum Dispersio", "Latin for \"brain splitting\". How fitting."),
        Dm.kv("Ultimort", "Your hope dies last."),
        Dm.kv("Lifelight", "No escape, no greater fate to be made."),
        Dm.kv("Bendbreaker", "FRAGILE: Please do not bend or break."),
        Dm.kv("Pop Pop", "The name referring to an onomatopeia (phonetic imitation) of a gun firing."),
        Dm.kv("Justice", "Justice is Splendor."),
        Dm.kv("Splendor", "Splendor is Justice."),
        Dm.kv("Revelation", "Awaken your faith."),
        Dm.kv("New Safety M62", "This model of firearm is popular hundreds of years later due to masculine associations created by the film industry."),
        Dm.kv("Unmaker", "What the !@#%* is this!"),
        Dm.kv("INKVD", "Savior of the soul and fighter against dirty thoughts."),
        Dm.kv("Life Leech", "An artifact said to draw its power from the life energy of others."),
        Dm.kv("Nullray", "Starless metal on the barrel imbibes light and routes it to the null place. The grip acrylic is patterned after ley lines."),
        Dm.kv("Mortis", "Put your faith into this weapon working."),
        Dm.kv("Ramiel", "Literally meaning \"God has thundered\". You could even interpret the gunshot as a thunder."),
        Dm.kv("Daredevil", "Hey now, you won't be reckless with this, will you?"),
        Dm.kv("Lacytanga", "Rules are written by the strong."),
        Dm.kv("A10", "The fist of God. Keep away from the terrible."),
        Dm.kv("Lucky", "Ain't that a kick in the head?"));

    // tg: code/modules/religion/burdened/psyker.dm:176
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_nullrod_core), FALSE));
        name = (string)pick(possible_names);
        desc = (string)possible_names[name];
        return dot;
    }

    // tg: code/modules/religion/burdened/psyker.dm:182
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        object dot = await base.suicide_act(user);
        name = I18n.T("Habemus Papam");
        desc = I18n.T("I announce to you a great joy.");
        return dot;
    }

    // tg: code/modules/religion/burdened/psyker.dm:187
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await pray_refill(user as mob_living_carbon_human);
        return null;
    }

    // tg: code/modules/religion/burdened/psyker.dm:190
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (isammocasing(tool) || tool is obj_item_ammo_box)
        {
            user.balloon_alert(user, I18n.T("no manual reloads!"));
            return ITEM_INTERACT_BLOCKING;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/religion/burdened/psyker.dm:197
    public virtual async DmTask pray_refill(mob_living_carbon_human user = null)
    {
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)) || !(user is mob_living_carbon_human))
            return;
        datum_brain_trauma_special_burdened burden = user.has_trauma_type(typeof(datum_brain_trauma_special_burdened)) as datum_brain_trauma_special_burdened;
        if (Dm.truthy(needs_burden) && (burden == null || burden.burden_level < 9))
        {
            to_chat(user, span_warning(I18n.T("You aren't burdened enough.")));
            return;
        }
        user.manual_emote(I18n.T("presses [] palms together...", user.p_their()));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return;
        }
        await user.say(I18n.T("#Oh great [], give me the ammunition I need!", GLOB.deity), forced: "ammo prayer");
        magazine.top_off();
        user.playsound_local(get_turf(this), Dm.resource("sound/effects/magic/magic_block_holy.ogg"), 50, TRUE);
        chamber_round();
    }
}

// tg: code/modules/religion/burdened/psyker.dm:230
public partial class obj_projectile_bullet_c38_holy : obj_projectile_bullet_c38
{
    // tg: code/modules/religion/burdened/psyker.dm:239
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        float roll_them_bones = rand(1, 38);
        if (roll_them_bones == 1 && isliving(target))
        {
            playsound(target, Dm.resource("sound/machines/synth/synth_yes.ogg"), 50, TRUE);
            playsound(target, pick(Dm.list(Dm.resource("sound/machines/coindrop.ogg"), Dm.resource("sound/machines/coindrop2.ogg"))), 40, TRUE);
            Dm.@new<obj_effect_temp_visual_crit>(get_turf(target));
        }
        return dot;
    }
}

// tg: code/modules/religion/burdened/psyker.dm:247
public partial class datum_action_cooldown_spell_pointed_psychic_projection : datum_action_cooldown_spell_pointed
{
    // tg: code/modules/religion/burdened/psyker.dm:263
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        object dot = null;
        dot = await base.is_valid_target(cast_on);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!isliving(cast_on))
            return FALSE;
        mob_living living_target = cast_on as mob_living;
        return Dm.num(living_target.has_status_effect(typeof(datum_status_effect_psychic_projection)) == null);
    }

    // tg: code/modules/religion/burdened/psyker.dm:272
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = null;
        dot = await base.cast(cast_on);
        mob_living cast_on_living = cast_on as mob_living;
        if (Dm.truthy(cast_on_living.can_block_magic(antimagic_flags)))
        {
            to_chat(cast_on_living, span_notice(I18n.T("Your mind feels weird, but it passes momentarily.")));
            to_chat(owner, span_warning(I18n.T("The spell had no effect!")));
            return FALSE;
        }
        to_chat(cast_on_living, span_userdanger(I18n.T("Your mind gets twisted!")));
        await cast_on_living.emote("scream");
        await cast_on_living.apply_status_effect(typeof(datum_status_effect_psychic_projection), projection_duration);
        return TRUE;
    }
}

/// Status effect that adds a weird view to its owner and causes them to rapidly shoot a firearm in their general direction.
// tg: code/modules/religion/burdened/psyker.dm:284
public partial class datum_status_effect_psychic_projection : datum_status_effect
{
    // tg: code/modules/religion/burdened/psyker.dm:294
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num(duration);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/religion/burdened/psyker.dm:298
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        atom_movable_plane_master_controller game_plane_master_controller = owner.hud_used?.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
        if (game_plane_master_controller == null)
            return FALSE;
        game_plane_master_controller.add_filter("psychic_blur", 10, angular_blur_filter(0, 0, 3) as DmList);
        game_plane_master_controller.add_filter("psychic_wave", 10, wave_filter(240, 240, 3, 0, WAVE_SIDEWAYS) as DmList);
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/modules/religion/burdened/psyker.dm:306
    public override async DmTask<object> on_remove()
    {
        atom_movable_plane_master_controller game_plane_master_controller = owner.hud_used?.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
        if (game_plane_master_controller == null)
            return null;
        game_plane_master_controller.remove_filter("psychic_blur");
        game_plane_master_controller.remove_filter("psychic_wave");
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/religion/burdened/psyker.dm:313
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        obj_item_gun held_gun = owner?.is_holding_item_of_type(typeof(obj_item_gun)) as obj_item_gun;
        if (held_gun == null)
            return null;
        if (!Dm.truthy(held_gun.can_shoot()))
        {
            if (firing_delay < times_dry_fired)
            {
                firing_delay++;
                return null;
            }
            firing_delay = 0;
            times_dry_fired++;
        }
        else
            times_dry_fired = 0;
        turf target_turf = get_offset_target_turf(get_ranged_target_turf(owner, owner.dir, 7) as atom, dx: rand(-1, 1), dy: rand(-1, 1)) as turf;
        await held_gun.process_fire(target_turf, owner, TRUE, null, (string)pick(GLOB.all_body_zones));
        held_gun.fire_cd = FALSE;
        return null;
    }
}

// tg: code/modules/religion/burdened/psyker.dm:329
public partial class datum_action_cooldown_spell_charged_psychic_booster : datum_action_cooldown_spell_charged
{
    // tg: code/modules/religion/burdened/psyker.dm:349
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(boosted))
            stop_effects();
        return base.Destroy(force);
    }

    // tg: code/modules/religion/burdened/psyker.dm:354
    public override async DmTask Remove(mob removed_from = null)
    {
        if (Dm.truthy(boosted))
            stop_effects();
        await base.Remove(removed_from);
    }

    // tg: code/modules/religion/burdened/psyker.dm:359
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = null;
        dot = await base.cast(cast_on);
        if (Dm.truthy(boosted))
            return dot;
        boosted = TRUE;
        to_chat(owner, span_boldnotice(I18n.T("Your trigger fingers feel stronger.")));
        ADD_TRAIT(cast_on, TRAIT_DOUBLE_TAP, type);
        RegisterSignal(cast_on, COMSIG_PROJECTILE_FIRER_BEFORE_FIRE, nameof(modify_projectile));
        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_effects)), effect_time, file: "code/modules/religion/burdened/psyker.dm", line: 367);
        return dot;
    }

    // tg: code/modules/religion/burdened/psyker.dm:369
    public virtual void stop_effects()
    {
        boosted = FALSE;
        to_chat(owner, span_danger(I18n.T("Your trigger fingers feel weaker.")));
        REMOVE_TRAIT(owner, TRAIT_DOUBLE_TAP, type);
        UnregisterSignal(owner, COMSIG_PROJECTILE_FIRER_BEFORE_FIRE);
    }

    // tg: code/modules/religion/burdened/psyker.dm:375
    public virtual async DmTask modify_projectile(datum source = null, obj_projectile bullet = null, atom firer = null, atom original_target = null)
    {
        atom target = original_target;
        if (isturf(target) || (isobj(target) && !Dm.truthy(target.density))) //if weird target, we try to compensate in our homing
        {
            foreach (mob_living shooting_target in Dm.each<mob_living>(range(1, get_turf(target))))
            {
                if (shooting_target == firer)
                    continue;
                target = shooting_target;
                break;
            }
        }
        if (!Dm.truthy(await bullet.can_hit_target(target, direct_target: TRUE, ignore_loc: TRUE)))
            return;
        bullet.original = target;
        bullet.homing_turn_speed = 30;
        bullet.set_homing_target(target);
    }
}

// tg: code/modules/religion/burdened/psyker.dm:389
public partial class datum_action_cooldown_spell_forcewall_psychic_wall : datum_action_cooldown_spell_forcewall
{
    // tg: code/modules/religion/burdened/psyker.dm:401
    public override async DmTask spawn_wall(turf cast_turf = null)
    {
        await base.spawn_wall(cast_turf);
        // play_fov_effect(cast_turf, 5, "forcefield", time = 10 SECONDS): the field of view's edge effect is presentation the
        // first-person view does not have
        Dm.Deferred("code/modules/religion/burdened/psyker.dm:403", "spawn_wall: play_fov_effect (presentation)");
    }
}

public sealed partial class TgState
{
    // tg: code/modules/religion/burdened/psyker.dm:140  /obj/item/gun/ballistic/revolver/chaplain's var/static/list/possible_names
    internal DmList obj_item_gun_ballistic_revolver_chaplain__possible_names;
}
