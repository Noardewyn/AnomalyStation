// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/wall_engraver.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the wall engraver, whole (the chisel engraves a memory into a wall).

/// An element that lets you engrave walls when right click is used
// tg: code/datums/elements/wall_engraver.dm:2
public partial class datum_element_wall_engraver : datum_element
{
    // tg: code/datums/elements/wall_engraver.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);

        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(target, COMSIG_ITEM_PRE_ATTACK_SECONDARY, nameof(on_item_pre_attack_secondary));
        return dot;
    }

    // tg: code/datums/elements/wall_engraver.dm:13
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ATOM_EXAMINE);
        UnregisterSignal(source, COMSIG_ITEM_PRE_ATTACK_SECONDARY);
        return dot;
    }

    ///signal called on parent being examined
    // tg: code/datums/elements/wall_engraver.dm:19
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(I18n.T("You can engrave some walls with your secondary attack if you can think of something interesting to engrave.")));
    }

    ///signal called on parent being used to right click attack something
    // tg: code/datums/elements/wall_engraver.dm:24
    [SignalHandler]
    public virtual object on_item_pre_attack_secondary(datum source = null, atom target = null, mob_living user = null)
    {
        Waiting.Detach(try_chisel(source as obj_item, target as turf_closed, user)); // INVOKE_ASYNC(src, PROC_REF(try_chisel), source, target, user)

        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/elements/wall_engraver.dm:31
    public virtual async DmTask try_chisel(obj_item item = null, turf_closed wall = null, mob_living user = null)
    {
        if (wall == null || user.mind == null)
            return;
        if (HAS_TRAIT_FROM(wall, TRAIT_NOT_ENGRAVABLE, ENGRAVED_TRAIT))
        {
            user.balloon_alert(user, I18n.T("wall has already been engraved!"));
            return;
        }
        if (HAS_TRAIT(wall, TRAIT_NOT_ENGRAVABLE))
        {
            user.balloon_alert(user, I18n.T("wall cannot be engraved!"));
            return;
        }
        if (!Dm.truthy(Dm.length(user.mind?.memories)))
        {
            user.balloon_alert(user, I18n.T("nothing memorable to engrave!"));
            return;
        }
        datum_memory memory_to_engrave = await user.mind.select_memory("engrave") as datum_memory;
        if (memory_to_engrave == null)
            return;
        if (!Dm.truthy(await user.Adjacent(wall)))
            return;
        item.add_fingerprint(user);
        playsound(item, item.hitsound, 30, TRUE, -1);
        await user.do_attack_animation(wall);
        user.balloon_alert(user, I18n.T("engraving wall..."));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: wall)))
            return;
        user.balloon_alert(user, I18n.T("wall engraved"));
        await user.do_attack_animation(wall);

        bool do_persistent_save = !Dm.truthy(Dm.bitand(memory_to_engrave.memory_flags, MEMORY_FLAG_NOPERSISTENCE));
        object engraved_story = memory_to_engrave.generate_story(STORY_ENGRAVING, STORY_FLAG_DATED);

        if (!Dm.truthy(engraved_story))
            throw Dm.CRASH(Dm.text("Tried to submit a memory with an invalid story []", memory_to_engrave), "code/datums/elements/wall_engraver.dm", 61);

        await wall.AddComponent(typeof(datum_component_engraved), engraved_story, Dm.named("persistent_save", do_persistent_save ? TRUE : FALSE), Dm.named("story_value", memory_to_engrave.story_value));
        memory_to_engrave.memory_flags = Dm.bitor(memory_to_engrave.memory_flags, MEMORY_FLAG_ALREADY_USED);
        //while someone just engraved a story "worth engraving" we should add this to SSpersistence for a possible prison tattoo

        if (do_persistent_save)
        {
            DmList tattoo_entry = Dm.list();

            object tattoo_story = memory_to_engrave.generate_story(STORY_TATTOO);

            if (!Dm.truthy(tattoo_story))
                throw Dm.CRASH(Dm.text("Tried to submit a memory with an invalid story []", memory_to_engrave), "code/datums/elements/wall_engraver.dm", 73);

            tattoo_entry["story"] = tattoo_story;
            SSpersistence.prison_tattoos_to_save = Dm.add(SSpersistence.prison_tattoos_to_save, Dm.list(tattoo_entry)) as DmList;
        }
    }
}
