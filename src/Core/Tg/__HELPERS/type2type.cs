// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/type2type.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only file2list, modulo_angle_to_dir, angle2dir, dir2angle, type2top, dir2offset (M3 P3: the light fixture's
// light offset) and heat2colour with its channels (M4b slice 3: the hotspots' colour), body_zone2cover_flags, cover_flags2body_zones (M6 slice 2: the clothing's zone damage; the carbon inventory's copy moved here), slot2body_zone (gaps VI1), color_matrix2color_hex, dir2text and text2dir (M5 slice 2: the materials' contact effects and colours, the floor tiles' rotation) translated (the configuration reads its
// files with file2list and names its entries with type2top, boot package, wave 3; the Move chain and the mob's space
// movement turn directions into angles and back, package D, wave 4); the other conversions of type2type.dm are translated
// with the code that uses them.

/*
 * Holds procs designed to change one type of value, into another.
 * Contains:
 * file2list
 * angle2dir
 * angle2text
 * text2dir_extended & dir2text_short
 */
public static partial class GlobalProcs
{
    //Splits the text of a file at seperator and returns them in a list.
    //returns an empty list if the file doesn't exist
    // tg: code/__HELPERS/type2type.dm:14
    public static object file2list(this DmWorld world, object filename = null, string seperator = null, float? trim = null)
    {
        seperator ??= "\n";
        trim ??= TRUE;
        if (Dm.truthy(trim))
            return splittext(GlobalProcs.trim(file2text(filename)), seperator);
        return splittext(file2text(filename), seperator);
    }

    //Converts an angle (degrees) into a ss13 direction
    // tg: code/__HELPERS/type2type.dm:65  #define angle2dir(X) (GLOB.modulo_angle_to_dir[round((((X%360)+382.5)%360)/45)+1])
    // (a function-like macro defined outside __DEFINES and never #undef'd: a GlobalProcs member, rules 11.1)
    public static object angle2dir(float X)
    {
        return GLOB.modulo_angle_to_dir[round(Dm.mod(Dm.mod(X, 360) + 382.5f, 360) / 45) + 1];
    }

    // tg: code/__HELPERS/type2type.dm:67
    public static float angle2dir_cardinal(float degree)
    {
        degree = SIMPLIFY_DEGREES(degree);
        float sw__1 = round(degree, 0.1f);
        if (sw__1 >= 315.5f && sw__1 <= 360 || sw__1 >= 0 && sw__1 <= 45.5f)
            return NORTH;
        else if (sw__1 >= 45.6f && sw__1 <= 135.5f)
            return EAST;
        else if (sw__1 >= 135.6f && sw__1 <= 225.5f)
            return SOUTH;
        else if (sw__1 >= 225.6f && sw__1 <= 315.5f)
            return WEST;
        return 0;
    }

    //returns the north-zero clockwise angle in degrees, given a direction
    // tg: code/__HELPERS/type2type.dm:80
    public static object dir2angle(object D = null)
    {
        object sw__1 = D;
        if (Dm.eq(sw__1, NORTH))
            return 0;
        else if (Dm.eq(sw__1, SOUTH))
            return 180;
        else if (Dm.eq(sw__1, EAST))
            return 90;
        else if (Dm.eq(sw__1, WEST))
            return 270;
        else if (Dm.eq(sw__1, NORTHEAST))
            return 45;
        else if (Dm.eq(sw__1, SOUTHEAST))
            return 135;
        else if (Dm.eq(sw__1, NORTHWEST))
            return 315;
        else if (Dm.eq(sw__1, SOUTHWEST))
            return 225;
        else
            return null;
    }

    //returns a string the last bit of a type, without the preceeding '/'
    // tg: code/__HELPERS/type2type.dm:355
    public static object type2top(object typepath = null)
    {
        //handle the builtins manually
        if (!ispath(typepath))
            return null;
        object sw__1 = typepath;
        if (Dm.eq(sw__1, typeof(datum)))
            return "datum";
        else if (Dm.eq(sw__1, typeof(atom)))
            return "atom";
        else if (Dm.eq(sw__1, typeof(obj)))
            return "obj";
        else if (Dm.eq(sw__1, typeof(mob)))
            return "mob";
        else if (Dm.eq(sw__1, typeof(area)))
            return "area";
        else if (Dm.eq(sw__1, typeof(turf)))
            return "turf";
        else //regex everything else (works for /proc too)
            return LOWER_TEXT(replacetext(Dm.text("[]", typepath), Dm.text("[]/", Dm.parent_type((Type)typepath)), ""));
    }
}

public partial class datum_controller_global_vars
{
    //Converts an angle (degrees) into a ss13 direction
    // tg: code/__HELPERS/type2type.dm:64  GLOBAL_LIST_INIT(modulo_angle_to_dir, list(NORTH,NORTHEAST,EAST,SOUTHEAST,SOUTH,SOUTHWEST,WEST,NORTHWEST))
    public DmList modulo_angle_to_dir;

    // tg: code/__HELPERS/type2type.dm:64  (GLOBAL_MANAGED(modulo_angle_to_dir, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmodulo_angle_to_dir()
    {
        modulo_angle_to_dir = Dm.list(NORTH, NORTHEAST, EAST, SOUTHEAST, SOUTH, SOUTHWEST, WEST, NORTHWEST);
        Dm.append(ref gvars_datum_init_order, "modulo_angle_to_dir");
    }
}

public static partial class GlobalProcs
{
    /// Returns a list(x, y), being the change in position required to step in the passed in direction
    // tg: code/__HELPERS/type2type.dm:109  (M3 P3)
    public static object dir2offset(object dir = null)
    {
        object sw__1 = dir;
        if (Dm.eq(sw__1, NORTH))
            return Dm.list(0, 1);
        else if (Dm.eq(sw__1, SOUTH))
            return Dm.list(0, -1);
        else if (Dm.eq(sw__1, EAST))
            return Dm.list(1, 0);
        else if (Dm.eq(sw__1, WEST))
            return Dm.list(-1, 0);
        else if (Dm.eq(sw__1, NORTHEAST))
            return Dm.list(1, 1);
        else if (Dm.eq(sw__1, SOUTHEAST))
            return Dm.list(1, -1);
        else if (Dm.eq(sw__1, NORTHWEST))
            return Dm.list(-1, 1);
        else if (Dm.eq(sw__1, SOUTHWEST))
            return Dm.list(-1, -1);
        else
            return Dm.list(0, 0);
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/type2type.dm:143  (M10 slice I)
    public static object rights2text(object rights = null, object seperator = null, object prefix = null)
    {
        seperator ??= "";
        prefix ??= "+";
        seperator = Dm.text("[][]", seperator, prefix);
        int r = (int)Dm.num(rights);
        string dot = null;
        if ((r & R_BUILD) != 0) dot += Dm.text("[]BUILDMODE", seperator);
        if ((r & R_ADMIN) != 0) dot += Dm.text("[]ADMIN", seperator);
        if ((r & R_BAN) != 0) dot += Dm.text("[]BAN", seperator);
        if ((r & R_FUN) != 0) dot += Dm.text("[]FUN", seperator);
        if ((r & R_SERVER) != 0) dot += Dm.text("[]SERVER", seperator);
        if ((r & R_DEBUG) != 0) dot += Dm.text("[]DEBUG", seperator);
        if ((r & R_POSSESS) != 0) dot += Dm.text("[]POSSESS", seperator);
        if ((r & R_PERMISSIONS) != 0) dot += Dm.text("[]PERMISSIONS", seperator);
        if ((r & R_STEALTH) != 0) dot += Dm.text("[]STEALTH", seperator);
        if ((r & R_POLL) != 0) dot += Dm.text("[]POLL", seperator);
        if ((r & R_VAREDIT) != 0) dot += Dm.text("[]VAREDIT", seperator);
        if ((r & R_SOUND) != 0) dot += Dm.text("[]SOUND", seperator);
        if ((r & R_SPAWN) != 0) dot += Dm.text("[]SPAWN", seperator);
        if ((r & R_AUTOADMIN) != 0) dot += Dm.text("[]AUTOLOGIN", seperator);
        if ((r & R_DBRANKS) != 0) dot += Dm.text("[]DBRANKS", seperator);
        if (dot == null)
            dot = "NONE";
        return dot;
    }
}

public static partial class GlobalProcs
{
    //adapted from http://www.tannerhelland.com/4435/convert-temperature-rgb-algorithm-code/
    // tg: code/__HELPERS/type2type.dm:257  (M4b slice 3)
    public static object heat2colour(object temp = null)
    {
        return rgb(Dm.num(heat2colour_r(temp)), Dm.num(heat2colour_g(temp)), Dm.num(heat2colour_b(temp)));
    }

    // tg: code/__HELPERS/type2type.dm:261  (M4b slice 3)
    public static object heat2colour_r(object temp = null)
    {
        float t = Dm.num(temp);
        t /= 100;
        if (t <= 66)
            return 255;
        else
            return max(0, min(255, 329.698727446f * Dm.pow(t - 60, -0.1332047592f)));
    }

    // tg: code/__HELPERS/type2type.dm:269  (M4b slice 3)
    public static object heat2colour_g(object temp = null)
    {
        float t = Dm.num(temp);
        t /= 100;
        if (t <= 66)
            return max(0, min(255, 99.4708025861f * log(t) - 161.1195681661f));
        else
            return max(0, min(255, 288.1221685293f * (Dm.pow(t - 60, -0.075148492f))));
    }

    // tg: code/__HELPERS/type2type.dm:277  (M4b slice 3)
    public static object heat2colour_b(object temp = null)
    {
        float t = Dm.num(temp);
        t /= 100;
        if (t >= 66)
            return 255;
        else
        {
            if (t <= 16)
                return 0;
            else
                return max(0, min(255, 138.5177312231f * log(t - 10) - 305.0447927307f));
        }
    }
}

public static partial class GlobalProcs
{
    /// For finding out what body parts a body zone covers, the inverse of the below basically
    // tg: code/__HELPERS/type2type.dm:180
    public static object body_zone2cover_flags(object def_zone = null)
    {
        switch (def_zone)
        {
            case BODY_ZONE_CHEST:
                return CHEST | GROIN;
            case BODY_ZONE_PRECISE_GROIN:
                return GROIN;
            case BODY_ZONE_HEAD:
                return HEAD;
            case BODY_ZONE_L_ARM:
                return ARM_LEFT | HAND_LEFT;
            case BODY_ZONE_R_ARM:
                return ARM_RIGHT | HAND_RIGHT;
            case BODY_ZONE_L_LEG:
                return LEG_LEFT | FOOT_LEFT;
            case BODY_ZONE_R_LEG:
                return LEG_RIGHT | FOOT_RIGHT;
        }
        return null;
    }

    //Turns a Body_parts_covered bitfield into a list of organ/limb names.
    //(I challenge you to find a use for this) -I found a use for it!! | So did I!.
    // tg: code/__HELPERS/type2type.dm:199
    public static object cover_flags2body_zones(object bpc = null)
    {
        DmList covered_parts = Dm.list();

        if (!Dm.truthy(bpc))
            return covered_parts;

        if (Dm.truthy(Dm.bitand(bpc, HEAD)))
            Dm.combine(ref covered_parts, Dm.list(BODY_ZONE_HEAD, BODY_ZONE_PRECISE_EYES, BODY_ZONE_PRECISE_MOUTH));

        if (Dm.truthy(Dm.bitand(bpc, CHEST)))
            Dm.combine(ref covered_parts, Dm.list(BODY_ZONE_CHEST, BODY_ZONE_PRECISE_GROIN));
        else if (Dm.truthy(Dm.bitand(bpc, GROIN)))
            Dm.combine(ref covered_parts, BODY_ZONE_PRECISE_GROIN);

        if (Dm.truthy(Dm.bitand(bpc, HAND_LEFT)))
            Dm.combine(ref covered_parts, Dm.list(BODY_ZONE_L_ARM, BODY_ZONE_PRECISE_L_HAND));
        else if (Dm.truthy(Dm.bitand(bpc, ARM_LEFT)))
            Dm.combine(ref covered_parts, BODY_ZONE_L_ARM);
        if (Dm.truthy(Dm.bitand(bpc, HAND_RIGHT)))
            Dm.combine(ref covered_parts, Dm.list(BODY_ZONE_R_ARM, BODY_ZONE_PRECISE_R_HAND));
        else if (Dm.truthy(Dm.bitand(bpc, ARM_RIGHT)))
            Dm.combine(ref covered_parts, BODY_ZONE_R_ARM);

        if (Dm.truthy(Dm.bitand(bpc, FOOT_LEFT)))
            Dm.combine(ref covered_parts, Dm.list(BODY_ZONE_L_LEG, BODY_ZONE_PRECISE_L_FOOT));
        else if (Dm.truthy(Dm.bitand(bpc, LEG_LEFT)))
            Dm.combine(ref covered_parts, BODY_ZONE_L_LEG);
        if (Dm.truthy(Dm.bitand(bpc, FOOT_RIGHT)))
            Dm.combine(ref covered_parts, Dm.list(BODY_ZONE_R_LEG, BODY_ZONE_PRECISE_R_FOOT));
        else if (Dm.truthy(Dm.bitand(bpc, LEG_RIGHT)))
            Dm.combine(ref covered_parts, BODY_ZONE_R_LEG);

        return covered_parts;
    }

    // tg: code/__HELPERS/type2type.dm:233  (gaps VI1: the infective component's worn item)
    public static object slot2body_zone(object slot = null)
    {
        float s = Dm.num(slot);
        if (s == ITEM_SLOT_BACK || s == ITEM_SLOT_OCLOTHING || s == ITEM_SLOT_ICLOTHING || s == ITEM_SLOT_BELT || s == ITEM_SLOT_ID)
            return BODY_ZONE_CHEST;

        else if (s == ITEM_SLOT_GLOVES || s == ITEM_SLOT_HANDS || s == ITEM_SLOT_HANDCUFFED)
            return pick(BODY_ZONE_PRECISE_L_HAND, BODY_ZONE_PRECISE_R_HAND);

        else if (s == ITEM_SLOT_HEAD || s == ITEM_SLOT_NECK || s == ITEM_SLOT_EARS)
            return BODY_ZONE_HEAD;

        else if (s == ITEM_SLOT_MASK)
            return BODY_ZONE_PRECISE_MOUTH;

        else if (s == ITEM_SLOT_EYES)
            return BODY_ZONE_PRECISE_EYES;

        else if (s == ITEM_SLOT_FEET)
            return pick(BODY_ZONE_PRECISE_R_FOOT, BODY_ZONE_PRECISE_L_FOOT);

        else if (s == ITEM_SLOT_LEGCUFFED)
            return pick(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG);
        return null;
    }

    //will drop all values not on the diagonal
    // tg: code/__HELPERS/type2type.dm:349
    public static object color_matrix2color_hex(DmList the_matrix = null)
    {
        if (!islist(the_matrix) || the_matrix.len != 20)
            return "#ffffffff";
        return rgb(Dm.num(the_matrix[1]) * 255, Dm.num(the_matrix[6]) * 255, Dm.num(the_matrix[11]) * 255, Dm.num(the_matrix[16]) * 255);
    }
}

public static partial class GlobalProcs
{
    //Converts a direction into text
    // tg: code/__HELPERS/type2type.dm:20
    public static object dir2text(object direction = null)
    {
        object sw__1 = direction;
        if (Dm.eq(sw__1, NORTH))
            return "north";
        else if (Dm.eq(sw__1, SOUTH))
            return "south";
        else if (Dm.eq(sw__1, EAST))
            return "east";
        else if (Dm.eq(sw__1, WEST))
            return "west";
        else if (Dm.eq(sw__1, NORTHEAST))
            return "northeast";
        else if (Dm.eq(sw__1, SOUTHEAST))
            return "southeast";
        else if (Dm.eq(sw__1, NORTHWEST))
            return "northwest";
        else if (Dm.eq(sw__1, SOUTHWEST))
            return "southwest";

        return NONE;
    }

    //Turns text into proper directions
    // tg: code/__HELPERS/type2type.dm:42
    public static object text2dir(object direction = null)
    {
        switch (uppertext(Dm.str(direction)))
        {
            case "NORTH":
                return NORTH;
            case "SOUTH":
                return SOUTH;
            case "EAST":
                return EAST;
            case "WEST":
                return WEST;
            case "NORTHEAST":
                return NORTHEAST;
            case "NORTHWEST":
                return NORTHWEST;
            case "SOUTHEAST":
                return SOUTHEAST;
            case "SOUTHWEST":
                return SOUTHWEST;
        }

        return NONE;
    }

    //Converts a blend_mode constant to one acceptable to icon.Blend()
    // tg: code/__HELPERS/type2type.dm:131  (syndicate A3: the antagonists' preference pictures)
    public static object blendMode2iconMode(object blend_mode = null)
    {
        if (Dm.eq(blend_mode, BLEND_MULTIPLY))
            return ICON_MULTIPLY;
        if (Dm.eq(blend_mode, BLEND_ADD))
            return ICON_ADD;
        if (Dm.eq(blend_mode, BLEND_SUBTRACT))
            return ICON_SUBTRACT;
        return ICON_OVERLAY;
    }
}
