using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: the owner (2026-10-29). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>How strong each of the own mob's screen effects is, 0 to 1 (<see cref="ScreenEffects.Read"/>).</summary>
public readonly record struct ScreenEffectLevels(float Burning, float Heat, float Cold, float Toxic, float Radiation, float Shock);

/// <summary>
/// The owner (2026-10-29): what the first-person screen shows of the own mob's state, which tg shows on the body seen from above (the
/// flames, the electrocution's skeleton) or as an alert only. Read from what the server already sends: tg's alerts among the screen
/// entries (<c>fire</c>, <c>hot1</c>-<c>hot3</c>, <c>cold1</c>-<c>cold3</c>, <c>too_much_plas</c>, <c>too_much_co2</c>, the
/// <c>irradiated</c> overlay) and the own mob's <c>electrocuted_base</c> or <c>electrocuted_generic</c> overlay (human.dm electrocution_animation).
/// A port addition (D290, DV222).
/// </summary>
public static class ScreenEffects
{
    public static ScreenEffectLevels Read(ReplicaWorld replica)
    {
        float burning = 0, heat = 0, cold = 0, toxic = 0, radiation = 0, shock = 0;
        void See(string state)
        {
            switch (state)
            {
                case null:
                    return;
                case "fire":
                    burning = 1;
                    return;
                case "too_much_plas" or "too_much_co2":
                    toxic = 1;
                    return;
                case "irradiated":
                    radiation = 1;
                    return;
                case "electrocuted_base" or "electrocuted_generic":
                    shock = 1;
                    return;
            }
            // tg's throw_alert: the severity is appended to the state (hot1 to hot3)
            if (state.Length > 1 && state[^1] is >= '1' and <= '3')
            {
                float level = (state[^1] - '0') / 3f;
                if (state[..^1] == "hot")
                    heat = Math.Max(heat, level);
                else if (state[..^1] == "cold")
                    cold = Math.Max(cold, level);
            }
        }
        void SeeAppearance(TableRef reference)
        {
            if (!replica.TryGetAppearance(reference, out AppearanceEntry entry))
                return;
            See(replica.ResolveString(entry.IconState));
            foreach (TableRef overlay in entry.Overlays)
            {
                if (replica.TryGetAppearance(overlay, out AppearanceEntry o))
                    See(replica.ResolveString(o.IconState));
            }
        }
        foreach (ScreenEntry entry in replica.Screen.Values)
            SeeAppearance(entry.Appearance);
        if (replica.OwnEntity != 0 && replica.TryGet(replica.OwnEntity, out ReplicaEntity mob))
            SeeAppearance(mob.State.Appearance);
        return new ScreenEffectLevels(burning, heat, cold, toxic, radiation, shock);
    }
}
