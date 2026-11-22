// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_greyscale.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the vars greyscale_config and greyscale_colors (wave 3, the atoms package), update_greyscale (M2 F1: the
// jumpsuit's GAGS icon at Initialize) and set_greyscale (M5 slice 2: a material's greyscale colours, atom_materials.dm:266).

// tg: code/game/atom/atom_greyscale.dm:1
public partial class atom : datum
{
    ///The config type to use for greyscaled sprites. Both this and greyscale_colors must be assigned to work.
    // tg: code/game/atom/atom_greyscale.dm:3
    public object greyscale_config;
    ///A string of hex format colors to be used by greyscale sprites, ex: "#0054aa#badcff"
    // tg: code/game/atom/atom_greyscale.dm:5
    public object greyscale_colors;

    /// Handles updates to greyscale value updates.
    /// The colors argument can be either a list or the full color string.
    /// Child procs should call parent last so the update happens after all changes.
    // tg: code/game/atom/atom_greyscale.dm:10
    // M5 slice 2 (Appendix A): colors is object, not the declared list: tg passes a list or the joined colour string
    // (atom_materials.dm:416 passes initial(greyscale_colors)).
    [ShouldCallParent]
    public virtual async DmTask set_greyscale(object colors = null, object new_config = null, params object[] __extra)
    {
        if (colors is DmList colors__list)
            colors = colors__list.Join("");
        if (colors != null && !Dm.eq(greyscale_colors, colors)) // If you want to disable greyscale stuff then give a blank string
            greyscale_colors = colors;

        if (new_config != null && !Dm.eq(greyscale_config, new_config))
            greyscale_config = new_config;

        await update_greyscale();
    }

    /// Checks if this atom uses the GAGS system and if so updates the icon
    // tg: code/game/atom/atom_greyscale.dm:23
    [ShouldCallParent]
    public virtual async DmTask update_greyscale()
    {
        if (Dm.truthy(greyscale_colors) && Dm.truthy(greyscale_config))
            icon = SSgreyscale.GetColoredIconByType(greyscale_config, greyscale_colors);
        if (!Dm.truthy(smoothing_flags)) // This is a bitfield but we're just checking that some sort of smoothing is happening
            return;
        // M3 P4: atom colours (atom_color.dm) and icon smoothing are translated.
        await update_atom_colour();
        QUEUE_SMOOTH(this);
    }
}
