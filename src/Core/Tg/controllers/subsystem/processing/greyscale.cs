// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/greyscale.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: an inert stub subsystem (lead decision W3 G2) with GetColoredIconByType and ParseColorString translated (M2 P3,
// the jumpsuit's worn icon, scope 2j A.2): tg's subsystem builds every GAGS icon on the server from its JSON configs
// (Initialize, the configurations' Generate, rust-g's iconforge); the port composes GAGS icons on the client (D104), so the
// server only names the icon (DEVIATION(DV19) below). The rest of the file (Initialize, RefreshConfigsFromFile,
// GetColoredIconByTypeUniversalIcon, the iconforge jobs) is not translated.
// STUB(M4a): translated with clothing and the GAGS configs (M4a).

// tg: code/controllers/subsystem/processing/greyscale.dm:1 PROCESSING_SUBSYSTEM_DEF(greyscale)
[DmPath("/datum/controller/subsystem/processing/greyscale")]
public partial class datum_controller_subsystem_processing_greyscale : datum_controller_subsystem_processing
{
    // tg: code/controllers/subsystem/processing/greyscale.dm:2-5
    public datum_controller_subsystem_processing_greyscale()
    {
        name = "Greyscale";
        // STUB(M4a): SS_NO_INIT | SS_NO_FIRE instead of tg's SS_BACKGROUND (wait 3 SECONDS): nothing loads the configs
        // (Initialize, :13) on the server, which never renders a GAGS icon (D104).
        ss_flags = Dm.bitor(SS_NO_INIT, SS_NO_FIRE);
        wait = 3 * SECONDS;
        init_stage = INITSTAGE_EARLY;
    }

    // tg: code/controllers/subsystem/processing/greyscale.dm:6
    public DmList configurations;
    // tg: code/controllers/subsystem/processing/greyscale.dm:7
    public DmList layer_types;
    // tg: #ifdef USE_RUSTG_ICONFORGE_GAGS ... #endif (:8-11): gags_cache not translated (rust-g, rules 13)

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // tg: code/controllers/subsystem/processing/greyscale.dm:6
        configurations = Dm.list();
        // tg: code/controllers/subsystem/processing/greyscale.dm:7
        layer_types = Dm.list();
    }

    // The New() that PROCESSING_SUBSYSTEM_DEF(greyscale) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSgreyscale)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/greyscale.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSgreyscale != this)
        {
            if (SSgreyscale is datum_controller_subsystem_processing_greyscale)
            {
                Recover();
                qdel(SSgreyscale);
            }
            SSgreyscale = this;
        }
        PreInit();
    }

    /// <summary>
    /// The GAGS icon of a configuration and its colours. DEVIATION(DV19): "GAGS icons are composed on the client" (D104):
    /// instead of the icon tg's configuration generates (<c>configurations[type].Generate(colors)</c>, :85), this returns
    /// the text <c>gags:&lt;config type path&gt;|&lt;colours&gt;</c> (the contracts' form, src/Protocol/MessagesV2.cs
    /// AppearanceEntry.Icon), with tg's colour string as it is after the list join (<c>"#3f3f3f"</c>, or one
    /// <c>#rrggbb</c> per colour group, <c>"#ffffff#000000"</c>); the replicator passes it through as the icon text and the
    /// client composes it from the configuration's JSON and icon file.
    /// </summary>
    // tg: code/controllers/subsystem/processing/greyscale.dm:60
    public virtual object GetColoredIconByType(object type = null, object colors = null)
    {
        if (!ispath(type, typeof(datum_greyscale_config)))
            throw Dm.CRASH(Dm.text("An invalid greyscale configuration was given to `GetColoredIconByType()`: []", type), "code/controllers/subsystem/processing/greyscale.dm", 62);
        if (!Dm.truthy(initialized))
            throw Dm.CRASH("GetColoredIconByType() called before greyscale subsystem initialized!", "code/controllers/subsystem/processing/greyscale.dm", 64);
        type = Dm.text("[]", type);
        if (colors is DmList) // It's the color list format
            colors = ((DmList)colors).Join(null);
        else if (!istext(colors))
            throw Dm.CRASH(Dm.text("Invalid colors were given to `GetColoredIconByType()`: []", colors), "code/controllers/subsystem/processing/greyscale.dm", 69);
        // tg: #ifdef USE_RUSTG_ICONFORGE_GAGS ... #else (:70-84): the active branch is the #else (rules 1.7)
        // DEVIATION(DV19): return configurations[type].Generate(colors) (:85) becomes the client-composed GAGS name
        return Dm.text("gags:[]|[]", type, colors);
    }

    // tg: code/controllers/subsystem/processing/greyscale.dm:98
    public virtual object ParseColorString(object color_string = null)
    {
        DmList dot = null;
        dot = Dm.list();
        DmList split_colors = splittext(color_string, "#") as DmList;
        for (float color = 2, color__end = length(split_colors); color <= color__end; color++)
            Dm.append(ref dot, Dm.text("#[]", split_colors[color]));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/greyscale.dm:1  PROCESSING_SUBSYSTEM_DEF(greyscale): GLOBAL_REAL(SSgreyscale, ...)
    internal datum_controller_subsystem_processing_greyscale SSgreyscale;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_greyscale SSgreyscale => ref TgState.Current.SSgreyscale;
}
