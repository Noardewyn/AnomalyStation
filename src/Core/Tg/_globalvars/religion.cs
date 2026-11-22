// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/religion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10x slice A): the deity and the prayer types' colours, crosses and boxes that a prayer reads (pray.cs); gaps J2 (D298): the
// chaplain's religion, bible and holy weapon. The sect's procs (set_new_religious_sect, reset_religious_sect, :52-82) wait for the
// sects (not-translated.md 106).

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
