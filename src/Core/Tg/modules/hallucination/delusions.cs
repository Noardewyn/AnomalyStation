// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/delusions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. A delusion is an image with the override flag on each affected human, shown to the hallucinator's client alone:
// the client draws it in place of the human's own picture (protocol 35, DV114). The var blocks are generated (D111).

/// A hallucination that makes us and (possibly) other people look like something else.
// tg: code/modules/hallucination/delusions.dm:2
public partial class datum_hallucination_delusion : datum_hallucination
{
    // tg: code/modules/hallucination/delusions.dm:36
    [DmParams("hallucinator", "duration", "affects_us", "affects_others", "skip_nearby", "play_wabbajack")]
    public override async DmTask New(params object[] __extra)
    {
        object duration = Dm.arg(__extra, 1);
        object affects_us = Dm.arg(__extra, 2);
        object affects_others = Dm.arg(__extra, 3);
        object skip_nearby = Dm.arg(__extra, 4);
        object play_wabbajack = Dm.arg(__extra, 5);

        if (isnum(duration))
            this.duration = Dm.num(duration);
        if (affects_us != null)
            this.affects_us = Dm.num(affects_us);
        if (affects_others != null)
            this.affects_others = Dm.num(affects_others);
        if (skip_nearby != null)
            this.skip_nearby = Dm.num(skip_nearby);
        if (play_wabbajack != null)
            this.play_wabbajack = Dm.num(play_wabbajack);

        await base.New(__extra);
    }

    // tg: code/modules/hallucination/delusions.dm:56
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(hallucinator) && LAZYLEN(delusions) != 0)
        {
            if (hallucinator.client != null)
                Dm.remove(hallucinator.client.images, assoc_to_values(delusions));
            LAZYNULL(ref delusions);
        }

        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/delusions.dm:64
    public override async DmTask<object> start()
    {
        if (hallucinator.client == null || IS_UNCONSCIOUS(hallucinator))
            return FALSE;

        feedback_details += Dm.text("Delusion: []", delusion_name);
        Dm.combine(ref hallucinator.mob_flags, MOB_HAS_SCREENTIPS_NAME_OVERRIDE);
        RegisterSignal(hallucinator, COMSIG_MOB_REQUESTING_SCREENTIP_NAME_FROM_USER, nameof(screentip_name_override));
        RegisterSignal(hallucinator, COMSIG_LIVING_PERCEIVE_EXAMINE_NAME, nameof(examine_name_override));

        DmList funny_looking_mobs = Dm.list();

        // The delusion includes others - all humans
        if (Dm.truthy(affects_others))
            Dm.combine(funny_looking_mobs, GLOB.human_list.Copy());

        // The delusion includes us - we might be in it already, we might not
        if (Dm.truthy(affects_us) && ishuman(hallucinator))
            Dm.combine(funny_looking_mobs, hallucinator);
        // The delusion should not inlude us
        else
            Dm.remove(funny_looking_mobs, hallucinator);

        // The delusion shouldn not include anyone in view of us
        if (Dm.truthy(skip_nearby))
        {
            foreach (mob_living_carbon_human nearby_human in Dm.each<mob_living_carbon_human>(view(hallucinator)))
            {
                if (nearby_human == hallucinator) // Already handled by affects_us
                    continue;
                Dm.remove(funny_looking_mobs, nearby_human);
            }
        }

        foreach (mob_living_carbon_human found_human in Dm.each_cast<mob_living_carbon_human>(funny_looking_mobs.Copy()))
        {
            image funny_image = await make_delusion_image(found_human) as image;
            if (found_human != hallucinator)
                RegisterSignal(found_human, COMSIG_QDELETING, nameof(on_mob_delete));
            RegisterSignal(found_human, COMSIG_MOVABLE_Z_CHANGED, nameof(on_z_change));
            // ensures the delusion image's lighting updates if the mob's lighting changes
            RegisterSignal(found_human, COMSIG_ATOM_HOLDER_OVERLAY_LIGHT_APPLIED, nameof(on_mob_light_add));
            RegisterSignal(found_human, COMSIG_ATOM_HOLDER_OVERLAY_LIGHT_REMOVED, nameof(on_mob_light_remove));
            LAZYSET(ref delusions, found_human, funny_image);
            Dm.combine(hallucinator.client.images, funny_image);
        }

        if (Dm.truthy(play_wabbajack))
        {
            to_chat(hallucinator, span_hear("...wabbajack...wabbajack..."));
            hallucinator.playsound_local(get_turf(hallucinator), Dm.resource("sound/effects/magic/staff_change.ogg"), 50, TRUE);
        }

        if (duration > 0)
            QDEL_IN_HALLUCINATION(this, duration.Value, "code/modules/hallucination/delusions.dm", 118);
        return TRUE;
    }

    // tg: code/modules/hallucination/delusions.dm:121
    public virtual async DmTask<object> make_delusion_image(mob over_who = null)
    {
        image funny_image;
        if (delusion_appearance != null)
            funny_image = image(delusion_appearance, over_who);
        else
            funny_image = image(delusion_icon_file, over_who, Dm.str(delusion_icon_state));
        funny_image.name = Dm.str(delusion_name);
        funny_image.@override = TRUE;
        SET_PLANE_EXPLICIT(funny_image, ABOVE_GAME_PLANE, over_who);
        // copies over lighting underlays
        foreach (image underlay in Dm.each<image>(over_who.underlays))
        {
            if (Dm.eq(PLANE_TO_TRUE(underlay.plane), O_LIGHTING_VISUAL_PLANE))
                Dm.append(funny_image.underlays, underlay);
        }
        return funny_image;
    }

    // tg: code/modules/hallucination/delusions.dm:136
    [SignalHandler]
    public virtual void on_mob_delete(mob_living_carbon_human source = null)
    {
        if (hallucinator.client != null)
            Dm.remove(hallucinator.client.images, delusions[source]);
        LAZYREMOVE(ref delusions, source);
    }

    // tg: code/modules/hallucination/delusions.dm:142
    [SignalHandler]
    public virtual void on_z_change(mob_living_carbon_human source = null)
    {
        image funny_image = delusions[source] as image;
        SET_PLANE_EXPLICIT(funny_image, ABOVE_GAME_PLANE, source);
    }

    // tg: code/modules/hallucination/delusions.dm:147
    [SignalHandler]
    public virtual void on_mob_light_add(mob_living_carbon_human source = null, image visible_mask = null, image cone = null)
    {
        image funny_image = delusions[source] as image;
        Dm.combine(funny_image.underlays, visible_mask);
        if (cone != null)
            Dm.combine(funny_image.underlays, cone);
    }

    // tg: code/modules/hallucination/delusions.dm:154
    [SignalHandler]
    public virtual void on_mob_light_remove(mob_living_carbon_human source = null)
    {
        image funny_image = delusions[source] as image;
        foreach (image underlay in Dm.each<image>(funny_image.underlays.Copy()))
        {
            if (Dm.eq(PLANE_TO_TRUE(underlay.plane), O_LIGHTING_VISUAL_PLANE))
                Dm.remove(funny_image.underlays, underlay);
        }
    }

    // tg: code/modules/hallucination/delusions.dm:161
    [SignalHandler]
    public virtual object examine_name_override(datum source = null, mob_living examined = null, object visible_name = null, DmList name_override = null)
    {
        if (!ishuman(examined) || !Dm.truthy(LAZYACCESS(delusions, examined)))
            return NONE;

        name_override[1] = delusion_name;
        return COMPONENT_EXAMINE_NAME_OVERRIDEN;
    }

    // tg: code/modules/hallucination/delusions.dm:170
    [SignalHandler]
    public virtual object screentip_name_override(datum source = null, DmList returned_name = null, obj_item held_item = null, atom hovered = null)
    {
        if (!ishuman(hovered) || !Dm.truthy(LAZYACCESS(delusions, hovered)))
            return NONE;

        returned_name[1] = delusion_name;
        return SCREENTIP_NAME_SET;
    }
}

/// Used for making custom delusions.
// tg: code/modules/hallucination/delusions.dm:178
public partial class datum_hallucination_delusion_custom : datum_hallucination_delusion
{
    // tg: code/modules/hallucination/delusions.dm:181
    [DmParams("hallucinator", "duration", "affects_us", "affects_others", "skip_nearby", "play_wabbajack", "custom_icon_file", "custom_icon_state", "custom_name")]
    public override async DmTask New(params object[] __extra)
    {
        object custom_icon_file = Dm.arg(__extra, 6);
        object custom_icon_state = Dm.arg(__extra, 7);
        object custom_name = Dm.arg(__extra, 8);

        if (!Dm.truthy(custom_icon_file) || !Dm.truthy(custom_icon_state))
            stack_trace("Custom delusion hallucination was created without any custom icon information passed.", "code/modules/hallucination/delusions.dm", 191);

        this.delusion_icon_file = custom_icon_file;
        this.delusion_icon_state = custom_icon_state;
        this.delusion_name = custom_name;

        await base.New(__extra);
    }
}

// tg: code/modules/hallucination/delusions.dm:203
public partial class datum_hallucination_delusion_preset_monkey : datum_hallucination_delusion_preset
{
    // tg: code/modules/hallucination/delusions.dm:222
    public override async DmTask<object> start()
    {
        delusion_name = Dm.str(delusion_name) + Dm.text(" ([])", rand(1, 999));
        return await base.start();
    }
}

// tg: code/modules/hallucination/delusions.dm:254
public partial class datum_hallucination_delusion_preset_cyborg : datum_hallucination_delusion_preset
{
    // tg: code/modules/hallucination/delusions.dm:260
    public override async DmTask<object> make_delusion_image(mob over_who = null)
    {
        object dot = await base.make_delusion_image(over_who);
        hallucinator.playsound_local(get_turf(over_who), Dm.resource("sound/mobs/non-humanoids/cyborg/liveagain.ogg"), 75, TRUE);
        return dot;
    }
}

// tg: code/modules/hallucination/delusions.dm:264
public partial class datum_hallucination_delusion_preset_ghost : datum_hallucination_delusion_preset
{
    // tg: code/modules/hallucination/delusions.dm:270
    public override async DmTask<object> make_delusion_image(mob over_who = null)
    {
        image funny_image = await base.make_delusion_image(over_who) as image;
        funny_image.name = over_who.name;
        DO_FLOATING_ANIM(funny_image);
        return funny_image;
    }
}

// tg: code/modules/hallucination/delusions.dm:275
public partial class datum_hallucination_delusion_preset_syndies : datum_hallucination_delusion_preset
{
    // tg: code/modules/hallucination/delusions.dm:283
    public override async DmTask<object> make_delusion_image(mob over_who = null)
    {
        delusion_appearance = await get_dynamic_human_appearance(
            mob_spawn_path: pick(
                typeof(obj_effect_mob_spawn_corpse_human_syndicatesoldier),
                typeof(obj_effect_mob_spawn_corpse_human_syndicatecommando),
                typeof(obj_effect_mob_spawn_corpse_human_syndicatestormtrooper)) as Type,
            r_hand: pick(
                typeof(obj_item_knife_combat_survival),
                typeof(obj_item_melee_energy_sword_saber),
                typeof(obj_item_gun_ballistic_automatic_pistol),
                typeof(obj_item_gun_ballistic_automatic_c20r),
                typeof(obj_item_gun_ballistic_shotgun_bulldog))) as mutable_appearance;

        return await base.make_delusion_image(over_who);
    }
}

// tg: code/modules/hallucination/delusions.dm:303
public partial class datum_hallucination_delusion_preset_seccies : datum_hallucination_delusion_preset
{
    // tg: code/modules/hallucination/delusions.dm:309
    public override async DmTask<object> make_delusion_image(mob over_who = null)
    {
        delusion_appearance = await get_dynamic_human_appearance(
            outfit_path: typeof(datum_outfit_job_security),
            bloody_slots: prob(5) ? ALL : NONE,
            r_hand: prob(15) ? typeof(obj_item_melee_baton_security_loaded) : null,
            l_hand: prob(15) ? typeof(obj_item_melee_baton_security_loaded) : null) as mutable_appearance;
        return await base.make_delusion_image(over_who);
    }
}

// tg: code/modules/hallucination/delusions.dm:336
public partial class datum_hallucination_delusion_preset_heretic : datum_hallucination_delusion_preset
{
    // tg: code/modules/hallucination/delusions.dm:343
    public override async DmTask<object> make_delusion_image(mob over_who = null)
    {
        // This code is dummy hot for DUMB reasons so let's not make a mob constantly yeah?
        mutable_appearance heretic_appearance = TgState.Current.datum_hallucination_delusion_preset_heretic__heretic_appearance;
        if (heretic_appearance == null)
            heretic_appearance = TgState.Current.datum_hallucination_delusion_preset_heretic__heretic_appearance = await get_dynamic_human_appearance(typeof(datum_outfit_heretic), r_hand: NO_REPLACE) as mutable_appearance;
        delusion_appearance = heretic_appearance;
        return await base.make_delusion_image(over_who);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/hallucination/delusions.dm:341  var/static/mutable_appearance/heretic_appearance (proc-level static, rules 10.6)
    internal mutable_appearance datum_hallucination_delusion_preset_heretic__heretic_appearance;
}
