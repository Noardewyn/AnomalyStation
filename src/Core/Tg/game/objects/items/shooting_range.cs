// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/shooting_range.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file (the shooting targets, no target stake at the snapshot). A bullet or a beam that hits a target
// leaves tg's dent, scorch or light scorch where it struck (an overlay image shifted to the hit's pixel), unless the 25% roll
// spares it, the target already shows 35 marks, or the projectile is harmless; a welder clears them; the clown target honks. DV522:
// the server reads no tg icon (D104), so tg's test that the hit pixel of the target's sprite is opaque (icon.GetPixel) is not asked:
// a projectile fired at the target (its original) counts as on the target. As in tg, a practice laser leaves no mark: the object's
// bullet_act (obj_defense.dm:30) answers a hit that did no damage as a block, so always_leave_marks is never reached. The var blocks
// are generated (D111).

// tg: code/game/objects/items/shooting_range.dm:1
public partial class obj_item_target : obj_item
{
    // tg: code/game/objects/items/shooting_range.dm:12
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, volume: 40)))
        {
            LAZYNULL(ref bullethole_overlays);
            balloon_alert(user, I18n.T("target repaired"));
            update_appearance(UPDATE_OVERLAYS);
        }
        return TRUE;
    }

    // tg: code/game/objects/items/shooting_range.dm:19
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.combine(ref dot, bullethole_overlays);
        return dot;
    }

    // Projectiles which do not deal damage will not leave dent / scorch mark graphics.
    // However we snowflake some projectiles to leave them anyway, because they're appropriate.
    // tg: code/game/objects/items/shooting_range.dm:31  var/static/list/always_leave_marks (bullet_act's)
    private static DmList always_leave_marks
    {
        get => TgState.Current.obj_item_target_bullet_act__always_leave_marks;
        set => TgState.Current.obj_item_target_bullet_act__always_leave_marks = value;
    }

    // tg: code/game/objects/items/shooting_range.dm:23
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        piercing_hit ??= FALSE;
        if (prob(25))
            return await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked); // RNG change to just not leave a mark, like walls
        if (Dm.num(length(overlays)) > 35)
            return await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked); // Too many bullets, we're done here

        if (always_leave_marks == null)
        {
            always_leave_marks = typecacheof(Dm.list(
                typeof(obj_projectile_beam_practice),
                typeof(obj_projectile_beam_laser_rapid_practice)
            )) as DmList;
        }

        bool is_invalid_damage = hitting_projectile.damage_type != BRUTE && hitting_projectile.damage_type != BURN;
        bool is_safe = !Dm.truthy(hitting_projectile.is_hostile_projectile());
        bool is_generic_projectile = !Dm.truthy(is_type_in_typecache(hitting_projectile, always_leave_marks));
        if (is_generic_projectile && (is_invalid_damage || is_safe))
            return await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked); // Don't bother unless it's real shit

        float p_x = Dm.num(hitting_projectile.p_x) + Dm.num(pick(0, 0, 0, 0, 0, -1, 1)); // really ugly way of coding "sometimes offset p_x!"
        float p_y = Dm.num(hitting_projectile.p_y) + Dm.num(pick(0, 0, 0, 0, 0, -1, 1));
        // var/icon/our_icon = icon(icon, icon_state); if(!our_icon.GetPixel(p_x, p_y) || ...): DV522, the server reads no tg icon
        // (D104), so the hit pixel is taken as opaque and only the projectile's original decides
        if (hitting_projectile.original != this)
            return BULLET_ACT_FORCE_PIERCE; // We, "missed", I guess?

        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (!Dm.eq(dot, BULLET_ACT_HIT))
            return dot;

        image bullet_hole = image(icon: Dm.resource("icons/effects/effects.dmi"), icon_state: "dent", layer: OBJ_LAYER + 0.5f);
        bullet_hole.pixel_w = p_x - 1; //offset correction
        bullet_hole.pixel_z = p_y - 1;
        if (hitting_projectile.damage_type != BRUTE)
        {
            bullet_hole.dir = Dm.num(pick(GLOB.cardinals)); // random scorch design (/image/proc/setDir, __HELPERS/icons.dm:829, inline)
            if (Dm.num(hitting_projectile.damage) < 20 && is_generic_projectile)
                bullet_hole.icon_state = "light_scorch";
            else
                bullet_hole.icon_state = "scorch";
        }

        LAZYADD(ref bullethole_overlays, bullet_hole);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }
}

// tg: code/game/objects/items/shooting_range.dm:80
public partial class obj_item_target_clown : obj_item_target
{
    // tg: code/game/objects/items/shooting_range.dm:85
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        playsound(this, Dm.resource("sound/items/bikehorn.ogg"), 50, TRUE);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/shooting_range.dm:31  var/static/list/always_leave_marks (bullet_act's)
    internal DmList obj_item_target_bullet_act__always_leave_marks;
}
