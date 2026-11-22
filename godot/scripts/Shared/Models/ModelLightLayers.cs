using System;

namespace Anomaly.Client.Shared.Models;

/// <summary>
/// Which of a model's materials is a light layer, bright in the dark (a lamp's glow, tg's lighting_overlay; an alarm's or a console's light, its
/// emissive_appearance): the art names them Overlay or Emission (a button's light mask). Not one named Unlit (a fire alarm's first alert frame, which
/// tg's emissive mask leaves dark), and not the overlays tg draws as ordinary pictures that the art also calls overlays: a comfy chair's armrest
/// (chair.dm's GetArmrest), the wall healer's bar shadow (wall_healer.dm:188) and a suit storage unit's contents (suit_storage_unit.dm:238-243). The
/// owner (2026-10-19): a comfy chair floating in space glowed white.
/// </summary>
public static class ModelLightLayers
{
    private static readonly string[] Ordinary =
    {
        "Armrest", "Shadow", "suit_storage_classic_suit", "suit_storage_classic_helm", "suit_storage_classic_storage",
    };

    /// <summary>Whether the material named <paramref name="material"/> is a light layer.</summary>
    public static bool Is(string material)
    {
        if (string.IsNullOrEmpty(material))
            return false;
        if (!material.Contains("Overlay", StringComparison.Ordinal) && !material.Contains("Emission", StringComparison.Ordinal))
            return false;
        if (material.Contains("Unlit", StringComparison.Ordinal))
            return false;
        foreach (string word in Ordinary)
        {
            if (material.Contains(word, StringComparison.Ordinal))
                return false;
        }
        return true;
    }
}
