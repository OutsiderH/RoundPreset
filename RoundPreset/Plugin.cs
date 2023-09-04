using BepInEx;
using EFT.InventoryLogic;
using EFT.UI;
using IcyClawz.CustomInteractions;
using System.Collections.Generic;
using UnityEngine;

namespace OutsiderH.RoundPreset
{
    [BepInPlugin("outsiderh.roundpreset", "RoundPreset", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {

            CustomInteractionsManager.Register(new CustomInteractionsProvider(new[] { Color.red, new Color(1f, 0.41f, 0f), Color.yellow, Color.green, Color.cyan, Color.blue, new Color(0.58f, 0f, 0.82f), Color.black, Color.white }));
        }
    }
    internal sealed class CustomInteractionsProvider : IItemCustomInteractionsProvider
    {
        internal static StaticIcons StaticIcons => EFTHardSettings.Instance.StaticIcons;
        private readonly Color[] presetColors;
        public IEnumerable<CustomInteraction> GetCustomInteractions(ItemUiContext uiContext, EItemViewType viewType, Item item)
        {
            if (viewType != EItemViewType.Inventory)
            {
                yield break;
            }
            if (item is not MagazineClass)
            {
                yield break;
            }
            {
                yield return new CustomInteraction()
                {
                    Caption = () => "Save round",
                    Icon = () => StaticIcons.GetAttributeIcon(EItemAttributeId.Resource),
                    Enabled = () => (item as MagazineClass).Cartridges.Count != 0,
                    Action = () =>
                    {
                        
                    },
                    Error = () => "Their is no ammo inside this mag"
                };
            }
        }
        public CustomInteractionsProvider(Color[] presetColors)
        {
            this.presetColors = presetColors;
        }
    }
}
