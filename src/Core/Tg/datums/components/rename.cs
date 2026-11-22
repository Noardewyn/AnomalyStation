// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/rename.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298: the names and descriptions written with a pen). The var block is generated.

/**
	The rename component.

	This component is used to manage names and descriptions changed with the pen.

	Atoms can only have one instance of this component at a time.

	When a player renames or changes the description of an atom with a pen, this component gets applied to it.
	If a player resets the name and description, they will be reverted to their state before being changed and the component will be removed.
 */
// tg: code/datums/components/rename.dm:11
public partial class datum_component_rename : datum_component
{
    // tg: code/datums/components/rename.dm:22
    [DmParams("custom_name", "custom_desc")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        this.custom_name = Dm.arg(__extra, 0);
        this.custom_desc = Dm.arg(__extra, 1);
        apply_rename();
        ADD_TRAIT(parent, TRAIT_WAS_RENAMED, GetType());
        return null;
    }

    /**
		This proc will fire after the parent's name or desc is changed with a pen, which is trying to apply another rename component.
		Since the parent already has a rename component, it will remove the old one and apply the new one.
		The name and description changes will be merged or overwritten.
	*/
    // tg: code/datums/components/rename.dm:36
    [DmParams("new_comp", "i_am_original", "custom_name", "custom_desc")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        datum_component_rename new_comp = C as datum_component_rename;
        revert_rename();
        if (new_comp != null)
        {
            this.custom_name = new_comp.custom_name;
            this.custom_desc = new_comp.custom_desc;
        }
        else
        {
            this.custom_name = Dm.arg(__extra, 0);
            this.custom_desc = Dm.arg(__extra, 1);
        }
        apply_rename();
    }

    ///Saves the current name and description before changing them to the player's inputs.
    // tg: code/datums/components/rename.dm:47
    public virtual void apply_rename()
    {
        atom owner = parent as atom;
        original_name = owner.name;
        original_desc = owner.desc;
        owner.name = custom_name as string;
        owner.desc = custom_desc as string;
    }

    ///Reverts the name and description to the state before they were changed.
    // tg: code/datums/components/rename.dm:55
    public virtual void revert_rename()
    {
        atom owner = parent as atom;
        owner.name = original_name as string;
        owner.desc = original_desc as string;
    }

    // tg: code/datums/components/rename.dm:60
    public virtual void remove_component()
    {
        revert_rename();
        qdel(this);
    }

    // tg: code/datums/components/rename.dm:64
    public override object Destroy(float? force = null)
    {
        revert_rename();
        REMOVE_TRAIT(parent, TRAIT_WAS_RENAMED, GetType());
        return base.Destroy(force);
    }
}
