// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/religion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the deity and the prayer types' colours, crosses and boxes that a prayer reads (pray.cs), the chaplain's religion,
// bible and holy weapon, and the sect's selection and reset (the chapel).

// tg: code/_globalvars/religion.dm:1
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/_globalvars/religion.dm:2  GLOBAL_VAR(religion)
    public object religion;

    // tg: code/_globalvars/religion.dm:2  (GLOBAL_UNMANAGED(religion), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalreligion()
    {
        return;
    }

    // tg: code/_globalvars/religion.dm:4  GLOBAL_DATUM(religious_sect, /datum/religion_sect)
    public datum_religion_sect religious_sect;

    // tg: code/_globalvars/religion.dm:4  (GLOBAL_UNMANAGED(religious_sect), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalreligious_sect()
    {
        return;
    }

    // tg: code/_globalvars/religion.dm:7  GLOBAL_VAR(bible_name)
    public object bible_name;

    // tg: code/_globalvars/religion.dm:7  (GLOBAL_UNMANAGED(bible_name), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalbible_name()
    {
        return;
    }

    // tg: code/_globalvars/religion.dm:8  GLOBAL_VAR(bible_icon_state)
    public object bible_icon_state;

    // tg: code/_globalvars/religion.dm:8  (GLOBAL_UNMANAGED(bible_icon_state), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalbible_icon_state()
    {
        return;
    }

    // tg: code/_globalvars/religion.dm:9  GLOBAL_VAR(bible_inhand_icon_state)
    public object bible_inhand_icon_state;

    // tg: code/_globalvars/religion.dm:9  (GLOBAL_UNMANAGED(bible_inhand_icon_state), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalbible_inhand_icon_state()
    {
        return;
    }

    // tg: code/_globalvars/religion.dm:12  GLOBAL_LIST_EMPTY(chaplain_altars)
    public DmList chaplain_altars;

    // tg: code/_globalvars/religion.dm:12  (GLOBAL_MANAGED(chaplain_altars, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalchaplain_altars()
    {
        chaplain_altars = Dm.list();
        Dm.append(ref gvars_datum_init_order, "chaplain_altars");
    }

    // tg: code/_globalvars/religion.dm:15  GLOBAL_VAR(holy_weapon_type)
    public Type holy_weapon_type;

    // tg: code/_globalvars/religion.dm:15  (GLOBAL_UNMANAGED(holy_weapon_type), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalholy_weapon_type()
    {
        return;
    }

    // tg: code/_globalvars/religion.dm:16  GLOBAL_VAR(holy_armor_type) (gaps OD1: the chaplain's armaments beacon)
    public Type holy_armor_type;

    // tg: code/_globalvars/religion.dm:16  (GLOBAL_UNMANAGED(holy_armor_type), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalholy_armor_type()
    {
        return;
    }

    // tg: code/_globalvars/religion.dm:3  GLOBAL_VAR(deity)
    public object deity;

    // tg: code/_globalvars/religion.dm:3  (GLOBAL_UNMANAGED(deity), __DEFINES/__globals.dm:11)
    public virtual void InitGlobaldeity()
    {
        return;
    }

    // tg: code/_globalvars/religion.dm:18  GLOBAL_LIST_INIT(prayer_type_to_font_color, list(...))
    public DmList prayer_type_to_font_color;

    // tg: code/_globalvars/religion.dm:18  (GLOBAL_MANAGED(prayer_type_to_font_color, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalprayer_type_to_font_color()
    {
        prayer_type_to_font_color = Dm.list(
            Dm.kv(DEFAULT_PRAYER, "purple"),
            Dm.kv(CULT_PRAYER, "black"),
            Dm.kv(HERETIC_PRAYER, "green"),
            Dm.kv(CHAPLAIN_PRAYER, "yellow"),
            Dm.kv(SPIRITUAL_PRAYER, "blue"),
            Dm.kv(EVIL_PRAYER, "red"),
            Dm.kv(SANTA_PRAYER, "purple"),
            Dm.kv(SANTA_NAUGHTY_PRAYER, "red"));
    }

    // tg: code/_globalvars/religion.dm:29  GLOBAL_LIST_INIT(prayer_type_to_icon_state, list(...))
    public DmList prayer_type_to_icon_state;

    // tg: code/_globalvars/religion.dm:29  (GLOBAL_MANAGED(prayer_type_to_icon_state, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalprayer_type_to_icon_state()
    {
        prayer_type_to_icon_state = Dm.list(
            Dm.kv(DEFAULT_PRAYER, "bible"),
            Dm.kv(CULT_PRAYER, "tome"),
            Dm.kv(HERETIC_PRAYER, "necronomicon"),
            Dm.kv(CHAPLAIN_PRAYER, "kingyellow"),
            Dm.kv(SPIRITUAL_PRAYER, "holylight"),
            Dm.kv(EVIL_PRAYER, "burning"),
            Dm.kv(SANTA_PRAYER, "bible"), //here just in case, we use present boxes for the icon
            Dm.kv(SANTA_NAUGHTY_PRAYER, "burning"));
    }

    // tg: code/_globalvars/religion.dm:40  GLOBAL_LIST_INIT(prayer_type_to_message_box, list(...))
    public DmList prayer_type_to_message_box;

    // tg: code/_globalvars/religion.dm:40  (GLOBAL_MANAGED(prayer_type_to_message_box, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalprayer_type_to_message_box()
    {
        prayer_type_to_message_box = Dm.list(
            Dm.kv(DEFAULT_PRAYER, ""),
            Dm.kv(CULT_PRAYER, "red_box"),
            Dm.kv(HERETIC_PRAYER, "green_box"),
            Dm.kv(CHAPLAIN_PRAYER, "blue_box"),
            Dm.kv(SPIRITUAL_PRAYER, ""),
            Dm.kv(EVIL_PRAYER, "red_box"),
            Dm.kv(SANTA_PRAYER, "blue_box"),
            Dm.kv(SANTA_NAUGHTY_PRAYER, "red_box"));
    }
}

public static partial class GlobalProcs
{
    /// Sets a new religious sect used by all chaplains int he round
    // tg: code/_globalvars/religion.dm:52
    public static async DmTask set_new_religious_sect(object path = null, float? reset_existing = null)
    {
        reset_existing ??= FALSE;
        if (!ispath(path, typeof(datum_religion_sect)))
        {
            message_admins(Dm.text("[] has tried to spawn an item when selecting a sect.", ADMIN_LOOKUPFLW(usr)));
            return;
        }

        if (GLOB.religious_sect != null)
        {
            if (!Dm.truthy(reset_existing))
                return;
            reset_religious_sect();
        }

        GLOB.religious_sect = await Dm.new_async((Type)path) as datum_religion_sect;
        foreach (object i in Dm.each(GLOB.player_list))
        {
            if (!isliving(i))
                continue;
            mob_living am_i_holy_living = i as mob_living;
            if (!Dm.truthy(am_i_holy_living.mind?.holy_role))
                continue;
            await GLOB.religious_sect.on_conversion(am_i_holy_living);
        }
        // SEND_GLOBAL_SIGNAL(COMSIG_RELIGIOUS_SECT_CHANGED) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_RELIGIOUS_SECT_CHANGED]))
            SSdcs._SendSignal(COMSIG_RELIGIOUS_SECT_CHANGED, Dm.list(SSdcs));
    }

    /// Removes any existing religious sect from chaplains, allowing another to be selected
    // tg: code/_globalvars/religion.dm:73
    public static void reset_religious_sect()
    {
        foreach (object i in Dm.each(GLOB.player_list))
        {
            if (!isliving(i))
                continue;
            mob_living am_i_holy_living = i as mob_living;
            if (!Dm.truthy(am_i_holy_living.mind?.holy_role))
                continue;
            GLOB.religious_sect.on_deconversion(am_i_holy_living);
        }

        GLOB.religious_sect = null;
        // SEND_GLOBAL_SIGNAL(COMSIG_RELIGIOUS_SECT_RESET) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_RELIGIOUS_SECT_RESET]))
            SSdcs._SendSignal(COMSIG_RELIGIOUS_SECT_RESET, Dm.list(SSdcs));
    }
}
