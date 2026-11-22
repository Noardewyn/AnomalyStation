// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/headpike.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole, with the spears (weaponry/melee/spear.dm): a head on a spear, slapcrafted from a spear and a
// head or left by a spear's suicide; a mapped one makes its own spear and a nameless head. Named after the head's face and the
// spear, the head drawn over the pike twelve pixels up; taken down by hand, or broken when the head or the spear leaves it (the head
// always drops, the spear only when it is taken apart). The `spear` var holds the spear itself (the converter typed it as a path):
// hand-declared here. The var block is generated (D111).

// tg: code/game/objects/structures/headpike.dm:1
public partial class obj_structure_headpike : obj_structure
{
    // tg: code/game/objects/structures/headpike.dm:9  var/obj/item/spear/spear
    public obj_item_spear spear;

    // tg: code/game/objects/structures/headpike.dm:2  (the generated constructor, kept here: hand mode, see the header)
    public obj_structure_headpike()
    {
        name = "spooky head on a spear";
        desc = "When you really want to send a message.";
        icon = Dm.resource("icons/obj/structures.dmi");
        icon_state = "headpike";
        density = 0;
        anchored = 1;
    }

    // tg: code/game/objects/structures/headpike.dm:8  (the generated var initializer, kept here: hand mode)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_glass), 115), Dm.kv(typeof(datum_material_iron), 65)); // tg: code/game/objects/structures/headpike.dm:8
    }

    // tg: code/game/objects/structures/headpike.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
        {
            spear = Dm.@new(speartype, this) as obj_item_spear;
            victim = Dm.@new<obj_item_bodypart_head>(this);
            victim.real_name = generate_random_name() as string;
        }
        pixel_x = rand(-8, 8);
        return dot;
    }

    // tg: code/game/objects/structures/headpike.dm:35
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref victim);
        QDEL_NULL(ref spear);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/headpike.dm:40
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        victim = Dm.locate<obj_item_bodypart_head>(contents);
        spear = Dm.locate_type(speartype, contents) as obj_item_spear;
        update_appearance();
    }

    // tg: code/game/objects/structures/headpike.dm:46
    public override object update_name(float? updates = null)
    {
        name = I18n.T("[] on a []", victim.get_face_name(), spear.name);
        return base.update_name(updates);
    }

    // tg: code/game/objects/structures/headpike.dm:50
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        if (victim == null)
            return dot;
        mutable_appearance appearance = Dm.@new<mutable_appearance>();
        appearance.copy_overlays(victim);
        appearance.pixel_z = 12;
        appearance.layer = layer + 0.1f;
        Dm.append(dot, appearance);
        return dot;
    }

    // tg: code/game/objects/structures/headpike.dm:60
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone != victim && gone != spear)
            return dot;
        if (gone == victim)
            victim = null;
        if (gone == spear)
            spear = null;
        if (!QDELETED(this))
            await deconstruct(TRUE);
        return dot;
    }

    // tg: code/game/objects/structures/headpike.dm:71
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        obj_item_bodypart_head our_head = victim;
        obj_item_spear our_spear = spear;
        victim = null;
        spear = null;
        if (our_head != null)
            await our_head.forceMove(drop_location() as atom); //Make sure the head always comes off
        if (!Dm.truthy(disassembled))
        {
            await base.atom_deconstruct(disassembled);
            return;
        }
        if (our_spear != null)
            await our_spear.forceMove(drop_location() as atom);
    }

    // tg: code/game/objects/structures/headpike.dm:81
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        to_chat(user, span_notice(I18n.T("You take down [].", this)));
        await deconstruct(TRUE);
        return dot;
    }
}
