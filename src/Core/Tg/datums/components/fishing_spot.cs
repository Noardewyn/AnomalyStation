// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/fishing_spot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: a thing you can fish in. A rod cast at it starts the fishing challenge with its fish source; it lists its fish to
// those who know them, gives loot to explosions, takes released fish back and links to a fish-porter by multitool. The var block
// is generated (D111).

// tg: code/datums/components/fishing_spot.dm:2
public partial class datum_component_fishing_spot : datum_component
{
    // tg: code/datums/components/fishing_spot.dm:7
    [DmParams("configuration")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object configuration = Dm.arg(__extra, 0);
        if (ispath(configuration, typeof(datum_fish_source)))
        {
            //Create new one of the given type
            fish_source = await Dm.new_async((Type)configuration) as datum_fish_source;
        }
        else if (istype(configuration, typeof(datum_fish_source)))
        {
            //Use passed in instance
            fish_source = (datum_fish_source)configuration;
        }
        else
            return COMPONENT_INCOMPATIBLE;
        fish_source.on_fishing_spot_init(this);
        RegisterSignal(parent, COMSIG_FISHING_ROD_CAST, nameof(handle_cast));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examined));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE_MORE, nameof(on_examined_more));
        RegisterSignal(parent, COMSIG_NPC_FISHING, nameof(return_fishing_spot));
        RegisterSignal(parent, COMSIG_ATOM_EX_ACT, nameof(explosive_fishing));
        RegisterSignal(parent, COMSIG_FISH_RELEASED_INTO, nameof(fish_released));
        RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_MULTITOOL), nameof(link_to_fish_porter));
        ADD_TRAIT(parent, TRAIT_FISHING_SPOT, REF(this));
        return null;
    }

    // tg: code/datums/components/fishing_spot.dm:26
    public override object Destroy(float? force = null)
    {
        REMOVE_TRAIT(parent, TRAIT_FISHING_SPOT, REF(this));
        fish_source.on_fishing_spot_del(this);
        fish_source = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/fishing_spot.dm:32
    [SignalHandler]
    public virtual object handle_cast(datum source = null, obj_item_fishing_rod rod = null, mob user = null)
    {
        if (Dm.truthy(try_start_fishing(rod, user)))
            return FISHING_ROD_CAST_HANDLED;
        return NONE;
    }

    ///If the fish source has fishes that are shown in the
    // tg: code/datums/components/fishing_spot.dm:39
    [SignalHandler]
    public virtual void on_examined(datum source = null, mob user = null, DmList examine_text = null)
    {
        if (!HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISHING_SPOT) || !Dm.truthy(fish_source.has_known_fishes(source as atom)))
            return;

        Dm.append(examine_text, span_tinynoticeital(I18n.T("This is a fishing spot. You can look again to list its fishes...")));
    }

    // tg: code/datums/components/fishing_spot.dm:45
    [SignalHandler]
    public virtual void on_examined_more(datum source = null, mob user = null, DmList examine_text = null)
    {
        if (!HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISHING_SPOT) || !Dm.truthy(fish_source.has_known_fishes(source as atom)))
            return;

        Waiting.NoSleep(fish_source.get_catchable_fish_names(user, parent as atom, examine_text));
    }

    // tg: code/datums/components/fishing_spot.dm:51
    [SignalHandler]
    public virtual object try_start_fishing(obj_item possibly_rod = null, mob user = null)
    {
        obj_item_fishing_rod rod = possibly_rod as obj_item_fishing_rod;
        if (rod == null)
            return null;
        if (Dm.truthy(GLOB.fishing_challenges_by_user[user]) || rod.fishing_line != null)
        {
            user.balloon_alert(user, I18n.T("already fishing"));
            return COMPONENT_NO_AFTERATTACK;
        }
        object denial_reason = fish_source.reason_we_cant_fish(rod, user, parent as atom);
        if (Dm.truthy(denial_reason))
        {
            to_chat(user, span_warning(Dm.str(denial_reason)));
            return COMPONENT_NO_AFTERATTACK;
        }
        // In case the fishing source has anything else to do before beginning to fish.
        fish_source.on_start_fishing(rod, user, parent as atom);
        datum_fishing_challenge challenge = Dm.@new<datum_fishing_challenge>(this, rod, user);
        fish_source.pre_challenge_started(rod, user, challenge);
        Waiting.NoSleep(challenge.start(user as mob_living));
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/datums/components/fishing_spot.dm:70
    public virtual void return_fishing_spot(datum source = null, DmList fish_spot_container = null)
    {
        fish_spot_container[NPC_FISHING_SPOT] = fish_source;
    }

    // tg: code/datums/components/fishing_spot.dm:73
    [SignalHandler]
    public virtual void explosive_fishing(atom location = null, object severity = null)
    {
        fish_source.spawn_reward_from_explosion(location, severity);
    }

    // tg: code/datums/components/fishing_spot.dm:77
    [SignalHandler]
    public virtual object link_to_fish_porter(atom source = null, mob user = null, obj_item_multitool tool = null)
    {
        if (istype(tool.buffer, typeof(obj_machinery_fishing_portal_generator)))
        {
            obj_machinery_fishing_portal_generator portal = (obj_machinery_fishing_portal_generator)tool.buffer;
            return portal.link_fishing_spot(fish_source, source, user as mob_living);
        }
        return null;
    }

    // tg: code/datums/components/fishing_spot.dm:83
    [SignalHandler]
    public virtual void fish_released(atom source = null, obj_item_fish fish = null, mob_living releaser = null)
    {
        Waiting.NoSleep(fish_source.readd_fish(source, fish, releaser));
    }
}
