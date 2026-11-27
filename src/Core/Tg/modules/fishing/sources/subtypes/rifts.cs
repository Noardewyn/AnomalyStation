// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/sources/subtypes/rifts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the autowiki's table and the heretic's gains (rows of not-translated.md section 251): the space dragon's
// carp rifts and the heretic's dimensional rifts, which give arms, the limbs they ate and glimpses of the Mansus, and rip
// the arm off a non-heretic who loses the minigame. The var blocks are generated (D111).

// tg: code/modules/fishing/sources/subtypes/rifts.dm:29
public partial class datum_fish_source_dimensional_rift : datum_fish_source
{
    /**
     * You can fish up random arms, but you can also fish up arms (or heads, from TK) that were eaten at some point by a rift.
     * No need to check for what the location is, just get its limbs from its contents. It should always be a visible heretic rift. Should.
     */
    // tg: code/modules/fishing/sources/subtypes/rifts.dm:70
    public override object get_fish_table(atom location = null, float? from_explosion = null)
    {
        from_explosion ??= FALSE;
        DmList dot = base.get_fish_table(location, from_explosion) as DmList;
        if (istype(location, typeof(obj_machinery_fishing_portal_generator)))
        {
            obj_machinery_fishing_portal_generator portal = (obj_machinery_fishing_portal_generator)location;
            location = portal.current_linked_atom;
        }

        foreach (obj_item eaten_thing in Dm.each<obj_item>(location.get_all_contents()))
            dot[eaten_thing] = 6;
        return dot;
    }

    // tg: code/modules/fishing/sources/subtypes/rifts.dm:79
    public override object on_challenge_completed(mob user = null, datum_fishing_challenge challenge = null, object success = null)
    {
        object dot = base.on_challenge_completed(user, challenge, success);

        if (!Dm.truthy(success))
        {
            if (IS_HERETIC(user))
                return dot;
            if (!Dm.truthy(user.get_active_hand()))
            {
                Waiting.NoSleep(influence_fished(user, challenge));
                return null;
            }
            Waiting.NoSleep(on_epic_fail(user, challenge, success));
            return dot;
        }

        if (Dm.eq(challenge.reward_path, FISHING_INFLUENCE))
        {
            Waiting.NoSleep(influence_fished(user, challenge));
            return dot;
        }

        return dot;
    }

    /**
     * Override for influences and arms.
     */
    // tg: code/modules/fishing/sources/subtypes/rifts.dm:99
    public override async DmTask<object> spawn_reward(object reward_path = null, atom spawn_location = null, atom fishing_spot = null, params object[] __extra)
    {
        if (Dm.eq(reward_path, FISHING_INFLUENCE))
            return null;
        if (Dm.eq(reward_path, FISHING_RANDOM_ARM))
            return arm_fished(spawn_location);
        return await base.spawn_reward(reward_path, spawn_location, fishing_spot, __extra);
    }

    /**
     * This happens when a non-heretic fails the minigame. Their arm is ripped straight off and thrown into the rift.
     */
    // tg: code/modules/fishing/sources/subtypes/rifts.dm:110
    public virtual async DmTask on_epic_fail(mob user = null, datum_fishing_challenge challenge = null, object success = null)
    {
        challenge.location.visible_message(span_danger(I18n.T("[]'s tendrils lash out and pull on []'s [], ripping it clean off and throwing it towards itself!", challenge.location, user, user.get_active_hand())));
        obj_item_bodypart random_arm = user.get_active_hand() as obj_item_bodypart;
        if (Dm.truthy(await random_arm.dismember(BRUTE, FALSE)))
        {
            await random_arm.forceMove(user.drop_location() as atom);
            await random_arm.throw_at(challenge.location, 7, 1, null, TRUE);
        }
        // Abstract items shouldn't be thrown in!
        if (!Dm.truthy(Dm.bitand(challenge.used_rod.item_flags, ABSTRACT)))
        {
            await challenge.used_rod.forceMove(user.drop_location() as atom);
            await challenge.used_rod.throw_at(challenge.location, 7, 1, null, TRUE);
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(check_item_location), challenge.location, challenge.used_rod), 1 * SECONDS, file: "code/modules/fishing/sources/subtypes/rifts.dm", line: 121);
    }

    // tg: code/modules/fishing/sources/subtypes/rifts.dm:123
    public virtual async DmTask check_item_location(atom location = null, obj_item used_rod = null)
    {
        foreach (obj_item thingy in Dm.each<obj_item>(get_turf(location)))
        {
            // If it's not in the list and it's not what we know as the used rod, skip.
            // This lets fishing gloves be dragged in as well. I mean honestly if you try fishing in here with those you should just Fucking Die but that's for later.
            if (!Dm.truthy(is_type_in_list(thingy, Dm.list(typeof(obj_item_bodypart), typeof(obj_item_fishing_rod)))) && thingy != used_rod)
                continue;
            await thingy.forceMove(location);
            location.visible_message(span_danger(I18n.T("Tendrils lash out from [] and greedily drag [] inwards. You're probably never seeing [] again.", location, thingy, thingy)));
        }
    }

    // tg: code/modules/fishing/sources/subtypes/rifts.dm:132
    public virtual object arm_fished(atom spawn_location = null)
    {
        Type random_arm_path = pick(subtypesof(typeof(obj_item_bodypart_arm))) as Type;
        obj_item_bodypart_arm random_arm = Dm.@new(random_arm_path, spawn_location) as obj_item_bodypart_arm;
        spawn_location.visible_message(span_notice(I18n.T("A [] is snatched up from beneath the eldritch depths of []!", random_arm, spawn_location)));
        return random_arm;
    }

    // tg: code/modules/fishing/sources/subtypes/rifts.dm:138
    public virtual async DmTask influence_fished(mob user = null, datum_fishing_challenge challenge = null)
    {
        if (!Dm.eq(challenge.reward_path, FISHING_INFLUENCE))
            return;
        mob_living_carbon_human human_user = null;
        if (ishuman(user))
            human_user = (mob_living_carbon_human)user;

        user.visible_message(span_danger(I18n.T("[] reels [] [] in, catching a glimpse into the world beyond!", user, user.p_their(), challenge.used_rod)), span_notice(I18n.T("You catch.. a glimpse into the workings of the Mansus itself!")));
        // Heretics that fish in the rift gain knowledge.
        if (IS_HERETIC(user))
        {
            if (human_user != null)
                await human_user.add_mood_event("rift fishing", typeof(datum_mood_event_rift_fishing));
            obj_effect_heretic_influence fishfluence = challenge.location as obj_effect_heretic_influence;
            // But only if it's an open rift
            if (fishfluence == null)
            {
                to_chat(user, span_notice(I18n.T("You glimpse something fairly uninteresting.")));
                return;
            }
            // GAP(heretic): /obj/effect/heretic_influence/proc/after_drain and /datum/antagonist/heretic/proc/adjust_knowledge_points
            // (antagonists/heretic) are not declared; until the heretic is ported the rift is not drained and no knowledge
            // point is given (rifts.dm:155, 158, 162).
            datum_antagonist_heretic heretic_datum = GET_HERETIC(user);
            if (heretic_datum != null)
            {
                to_chat(user, I18n.T("[] []", span_hear(I18n.T("You hear a whisper...")), span_mansus(I18n.T("THE HIGHER I RISE, THE MORE I FISH."))));
                to_chat(user, span_boldnotice(I18n.T("Your infused rod improves your knowledge gain!")));
            }
            return;
        }

        // Non-heretics instead go crazy
        if (human_user != null)
        {
            await human_user.adjust_organ_loss(ORGAN_SLOT_BRAIN, 10, 190);
            await human_user.add_mood_event("gates_of_mansus", typeof(datum_mood_event_gates_of_mansus));
            human_user.do_jitter_animation(50);
        }
        // Hand fires at them from the location
        await fire_curse_hand(user as mob_living_carbon, get_turf(challenge.location));
    }
}
