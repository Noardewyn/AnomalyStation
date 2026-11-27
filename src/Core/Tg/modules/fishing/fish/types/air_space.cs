// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/types/air_space.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish of air and space (the sand surfer, the burrower crab, the bumpy, the glowing starfish that floats and
// ascends, and the baby carp that grows into a space carp). The var blocks are generated (D111).

// tg: code/modules/fishing/fish/types/air_space.dm:21
public partial class obj_item_fish_sand_crab : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/air_space.dm:46
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw crab", 2));
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:49
    public override object get_fish_taste_cooked()
    {
        return Dm.list(Dm.kv("cooked crab", 2));
    }
}

// tg: code/modules/fishing/fish/types/air_space.dm:72
public partial class obj_item_fish_starfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/air_space.dm:95
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:99
    public override object fish_grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_bluespace), 10));
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:102
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, add_emissive());
        return dot;
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:106
    public virtual object add_emissive()
    {
        // DEVIATION(DV12): if(status == FISH_ALIVE) return emissive_appearance(icon, "starfish_emissive", src, effect_type = EMISSIVE_NO_BLOOM),
        // the dim glow on the emissive plane: the 3D client renders light itself
        return null;
    }

    ///It spins, and dimly glows in the dark.
    // tg: code/modules/fishing/fish/types/air_space.dm:111
    public override void flop_animation()
    {
        DO_FLOATING_ANIM(this);
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:114
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] swallows [], and looks upwards...", user, this)));
        if (prob(20))
            await user.say("I must go. My people need me.", forced: "starfish suicide");
        _addtimer(Dm.@new<datum_callback>(this, nameof(ascension), user), 1 * SECONDS, file: "code/modules/fishing/fish/types/air_space.dm", line: 118);
        return MANUAL_SUICIDE;
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:121
    public virtual async DmTask ascension(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] abandons [] corporeal form!", user, user.p_their())));
        await user.drop_everything();
        user.add_filter("space", 1, layering_filter(icon: Dm.icon(Dm.resource("icons/mob/human/textures.dmi"), "spacey"), blend_mode: BLEND_INSET_OVERLAY) as DmList);
        await user.apply_status_effect(typeof(datum_status_effect_go_away_deletes_mob));
        qdel(this);
    }
}

// tg: code/modules/fishing/fish/types/air_space.dm:128
public partial class obj_item_fish_baby_carp : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/air_space.dm:240  #define PERSISTENCE_FISH_CARP_COLOR "carp_color" (file-local)
    private const string PERSISTENCE_FISH_CARP_COLOR = "carp_color";

    // tg: code/modules/fishing/fish/types/air_space.dm:166
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        color = pick_weight(GLOB.carp_colors);
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_FISH_BEFORE_GROWING, nameof(growth_checks));
        RegisterSignal(this, COMSIG_FISH_FINISH_GROWING, nameof(on_growth));
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:173
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] swallows [] whole!", user, this)));
        await forceMove(user);
        if (status == FISH_DEAD)
        {
            await user.emote("gasp");
            user.visible_message(span_suicide(I18n.T("[] chokes on [] and dies!", user, this)));
            return OXYLOSS;
        }

        // the fish grows
        _addtimer(Dm.@new<datum_callback>(this, nameof(gestation), user), 20 * SECONDS, file: "code/modules/fishing/fish/types/air_space.dm", line: 183);
        user.visible_message(span_suicide(I18n.T("[] starts growing unnaturally...", user)));

        matrix M = Dm.matrix();
        M.Scale(1.8f, 1.2f);
        animate(user, Dm.named("time", 20 * SECONDS), Dm.named("transform", M), Dm.named("easing", SINE_EASING));
        return MANUAL_SUICIDE;
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:190
    public virtual async DmTask gestation(mob_living user = null)
    {
        if (QDELETED(user) || QDELETED(this))
            return;
        // carp grow big and strong inside the nutritious innards of the human
        mob_living_basic_carp_mega babby = Dm.@new<mob_living_basic_carp_mega>(get_turf(user));
        babby.name = user.name + " Jr.";

        obj_item_bodypart chest = user.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart;
        if (chest != null)
            await babby.set_greyscale(Dm.list(chest.species_color)); // this isn't working. why isnt this working (a one-colour list joins to the same text)

        await user.gib(DROP_ALL_REMAINS);
        qdel(this);
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:204
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance eyes = mutable_appearance(icon, "baby_carp_eyes") as mutable_appearance;
        if (status == FISH_DEAD)
            eyes.icon_state += "_dead";
        else
            eyes.appearance_flags = Dm.bitor(RESET_COLOR, KEEP_APART);
        Dm.append(ref dot, eyes);
        return dot;
    }

    ///Determines the speed at which the carp grows based on how big it's
    // tg: code/modules/fishing/fish/types/air_space.dm:214
    public override async DmTask update_size_and_weight(object new_size = null, object new_weight = null, float? update_materials = null)
    {
        new_size ??= average_size;
        new_weight ??= average_weight;
        update_materials ??= TRUE;
        await base.update_size_and_weight(new_size, new_weight, update_materials);
        float growth_rate = 4.5f * MINUTES;
        growth_rate *= clamp(Dm.num(size) / average_size, 0.5f, 2);
        growth_rate *= clamp(Dm.num(weight) / average_weight, 0.5f, 2);

        await AddComponent(typeof(datum_component_fish_growth), typeof(mob_living_basic_carp_advanced), growth_rate);
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:222
    [SignalHandler]
    public virtual object growth_checks(datum source = null, object seconds_per_tick = null, object growth = null, object result_path = null)
    {
        float hunger = CLAMP01((world.time - Dm.num(last_feeding)) / feeding_frequency);
        if (Dm.num(get_health_percentage()) <= 0.6f || hunger >= 0.6f) //if too hurt or hungry, don't grow.
            return COMPONENT_DONT_GROW;

        if (loc == null || !HAS_TRAIT(loc, TRAIT_IS_AQUARIUM))
            return null;

        if (HAS_TRAIT(loc, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH)) //the aquarium has breeding disabled
            return COMPONENT_DONT_GROW;
        if (length(get_aquarium_fishes()) > AQUARIUM_MAX_BREEDING_POPULATION * 0.5f) //check if there's enough room to maturate.
            return COMPONENT_DONT_GROW;
        return null;
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:236
    [SignalHandler]
    public virtual void on_growth(datum source = null, mob_living_basic_carp result = null)
    {
        //yes, this means that if we use a spraycan on the fish, the resulting space carp will be of spraycan color
        Waiting.NoSleep(result.set_greyscale(colors: Dm.list(color)));
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:243
    public override void persistence_save(DmList data = null)
    {
        data[PERSISTENCE_FISH_CARP_COLOR] = color;
    }

    // tg: code/modules/fishing/fish/types/air_space.dm:246
    public override async DmTask persistence_load(DmList data = null)
    {
        await add_atom_colour(data[PERSISTENCE_FISH_CARP_COLOR], FIXED_COLOUR_PRIORITY);
    }
}
