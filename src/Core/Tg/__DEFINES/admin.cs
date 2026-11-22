// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/admin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M6 slice 1: a misplaced blood trail's mapping log names its place): the readable coordinates of an atom
// (COORD, AREACOORD, Admin_Coordinates_Readable, Safe_COORD_Location); the admin links ADMIN_QUE, ADMIN_FLW, ADMIN_LOOKUP,
// ADMIN_LOOKUPFLW, ADMIN_JMP, ADMIN_COORDJMP and ADMIN_VERBOSEJMP (silicons SI1, D326: the law rack's admin messages). The
// other admin defines are numbers and links of the admin panel, generated or not yet used.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/admin.dm:66  #define COORD(src) "[src ? src.Admin_Coordinates_Readable() : "nonexistent location"]"
    public static string COORD(atom src) => Dm.text("[]", src != null ? src.Admin_Coordinates_Readable() : "nonexistent location");

    // tg: code/__DEFINES/admin.dm:67  #define AREACOORD(src) "[src ? src.Admin_Coordinates_Readable(TRUE) : "nonexistent location"]"
    public static string AREACOORD(atom src) => Dm.text("[]", src != null ? src.Admin_Coordinates_Readable(TRUE) : "nonexistent location");

    // tg: code/__DEFINES/admin.dm:48
    public static string ADMIN_QUE(object user) => Dm.text("(<a href='byond://?_src_=holder;[];adminmoreinfo=[]'>?</a>)", HrefToken(forceGlobal: TRUE), AdminLinkRef(user));

    // BYOND gives text a reference too, and tg passes a key (a mob's fingerprintslast) to the admin link macros; the port's REF knows
    // no text, so a key's link carries the key itself (the admins' panels read no such link yet, D275)
    private static object AdminLinkRef(object user) => user is string key ? key : REF(user);

    // tg: code/__DEFINES/admin.dm:49
    public static string ADMIN_FLW(object user) => Dm.text("(<a href='byond://?_src_=holder;[];adminplayerobservefollow=[]'>FLW</a>)", HrefToken(forceGlobal: TRUE), AdminLinkRef(user));

    // tg: code/__DEFINES/admin.dm:57  (gaps A1)
    public static string ADMIN_SYNDICATE_REPLY(object user) => Dm.text("(<a href='byond://?_src_=holder;[];SyndicateReply=[]'>RPLY</a>)", HrefToken(forceGlobal: TRUE), AdminLinkRef(user));

    // tg: code/__DEFINES/admin.dm:59  (gaps A1)
    public static string ADMIN_SMITE(object user) => Dm.text("(<a href='byond://?_src_=holder;[];adminsmite=[]'>SMITE</a>)", HrefToken(forceGlobal: TRUE), AdminLinkRef(user));

    // tg: code/__DEFINES/admin.dm:51  (gaps A1b)
    public static string ADMIN_VV(object atom) => Dm.text("(<a href='byond://?_src_=vars;[];Vars=[]'>VV</a>)", HrefToken(forceGlobal: TRUE), REF(atom));

    // tg: code/__DEFINES/admin.dm:60
    public static string ADMIN_LOOKUP(object user) => Dm.text("[][]", key_name_admin(user), ADMIN_QUE(user));

    // tg: code/__DEFINES/admin.dm:61
    public static string ADMIN_LOOKUPFLW(object user) => Dm.text("[][] []", key_name_admin(user), ADMIN_QUE(user), ADMIN_FLW(user));

    // tg: code/__DEFINES/admin.dm:65
    public static string ADMIN_JMP(atom src) => Dm.text("(<a href='byond://?_src_=holder;[];adminplayerobservecoodjump=1;X=[];Y=[];Z=[]'>JMP</a>)", HrefToken(forceGlobal: TRUE), src.x, src.y, src.z);

    // tg: code/__DEFINES/admin.dm:68
    public static string ADMIN_COORDJMP(atom src) => Dm.text("[]", src != null ? src.Admin_Coordinates_Readable(FALSE, TRUE) : "nonexistent location");

    // tg: code/__DEFINES/admin.dm:69
    public static string ADMIN_VERBOSEJMP(atom src) => Dm.text("[]", src != null ? src.Admin_Coordinates_Readable(TRUE, TRUE) : "nonexistent location");
}

// tg: code/__DEFINES/admin.dm:83 (interleaved types, rules 1.9)
public partial class atom
{
    // tg: code/__DEFINES/admin.dm:83
    public virtual object Admin_Coordinates_Readable(object area_name = null, object admin_jump_ref = null)
    {
        turf turf_at_coords = Safe_COORD_Location() as turf;
        if (turf_at_coords == null)
            return "nonexistent location";
        return Dm.text("[]([],[],[])[]", Dm.truthy(area_name) ? Dm.text("[] ", get_area_name(turf_at_coords, TRUE)) : "", turf_at_coords.x, turf_at_coords.y, turf_at_coords.z, Dm.truthy(admin_jump_ref) ? Dm.text(" []", ADMIN_JMP(turf_at_coords)) : "");
    }

    // tg: code/__DEFINES/admin.dm:87
    public virtual object Safe_COORD_Location()
    {
        atom drop_atom = drop_location() as atom;
        if (drop_atom == null)
            return null; //not a valid atom.
        turf drop_turf = get_step(drop_atom, 0); //resolve where the thing is.
        if (drop_turf == null) //incase it's inside a valid drop container, inside another container. ie if a mech picked up a closet and has it inside its internal storage.
        {
            atom last_try = drop_atom.loc?.drop_location() as atom; //one last try, otherwise fuck it.
            if (last_try != null)
                drop_turf = get_step(last_try, 0);
        }
        return drop_turf;
    }
}

// tg: code/__DEFINES/admin.dm:98
public partial class turf : atom
{
    // tg: code/__DEFINES/admin.dm:98
    public override object Safe_COORD_Location()
    {
        return this;
    }
}
