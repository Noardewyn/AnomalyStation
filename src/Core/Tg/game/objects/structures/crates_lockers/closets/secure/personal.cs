// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/secure/personal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'.

// tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:22
public partial class obj_structure_closet_secure_closet_personal : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:22
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        if (prob(50)) // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:24
        {
            Dm.@new<obj_item_storage_backpack_duffelbag>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:25
        }
        if (prob(50)) // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:26
        {
            Dm.@new<obj_item_storage_backpack>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:27
        }
        else
        {
            Dm.@new<obj_item_storage_backpack_satchel>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:29
        }
        Dm.@new<obj_item_radio_headset>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:30
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:7
public partial class obj_structure_closet_secure_closet_personal
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:9  var/static/list/choices (a proc-level static, rules 10.6)
    private static ref DmList Initialize__choices => ref TgState.Current.obj_structure_closet_secure_closet_personal__Initialize__choices;

    // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:7
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Initialize__choices == null)
            Initialize__choices = Dm.list("Personal");
        access_choices = Initialize__choices;
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:14
    public override object can_unlock(mob_living user = null, obj_item_card_id player_id = null, obj_item_card_id registered_id = null)
    {
        if (registered_id == null) //first time anyone can unlock
            return TRUE;
        if (Dm.truthy(Waiting.NoSleep(allowed__proc(user)))) //players with ACCESS_ALL_PERSONAL_LOCKERS can override your ID
            return TRUE;
        return player_id == registered_id ? TRUE : FALSE;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:51
public partial class obj_structure_closet_secure_closet_personal_cabinet : obj_structure_closet_secure_closet_personal
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:51
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_storage_backpack_satchel_leather_withwallet>(this);
        Dm.@new<obj_item_instrument_piano_synth>(this);
        Dm.@new<obj_item_radio_headset>(this);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:9  /obj/structure/closet/secure_closet/personal/Initialize's var/static/list/choices
    internal DmList obj_structure_closet_secure_closet_personal__Initialize__choices;
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:33  (gaps ST3, D333)
public partial class obj_structure_closet_secure_closet_personal_patient : obj_structure_closet_secure_closet_personal
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:35
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_clothing_under_color_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:36
        Dm.@new<obj_item_clothing_shoes_sneakers_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/personal.dm:37
    }
}
