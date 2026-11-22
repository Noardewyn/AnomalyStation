// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/mother.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file but for the leash that makes mother follow the hallucinator (the leash component paths with the pathfinding
// AI, M10: a marked leaf, so she stays where she appears); her lines' language and runechat from M9 slice A. The var block is generated (D111).

/// Your mother appears to scold you.
// tg: code/modules/hallucination/mother.dm:2
public partial class datum_hallucination_your_mother : datum_hallucination
{
    // tg: code/modules/hallucination/mother.dm:9
    public override async DmTask<object> start()
    {
        if (hallucinator.client == null || IS_UNCONSCIOUS(hallucinator))
            return FALSE;

        DmList spawn_locs = Dm.list();
        foreach (turf_open_floor floor in Dm.each<turf_open_floor>(view(4, hallucinator)))
        {
            if (Dm.truthy(await floor.is_blocked_turf(exclude_mobs: TRUE)))
                continue;
            Dm.append(spawn_locs, floor);
        }

        if (length(spawn_locs) == 0)
            return FALSE;
        turf spawn_loc = pick(spawn_locs) as turf;
        mother = Dm.@new<obj_effect_client_image_holder_hallucination_your_mother>(spawn_loc, hallucinator, this);
        Dm.Deferred("code/modules/hallucination/mother.dm:22", "start: mother.AddComponent(/datum/component/leash, owner = hallucinator, distance = get_dist(hallucinator, mother)) (the leash component and its pathfinding, M10+)");
        point_at(hallucinator);
        await talk(Dm.text("[]!!!!", capitalize(hallucinator.real_name))); // Your mother won't be fooled by paltry disguises
        DmList scold_lines = Dm.list(
            pick_list_replacements(MOTHER_FILE, "do_something"),
            pick_list_replacements(MOTHER_FILE, "be_upset"),
            pick_list_replacements(MOTHER_FILE, "get_reprimanded"));
        float delay = 2 * SECONDS;
        foreach (object line in Dm.each(scold_lines))
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(talk), line), delay, file: "code/modules/hallucination/mother.dm", line: 33);
            delay += 2 * SECONDS;
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(exit)), delay + 4 * SECONDS, file: "code/modules/hallucination/mother.dm", line: 36);
        return TRUE;
    }

    // tg: code/modules/hallucination/mother.dm:39
    public virtual void point_at(atom target = null)
    {
        turf tile = get_turf(target);
        if (tile == null)
            return;

        image visual = image(Dm.resource("icons/hud/screen_gen.dmi"), mother.loc, "arrow", FLY_LAYER);

        Waiting.DetachSync(() => flick_overlay_global(visual, Dm.list(hallucinator.client), 2.5f * SECONDS));
        animate(visual, Dm.named("pixel_x", (tile.x - mother.x) * ICON_SIZE_X), Dm.named("pixel_y", (tile.y - mother.y) * ICON_SIZE_Y), Dm.named("time", 1.7f), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_OUT)));
    }

    // tg: code/modules/hallucination/mother.dm:48
    public virtual async DmTask talk(object text = null)
    {
        object plus_runechat = safe_read_pref(hallucinator.client, typeof(datum_preference_toggle_enable_runechat)); // hallucinator.client?.prefs: DV161
        object understood_language = hallucinator.get_random_understood_language();
        DmList spans = Dm.list(mother.speech_span);

        if (!Dm.truthy(plus_runechat))
        {
            image speech_overlay = image(Dm.resource("icons/mob/effects/talk.dmi"), mother, "default0", ABOVE_MOB_LAYER);
            Waiting.DetachSync(() => flick_overlay_global(speech_overlay, Dm.list(hallucinator.client), 30));
        }
        else
            hallucinator.create_chat_message(mother, understood_language, text, spans);

        object message = await hallucinator.compose_message(mother, understood_language, text, null, null, null, spans, null, TRUE);
        to_chat(hallucinator, message);
    }

    // tg: code/modules/hallucination/mother.dm:61
    public virtual void exit()
    {
        qdel(this);
    }
}

// tg: code/modules/hallucination/mother.dm:64
public partial class datum_outfit_yourmother : datum_outfit
{
    // tg: code/modules/hallucination/mother.dm:71
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        await base.post_equip(user, visuals_only);
        user.set_hairstyle("Braided", update: TRUE); //get_dynamic_human_appearance uses bald dummies
    }
}

// tg: code/modules/hallucination/mother.dm:75
public partial class obj_effect_client_image_holder_hallucination_your_mother : obj_effect_client_image_holder_hallucination
{
    // tg: code/modules/hallucination/mother.dm:82
    [DmParams("mapload", "mobs_which_see_us", "parent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_hallucination parent = Dm.arg<datum_hallucination>(__extra, 1);
        mob_living hallucinator = parent.hallucinator;
        if (ishuman(hallucinator) && !isplasmaman(hallucinator)) //Plasmapeople don't have parents in a traditional sense, so their mother is different.
        {
            mob_living_carbon dna_haver = (mob_living_carbon)hallucinator;
            image_icon = image(Waiting.NoSleep(get_dynamic_human_appearance(typeof(datum_outfit_yourmother), (dna_haver.dna.species as datum_species)?.type)));
            return base.Initialize(mapload, __extra);
        }

        if (isplasmaman(hallucinator))
        {
            image_icon = Dm.resource("icons/turf/floors.dmi");
            image_state = "liquidplasma";
            return base.Initialize(mapload, __extra);
        }

        if (hallucinator is mob_living_basic_pet_dog_corgi_ian)
        {
            // getFlatIcon(...) is the 2D flattening of the look; the look itself is what the image takes here
            image_icon = image(Waiting.NoSleep(get_dynamic_human_appearance(typeof(datum_outfit_job_hop))));
            name = "Head of Personnel";
            return base.Initialize(mapload, __extra);
        }

        image_icon = hallucinator.icon;
        image_state = hallucinator.icon_state;
        image_pixel_x = hallucinator.pixel_x;
        image_pixel_y = hallucinator.pixel_y;
        return base.Initialize(mapload, __extra);
    }
}
