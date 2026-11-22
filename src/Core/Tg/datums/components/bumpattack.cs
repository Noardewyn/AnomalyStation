// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/bumpattack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file (the pillow suit's charge: worn in its slot, a bump into a mob attacks it with the suit's pillow,
// once per melee cooldown). The var block is generated (D111).

/**
 * Add an auto attack on bump behaviour to item
 * valid_inventory_slot: the inventory slot the item could be held in while still bumpattacking with it
 * proxy_weapon: the weapon that will gain this behaviour
 */
// tg: code/datums/components/bumpattack.dm:8
public partial class datum_component_bumpattack : datum_component
{
    // tg: code/datums/components/bumpattack.dm:6  #define COOLDOWN_BUMP_ATTACK "bump_attack_cooldown" (#undef'd at the file's end)
    private const string COOLDOWN_BUMP_ATTACK = "bump_attack_cooldown";

    // tg: code/datums/components/bumpattack.dm:21
    [DmParams("valid_inventory_slot", "proxy_weapon")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object valid_inventory_slot = Dm.arg(__extra, 0);
        obj_item proxy_weapon = Dm.arg(__extra, 1) as obj_item;
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        this.valid_inventory_slot = valid_inventory_slot;
        this.proxy_weapon = proxy_weapon;

        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/bumpattack.dm:29
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(check_equip));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(check_drop));
    }

    // tg: code/datums/components/bumpattack.dm:33
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));
    }

    // tg: code/datums/components/bumpattack.dm:36
    [SignalHandler]
    public virtual void check_equip(obj_item source = null, mob_living user = null, object slot = null)
    {
        if (user == null) // iunno, thoroughness
            return;
        if (Dm.truthy(Dm.bitand(slot, valid_inventory_slot)))
            activate(user);
        else
            deactivate();
    }

    // tg: code/datums/components/bumpattack.dm:45
    [SignalHandler]
    public virtual void check_drop(datum source = null, mob_living dropper = null)
    {
        deactivate();
    }

    // tg: code/datums/components/bumpattack.dm:49
    public virtual void activate(mob_living user = null)
    {
        if (user == null) // istype(user): the parameter's own type
            return;
        active = TRUE;
        wearer = user;
        RegisterSignal(user, COMSIG_LIVING_MOB_BUMP, nameof(check_bump));
    }

    // tg: code/datums/components/bumpattack.dm:56
    public virtual void deactivate()
    {
        active = FALSE;
        if (wearer != null)
            UnregisterSignal(wearer, COMSIG_LIVING_MOB_BUMP);
        wearer = null;
    }

    // tg: code/datums/components/bumpattack.dm:62
    [SignalHandler]
    public virtual void check_bump(mob_living bumper = null, mob_living target = null)
    {
        obj_item our_weapon = proxy_weapon ?? parent as obj_item;
        if (our_weapon == null) // istype(our_weapon): the local's own type
            throw Dm.CRASH(Dm.text("[] somehow failed istype", our_weapon), "code/datums/components/bumpattack.dm", 66);
        if (!Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_BUMP_ATTACK))) // TIMER_COOLDOWN_FINISHED
        {
            // TIMER_COOLDOWN_START(src, COOLDOWN_BUMP_ATTACK, attack_cooldown) (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
            LAZYSET(ref cooldowns, COOLDOWN_BUMP_ATTACK, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), this, COOLDOWN_BUMP_ATTACK), attack_cooldown, file: "code/datums/components/bumpattack.dm", line: 68));
            Waiting.Detach(target.attackby(our_weapon, bumper));
            bumper.visible_message(span_danger(I18n.T("[] charges into [], attacking with []!", bumper, target, our_weapon)), span_danger(I18n.T("You charge into [], attacking with []!", target, our_weapon)), vision_distance: COMBAT_MESSAGE_RANGE);
        }
    }
}
