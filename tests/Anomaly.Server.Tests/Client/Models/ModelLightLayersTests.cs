using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// The owner (2026-10-19): a comfy chair floating in space glowed white. A model's light layers are its Overlay and Emission materials, but not an
/// Unlit one nor the ordinary overlays the art also calls overlays (the armrest, a shadow, a suit storage unit's contents).
/// </summary>
public class ModelLightLayersTests
{
    [Theory]
    [InlineData("Light_Overlay_Original.006", true)]
    [InlineData("Door_Button_Mask_Emission.060", true)]
    [InlineData("Console_Overlay_Original_Atlas.007", true)]
    [InlineData("Overlay_suit_storage_classic_lights_closed", true)]
    [InlineData("Fire_Alarm_Overlay_Unlit_Original.003", false)]
    [InlineData("Comfy_Armrest_Overlay_Tint", false)]
    [InlineData("Wall_Healer_Shadow_Overlay.001", false)]
    [InlineData("Overlay_suit_storage_classic_helm", false)]
    [InlineData("Overlay_suit_storage_classic_suit", false)]
    [InlineData("Overlay_suit_storage_classic_storage", false)]
    [InlineData("Comfy_Upholstery_Tint", false)]
    [InlineData("", false)]
    public void OnlyTheLightMaterialsGlow(string material, bool light)
        => Assert.Equal(light, ModelLightLayers.Is(material));
}
