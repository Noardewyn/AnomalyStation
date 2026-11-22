// Translated from tgstation (snapshot 1d99bd7f): code/datums/embedding.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 3b: embedding). The var block is generated (D111). Left for their milestones inside the
// translated procs: the scream of a painful removal (emote, M9: Dm.Deferred). M7x slice C: setup_shrapnel, the shrapnel payload
// of a projectile's embed.

/*
 * The magical embedding datum which is a container for all embedding interactions an item (or a projectile) can have.
 * Whenever an item with an embedding datum is thrown into a carbon with either EMBED_THROWSPEED_THRESHOLD throwspeed or ignore_throwspeed_threshold set to TRUE, it will
 * embed into them, with latter option reserved for sticky items and shrapnel.
 * Whenever a projectile embeds, the datum is copied onto the shrapnel
 */
// tg: code/datums/embedding.dm:13
[DmPath("/datum/embedding")]
public partial class datum_embedding : datum
{
    /// How quicker is it for someone else to rip out an item?
    // tg: code/datums/embedding.dm:2  #define RIPPING_OUT_HELP_TIME_MULTIPLIER (file-local, #undef at :618)
    private const float RIPPING_OUT_HELP_TIME_MULTIPLIER = 0.75f;
    /// How much safer is it for someone else to rip out an item?
    // tg: code/datums/embedding.dm:4  #define RIPPING_OUT_HELP_DAMAGE_MULTIPLIER (file-local, #undef at :619)
    private const float RIPPING_OUT_HELP_DAMAGE_MULTIPLIER = 0.75f;

    // tg: code/datums/embedding.dm:50
    public override async DmTask New(params object[] __extra)
    {
        obj_item creator = Dm.arg<obj_item>(__extra, 0);
        await base.New(Dm.forward(__extra, creator));
        if (creator != null)
            register_on(creator);
    }

    /// Registers ourselves with an item
    // tg: code/datums/embedding.dm:56
    public virtual void register_on(obj_item new_parent = null)
    {
        if (!isitem(new_parent))
            throw Dm.CRASH(Dm.text("Embedding datum attempted to register on a non-item object [] ([])", new_parent, new_parent?.type), "code/datums/embedding.dm", 58);

        parent = new_parent;
        RegisterSignal(parent, COMSIG_QDELETING, nameof(on_qdel));

        RegisterSignal(parent, COMSIG_MOVABLE_IMPACT_ZONE, nameof(try_embed));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE_TAGS, nameof(examined_tags));
    }

    // tg: code/datums/embedding.dm:66
    public override object Destroy(float? force = null)
    {
        if (parent == null)
            return base.Destroy(force);
        parent.set_embed(null);
        UnregisterSignal(parent, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_IMPACT_ZONE, COMSIG_ATOM_EXAMINE));
        owner = null;
        owner_limb = null;
        parent = null;
        return base.Destroy(force);
    }

    /// Creates a copy and sets all of its *relevant* variables
    /// Children should override this with new variables if they add any "generic" ones
    // tg: code/datums/embedding.dm:78
    public virtual object create_copy(atom_movable new_owner = null)
    {
        datum_embedding brother = (datum_embedding)Dm.@new(type, new_owner);
        brother.embed_chance = embed_chance;
        brother.fall_chance = fall_chance;
        brother.pain_chance = pain_chance;
        brother.pain_mult = pain_mult;
        brother.impact_pain_mult = impact_pain_mult;
        brother.remove_pain_mult = remove_pain_mult;
        brother.rip_time = rip_time;
        brother.ignore_throwspeed_threshold = ignore_throwspeed_threshold;
        brother.jostle_chance = jostle_chance;
        brother.jostle_pain_mult = jostle_pain_mult;
        brother.pain_stam_pct = pain_stam_pct;
        brother.immune_traits = immune_traits.Copy();
        brother.stealthy_embed = stealthy_embed;
        return brother;
    }

    ///Someone inspected our embeddable item
    // tg: code/datums/embedding.dm:96
    [SignalHandler]
    public virtual void examined_tags(obj_item source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.truthy(is_harmless()))
            examine_list["sticky"] = I18n.T("[] looks sticky, and could probably get stuck to someone if thrown properly!", parent);
        else
            examine_list["embeddable"] = I18n.T("[] has a fine point, and could probably embed in someone if thrown properly!", parent);
    }

    /// Is passed victim a valid target for us to embed into?
    // tg: code/datums/embedding.dm:105
    public virtual object can_embed(atom_movable source = null, mob_living_carbon victim = null, object hit_zone = null, datum_thrownthing throwingdatum = null)
    {
        if (victim is not mob_living_carbon)
            return FALSE;

        if (HAS_TRAIT(victim, TRAIT_GODMODE))
            return null;

        if (Dm.truthy(immune_traits))
        {
            foreach (object immunity_trait in Dm.each(immune_traits))
            {
                if (HAS_TRAIT(victim, immunity_trait))
                    return FALSE;
            }
        }

        if (isitem(source))
        {
            float flying_speed = Dm.truthy(throwingdatum?.speed) ? Dm.num(throwingdatum.speed) : Dm.num(((obj_item)source).throw_speed);
            if (flying_speed < EMBED_THROWSPEED_THRESHOLD && !Dm.truthy(ignore_throwspeed_threshold))
                return FALSE;
        }

        return TRUE;
    }

    /// Attempts to embed an object
    // tg: code/datums/embedding.dm:125
    [SignalHandler]
    public virtual object try_embed(obj_item weapon = null, mob_living_carbon victim = null, object hit_zone = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        if (Dm.truthy(blocked) || !Dm.truthy(can_embed(parent, victim, hit_zone, throwingdatum)))
        {
            failed_embed(victim, hit_zone);
            return null;
        }

        if (!Dm.truthy(roll_embed_chance(victim, hit_zone, throwingdatum)))
        {
            failed_embed(victim, hit_zone, random: TRUE);
            return null;
        }

        obj_item_bodypart limb = (victim.get_bodypart(hit_zone as string) is var zone_part && Dm.truthy(zone_part) ? zone_part : victim.get_bodypart()) as obj_item_bodypart;
        Waiting.NoSleep(embed_into(victim, limb));
        return MOVABLE_IMPACT_ZONE_OVERRIDE;
    }

    /// Attempts to embed shrapnel from a projectile
    // tg: code/datums/embedding.dm:141
    public virtual async DmTask try_embed_projectile(obj_projectile source = null, atom hit = null, object hit_zone = null, object blocked = null, object pierce_hit = null)
    {
        if (Dm.truthy(pierce_hit))
            return;

        if (Dm.num(blocked) >= 100 || !Dm.truthy(can_embed(source, hit as mob_living_carbon)))
        {
            failed_embed(hit as mob_living_carbon, hit_zone);
            return;
        }

        mob_living_carbon victim = (mob_living_carbon)hit;
        setup_shrapnel(source, victim);
        obj_item payload = null; // var/obj/item/payload = setup_shrapnel(source, victim): setup_shrapnel returns nothing

        if (!Dm.truthy(roll_embed_chance(victim, hit_zone)))
        {
            failed_embed(victim, hit_zone, random: TRUE);
            return;
        }

        obj_item_bodypart limb = (victim.get_bodypart(hit_zone as string) is var zone_part && Dm.truthy(zone_part) ? zone_part : victim.get_bodypart()) as obj_item_bodypart;
        await embed_into(victim, limb);
        if (Dm.truthy(source._listen_lookup?[COMSIG_PROJECTILE_ON_EMBEDDED]))
            source._SendSignal(COMSIG_PROJECTILE_ON_EMBEDDED, Dm.list(source, payload, hit));
    }

    /// Used for custom logic while setting up shrapnel payload
    // tg: code/datums/embedding.dm:161
    public virtual void setup_shrapnel(obj_projectile source = null, mob_living_carbon victim = null)
    {
        Type shrapnel_type = source.shrapnel_type;
        obj_item payload = Dm.@new(shrapnel_type, get_turf(victim)) as obj_item;
        // Detach from parent, we don't want em to delete us
        source.set_embed(null, dont_delete: TRUE);
        // Hook signals up first, as payload sends a comsig upon embed update
        register_on(payload);
        payload.set_embed(this);
        if (istype(payload, typeof(obj_item_shrapnel_bullet)))
            payload.name = source.name;
        if (Dm.truthy(source._listen_lookup?[COMSIG_PROJECTILE_ON_SPAWN_EMBEDDED]))
            source._SendSignal(COMSIG_PROJECTILE_ON_SPAWN_EMBEDDED, Dm.list(source, payload, victim));
    }

    /// Calculates the actual chance to embed based on armour penetration and throwing speed, then returns true if we pass that probability check
    // tg: code/datums/embedding.dm:174
    public virtual object roll_embed_chance(mob_living_carbon victim = null, object hit_zone = null, datum_thrownthing throwingdatum = null)
    {
        float chance = embed_chance;

        // Something threw us really, really fast
        if (throwingdatum != null && Dm.num(throwingdatum.speed) > Dm.num(parent.throw_speed))
            chance += (Dm.num(throwingdatum.speed) - Dm.num(parent.throw_speed)) * EMBED_CHANCE_SPEED_BONUS;

        if (Dm.truthy(is_harmless()))
            return prob(embed_chance) ? TRUE : FALSE;

        // We'll be nice and take the better of bullet and bomb armor, halved
        float armor = max(Dm.num(victim.run_armor_check(hit_zone, BULLET, armour_penetration: parent.armour_penetration, silent: TRUE)), Dm.num(victim.run_armor_check(hit_zone, BOMB, armour_penetration: parent.armour_penetration, silent: TRUE))) * 0.5f;
        // We only care about armor penetration if there's actually armor to penetrate
        if (armor == 0)
            return prob(chance) ? TRUE : FALSE;

        if (Dm.truthy(parent.weak_against_armour))
            armor *= ARMOR_WEAKENED_MULTIPLIER;

        chance -= armor;
        if (chance < 0)
        {
            victim.visible_message(span_danger(I18n.T("[] bounces off []'s armor, unable to embed!", parent, victim)),
                span_notice(I18n.T("[] bounces off your armor, unable to embed!", parent)), vision_distance: COMBAT_MESSAGE_RANGE);
            return FALSE;
        }

        return prob(chance) ? TRUE : FALSE;
    }

    /// We've tried to embed into something and failed
    /// Random being TRUE means we've lost the roulette, FALSE means we've either been blocked or the target is invalid
    // tg: code/datums/embedding.dm:203
    public virtual void failed_embed(mob_living_carbon victim = null, object hit_zone = null, float? random = null)
    {
        random ??= FALSE;
        if (parent is not obj_item)
            return;
        if (Dm.truthy(parent._listen_lookup?[COMSIG_ITEM_FAILED_EMBED]))
            parent._SendSignal(COMSIG_ITEM_FAILED_EMBED, Dm.list(parent, victim, hit_zone));
        if (Dm.truthy(Dm.bitand(parent.item_flags, DROPDEL)) && !QDELETED(parent))
            qdel(parent);
    }

    /// Does this item deal any damage when embedding or jostling inside of someone?
    // tg: code/datums/embedding.dm:211
    public virtual object is_harmless(float? consider_stamina = null)
    {
        consider_stamina ??= FALSE;
        return pain_mult == 0 && jostle_pain_mult == 0 && (Dm.truthy(consider_stamina) || pain_stam_pct < 1) ? TRUE : FALSE;
    }

    //Handles actual embedding logic.
    // tg: code/datums/embedding.dm:215
    [ShouldNotOverride]
    public virtual async DmTask<object> embed_into(mob_living_carbon victim = null, obj_item_bodypart target_limb = null)
    {
        set_owner(victim, target_limb);

        START_PROCESSING(SSprocessing, this);
        owner_limb._embed_object(parent);
        await parent.forceMove(owner);
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(weapon_disappeared));
        RegisterSignal(parent, COMSIG_MAGIC_RECALL, nameof(magic_pull));
        owner.visible_message(span_danger(I18n.T("[] [] []'s []!", parent, Dm.truthy(is_harmless()) ? I18n.T("sticks itself to") : I18n.T("embeds itself in"), owner, owner_limb.plaintext_zone)),
            span_userdanger(I18n.T("[] [] your []!", parent, Dm.truthy(is_harmless()) ? I18n.T("sticks itself to") : I18n.T("embeds itself in"), owner_limb.plaintext_zone)));

        float damage = Dm.num(parent.throwforce);
        if (!Dm.truthy(is_harmless(consider_stamina: TRUE)))
        {
            if (!Dm.truthy(stealthy_embed))
            {
                owner.throw_alert(ALERT_EMBEDDED_OBJECT, typeof(atom_movable_screen_alert_embeddedobject));
                await owner.add_mood_event("embedded", typeof(datum_mood_event_embedded));
            }
            if (!Dm.truthy(is_harmless()))
            {
                playsound(owner, Dm.resource("sound/items/weapons/bladeslice.ogg"), 40);
                if (Dm.truthy(owner_limb.can_bleed()))
                    await parent.add_mob_blood(owner); // it embedded itself in you, of course it's bloody!
            }
            damage += Dm.num(parent.w_class) * impact_pain_mult;
        }

        if (Dm.truthy(parent._listen_lookup?[COMSIG_ITEM_EMBEDDED]))
            parent._SendSignal(COMSIG_ITEM_EMBEDDED, Dm.list(parent, victim, target_limb));
        await on_successful_embed(victim, target_limb);

        if (damage <= 0)
            return TRUE;

        object armor = owner.run_armor_check(owner_limb.body_zone, MELEE, I18n.T("Your armor has protected your [].", owner_limb.plaintext_zone),
            I18n.T("Your armor has softened a hit to your [].", owner_limb.plaintext_zone), parent.armour_penetration,
            weak_against_armour: parent.weak_against_armour);

        await owner.apply_damage(
            damage: (1 - pain_stam_pct) * damage,
            damagetype: BRUTE,
            def_zone: owner_limb.body_zone,
            blocked: Dm.num(armor),
            wound_bonus: parent.wound_bonus,
            exposed_wound_bonus: parent.exposed_wound_bonus,
            sharpness: Dm.num(parent.get_sharpness()),
            attacking_item: parent);

        await owner.apply_damage(
            damage: pain_stam_pct * damage,
            damagetype: STAMINA);
        return TRUE;
    }

    /// Proc which is called upon successfully embedding into someone/something, for children to override
    // tg: code/datums/embedding.dm:268
    public virtual async DmTask on_successful_embed(mob_living_carbon victim = null, obj_item_bodypart target_limb = null)
    {
        return;
    }

    /// Registers signals that our owner should have
    /// Handles jostling, tweezing embedded items out and grenade chain reactions
    // tg: code/datums/embedding.dm:273
    public virtual void set_owner(mob_living_carbon victim = null, obj_item_bodypart target_limb = null)
    {
        owner = victim;
        owner_limb = target_limb;
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(owner_moved));
        RegisterSignal(owner, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_item_interaction));
        RegisterSignal(owner, COMSIG_ATOM_EX_ACT, nameof(on_ex_act));
        RegisterSignal(owner_limb, COMSIG_BODYPART_REMOVED, nameof(on_removed));
    }

    /// Avoid calling this directly as this doesn't move the object from its owner's contents
    /// Returns TRUE if the item got deleted due to DROPDEL flag
    // tg: code/datums/embedding.dm:283
    public virtual async DmTask<object> stop_embedding()
    {
        STOP_PROCESSING(SSprocessing, this);
        if (owner_limb != null)
        {
            UnregisterSignal(owner_limb, COMSIG_BODYPART_REMOVED);
            owner_limb._unembed_object(parent);
        }
        if (owner != null)
        {
            UnregisterSignal(owner, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_ATOM_ITEM_INTERACTION, COMSIG_ATOM_EX_ACT));
            if (!Dm.truthy(owner.has_embedded_objects()))
            {
                owner.clear_alert(ALERT_EMBEDDED_OBJECT);
                owner.clear_mood_event("embedded");
            }
        }
        UnregisterSignal(parent, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_MAGIC_RECALL));
        if (Dm.truthy(parent._listen_lookup?[COMSIG_ITEM_UNEMBEDDED]))
            parent._SendSignal(COMSIG_ITEM_UNEMBEDDED, Dm.list(parent, owner, owner_limb));
        owner = null;
        owner_limb = null;
        if (Dm.truthy(Dm.bitand(parent.item_flags, DROPDEL)) && !QDELETED(parent))
        {
            qdel(parent);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/datums/embedding.dm:302
    [SignalHandler]
    public virtual void on_qdel(atom_movable source = null)
    {
        if (owner_limb != null)
            weapon_disappeared();
        qdel(this);
    }

    /// Move self to owner's turf when our limb gets removed
    // tg: code/datums/embedding.dm:309
    [SignalHandler]
    public virtual void on_removed(datum source = null, mob_living_carbon old_owner = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(stop_embedding()))) // Dropdel?
            Waiting.NoSleep(parent.forceMove(old_owner.drop_location() as atom));
    }

    /// Someone attempted to pull us out! Either the owner by inspecting themselves, or someone else by examining the owner and clicking the link.
    // tg: code/datums/embedding.dm:315
    public virtual async DmTask rip_out(mob_living jack_the_ripper = null)
    {
        if (!Dm.truthy(await owner.IsReachableBy(jack_the_ripper)))
            return;

        if (!Dm.truthy(await jack_the_ripper.can_perform_action(owner, Dm.bitor(Dm.bitor(FORBID_TELEKINESIS_REACH, NEED_HANDS), ALLOW_RESTING))))
            return;

        float time_taken = rip_time * Dm.num(parent.w_class);
        float damage_mult = 1;
        if (jack_the_ripper != owner)
        {
            time_taken *= RIPPING_OUT_HELP_TIME_MULTIPLIER;
            damage_mult *= RIPPING_OUT_HELP_DAMAGE_MULTIPLIER;
            owner.visible_message(span_warning(I18n.T("[] attempts to remove [] from []'s []!", jack_the_ripper, parent, owner, owner_limb.plaintext_zone)),
                span_userdanger(I18n.T("[] attempt to remove [] from your []!", jack_the_ripper, parent, owner_limb.plaintext_zone)), ignored_mobs: jack_the_ripper);
            to_chat(jack_the_ripper, span_notice(I18n.T("You attempt to remove [] from []'s []...", parent, owner, owner_limb.plaintext_zone)));
        }
        else
        {
            owner.visible_message(span_warning(I18n.T("[] attempts to remove [] from [] [].", owner, parent, owner.p_their(), owner_limb.plaintext_zone)),
                span_notice(I18n.T("You attempt to remove [] from your []...", parent, owner_limb.plaintext_zone)));
        }

        if (!Dm.truthy(await do_after(jack_the_ripper, time_taken, owner, extra_checks: Dm.@new<datum_callback>(this, nameof(still_in)))))
            return;

        if (parent.loc != owner || !Dm.@in(parent, owner_limb?.embedded_objects))
            return;

        if (jack_the_ripper == owner)
        {
            owner.visible_message(span_notice(I18n.T("[] successfully rips [] [] of [] []!", owner, parent, Dm.truthy(is_harmless()) ? I18n.T("off") : I18n.T("out"), owner.p_their(), owner_limb.plaintext_zone)),
                span_notice(I18n.T("You successfully remove [] from your [].", parent, owner_limb.plaintext_zone)));
        }
        else
        {
            owner.visible_message(span_notice(I18n.T("[] successfully rips [] [] of []'s []!", jack_the_ripper, parent, Dm.truthy(is_harmless()) ? I18n.T("off") : I18n.T("out"), owner, owner_limb.plaintext_zone)),
                span_userdanger(I18n.T("[] removes [] from your []!", jack_the_ripper, parent, owner_limb.plaintext_zone)), ignored_mobs: jack_the_ripper);
            to_chat(jack_the_ripper, span_notice(I18n.T("You successfully remove [] from []'s [].", parent, owner, owner_limb.plaintext_zone)));
        }

        if (!Dm.truthy(is_harmless()))
            await damaging_removal_effect(damage_mult);
        await remove_embedding(jack_the_ripper);
    }

    /// Handles damage effects upon forceful removal
    // tg: code/datums/embedding.dm:353
    public virtual async DmTask damaging_removal_effect(object ouchies_multiplier = null)
    {
        float damage = Dm.num(parent.w_class) * remove_pain_mult * Dm.num(ouchies_multiplier);
        await owner.apply_damage(
            damage: (1 - pain_stam_pct) * damage,
            damagetype: BRUTE,
            def_zone: owner_limb,
            wound_bonus: max(0, Dm.num(parent.wound_bonus)), // It hurts to rip it out, get surgery you dingus. unlike the others, this CAN wound + increase slash bloodflow
            sharpness: Dm.truthy(parent.get_sharpness()) ? Dm.num(parent.get_sharpness()) : SHARP_EDGED, // always sharp, even if the object isn't
            attacking_item: parent);

        await owner.apply_damage(
            damage: pain_stam_pct * damage,
            damagetype: STAMINA);

        await owner.emote("scream");
    }

    /// The proper proc to call when you want to remove something. If a mob is passed, the item will be put in its hands - otherwise it's just dumped onto the ground
    // tg: code/datums/embedding.dm:372
    public virtual async DmTask remove_embedding(mob_living to_hands = null)
    {
        mob_living_carbon stored_owner = owner;
        if (Dm.truthy(await stop_embedding())) // Dropdel?
            return;
        await parent.forceMove(stored_owner.drop_location() as atom);
        if (to_hands != null)
            await to_hands.put_in_hands(parent);
    }

    /// When owner moves around, attempt to jostle the item
    // tg: code/datums/embedding.dm:381
    [SignalHandler]
    public virtual void owner_moved(mob_living_carbon source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null)
    {
        float chance = jostle_chance;
        if (!Dm.truthy(forced) && (Dm.eq(owner.move_intent, MOVE_INTENT_WALK) || Dm.eq(owner.body_position, LYING_DOWN)) && !Dm.truthy(CHECK_MOVE_LOOP_FLAGS(source, MOVEMENT_LOOP_OUTSIDE_CONTROL)))
            chance *= 0.5f;

        if (Dm.truthy(is_harmless(consider_stamina: TRUE)) || !prob(chance))
            return;

        float damage = Dm.num(parent.w_class) * jostle_pain_mult;
        Waiting.NoSleep(owner.apply_damage(
            damage: (1 - pain_stam_pct) * damage,
            damagetype: BRUTE,
            def_zone: owner_limb,
            wound_bonus: CANT_WOUND,
            sharpness: Dm.num(parent.get_sharpness()),
            attacking_item: parent));

        Waiting.NoSleep(owner.apply_damage(
            damage: pain_stam_pct * damage,
            damagetype: STAMINA));

        if (Dm.truthy(stealthy_embed))
            to_chat(owner, span_danger(I18n.T("Something in your [] jostles and stings!", owner_limb.plaintext_zone)));
        else
            to_chat(owner, span_userdanger(I18n.T("[] embedded in your [] jostles and stings!", parent, owner_limb.plaintext_zone)));
        Waiting.NoSleep(jostle_effects());
    }

    /// Effects which should occur when the owner moves, sometimes
    // tg: code/datums/embedding.dm:413
    public virtual async DmTask jostle_effects()
    {
        return;
    }

    /// When someone attempts to pluck us with tweezers or wirecutters
    // tg: code/datums/embedding.dm:417
    [SignalHandler]
    public virtual object on_item_interaction(mob_living_carbon victim = null, mob user = null, obj_item tool = null)
    {
        if (!Dm.eq(user.zone_selected, owner_limb.body_zone) || (!Dm.eq(tool.tool_behaviour, TOOL_HEMOSTAT) && !Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER)))
            return null;

        if (parent != LAZYACCESS(owner_limb.embedded_objects, 1)) // Don't pluck everything at the same time
            return null;

        // Ensure that we can actually
        if (!Dm.truthy(owner.try_inject(user, owner_limb.body_zone, Dm.bitor(INJECT_CHECK_IGNORE_SPECIES, INJECT_TRY_SHOW_ERROR_MESSAGE))))
            return COMPONENT_NO_AFTERATTACK;

        Waiting.Detach(try_pluck(tool, user));
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/datums/embedding.dm:433
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process__body(dot, seconds_per_tick), dot); // the slot's set waitfor = FALSE (datum process, processing.dm:48)
    }

    private async DmTask<object> process__body(DotBox<object> dot, object seconds_per_tick)
    {
        if (owner == null || owner_limb == null || owner_limb.owner != owner)
        {
            stack_trace(Dm.text("Attempted to process embedding on [] ([]) without an owner, owner_limb or owner-less limb!", parent, parent.type), "code/datums/embedding.dm", 436);
            await parent.forceMove(get_turf(parent));
            return dot.Value;
        }

        if (Dm.truthy(await process_effect(seconds_per_tick)))
            return dot.Value;

        if (owner.stat == DEAD)
            return dot.Value;

        float fall_chance_current = SPT_PROB_RATE(fall_chance / 100, seconds_per_tick) * 100;
        if (Dm.eq(owner.body_position, LYING_DOWN))
            fall_chance_current *= 0.2f;

        if (prob(fall_chance_current))
        {
            await fall_out();
            return dot.Value;
        }

        float damage = Dm.num(parent.w_class) * pain_mult;
        float pain_chance_current = SPT_PROB_RATE(pain_chance / 100, seconds_per_tick) * 100;
        if (Dm.truthy(pain_stam_pct) && HAS_TRAIT_FROM(owner, TRAIT_INCAPACITATED, STAMINA)) //if it's a less-lethal embed, give them a break if they're already stamcritted
        {
            pain_chance_current *= 0.2f;
            damage *= 0.5f;
        }
        else if (Dm.eq(owner.body_position, LYING_DOWN))
            pain_chance_current *= 0.2f;

        if (Dm.truthy(is_harmless(consider_stamina: TRUE)) || !prob(pain_chance_current))
            return dot.Value;

        await owner.apply_damage(
            damage: (1 - pain_stam_pct) * damage,
            damagetype: BRUTE,
            def_zone: owner_limb,
            wound_bonus: CANT_WOUND,
            sharpness: Dm.num(parent.get_sharpness()),
            attacking_item: parent);

        await owner.apply_damage(
            damage: pain_stam_pct * damage,
            damagetype: STAMINA);
        if (Dm.truthy(stealthy_embed))
            to_chat(owner, span_danger(I18n.T("Something in your [] []", owner_limb.plaintext_zone, pain_stam_pct < 1 ? I18n.T("hurts!") : I18n.T("weighs you down."))));
        else
            to_chat(owner, span_userdanger(I18n.T("[] embedded in your [] []", parent, owner_limb.plaintext_zone, pain_stam_pct < 1 ? I18n.T("hurts!") : I18n.T("weighs you down."))));
        return dot.Value;
    }

    /// Called every process, return TRUE in order to abort further processing - if it falls out, etc
    // tg: code/datums/embedding.dm:483
    public virtual async DmTask<object> process_effect(object seconds_per_tick = null)
    {
        return null;
    }

    /// Attempt to pluck out the embedded item using tweezers of some kind
    // tg: code/datums/embedding.dm:487
    public virtual async DmTask try_pluck(obj_item tool = null, mob user = null)
    {
        float pluck_time = rip_time * (Dm.num(parent.w_class) * 0.3f) * tool.toolspeed;
        bool self_pluck = user == owner;
        bool safe_pluck = !Dm.eq(tool.tool_behaviour, TOOL_HEMOSTAT);
        // Don't harm ourselves if we're just stuck
        if (Dm.truthy(is_harmless()))
            safe_pluck = true;
        if (self_pluck)
            pluck_time *= 1.5f;
        // Wirecutters are harder to use for this
        if (safe_pluck)
            pluck_time *= 1.5f;

        if (self_pluck)
        {
            owner.visible_message(span_danger(I18n.T("[] begins plucking [] from [] [] with []...", owner, parent, owner.p_their(), owner_limb.plaintext_zone, tool)),
                span_notice(I18n.T("You start plucking [] from your [] with []...", parent, owner_limb.plaintext_zone, tool)), visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        }
        else
        {
            user.visible_message(span_danger(I18n.T("[] begins plucking [] from []'s [] with []...", user, parent, owner, owner_limb.plaintext_zone, tool)),
                span_notice(I18n.T("You start plucking [] from []'s [] with []...", parent, owner, owner_limb.plaintext_zone, tool)), ignored_mobs: owner);
            to_chat(owner, span_userdanger(I18n.T("[] begins plucking [] from your [] with []... ", user, parent, owner_limb.plaintext_zone, tool)));
        }

        if (!Dm.truthy(await do_after(user, pluck_time, owner, extra_checks: Dm.@new<datum_callback>(this, nameof(still_in)))))
        {
            if (self_pluck)
                to_chat(user, span_danger(I18n.T("You fail to pluck [] from your [].", parent, owner_limb.plaintext_zone)));
            else
            {
                to_chat(user, span_danger(I18n.T("You fail to pluck [] from []'s [].", parent, owner, owner_limb.plaintext_zone)));
                to_chat(owner, span_danger(I18n.T("[] fails to pluck [] from your [].", user, parent, owner_limb.plaintext_zone)));
            }
            return;
        }

        // tg passes the message as to_chat's target (no html), so BYOND shows nothing: kept as tg's
        if (self_pluck)
            to_chat(span_notice(I18n.T("You pluck [] from your [][]", parent, owner_limb.plaintext_zone, safe_pluck ? "." : span_danger(I18n.T(", but it hurts like hell")))));

        if (!safe_pluck)
            await damaging_removal_effect(min(self_pluck ? 1 : RIPPING_OUT_HELP_DAMAGE_MULTIPLIER, 0.4f * Dm.num(tool.w_class)));

        await remove_embedding(user as mob_living);
    }

    /// Called when then item randomly falls out of a carbon. This handles the damage and descriptors, then calls remove_embedding()
    // tg: code/datums/embedding.dm:525
    public virtual async DmTask fall_out()
    {
        if (Dm.truthy(is_harmless()))
        {
            owner.visible_message(span_warning(I18n.T("[] falls off of []'s []!", parent, owner.name, owner_limb.plaintext_zone)),
                span_warning(I18n.T("[] falls off of your []!", parent, owner_limb.plaintext_zone)));
            await remove_embedding();
            return;
        }

        float damage = Dm.num(parent.w_class) * remove_pain_mult;
        await owner.apply_damage(
            damage: (1 - pain_stam_pct) * damage,
            damagetype: BRUTE,
            def_zone: owner_limb,
            wound_bonus: CANT_WOUND,
            sharpness: Dm.num(parent.get_sharpness()),
            attacking_item: parent);

        await owner.apply_damage(
            damage: pain_stam_pct * damage,
            damagetype: STAMINA);

        owner.visible_message(span_danger(I18n.T("[] falls out of []'s []!", parent, owner.name, owner_limb.plaintext_zone)),
            span_userdanger(I18n.T("[] falls out of your []!", parent, owner_limb.plaintext_zone)));
        await remove_embedding();
    }

    /// Whenever the parent item is forcefully moved by some weird means
    // tg: code/datums/embedding.dm:552
    [SignalHandler]
    public virtual void weapon_disappeared(atom old_loc = null, object dir = null, object forced = null)
    {
        // If something moved it to their limb, its not really *disappearing*, is it?
        if (owner != null && parent.loc != owner_limb)
            to_chat(owner, span_userdanger(I18n.T("[] that was embedded in your [] disappears!", parent, owner_limb.plaintext_zone)));
        Waiting.NoSleep(stop_embedding());
    }

    /// So the sticky grenades chain-detonate, because mobs are very careful with which of their contents they blow up
    // tg: code/datums/embedding.dm:560
    [SignalHandler]
    public virtual void on_ex_act(atom source = null, object severity = null)
    {
        // In the process of owner's ex_act
        if (QDELETED(parent))
            return;
        if (Dm.eq(severity, EXPLODE_DEVASTATE))
            Dm.append(ref SSexplosions.high_mov_atom, parent);
        else if (Dm.eq(severity, EXPLODE_HEAVY))
            Dm.append(ref SSexplosions.med_mov_atom, parent);
        else if (Dm.eq(severity, EXPLODE_LIGHT))
            Dm.append(ref SSexplosions.low_mov_atom, parent);
    }

    /// Called when an object is ripped out of someone's body by magic or other abnormal means
    // tg: code/datums/embedding.dm:574
    [SignalHandler]
    public virtual void magic_pull(obj_item weapon = null, mob_living caster = null)
    {
        if (Dm.truthy(is_harmless()))
        {
            owner.visible_message(span_danger(I18n.T("[] vanishes from []'s []!", parent, owner, owner_limb.plaintext_zone)), span_userdanger(I18n.T("[] vanishes from []!", parent, owner_limb.plaintext_zone)));
            return;
        }

        float damage = Dm.num(parent.w_class) * remove_pain_mult;

        Waiting.NoSleep(owner.apply_damage(
            damage: (1 - pain_stam_pct) * damage * 1.5f,
            damagetype: BRUTE,
            def_zone: owner_limb,
            wound_bonus: max(0, Dm.num(parent.wound_bonus)), // Performs exit wounds and flings the user to the caster if nearby
            sharpness: Dm.truthy(parent.get_sharpness()) ? Dm.num(parent.get_sharpness()) : SHARP_EDGED,
            attacking_item: parent));

        Waiting.NoSleep(owner.apply_damage(
            damage: pain_stam_pct * damage,
            damagetype: STAMINA));

        Waiting.NoSleep(owner.cause_wound_of_type_and_severity(WOUND_PIERCE, owner_limb, WOUND_SEVERITY_MODERATE));
        playsound(owner, Dm.resource("sound/effects/wounds/blood2.ogg"), 50, TRUE);

        float dist = Dm.num(get_dist(caster, owner)); //Check if the caster is close enough to yank them in
        if (dist >= 7)
        {
            owner.visible_message(span_danger(I18n.T("[] is violently torn from []'s []!", parent, owner, owner_limb.plaintext_zone)), span_userdanger(I18n.T("[] is violently torn from your []!", parent, owner_limb.plaintext_zone)));
            return;
        }

        Waiting.NoSleep(owner.throw_at(caster, Dm.num(get_dist(owner, caster)) - 1, 1, caster));
        Waiting.NoSleep(owner.Paralyze(1 * SECONDS));
        owner.visible_message(span_alert(I18n.T("[] is sent flying towards [] as the [] tears out of them!", owner, caster, parent)), span_alert(I18n.T("You are launched at [] as the [] tears from your body and towards their hand!", caster, parent)));
    }

    // tg: code/datums/embedding.dm:609
    public virtual object still_in()
    {
        if (parent.loc != owner)
            return FALSE;
        if (!Dm.@in(parent, owner_limb?.embedded_objects))
            return FALSE;
        if (owner_limb?.owner != owner)
            return FALSE;
        return TRUE;
    }
}
