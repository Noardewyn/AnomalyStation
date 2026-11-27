// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/self/summonitem.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the instant summons spell and the abductor's baton recall). The var blocks are generated.

// tg: code/modules/spells/spell_types/self/summonitem.dm:1
public partial class datum_action_cooldown_spell_summonitem : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/self/summonitem.dm:18
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        await AddComponent(typeof(datum_component_action_item_overlay), Dm.named("item_callback", Dm.@new<datum_callback>(this, nameof(get_marked))));
    }

    // tg: code/modules/spells/spell_types/self/summonitem.dm:22
    public override object Destroy(float? force = null)
    {
        if (marked_item != null)
            unmark_item();
        return base.Destroy(force);
    }

    /// For use in callbacks to get the marked item
    // tg: code/modules/spells/spell_types/self/summonitem.dm:28
    public virtual object get_marked()
    {
        return marked_item;
    }

    // tg: code/modules/spells/spell_types/self/summonitem.dm:31
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        await DmTask.CompletedTask;
        return isliving(cast_on);
    }

    /// Set the passed object as our marked item
    // tg: code/modules/spells/spell_types/self/summonitem.dm:35
    public virtual void mark_item(obj to_mark = null)
    {
        marked_item = to_mark;
        RegisterSignal(marked_item, COMSIG_QDELETING, nameof(on_marked_item_deleted));

        name = Dm.text("Recall []", marked_item);
        Waiting.NoSleep(build_all_button_icons());
    }

    /// Unset our current marked item
    // tg: code/modules/spells/spell_types/self/summonitem.dm:43
    public virtual void unmark_item()
    {
        UnregisterSignal(marked_item, COMSIG_QDELETING);
        marked_item = null;

        if (!QDELING(this))
        {
            name = Dm.initial(this, (datum_action p) => p.name);
            Waiting.NoSleep(build_all_button_icons());
        }
    }

    /// Signal proc for [COMSIG_QDELETING] on our marked item, unmarks our item if it's deleted
    // tg: code/modules/spells/spell_types/self/summonitem.dm:52
    [SignalHandler]
    public virtual void on_marked_item_deleted(datum source = null)
    {
        if (owner != null)
            to_chat(owner, span_boldwarning(I18n.T("You sense your marked item has been destroyed!")));
        unmark_item();
    }

    // tg: code/modules/spells/spell_types/self/summonitem.dm:59
    public override async DmTask<object> cast(atom cast_on__atom = null)
    {
        mob_living cast_on = cast_on__atom as mob_living;
        object dot = await base.cast(cast_on__atom);
        if (QDELETED(marked_item))
        {
            await try_link_item(cast_on);
            return dot;
        }

        if (marked_item == cast_on.get_active_held_item())
        {
            await try_unlink_item(cast_on);
            return dot;
        }

        await try_recall_item(cast_on);
        return dot;
    }

    /// Checks if the passed item is a valid item that can be marked / linked to summon.
    // tg: code/modules/spells/spell_types/self/summonitem.dm:72
    public virtual object can_link_to(obj_item potential_mark = null, mob_living caster = null)
    {
        if (Dm.truthy(Dm.bitand(potential_mark.item_flags, ABSTRACT)))
            return FALSE;
        return TRUE;
    }

    /// If we don't have a marked item, attempts to mark the caster's held item.
    // tg: code/modules/spells/spell_types/self/summonitem.dm:78
    public virtual async DmTask<object> try_link_item(mob_living caster = null)
    {
        obj_item potential_mark = caster.get_active_held_item() as obj_item;
        if (potential_mark == null)
        {
            if (Dm.truthy(caster.get_inactive_held_item()))
                to_chat(caster, span_warning(I18n.T("You must hold the desired item in your hands to mark it for recall!")));
            else
                to_chat(caster, span_warning(I18n.T("You aren't holding anything that can be marked for recall!")));
            return FALSE;
        }

        string link_message = "";
        if (!Dm.truthy(can_link_to(potential_mark, caster)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(potential_mark.SEND_SIGNAL(COMSIG_ITEM_MARK_RETRIEVAL, Dm.list(potential_mark, this, caster)), COMPONENT_BLOCK_MARK_RETRIEVAL)))
            return FALSE;
        if (HAS_TRAIT(potential_mark, TRAIT_NODROP))
            link_message += I18n.T("Though it feels redundant... ");

        link_message += I18n.T("You mark [] for recall.", potential_mark);
        to_chat(caster, span_notice(link_message));
        mark_item(potential_mark);
        return TRUE;
    }

    /// If we have a marked item and it's in our hand, we will try to unlink it
    // tg: code/modules/spells/spell_types/self/summonitem.dm:101
    public virtual async DmTask<object> try_unlink_item(mob_living caster = null)
    {
        to_chat(caster, span_notice(I18n.T("You begin removing the mark on []...", marked_item)));
        if (!Dm.truthy(await do_after(caster, 5 * SECONDS, marked_item)))
        {
            to_chat(caster, span_notice(I18n.T("You decide to keep [] marked.", marked_item)));
            return FALSE;
        }

        to_chat(caster, span_notice(I18n.T("You remove the mark on [] to use elsewhere.", marked_item)));
        unmark_item();
        return TRUE;
    }

    /// Recalls our marked item to the caster. May bring some unexpected things along.
    // tg: code/modules/spells/spell_types/self/summonitem.dm:112
    public virtual async DmTask try_recall_item(mob_living caster = null)
    {
        obj item_to_retrieve = marked_item;

        if (item_to_retrieve.loc != null)
        {
            // I don't want to know how someone could put something
            // inside itself but these are wizards so let's be safe
            float infinite_recursion = 0;

            // if it's in something, you get the whole thing.
            while (!isturf(item_to_retrieve.loc) && infinite_recursion < 10)
            {
                if (isitem(item_to_retrieve.loc))
                {
                    obj_item mark_loc = item_to_retrieve.loc as obj_item;
                    // Being able to summon abstract things because
                    // your item happened to get placed there is a no-no
                    if (Dm.truthy(Dm.bitand(mark_loc.item_flags, ABSTRACT)))
                        break;
                }

                // If its on someone, properly drop it
                if (ismob(item_to_retrieve.loc))
                {
                    if (!issilicon(item_to_retrieve.loc))
                        break;

                    // Items in silicons warp the whole silicon
                    mob holding_mark = item_to_retrieve.loc as mob;
                    holding_mark.loc.visible_message(span_warning(I18n.T("[] suddenly disappears!", holding_mark)));
                    await holding_mark.forceMove(caster.loc);
                    holding_mark.loc.visible_message(span_warning(I18n.T("[] suddenly appears!", holding_mark)));
                    holding_mark.SEND_SIGNAL(COMSIG_MAGIC_RECALL, Dm.list(holding_mark, caster, item_to_retrieve));
                    playsound(holding_mark, Dm.resource("sound/effects/magic/summonitems_generic.ogg"), 50, TRUE);
                    return;
                }

                else if (isobj(item_to_retrieve.loc))
                {
                    obj retrieved_item = item_to_retrieve.loc as obj;
                    // Can't bring anchored things
                    if (Dm.truthy(retrieved_item.anchored))
                        break;
                    // Edge cases for moving certain machinery...
                    if (istype(retrieved_item, typeof(obj_machinery_portable_atmospherics)))
                    {
                        obj_machinery_portable_atmospherics atmos_item = retrieved_item as obj_machinery_portable_atmospherics;
                        await atmos_item.disconnect();
                        atmos_item.update_appearance();
                    }

                    // Otherwise bring the whole thing with us
                    item_to_retrieve = retrieved_item;
                }

                infinite_recursion += 1;
            }
        }

        if (item_to_retrieve == null)
            return;

        item_to_retrieve.SEND_SIGNAL(COMSIG_MAGIC_RECALL, Dm.list(item_to_retrieve, caster, item_to_retrieve));

        if (ismob(item_to_retrieve.loc))
        {
            mob holder = item_to_retrieve.loc as mob;
            if (!Dm.truthy(await holder.dropItemToGround(item_to_retrieve as obj_item, force: TRUE)))
                return;
        }

        item_to_retrieve.loc?.visible_message(span_warning(I18n.T("[] suddenly disappears!", item_to_retrieve)));

        if (isitem(item_to_retrieve) && Dm.truthy(await caster.put_in_hands(item_to_retrieve as obj_item)))
            item_to_retrieve.loc.visible_message(span_warning(I18n.T("[] suddenly appears in []'s hand!", item_to_retrieve, caster)));
        else
        {
            await item_to_retrieve.forceMove(caster.drop_location() as atom);
            item_to_retrieve.loc.visible_message(span_warning(I18n.T("[] suddenly appears!", item_to_retrieve)));
        }

        playsound(get_turf(item_to_retrieve), Dm.resource("sound/effects/magic/summonitems_generic.ogg"), 50, TRUE);
    }
}

// tg: code/modules/spells/spell_types/self/summonitem.dm:179
public partial class datum_action_cooldown_spell_summonitem_abductor : datum_action_cooldown_spell_summonitem
{
    // tg: code/modules/spells/spell_types/self/summonitem.dm:193
    public override object can_link_to(obj_item potential_mark = null, mob_living caster = null)
    {
        object dot = base.can_link_to(potential_mark, caster);
        if (!Dm.truthy(dot))
            return dot;

        if (!istype(potential_mark, typeof(obj_item_melee_baton_abductor)))
        {
            to_chat(caster, span_warning(I18n.T("[] has no translocation crystals to link to!", potential_mark)));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/spells/spell_types/self/summonitem.dm:204
    public override async DmTask<object> try_unlink_item(mob_living caster = null)
    {
        to_chat(caster, span_warning(I18n.T("You can't unlink []'s translocation crystals.", marked_item)));
        await DmTask.CompletedTask;
        return FALSE;
    }
}
