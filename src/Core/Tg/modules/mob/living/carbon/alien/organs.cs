// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the alien organs: the plasma vessel (its regeneration on weeds and the HUD's plasma display), the
// hive node (the queen's death), the resin spinner, the acid and neurotoxin glands, the egg sac and the stomach that devours (its
// struggle from inside, the burst and the ejection). The var blocks are generated (D111).

// tg: code/modules/mob/living/carbon/alien/organs.dm:5
public partial class obj_item_organ_alien_plasmavessel : obj_item_organ_alien
{
    // tg: code/modules/mob/living/carbon/alien/organs.dm:52
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        float delta_time = DELTA_WORLD_TIME(SSmobs);
        //Instantly healing to max health in a single tick would be silly. If it takes 8 seconds to fire, then something's fucked.
        float delta_time_capped = min(delta_time, 8);
        //If there are alien weeds on the ground then heal if needed or give some plasma
        if (!(Dm.locate<obj_structure_alien_weeds>(owner.loc) != null))
        {
            await owner.adjustPlasma(0.1f * plasma_rate * delta_time);
            return;
        }

        if (owner.health >= owner.maxHealth)
        {
            await owner.adjustPlasma(plasma_rate * delta_time);
            return;
        }

        float heal_amt = heal_rate;
        if (!isalien(owner))
            heal_amt *= 0.2f;
        await owner.adjustPlasma(0.5f * plasma_rate * delta_time_capped);
        await owner.adjust_brute_loss(-heal_amt * delta_time_capped);
        await owner.adjust_fire_loss(-heal_amt * delta_time_capped);
        await owner.adjust_oxy_loss(-heal_amt * delta_time_capped);
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:75
    public virtual void adjust_plasma(object amount = null)
    {
        stored_plasma = clamp(stored_plasma + Dm.num(amount), 0, max_plasma);
        update_plasma_display();
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:79
    [SignalHandler]
    public virtual void on_hud_created(mob source = null)
    {
        if (!Dm.truthy(owner.hud_used.screen_objects[HUD_ALIEN_PLASMA_DISPLAY]))
            Waiting.NoSleep(owner.hud_used.add_screen_object(typeof(atom_movable_screen_alien_plasma_display), HUD_ALIEN_PLASMA_DISPLAY, HUD_GROUP_INFO, ui_loc: owner.hud_used is datum_hud_alien ? null : ui_alienplasmadisplay_human, update_screen: TRUE));
        update_plasma_display();
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:86
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(organ_owner, COMSIG_MOB_HUD_CREATED, nameof(on_hud_created));
        if (organ_owner.hud_used != null)
            on_hud_created();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:92
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal((datum)organ_owner, COMSIG_MOB_GET_STATUS_TAB_ITEMS);
        datum_hud owner_hud = (organ_owner as mob_living_carbon)?.hud_used;
        if (owner_hud != null)
            await owner_hud.remove_screen_object(HUD_ALIEN_PLASMA_DISPLAY);
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:97
    public virtual void update_plasma_display()
    {
        atom_movable_screen display = owner?.hud_used?.screen_objects[HUD_ALIEN_PLASMA_DISPLAY] as atom_movable_screen;
        if (display != null)
            display.maptext = MAPTEXT(
                Dm.text("<div align='center' valign='middle' style='position:relative; top:0px; left:6px'><font color='magenta'>[]</font></div>", round(Dm.num(owner.getPlasma()))));
    }
}

// tg: code/modules/mob/living/carbon/alien/organs.dm:104
public partial class obj_item_organ_alien_hivenode : obj_item_organ_alien
{
    // tg: code/modules/mob/living/carbon/alien/organs.dm:102  #define QUEEN_DEATH_DEBUFF_DURATION 2400 (#undef at :157)
    private const int QUEEN_DEATH_DEBUFF_DURATION = 2400;

    // tg: code/modules/mob/living/carbon/alien/organs.dm:115
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        organ_owner.add_faction(ROLE_ALIEN);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:119
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        special ??= FALSE;
        if (organ_owner != null)
            ((mob_living_carbon)organ_owner).remove_faction(ROLE_ALIEN);
        await base.on_mob_remove(organ_owner, special, movement_flags);
    }

    //When the alien queen dies, all aliens suffer a penalty as punishment for failing to protect her.
    // tg: code/modules/mob/living/carbon/alien/organs.dm:125
    public virtual async DmTask queen_death()
    {
        if (owner == null || owner.stat == DEAD)
            return;
        if (isalien(owner)) //Different effects for aliens than humans
        {
            to_chat(owner, span_userdanger(I18n.T("Your Queen has been struck down!")));
            to_chat(owner, span_danger(I18n.T("You are struck with overwhelming agony! You feel confused, and your connection to the hivemind is severed.")));
            await owner.emote("roar");
            await owner.Stun(200); //Actually just slows them down a bit.
        }

        else if (ishuman(owner)) //Humans, being more fragile, are more overwhelmed by the mental backlash.
        {
            to_chat(owner, span_danger(I18n.T("You feel a splitting pain in your head, and are struck with a wave of nausea. You cannot hear the hivemind anymore!")));
            await owner.emote("scream");
            await owner.Paralyze(100);
        }

        await owner.adjust_jitter(1 * MINUTES);
        await owner.adjust_confusion(30 * SECONDS);
        await owner.adjust_stutter(1 * MINUTES);

        recent_queen_death = TRUE;
        owner.throw_alert(ALERT_XENO_NOQUEEN, typeof(atom_movable_screen_alert_alien_vulnerable));
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_queen_death)), QUEEN_DEATH_DEBUFF_DURATION, file: "code/modules/mob/living/carbon/alien/organs.dm", line: 145);
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:148
    public virtual void clear_queen_death()
    {
        if (QDELETED(this)) //In case the node is deleted
            return;
        recent_queen_death = FALSE;
        if (owner == null) //In case the xeno is butchered or subjected to surgery after death.
            return;
        to_chat(owner, span_noticealien(I18n.T("The pain of the queen's death is easing. You begin to hear the hivemind again.")));
        owner.clear_alert(ALERT_XENO_NOQUEEN);
    }
}

/// The stomach that lets aliens eat people/things
// tg: code/modules/mob/living/carbon/alien/organs.dm:192
public partial class obj_item_organ_stomach_alien : obj_item_organ_stomach
{
    // tg: code/modules/mob/living/carbon/alien/organs.dm:199
    [DmParams("nomnom")]
    public override object stomach_acid_power(atom_movable nomnom = null, object seconds_per_tick = null)
    {
        return 75;
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:202
    public override async DmTask<object> consume_thing(atom_movable thing = null)
    {
        object dot = await base.consume_thing(thing);
        if (isliving(thing))
            RegisterSignal(thing, COMSIG_LIVING_DEATH, nameof(content_died));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:207
    public override void content_moved(atom_movable source = null)
    {
        base.content_moved(source);
        if (source.loc == this || source.loc == owner)
            return;
        UnregisterSignal(source, COMSIG_LIVING_DEATH);
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:213
    [SignalHandler]
    public virtual void content_died(atom_movable source = null)
    {
        // Can fully digest corpses
        qdel(source);
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:218
    [DmParams("stomach_owner", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon stomach_owner = organ_owner;
        RegisterSignal(stomach_owner, COMSIG_ATOM_RELAYMOVE, nameof(something_moved));
        return await base.on_mob_insert(stomach_owner, special, movement_flags);
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:222
    [DmParams("stomach_owner", "special", "movement_flags")]
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        object stomach_owner = organ_owner;
        UnregisterSignal((datum)stomach_owner, COMSIG_ATOM_RELAYMOVE);
        await base.on_mob_remove(stomach_owner, special, movement_flags);
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:226
    [SignalHandler]
    public virtual object something_moved(mob_living source = null, mob_living user = null, object direction = null)
    {
        Waiting.NoSleep(relaymove(user, direction));
        return COMSIG_BLOCK_RELAYMOVE;
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:231
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (!Dm.@in(user, this.stomach_contents))
            return null;
        if (!prob(40))
            return null;
        atom play_from = (atom)owner ?? this;
        string stomach_text = owner != null ? I18n.T("\\the []'s stomach", owner) : I18n.T("\\the []", this);
        if (prob(25))
        {
            play_from.audible_message(span_warning(I18n.T("You hear something rumbling inside []...", stomach_text)),
                span_warning(I18n.T("You hear something rumbling.")), 4,
                self_message: span_userdanger(I18n.T("Something is rumbling inside your stomach!")));
        }

        if (user.client != null)
            user.client.move_delay = world.time + 1.5f * SECONDS;

        object attack_name = "";
        object attack_verb = "";
        float impact = 0;
        obj_item pokie = user.get_active_held_item() as obj_item;
        if (pokie != null)
        {
            float dmg = Dm.num(Dm.truthy(pokie.force) ? pokie.force : 0);
            DmList attack_verbs = pokie.attack_verb_continuous;
            impact = rand(round(dmg / 4), dmg);
            attack_name = pokie.name;
            attack_verb = Dm.truthy(Dm.length(attack_verbs)) ? Dm.text("[]", pick(attack_verbs)) : I18n.T("attacks");
        }

        if (!Dm.truthy(impact))
            return null;

        await apply_organ_damage(impact);

        float damage_ratio = Dm.num(damage) / max(maxHealth, 1);
        if (owner != null)
        {
            obj_item_bodypart part = owner.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart;
            // Brute damage to the mob is less then to the organ, so there's a higher chance of the explosion happening before xeno death
            await owner.apply_damage(impact / 2, BRUTE, part);
            // We choose the option that's best for the check
            float part_dam_ratio = Dm.num(part.brute_dam) / max(Dm.num(part.max_damage), 1);
            if (damage_ratio < part_dam_ratio)
                damage_ratio = part_dam_ratio;
        }

        play_from.visible_message(span_danger(I18n.T("[] [] [] wall with the []!", user, attack_verb, stomach_text, attack_name)),
                span_userdanger(I18n.T("[] [] your stomach wall with the []!", user, attack_verb, attack_name)));

        // At 100% damage, the stomach burts
        // Otherwise, we give them a -50% -> 50% chance scaling with damage dealt
        if (!prob((damage_ratio * 100) - 50) && damage_ratio != 1)
        {
            playsound(play_from, Dm.resource("sound/mobs/non-humanoids/alien/alien_organ_cut.ogg"), 100, 1);
            // We try and line up the "jump" here with the sound of the hit
            float oldx = play_from.pixel_x;
            float oldy = play_from.pixel_y;
            animate(play_from, Dm.named("pixel_x", oldx), Dm.named("pixel_y", oldx), 0.1f * SECONDS);
            float newx = oldx + Dm.num(at_least(rand(-8, 8), 2));
            float newy = oldy + Dm.num(at_least(rand(-8, 8), 2));
            // Here's a bit before the hit
            animate(Dm.named("pixel_x", newx), Dm.named("pixel_y", newx), 0.15f * SECONDS, Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
            newx += Dm.num(at_least(rand(-4, 4), 1));
            newy += Dm.num(at_least(rand(-4, 4), 1));
            // Here's a bit after the hit, we've got maybe 2 ticks to add a bit more juice
            animate(Dm.named("pixel_x", newy), Dm.named("pixel_y", newx), 0.1f * SECONDS);
            // Now we're gonna walk back to rest in maybe 3 ticks?
            animate(Dm.named("pixel_x", oldx), Dm.named("pixel_y", oldx), 0.5f * SECONDS);

            shake_camera(user, 0.1f * SECONDS, 0.5f);
            if (owner != null)
                shake_camera(owner, 0.3f * SECONDS, 1.5f);
            return null;
        }
        // Failure condition
        if (isalienadult(user))
        {
            play_from.visible_message(span_danger(I18n.T("[] blows a hole in [] and escapes!", user, stomach_text)),
                span_userdanger(I18n.T("As your hive's food bursts out of your stomach, one thought fills your mind. \"Oh, so this is how the other side feels\"")));
        }
        else // Just to be safe ya know?
        {
            play_from.visible_message(span_danger(I18n.T("[] blows a hole in [] and escapes!", user, stomach_text)),
                span_userdanger(I18n.T("[] escapes from your []. Hell, that hurts.", user, stomach_text)));
        }

        playsound(get_turf(play_from), Dm.resource("sound/mobs/non-humanoids/alien/alien_explode.ogg"), 100, extrarange: 4);
        await eject_stomach(border_diamond_range_turfs(play_from, 6), 5, 1.5f, 1, 8);
        shake_camera(user, 1 * SECONDS, 3);
        if (owner != null)
        {
            shake_camera(owner, 2, 5);
            owner.investigate_log(I18n.T("has been gibbed by something inside [] stomach.", owner.p_their()), INVESTIGATE_DEATHS);
            await owner.gib(DROP_ALL_REMAINS);
        }
        qdel(this);
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:315
    public override async DmTask<object> empty_contents(float? chance = null, float? damaging = null, float? min_amount = null)
    {
        chance ??= 100;
        damaging ??= FALSE;
        min_amount ??= 0;
        // Under high pressure from all the acid!
        return await eject_stomach(border_diamond_range_turfs(get_turf(this), 3), 3, 1, 1, 4);
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:319
    [SignalHandler]
    public override void on_vomit(mob_living_carbon vomiter = null, object distance = null, object force = null)
    {
        // If you get a xeno to vomit, you may be able to recover your comrades' corpses
        Waiting.NoSleep(empty_contents(chance: 100));
    }

    // tg: code/modules/mob/living/carbon/alien/organs.dm:323
    public virtual async DmTask<object> eject_stomach(DmList targets = null, object spit_range = null, object content_speed = null, object particle_delay = null, float? particle_count = null)
    {
        particle_count ??= 4;
        atom spit_as = (atom)owner ?? this;
        float ejected = Dm.length(stomach_contents);
        // Throw out the stuff in our stomach
        foreach (atom_movable thing in Dm.each_cast<atom_movable>(stomach_contents))
        {
            await thing.forceMove(spit_as.drop_location() as atom);
            if (Dm.truthy(Dm.length(targets)))
                await thing.throw_at(pick(targets) as atom, spit_range, content_speed, thrower: spit_as, spin: TRUE);
        }

        for (float a = 1, a__end = Dm.num(particle_count); a <= a__end; a++)
        {
            if (!Dm.truthy(Dm.length(targets)))
                break;
            obj_effect_particle_effect_water_extinguisher_stomach_acid acid = Dm.@new<obj_effect_particle_effect_water_extinguisher_stomach_acid>(get_turf(spit_as));
            turf my_target = pick_n_take(targets) as turf;
            datum_reagents acid_reagents = Dm.@new<datum_reagents>(5);
            acid.reagents = acid_reagents;
            acid_reagents.my_atom = acid;
            await acid_reagents.add_reagent(typeof(datum_reagent_toxin_acid), 30);
            acid.move_at(my_target, particle_delay, spit_range);
        }

        return ejected;
    }
}
