// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/color.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M2 F1): the colour jumpsuits and jumpskirts, the random jumpsuit that a new assistant gets
// (/datum/colored_assistant/random, colorful_assistants.dm) and its pick (get_random_jumpsuit, get_random_jumpskirt).

// tg: code/modules/clothing/under/color.dm:1
[DmPath("/obj/item/clothing/under/color")]
public partial class obj_item_clothing_under_color : obj_item_clothing_under
{
    // tg: code/modules/clothing/under/color.dm:2-16
    public obj_item_clothing_under_color()
    {
        name = "jumpsuit";
        desc = "A standard issue colored jumpsuit. Variety is the spice of life!";
        icon = Dm.resource("icons/map_icons/clothing/under/color.dmi");
        icon_state = "/obj/item/clothing/under/color";
        post_init_icon_state = "jumpsuit";
        inhand_icon_state = "jumpsuit";
        worn_icon_state = "jumpsuit";
        worn_icon = Dm.resource("icons/mob/clothing/under/color.dmi");
        dying_key = DYE_REGISTRY_UNDER;
        greyscale_colors = "#3f3f3f";
        greyscale_config = typeof(datum_greyscale_config_jumpsuit);
        greyscale_config_worn = typeof(datum_greyscale_config_jumpsuit_worn);
        greyscale_config_inhand_left = typeof(datum_greyscale_config_jumpsuit_inhand_left);
        greyscale_config_inhand_right = typeof(datum_greyscale_config_jumpsuit_inhand_right);
        flags_1 = IS_PLAYER_COLORABLE_1;
    }
}

// tg: code/modules/clothing/under/color.dm:18
[DmPath("/obj/item/clothing/under/color/jumpskirt")]
public partial class obj_item_clothing_under_color_jumpskirt : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:19-25
    public obj_item_clothing_under_color_jumpskirt()
    {
        icon_state = "/obj/item/clothing/under/color/jumpskirt";
        post_init_icon_state = "jumpskirt";
        body_parts_covered = Dm.bitor(Dm.bitor(CHEST, GROIN), ARMS);
        dying_key = DYE_REGISTRY_JUMPSKIRT;
        female_sprite_flags = FEMALE_UNIFORM_TOP_ONLY;
        supports_variations_flags = CLOTHING_DIGITIGRADE_VARIATION_NO_NEW_ICON;
        bodyshapes_with_variations = NONE;
    }
}

/// Returns a random, acceptable jumpsuit typepath
public static partial class GlobalProcs
{
    // tg: code/modules/clothing/under/color.dm:28
    public static object get_random_jumpsuit()
    {
        return pick(
            Dm.sub(Dm.sub(Dm.sub(Dm.sub(Dm.sub(subtypesof(typeof(obj_item_clothing_under_color)),
                typesof(typeof(obj_item_clothing_under_color_jumpskirt))),
                typeof(obj_item_clothing_under_color_random)),
                typeof(obj_item_clothing_under_color_grey_ancient)),
                typeof(obj_item_clothing_under_color_black_ghost)),
                typeof(obj_item_clothing_under_rank_prisoner))
        );
    }
}

// tg: code/modules/clothing/under/color.dm:38
[DmPath("/obj/item/clothing/under/color/random")]
public partial class obj_item_clothing_under_color_random : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:39-41
    public obj_item_clothing_under_color_random()
    {
        icon = Dm.resource("icons/obj/clothing/under/color.dmi");
        icon_state = "random_jumpsuit";
        flags_1 = Dm.bitor(Dm.num(Dm.initial(typeof(obj_item_clothing_under_color), (obj_item_clothing_under_color p) => p.flags_1)), NO_NEW_GAGS_PREVIEW_1);
    }

    // tg: code/modules/clothing/under/color.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        Type C = get_random_jumpsuit() as Type;
        if (ishuman(loc))
        {
            mob_living_carbon_human H = loc as mob_living_carbon_human;
            Waiting.NoSleep(H.equip_to_slot_or_del(Dm.@new(C, H) as obj_item, ITEM_SLOT_ICLOTHING, initial: TRUE)); //or else you end up with naked assistants running around everywhere...
        }
        else
            Dm.@new(C, loc);
        return INITIALIZE_HINT_QDEL;
    }
}

/// Returns a random, acceptable jumpskirt typepath
public static partial class GlobalProcs
{
    // tg: code/modules/clothing/under/color.dm:54
    public static object get_random_jumpskirt()
    {
        return pick(
            Dm.sub(Dm.sub(subtypesof(typeof(obj_item_clothing_under_color_jumpskirt)),
                typeof(obj_item_clothing_under_color_jumpskirt_random)),
                typeof(obj_item_clothing_under_rank_prisoner_skirt))
        );
    }
}

// tg: code/modules/clothing/under/color.dm:61
[DmPath("/obj/item/clothing/under/color/jumpskirt/random")]
public partial class obj_item_clothing_under_color_jumpskirt_random : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:62-64
    public obj_item_clothing_under_color_jumpskirt_random()
    {
        icon = Dm.resource("icons/obj/clothing/under/color.dmi");
        icon_state = "random_jumpsuit"; //Skirt variant needed
        flags_1 = Dm.bitor(Dm.num(Dm.initial(typeof(obj_item_clothing_under_color_jumpskirt), (obj_item_clothing_under_color_jumpskirt p) => p.flags_1)), NO_NEW_GAGS_PREVIEW_1);
    }

    // tg: code/modules/clothing/under/color.dm:66
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        Type C = get_random_jumpskirt() as Type;
        if (ishuman(loc))
        {
            mob_living_carbon_human H = loc as mob_living_carbon_human;
            Waiting.NoSleep(H.equip_to_slot_or_del(Dm.@new(C, H) as obj_item, ITEM_SLOT_ICLOTHING, initial: TRUE));
        }
        else
            Dm.@new(C, loc);
        return INITIALIZE_HINT_QDEL;
    }
}

// tg: code/modules/clothing/under/color.dm:76
[DmPath("/obj/item/clothing/under/color/black")]
public partial class obj_item_clothing_under_color_black : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:77-79
    public obj_item_clothing_under_color_black()
    {
        name = "black jumpsuit";
        resistance_flags = NONE;
        flags_1 = Dm.bitor(Dm.num(Dm.initial(typeof(obj_item_clothing_under_color), (obj_item_clothing_under_color p) => p.flags_1)), NO_NEW_GAGS_PREVIEW_1);
    }
}

// tg: code/modules/clothing/under/color.dm:81
[DmPath("/obj/item/clothing/under/color/jumpskirt/black")]
public partial class obj_item_clothing_under_color_jumpskirt_black : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:82-83
    public obj_item_clothing_under_color_jumpskirt_black()
    {
        name = "black jumpskirt";
        flags_1 = Dm.bitor(Dm.num(Dm.initial(typeof(obj_item_clothing_under_color_jumpskirt), (obj_item_clothing_under_color_jumpskirt p) => p.flags_1)), NO_NEW_GAGS_PREVIEW_1);
    }
}

// tg: code/modules/clothing/under/color.dm:85
[DmPath("/obj/item/clothing/under/color/black/ghost")]
public partial class obj_item_clothing_under_color_black_ghost : obj_item_clothing_under_color_black
{
    // tg: code/modules/clothing/under/color.dm:86
    public obj_item_clothing_under_color_black_ghost()
    {
        item_flags = DROPDEL;
    }

    // tg: code/modules/clothing/under/color.dm:88
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, CULT_TRAIT);
        return dot;
    }
}

// tg: code/modules/clothing/under/color.dm:92
[DmPath("/obj/item/clothing/under/color/grey")]
public partial class obj_item_clothing_under_color_grey : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:93-96
    public obj_item_clothing_under_color_grey()
    {
        name = "grey jumpsuit";
        desc = "A tasteful grey jumpsuit that reminds you of the good old days.";
        icon_state = "/obj/item/clothing/under/color/grey";
        greyscale_colors = "#b3b3b3";
    }
}

// tg: code/modules/clothing/under/color.dm:98
[DmPath("/obj/item/clothing/under/color/jumpskirt/grey")]
public partial class obj_item_clothing_under_color_jumpskirt_grey : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:99-102
    public obj_item_clothing_under_color_jumpskirt_grey()
    {
        name = "grey jumpskirt";
        desc = "A tasteful grey jumpskirt that reminds you of the good old days.";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/grey";
        greyscale_colors = "#b3b3b3";
    }
}

// tg: code/modules/clothing/under/color.dm:104
[DmPath("/obj/item/clothing/under/color/grey/ancient")]
public partial class obj_item_clothing_under_color_grey_ancient : obj_item_clothing_under_color_grey
{
    // tg: code/modules/clothing/under/color.dm:105-115
    public obj_item_clothing_under_color_grey_ancient()
    {
        name = "ancient jumpsuit";
        desc = "A terribly ragged and frayed grey jumpsuit. It looks like it hasn't been washed in over a decade.";
        icon = Dm.resource("icons/obj/clothing/under/color.dmi");
        icon_state = "grey_ancient";
        post_init_icon_state = null;
        inhand_icon_state = "gy_suit";
        greyscale_config = null;
        greyscale_config_inhand_left = null;
        greyscale_config_inhand_right = null;
        greyscale_config_worn = null;
        can_adjust = FALSE;
    }
}

// tg: code/modules/clothing/under/color.dm:117
[DmPath("/obj/item/clothing/under/color/blue")]
public partial class obj_item_clothing_under_color_blue : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:118-120
    public obj_item_clothing_under_color_blue()
    {
        name = "blue jumpsuit";
        icon_state = "/obj/item/clothing/under/color/blue";
        greyscale_colors = "#52aecc";
    }
}

// tg: code/modules/clothing/under/color.dm:122
[DmPath("/obj/item/clothing/under/color/jumpskirt/blue")]
public partial class obj_item_clothing_under_color_jumpskirt_blue : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:123-125
    public obj_item_clothing_under_color_jumpskirt_blue()
    {
        name = "blue jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/blue";
        greyscale_colors = "#52aecc";
    }
}

// tg: code/modules/clothing/under/color.dm:127
[DmPath("/obj/item/clothing/under/color/green")]
public partial class obj_item_clothing_under_color_green : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:128-130
    public obj_item_clothing_under_color_green()
    {
        name = "green jumpsuit";
        icon_state = "/obj/item/clothing/under/color/green";
        greyscale_colors = "#9ed63a";
    }
}

// tg: code/modules/clothing/under/color.dm:132
[DmPath("/obj/item/clothing/under/color/jumpskirt/green")]
public partial class obj_item_clothing_under_color_jumpskirt_green : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:133-135
    public obj_item_clothing_under_color_jumpskirt_green()
    {
        name = "green jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/green";
        greyscale_colors = "#9ed63a";
    }
}

// tg: code/modules/clothing/under/color.dm:137
[DmPath("/obj/item/clothing/under/color/orange")]
public partial class obj_item_clothing_under_color_orange : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:138-141
    public obj_item_clothing_under_color_orange()
    {
        name = "orange jumpsuit";
        desc = "Don't wear this near paranoid security officers.";
        icon_state = "/obj/item/clothing/under/color/orange";
        greyscale_colors = "#ff8c19";
    }
}

// tg: code/modules/clothing/under/color.dm:143
[DmPath("/obj/item/clothing/under/color/jumpskirt/orange")]
public partial class obj_item_clothing_under_color_jumpskirt_orange : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:144-146
    public obj_item_clothing_under_color_jumpskirt_orange()
    {
        name = "orange jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/orange";
        greyscale_colors = "#ff8c19";
    }
}

// tg: code/modules/clothing/under/color.dm:148
[DmPath("/obj/item/clothing/under/color/pink")]
public partial class obj_item_clothing_under_color_pink : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:149-152
    public obj_item_clothing_under_color_pink()
    {
        name = "pink jumpsuit";
        desc = "Just looking at this makes you feel <i>fabulous</i>.";
        icon_state = "/obj/item/clothing/under/color/pink";
        greyscale_colors = "#ffa69b";
    }
}

// tg: code/modules/clothing/under/color.dm:154
[DmPath("/obj/item/clothing/under/color/jumpskirt/pink")]
public partial class obj_item_clothing_under_color_jumpskirt_pink : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:155-157
    public obj_item_clothing_under_color_jumpskirt_pink()
    {
        name = "pink jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/pink";
        greyscale_colors = "#ffa69b";
    }
}

// tg: code/modules/clothing/under/color.dm:159
[DmPath("/obj/item/clothing/under/color/red")]
public partial class obj_item_clothing_under_color_red : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:160-162
    public obj_item_clothing_under_color_red()
    {
        name = "red jumpsuit";
        icon_state = "/obj/item/clothing/under/color/red";
        greyscale_colors = "#eb0c07";
    }
}

// tg: code/modules/clothing/under/color.dm:164
[DmPath("/obj/item/clothing/under/color/jumpskirt/red")]
public partial class obj_item_clothing_under_color_jumpskirt_red : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:165-167
    public obj_item_clothing_under_color_jumpskirt_red()
    {
        name = "red jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/red";
        greyscale_colors = "#eb0c07";
    }
}

// tg: code/modules/clothing/under/color.dm:169
[DmPath("/obj/item/clothing/under/color/white")]
public partial class obj_item_clothing_under_color_white : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:170-172
    public obj_item_clothing_under_color_white()
    {
        name = "white jumpsuit";
        icon_state = "/obj/item/clothing/under/color/white";
        greyscale_colors = "#ffffff";
    }
}

// tg: code/modules/clothing/under/color.dm:174
[DmPath("/obj/item/clothing/under/color/jumpskirt/white")]
public partial class obj_item_clothing_under_color_jumpskirt_white : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:175-177
    public obj_item_clothing_under_color_jumpskirt_white()
    {
        name = "white jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/white";
        greyscale_colors = "#ffffff";
    }
}

// tg: code/modules/clothing/under/color.dm:179
[DmPath("/obj/item/clothing/under/color/yellow")]
public partial class obj_item_clothing_under_color_yellow : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:180-182
    public obj_item_clothing_under_color_yellow()
    {
        name = "yellow jumpsuit";
        icon_state = "/obj/item/clothing/under/color/yellow";
        greyscale_colors = "#ffe14d";
    }
}

// tg: code/modules/clothing/under/color.dm:184
[DmPath("/obj/item/clothing/under/color/jumpskirt/yellow")]
public partial class obj_item_clothing_under_color_jumpskirt_yellow : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:185-187
    public obj_item_clothing_under_color_jumpskirt_yellow()
    {
        name = "yellow jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/yellow";
        greyscale_colors = "#ffe14d";
    }
}

// tg: code/modules/clothing/under/color.dm:189
[DmPath("/obj/item/clothing/under/color/darkblue")]
public partial class obj_item_clothing_under_color_darkblue : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:190-192
    public obj_item_clothing_under_color_darkblue()
    {
        name = "dark blue jumpsuit";
        icon_state = "/obj/item/clothing/under/color/darkblue";
        greyscale_colors = "#3285ba";
    }
}

// tg: code/modules/clothing/under/color.dm:194
[DmPath("/obj/item/clothing/under/color/jumpskirt/darkblue")]
public partial class obj_item_clothing_under_color_jumpskirt_darkblue : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:195-197
    public obj_item_clothing_under_color_jumpskirt_darkblue()
    {
        name = "dark blue jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/darkblue";
        greyscale_colors = "#3285ba";
    }
}

// tg: code/modules/clothing/under/color.dm:199
[DmPath("/obj/item/clothing/under/color/teal")]
public partial class obj_item_clothing_under_color_teal : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:200-202
    public obj_item_clothing_under_color_teal()
    {
        name = "teal jumpsuit";
        icon_state = "/obj/item/clothing/under/color/teal";
        greyscale_colors = "#77f3b7";
    }
}

// tg: code/modules/clothing/under/color.dm:204
[DmPath("/obj/item/clothing/under/color/jumpskirt/teal")]
public partial class obj_item_clothing_under_color_jumpskirt_teal : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:205-207
    public obj_item_clothing_under_color_jumpskirt_teal()
    {
        name = "teal jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/teal";
        greyscale_colors = "#77f3b7";
    }
}

// tg: code/modules/clothing/under/color.dm:209
[DmPath("/obj/item/clothing/under/color/lightpurple")]
public partial class obj_item_clothing_under_color_lightpurple : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:210-212
    public obj_item_clothing_under_color_lightpurple()
    {
        name = "light purple jumpsuit";
        icon_state = "/obj/item/clothing/under/color/lightpurple";
        greyscale_colors = "#9f70cc";
    }
}

// tg: code/modules/clothing/under/color.dm:214
[DmPath("/obj/item/clothing/under/color/jumpskirt/lightpurple")]
public partial class obj_item_clothing_under_color_jumpskirt_lightpurple : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:215-217
    public obj_item_clothing_under_color_jumpskirt_lightpurple()
    {
        name = "light purple jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/lightpurple";
        greyscale_colors = "#9f70cc";
    }
}

// tg: code/modules/clothing/under/color.dm:219
[DmPath("/obj/item/clothing/under/color/darkgreen")]
public partial class obj_item_clothing_under_color_darkgreen : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:220-222
    public obj_item_clothing_under_color_darkgreen()
    {
        name = "dark green jumpsuit";
        icon_state = "/obj/item/clothing/under/color/darkgreen";
        greyscale_colors = "#6fbc22";
    }
}

// tg: code/modules/clothing/under/color.dm:224
[DmPath("/obj/item/clothing/under/color/jumpskirt/darkgreen")]
public partial class obj_item_clothing_under_color_jumpskirt_darkgreen : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:225-227
    public obj_item_clothing_under_color_jumpskirt_darkgreen()
    {
        name = "dark green jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/darkgreen";
        greyscale_colors = "#6fbc22";
    }
}

// tg: code/modules/clothing/under/color.dm:229
[DmPath("/obj/item/clothing/under/color/lightbrown")]
public partial class obj_item_clothing_under_color_lightbrown : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:230-232
    public obj_item_clothing_under_color_lightbrown()
    {
        name = "light brown jumpsuit";
        icon_state = "/obj/item/clothing/under/color/lightbrown";
        greyscale_colors = "#c59431";
    }
}

// tg: code/modules/clothing/under/color.dm:234
[DmPath("/obj/item/clothing/under/color/jumpskirt/lightbrown")]
public partial class obj_item_clothing_under_color_jumpskirt_lightbrown : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:235-237
    public obj_item_clothing_under_color_jumpskirt_lightbrown()
    {
        name = "light brown jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/lightbrown";
        greyscale_colors = "#c59431";
    }
}

// tg: code/modules/clothing/under/color.dm:239
[DmPath("/obj/item/clothing/under/color/brown")]
public partial class obj_item_clothing_under_color_brown : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:240-242
    public obj_item_clothing_under_color_brown()
    {
        name = "brown jumpsuit";
        icon_state = "/obj/item/clothing/under/color/brown";
        greyscale_colors = "#a17229";
    }
}

// tg: code/modules/clothing/under/color.dm:244
[DmPath("/obj/item/clothing/under/color/jumpskirt/brown")]
public partial class obj_item_clothing_under_color_jumpskirt_brown : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:245-247
    public obj_item_clothing_under_color_jumpskirt_brown()
    {
        name = "brown jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/brown";
        greyscale_colors = "#a17229";
    }
}

// tg: code/modules/clothing/under/color.dm:249
[DmPath("/obj/item/clothing/under/color/maroon")]
public partial class obj_item_clothing_under_color_maroon : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:250-252
    public obj_item_clothing_under_color_maroon()
    {
        name = "maroon jumpsuit";
        icon_state = "/obj/item/clothing/under/color/maroon";
        greyscale_colors = "#cc295f";
    }
}

// tg: code/modules/clothing/under/color.dm:254
[DmPath("/obj/item/clothing/under/color/jumpskirt/maroon")]
public partial class obj_item_clothing_under_color_jumpskirt_maroon : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:255-257
    public obj_item_clothing_under_color_jumpskirt_maroon()
    {
        name = "maroon jumpskirt";
        icon_state = "/obj/item/clothing/under/color/jumpskirt/maroon";
        greyscale_colors = "#cc295f";
    }
}

// tg: code/modules/clothing/under/color.dm:259
[DmPath("/obj/item/clothing/under/color/rainbow")]
public partial class obj_item_clothing_under_color_rainbow : obj_item_clothing_under_color
{
    // tg: code/modules/clothing/under/color.dm:260-271
    public obj_item_clothing_under_color_rainbow()
    {
        name = "rainbow jumpsuit";
        desc = "A multi-colored jumpsuit!";
        icon = Dm.resource("icons/obj/clothing/under/color.dmi");
        icon_state = "rainbow";
        post_init_icon_state = null;
        inhand_icon_state = "rainbow";
        greyscale_config = null;
        greyscale_config_inhand_left = null;
        greyscale_config_inhand_right = null;
        greyscale_config_worn = null;
        can_adjust = FALSE;
        flags_1 = NONE;
    }

    // tg: code/modules/clothing/under/color.dm:273
    public override object get_general_color(icon base_icon = null)
    {
        return "#3f3f3f";
    }
}

// tg: code/modules/clothing/under/color.dm:276
[DmPath("/obj/item/clothing/under/color/jumpskirt/rainbow")]
public partial class obj_item_clothing_under_color_jumpskirt_rainbow : obj_item_clothing_under_color_jumpskirt
{
    // tg: code/modules/clothing/under/color.dm:277-288
    public obj_item_clothing_under_color_jumpskirt_rainbow()
    {
        name = "rainbow jumpskirt";
        desc = "A multi-colored jumpskirt!";
        icon = Dm.resource("icons/obj/clothing/under/color.dmi");
        icon_state = "rainbow_skirt";
        post_init_icon_state = null;
        inhand_icon_state = "rainbow";
        greyscale_config = null;
        greyscale_config_inhand_left = null;
        greyscale_config_inhand_right = null;
        greyscale_config_worn = null;
        can_adjust = FALSE;
        flags_1 = NONE;
    }
}
