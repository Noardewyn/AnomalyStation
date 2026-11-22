// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/ballistic/shotgun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice D: the old buckshot's smoke (the black market shell, the "old" box of security_boxes.dm). Gaps GU1 (D333): the scatter laser
// shell's malfunction under an EMP, the technological shell's slapcrafting into the meteor, pulse, dragon's breath and ion slugs, the
// shotgun darts' reagents (not collected into a box from the floor). Left (docs/ai/specs/not-translated.md section 215): the bioterror dart
// (shotgun.dm:232, the nuclear operatives' box, D333). The other shells only set vars (the generated var blocks, D111).

// tg: code/modules/projectiles/ammunition/ballistic/shotgun.dm:99
public partial class obj_item_ammo_casing_shotgun_buckshot_old : obj_item_ammo_casing_shotgun_buckshot
{
    // tg: code/modules/projectiles/ammunition/ballistic/shotgun.dm:104
    public override async DmTask<object> fire_casing(atom target = null, mob_living user = null, object @params = null, object distro = null, object quiet = null, object zone_override = null, object spread = null, atom fired_from = null)
    {
        object dot = await base.fire_casing(target, user, @params, distro, quiet, zone_override, spread, fired_from);
        if (fired_from != null)
            await do_smoke(0, fired_from, fired_from);
        return dot;
    }
}

// tg: code/modules/projectiles/ammunition/ballistic/shotgun.dm:168
public partial class obj_item_ammo_casing_shotgun_scatterlaser : obj_item_ammo_casing_shotgun
{
    // tg: code/modules/projectiles/ammunition/ballistic/shotgun.dm:178
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (loaded_projectile == null || !prob(40 / Dm.num(severity)))
            return dot;
        name = I18n.T("malfunctioning laser shell");
        desc = I18n.T("An advanced shotgun shell that uses a micro laser to replicate the effects of a scatter laser weapon in a ballistic package. The capacitor powering this assembly appears to be smoking.");
        projectile_type = typeof(obj_projectile_beam_scatter_pathetic);
        loaded_projectile = Dm.@new(projectile_type, this) as obj_projectile;
        return dot;
    }
}

// tg: code/modules/projectiles/ammunition/ballistic/shotgun.dm:187
public partial class obj_item_ammo_casing_shotgun_techshell : obj_item_ammo_casing_shotgun
{
    // tg: code/modules/projectiles/ammunition/ballistic/shotgun.dm:194
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // tg: code/modules/projectiles/ammunition/ballistic/shotgun.dm:197  var/static/list/slapcraft_recipe_list (a proc-level static, rules 10.6)
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", TgState.Current.obj_item_ammo_casing_shotgun_techshell__Initialize__slapcraft_recipe_list)));
        return dot;
    }
}

// tg: code/modules/projectiles/ammunition/ballistic/shotgun.dm:204
public partial class obj_item_ammo_casing_shotgun_dart : obj_item_ammo_casing_shotgun
{
    // tg: code/modules/projectiles/ammunition/ballistic/shotgun.dm:212
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(reagent_amount, OPENCONTAINER);
        return dot;
    }

    // Can't be collected into an ammo box from the floor, for whatever reason.
    // tg: code/modules/projectiles/ammunition/ballistic/shotgun.dm:217
    public override async DmTask<object> collect_into_box(mob_living user = null, obj_item_ammo_box box = null)
    {
        return FALSE;
    }
}

// The static lists of the shotgun shells (rules 10.6: proc-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/modules/projectiles/ammunition/ballistic/shotgun.dm:197  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/meteorslug, ...)
    internal DmList obj_item_ammo_casing_shotgun_techshell__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_meteorslug), typeof(datum_crafting_recipe_pulseslug), typeof(datum_crafting_recipe_dragonsbreath), typeof(datum_crafting_recipe_ionslug));
}
