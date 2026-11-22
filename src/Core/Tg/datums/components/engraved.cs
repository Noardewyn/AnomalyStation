// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/engraved.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the engraved wall, whole (the chisel's wall engraving). SSpersistence's saving and loading of the engravings is the
// persistence's (its load_wall_engravings and save_wall_engravings are not translated). The var block is generated (D111).

/**
 * # engraved component!
 *
 * component for walls that applies an engraved overlay and lets you examine it to read a story (+ art element yay)
 * new creations will get a high art value, cross round scrawlings will get a low one.
 * MUST be a component, though it doesn't look like it. SSPersistence demandeth
 */
// tg: code/datums/components/engraved.dm:8
public partial class datum_component_engraved : datum_component
{
    // tg: code/datums/components/engraved.dm:18
    [DmParams("engraved_description", "persistent_save", "story_value")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object engraved_description = Dm.arg(__extra, 0);
        object persistent_save = Dm.arg(__extra, 1);
        object story_value = Dm.arg(__extra, 2);
        object dot = await base.Initialize(__extra);
        if (!isclosedturf(parent))
            return COMPONENT_INCOMPATIBLE;
        turf_closed engraved_wall = (turf_closed)parent;

        this.engraved_description = engraved_description;
        this.persistent_save = persistent_save;
        this.story_value = story_value;

        float beauty_value = 0;
        float value = Dm.num(story_value);
        if (value == STORY_VALUE_KEY)
            beauty_value = 0; // what is this
        else if (value == STORY_VALUE_SHIT)
            beauty_value = rand(-50, 50); //Ugly or mediocre at best
        else if (value == STORY_VALUE_NONE)
            beauty_value = rand(0, 100); //No inherent value
        else if (value == STORY_VALUE_MEH)
            beauty_value = rand(100, 200); //Its an okay tale
        else if (value == STORY_VALUE_OKAY)
            beauty_value = rand(150, 300); //Average story! most things are like this
        else if (value == STORY_VALUE_AMAZING)
            beauty_value = rand(300, 600); //Really impactful stories, seeing a lost limb, losing a loved pet.
        else if (value == STORY_VALUE_LEGENDARY)
            beauty_value = rand(500, 800); //Almost always a good story! this is for memories you can barely ever get, killing megafauna, doing ultimate feats!

        await engraved_wall.AddElement(typeof(datum_element_art), beauty_value / ENGRAVING_BEAUTY_TO_ART_FACTOR);
        if (Dm.truthy(persistent_save))
            await engraved_wall.AddElement(typeof(datum_element_beauty), beauty_value);
        else
            await engraved_wall.AddElement(typeof(datum_element_beauty), beauty_value / ENGRAVING_PERSISTENCE_BEAUTY_LOSS_FACTOR); //Old age does them harm
        icon_state_append = rand(1, 2);
        //must be here to allow overlays to be updated
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        engraved_wall.update_appearance();
        return dot;
    }

    // tg: code/datums/components/engraved.dm:55
    public override object Destroy(float? force = null)
    {
        if (parent == null)
            return base.Destroy(force);
        parent.RemoveElement(typeof(datum_element_art));
        //must be here to allow overlays to be updated
        UnregisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS);
        if (!QDELING(parent))
        {
            atom parent_atom = (atom)parent;
            parent_atom.update_appearance();
        }
        return base.Destroy(force); //call this after since we null out the parent
    }

    // tg: code/datums/components/engraved.dm:66
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        //supporting component transfer means putting these here instead of initialize
        Dm.append(ref SSpersistence.wall_engravings, this);
        ADD_TRAIT(parent, TRAIT_NOT_ENGRAVABLE, ENGRAVED_TRAIT);
    }

    // tg: code/datums/components/engraved.dm:72
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_EXAMINE);
        //supporting component transfer means putting these here instead of destroy
        Dm.remove(ref SSpersistence.wall_engravings, this);
        REMOVE_TRAIT(parent, TRAIT_NOT_ENGRAVABLE, ENGRAVED_TRAIT);
    }

    /// Used to maintain the acid overlay on the parent [/atom].
    // tg: code/datums/components/engraved.dm:79
    [SignalHandler]
    public virtual void on_update_overlays(atom parent_atom = null, DmList overlays = null)
    {
        Dm.append(ref overlays, mutable_appearance(Dm.resource("icons/turf/wall_overlays.dmi"), Dm.text("engraving[]", icon_state_append)));
    }

    ///signal called on parent being examined
    // tg: code/datums/components/engraved.dm:85
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_boldnotice(Dm.text("[]", engraved_description)));
    }

    ///returns all the information SSpersistence needs in a list to load up this engraving on a future round!
    // tg: code/datums/components/engraved.dm:90
    public virtual DmList save_persistent()
    {
        DmList saved_data = Dm.list();
        saved_data["story"] = engraved_description;
        saved_data["story_value"] = story_value;

        return Dm.list(saved_data);
    }
}
