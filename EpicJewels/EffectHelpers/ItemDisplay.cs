using EpicJewels.Common;
using EpicJewels.GemEffects;
using HarmonyLib;
using Jewelcrafting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Logger = EpicJewels.Common.EJLogger;

namespace EpicJewels.EffectHelpers
{
    
    public static class ItemDisplay
    {
        // Targets the static GetTooltip overload; the instance one is GetTooltip(int stackOverride),
        // so the full argument list is what tells them apart. It has to track the game exactly -
        // the trailing `bool appending` arrived in the 1.0.7 update. `appending` only suppresses
        // the description header for the recursive m_appendToolTip call, so nothing below changes:
        // that inner call passes a prefab ItemData that fails the IsItemEquiped check and returns early.
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
        public static class ItemToolTipDisplayEnhancer
        {
            public static List<ItemDrop.ItemData.ItemType> allowed_item_types = new() {
                ItemDrop.ItemData.ItemType.Attach_Atgeir,
                ItemDrop.ItemData.ItemType.Bow,
                ItemDrop.ItemData.ItemType.OneHandedWeapon,
                ItemDrop.ItemData.ItemType.TwoHandedWeapon,
                ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft
            };
            private static void Postfix(ItemDrop.ItemData item, ref String __result)
            {
                if (Player.m_localPlayer == null || Player.m_localPlayer.IsItemEquiped(item) == false || ValConfig.EnableItemTooltipDisplay.Value == false) { return; }
                if (!allowed_item_types.Contains(item.m_shared.m_itemType)) { return; }

                Player.m_localPlayer.GetSkills().GetRandomSkillRange(out var min, out var max, item.m_shared.m_skillType);
                float total_dmg = item.m_shared.m_damages.GetTotalDamage();
                Dictionary<String, DmgModDetails> pdm_active = DetermineCharacterDamageModifiers();

                float coinhoarder_power = Player.m_localPlayer.GetEffectPower<GemEffects.CoinHoarder.Config>("Coin Hoarder").Power;
                if (coinhoarder_power > 0) {
                    coinhoarder_power = CoinHoarder.CoinHoarderBonusCalc(Player.m_localPlayer);
                }
                float spellblade_power = Player.m_localPlayer.GetEffectPower<GemEffects.Spellsword.Config>("Spellsword").Power;

                List<String> entry_lines = __result.Split('\n').ToList();
                List<String> result_lines = new List<string>(entry_lines);
                for (int i = 0; i < entry_lines.Count; i++)
                {
                    // Skip all the short lines, we don't care about them
                    if (entry_lines[i].Length < 17) { continue; }

                    string line_desc = entry_lines[i].Split(':')[0].Trim();
                    //Logger.LogDebug($"ItemDescription {entry_lines[i]} | {line_desc}|");
                    switch (line_desc)
                    {
                        case "$inventory_damage":
                            break;
                        case "$inventory_fire":
                            if (coinhoarder_power > 0 || spellblade_power > 0) {
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_fire, min, max, 0f, coinhoarder_power, spellblade_power);
                            }
                            break;
                        case "$inventory_poison":
                            if (coinhoarder_power > 0 || spellblade_power > 0) {
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_poison, min, max, 0f, coinhoarder_power, spellblade_power);
                            }
                            break;
                        case "$inventory_frost":
                            if (coinhoarder_power > 0 || spellblade_power > 0)
                            {
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_frost, min, max, 0f, coinhoarder_power, spellblade_power);
                            }
                            break;
                        case "$inventory_blunt":
                            if (pdm_active.ContainsKey("Add Blunt Damage"))
                            {
                                pdm_active["Add Blunt Damage"].added = true;
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_blunt, min, max, pdm_active["Add Blunt Damage"].power, coinhoarder_power, spellblade_power);
                            } else if (coinhoarder_power > 0 || spellblade_power > 0) {
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_blunt, min, max, 0f, coinhoarder_power, spellblade_power);
                            }
                            break;
                        case "$inventory_slash":
                            if (pdm_active.ContainsKey("Add Slash Damage"))
                            {
                                pdm_active["Add Slash Damage"].added = true;
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_slash, min, max, pdm_active["Add Slash Damage"].power, coinhoarder_power, spellblade_power);
                            } else if (coinhoarder_power > 0 || spellblade_power > 0) {
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_slash, min, max, 0f, coinhoarder_power, spellblade_power);
                            }
                            break;
                        case "$inventory_pierce":
                            if (pdm_active.ContainsKey("Add Pierce Damage"))
                            {
                                pdm_active["Add Pierce Damage"].added = true;
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_pierce, min, max, pdm_active["Add Pierce Damage"].power, coinhoarder_power, spellblade_power);
                            } else if (coinhoarder_power > 0 || spellblade_power > 0) {
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_pierce, min, max, 0f, coinhoarder_power, spellblade_power);
                            }
                            break;
                        case "$inventory_lightning":
                            if (pdm_active.ContainsKey("Add Lightning Damage"))
                            {
                                pdm_active["Add Lightning Damage"].added = true;
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_lightning, min, max, pdm_active["Add Lightning Damage"].power, coinhoarder_power, spellblade_power);
                            } else if (coinhoarder_power > 0 || spellblade_power > 0) {
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_lightning, min, max, 0f, coinhoarder_power, spellblade_power);
                            }
                            break;
                        case "$inventory_spirit":
                            if (pdm_active.ContainsKey("Add Spirit Damage"))
                            {
                                pdm_active["Add Spirit Damage"].added = true;
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_spirit, min, max, pdm_active["Add Spirit Damage"].power, coinhoarder_power, spellblade_power);
                            } else if (coinhoarder_power > 0 || spellblade_power > 0) {
                                result_lines[i] = AddModifierExplainer(entry_lines[i], total_dmg, item.m_shared.m_damages.m_spirit, min, max, 0f, coinhoarder_power, spellblade_power);
                            }
                            break;
                    }
                }

                // Add damage values that provide extra bonuses, even if its not a default damage type to the weapon
                foreach (var entry in pdm_active)
                {
                    if (entry.Value.added == true) { continue; }

                    result_lines.Add(BuildModifierExplainer(entry.Value.localizedName, total_dmg, min, max, entry.Value.power));
                }
                // Return our modified version of the item tooltip
                string result_modified = string.Join("\n", result_lines.ToArray());
                //Logger.LogDebug($"ItemDescription {result_modified}");
                __result = result_modified;
            }
        }

        class DmgModDetails
        {
            public string localizedName { get; set; }
            public bool added { get; set; }
            public float power { get; set; }
        }


        private static Dictionary<String, DmgModDetails> DetermineCharacterDamageModifiers()
        {
            Dictionary<String, DmgModDetails> set_damage_modifiers = new Dictionary<String, DmgModDetails>();
            if (Player.m_localPlayer.GetEffectPower<GemEffects.AddSpiritDamage.Config>("Add Spirit Damage").Power > 0)
            {
                set_damage_modifiers.Add("Add Spirit Damage", new DmgModDetails() { added = false, localizedName = "$inventory_spirit", power = Player.m_localPlayer.GetEffectPower<GemEffects.AddSpiritDamage.Config>("Add Spirit Damage").Power });
            }
            if (Player.m_localPlayer.GetEffectPower<GemEffects.AddLightningDamage.Config>("Add Lightning Damage").Power > 0)
            {
                set_damage_modifiers.Add("Add Lightning Damage", new DmgModDetails() { added = false, localizedName = "$inventory_lightning", power = Player.m_localPlayer.GetEffectPower<GemEffects.AddLightningDamage.Config>("Add Lightning Damage").Power });
            }
            if (Player.m_localPlayer.GetEffectPower<GemEffects.AddPierceDamage.Config>("Add Pierce Damage").Power > 0)
            {
                set_damage_modifiers.Add("Add Pierce Damage", new DmgModDetails() { added = false, localizedName = "$inventory_pierce", power = Player.m_localPlayer.GetEffectPower<GemEffects.AddPierceDamage.Config>("Add Pierce Damage").Power });
            }
            if (Player.m_localPlayer.GetEffectPower<GemEffects.AddSlashDamage.Config>("Add Slash Damage").Power > 0)
            {
                set_damage_modifiers.Add("Add Slash Damage", new DmgModDetails() { added = false, localizedName = "$inventory_slash", power = Player.m_localPlayer.GetEffectPower<GemEffects.AddSlashDamage.Config>("Add Slash Damage").Power });
            }
            if (Player.m_localPlayer.GetEffectPower<GemEffects.AddBluntDamage.Config>("Add Blunt Damage").Power > 0)
            {
                set_damage_modifiers.Add("Add Blunt Damage", new DmgModDetails() { added = false, localizedName = "$inventory_blunt", power = Player.m_localPlayer.GetEffectPower<GemEffects.AddBluntDamage.Config>("Add Blunt Damage").Power });
            }
            return set_damage_modifiers;
        }

        private static string AddModifierExplainer(string current_line, float total_dmg, float m_dmg_value, float min, float max, float bonus_power, float coinhoarder_mult = 1f, float spellsword_mult = 0f) {
            // Logging in this function is incredibly noisey since it is ran constantly whenever someone is mousing over any item in their inventory.

            float bonus_dmg = total_dmg * (bonus_power / 100);
            float dmg_mult = coinhoarder_mult;
            string[] line_arr = current_line.Split(' ');
            // We rewrite line_arr[1] and line_arr[2] below; a damage line that does not split into at
            // least three tokens would throw out of a GetTooltip postfix and break the whole tooltip.
            if (line_arr.Length < 3) {
                return current_line;
            }
            //EJLogger.LogInfo($"Modifying ({current_line}) {line_arr[1]}");
            // Change the damage text color to the specified one
            Match match = Regex.Match(line_arr[1], @"(?<=>)(\d+)(?=<)");
            if (match.Success == false) {
                return current_line;
            }
            int dmg;
            bool parsed = int.TryParse(match.Value, out dmg);
            if (parsed == false) {
                return current_line; 
            }
            // 0 is sent in when the player does not have any levels of the effect
            if (dmg_mult == 0) { dmg_mult = 1f; }

            if (spellsword_mult > 0) {
                float eitr_cost = Player.m_localPlayer.GetEffectPower<GemEffects.EitrFused.Config>("Eitr Fused").Cost + 5;
                if (Player.m_localPlayer.HaveEitr(eitr_cost)) {
                    dmg_mult *= (1f + (spellsword_mult / 100f));
                }
            }

            //EJLogger.LogInfo($"dmg {dmg} bonus_dmg {bonus_dmg} dmg_mult {dmg_mult}");
            line_arr[1] = $"<color=purple>{((dmg + bonus_dmg) * dmg_mult).ToString("F1")}</color>";
            // Not sure if this will be more confusing or not, maybe just recoloring is enough
            // Add the sum of bonus damage
            line_arr[1] += $" <color=purple>[+{(bonus_dmg + (dmg_mult * dmg - dmg)).ToString("F1")}]</color>";

            line_arr[2] = $"<color=purple>({Mathf.RoundToInt((m_dmg_value + bonus_dmg + (dmg_mult * dmg - dmg)) * min)}-{Mathf.RoundToInt((m_dmg_value + bonus_dmg + (dmg_mult * dmg - dmg)) * max)})";
            //EJLogger.LogInfo($"Result {line_arr[1]}");
            return string.Join(" ", line_arr);
        }

        private static string BuildModifierExplainer(string dmg_localization, float total_dmg, float min, float max, float bonus_power)
        {
            float bonus_dmg = total_dmg * (bonus_power / 100);
            return $"{dmg_localization}: <color=purple>{(bonus_dmg).ToString("F1")}</color> <color=purple>({Mathf.RoundToInt((bonus_dmg) * min)}-{Mathf.RoundToInt((bonus_dmg) * max)})</color>";
        }
    }
}
