using System.Collections.Generic;
using BepInEx.Logging;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrakeLabs
{
    public class ItemLib
    {
        private void makeItem(string name, ItemConfig itemConfig, string prefab)
        {
            makeItem(name, itemConfig.Name, itemConfig.Description, prefab,
                new List<RequirementConfig>(itemConfig.Requirements), itemConfig.CraftingStation);
        }

        private void makeItem(ItemConfig itemConfig, GameObject prefab)
        {
            CustomItem customItem = new CustomItem(prefab, true, itemConfig);
            ItemManager.Instance.AddItem(customItem);
        }

        private void makeItem(string name, string gameName, string description, string prefab,
            List<RequirementConfig> requirements, string craftingStation = "piece_workbench")
        {
            ItemConfig itemConfig = new ItemConfig();
            itemConfig.Name = gameName;
            itemConfig.Description = description;
            itemConfig.CraftingStation = craftingStation;
            foreach (var requirement in requirements)
            {
                itemConfig.AddRequirement(requirement);
            }

            CustomItem customItem = new CustomItem(name, prefab, itemConfig);
            ItemManager.Instance.AddItem(customItem);
        }

        private void makePiece(string name, string gameName, string prefab)
        {
            PieceConfig pieceConfig = new PieceConfig
            {
                PieceTable = "Hammer", // Add to the Hammer build menu
                Category = "Public", // Optional category
                Enabled = true,
                Name = gameName
            };
            CustomPiece customPiece = new CustomPiece(name, prefab, pieceConfig);

            if (customPiece.Piece.GetComponentInChildren<Door>() != null)
            {
                Debug.Log($"Public Door version of piece {customPiece.Piece.name}");
                customPiece.Piece.GetComponentInChildren<Door>().m_checkGuardStone = false;
            }

            if (customPiece.Piece.GetComponentInChildren<Container>())
            {
                Debug.Log($"Public Door version of piece {customPiece.Piece.name}");
                customPiece.Piece.GetComponentInChildren<Container>().m_checkGuardStone = false;
            }

            PieceManager.Instance.AddPiece(customPiece);
        }

        public void addPublicPieces()
        {
            makePiece("piece_chest_wood_public", "Chest (public)", "piece_chest_wood");
            makePiece("piece_chest_public", "Reinforced Chest (public)", "piece_chest");
            makePiece("wood_door_public", "Wood Door (public)", "wood_door");
            makePiece("wood_gate_public", "Wood Gate (public)", "wood_gate");
        }
    }
}