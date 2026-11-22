// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/dehy_carp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3c, D316). The var blocks are generated (D111).

/*
 * Dehydrated Carp
 * Instant carp, just add water
 */

//Child of carpplushie because this should do everything the toy does and more
// tg: code/game/objects/items/dehy_carp.dm:7
public partial class obj_item_toy_plush_carpplushie_dehy_carp : obj_item_toy_plush_carpplushie
{
    //Attack self
    // tg: code/game/objects/items/dehy_carp.dm:15
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (owner != null)
            return await base.attack_self(user, modifiers, __extra);
        add_fingerprint(user);
        to_chat(user, span_notice(I18n.T("You pet []. You swear it looks up at you.", this)));
        owner = user;
        RegisterSignal(owner, COMSIG_QDELETING, nameof(owner_deleted));
        return null;
    }

    // tg: code/game/objects/items/dehy_carp.dm:23
    public virtual void Swell()
    {
        if (Dm.truthy(swelling))
            return;
        swelling = TRUE;
        desc = I18n.T("It's growing!");
        visible_message(span_notice(I18n.T("[] swells up!", this)));

        //Animation
        icon = Dm.resource("icons/mob/simple/carp.dmi");
        flick("carp_swell", this);
        //Wait for animation to end
        _addtimer(Dm.@new<datum_callback>(this, nameof(spawn_carp)), 0.6f * SECONDS, file: "code/game/objects/items/dehy_carp.dm", line: 34);
    }

    // tg: code/game/objects/items/dehy_carp.dm:36
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] starts eating []. It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        playsound(this, Dm.resource("sound/items/eatfood.ogg"), 50, TRUE);
        if (user == null)
            return BRUTELOSS;
        await user.Paralyze(3 * SECONDS);
        await forceMove(user); //we move it AWAAAYY
        await sleep(2 * SECONDS);
        if (QDELETED(this))
            return SHAME;
        if (!QDELETED(user))
        {
            await user.spawn_gibs();
            await user.apply_damage(200, def_zone: BODY_ZONE_CHEST);
            await forceMove(drop_location() as atom); //we move it back
        }
        swelling = TRUE;
        icon = Dm.resource("icons/mob/simple/carp.dmi");
        flick("carp_swell", this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(spawn_carp)), 0.6f * SECONDS, file: "code/game/objects/items/dehy_carp.dm", line: 55);
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/dehy_carp.dm:58
    public virtual async DmTask spawn_carp()
    {
        if (QDELETED(this)) //we got toasted while animating
            return;
        //Make space carp
        mob_living spawned_mob = await Dm.new_async(mobtype as Type, get_turf(this), owner) as mob_living;
        //Make carp non-hostile to user
        if (owner != null)
        {
            spawned_mob.set_allies(Dm.list(Dm.text("[]", REF(owner))));
            spawned_mob.grant_language(typeof(datum_language_common), UNDERSTOOD_LANGUAGE, LANGUAGE_ATOM);
        }
        foreach (mob_living viewer in Dm.each<mob_living>(viewers(5, get_turf(this))))
            to_chat(viewer, viewer == owner ? span_notice(I18n.T("The newly grown [] looks up at you with friendly eyes.", spawned_mob.name)) : span_warning(I18n.T("You have a bad feeling about this.")));
        qdel(this);
    }

    // tg: code/game/objects/items/dehy_carp.dm:71
    [SignalHandler]
    public virtual void owner_deleted(datum source = null)
    {
        UnregisterSignal(owner, COMSIG_QDELETING);
        owner = null;
    }
}
