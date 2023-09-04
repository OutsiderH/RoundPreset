using BepInEx;
using EFT.InventoryLogic;
using EFT.UI;
using IcyClawz.CustomInteractions;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using BepInEx.Logging;
using System.Reflection;
using Comfort.Common;
using System.Diagnostics.Contracts;

namespace OutsiderH.RoundPreset
{
    using static Plugin;
    [BepInPlugin("outsiderh.roundpreset", "RoundPreset", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource internalLogger;
        internal static readonly Dictionary<string, string[]> localizeTable = new()
        {
            {"en", new[]{"Save round", "Load round"} },
            {"zh", new[]{"保存弹药配置", "加载弹药配置"} }
        };
        private void Awake()
        {
            internalLogger = Logger;
            CustomInteractionsManager.Register(new CustomInteractionsProvider());
        }
        internal enum ELocalizedStringIndex : int
        {
            SaveRound = 0,
            LoadRound = 1
        }
    }
    internal sealed class CustomInteractionsProvider : IItemCustomInteractionsProvider
    {
        internal static StaticIcons StaticIcons => EFTHardSettings.Instance.StaticIcons;
        public IEnumerable<CustomInteraction> GetCustomInteractions(ItemUiContext uiContext, EItemViewType viewType, Item item)
        {
            if (viewType != EItemViewType.Inventory)
            {
                yield break;
            }
            if (item is not MagazineClass mag)
            {
                yield break;
            }
            {
                yield return new CustomInteraction()
                {
                    Caption = () => "Save round",
                    Icon = () => StaticIcons.GetAttributeIcon(EItemAttributeId.CenterOfImpact),
                    Enabled = () => (mag.Count == mag.MaxCount),
                    Action = () =>
                    {
                        mag.Cartridges.Items.ExecuteForEach(val => internalLogger.LogMessage($"ID: {val.Id}, Name: {val.LocalizedShortName()}, Count:{val.StackObjectsCount}"));
                        /*uiContext.FindCompatibleAmmo(mag);
                         * (string AmmoID, int Count)
                         */
                    },
                    Error = () => "This magazine is not full"
                };
            }
        }
    }
}

//new[] { Color.red, new Color(1f, 0.41f, 0f), Color.yellow, Color.green, Color.cyan, Color.blue, new Color(0.58f, 0f, 0.82f), Color.black, Color.white }