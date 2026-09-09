using HarmonyLib;
using JetBrains.Annotations;
using Jewelcrafting;
using UnityEngine;
using Logger = EpicJewels.Common.EJLogger;

namespace EpicJewels.GemEffects
{
    internal class WaterResistant
    {
        [PublicAPI]
        public struct Config
        {
            [InverseMultiplicativePercentagePower] public float Power;
        }

        private static float delayWetTill = 0;
        private static int wet_hash =  "Wet".GetStableHashCode();

        // Argument types must mirror SEMan.AddStatusEffect(int, bool, int, float, short) exactly.
        // Harmony picks the overload off the full parameter list, and 1.0.7 appended the optional
        // `short variant` that 0.221.12 did not have. A stale list resolves to no method at all and
        // throws out of PatchAll, taking every later patch in the mod down with it.
        [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect), typeof(int), typeof(bool), typeof(int), typeof(float), typeof(short))]
        public static class Waterproof_SEMan_AddStatusEffect_Patch
        {
            public static bool Prefix(SEMan __instance, int nameHash)
            {
                if (__instance.m_character.IsPlayer() && __instance.m_character is Player player && nameHash == wet_hash)
                {
                    // Waterproof, never gets wet
                    if (player.GetEffectPower<Waterproof.Config>("Waterproof").Power > 0) { return false; }
                    if (player.GetEffectPower<Config>("Water Resistant").Power > 0)
                    {
                        // Logger.LogInfo($"Water Resistance activated.");
                        if (delayWetTill == 0)
                        {
                            delayWetTill = Time.time + player.GetEffectPower<Config>("Water Resistant").Power;
                            // Logger.LogDebug($"Set water resistance timeout: {delayWetTill}");
                            return false;
                        }
                        if (delayWetTill < Time.time)
                        {
                            // Logger.LogDebug($"Water resistance is expired.");
                            // if the delay wet timer is more than a few seconds past, we should reset it and avoid being wet right now
                            // this happens when the player tried getting wet, but then stopped having the wet effect applied before the resistance wore off
                            // eg: the delay wet timer is from an old application of wetness
                            if ((delayWetTill + 2) < Time.time)
                            {
                                // Logger.LogDebug($"Reset water resistance.");
                                delayWetTill = 0;
                                return false;
                            }
                            // Reset since the character got wet
                            delayWetTill = 0;
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }
    }
}
