// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/special/grenade_launcher.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the grenadier kit's pneumatic grenade launcher; its fantasy affixes: gaps GU1, the fantasy
// component that calls them is not ported). The var block is generated (D111).

// tg: code/modules/projectiles/guns/special/grenade_launcher.dm:1
public partial class obj_item_gun_grenadelauncher : obj_item_gun
{
    // tg: code/modules/projectiles/guns/special/grenade_launcher.dm:16
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("[] / [] grenades loaded.", grenades.len, max_grenades));
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/grenade_launcher.dm:20
    public override async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        await base.apply_fantasy_bonuses(bonus);
        max_grenades = Dm.num(modify_fantasy_variable("max_syringes", max_grenades, bonus, minimum: 1));
    }

    // tg: code/modules/projectiles/guns/special/grenade_launcher.dm:24
    public override async DmTask remove_fantasy_bonuses(object bonus = null)
    {
        max_grenades = Dm.num(reset_fantasy_variable("max_syringes", max_grenades));
        await base.remove_fantasy_bonuses(bonus);
    }

    // tg: code/modules/projectiles/guns/special/grenade_launcher.dm:28
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!isgrenade(tool))
            return NONE;
        if (istype(tool, typeof(obj_item_grenade_c4)))
            return NONE;
        if (grenades.len == max_grenades)
        {
            balloon_alert(user, I18n.T("it's already full!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        Dm.append(ref grenades, tool);
        balloon_alert(user, I18n.T("[] / [] grenades loaded", grenades.len, max_grenades));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/special/grenade_launcher.dm:42
    public override object can_shoot()
    {
        return grenades.len;
    }

    // tg: code/modules/projectiles/guns/special/grenade_launcher.dm:45
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        user.visible_message(span_danger(I18n.T("[] fired a grenade!", user)),
            span_danger(I18n.T("You fire the grenade launcher!")));
        var F = grenades[1] as obj_item_grenade; //Now with less copypasta!
        grenades.Remove(F);
        await F.forceMove(user.loc);
        await F.throw_at(target, 30, 2, user);
        message_admins(Dm.text("[] fired a grenade ([]) from a grenade launcher ([]) from [] at [] [].", key_name_admin(user), F.name, this, AREACOORD(user), target, AREACOORD(target)));
        user.log_message(Dm.text("fired a grenade ([]) with a grenade launcher ([]) from [] at [] [].", F.name, this, AREACOORD(user), target, AREACOORD(target)), LOG_GAME);
        user.log_message(Dm.text("fired a grenade ([]) with a grenade launcher ([]) from [] at [] [].", F.name, this, AREACOORD(user), target, AREACOORD(target)), LOG_ATTACK, log_globally: FALSE);
        F.active = 1;
        F.icon_state = Dm.text("[]_active", Dm.initial(F, (obj_item_grenade g) => g.icon_state));
        playsound(user.loc, "sound/items/weapons/armbomb.ogg", 75, TRUE, -3);
        _addtimer(Dm.@new<datum_callback>(F, nameof(obj_item_grenade.detonate)), 1.5f * SECONDS,
            file: "code/modules/projectiles/guns/special/grenade_launcher.dm", line: 61);
        return TRUE;
    }
}
