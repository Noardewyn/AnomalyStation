// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/conjure_item/_conjure_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/spells/spell_types/conjure_item/_conjure_item.dm:1
public partial class datum_action_cooldown_spell_conjure_item : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/conjure_item/_conjure_item.dm:16
    public override object Destroy(float? force = null)
    {
        // If we delete_old, clean up all of our items on delete
        if (Dm.truthy(delete_old))
            QDEL_LAZYLIST(ref item_refs);

        // If we don't delete_old, just let all the items be free
        else
            item_refs = null;

        return base.Destroy(force);
    }

    // tg: code/modules/spells/spell_types/conjure_item/_conjure_item.dm:27
    public override async DmTask<object> can_cast_spell(float? feedback = null)
    {
        object dot = await base.can_cast_spell(feedback);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!Dm.truthy(requires_hands))
            return TRUE;

        if (!isliving(owner))
            return FALSE;

        mob_living living_owner = owner as mob_living;
        if (living_owner.usable_hands < 1)
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("no free hands!"));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/spells/spell_types/conjure_item/_conjure_item.dm:46
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        if (!Dm.truthy(requires_hands))
            return TRUE;
        if (!isliving(cast_on))
            return FALSE;
        mob_living living_cast_on = cast_on as mob_living;
        return living_cast_on.usable_hands >= 1 ? TRUE : FALSE;
    }

    // tg: code/modules/spells/spell_types/conjure_item/_conjure_item.dm:54
    public override async DmTask<object> cast(atom cast_on = null)
    {
        if (Dm.truthy(delete_old) && Dm.truthy(LAZYLEN(item_refs)))
            QDEL_LAZYLIST(ref item_refs);

        mob mob_caster = cast_on as mob;
        if (mob_caster != null)
        {
            obj_item existing_item = mob_caster.get_active_held_item() as obj_item;
            if (existing_item != null)
                await mob_caster.dropItemToGround(existing_item);
        }

        obj created = make_item(cast_on) as obj;
        if (QDELETED(created))
            throw Dm.CRASH(Dm.text("[] tried to create an item, but failed. It's item type is [].", type, item_type), "code/modules/spells/spell_types/conjure_item/_conjure_item.dm", 66);

        if (!isitem(created))
        {
            await created.forceMove(cast_on.drop_location() as atom);
            return null;
        }

        if (mob_caster != null)
            await mob_caster.put_in_hands(created as obj_item, del_on_fail: delete_on_failure);

        post_created(cast_on, created);

        return await base.cast(cast_on);
    }

    /// Instantiates the item we're conjuring and returns it.
    /// Item is made in at the caster's.
    // tg: code/modules/spells/spell_types/conjure_item/_conjure_item.dm:81
    public virtual object make_item(atom caster = null)
    {
        obj_item made_item = Dm.@new(item_type, caster.loc) as obj_item;
        LAZYADD(ref item_refs, WEAKREF(made_item));
        return made_item;
    }

    /// Called after item has been handed to the caster, for any additional presentation
    // tg: code/modules/spells/spell_types/conjure_item/_conjure_item.dm:87
    public virtual void post_created(atom cast_on = null, atom created = null)
    {
        return;
    }
}
