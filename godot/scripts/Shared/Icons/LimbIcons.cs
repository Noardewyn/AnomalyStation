namespace Anomaly.Client.Shared.Icons;

// Engine-free and dependency-free: also compiled into tools/Anomaly.Tools.Icons, whose body-part mask table (the server's, DV95) reads
// these files, so the client's aim (BodyZoneAim, DV30) and the server's table know the same limb files.

/// <summary>Which of tg's icon files hold body-part pictures (bodypart.dm's get_limb_icon).</summary>
public static class LimbIcons
{
    /// <summary>tg's human body parts and every species' and augment's: a file, or a folder whose files under it all count.</summary>
    public static readonly string[] Roots = { "icons/mob/human/bodyparts.dmi", "icons/mob/human/bodyparts_greyscale.dmi", "icons/mob/human/species", "icons/mob/augmentation" };

    /// <summary>
    /// Whether an appearance's icon resource is one of tg's limb files: the file itself, or the base file of an icon made at run time
    /// (<c>iconops:FILE|...</c>, a husk's limb). A GAGS icon or any other file is not.
    /// </summary>
    public static bool IsLimbFile(string resource)
    {
        if (string.IsNullOrEmpty(resource))
            return false;
        const string ops = "iconops:";
        if (resource.StartsWith(ops, StringComparison.Ordinal))
        {
            int end = resource.IndexOf('|', ops.Length);
            resource = Uri.UnescapeDataString(end < 0 ? resource.Substring(ops.Length) : resource.Substring(ops.Length, end - ops.Length));
        }
        foreach (string root in Roots)
            if (resource == root || resource.StartsWith(root + "/", StringComparison.Ordinal))
                return true;
        return false;
    }
}
