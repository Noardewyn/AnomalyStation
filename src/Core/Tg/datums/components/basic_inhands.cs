// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/basic_inhands.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Basic handling for showing held items in a mob's hands
// tg: code/datums/components/basic_inhands.dm:4
public partial class datum_component_basic_inhands : datum_component
{
    // tg: code/datums/components/basic_inhands.dm:15
    [DmParams("display_layer", "y_offset", "x_offset")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        await base.Initialize(__extra);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        this.display_layer = Dm.arg(__extra, 0) ?? 1f;
        this.y_offset = Dm.arg(__extra, 1) ?? 0f;
        this.x_offset = Dm.arg(__extra, 2) ?? 0f;
        cached_overlays = Dm.list();
        return null;
    }

    // tg: code/datums/components/basic_inhands.dm:24
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_updated_overlays));
        RegisterSignal(parent, COMSIG_MOB_UPDATE_HELD_ITEMS, nameof(on_updated_held_items));
    }

    // tg: code/datums/components/basic_inhands.dm:29
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_UPDATE_OVERLAYS, COMSIG_MOB_UPDATE_HELD_ITEMS));
    }

    /// When your overlays update, add your held overlays
    // tg: code/datums/components/basic_inhands.dm:34
    [SignalHandler]
    public virtual void on_updated_overlays(atom parent_atom = null, DmList overlays = null)
    {
        Dm.append(ref overlays, cached_overlays);
    }

    /// When your number of held items changes, regenerate held icons
    // tg: code/datums/components/basic_inhands.dm:39
    [SignalHandler]
    public virtual void on_updated_held_items(mob_living holding_mob = null)
    {
        DmList held_overlays = Dm.list();
        foreach (obj_item held in Dm.each<obj_item>(holding_mob.held_items))
        {
            bool is_right = IS_RIGHT_INDEX(Dm.num(holding_mob.get_held_index_of_item(held)));
            object icon_file = is_right ? held.righthand_file : held.lefthand_file;
            mutable_appearance held_overlay = Waiting.NoSleep(held.build_worn_icon(default_layer: HANDS_LAYER, default_icon_file: icon_file, isinhands: TRUE)) as mutable_appearance;
            held_overlay.pixel_z = Dm.num(held_overlay.pixel_z) + Dm.num(y_offset);
            held_overlay.pixel_w = Dm.num(held_overlay.pixel_w) + Dm.num(x_offset) * (is_right ? 1 : -1);
            Dm.append(ref held_overlays, held_overlay);
        }

        cached_overlays = held_overlays;
        holding_mob.update_appearance(UPDATE_OVERLAYS);
    }
}
