// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/modular_computer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file's global lists (M9 slice E: the PDA themes); its defines are generated (D111).

public partial class datum_controller_global_vars
{
    ///List of PDA themes that are accessible to everyone by default.
    // tg: code/__DEFINES/modular_computer.dm:144  GLOBAL_LIST_INIT(default_pda_themes, list(...))
    public DmList default_pda_themes;

    // tg: code/__DEFINES/modular_computer.dm:144  (GLOBAL_MANAGED(default_pda_themes, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldefault_pda_themes()
    {
        default_pda_themes = Dm.list(
            Dm.kv(PDA_THEME_NTOS_NAME, PDA_THEME_NTOS),
            Dm.kv(PDA_THEME_DARK_MODE_NAME, PDA_THEME_DARK_MODE),
            Dm.kv(PDA_THEME_RETRO_NAME, PDA_THEME_RETRO),
            Dm.kv(PDA_THEME_SYNTH_NAME, PDA_THEME_SYNTH),
            Dm.kv(PDA_THEME_TERMINAL_NAME, PDA_THEME_TERMINAL),
            Dm.kv(PDA_THEME_BIRD_NAME, PDA_THEME_BIRD));
        Dm.append(ref gvars_datum_init_order, "default_pda_themes");
    }

    ///List of PDA themes that are accessible to everyone by default.
    // tg: code/__DEFINES/modular_computer.dm:154  GLOBAL_LIST_INIT(pda_name_to_theme, list(...))
    public DmList pda_name_to_theme;

    // tg: code/__DEFINES/modular_computer.dm:154  (GLOBAL_MANAGED(pda_name_to_theme, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpda_name_to_theme()
    {
        pda_name_to_theme = Dm.list(
            Dm.kv(PDA_THEME_NTOS_NAME, PDA_THEME_NTOS),
            Dm.kv(PDA_THEME_DARK_MODE_NAME, PDA_THEME_DARK_MODE),
            Dm.kv(PDA_THEME_RETRO_NAME, PDA_THEME_RETRO),
            Dm.kv(PDA_THEME_SYNTH_NAME, PDA_THEME_SYNTH),
            Dm.kv(PDA_THEME_TERMINAL_NAME, PDA_THEME_TERMINAL),
            Dm.kv(PDA_THEME_SYNDICATE_NAME, PDA_THEME_SYNDICATE),
            Dm.kv(PDA_THEME_CAT_NAME, PDA_THEME_CAT),
            Dm.kv(PDA_THEME_LIGHT_MODE_NAME, PDA_THEME_LIGHT_MODE),
            Dm.kv(PDA_THEME_SPOOKY_NAME, PDA_THEME_SPOOKY),
            Dm.kv(PDA_THEME_HACKERMAN_NAME, PDA_THEME_HACKERMAN),
            Dm.kv(PDA_THEME_ROULETTE_NAME, PDA_THEME_ROULETTE),
            Dm.kv(PDA_THEME_ABDUCTOR_NAME, PDA_THEME_ABDUCTOR),
            Dm.kv(PDA_THEME_BIRD_NAME, PDA_THEME_BIRD));
        Dm.append(ref gvars_datum_init_order, "pda_name_to_theme");
    }

    // tg: code/__DEFINES/modular_computer.dm:171  GLOBAL_LIST_INIT(pda_id_to_name, list(...))
    public DmList pda_id_to_name;

    // tg: code/__DEFINES/modular_computer.dm:171  (GLOBAL_MANAGED(pda_id_to_name, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpda_id_to_name()
    {
        pda_id_to_name = Dm.list(
            Dm.kv(PDA_THEME_ID_CAT, PDA_THEME_CAT_NAME),
            Dm.kv(PDA_THEME_ID_LIGHT_MODE, PDA_THEME_LIGHT_MODE_NAME),
            Dm.kv(PDA_THEME_ID_SPOOKY, PDA_THEME_SPOOKY_NAME),
            Dm.kv(PDA_THEME_ID_HACKERMAN, PDA_THEME_HACKERMAN_NAME),
            Dm.kv(PDA_THEME_ID_ROULETTE, PDA_THEME_ROULETTE_NAME),
            Dm.kv(PDA_THEME_ABDUCTOR, PDA_THEME_ABDUCTOR_NAME));
        Dm.append(ref gvars_datum_init_order, "pda_id_to_name");
    }
}
