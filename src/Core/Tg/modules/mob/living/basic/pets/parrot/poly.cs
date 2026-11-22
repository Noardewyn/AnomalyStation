// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/parrot/poly.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the text-to-speech voice (a row) (gaps package C2; the ghost's possession disease came with gaps VI1).

/// The classically famous compadre to the Chief Engineer, Poly.
// tg: code/modules/mob/living/basic/pets/parrot/poly.dm:16
public partial class mob_living_basic_parrot_poly : mob_living_basic_parrot
{
    /// Default poly, presumably died the last shift and has no special traits.
    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:2
    private const string POLY_DEFAULT = "default";
    /// Poly has survived a number of rounds equivalent to the longest survival of his being.
    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:4
    private const string POLY_LONGEST_SURVIVAL = "longest_survival";
    /// Poly has survived a number of rounds equivalent to the longest deathstreak of his being.
    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:6
    private const string POLY_BEATING_DEATHSTREAK = "longest_deathstreak";
    /// Poly has only just survived a round, and is doing a consecutive one.
    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:8
    private const string POLY_CONSECUTIVE_ROUND = "consecutive_round";

    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (!Dm.truthy(memory_saved))
        {
            roundend_callback = Dm.@new<datum_callback>(this, nameof(Write_Memory));
            SSticker.OnRoundend(roundend_callback);
        }

        // REGISTER_REQUIRED_MAP_ITEM(1, 1): the unit tests' map item check (UNIT_TESTS)
        update_appearance();

        if (!Dm.truthy(SStts?.tts_enabled))
            return dot;

        voice = "Woman 2";
        if (Dm.truthy(SStts.pitch_enabled))
        {
            // DEVIATION(DV355): a few semitones up instead of tg's one or two octaves (12, 24), in one voice, said at 1.5 times its speed (the owner's
            // tuning, 2026-11-08). The clients raise the pitch by resampling a line synthesized slower by the same factor; below about 0.6 of the speed
            // the synthesis garbles the words (Whisper lost 39% of them at 7 semitones and 0.65; the owner, 2026-11-06).
            if (Dm.truthy(findtext(voice, "Woman")))
                pitch = 3;
            else
                pitch = 4;
            voice_filter = "atempo=1.5";
        }
        else
            voice_filter = "rubberband=pitch=1.2"; // Use the filter to pitch up if we can't naturally pitch up.
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:54
    public override object Destroy(float? force = null)
    {
        LAZYREMOVE(ref SSticker.round_end_events, roundend_callback); // we do the memory writing stuff on death, but this is important to yeet as fast as we can if we need to destroy
        roundend_callback = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:59
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (HAS_TRAIT(this, TRAIT_DONT_WRITE_MEMORY))
            return await base.death(gibbed, __extra); // Don't read memory either.
        if (!Dm.truthy(memory_saved))
            Write_Memory(TRUE);
        object special_status = determine_special_poly();
        if (Dm.eq(special_status, POLY_LONGEST_SURVIVAL) || Dm.eq(special_status, POLY_BEATING_DEATHSTREAK) || prob(0.666f))
        {
            mob_living_basic_parrot_poly_ghost specter = Dm.@new<mob_living_basic_parrot_poly_ghost>(loc);
            if (mind != null)
                await mind.transfer_to(specter);
            else
                specter.PossessByPlayer(key);
        }
        return await base.death(gibbed, __extra);
    }

    // there's only one poly, so there should only be one ongoing list of phrases. i guess
    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:73
    public override DmList get_static_list_of_phrases()
    {
        DmList phrases_to_return = TgState.Current.mob_living_basic_parrot_poly__phrases_to_return ??= Dm.list();
        if (Dm.truthy(length(phrases_to_return)))
            return phrases_to_return;

        Dm.append(ref phrases_to_return, read_memory()); // must come first!!!
        // now add some valuable lines every poly should have
        Dm.append(ref phrases_to_return, Dm.list(
            ":e Check the crystal, you chucklefucks!",
            ":e OH GOD ITS ABOUT TO DELAMINATE CALL THE SHUTTLE",
            ":e WHO TOOK THE DAMN MODSUITS?",
            ":e Wire the solars, you lazy bums!",
            "Poly wanna cracker!"));
        switch (determine_special_poly())
        {
            case POLY_DEFAULT:
                Dm.append(ref phrases_to_return, pick("...alive?", "This isn't parrot heaven!", "I live, I die, I live again!", "The void fades!"));
                break;
            case POLY_LONGEST_SURVIVAL:
                Dm.append(ref phrases_to_return, pick(Dm.text("...[].", longest_survival), "The things I've seen!", "I have lived many lives!", "What are you before me?"));
                break;
            case POLY_BEATING_DEATHSTREAK:
                Dm.append(ref phrases_to_return, pick("What are you waiting for!", "Violence breeds violence!", "Blood! Blood!", "Strike me down if you dare!"));
                break;
            case POLY_CONSECUTIVE_ROUND:
                Dm.append(ref phrases_to_return, pick("...again?", "No, It was over!", "Let me out!", "It never ends!"));
                break;
        }

        return phrases_to_return;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:99
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        switch (determine_special_poly())
        {
            case POLY_LONGEST_SURVIVAL:
                desc += Dm.text(" Old as sin, and just as loud. Claimed to be [].", rounds_survived);
                break;
            case POLY_BEATING_DEATHSTREAK:
                desc += Dm.text(" The squawks of [] dead parrots ring out in your ears...", -Dm.num(rounds_survived));
                break;
            case POLY_CONSECUTIVE_ROUND:
                desc += Dm.text(" Over [] shifts without a \"terrible\" \"accident\"!", rounds_survived);
                break;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:109
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        switch (determine_special_poly())
        {
            case POLY_LONGEST_SURVIVAL:
                await add_atom_colour("#EEEE22", FIXED_COLOUR_PRIORITY);
                break;
            case POLY_BEATING_DEATHSTREAK:
                await add_atom_colour("#BB7777", FIXED_COLOUR_PRIORITY);
                break;
        }
        return dot;
    }

    /// Reads the memory of the parrot, and updates the necessary variables. Returns a list of phrases to add to the parrot's speech buffer.
    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:118
    public virtual DmList read_memory()
    {
        // the legacy savefile branch (data/npc_saves/Poly.sav, tg's old format) has no port: no such file exists here
        DmFileRef json_file = Dm.file("data/npc_saves/Poly.json");
        if (!Dm.truthy(Dm.fexists(json_file)))
            return Dm.list();
        DmList json = Dm.json_decode(Dm.file2text(json_file)) as DmList;
        DmList returnable_list = json["phrases"] as DmList;
        rounds_survived = Dm.num(json["roundssurvived"]);
        longest_survival = Dm.num(json["longestsurvival"]);
        longest_deathstreak = Dm.num(json["longestdeathstreak"]);

        return returnable_list;
    }

    /// Determines the type of Poly we might have here based on the statistics we got from the memory.
    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:142
    public virtual string determine_special_poly()
    {
        if (rounds_survived == longest_survival)
            return POLY_LONGEST_SURVIVAL;
        else if (rounds_survived == longest_deathstreak)
            return POLY_BEATING_DEATHSTREAK;
        else if (rounds_survived > 0)
            return POLY_CONSECUTIVE_ROUND;
        else
            return POLY_DEFAULT;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:152
    public override object Write_Memory(object dead = null, object gibbed = null)
    {
        object dot = base.Write_Memory(dead, gibbed);
        if (!Dm.truthy(dot) || Dm.truthy(memory_saved)) // if we die, no more memory
            return FALSE;

        if (!Dm.truthy(dead) && stat != DEAD)
            dead = FALSE;

        string file_path = "data/npc_saves/Poly.json";
        DmList file_data = Dm.list();

        DmList exportable_speech_buffer = (ai_controller as datum_ai_controller).blackboard[BB_EXPORTABLE_STRING_BUFFER_LIST] as DmList; // should have been populated when we sent the signal out on parent
        if (Dm.truthy(length(exportable_speech_buffer)))
            file_data["phrases"] = exportable_speech_buffer;

        if (Dm.truthy(dead))
        {
            file_data["roundssurvived"] = Dm.min(rounds_survived - 1, 0);
            file_data["longestsurvival"] = longest_survival;
            if (rounds_survived - 1 < longest_deathstreak)
                file_data["longestdeathstreak"] = rounds_survived - 1;
            else
                file_data["longestdeathstreak"] = longest_deathstreak;
        }
        else
        {
            file_data["roundssurvived"] = Dm.max(rounds_survived, 0) + 1;
            if (rounds_survived + 1 > longest_survival)
                file_data["longestsurvival"] = rounds_survived + 1;
            else
                file_data["longestsurvival"] = longest_survival;
            file_data["longestdeathstreak"] = longest_deathstreak;
        }

        rustg_file_write(Dm.json_encode(file_data, JSON_PRETTY_PRINT), file_path);
        memory_saved = TRUE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:187
    public override void setup_headset()
    {
        ears = Dm.@new<obj_item_radio_headset_headset_eng>(this);
    }
}

// tg: code/modules/mob/living/basic/pets/parrot/poly.dm:191
public partial class mob_living_basic_parrot_poly_ghost : mob_living_basic_parrot_poly
{
    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:9
    private const string POLY_POSSESS_FILTER = "poly_possess_filter";
    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:11
    private const string POLY_POSSESS_GLOW = "#522059";

    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:202
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // block anything and everything that could possibly happen with writing memory for ghosts
        memory_saved = TRUE;
        add_traits(Dm.list(TRAIT_GODMODE, TRAIT_DONT_WRITE_MEMORY), INNATE_TRAIT);
        RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        return base.Initialize(mapload, __extra);
    }

    //we perch on human souls
    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:210
    public override async DmTask<object> perch_on_human(mob_living_carbon_human target = null)
    {
        if (loc == target) //dismount
        {
            await forceMove(get_turf(target));
            return FALSE;
        }
        if (ishuman(loc))
        {
            balloon_alert(this, "already possessing!");
            return FALSE;
        }
        await forceMove(target);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/poly.dm:220
    [SignalHandler]
    public virtual void on_moved(atom_movable movable = null, atom old_loc = null)
    {
        if (ishuman(old_loc))
        {
            mob_living unpossessed_human = old_loc as mob_living;
            unpossessed_human.remove_filter(POLY_POSSESS_FILTER);
            return;
        }

        if (!ishuman(loc))
            return;

        mob_living possessed_human = loc as mob_living;
        possessed_human.add_filter(POLY_POSSESS_FILTER, 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", POLY_POSSESS_GLOW), Dm.kv("size", 2)));
        object filter = possessed_human.get_filter(POLY_POSSESS_FILTER);

        if (Dm.truthy(filter))
        {
            Dm.animate(filter, Dm.named("alpha", 200), Dm.named("time", 2 * SECONDS), Dm.named("loop", -1));
            Dm.animate(Dm.named("alpha", 60), Dm.named("time", 2 * SECONDS));
        }

        datum_disease_parrot_possession on_possession = Dm.@new<datum_disease_parrot_possession>(); // gaps VI1
        on_possession.set_parrot(this);
        Waiting.NoSleep(possessed_human.ForceContractDisease(on_possession, make_copy: FALSE, del_on_fail: TRUE));
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_parrot_poly__phrases_to_return;
}
