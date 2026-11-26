// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/light_eater.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: the element behind the nightmare's light eater (blade): whatever the carrier strikes loses its
// light for good. The var block is generated (D111).

public sealed partial class TgState
{
    // tg: code/datums/elements/light_eater.dm:7  var/static/list/blacklisted_areas
    internal DmList datum_element_light_eater__blacklisted_areas = typecacheof(Dm.list(
        typeof(turf_open_space),
        typeof(turf_open_lava))) as DmList;
}

/**
 * Makes anything it attaches to capable of permanently removing something's ability to produce light.
 *
 * The temporary equivalent is [/datum/component/light_eater]
 */
// tg: code/datums/elements/light_eater.dm:5
public partial class datum_element_light_eater : datum_element
{
    // tg: code/datums/elements/light_eater.dm:11
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        if (isatom(target))
        {
            if (ismovable(target))
            {
                if (ismachinery(target) || isstructure(target))
                    RegisterSignal(target, COMSIG_PROJECTILE_ON_HIT, nameof(on_projectile_hit));
                RegisterSignal(target, COMSIG_MOVABLE_IMPACT, nameof(on_throw_impact));
                if (isitem(target))
                {
                    if (isgun(target))
                        RegisterSignal(target, COMSIG_PROJECTILE_ON_HIT, nameof(on_projectile_hit));
                    RegisterSignal(target, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(on_interacting_with));
                    RegisterSignal(target, COMSIG_ITEM_HIT_REACT, nameof(on_hit_reaction));
                }
                else if (isprojectile(target))
                {
                    RegisterSignal(target, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(on_projectile_self_hit));
                }
            }
        }
        else if (istype(target, typeof(datum_reagent)))
        {
            RegisterSignal(target, COMSIG_REAGENT_EXPOSE_ATOM, nameof(on_expose_atom));
        }
        else if (isprojectilespell(target))
        {
            RegisterSignal(target, COMSIG_PROJECTILE_ON_HIT, nameof(on_projectile_hit));
        }
        else
        {
            return ELEMENT_INCOMPATIBLE;
        }

        return await base.Attach(target, __extra);
    }

    // tg: code/datums/elements/light_eater.dm:34
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(
            COMSIG_MOVABLE_IMPACT,
            COMSIG_ITEM_INTERACTING_WITH_ATOM,
            COMSIG_ITEM_HIT_REACT,
            COMSIG_PROJECTILE_ON_HIT,
            COMSIG_REAGENT_EXPOSE_ATOM));
        return base.Detach(source, __extra);
    }

    /**
     * Makes the light eater consume all of the lights attached to the target atom.
     *
     * Arguments:
     * - [food][/atom]: The atom to start the search for lights at.
     * - [eater][/datum]: The light eater being used in this case.
     */
    // tg: code/datums/elements/light_eater.dm:50
    public virtual float eat_lights(atom food = null, datum eater = null)
    {
        DmList buffet = table_buffet(food) as DmList;
        if (length(buffet) == 0)
            return 0;

        float dot = 0;
        foreach (object morsel in Dm.each(buffet))
            dot += devour(morsel as atom, eater);

        if (dot == 0)
            return dot;

        food.visible_message(
            span_danger(I18n.T("Something dark in [] lashes out at [] and [] light goes out in an instant!", eater, food, food.p_their())),
            span_userdanger(I18n.T("You feel something dark in [] lash out and gnaw through your light in an instant! It recedes just as fast, but you can feel that [] left something hungry behind.", eater, eater.p_theyve())),
            span_danger(I18n.T("You feel a gnawing pulse eat at your sight.")));
        return dot;
    }

    /**
     * Aggregates a list of the light sources attached to the target atom.
     *
     * Arguments:
     * - [comissary][/atom]: The origin node of all of the light sources to search through.
     * - [devourer][/datum]: The light eater this element is attached to. Since the element is compatible with reagents this needs to be a datum.
     */
    // tg: code/datums/elements/light_eater.dm:72
    public virtual object table_buffet(atom commisary = null, datum devourer = null)
    {
        DmList dot = Dm.list();
        if (Dm.truthy(commisary._listen_lookup?[COMSIG_LIGHT_EATER_QUEUE]))
            commisary._SendSignal(COMSIG_LIGHT_EATER_QUEUE, Dm.list(commisary, dot, devourer));
        foreach (datum_light_source morsel in Dm.each<datum_light_source>(commisary.light_sources))
            dot[morsel.source_atom] = TRUE;
        return dot;
    }

    /**
     * Consumes the light on the target, permanently rendering it incapable of producing light
     *
     * Arguments:
     * - [morsel][/atom]: The light-producing thing we are eating
     * - [eater][/datum]: The light eater eating the morsel. This is the datum that the element is attached to that started this chain.
     */
    // tg: code/datums/elements/light_eater.dm:86
    public virtual float devour(atom morsel = null, datum eater = null)
    {
        if (Dm.truthy(is_type_in_typecache(morsel, TgState.Current.datum_element_light_eater__blacklisted_areas)))
            return FALSE;
        if (istransparentturf(morsel))
            return FALSE;
        if (morsel.light_power <= 0 || morsel.light_range <= 0 || !Dm.truthy(morsel.light_on))
            return FALSE;
        if (Dm.truthy(Dm.bitand(!Dm.truthy(morsel._listen_lookup?[COMSIG_LIGHT_EATER_ACT]) ? NONE : morsel._SendSignal(COMSIG_LIGHT_EATER_ACT, Dm.list(morsel, eater)), COMPONENT_BLOCK_LIGHT_EATER)))
            return FALSE; // Either the light eater can't eat it or it had special behaviors.

        Waiting.NoSleep(morsel.AddElement(typeof(datum_element_light_eaten)));
        if (Dm.truthy(_listen_lookup?[COMSIG_LIGHT_EATER_DEVOUR]))
            _SendSignal(COMSIG_LIGHT_EATER_DEVOUR, Dm.list(this, morsel));
        return TRUE;
    }

    /////////////////////
    // SIGNAL HANDLERS //
    /////////////////////

    /**
     * Called when a movable source is thrown and strikes a target
     *
     * Arugments:
     * - [source][/atom/movable]: The movable atom that was thrown
     * - [hit_atom][/atom]: The target atom that was struck by the source in flight
     * - [thrownthing][/datum/thrownthing]: A datum containing the information for the throw
     */
    // tg: code/datums/elements/light_eater.dm:114
    [SignalHandler]
    public virtual object on_throw_impact(atom_movable source = null, atom hit_atom = null, datum_thrownthing thrownthing = null)
    {
        eat_lights(hit_atom, source);
        return NONE;
    }

    /**
     * Called when a target is interacted with by a source item
     *
     * Arguments:
     * - [source][/obj/item]: The item what was used to strike the target
     * - [user][/mob/living]: The mob using the source to strike the target
     * - [target][/atom]: The atom being struck by the user with the source
     */
    // tg: code/datums/elements/light_eater.dm:127
    [SignalHandler]
    public virtual object on_interacting_with(obj_item source = null, mob_living user = null, atom target = null)
    {
        if (eat_lights(target, source) != 0)
        {
            if (ismob(target))
            {
                mob hit_user = (mob)target;
                if (hit_user.pulling != null)
                {
                    atom pulled_thing = hit_user.pulling; // potentially dragging a light
                    if (!isliving(pulled_thing)) // we don't want conga lines to be affected
                        eat_lights(pulled_thing, source);
                }
            }
            // do a "pretend" attack if we're hitting something that can't normally be
            if (isobj(target))
            {
                obj smacking = (obj)target;
                if (Dm.truthy(Dm.bitand(smacking.obj_flags, CAN_BE_HIT)))
                    return NONE;
            }
            else if (!isturf(target))
            {
                return NONE;
            }
            Waiting.NoSleep(user.do_attack_animation(target));
            user.changeNext_move(CLICK_CD_RAPID);
            target.play_attack_sound();
        }
        // not particularly picky about what happens afterwards in the attack chain
        return NONE;
    }

    /**
     * Called when a source object is used to block a thrown object, projectile, or attack
     *
     * Arguments:
     * - [source][/obj/item]: The item what was used to block the target
     * - [owner][/mob/living/carbon/human]: The mob that blocked the target with the source
     * - [hitby][/atom/movable]: The movable that was blocked by the owner with the source
     * - attack_text: The text tring that will be used to report that the target was blocked
     * - final_block_chance: The probability of blocking the target with the source
     * - attack_type: The type of attack that was blocked
     */
    // tg: code/datums/elements/light_eater.dm:158
    [SignalHandler]
    public virtual object on_hit_reaction(obj_item source = null, mob_living_carbon_human owner = null, atom_movable hitby = null, object attack_text = null, object final_block_chance = null, object damage = null, object attack_type = null, object damage_type = null)
    {
        if (prob(Dm.num(final_block_chance)))
            eat_lights(hitby, source);
        return NONE;
    }

    /**
     * Called when a produced projectile strikes a target atom
     *
     * Arguments:
     * - [source][/datum]: The thing that created the projectile
     * - [firer][/atom/movable]: The movable atom that fired the projectile
     * - [target][/atom]: The atom that was struck by the projectile
     * - angle: The angle the target was struck at
     */
    // tg: code/datums/elements/light_eater.dm:170
    [SignalHandler]
    public virtual object on_projectile_hit(datum source = null, atom_movable firer = null, atom target = null, object angle = null)
    {
        eat_lights(target, source);
        return NONE;
    }

    /**
     * Called when a source projectile strikes a target atom
     *
     * Arguments:
     * - [source][/obj/projectile]: The projectile striking the target atom
     * - [firer][/atom/movable]: The movable atom that fired the projectile
     * - [target][/atom]: The atom that was struck by the projectile
     * - angle: The angle the target was struck at
     * - hit_limb: The limb that was hit, if the target was a carbon
     */
    // tg: code/datums/elements/light_eater.dm:184
    [SignalHandler]
    public virtual object on_projectile_self_hit(obj_projectile source = null, atom_movable firer = null, atom target = null, object angle = null, object hit_limb = null)
    {
        eat_lights(target, source);
        return NONE;
    }

    /**
     * Called when a source reagent exposes a target atom
     *
     * Arguments:
     * - [source][/datum/reagent]: The reagents that exposed the target atom
     * - [target][/atom]: The atom that was exposed to the light reater reagents
     * - reac_volume: The volume of the reagents the target was exposed to
     */
    // tg: code/datums/elements/light_eater.dm:198
    [SignalHandler]
    public virtual object on_expose_atom(datum_reagent source = null, atom target = null, object reac_volume = null, object methods = null)
    {
        eat_lights(target, source);
        return NONE;
    }
}
