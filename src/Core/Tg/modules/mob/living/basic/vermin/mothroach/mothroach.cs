// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/vermin/mothroach/mothroach.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/vermin/mothroach/mothroach.dm:2
public partial class mob_living_basic_mothroach : mob_living_basic
{
    ///list of pet commands we follow
    // tg: code/modules/mob/living/basic/vermin/mothroach/mothroach.dm:35
    private static DmList pet_commands => TgState.Current.mob_living_basic_mothroach__pet_commands ??= Dm.list(typeof(datum_pet_command_idle), typeof(datum_pet_command_free), typeof(datum_pet_command_follow_start_active), typeof(datum_pet_command_perform_trick_sequence));

    // tg: code/modules/mob/living/basic/vermin/mothroach/mothroach.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList food_types = TgState.Current.mob_living_basic_mothroach__food_types ??= Dm.list(typeof(obj_item_clothing));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), null, null, null, null, null, null, food_types));
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_FOODS, typecacheof(food_types));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "squeak"));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_MOTHROACH, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        add_verb(this, "/mob/living/proc/toggle_resting");
        add_traits(Dm.list(TRAIT_MOTH_EMOTES_ALLOWED, TRAIT_VENTCRAWLER_ALWAYS), INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/basic/vermin/mothroach/mothroach.dm:56
    public override async DmTask toggle_resting()
    {
        await base.toggle_resting();
        if (stat == DEAD)
            return;
        if (Dm.truthy(resting))
            icon_state = Dm.text("[]_rest", icon_living);
        else
            icon_state = Dm.text("[]", icon_living);
        await regenerate_icons();
    }

    // tg: code/modules/mob/living/basic/vermin/mothroach/mothroach.dm:65
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (this.stat == DEAD)
            return dot;
        playsound(loc, Dm.resource("sound/mobs/humanoids/moth/scream_moth.ogg"), 50, TRUE);
        return dot;
    }

    // tg: code/modules/mob/living/basic/vermin/mothroach/mothroach.dm:72
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby(attacking_item, user, modifiers, attack_modifiers);
        if (this.stat == DEAD)
            return dot;
        playsound(loc, Dm.resource("sound/mobs/humanoids/moth/scream_moth.ogg"), 50, TRUE);
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_mothroach__pet_commands;
    internal DmList mob_living_basic_mothroach__food_types;
}
