// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/toolboxes/mechanical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4a P3: the containers' PopulateContents; syndicate G4a, D316: the rest).

// Syndicate G4a (D316): the assistant's traitor toolbox, whose damage grows with the telecrystals inside, and the heirloom's empty
// toolbox.

// tg: code/game/objects/items/storage/toolboxes/mechanical.dm:23
public partial class obj_item_storage_toolbox_mechanical_old_heirloom : obj_item_storage_toolbox_mechanical_old
{
    // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:30
    public override async DmTask PopulateContents()
    {
        return;
    }
}

// tg: code/game/objects/items/storage/toolboxes/mechanical.dm:38
public partial class obj_item_storage_toolbox_mechanical_old_clean : obj_item_storage_toolbox_mechanical_old
{
    // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:47
    public virtual void calc_damage()
    {
        float power = 0;
        foreach (obj_item_stack_telecrystal stored_crystals in Dm.each<obj_item_stack_telecrystal>(get_all_contents()))
            power += Dm.num(stored_crystals.amount);
        force = Dm.num(Dm.initial(this, (obj_item_storage_toolbox_mechanical_old_clean p) => p.force)) + power;
        throwforce = Dm.num(Dm.initial(this, (obj_item_storage_toolbox_mechanical_old_clean p) => p.throwforce)) + power;
    }

    // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:54
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        calc_damage();
        return await base.pre_attack(target, user, modifiers, attack_modifiers);
    }

    // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:58
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        calc_damage();
        return base.throw_impact(hit_atom, throwingdatum, __extra);
    }

    // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:62
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_screwdriver>(this);
        Dm.@new<obj_item_wrench>(this);
        Dm.@new<obj_item_weldingtool>(this);
        Dm.@new<obj_item_crowbar>(this);
        Dm.@new<obj_item_wirecutters>(this);
        Dm.@new<obj_item_multitool>(this);
        Dm.@new<obj_item_clothing_gloves_color_yellow>(this);
    }
}

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'.

// tg: code/game/objects/items/storage/toolboxes/mechanical.dm:9
public partial class obj_item_storage_toolbox_mechanical : obj_item_storage_toolbox
{
    // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:9
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_screwdriver>(this); // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:10
        Dm.@new<obj_item_wrench>(this); // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:11
        Dm.@new<obj_item_weldingtool>(this); // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:12
        Dm.@new<obj_item_crowbar>(this); // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:13
        Dm.@new<obj_item_analyzer>(this); // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:14
        Dm.@new<obj_item_wirecutters>(this); // tg: code/game/objects/items/storage/toolboxes/mechanical.dm:15
    }
}
