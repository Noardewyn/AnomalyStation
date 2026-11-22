// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/egg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The eggs (gaps package C4): the griddle's half and the omelettes wait for the kitchen (rows).

public partial class datum_controller_global_vars
{
    // tg: code/modules/food_and_drinks/food/egg.dm:17  GLOBAL_VAR_INIT(chicks_from_eggs, 0)
    public float chicks_from_eggs;
    public virtual void InitGlobalchicks_from_eggs()
    {
        chicks_from_eggs = 0;
        Dm.append(ref gvars_datum_init_order, "chicks_from_eggs");
    }
}

// tg: code/modules/food_and_drinks/food/egg.dm:20
public partial class obj_item_food_egg : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/egg.dm:67
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (Dm.truthy(dot)) // was it caught by a mob?
            return dot;

        turf hit_turf = get_turf(hit_atom) as turf;
        Dm.@new<obj_effect_decal_cleanable_food_egg_smudge>(hit_turf);
        if (prob(chick_throw_prob))
            spawn_impact_chick(hit_turf);
        Waiting.NoSleep((reagents as datum_reagents).expose(hit_atom, TOUCH));
        qdel(this);
        return dot;
    }

    /// Spawn a baby chicken from a thrown egg
    // tg: code/modules/food_and_drinks/food/egg.dm:79
    public virtual void spawn_impact_chick(turf spawn_turf = null)
    {
        float chickens_remaining = MAX_CHICKENS - GLOB.chicks_from_eggs;
        if (chickens_remaining < 1)
            return;
        float spawned_chickens = prob(97) ? 1 : Dm.min(4, chickens_remaining); // We don't want to go over the limit
        if (spawned_chickens > 1) // Chicken jackpot!
            visible_message(span_notice(Dm.text("[] chicks come out of the egg! Jackpot!", spawned_chickens)));
        else
            visible_message(span_notice("A chick comes out of the cracked egg!"));
        for (int i = 1; i <= spawned_chickens; i++)
        {
            Dm.@new<mob_living_basic_chick>(spawn_turf);
            GLOB.chicks_from_eggs++;
        }
    }

    // tg: code/modules/food_and_drinks/food/egg.dm:92
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_toy_crayon crayon)
        {
            object pigment = crayon.crayon_color;
            if (!Dm.@in(pigment, Dm.list("blue", "green", "mime", "orange", "purple", "rainbow", "red", "yellow")))
            {
                to_chat(usr, span_notice(Dm.text("[] refuses to take on this colour!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(usr, span_notice(Dm.text("You colour [] with [].", this, tool)));
            icon_state = Dm.text("egg-[]", pigment);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stamp_clown)
        {
            object clowntype = pick("grock", "grimaldi", "rainbow", "chaos", "joker", "sexy", "standard", "bobble",
                "krusty", "bozo", "pennywise", "ronald", "jacobs", "kelly", "popov", "cluwne");
            icon_state = Dm.text("egg-clown-[]", clowntype);
            to_chat(usr, span_notice(Dm.text("You stamp [] with [], creating an artistic and not remotely horrifying likeness of clown makeup.", this, tool)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (is_reagent_container(tool))
        {
            obj_item_reagent_containers dunk_test_container = tool as obj_item_reagent_containers;
            if (!Dm.truthy(dunk_test_container.is_drainable()) || !Dm.truthy((dunk_test_container.reagents as datum_reagents).has_reagent(typeof(datum_reagent_water))))
                return NONE;

            to_chat(user, span_notice(Dm.text("You check if [] is rotten.", this)));
            if (this is obj_item_food_egg_rotten)
                to_chat(user, span_warning(Dm.text("[] floats in the []!", this, dunk_test_container)));
            else
                to_chat(user, span_notice(Dm.text("[] sinks into the []!", this, dunk_test_container)));
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/food_and_drinks/food/egg.dm:127 (gaps H2: cracking an egg on the griddle)
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is not obj_machinery_griddle)
            return NONE;

        obj_machinery_griddle hit_griddle = (obj_machinery_griddle)interacting_with;
        if (Dm.length(hit_griddle.griddled_objects) >= hit_griddle.max_items)
        {
            interacting_with.balloon_alert(user, I18n.T("no room!"));
            return ITEM_INTERACT_BLOCKING;
        }
        atom broken_egg = Dm.@new<obj_item_food_rawegg>(interacting_with.loc);
        if (Dm.truthy(LAZYACCESS(modifiers, ICON_X)))
            broken_egg.pixel_x = clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_X))) - 16, -(ICON_SIZE_X / 2), ICON_SIZE_X / 2);
        if (Dm.truthy(LAZYACCESS(modifiers, ICON_Y)))
            broken_egg.pixel_y = clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_Y))) - 16, -(ICON_SIZE_Y / 2), ICON_SIZE_Y / 2);
        playsound(user, Dm.resource("sound/items/sheath.ogg"), 40, TRUE);
        await ((datum_reagents)reagents).trans_to(broken_egg, ((datum_reagents)reagents).total_volume, copy_only: TRUE);

        hit_griddle.AddToGrill(broken_egg as obj_item, user);
        interacting_with.balloon_alert(user, I18n.T("cracks [] open", this));

        qdel(this);
        return ITEM_INTERACT_BLOCKING;
    }
}

// tg: code/modules/food_and_drinks/food/egg.dm:58
public partial class obj_item_food_egg_gland : obj_item_food_egg
{
    // tg: code/modules/food_and_drinks/food/egg.dm:60
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep((reagents as datum_reagents).add_reagent(get_random_reagent_id(), 15));
        object color = mix_color_from_reagents((reagents as datum_reagents).reagent_list);
        Waiting.NoSleep(add_atom_colour(color, FIXED_COLOUR_PRIORITY));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/egg.dm:185
public partial class obj_item_food_egg_fertile : obj_item_food_egg
{
    // tg: code/modules/food_and_drinks/food/egg.dm:190
    [DmParams("mapload", "loc")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_fertile_egg), typeof(mob_living_basic_chick), 1f, 2f, 200f, 0f, typecacheof(Dm.list(typeof(turf))), FALSE));
        return dot;
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_egg : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/egg.dm:35
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_boiledegg), rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/egg.dm:38
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), typeof(obj_item_food_boiledegg));
    }
}

public partial class obj_item_food_egg_rotten : obj_item_food_egg
{
    // tg: code/modules/food_and_drinks/food/egg.dm:51
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_boiledegg_rotten), rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/food_and_drinks/food/egg.dm:54
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), typeof(obj_item_food_boiledegg_rotten));
    }
}

public partial class obj_item_food_rawegg : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/egg.dm:232
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_friedegg), rand(20 * SECONDS, 35 * SECONDS), TRUE, FALSE);
    }
}

// tg: code/modules/food_and_drinks/food/egg.dm:416
public partial class obj_item_food_rawegg_spore : obj_item_food_rawegg
{
    // tg: code/modules/food_and_drinks/food/egg.dm:422
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_BLOBSPORE, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/egg.dm:426
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_friedegg_spore), rand(15 * SECONDS, 25 * SECONDS), TRUE, FALSE);
    }
}

// Gaps H2 (D333): the omelette eaten with a fork and the spore sack cracked on the griddle.

// tg: code/modules/food_and_drinks/food/egg.dm:271
public partial class obj_item_food_omelette : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/egg.dm:287
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_love_food_buff), typeof(datum_status_effect_food_speech_french)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/egg.dm:291
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_kitchen_fork)
            return NONE;

        obj_item_kitchen_fork fork = (obj_item_kitchen_fork)tool;
        if (fork.forkload != null)
        {
            to_chat(user, span_warning(I18n.T("You already have omelette on your fork!")));
            return ITEM_INTERACT_BLOCKING;
        }

        fork.icon_state = "forkloaded";
        user.visible_message(span_notice(I18n.T("[] takes a piece of omelette with [] fork!", user, user.p_their())),
            span_notice(I18n.T("You take a piece of omelette with your fork.")));

        datum_reagents reagents = (datum_reagents)this.reagents;
        datum_reagent reagent = pick(reagents.reagent_list) as datum_reagent;
        await reagents.remove_reagent(reagent.type, 1);
        fork.forkload = reagent;
        if (reagents.total_volume <= 0)
            qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/food_and_drinks/food/egg.dm:358
public partial class obj_item_food_spore_sack : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/egg.dm:372
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(50))
            icon_state = Dm.text("[]2", base_icon_state);

        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_BLOBSPORE, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/egg.dm:379
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is not obj_machinery_griddle)
            return NONE;

        obj_machinery_griddle hit_griddle = (obj_machinery_griddle)interacting_with;
        if (Dm.length(hit_griddle.griddled_objects) >= hit_griddle.max_items)
        {
            interacting_with.balloon_alert(user, I18n.T("no room!"));
            return ITEM_INTERACT_BLOCKING;
        }
        atom broken_egg = Dm.@new<obj_item_food_rawegg_spore>(interacting_with.loc);
        if (Dm.truthy(LAZYACCESS(modifiers, ICON_X)))
            broken_egg.pixel_x = clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_X))) - 16, -(ICON_SIZE_X / 2), ICON_SIZE_X / 2);
        if (Dm.truthy(LAZYACCESS(modifiers, ICON_Y)))
            broken_egg.pixel_y = clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_Y))) - 16, -(ICON_SIZE_Y / 2), ICON_SIZE_Y / 2);
        playsound(user, Dm.resource("sound/items/sheath.ogg"), 40, TRUE);
        await ((datum_reagents)reagents).trans_to(broken_egg, ((datum_reagents)reagents).total_volume, copy_only: TRUE);

        hit_griddle.AddToGrill(broken_egg as obj_item, user);
        interacting_with.balloon_alert(user, I18n.T("cracks [] open", this));

        qdel(this);
        return ITEM_INTERACT_BLOCKING;
    }
}
