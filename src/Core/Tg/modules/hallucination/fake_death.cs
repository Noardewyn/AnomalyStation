// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/fake_death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The dust variant hides the hallucinator from everybody else with an override image (the alternate
// appearance of the one-person kind, protocol 35) and shows him a dust animation. The var blocks are generated (D111).

// This hallucinations makes us suddenly think we died, stopping us / changing our hud / sending a fake deadchat message.
// tg: code/modules/hallucination/fake_death.dm:2
public partial class datum_hallucination_death : datum_hallucination
{
    // tg: code/modules/hallucination/fake_death.dm:8
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(hallucinator))
        {
            // Really make sure these go away, would be bad if they stuck around
            hallucinator.remove_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_dead), REF(this));
            REMOVE_TRAIT(hallucinator, TRAIT_MUTE, REF(this));
            REMOVE_TRAIT(hallucinator, TRAIT_EMOTEMUTE, REF(this));
        }

        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/fake_death.dm:18
    public override async DmTask<object> start()
    {
        if (IS_UNCONSCIOUS(hallucinator)) // on your way there already
            return FALSE;

        if (Dm.truthy(floor_them))
            await hallucinator.Paralyze(30 * SECONDS);
        else
            await hallucinator.Immobilize(30 * SECONDS);

        await hallucinator.apply_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_dead), REF(this));
        hallucinator.add_traits(Dm.list(TRAIT_MUTE, TRAIT_EMOTEMUTE), REF(this));

        to_chat(hallucinator, span_deadsay(Dm.text("<b>[]</b> has died at <b>[]</b>.", hallucinator.real_name, get_area_name(hallucinator))));

        float delay = 0;

        if (prob(50))
        {
            mob who_is_salting = null;
            if (length(GLOB.dead_player_list) != 0)
                who_is_salting = pick(GLOB.dead_mob_list) as mob;

            if (who_is_salting != null)
            {
                delay = rand(2 * SECONDS, 5 * SECONDS);

                DmList things_to_hate = Dm.list(
                    "admins",
                    "batons",
                    "blood cult",
                    "coders",
                    "heretics",
                    "myself",
                    "revenants",
                    "revs",
                    "sec",
                    "ss13",
                    "this game",
                    "this round",
                    "this shift",
                    "this shit",
                    "this",
                    "wizards",
                    "you");

                DmList dead_chat_salt = Dm.list(
                    "...",
                    "FUCK",
                    "git gud",
                    "god damn it",
                    Dm.text("hey []", first_name(hallucinator.name)),
                    Dm.text("i[] hate []", prob(50) ? " fucking" : "", pick(things_to_hate)),
                    "is the AI rogue?",
                    "rip",
                    "shitsec",
                    "why did i just drop dead?",
                    "why was i gibbed",
                    "wizard?",
                    "you too?");

                _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.to_chat), hallucinator, span_deadsay(Dm.text("<b>DEAD: []</b> says, \"[]\"", who_is_salting.name, pick(dead_chat_salt)))), delay, file: "code/modules/hallucination/fake_death.dm", line: 75);
            }
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(wake_up)), delay + rand(7 * SECONDS, 9 * SECONDS), file: "code/modules/hallucination/fake_death.dm", line: 77);
        return TRUE;
    }

    // tg: code/modules/hallucination/fake_death.dm:80
    public virtual async DmTask wake_up()
    {
        if (!QDELETED(hallucinator))
        {
            hallucinator.remove_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_dead), REF(this));
            if (Dm.truthy(floor_them))
                await hallucinator.SetParalyzed(0 * SECONDS);
            else
                await hallucinator.SetImmobilized(0 * SECONDS);
            hallucinator.remove_traits(Dm.list(TRAIT_MUTE, TRAIT_EMOTEMUTE), REF(this));
        }

        if (!QDELETED(this))
            qdel(this);
    }
}

// A subtype of death which plays a dusted animation.
// tg: code/modules/hallucination/fake_death.dm:98
public partial class datum_hallucination_death_dust : datum_hallucination_death
{
    // tg: code/modules/hallucination/fake_death.dm:104
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(hallucinator) && LAZYLEN(created_images) != 0)
        {
            if (hallucinator.client != null)
                Dm.remove(hallucinator.client.images, created_images);
            created_images = null;
        }

        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/fake_death.dm:111
    public override async DmTask<object> start()
    {
        object dot = await base.start();
        if (!Dm.truthy(dot))
            return dot;

        LAZYINITLIST(ref created_images);
        // Makes hallucinator invisible, we create a clone image to animate on
        image make_them_invisible = image(null, hallucinator);
        make_them_invisible.@override = TRUE;
        Dm.append(created_images, make_them_invisible);
        // Makes remains, only visible if on a turf
        if (isturf(hallucinator.loc))
            Dm.append(created_images, image_of_type(typeof(obj_effect_decal_remains_human), hallucinator.loc));
        // Makes a ghost
        image fake_ghost = image_of_type(typeof(mob_dead_observer), get_turf(hallucinator));
        DO_FLOATING_ANIM(fake_ghost);
        Dm.append(created_images, fake_ghost);

        if (hallucinator.client != null)
            Dm.combine(hallucinator.client.images, created_images);

        // Does the actual animation here
        if (isturf(hallucinator.loc))
            Dm.@new<obj_effect_temp_visual_dust_hallucination>(hallucinator.loc, hallucinator);
        return dot;
    }

    // image(/type, loc): the image of the type's look (BYOND makes an image of a typepath from its initial appearance; the shim's image() takes
    // an instance, a resource or text, so the type's initial icon, state and layer are read here)
    // tg: code/modules/hallucination/fake_death.dm:121  image(/obj/effect/decal/remains/human, hallucinator.loc)
    internal static image image_of_type(Type path, object loc)
        => image(Dm.initial(path, (atom p) => p.icon), loc, Dm.initial(path, (atom p) => p.icon_state), Dm.initial(path, (atom p) => p.layer));
}

// tg: code/modules/hallucination/fake_death.dm:135
public partial class obj_effect_temp_visual_dust_hallucination : obj_effect_temp_visual
{
    // tg: code/modules/hallucination/fake_death.dm:140
    [DmParams("mapload", "hallucinator")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living hallucinator = Dm.arg<mob_living>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, hallucinator));
        if (hallucinator == null)
            return INITIALIZE_HINT_QDEL;

        dir = hallucinator.dir;
        appearance = hallucinator.appearance;
        mouse_opacity = MOUSE_OPACITY_TRANSPARENT;

        // make it invisible to everyone else
        image invisible = image(null, this);
        invisible.@override = TRUE;
        add_alt_appearance(
            /* type = */typeof(datum_atom_hud_alternate_appearance_basic_one_person_reversed),
            /* key = */Dm.text("[]", REF(this)),
            /* image = */invisible,
            /* options = */null,
            /* non-seer = */hallucinator);

        // do the dust animation, only the hallucinator can see it now
        dust_animation();
        return dot;
    }
}
