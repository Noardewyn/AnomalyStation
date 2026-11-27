// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the ash walkers' nest (its team and objective, the bodies it takes, the walkers it remakes and the eggs it lays), its
// collapse into a chasm when it is destroyed, and the tendril's light. The var blocks are generated.

//The ash walker den consumes corpses or unconscious mobs to create ash walker eggs. For more info on those, check ghost_role_spawners.dm
// tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:4
public partial class obj_structure_lavaland_ash_walker : obj_structure_lavaland
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:1
    private const float ASH_WALKER_SPAWN_THRESHOLD = 2;

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ashies = Dm.@new<datum_team_ashwalkers>();
        var objective = Dm.@new<datum_objective_protect_object>();
        objective.set_target(this);
        objective.team = ashies;
        linked_objective = objective;
        Dm.append(ref ashies.objectives, objective);
        START_PROCESSING(SSprocessing, this);
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:32
    public override object Destroy(float? force = null)
    {
        ashies = null;
        linked_objective = null;
        STOP_PROCESSING(SSprocessing, this);
        return base.Destroy(force);
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:38
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        var core_to_drop = (Type)pick(subtypesof(typeof(obj_item_assembly_signaler_anomaly)));
        Dm.@new(core_to_drop, get_step(loc, Dm.num(pick(GLOB.alldirs))));
        Dm.@new<obj_effect_collapse>(loc);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:43
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        Waiting.NoSleep(consume());
        spawn_mob();
        return null;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:47
    public virtual async DmTask consume()
    {
        foreach (mob_living offeredmob in Dm.each<mob_living>(view(1, this))) //Only for corpse right next to/on same tile
        {
            if (offeredmob.loc == this)
                continue; //Ashwalker Revive in Progress...
            if (!IS_UNCONSCIOUS_OR_CRIT(offeredmob))
                continue;
            await offeredmob.unequip_everything();

            if (issilicon(offeredmob)) //no advantage to sacrificing borgs...
            {
                offeredmob.investigate_log("has been gibbed by the necropolis tendril.", INVESTIGATE_DEATHS);
                visible_message(span_notice(I18n.T("Serrated tendrils eagerly pull [] apart, but find nothing of interest.", offeredmob)));
                await offeredmob.gib();
                return;
            }

            if (Dm.truthy(offeredmob.mind?.has_antag_datum(typeof(datum_antagonist_ashwalker))) && (Dm.truthy(offeredmob.ckey) || Dm.truthy(offeredmob.get_ghost(FALSE, TRUE)))) //special interactions for dead lava lizards with ghosts attached
            {
                visible_message(span_warning(I18n.T("Serrated tendrils carefully pull [] to [], absorbing the body and creating it anew.", offeredmob, this)));
                mob deadmob;
                if (Dm.truthy(offeredmob.ckey))
                    deadmob = offeredmob;
                else
                    deadmob = offeredmob.get_ghost(FALSE, TRUE) as mob;
                to_chat(deadmob, I18n.T("Your body has been returned to the nest. You are being remade anew, and will awaken shortly. </br><b>Your memories will remain intact in your new body, as your soul is being salvaged</b>"));
                // SEND_SOUND(deadmob, sound(...)) (__DEFINES/sound.dm: DIRECT_OUTPUT, rules 12)
                Dm.output_to(deadmob, sound(Dm.resource("sound/effects/magic/enter_blood.ogg"), volume: 100));
                _addtimer(Dm.@new<datum_callback>(this, nameof(remake_walker), offeredmob), 20 * SECONDS, file: "code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm", line: 70);
                await offeredmob.forceMove(this);
                return;
            }

            if (ismegafauna(offeredmob))
                meat_counter += 20;
            else
                meat_counter++;
            visible_message(span_warning(I18n.T("Serrated tendrils eagerly pull [] to [], tearing the body apart as its blood seeps over the eggs.", offeredmob, this)));
            playsound(get_turf(this), Dm.resource("sound/effects/magic/demon_consume.ogg"), 100, TRUE);
            object deliverykey = offeredmob.fingerprintslast; //ckey of whoever brought the body
            mob deliverymob = get_mob_by_key(deliverykey); //mob of said ckey
            //there is a 40% chance that the Lava Lizard unlocks their respawn with each sacrifice
            if (deliverymob != null && Dm.truthy(deliverymob.mind?.has_antag_datum(typeof(datum_antagonist_ashwalker))) && Dm.@in(deliverykey, ashies.players_spawned) && prob(40))
            {
                to_chat(deliverymob, span_warning(I18n.T("<b>The Necropolis is pleased with your sacrifice. You feel confident your existence after death is secure.</b>")));
                Dm.remove(ref ashies.players_spawned, deliverykey);
            }
            offeredmob.investigate_log("has been gibbed by the necropolis tendril.", INVESTIGATE_DEATHS);
            await offeredmob.gib(DROP_ALL_REMAINS);
            atom_integrity = Dm.min(Dm.num(atom_integrity) + max_integrity * 0.05f, max_integrity); //restores 5% hp of tendril
            foreach (mob_living L in Dm.each<mob_living>(view(5, this)))
            {
                if (Dm.truthy(L.mind?.has_antag_datum(typeof(datum_antagonist_ashwalker))))
                    await L.add_mood_event("oogabooga", typeof(datum_mood_event_sacrifice_good));
                else
                    await L.add_mood_event("oogabooga", typeof(datum_mood_event_sacrifice_bad));
            }
            ashies.sacrifices_made++;
        }
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:95
    public virtual async DmTask remake_walker(mob_living_carbon oldmob = null)
    {
        var newwalker = Dm.@new<mob_living_carbon_human>(get_step(loc, Dm.num(pick(GLOB.alldirs))));
        newwalker.set_species(typeof(datum_species_lizard_ashwalker));
        newwalker.real_name = oldmob.real_name;
        newwalker.undershirt = "Nude";
        newwalker.underwear = "Nude";
        await newwalker.update_body();
        newwalker.remove_language(typeof(datum_language_common));
        await oldmob.mind.transfer_to(newwalker);
        newwalker.mind.grab_ghost();
        to_chat(newwalker, I18n.T("<b>You have been pulled back from beyond the grave, with a new body and renewed purpose. Glory to the Necropolis!</b>"));
        playsound(get_turf(newwalker), Dm.resource("sound/effects/magic/exit_blood.ogg"), 100, TRUE);
        qdel(oldmob);
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:109
    public virtual void spawn_mob()
    {
        if (meat_counter >= ASH_WALKER_SPAWN_THRESHOLD)
        {
            Dm.@new<obj_effect_mob_spawn_ghost_role_human_ash_walker>(get_step(loc, Dm.num(pick(GLOB.alldirs))), ashies);
            visible_message(span_danger(I18n.T("One of the eggs swells to an unnatural size and tumbles free. It's ready to hatch!")));
            meat_counter -= ASH_WALKER_SPAWN_THRESHOLD;
            ashies.eggs_created++;
        }
    }
}

// tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:132
public partial class obj_effect_collapse : obj_effect
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:144
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        emitted_light = Dm.@new<obj_effect_light_emitter_tendril>(loc);
        visible_message(span_bolddanger(I18n.T("The tendril writhes in fury as the earth around it begins to crack and break apart! Get back!")));
        balloon_alert_to_viewers(I18n.T("interact to grab loot before collapse!"), vision_distance: 7);
        playsound(loc, Dm.resource("sound/effects/tendril_destroyed.ogg"), 200, FALSE, 50, TRUE, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(collapse)), 5 * SECONDS, file: "code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm", line: 150);
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:152
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList examine_messages = await base.examine(user, __extra) as DmList;
        if (isliving(user))
        {
            if (Dm.truthy(has_collected(user)))
                Dm.append(ref examine_messages, span_boldnotice(I18n.T("You've grabbed what you can, now get out!")));
            else
                Dm.append(ref examine_messages, span_boldnotice(I18n.T("You might have some time to grab some goodies with an open hand before it collapses!")));
        }
        return examine_messages;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:161
    [DmParams("collector", "modifiers")]
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        var collector = user as mob_living;
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(has_collected(collector)))
        {
            to_chat(collector, span_danger(I18n.T("You've already gotten some loot, just get out of there with it!")));
            return dot;
        }
        visible_message(span_warning(I18n.T("Something falls free of the tendril!")));
        var loot = Dm.@new<obj_structure_closet_crate_necropolis_tendril>(loc);
        await collector.start_pulling(loot);
        Dm.append(ref collected, WEAKREF(collector));
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:171
    public override object Destroy(float? force = null)
    {
        collected.Cut();
        QDEL_NULL(ref emitted_light);
        return base.Destroy(force);
    }

    ///Helper proc that resolves weakrefs to determine if collector is in collected list, returning a boolean.
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:177
    public virtual object has_collected(mob collector = null)
    {
        foreach (datum_weakref weakref in Dm.each<datum_weakref>(collected))
        {
            var resolved = weakref.resolve() as mob_living;
            //it could have been collector, it could not have been, we don't care
            if (resolved == null)
                continue;
            if (resolved == collector)
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/ash_walker_den.dm:187
    public virtual async DmTask collapse()
    {
        foreach (mob viewer in Dm.each<mob>(range(7, this)))
            shake_camera(viewer, 15, 1);
        playsound(get_turf(this), Dm.resource("sound/effects/explosion/explosionfar.ogg"), 200, TRUE);
        visible_message(span_bolddanger(I18n.T("The tendril falls inward, the ground around it widening into a yawning chasm!")));
        foreach (turf ground in Dm.each<turf>(RANGE_TURFS(2, this)))
        {
            if (HAS_TRAIT(ground, TRAIT_NO_TERRAFORM))
                continue;
            if (!Dm.truthy(ground.density))
                await ground.TerraformTurf(typeof(turf_open_chasm_lavaland), typeof(turf_open_chasm_lavaland), flags: CHANGETURF_INHERIT_AIR);
        }
        qdel(this);
    }
}
