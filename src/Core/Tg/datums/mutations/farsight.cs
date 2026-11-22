// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/farsight.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): Farsight and its view toggle; the client's view size is a recorded leaf (not-translated section 165). The var blocks are generated (D111).

// tg: code/datums/mutations/farsight.dm:1
public partial class datum_mutation_farsight : datum_mutation
{
    // tg: code/datums/mutations/farsight.dm:12
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        if (dot is datum_action_cooldown_spell_farsight to_modify)
            to_modify.set_sight_range(Dm.initial(to_modify, (datum_action_cooldown_spell_farsight f) => f.sight_range) * (GET_MUTATION_POWER(this) == 1 ? 1 : 3));
        return dot;
    }
}

// tg: code/datums/mutations/farsight.dm:18
public partial class datum_action_cooldown_spell_farsight : datum_action_cooldown_spell
{
    // tg: code/datums/mutations/farsight.dm:35
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(sight_range)))
            set_sight_range(sight_range);
        return dot;
    }

    // tg: code/datums/mutations/farsight.dm:40
    public virtual void set_sight_range(object new_value = null)
    {
        if (Dm.eq(sight_range, new_value))
            return;
        sight_range = Dm.num(new_value);
        if (Dm.truthy(active))
            set_view(owner?.client); // owner?.client?.view_size.setTo(sight_range)
    }

    // tg: code/datums/mutations/farsight.dm:47
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = await base.cast(cast_on);
        mob_living caster = cast_on as mob_living;
        if (Dm.truthy(active))
        {
            reset_view(caster.client); // caster.client?.view_size.resetToDefault()
            active = FALSE;
            cooldown_time *= 2;
        }
        else
        {
            set_view(caster.client); // caster.client?.view_size.setTo(sight_range)
            active = TRUE;
            cooldown_time *= 0.5f;
        }
        await build_all_button_icons(UPDATE_BUTTON_STATUS);
        return dot;
    }

    // tg: code/datums/mutations/farsight.dm:60
    public override object get_caster_from_target(atom target = null)
    {
        return target; // This ability only effect's the caster's client, no reason to try and fail to effect the closet or mech we're inside.
    }

    // tg: code/datums/mutations/farsight.dm:63
    public override object is_action_active(atom_movable_screen_movable_action_button current_button = null)
    {
        return active;
    }

    // tg: code/datums/mutations/farsight.dm:66
    public override async DmTask Remove(mob remove_from = null)
    {
        await base.Remove(remove_from);
        if (Dm.truthy(active))
        {
            reset_view(remove_from.client); // remove_from.client?.view_size.resetToDefault()
            active = FALSE;
            cooldown_time *= 2;
        }
    }

    // The client's view size (/datum/view_data, view.dm's setTo and resetToDefault) is not declared: a client's view is the port's
    // first-person camera, which the server does not size by tiles. Recorded leaves (not-translated section 165).
    private static void set_view(client viewer)
    {
        if (viewer != null)
            Dm.Deferred("code/datums/mutations/farsight.dm:45", "farsight: client.view_size.setTo (the client's view size)");
    }

    private static void reset_view(client viewer)
    {
        if (viewer != null)
            Dm.Deferred("code/datums/mutations/farsight.dm:50", "farsight: client.view_size.resetToDefault (the client's view size)");
    }
}
