using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using UnityEngine;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;

namespace DrakeLabs
{
    [BepInPlugin(GUID, ModName, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class DrakeLabs : BaseUnityPlugin
    {
        public const string CompanyName = "DrakeMods";
        public const string ModName = "DrakeLabs";
        public const string Version = "0.1.1";
        public const string GUID = "com." + CompanyName + "." + ModName;
        public ConfigEntry<string> PublicPiecesConfig; // Config entry for public pieces list
        public static readonly char[] ConfigSeparator = { ',' }; // Separator for config entries

        public static AssetBundle PloamBundle;
        public static AssetBundle DrakeBundle;
        private readonly Harmony harmony = new Harmony("drakesmod.DrakeLabs");

        private readonly ItemLib _itemLib;
        
        public DrakeLabs()
        {
            _itemLib = new ItemLib();
        }

        public ItemLib ItemLib
        {
            get { return _itemLib; }
        }

        private void Awake()
        {
            addBundles();
            PublicPiecesConfig = Config.Bind(
                "General",
                "PublicPieces",
                "piece_chest_wood:Chest (public),piece_chest:Reinforced Chest (public),wood_door:Wood Door (public),wood_gate:Wood Gate (public)",
                new ConfigDescription(
                    "List of items to make public. Format: original_name:display_name, separated by commas.",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true })
            );
            PrefabManager.OnVanillaPrefabsAvailable += makeCustom;
            PrefabManager.OnVanillaPrefabsAvailable += ItemLib.addPublicPieces;
            harmony.PatchAll();
        }

        private void makeCustom()
        {
            GameObject ploam_prefab = LoadPrefab("Ploam", PloamBundle);
      
            var ploam = new CustomItem(ploam_prefab, fixReference: true);
            ItemManager.Instance.AddItem(ploam);

            GameObject floam_barrel = LoadPrefab("barell_floam", PloamBundle);
            GameObject gloam_barrel = LoadPrefab("barell_gloam", PloamBundle);
            GameObject ploam_barrel = LoadPrefab("barell_ploam", PloamBundle);
            GameObject ploam_ball = LoadPrefab("ploam_ball", PloamBundle);
            GameObject bandit_mask = LoadPrefab("BanditMask", PloamBundle);
            GameObject ball = LoadPrefab("ball", PloamBundle);
  //          GameObject boomstick = LoadPrefab("boomstick", PloamBundle);
            GameObject shirt = LoadPrefab("vestArmor", PloamBundle);
  //          GameObject legs = LoadPrefab("legArmor", PloamBundle);
    //        GameObject viking = LoadPrefab("piece_banner_viking", PloamBundle);

            var floamReq = new[] { new RequirementConfig("Ploam", 20) };
            var maskReq = new[] { new RequirementConfig("DeerHide", 5) };
            var boomstickReq = new[] { new RequirementConfig("Iron", 2) };
            var bowelReq = new[] { new RequirementConfig("Wood", 1) };


            addPiece("Floam Barrel", floam_barrel, floamReq);
            addPiece("Gloam Barrel", gloam_barrel, floamReq);
            addPiece("Ploam Barrel", ploam_barrel, floamReq);
            addPiece("Ploam Ball", ploam_ball, floamReq);
     //       addPiece("Flag", viking, floamReq);
            addItem("Bandit Mask", bandit_mask, maskReq);
      //      addItem("Shirt", shirt, maskReq);
     //       addItem("Legs", legs, maskReq);
           

        }

        private void addItem(string name, GameObject prefab, RequirementConfig[] requirements)
        {
            try
            {
                if (prefab == null)
                {
                    Debug.LogError("Unable to add item " + name);
                    return;
                }

                Debug.Log("Attempting custom item");

                var itemConfig = new ItemConfig();
                itemConfig.Requirements = requirements;
                itemConfig.CraftingStation = CraftingStations.Workbench;
                var item = new CustomItem(prefab, true, itemConfig);

                if (item != null)
                {
                    ZLog.Log("DrakeLabs added item " + prefab.name);
                    ItemManager.Instance.AddItem(item);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("DrakeLabs failed to add item " + prefab);
                Debug.LogError(ex);
            }
        }

        private void addBundles()
        {
            string folder = "DrakeMod-DrakeLabs";
            string assetBundlePath = "DrakeMod-DrakeLabs/Assets/ploam";
            string assetBundlePathDrake = "DrakeMod-DrakeLabs/Assets/drake2";
            Debug.Log($"Loading Assets skippity do ${assetBundlePath} ");
            PloamBundle = AssetUtils.LoadAssetBundle(assetBundlePath);
            if (PloamBundle == null)
            {
                Logger.LogError($"Failed to load AssetBundle from {assetBundlePath}");
            }
            Debug.Log($"Loading Assets drake2 skippity do ${assetBundlePathDrake} ");
            DrakeBundle = AssetUtils.LoadAssetBundle(assetBundlePathDrake);
            if (DrakeBundle == null)
            {
                Logger.LogError($"Failed to load AssetBundle from {assetBundlePath}");
            }
        }
      
        /*
        private void addStatue()
        {
            GameObject statue = LoadPrefab("Odin_Statue", box);
            Debug.Log("Attempting to add assets");
            if (statue != null)
            {
                var piece = new CustomPiece(box, "Odin_Statue", true, new PieceConfig
                {
                    Requirements = new[]
                    {
                        new RequirementConfig("Stone", 5, 0, true)
                    },
                    Name = "Stone Statue",
                    Category = PieceCategories.Building,
                    PieceTable = PieceTables.Hammer
                });
                PieceManager.Instance.AddPiece(piece);
                // PrefabManager.Instance.AddPrefab(wall);
            }
            else
            {
                Debug.LogError("Failed to load asset");
            }
        }
        */
        
        public static GameObject LoadPrefab(string prefabName, AssetBundle bundle)
        {
            if (bundle == null) return null;

            var prefab = bundle.LoadAsset<GameObject>(prefabName);
            if (prefab != null)
            {
                Debug.Log($"Successfully loaded prefab: {prefabName}");
            }
            else
            {
                Debug.LogError($"Failed to load prefab: {prefabName}");
            }

            return prefab;
        }

        private static void addPiece(string name,
            GameObject prefab,
            RequirementConfig[] reqs,
            string category = "Misc")
        {
            try
            {
                if (prefab == null)
                {
                    Debug.LogError("Unable to add piece " + name);
                    return;
                }

                Debug.Log("Attempting custom piece");
                CustomPiece customPiece = new CustomPiece(prefab, true, new PieceConfig
                {
                    PieceTable = "Hammer",
                    Category = category,
                    Requirements = reqs,
                    Name = name
                });
                if (customPiece != null)
                {
                    ZLog.Log("DrakeLabs added piece " + prefab.name);
                    PieceManager.Instance.AddPiece(customPiece);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("DrakeLabs failed to add piece " + prefab);
                Debug.LogError(ex);
            }
        }
    }
}