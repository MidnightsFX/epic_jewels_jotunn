using BepInEx;
using BepInEx.Logging;
using EpicJewels.Common;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EpicJewels
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency("org.bepinex.plugins.jewelcrafting", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("org.bepinex.plugins.crystallights", BepInDependency.DependencyFlags.SoftDependency)]
    internal class EpicJewels : BaseUnityPlugin
    {
        public const string PluginGUID = "MidnightsFX.EpicJewels";
        public const string PluginName = "EpicJewels";
        public const string PluginVersion = "1.1.2";

        public static ManualLogSource Log;

        internal static bool CrystalLightsLoaded = false;
        internal ValConfig cfg;
        internal static AssetBundle EmbeddedResourceBundle;
        internal static Harmony Harmony = new Harmony(PluginGUID);
        public static IDeserializer yamldeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        public static ISerializer yamlserializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).DisableAliases().Build();
        public static Material spiritCreature;

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        // public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        public void Awake() {
            Log = this.Logger;
            cfg = new ValConfig(Config);
            AddLocalizations();
            EmbeddedResourceBundle = LoadAssetBundle("EpicJewels.AssetsEmbedded.epicjewels");
            EJLogger.LogDebug("Logging embedded assets.");
            foreach (string asset_name in EmbeddedResourceBundle.GetAllAssetNames()) {
                EJLogger.LogDebug(asset_name);
            }
            EJLogger.LogInfo("Let the gems flow.");
            GemEffects.EffectList.AddGemEffects();
            GemResources.AddGems();
            


            Dictionary<string, BepInEx.BaseUnityPlugin> plugins = BepInExUtils.GetPlugins();
            if (plugins.Keys.Contains("org.bepinex.plugins.crystallights") || ValConfig.EnableCrystalLightsAlways.Value) {
                CrystalLightsLoaded = true;
                GemResources.AddAllCrystalLights();
                EJLogger.LogInfo("Epic Crystal Lights enabled.");
            }

            spiritCreature = EmbeddedResourceBundle.LoadAsset<Material>("spirit_animal_mat.mat");

            Assembly assembly = Assembly.GetExecutingAssembly();
            Harmony.PatchAll(assembly);

            ValConfig.cfg.SaveOnConfigSet = true;
            ValConfig.cfg.Save();
        }

        public static AssetBundle LoadAssetBundle(string bundleName)
        {
            var resourceAssembly = typeof(EpicJewels).Assembly;
            string text = null;
            AssetBundle result;
            try
            {
                text = resourceAssembly.GetManifestResourceNames().Single((string str) => str.EndsWith(bundleName));
            }
            catch (Exception) {}
            if (text == null) {
                EJLogger.LogError($"Could not find an embedded asset bundle matching '{bundleName}'.");
                return null;
            }
            using (Stream stream = resourceAssembly.GetManifestResourceStream(text))
            {
                result = AssetBundle.LoadFromStream(stream);
            }
            return result;
        }

        public static String LoadEmbeddedAssetToString(string assetName) 
        {
            var resourceAssembly = typeof(EpicJewels).Assembly;
            string text = null;
            string result;
            try
            {
                text = resourceAssembly.GetManifestResourceNames().Single((string str) => str.EndsWith(assetName));
            } catch (Exception) { }
            if (text == null) { return null; }
            using (Stream stream = resourceAssembly.GetManifestResourceStream(text))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    result = reader.ReadToEnd();
                }
            }
            return result;
        }

        private void AddLocalizations() {
            // Use this class to add your own localization to the game
            // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
            CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();
            // ValheimFortress.localizations.English.json
            // load all localization files within the localizations directory
            EJLogger.LogInfo("Loading Localizations.");
            foreach (string embeddedResouce in typeof(EpicJewels).Assembly.GetManifestResourceNames()) {
                if (!embeddedResouce.Contains("Localizations")) { continue; }
                // Read the localization file
                EJLogger.LogDebug($"Reading localization resource: {embeddedResouce}");
                string localization = ReadEmbeddedResourceFile(embeddedResouce);
                // since I use comments in the localization that are not valid JSON those need to be stripped
                string cleaned_localization = Regex.Replace(localization, @"\/\/.*", "");
                // Just the localization name
                var localization_name = embeddedResouce.Split('.');
                EJLogger.LogDebug($"Adding localization: {localization_name[2]}");
                Localization.AddJsonFile(localization_name[2], cleaned_localization);
            }
        }

        /// <summary>
        /// This reads an embedded file resouce name, these are all resouces packed into the DLL
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        internal static string ReadEmbeddedResourceFile(string filename) {
            using (var stream = typeof(EpicJewels).Assembly.GetManifestResourceStream(filename)) {
                using (var reader = new StreamReader(stream)) {
                    return reader.ReadToEnd();
                }
            }
        }


        // TODO: Remove once blaxxun either sets scaling size for the synergies box or enables text fit
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
        public static class EnableSynergyTextfit
        {
            [HarmonyPriority(Priority.Last)]
            public static void Postfix()
            {
                IEnumerable<GameObject> objects = Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj.name.StartsWith("JC_Synergies_Window"));
                EJLogger.LogDebug($"Found {objects.Count()} Synergy panels to update.");
                foreach (GameObject go in objects)
                {
                    // don't break crap if we can't modify a textbox
                    try
                    {
                        EJLogger.LogDebug($"Updating Synergy GO {go}");
                        go.transform.Find("Bkg/Left_Text/Left_Text_1").gameObject.GetComponent<Text>().resizeTextForBestFit = true;
                    }
                    catch (Exception) { }
                }
            }
        }
        
    }
}