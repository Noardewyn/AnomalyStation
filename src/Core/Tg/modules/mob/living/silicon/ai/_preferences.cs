// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/_preferences.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only ai_status_display_emotes, ai_core_to_status_display_mapping, ai_status_display_all_options and
// init_ai_status_display_options translated (the AI status display's pictures, gaps W1); the holograms' lists come with the AI.

public partial class datum_controller_global_vars : datum_controller
{
    // A mapping between AI_EMOTION_* string constants, which also double as user readable descriptions, and the name of the iconfile. (used for /obj/machinery/status_display/ai )
    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:2
    public DmList ai_status_display_emotes;

    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:2  (GLOBAL_LIST_INIT, GLOBAL_MANAGED(ai_status_display_emotes, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalai_status_display_emotes()
    {
        ai_status_display_emotes = Dm.list(
            // Original AI emotion states
            Dm.kv(AI_EMOTION_AWESOME, "ai_awesome"),
            Dm.kv(AI_EMOTION_BLANK, AI_DISPLAY_DONT_GLOW),
            Dm.kv(AI_EMOTION_BLUE_GLOW, "ai_sal"),
            Dm.kv(AI_EMOTION_CONFUSED, "ai_confused"),
            Dm.kv(AI_EMOTION_DORFY, "ai_urist"),
            Dm.kv(AI_EMOTION_FACEPALM, "ai_facepalm"),
            Dm.kv(AI_EMOTION_FRIEND_COMPUTER, "ai_friend"),
            Dm.kv(AI_EMOTION_HAPPY, "ai_happy"),
            Dm.kv(AI_EMOTION_NEUTRAL, "ai_neutral"),
            Dm.kv(AI_EMOTION_PROBLEMS, "ai_trollface"),
            Dm.kv(AI_EMOTION_RED_GLOW, "ai_hal"),
            Dm.kv(AI_EMOTION_SAD, "ai_sad"),
            Dm.kv(AI_EMOTION_THINKING, "ai_thinking"),
            Dm.kv(AI_EMOTION_UNSURE, "ai_unsure"),
            Dm.kv(AI_EMOTION_VERY_HAPPY, "ai_veryhappy"),
            Dm.kv(AI_EMOTION_DEAD, "ai_dead"),
            Dm.kv(AI_EMOTION_DOWNLOAD, "ai_download"));
        Dm.append(ref gvars_datum_init_order, "ai_status_display_emotes");
    }

    // Mapping from AI core display options to new status display icon states
    // This allows AI status displays to show the same choice as AI core displays
    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:25
    public DmList ai_core_to_status_display_mapping;

    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:25  (GLOBAL_LIST_INIT, GLOBAL_MANAGED(ai_core_to_status_display_mapping, ...))
    public virtual void InitGlobalai_core_to_status_display_mapping()
    {
        ai_core_to_status_display_mapping = Dm.list(
            Dm.kv("Alien", "ai_status_alien"),
            Dm.kv("Angel", "ai_status_angel"),
            Dm.kv("Banned", "ai_status_banned"),
            Dm.kv("Bliss", "ai_status_bliss"),
            Dm.kv("Blue", "ai_status_blue"),
            Dm.kv("Clown", "ai_status_clown"),
            Dm.kv("Database", "ai_status_database"),
            Dm.kv("Dorf", "ai_status_dorf"),
            Dm.kv("Firewall", "ai_status_firewall"),
            Dm.kv("Fuzzy", "ai_status_fuzzy"),
            Dm.kv("Gentoo", "ai_status_gentoo"),
            Dm.kv("Glitchman", "ai_status_glitchman"),
            Dm.kv("Gondola", "ai_status_gondola"),
            Dm.kv("Goon", "ai_status_goon"),
            Dm.kv("Hades", "ai_status_hades"),
            Dm.kv("HAL 9000", "ai_status_hal9000"),
            Dm.kv("Heartline", "ai_status_heartline"),
            Dm.kv("Helios", "ai_status_helios"),
            Dm.kv("House", "ai_status_house"),
            Dm.kv("Matrix", "ai_status_matrix"),
            Dm.kv("Monochrome", "ai_status_monochrome"),
            Dm.kv("Murica", "ai_status_murica"),
            Dm.kv("Nanotrasen", "ai_status_nanotrasen"),
            Dm.kv("Not Malf", "ai_status_not_malf"),
            Dm.kv("President", "ai_status_president"),
            Dm.kv("Rainbow", "ai_status_rainbow"),
            Dm.kv("Red October", "ai_status_red_october"),
            Dm.kv("Red", "ai_status_red"),
            Dm.kv("Static", "ai_status_static"),
            Dm.kv("Syndicat Meow", "ai_status_syndicat_meow"),
            Dm.kv("Text", "ai_status_text"),
            Dm.kv("Too Deep", "ai_status_too_deep"),
            Dm.kv("Triumvirate", "ai_status_triumvirate"),
            Dm.kv("Weird", "ai_status_weird"));
        Dm.append(ref gvars_datum_init_order, "ai_core_to_status_display_mapping");
    }

    // Combined list for AI status display preferences, including both emotion states and AI core display options
    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:63
    public DmList ai_status_display_all_options;

    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:63  (GLOBAL_LIST_INIT, GLOBAL_MANAGED(ai_status_display_all_options, list()))
    public virtual void InitGlobalai_status_display_all_options()
    {
        ai_status_display_all_options = Dm.list();
        Dm.append(ref gvars_datum_init_order, "ai_status_display_all_options");
    }
}

public static partial class GlobalProcs
{
    // Initialize the combined list at runtime
    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:66
    public static void init_ai_status_display_options()
    {
        if (Dm.truthy(length(GLOB.ai_status_display_all_options))) // Already initialized
            return;

        // Start with original emotes
        GLOB.ai_status_display_all_options = GLOB.ai_status_display_emotes.Copy();

        // Add AI core display mappings
        foreach (object core_option in Dm.each(GLOB.ai_core_to_status_display_mapping))
            GLOB.ai_status_display_all_options[core_option] = GLOB.ai_core_to_status_display_mapping[core_option];
    }
}

// Silicons SI4 (D326): the rest of the file: the AI's hologram choices and core display screens, and resolving a core display choice.
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:77  GLOBAL_LIST_INIT(ai_hologram_category_options, ...)
    public DmList ai_hologram_category_options;

    public virtual void InitGlobalai_hologram_category_options()
    {
        ai_hologram_category_options = Dm.list(
            Dm.kv(AI_HOLOGRAM_CATEGORY_ANIMAL, Dm.list(
                AI_HOLOGRAM_BEAR,
                AI_HOLOGRAM_CARP,
                AI_HOLOGRAM_CAT,
                AI_HOLOGRAM_CAT_2,
                AI_HOLOGRAM_CHICKEN,
                AI_HOLOGRAM_CORGI,
                AI_HOLOGRAM_COW,
                AI_HOLOGRAM_CRAB,
                AI_HOLOGRAM_FOX,
                AI_HOLOGRAM_GOAT,
                AI_HOLOGRAM_PARROT,
                AI_HOLOGRAM_PUG,
                AI_HOLOGRAM_SPIDER
            )),
            Dm.kv(AI_HOLOGRAM_CATEGORY_UNIQUE, Dm.list(
                AI_HOLOGRAM_DEFAULT,
                AI_HOLOGRAM_FACE,
                AI_HOLOGRAM_NARSIE,
                AI_HOLOGRAM_RATVAR,
                AI_HOLOGRAM_XENO
            ))
        );
        Dm.append(ref gvars_datum_init_order, "ai_hologram_category_options");
    }

    // New items need to also be added to ai_hologram_icon_state list
    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:105  GLOBAL_LIST_INIT(ai_hologram_icons, ...)
    public DmList ai_hologram_icons;

    public virtual void InitGlobalai_hologram_icons()
    {
        ai_hologram_icons = Dm.list(
            /* Animal */
            Dm.kv(AI_HOLOGRAM_BEAR, Dm.resource("icons/mob/simple/animal.dmi")),
            Dm.kv(AI_HOLOGRAM_CARP, Dm.resource("icons/mob/simple/carp.dmi")),
            Dm.kv(AI_HOLOGRAM_CAT, Dm.resource("icons/mob/simple/pets.dmi")),
            Dm.kv(AI_HOLOGRAM_CAT_2, Dm.resource("icons/mob/simple/pets.dmi")),
            Dm.kv(AI_HOLOGRAM_CHICKEN, Dm.resource("icons/mob/simple/animal.dmi")),
            Dm.kv(AI_HOLOGRAM_CORGI, Dm.resource("icons/mob/simple/pets.dmi")),
            Dm.kv(AI_HOLOGRAM_COW, Dm.resource("icons/mob/simple/cows.dmi")),
            Dm.kv(AI_HOLOGRAM_CRAB, Dm.resource("icons/mob/simple/animal.dmi")),
            Dm.kv(AI_HOLOGRAM_FOX, Dm.resource("icons/mob/simple/pets.dmi")),
            Dm.kv(AI_HOLOGRAM_GOAT, Dm.resource("icons/mob/simple/animal.dmi")),
            Dm.kv(AI_HOLOGRAM_PARROT, Dm.resource("icons/mob/simple/animal.dmi")),
            Dm.kv(AI_HOLOGRAM_PUG, Dm.resource("icons/mob/simple/pets.dmi")),
            Dm.kv(AI_HOLOGRAM_SPIDER, Dm.resource("icons/mob/simple/arachnoid.dmi")),
            /* Unique */
            Dm.kv(AI_HOLOGRAM_DEFAULT, Dm.resource("icons/mob/silicon/ai.dmi")),
            Dm.kv(AI_HOLOGRAM_FACE, Dm.resource("icons/mob/silicon/ai.dmi")),
            Dm.kv(AI_HOLOGRAM_NARSIE, Dm.resource("icons/mob/silicon/ai.dmi")),
            Dm.kv(AI_HOLOGRAM_RATVAR, Dm.resource("icons/mob/silicon/ai.dmi")),
            Dm.kv(AI_HOLOGRAM_XENO, Dm.resource("icons/mob/nonhuman-player/alien.dmi"))
        );
        Dm.append(ref gvars_datum_init_order, "ai_hologram_icons");
    }

    // New items need to also be added to ai_hologram_icons list
    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:128  GLOBAL_LIST_INIT(ai_hologram_icon_state, ...)
    public DmList ai_hologram_icon_state;

    public virtual void InitGlobalai_hologram_icon_state()
    {
        ai_hologram_icon_state = Dm.list(
            /* Animal */
            Dm.kv(AI_HOLOGRAM_BEAR, "bear"),
            Dm.kv(AI_HOLOGRAM_CARP, "carp"),
            Dm.kv(AI_HOLOGRAM_CAT, "cat"),
            Dm.kv(AI_HOLOGRAM_CAT_2, "cat2"),
            Dm.kv(AI_HOLOGRAM_CHICKEN, "chicken_brown"),
            Dm.kv(AI_HOLOGRAM_CORGI, "corgi"),
            Dm.kv(AI_HOLOGRAM_COW, "cow"),
            Dm.kv(AI_HOLOGRAM_CRAB, "crab"),
            Dm.kv(AI_HOLOGRAM_FOX, "fox"),
            Dm.kv(AI_HOLOGRAM_GOAT, "goat"),
            Dm.kv(AI_HOLOGRAM_PARROT, "parrot_fly"),
            Dm.kv(AI_HOLOGRAM_PUG, "pug"),
            Dm.kv(AI_HOLOGRAM_SPIDER, "guard"),
            /* Unique */
            Dm.kv(AI_HOLOGRAM_DEFAULT, "default"),
            Dm.kv(AI_HOLOGRAM_FACE, "floating face"),
            Dm.kv(AI_HOLOGRAM_NARSIE, "horror"),
            Dm.kv(AI_HOLOGRAM_RATVAR, "clock"),
            Dm.kv(AI_HOLOGRAM_XENO, "alienq")
        );
        Dm.append(ref gvars_datum_init_order, "ai_hologram_icon_state");
    }

    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:152  GLOBAL_LIST_INIT(ai_core_display_screens, sort_list(list(...)))
    public DmList ai_core_display_screens;

    public virtual void InitGlobalai_core_display_screens()
    {
        ai_core_display_screens = sort_list(Dm.list(
            ":thinking:",
            "Alien",
            "Angel",
            "Banned",
            "Bliss",
            "Blue",
            "Clown",
            "Database",
            "Dorf",
            "Firewall",
            "Fuzzy",
            "Gentoo",
            "Glitchman",
            "Gondola",
            "Goon",
            "Hades",
            "HAL 9000",
            "Heartline",
            "Helios",
            "House",
            "Inverted",
            "Matrix",
            "Monochrome",
            "Murica",
            "Nanotrasen",
            "Not Malf",
            "Portrait",
            "President",
            "Rainbow",
            "Random",
            "Red October",
            "Red",
            "Static",
            "Syndicat Meow",
            "Text",
            "Too Deep",
            "Triumvirate-M",
            "Triumvirate",
            "Weird"
        )) as DmList;
        Dm.append(ref gvars_datum_init_order, "ai_core_display_screens");
    }
}

public static partial class GlobalProcs
{
    /// A form of resolve_ai_icon that is guaranteed to never sleep.
    /// Not always accurate, but always synchronous.
    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:195
    public static string resolve_ai_icon_sync(object input = null)
    {
        if (!Dm.truthy(input) || !Dm.@in(input, GLOB.ai_core_display_screens))
            return "ai";
        if (Dm.eq(input, "Random"))
            input = pick(Dm.sub(GLOB.ai_core_display_screens, "Random"));
        return Dm.text("ai-[]", LOWER_TEXT(input));
    }

    // tg: code/modules/mob/living/silicon/ai/_preferences.dm:205
    public static async DmTask<string> resolve_ai_icon(object input = null)
    {
        if (Dm.eq(input, "Portrait"))
        {
            datum_portrait_picker tgui = Dm.@new<datum_portrait_picker>(usr); //create the datum
            await tgui.ui_interact(usr); //datum has a tgui component, here we open the window
            return "ai-portrait"; //just take this until they decide
        }

        return resolve_ai_icon_sync(input);
    }
}

