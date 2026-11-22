// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/stoats/stoat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/stoats/stoat.dm:2
public partial class mob_living_basic_stoat : mob_living_basic
{
    ///list of pet commands we follow
    // tg: code/modules/mob/living/basic/stoats/stoat.dm:39
    private static DmList pet_commands => TgState.Current.mob_living_basic_stoat__pet_commands ??= Dm.list(typeof(datum_pet_command_idle), typeof(datum_pet_command_move), typeof(datum_pet_command_free), typeof(datum_pet_command_attack), typeof(datum_pet_command_follow), typeof(datum_pet_command_fetch));

    // tg: code/modules/mob/living/basic/stoats/stoat.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_tiny_mob_hunter)));
        DmList eatable_food = TgState.Current.mob_living_basic_stoat__eatable_food ??= Dm.list(typeof(obj_item_food_deadmouse), typeof(obj_item_food_egg));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), eatable_food, 70f, 0f));
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_FOODS, typecacheof(eatable_food));
        Waiting.NoSleep(AddElement(typeof(datum_element_wears_collar)));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_STOAT, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        if (Dm.truthy(can_breed))
            add_breeding_component();
        DmList display_emote = TgState.Current.mob_living_basic_stoat__display_emote ??= Dm.list(
            Dm.kv(BB_EMOTE_SAY, Dm.list("Chirp chirp chirp!")),
            Dm.kv(BB_EMOTE_SEE, Dm.list("sweeps its tail!", "jumps around!", "licks its fur!")),
            Dm.kv(BB_SPEAK_CHANCE, 2f),
            Dm.kv(BB_EMOTE_SOUND, Dm.list(Dm.resource("sound/mobs/non-humanoids/stoat/stoat_sounds.ogg"))));
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_MOB_SPEAK_LINES, display_emote);
        return dot;
    }

    // tg: code/modules/mob/living/basic/stoats/stoat.dm:77
    public virtual void add_breeding_component()
    {
        DmList baby_paths = TgState.Current.mob_living_basic_stoat__baby_paths ??= Dm.list(Dm.kv(typeof(mob_living_basic_stoat_kit), 100f)); // Placeholder until we get proper baby stoats
        Waiting.NoSleep(AddComponent(typeof(datum_component_breed), typecacheof(Dm.list(typeof(mob_living_basic_stoat))), null, baby_paths));
    }
}

// tg: code/modules/mob/living/basic/stoats/stoat.dm:90
public partial class mob_living_basic_stoat_kit : mob_living_basic_stoat
{
    // tg: code/modules/mob/living/basic/stoats/stoat.dm:102
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_growth_and_differentiation), 20 * MINUTES, typeof(mob_living_basic_stoat), 100f, 0.5f, 1f, null, Dm.list(COMSIG_MOB_CLIENT_LOGIN)));
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_stoat__pet_commands;
    internal DmList mob_living_basic_stoat__eatable_food;
    internal DmList mob_living_basic_stoat__display_emote;
    internal DmList mob_living_basic_stoat__baby_paths;
}
