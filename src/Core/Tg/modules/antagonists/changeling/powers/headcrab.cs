// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/headcrab.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Last Resort, the changeling's body swelling and bursting in a gore explosion that blinds and
// splatters the bystanders, the walls of a cage torn down, and the headslug the changeling lives on in. The var block is
// generated (D111).

// tg: code/modules/antagonists/changeling/powers/headcrab.dm:4
public partial class datum_action_changeling_headcrab : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/headcrab.dm:1  #define LAST_RESORT_EXPLOSION_RANGE 2 (file-local, #undef at :144)
    private const int LAST_RESORT_EXPLOSION_RANGE = 2;
    // tg: code/modules/antagonists/changeling/powers/headcrab.dm:2  #define LAST_RESORT_BLIND_RANGE 4 (file-local, #undef at :145)
    private const int LAST_RESORT_BLIND_RANGE = 4;

    // tg: code/modules/antagonists/changeling/powers/headcrab.dm:16
    public override object can_be_used_by(mob_living user = null)
    {
        if (HAS_TRAIT(user, TRAIT_TEMPORARY_BODY))
            return FALSE;
        if (isanimal_or_basicmob(user) && !(user is mob_living_basic_headslug) && !isconstruct(user) && !Dm.truthy(Dm.bitand(user.mob_biotypes, MOB_SPIRIT)))
            return TRUE;
        return base.can_be_used_by(user);
    }

    // tg: code/modules/antagonists/changeling/powers/headcrab.dm:23  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4, an async
    // method in this DmTask slot; it never writes '.', so it has no box)
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        return Waiting.WaitforFalse(sting_action__body(user, target, null), null);
    }

    // The body of the set waitfor = FALSE wrapper above (waiting-model.md 5.4).
    private async DmTask<object> sting_action__body(mob_living user, mob_living target, DotBox<object> dot)
    {
        object confirm = await tgui_alert(user, I18n.T("Are we sure we wish to destroy our body and create a headslug?"), I18n.T("Last Resort"), Dm.list(I18n.T("Yes"), I18n.T("No")));
        if (Dm.truthy(active) || !Dm.eq(confirm, I18n.T("Yes")))
            return null;
        active = TRUE;
        await base.sting_action(user, target);
        user.visible_message(span_boldwarning(I18n.T("[]'s body begins to pulsate and swell unnaturally!", user)));
        playsound(user, Dm.resource("sound/effects/wounds/crack1.ogg"), 100, TRUE);
        animate(user, Dm.named("transform", Dm.mul(user.transform, 1.5f)), Dm.named("color", COLOR_RED), Dm.named("time", 1 * SECONDS));
        if (Dm.truthy(await is_walled(user)))
        {
            await user.Immobilize(1.5f * SECONDS); // to prevent the breaking of the wrong walls (who would think of using the antistun after the last resort)
            await escaping_prison(user);
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(gibbing), user), 1.1f * SECONDS, file: "code/modules/antagonists/changeling/powers/headcrab.dm", line: 36);
        return null;
    }

    // tg: code/modules/antagonists/changeling/powers/headcrab.dm:38
    public virtual async DmTask gibbing(mob_living user = null)
    {
        if (QDELETED(user))
        {
            active = FALSE;
            return;
        }
        await gore_explosion(user);

        datum_mind stored_mind = user.mind;
        DmList organs = user.get_organs_for_zone(BODY_ZONE_HEAD, TRUE) as DmList;
        turf user_turf = get_turf(user) as turf;

        await user.transfer_observers_to(user_turf); // user is about to be deleted, store orbiters on the turf
        if (user.stat != DEAD)
            user.investigate_log("has been gibbed by headslug burst.", INVESTIGATE_DEATHS);
        await user.gib(DROP_ALL_REMAINS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(spawn_headcrab), stored_mind, user_turf, organs), 0.5f * SECONDS, file: "code/modules/antagonists/changeling/powers/headcrab.dm", line: 52); // without this delay the worm will be almost dead due to the explosion :(
    }

    /// Creates a light explosion, blinds and confuses mobs in range
    // tg: code/modules/antagonists/changeling/powers/headcrab.dm:55
    public virtual async DmTask gore_explosion(mob_living user = null)
    {
        DmList user_DNA = user.get_blood_dna_list() as DmList;
        user.visible_message(span_boldwarning(I18n.T("[]'s body ruptures in a violent explosion of biomass!", user)));
        playsound(user, Dm.resource("sound/effects/goresplat.ogg"), 100, TRUE); //yuck!!
        // explosion_cause = src: the port's explosion() takes an atom cause (its log names it), and the changeling power is a datum
        await explosion(user, light_impact_range: LAST_RESORT_EXPLOSION_RANGE, flame_range: 0, flash_range: 0, adminlog: TRUE, silent: TRUE, explosion_cause: null);
        await user.spawn_gibs();

        foreach (turf bloody_turf in Dm.each<turf>(view(LAST_RESORT_BLIND_RANGE, user)))
        {
            obj_effect_decal_cleanable_blood blood_spot = Dm.@new<obj_effect_decal_cleanable_blood>(bloody_turf);
            await blood_spot.add_blood_DNA(user_DNA);
        }

        foreach (mob_living blinded in Dm.each<mob_living>(view(LAST_RESORT_BLIND_RANGE, user)))
        {
            if (blinded == user)
                continue;
            blinded.visible_message(span_danger(I18n.T("[] is splattered with blood!", blinded)), span_userdanger(I18n.T("You're splattered with blood!")));
            await blinded.add_blood_DNA(user_DNA);
            await blinded.add_mood_event("splattered_with_blood", typeof(datum_mood_event_splattered_with_blood));
            playsound(blinded, Dm.resource("sound/effects/splat.ogg"), 50, TRUE, extrarange: SILENCED_SOUND_EXTRARANGE);

            if (ishuman(blinded))
            {
                mob_living_carbon_human blinded_human = blinded as mob_living_carbon_human;
                obj_item_organ_eyes eyes = blinded_human.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
                if (eyes == null || Dm.truthy(blinded_human.is_blind()))
                    continue;
                to_chat(blinded_human, span_userdanger(I18n.T("You are blinded by a shower of blood!")));
                await blinded_human.Stun(4 * SECONDS);
                await blinded_human.set_eye_blur_if_lower(40 * SECONDS);
                await blinded_human.adjust_confusion(12 * SECONDS);
            }
            if (issilicon(blinded))
            {
                mob_living_silicon blinded_silicon = blinded as mob_living_silicon;
                to_chat(blinded_silicon, span_userdanger(I18n.T("Your sensors are disabled by a shower of blood!")));
                await blinded_silicon.Paralyze(6 * SECONDS);
            }
        }
    }

    /// Creates the headrab to occupy
    // tg: code/modules/antagonists/changeling/powers/headcrab.dm:89
    public virtual async DmTask spawn_headcrab(datum_mind stored_mind = null, turf spawn_location = null, DmList organs = null)
    {
        mob_living_basic_headslug crab = Dm.@new<mob_living_basic_headslug>(spawn_location);
        foreach (obj_item_organ I in Dm.each<obj_item_organ>(organs))
            await I.forceMove(crab);

        await stored_mind.transfer_to(crab, force_key_move: TRUE);
        await spawn_location.transfer_observers_to(crab);
        to_chat(crab, span_warning(I18n.T("We burst out of the remains of our former body in a shower of gore!")));
        active = FALSE;
    }

    /// Ruptures nearby walls using the torn_wall component. Also it destroys objects with density.
    // tg: code/modules/antagonists/changeling/powers/headcrab.dm:100
    public virtual async DmTask escaping_prison(mob_living user = null)
    {
        user.visible_message(span_boldwarning(I18n.T("[]'s expanding form begins crushing the surrounding obstacles!", user)));
        DmList walls_to_destroy = Dm.list();

        foreach (turf nearby_turf in Dm.each<turf>(range(1, user)))
        {
            if (iswallturf(nearby_turf))
            {
                turf_closed_wall nearby_wall = nearby_turf as turf_closed_wall;
                datum_component_torn_wall torn_comp = nearby_wall.GetComponent(typeof(datum_component_torn_wall)) as datum_component_torn_wall;
                if (torn_comp == null)
                    await nearby_wall.AddComponent(typeof(datum_component_torn_wall), Dm.named("current_stage", 1));
                else
                    torn_comp.current_stage = 1;
                Dm.append(ref walls_to_destroy, nearby_wall);
                continue;
            }
            foreach (obj obj_obstacle in Dm.each<obj>(nearby_turf))
            {
                if (!Dm.truthy(obj_obstacle.density) || Dm.truthy(Dm.bitand(obj_obstacle.resistance_flags, INDESTRUCTIBLE)) || !Dm.truthy(obj_obstacle.anchored))
                    continue;
                if (Dm.truthy(obj_obstacle.uses_integrity))
                    await obj_obstacle.take_damage(300);
            }
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(finalize_destruction), walls_to_destroy, user, user.loc), 1 * SECONDS, file: "code/modules/antagonists/changeling/powers/headcrab.dm", line: 120);
    }

    /// Completes the destruction of the walls after a 1-second delay for more drama
    // tg: code/modules/antagonists/changeling/powers/headcrab.dm:123
    public virtual async DmTask finalize_destruction(DmList affected_walls = null, mob_living user = null, atom user_prev_loc = null)
    {
        if (QDELETED(user) || (user.loc != user_prev_loc))
        {
            active = FALSE;
            return;
        }
        foreach (turf_closed_wall wall in Dm.each<turf_closed_wall>(affected_walls))
        {
            if (QDELETED(wall))
                continue;
            await wall.AddComponent(typeof(datum_component_torn_wall));
        }
    }

    // tg: code/modules/antagonists/changeling/powers/headcrab.dm:132
    public virtual async DmTask<object> is_walled(mob_living user = null)
    {
        turf ling_turf = get_turf(user) as turf;
        if (ling_turf == null)
            return FALSE;
        float blocked_directions = 0;

        foreach (object dir in Dm.each(GLOB.cardinals))
        {
            turf neighbor = get_step(ling_turf, Dm.num(dir));
            if (neighbor == null || Dm.truthy(await neighbor.is_blocked_turf(exclude_mobs: TRUE)))
                blocked_directions++;
        }
        return Dm.num(blocked_directions >= 3); //1x2 cage won't be able to contain linga
    }
}
