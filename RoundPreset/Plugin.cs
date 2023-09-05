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
using System.Linq;

namespace OutsiderH.RoundPreset
{
    using static Plugin;
    [BepInPlugin("outsiderh.roundpreset", "RoundPreset", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource internalLogger;
        internal static readonly Dictionary<string, string[]> localizeTable = new()
        {
            {"en", new[]{"Save round", "Load round", "This magazine is not full" } },
            {"zh", new[]{"保存弹药预设", "加载弹药预设", "首先填充弹匣"} }
        };
        internal static Dictionary<(string caliber, int size), IList<IReadOnlyList<(string id, int count)>>> savedPresets = new();
        private void Awake()
        {
            internalLogger = Logger;
            CustomInteractionsManager.Register(new CustomInteractionsProvider());
        }
        internal static string GetLocalizedString(ELocalizedStringIndex index)
        {
            string gameLanguage = Singleton<SharedGameSettingsClass>.Instance?.Game?.Settings?.Language;
            if (gameLanguage == null || !localizeTable.ContainsKey(gameLanguage))
            {
                gameLanguage = "en";
            }
            return localizeTable[gameLanguage][(int)index];
        }
        internal enum ELocalizedStringIndex : int
        {
            SaveRound = 0,
            LoadRound = 1,
            MagNotFull = 2
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
                    Caption = () => GetLocalizedString(ELocalizedStringIndex.SaveRound),
                    Icon = () => StaticIcons.GetAttributeIcon(EItemAttributeId.CenterOfImpact),
                    Enabled = () => (mag.Count == mag.MaxCount),
                    Action = () =>
                    {
                        (string, int) magSet = ((mag.FirstRealAmmo() as BulletClass).Caliber, mag.MaxCount);
                        IReadOnlyList<(string, int)> ammos = mag.Cartridges.Items.Select(val => (val.TemplateId, val.StackObjectsCount)).ToList();
                        if (savedPresets.ContainsKey(magSet))
                        {
                            savedPresets[magSet].Add(ammos);
                            internalLogger.LogMessage($"Add a preset to exist magazine template 'caliber: {magSet.Item1}, size: {magSet.Item2}'");
                            ammos.ExecuteForEach(val => internalLogger.LogMessage($"ammo id: {val.Item1}, ammo count: {val.Item2}"));
                        }
                        else
                        {
                            savedPresets.Add(magSet, new List<IReadOnlyList<(string, int)>> { ammos });
                            internalLogger.LogMessage($"Add new magazine template 'caliber: {magSet.Item1}, size: {magSet.Item2}' with preset");
                            ammos.ExecuteForEach(val => internalLogger.LogMessage($"ammo id: {val.Item1}, ammo count: {val.Item2}"));
                        }
                        /*uiContext.FindCompatibleAmmo(mag);
                         * (string AmmoID, int Count)
                         */
                    },
                    Error = () => GetLocalizedString(ELocalizedStringIndex.MagNotFull)
                };
            }
        }
    }
}

//new[] { Color.red, new Color(1f, 0.41f, 0f), Color.yellow, Color.green, Color.cyan, Color.blue, new Color(0.58f, 0f, 0.82f), Color.black, Color.white }