// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/shapeshift/_shapeshift.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the shapeshift spells' base (the burning black slime extract's slime form is the first user; the wizard's, the
// heretics', the druid's and the polymorph belt's subtypes keep their own rows). The var block is generated (D111), but
// shapeshift_type, which tg types as a living mob and fills with a typepath.

/**
 * Shapeshift spells.
 *
 * Allows the caster to transform to and from a different mob type.
 */
// tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:10
public partial class datum_action_cooldown_spell_shapeshift : datum_action_cooldown_spell
{
    /// Our chosen type.
    public Type shapeshift_type; // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:27

    // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:10
    public datum_action_cooldown_spell_shapeshift()
    {
        button_icon_state = "shapeshift";
        school = "transmutation";
        cooldown_time = 100;
    }

    /// Helper for checking of someone's shapeshifted currently.
    private static bool is_shifted(mob_living mob) => Dm.truthy(mob.has_status_effect(typeof(datum_status_effect_shapechange_mob_from_spell)));

    // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:35
    public override async DmTask Remove(mob remove_from = null)
    {
        unshift_owner();
        await base.Remove(remove_from);
    }

    // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:39
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        await DmTask.CompletedTask;
        return isliving(cast_on) ? TRUE : FALSE;
    }

    // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:42
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        object dot = await base.before_cast(cast_on);
        if (Dm.truthy(Dm.bitand(dot, SPELL_CANCEL_CAST)))
            return dot;
        mob_living caster = (mob_living)cast_on;

        if (shapeshift_type != null)
        {
            // If another shapeshift spell was casted while we're already shifted, they could technically go to do_unshapeshift().
            // However, we don't really want people casting shapeshift A to un-shapeshift from shapeshift B,
            // as it could cause bugs or unintended behavior. So we'll just stop them here.
            if (is_shifted(caster) && !Dm.truthy(is_type_in_list(caster, possible_shapes)))
            {
                to_chat(caster, span_warning(I18n.T("This spell won't un-shapeshift you from this form!")));
                return Dm.bitor(dot, SPELL_CANCEL_CAST);
            }

            return dot;
        }

        if (Dm.length(possible_shapes) == 1)
        {
            shapeshift_type = possible_shapes[1] as Type;
            return dot;
        }

        // Not bothering with caching these as they're only ever shown once
        DmList shape_names_to_types = Dm.list();
        DmList shape_names_to_image = Dm.list();
        if (!Dm.truthy(Dm.length(shape_names_to_types)) || !Dm.truthy(Dm.length(shape_names_to_image)))
        {
            foreach (Type path in Dm.each_cast<Type>(possible_shapes))
            {
                object shape_name = Dm.initial(path, "name");
                shape_names_to_types[shape_name] = path;
                shape_names_to_image[shape_name] = image(icon: Dm.initial(path, "icon"), icon_state: Dm.initial(path, "icon_state"));
            }
        }

        object picked_type = await show_radial_menu(
            caster,
            caster,
            shape_names_to_image,
            custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), caster),
            radius: 38
        );

        if (!Dm.truthy(picked_type))
            return Dm.bitor(dot, SPELL_CANCEL_CAST);

        Type shift_type = shape_names_to_types[picked_type] as Type;
        if (!ispath(shift_type))
            return Dm.bitor(dot, SPELL_CANCEL_CAST);

        shapeshift_type = shift_type ?? pick(possible_shapes) as Type;
        if (QDELETED(this) || QDELETED(owner) || !Dm.truthy(await can_cast_spell(feedback: FALSE)))
            return Dm.bitor(dot, SPELL_CANCEL_CAST);
        return dot;
    }

    // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:89
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = await base.cast(cast_on);
        mob_living caster = (mob_living)cast_on;
        if (caster.buckled is atom_movable buckled_to)
            await buckled_to.unbuckle_mob(caster, force: TRUE);

        bool currently_ventcrawling = Dm.truthy(Dm.bitand(caster.movement_type, VENTCRAWLING));
        mob_living resulting_mob;

        // Do the shift back or forth
        if (is_shifted(caster))
            resulting_mob = do_unshapeshift(caster) as mob_living;
        else
            resulting_mob = await do_shapeshift(caster) as mob_living;

        // The shift is done, let's make sure they're in a valid state now
        // If we're not ventcrawling, we don't need to mind
        if (!currently_ventcrawling || resulting_mob == null)
            return dot;

        // We are ventcrawling - can our new form support ventcrawling?
        if (HAS_TRAIT(resulting_mob, TRAIT_VENTCRAWLER_ALWAYS) || HAS_TRAIT(resulting_mob, TRAIT_VENTCRAWLER_NUDE))
            return dot;

        // Uh oh. You've shapeshifted into something that can't fit into a vent, while ventcrawling.
        await eject_from_vents(resulting_mob);
        return dot;
    }

    /// Whenever someone shapeshifts within a vent,
    /// and enters a state in which they are no longer a ventcrawler,
    /// they are brutally ejected from the vents. In the form of gibs.
    // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:117
    public virtual async DmTask eject_from_vents(mob_living cast_on = null)
    {
        obj_machinery_atmospherics pipe_you_die_in = cast_on.loc as obj_machinery_atmospherics;
        object our_pipeline;
        object pipenets = pipe_you_die_in.return_pipenets();
        if (pipenets is DmList pipenet_list)
            our_pipeline = pipenet_list[1];
        else
            our_pipeline = pipenets;

        to_chat(cast_on, span_userdanger(I18n.T("Casting [] inside of [] quickly turns you into a bloody mush!", this, pipe_you_die_in)));
        Type gib_type = cast_on.get_gibs_type() as Type;

        foreach (obj_machinery_atmospherics_components_unary possible_vent in Dm.each<obj_machinery_atmospherics_components_unary>(range(10, get_turf(cast_on))))
        {
            if (Dm.truthy(Dm.length(possible_vent.parents)) && Dm.eq(possible_vent.parents[1], our_pipeline))
            {
                Dm.@new(gib_type, get_turf(possible_vent));
                playsound(possible_vent, "sound/mobs/non-humanoids/frog/reee.ogg", 75, TRUE);
            }
        }

        priority_announce(I18n.T("We detected a pipe blockage around [], please dispatch someone to investigate.", get_area(get_turf(cast_on))), Dm.text("[]", command_name()));
        // Gib our caster, and make sure to leave nothing behind
        // (If we leave something behind, it'll drop on the turf of the pipe, which is kinda wrong.)
        cast_on.investigate_log("has been gibbed by shapeshifting while ventcrawling.", INVESTIGATE_DEATHS);
        await cast_on.gib();
    }

    /// Callback for the radial that allows the user to choose their species.
    // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:141
    public virtual object check_menu(mob_living caster = null)
    {
        if (QDELETED(this))
            return FALSE;
        if (QDELETED(caster))
            return FALSE;

        return !Dm.truthy(caster.incapacitated) ? TRUE : FALSE;
    }

    /// Actually does the shapeshift, for the caster.
    // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:150
    public virtual async DmTask<object> do_shapeshift(mob_living caster = null)
    {
        mob_living new_shape = create_shapeshift_mob(caster.loc) as mob_living;
        datum_status_effect_shapechange_mob shapechange = await new_shape.apply_status_effect(shapechange_type, caster, this) as datum_status_effect_shapechange_mob;
        if (shapechange == null)
        {
            // We failed to shift, maybe because we were already shapeshifted?
            // Whatver the case, this shouldn't happen, so throw a stack trace.
            to_chat(caster, span_warning(I18n.T("You can't shapeshift in this form!")));
            stack_trace(Dm.text("[] do_shapeshift was called when the mob was already shapeshifted (from a spell).", type), "code/modules/spells/spell_types/shapeshift/_shapeshift.dm", 158);
            return null;
        }

        // Make sure it's castable even in their new form.
        pre_shift_requirements = spell_requirements;
        spell_requirements = Dm.num(Dm.bitand(spell_requirements, Dm.bitnot(Dm.bitor(SPELL_REQUIRES_HUMAN, SPELL_REQUIRES_WIZARD_GARB))));

        // If you shapeshift into a pet subtype we don't want to update Poly's deathcount or something when you die
        new_shape.add_traits(Dm.list(TRAIT_DONT_WRITE_MEMORY, TRAIT_SHAPESHIFTED), SHAPESHIFT_TRAIT);

        // Make sure that if you shapechanged into a bot, the AI can't just turn you off.
        if (new_shape is mob_living_basic_bot polymorph_bot)
        {
            polymorph_bot.bot_access_flags = Dm.bitor(polymorph_bot.bot_access_flags, BOT_COVER_EMAGGED);
            polymorph_bot.bot_mode_flags = Dm.bitand(polymorph_bot.bot_mode_flags, Dm.bitnot(BOT_MODE_REMOTE_ENABLED));
        }

        return new_shape;
    }

    /// Actually does the un-shapeshift, from the caster. (Caster is a shapeshifted mob.)
    // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:176
    public virtual object do_unshapeshift(mob_living caster = null)
    {
        datum_status_effect_shapechange_mob shapechange = caster.has_status_effect(shapechange_type) as datum_status_effect_shapechange_mob;
        if (shapechange == null)
        {
            // We made it to do_unshapeshift without having a shapeshift status effect, this shouldn't happen.
            to_chat(caster, span_warning(I18n.T("You can't un-shapeshift from this form!")));
            stack_trace(Dm.text("[] do_unshapeshift was called when the mob wasn't even shapeshifted (from a spell).", type), "code/modules/spells/spell_types/shapeshift/_shapeshift.dm", 182);
            return null;
        }

        // Restore the requirements.
        spell_requirements = Dm.num(pre_shift_requirements);
        pre_shift_requirements = null;

        mob_living unshapeshifted_mob = shapechange.caster_mob;
        caster.remove_status_effect(shapechange_type);
        return unshapeshifted_mob;
    }

    /// Helper proc that instantiates the mob we shapeshift into.
    /// Returns an instance of a living mob. Can be overridden.
    // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:194
    public virtual object create_shapeshift_mob(atom loc = null)
    {
        return Dm.@new(shapeshift_type, loc);
    }

    /// Removes an active shapeshift effect from the owner
    // tg: code/modules/spells/spell_types/shapeshift/_shapeshift.dm:198
    public virtual void unshift_owner()
    {
        if (owner == null)
            return;
        if (is_shifted((mob_living)owner))
            do_unshapeshift((mob_living)owner);
    }
}
