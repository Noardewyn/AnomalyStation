// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/clown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the clown shoes: the squeak, the waddle, the mood, the pockets and the fishing difficulty element.
// Syndicate N3 (D316): the combat clown shoes and the mk-honk combat shoes of the clown operatives.

// tg: code/modules/clothing/shoes/clown.dm:1
public partial class obj_item_clothing_shoes_clown_shoes : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/clown.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes_clown)));
        Waiting.NoSleep(LoadComponent(typeof(datum_component_squeak), squeak_sound, 50, Dm.named("falloff_exponent", 20))); //die off quick please
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CLOWN, CELL_VIRUS_TABLE_GENERIC, rand(2, 3), 0));
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 3)); //Goofy
        Dm.Deferred("code/modules/clothing/shoes/clown.dm:17", "Initialize: AddElement(/datum/element/ignites_matches): the matches (J3, smoking)");
        return dot;
    }

    // tg: code/modules/clothing/shoes/clown.dm:21
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_FEET)))
        {
            if (Dm.truthy(enabled_waddle))
                await user.AddElementTrait(TRAIT_WADDLING, SHOES_TRAIT, typeof(datum_element_waddling));
            if (is_clown_job(user.mind?.assigned_role))
                await ((mob_living)user).add_mood_event("clownshoes", typeof(datum_mood_event_clownshoes));
        }
    }

    // tg: code/modules/clothing/shoes/clown.dm:29
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        REMOVE_TRAIT(user, TRAIT_WADDLING, SHOES_TRAIT);
        if (is_clown_job(user.mind?.assigned_role))
            ((mob_living)user).clear_mood_event("clownshoes");
        return dot;
    }

    // tg: code/modules/clothing/shoes/clown.dm:35
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        if (!isliving(user))
            return CLICK_ACTION_BLOCKING;
        if (user.get_active_held_item() != this)
        {
            to_chat(user, span_warning(I18n.T("You must hold the [] in your hand to do this!", this)));
            return CLICK_ACTION_BLOCKING;
        }
        if (!Dm.truthy(enabled_waddle))
        {
            to_chat(user, span_notice(I18n.T("You switch off the waddle dampeners!")));
            enabled_waddle = TRUE;
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You switch on the waddle dampeners!")));
            enabled_waddle = FALSE;
        }
        return CLICK_ACTION_SUCCESS;
    }
}

//COMBAT CLOWN SHOES
//Clown shoes with combat stats and noslip. Of course they still squeak.
// tg: code/modules/clothing/shoes/clown.dm:75
public partial class obj_item_clothing_shoes_clown_shoes_combat : obj_item_clothing_shoes_clown_shoes
{
    // tg: code/modules/clothing/shoes/clown.dm:95
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        return dot;
    }
}

//The super annoying version
// tg: code/modules/clothing/shoes/clown.dm:105
public partial class obj_item_clothing_shoes_clown_shoes_banana_shoes_combat : obj_item_clothing_shoes_clown_shoes_banana_shoes
{
    /// Recharging rate in PPS (peels per second)
    // tg: code/modules/clothing/shoes/clown.dm:100
    private const int BANANA_SHOES_RECHARGE_RATE = 17;
    // tg: code/modules/clothing/shoes/clown.dm:101
    private const int BANANA_SHOES_MAX_CHARGE = 3000;

    // tg: code/modules/clothing/shoes/clown.dm:125
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        Waiting.NoSleep(bananium.insert_amount_mat(BANANA_SHOES_MAX_CHARGE, typeof(datum_material_bananium)));

        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/clothing/shoes/clown.dm:133
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        float bananium_amount = Waiting.NoSleep(bananium.get_material_amount(typeof(datum_material_bananium)));
        if (bananium_amount < BANANA_SHOES_MAX_CHARGE)
            Waiting.NoSleep(bananium.insert_amount_mat(min(BANANA_SHOES_RECHARGE_RATE * Dm.num(seconds_per_tick), BANANA_SHOES_MAX_CHARGE - bananium_amount), typeof(datum_material_bananium)));
        return null;
    }

    // tg: code/modules/clothing/shoes/clown.dm:138
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await ui_action_click(user);
        return null;
    }
}
