// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/cain_and_abel/dagger_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mining/lavaland/cain_and_abel/dagger_abilities.dm:1
public partial class datum_action_cooldown_dagger_swing : datum_action_cooldown
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_abilities.dm:23 (var/static/list/possible_sounds)
    public static DmList possible_sounds => TgState.Current.datum_action_cooldown_dagger_swing__possible_sounds ??= Dm.list(
        Dm.resource("sound/items/weapons/cain_and_abel/dagger_slash_1.ogg"),
        Dm.resource("sound/items/weapons/cain_and_abel/dagger_slash_2.ogg"),
        Dm.resource("sound/items/weapons/cain_and_abel/dagger_slash_3.ogg"),
        Dm.resource("sound/items/weapons/cain_and_abel/dagger_slash_4.ogg"),
        Dm.resource("sound/items/weapons/cain_and_abel/dagger_slash_5.ogg"),
        Dm.resource("sound/items/weapons/cain_and_abel/dagger_slash_6.ogg"));

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_abilities.dm:11
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        mob_living living_owner = owner as mob_living;
        obj_item_cain_and_abel cain_and_abel = target as obj_item_cain_and_abel;

        if (!Dm.truthy(living_owner.is_holding(cain_and_abel)))
        {
            owner.balloon_alert(owner, "must be held");
            return FALSE;
        }

        if (!Dm.truthy(cain_and_abel.check_wield(owner as mob_living)))
        {
            owner.balloon_alert(owner, "offhand busy!");
            return TRUE;
        }

        await living_owner.apply_status_effect(typeof(datum_status_effect_dagger_swinging));

        DmList sounds_to_pick_from = possible_sounds.Copy();
        for (float index = 0, index__end = 5; index <= index__end; index++)
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), owner, pick_n_take(sounds_to_pick_from), 65, TRUE), 0.15f * SECONDS * index, file: "code/modules/mining/lavaland/cain_and_abel/dagger_abilities.dm", line: 36);
        StartCooldown();
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_abilities.dm:23 (var/static/list/possible_sounds)
    internal DmList datum_action_cooldown_dagger_swing__possible_sounds;
}
