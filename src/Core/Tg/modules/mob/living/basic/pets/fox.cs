// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/fox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the cult and the swab (rows) (gaps package C2).

/* Foxes.
*
* Foxes are cowardly creatures that will hunt any small animals, but only when no one is looking.
*/

// tg: code/modules/mob/living/basic/pets/fox.dm:7
public partial class mob_living_basic_pet_fox : mob_living_basic_pet
{
    ///list of our pet commands we follow
    // tg: code/modules/mob/living/basic/pets/fox.dm:32
    private static DmList pet_commands => TgState.Current.mob_living_basic_pet_fox__pet_commands ??= Dm.list(typeof(datum_pet_command_idle), typeof(datum_pet_command_move), typeof(datum_pet_command_free), typeof(datum_pet_command_follow_start_active), typeof(datum_pet_command_attack), typeof(datum_pet_command_perform_trick_sequence));

    // tg: code/modules/mob/living/basic/pets/fox.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        Waiting.NoSleep(AddElement(typeof(datum_element_cultist_pet)));
        Waiting.NoSleep(AddElement(typeof(datum_element_wears_collar)));
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "yap"));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW));
        Waiting.NoSleep(AddElement(typeof(datum_element_tiny_mob_hunter), MOB_SIZE_SMALL));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_FOX, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_pet_fox__pet_commands;
}
