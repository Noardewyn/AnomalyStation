// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/clickbox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * This component adds a near-invisible underlay to a movable target to expand the clickable surface of its icon without
 * resorting to MOUSE_OPACITY_OPAQUE.
 * The underlay ignores the parent's color and alpha and can be offset.
 */
// tg: code/datums/components/clickbox.dm:10
public partial class datum_component_clickbox : datum_component
{
    // tg: code/datums/components/clickbox.dm:1  #define CLICKBOX_LAYER FLOAT_LAYER-999 (file-local, #undef at :102)
    private const float CLICKBOX_LAYER = FLOAT_LAYER - 999;

    // tg: code/datums/components/clickbox.dm:26
    [DmParams("icon_state", "x_offset", "y_offset", "max_scale", "min_scale", "dead_state")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object icon_state = Dm.arg(__extra, 0);
        object x_offset = Dm.arg(__extra, 1);
        object y_offset = Dm.arg(__extra, 2);
        object max_scale = Dm.arg(__extra, 3);
        object min_scale = Dm.arg(__extra, 4);
        object dead_state = Dm.arg(__extra, 5);
        icon_state ??= "sphere";
        x_offset ??= 0;
        y_offset ??= 0;
        max_scale ??= 2;
        min_scale ??= 0.5f;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        this.icon_state = icon_state as string;
        this.x_offset = Dm.num(x_offset);
        this.y_offset = Dm.num(y_offset);
        this.max_scale = Dm.num(max_scale);
        this.min_scale = Dm.num(min_scale);

        RegisterSignal(parent, COMSIG_ATOM_VV_MODIFY_TRANSFORM, nameof(on_vv_modify_transform));

        object clickbox_icon_state = icon_state;
        if (isliving(parent))
        {
            mob_living living_parent = parent as mob_living;
            RegisterSignal(living_parent, COMSIG_LIVING_POST_UPDATE_TRANSFORM, nameof(on_update_transform));
            if (Dm.truthy(dead_state))
            {
                this.dead_state = dead_state;
                RegisterSignal(living_parent, COMSIG_LIVING_DEATH, nameof(on_death));
                RegisterSignal(living_parent, COMSIG_LIVING_REVIVE, nameof(on_revive));
                if (living_parent.stat == DEAD)
                    clickbox_icon_state = dead_state;
            }
        }

        update_underlay(clickbox_icon_state);
        return null;
    }

    // tg: code/datums/components/clickbox.dm:51
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        atom_movable mov_parent = parent as atom_movable;
        UnregisterSignal(mov_parent, Dm.list(COMSIG_ATOM_VV_MODIFY_TRANSFORM, COMSIG_LIVING_POST_UPDATE_TRANSFORM, COMSIG_LIVING_DEATH, COMSIG_LIVING_REVIVE));
        Dm.remove(mov_parent.underlays, clickbox_underlay);
    }

    /// Removes the old underlay and adds a new one. The underlay is scaled up/down if necessary
    // tg: code/datums/components/clickbox.dm:57
    public virtual void update_underlay(object clickbox_icon_state = null, object width = null, object height = null)
    {
        atom_movable mov_parent = parent as atom_movable;
        if (!Dm.truthy(clickbox_icon_state))
            clickbox_icon_state = Dm.truthy(clickbox_underlay?.icon_state) ? clickbox_underlay.icon_state : icon_state;
        Dm.remove(mov_parent.underlays, clickbox_underlay); // Remove the previous underlay.

        float clickbox_width = 1;
        float abs_width = Dm.abs(Dm.num(width)); //Taking into account inverted transform values.
        if (abs_width > max_scale)
            clickbox_width = max_scale / Dm.num(width);
        else if (abs_width != 0 && abs_width < min_scale)
            clickbox_width = min_scale / Dm.num(width);

        float clickbox_height = 1;
        float abs_height = Dm.abs(Dm.num(height)); //Ditto
        if (abs_height > max_scale)
            clickbox_height = max_scale / Dm.num(height);
        else if (abs_height != 0 && abs_height < min_scale)
            clickbox_height = min_scale / Dm.num(height);

        clickbox_underlay = mutable_appearance(Dm.resource("icons/ui/clickbox.dmi"), clickbox_icon_state as string, CLICKBOX_LAYER, alpha: 1, appearance_flags: Dm.bitor(RESET_COLOR, RESET_ALPHA)) as mutable_appearance;
        clickbox_underlay.transform = Dm.matrix(clickbox_underlay.transform).Scale(clickbox_width, clickbox_height);
        //Keeps the underlay more or less centered.
        clickbox_underlay.pixel_w = x_offset * 1 / clickbox_width;
        clickbox_underlay.pixel_z = y_offset * 1 / clickbox_height;
        Dm.append(mov_parent.underlays, clickbox_underlay);
    }

    // tg: code/datums/components/clickbox.dm:84
    [SignalHandler]
    public virtual void on_vv_modify_transform(atom source = null)
    {
        matrix source_transform = Dm.matrix(source.transform);
        float width = source_transform.a;
        float height = source_transform.e;
        update_underlay(clickbox_underlay.icon_state, width, height);
    }

    // tg: code/datums/components/clickbox.dm:90
    [SignalHandler]
    public virtual void on_update_transform(mob_living source = null, object previous_size = null)
    {
        update_underlay(clickbox_underlay.icon_state, source.current_size, source.current_size);
    }

    // tg: code/datums/components/clickbox.dm:94
    [SignalHandler]
    public virtual void on_death(mob_living source = null)
    {
        update_underlay(dead_state, source.current_size, source.current_size);
    }

    // tg: code/datums/components/clickbox.dm:98
    [SignalHandler]
    public virtual void on_revive(mob_living source = null)
    {
        update_underlay(icon_state, source.current_size, source.current_size);
    }
}
