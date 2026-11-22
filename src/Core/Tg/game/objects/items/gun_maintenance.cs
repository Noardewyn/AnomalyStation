// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/gun_maintenance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. The gun maintenance kit (three uses; the makeshift one, one) repairs a damaged gun to full, resets
// a ballistic gun's raised misfire chance and unjams a bolt-action rifle with its jam chances reset; as in tg, a ballistic gun always
// spends a use. The kit says its uses on examine and is used up at none. The var blocks are generated (D111).

// tg: code/game/objects/items/gun_maintenance.dm:1
public partial class obj_item_gun_maintenance_supplies : obj_item
{
    // tg: code/game/objects/items/gun_maintenance.dm:23
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("This kit has [] uses out of [] left.", uses, max_uses)));
        return dot;
    }

    // tg: code/game/objects/items/gun_maintenance.dm:27
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return ITEM_INTERACT_BLOCKING;

        if (!isgun(interacting_with))
        {
            balloon_alert(user, I18n.T("not a gun!"));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_gun gun_to_fix = interacting_with as obj_item_gun;

        bool gun_is_damaged = Dm.num(gun_to_fix.get_integrity()) < gun_to_fix.max_integrity;
        float use_charge = FALSE;

        if (gun_is_damaged)
        {
            await gun_to_fix.repair_damage(gun_to_fix.max_integrity);
            use_charge = TRUE;
        }

        if (gun_to_fix is obj_item_gun_ballistic)
        {
            obj_item_gun_ballistic ballistic_gun_to_fix = gun_to_fix as obj_item_gun_ballistic;

            if (ballistic_gun_to_fix.misfire_probability > Dm.initial(ballistic_gun_to_fix, (obj_item_gun_ballistic p) => p.misfire_probability))
                ballistic_gun_to_fix.misfire_probability = Dm.initial(ballistic_gun_to_fix, (obj_item_gun_ballistic p) => p.misfire_probability);

            if (ballistic_gun_to_fix is obj_item_gun_ballistic_rifle_boltaction)
            {
                obj_item_gun_ballistic_rifle_boltaction rifle_to_fix = ballistic_gun_to_fix as obj_item_gun_ballistic_rifle_boltaction;
                if (Dm.truthy(rifle_to_fix.jammed))
                {
                    rifle_to_fix.jammed = FALSE;
                    rifle_to_fix.unjam_chance = Dm.initial(rifle_to_fix, (obj_item_gun_ballistic_rifle_boltaction p) => p.unjam_chance);
                    rifle_to_fix.jamming_chance = Dm.initial(rifle_to_fix, (obj_item_gun_ballistic_rifle_boltaction p) => p.jamming_chance);
                }
            }
            use_charge = TRUE;
        }

        if (!Dm.truthy(use_charge))
        {
            balloon_alert(user, I18n.T("no need for repair!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("maintenance complete"));
        use_the_kit();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/gun_maintenance.dm:67
    public virtual void use_the_kit()
    {
        uses--;
        if (!Dm.truthy(uses))
            qdel(this);
    }
}
