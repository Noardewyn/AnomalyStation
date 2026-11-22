// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/clown_ops/clownop.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3, D316: the clown operatives). The var blocks and the preview outfits are generated (D111).

// tg: code/modules/antagonists/clown_ops/clownop.dm:2
public partial class datum_antagonist_nukeop_clownop : datum_antagonist_nukeop
{
    // tg: code/modules/antagonists/clown_ops/clownop.dm:15
    public override async DmTask admin_add(datum_mind new_owner = null, mob admin = null)
    {
        await new_owner.add_antag_datum(this);
        message_admins(Dm.text("[] has clown op'ed [].", key_name_admin(admin), key_name_admin(new_owner)));
        log_admin(Dm.text("[] has clown op'ed [].", key_name(admin), key_name(new_owner)));
    }

    // tg: code/modules/antagonists/clown_ops/clownop.dm:20
    public override async DmTask<object> apply_innate_effects(mob_living mob_override = null)
    {
        object dot = await base.apply_innate_effects(mob_override);
        mob_living L = (owner.current as mob_living) ?? mob_override;
        ADD_TRAIT(L.mind, TRAIT_NAIVE, CLOWNOP_TRAIT);
        return dot;
    }

    // tg: code/modules/antagonists/clown_ops/clownop.dm:25
    public override async DmTask remove_innate_effects(mob_living mob_override = null)
    {
        mob_living L = (owner.current as mob_living) ?? mob_override;
        REMOVE_TRAIT(L.mind, TRAIT_NAIVE, CLOWNOP_TRAIT);
    }

    // tg: code/modules/antagonists/clown_ops/clownop.dm:29
    public override async DmTask<object> equip_op()
    {
        object dot = await base.equip_op();
        mob_living current_mob = owner.current as mob_living;
        var liver = current_mob.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null)
            ADD_TRAIT(liver, TRAIT_COMEDY_METABOLISM, CLOWNOP_TRAIT);
        return dot;
    }
}

// tg: code/modules/antagonists/clown_ops/clownop.dm:44
public partial class datum_antagonist_nukeop_leader_clownop : datum_antagonist_nukeop_leader
{
    // tg: code/modules/antagonists/clown_ops/clownop.dm:36
    public override async DmTask give_alias()
    {
        title ??= Dm.str(pick("Head Honker", "Slipmaster", "Clown King", "Honkbearer"));
        await base.give_alias();
        if (ishuman(owner.current))
            await owner.current.fully_replace_character_name(owner.current.real_name, Dm.text("[] []", title, owner.current.real_name));
        else
            await owner.current.fully_replace_character_name(owner.current.real_name, Dm.text("[] []", nuke_team.syndicate_name, title));
    }

    // tg: code/modules/antagonists/clown_ops/clownop.dm:53
    public override async DmTask<object> apply_innate_effects(mob_living mob_override = null)
    {
        object dot = await base.apply_innate_effects(mob_override);
        mob_living L = (owner.current as mob_living) ?? mob_override;
        ADD_TRAIT(L.mind, TRAIT_NAIVE, CLOWNOP_TRAIT);
        return dot;
    }

    // tg: code/modules/antagonists/clown_ops/clownop.dm:58
    public override async DmTask remove_innate_effects(mob_living mob_override = null)
    {
        mob_living L = (owner.current as mob_living) ?? mob_override;
        REMOVE_TRAIT(L.mind, TRAIT_NAIVE, CLOWNOP_TRAIT);
    }

    // tg: code/modules/antagonists/clown_ops/clownop.dm:62
    public override async DmTask<object> equip_op()
    {
        object dot = await base.equip_op();
        mob_living L = owner.current as mob_living;
        var liver = L.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (liver != null)
            ADD_TRAIT(liver, TRAIT_COMEDY_METABOLISM, CLOWNOP_TRAIT);
        return dot;
    }
}
