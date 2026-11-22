// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_clothes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. The MODsuit parts a control unit deploys: the helmet (a hat can sit loose on it), the
// chestplate, the gauntlets, the boots (their rigstep footsteps follow the suit's slowdown) and the glasses and tie some themes
// use; none takes a speed potion. Left with rows (not-translated.md 219): the mesh texture the helmet and chestplate draw on the
// head and chest (datums/elements/equipment_bodypart_texture.dm, body textures) and the Cerulean modsuit icon (:51, server-side
// icon cutting and blending, as human_update_icons' cerulean variants). The var blocks are generated (D111).

// tg: code/modules/mod/mod_clothes.dm:1
public partial class obj_item_clothing_head_mod : obj_item_clothing_head
{
    // Even without a hat stabilizer, hats can be worn - however, they'll fall off very easily
    // tg: code/modules/mod/mod_clothes.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_SPEED_POTION, INNATE_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_hat_stabilizer), Dm.named("loose_hat", TRUE)));
        // AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_HEAD, /datum/bodypart_texture/mesh/space): the mesh
        // texture on the head (body textures are not ported)
        Dm.Deferred("code/modules/mod/mod_clothes.dm:19", "Initialize: AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_HEAD, /datum/bodypart_texture/mesh/space), the mesh texture on the head (bodypart textures, M10+)");
        return dot;
    }
}

// tg: code/modules/mod/mod_clothes.dm:21
public partial class obj_item_clothing_suit_mod : obj_item_clothing_suit
{
    // tg: code/modules/mod/mod_clothes.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_SPEED_POTION, INNATE_TRAIT);
        // AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_CHEST, /datum/bodypart_texture/mesh/space)
        Dm.Deferred("code/modules/mod/mod_clothes.dm:44", "Initialize: AddElement(/datum/element/equipment_bodypart_texture, BODY_ZONE_CHEST, /datum/bodypart_texture/mesh/space), the mesh texture on the chest (bodypart textures, M10+)");
        return dot;
    }

    /// This proc handles icon building for Ceruleans wearing modsuits.
    // tg: code/modules/mod/mod_clothes.dm:51
    public virtual object handle_cerulean_modsuit(icon base_icon = null, object key = null, object greyscale_colors = null, object physique = null)
    {
        // The pre-drawn Cerulean modsuit, else the theme's palette of modular parts blended over the cut base icon, and the
        // flippers: icons cut and blended on the server (icon procs), read only by cerulean_update_icons.dm, which is not ported.
        Dm.NotTranslated("code/modules/mod/mod_clothes.dm:51", "handle_cerulean_modsuit: the Cerulean modsuit icon (server-side icon rendering; M10+)");
        return null;
    }
}

// tg: code/modules/mod/mod_clothes.dm:104
public partial class obj_item_clothing_gloves_mod : obj_item_clothing_gloves
{
    // tg: code/modules/mod/mod_clothes.dm:119
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_SPEED_POTION, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/mod/mod_clothes.dm:123
public partial class obj_item_clothing_shoes_mod : obj_item_clothing_shoes
{
    // tg: code/modules/mod/mod_clothes.dm:138
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_SPEED_POTION, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mod/mod_clothes.dm:142
    public virtual async DmTask update_footstep_sounds()
    {
        float sw__1 = Dm.num(slowdown);
        if (sw__1 >= 0.3f && sw__1 <= INFINITY)
            await AddComponent(typeof(datum_component_shoe_footstep), Dm.list(Dm.resource("sound/items/modsuit/rigstep_chonk.ogg")), Dm.named("volume", 50));
        else if (sw__1 >= 0.2f && sw__1 <= 0.3f)
            await AddComponent(typeof(datum_component_shoe_footstep), Dm.list(Dm.resource("sound/items/modsuit/rigstep_heavy.ogg")), Dm.named("volume", 50));
        else if (sw__1 >= 0.1f && sw__1 <= 0.2f)
            await AddComponent(typeof(datum_component_shoe_footstep), Dm.list(Dm.resource("sound/items/modsuit/rigstep_medium.ogg")), Dm.named("volume", 50));
        else if (sw__1 >= -INFINITY && sw__1 <= 0.1f)
            await AddComponent(typeof(datum_component_shoe_footstep), Dm.list(Dm.resource("sound/items/modsuit/rigstep.ogg")), Dm.named("volume", 50));
    }
}

// tg: code/modules/mod/mod_clothes.dm:153
public partial class obj_item_clothing_glasses_mod : obj_item_clothing_glasses
{
    // tg: code/modules/mod/mod_clothes.dm:165
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_SPEED_POTION, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/mod/mod_clothes.dm:169
public partial class obj_item_clothing_neck_mod : obj_item_clothing_neck
{
    // tg: code/modules/mod/mod_clothes.dm:182
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_SPEED_POTION, INNATE_TRAIT);
        return dot;
    }
}
