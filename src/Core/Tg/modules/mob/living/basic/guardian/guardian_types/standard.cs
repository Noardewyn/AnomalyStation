// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_types/standard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file. The var blocks are generated.

/// Plain, but durable and strong. Can destroy walls.
// tg: code/modules/mob/living/basic/guardian/guardian_types/standard.dm:2
public partial class mob_living_basic_guardian_standard : mob_living_basic_guardian
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/standard.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_wall_tearer), Dm.named("allow_reinforced", FALSE), Dm.named("tear_time", 1.5f * SECONDS)));
        datum_action_select_guardian_battlecry cry = Dm.@new<datum_action_select_guardian_battlecry>(this);
        Waiting.NoSleep(cry.Grant(this));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/standard.dm:24
    public override async DmTask do_attack_animation(atom attacked_atom = null, object visual_effect_icon = null, obj_item used_item = null, object no_effect = null, float? fov_effect = null, object item_animation_override = null)
    {
        await base.do_attack_animation(attacked_atom, visual_effect_icon, used_item, no_effect, fov_effect, item_animation_override);
        if (!isliving(attacked_atom) && !isclosedturf(attacked_atom))
            return;
        string msg = "";
        for (float i = 1; i <= 9; i++)
            msg += battlecry;
        await say(Dm.text("[]!!", msg), ignore_spam: TRUE);
        for (float sounds = 1; sounds <= 4; sounds++)
            _addtimer(Dm.@new<datum_callback>(this, nameof(do_attack_sound), attacked_atom), sounds * DECISECONDS, TIMER_DELETE_ME, file: "code/modules/mob/living/basic/guardian/guardian_types/standard.dm", line: 34);
    }

    /// Echo our punching sounds
    // tg: code/modules/mob/living/basic/guardian/guardian_types/standard.dm:36
    public virtual void do_attack_sound(atom playing_from = null)
    {
        if (QDELETED(playing_from))
            return;
        playsound(playing_from, attack_sound, 50, TRUE, TRUE);
    }
}

/// Action to change our battlecry
// tg: code/modules/mob/living/basic/guardian/guardian_types/standard.dm:42
public partial class datum_action_select_guardian_battlecry : datum_action
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/standard.dm:52
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        if (!istype(owner, typeof(mob_living_basic_guardian_standard)))
            return FALSE;
        return await base.IsAvailable(feedback);
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/standard.dm:57
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_basic_guardian_standard stand = (mob_living_basic_guardian_standard)owner;
        object input = await tgui_input_text(owner, "What do you want your battlecry to be?", "Battle Cry", max_length: max_length);
        if (!Dm.truthy(input))
            return dot;
        stand.battlecry = Dm.str(input);
        return dot;
    }
}
