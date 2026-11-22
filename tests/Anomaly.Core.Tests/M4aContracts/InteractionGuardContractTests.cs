using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.M4aContracts;

/// <summary>The M4a contracts step's interaction guard API (Tg/Infrastructure/InertSeam.cs, D132, docs/ai/specs/interaction.md section 7).</summary>
public class InteractionGuardContractTests
{
    [Fact]
    public void TheGuardedProcsAreTgProcsOfTheChainOnce()
    {
        Assert.Equal(InertSeam.GuardedProcs.Count, InertSeam.GuardedProcs.Distinct().Count());
        // Every name is a DM proc name of tg's interaction chain with a slot (docs/ai/specs/generated/slots.tsv): spot checks of
        // the entry points the scout names (scope 2c) and of the DM spelling (attack, not the C# attack__proc).
        foreach (string name in new[] { "attack_hand", "pickup", "attack_self", "melee_attack_chain", "interact_with_atom", "item_interaction", "attackby", "equipped", "dropped", "throw_impact", "attack" })
            Assert.Contains(name, InertSeam.GuardedProcs);
        Assert.DoesNotContain("attack__proc", InertSeam.GuardedProcs);
        Assert.DoesNotContain("equipped__proc", InertSeam.GuardedProcs);
    }

    [Fact]
    public void TheKeybindingTableNamesTgsKeybindingTypes()
    {
        var types = new HashSet<Type>();
        for (int code = 1; code <= 10; code++)
        {
            Type t = ClientSeam.KeybindingType((byte)code);
            Assert.NotNull(t);
            Assert.True(typeof(datum_keybinding).IsAssignableFrom(t), t.Name);
            Assert.True(types.Add(t));
        }
        Assert.Null(ClientSeam.KeybindingType(0));
        Assert.Null(ClientSeam.KeybindingType(11)); // protocol version 37: 11 and 12 are the verbs Sleep and Succumb (D244), no keybinding type
        Assert.Null(ClientSeam.KeybindingType(12));
        Assert.Null(ClientSeam.KeybindingType(13));
        Assert.Equal(typeof(datum_keybinding_living_hold_throw_mode), ClientSeam.KeybindingType(8));
        Assert.Equal(typeof(datum_keybinding_mob_stop_pulling), ClientSeam.KeybindingType(9)); // protocol version 20 (pulling)
        Assert.Equal(typeof(datum_keybinding_living_resist), ClientSeam.KeybindingType(10)); // protocol version 37 (M8z)
    }
}
