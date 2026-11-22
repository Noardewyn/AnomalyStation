// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/costume.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the griffin boots' and the ducky shoes' pockets, the bronze boots' clockwork steps, the jester's
// jingle and the ducky shoes' quack and waddle). The glow shoes' colour picker (gags_recolorable) waits for the GAGS recolouring
// (not-translated.md 209) and their glow is DV12; the fishing difficulty elements wait for fishing. The var blocks are generated (D111).

// tg: code/modules/clothing/shoes/costume.dm:12
public partial class obj_item_clothing_shoes_griffin : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/costume.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        return dot;
    }
}

// tg: code/modules/clothing/shoes/costume.dm:45
public partial class obj_item_clothing_shoes_bronze : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/costume.dm:53
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_squeak), Dm.list(Dm.kv(Dm.resource("sound/machines/clockcult/integration_cog_install.ogg"), 1), Dm.kv(Dm.resource("sound/effects/magic/clockwork/fellowship_armory.ogg"), 1)), 50, Dm.named("extrarange", SHORT_RANGE_SOUND_EXTRARANGE)));
        Dm.Deferred("code/modules/clothing/shoes/costume.dm:56", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 4): fishing (M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/shoes/costume.dm:79
public partial class obj_item_clothing_shoes_glow : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/costume.dm:91
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/shoes/costume.dm:93", "Initialize: AddElement(/datum/element/gags_recolorable): the spray can's GAGS recolouring (M10+)");
        Waiting.NoSleep(update_icon(UPDATE_OVERLAYS));
        return dot;
    }

    // tg: code/modules/clothing/shoes/costume.dm:96
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        // DEVIATION(DV12): if(!isinhands) . += emissive_appearance(DEFAULT_SHOES_FILE, "glow_shoes_emissive", src, alpha = src.alpha):
        // the worn glow on the emissive plane is not created
        return dot;
    }

    // tg: code/modules/clothing/shoes/costume.dm:101
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): . += emissive_appearance('icons/obj/clothing/shoes.dmi', "glow_shoes_emissive", offset_spokesman = src,
        // alpha = src.alpha): the glow on the emissive plane is not created
        return dot;
    }
}

// tg: code/modules/clothing/shoes/costume.dm:111
public partial class obj_item_clothing_shoes_jester_shoes : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/costume.dm:123
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(LoadComponent(typeof(datum_component_squeak), Dm.list(Dm.kv(Dm.resource("sound/effects/jingle.ogg"), 1)), 50, Dm.named("falloff_exponent", 20), Dm.named("step_delay_override", 0)));
        return dot;
    }
}

// tg: code/modules/clothing/shoes/costume.dm:128
public partial class obj_item_clothing_shoes_ducky_shoes : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/costume.dm:134
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        Waiting.NoSleep(LoadComponent(typeof(datum_component_squeak), Dm.list(Dm.kv(Dm.resource("sound/effects/quack.ogg"), 1)), 50, Dm.named("falloff_exponent", 20)));
        Dm.Deferred("code/modules/clothing/shoes/costume.dm:139", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -7): fishing (M10+)"); //deploy tactical duckling lure
        return dot;
    }

    // tg: code/modules/clothing/shoes/costume.dm:141
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_FEET)))
            await user.AddElementTrait(TRAIT_WADDLING, SHOES_TRAIT, typeof(datum_element_waddling));
    }

    // tg: code/modules/clothing/shoes/costume.dm:146
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        REMOVE_TRAIT(user, TRAIT_WADDLING, SHOES_TRAIT);
        return dot;
    }
}
