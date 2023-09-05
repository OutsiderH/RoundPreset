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
using EFT;

namespace OutsiderH.RoundPreset
{
    using static Plugin;
    [BepInPlugin("outsiderh.roundpreset", "RoundPreset", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource internalLogger;
        internal static readonly Dictionary<string, string[]> localizeTable = new()
        {
            {"en", new[]{"Save round", "Load round", "This magazine is not full", "This magazine is not empty", "No preset found", "preset", "apply", "delete", "ammo not include" } },
            {"ch", new[]{"保存弹药预设", "加载弹药预设", "首先填充弹匣", "首先清空弹匣", "没有找到预设", "预设", "应用", "删除", "弹药不足"} }
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
            MagNotFull = 2,
            MagNotEmpty = 3,
            PresetNotFound = 4,
            Preset = 5,
            Apply = 6,
            Delete = 7,
            NoAmmo = 8
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
                    Icon = () => StaticIcons.GetAttributeIcon(EItemInfoButton.UnloadAmmo),
                    Enabled = () => (mag.Count == mag.MaxCount),
                    Action = () =>
                    {
                        (string caliber, int size) magSet = ((mag.FirstRealAmmo() as BulletClass).Caliber, mag.MaxCount);
                        IReadOnlyList<(string, int)> ammos = mag.Cartridges.Items.Select(val => (val.TemplateId, val.StackObjectsCount)).ToList();
                        if (savedPresets.ContainsKey(magSet))
                        {
                            savedPresets[magSet].Add(ammos);
                            internalLogger.LogMessage($"Add a preset to exist magazine template 'caliber: {magSet.caliber}, size: {magSet.size}'");
                            ammos.ExecuteForEach(((string id, int count) val) => internalLogger.LogMessage($"ammo id: {val.id}, ammo count: {val.count}"));
                        }
                        else
                        {
                            savedPresets.Add(magSet, new List<IReadOnlyList<(string, int)>> { ammos });
                            internalLogger.LogMessage($"Add new magazine template 'caliber: {magSet.caliber}, size: {magSet.size}' with preset");
                            ammos.ExecuteForEach(((string id, int count) val) => internalLogger.LogMessage($"ammo id: {val.id}, ammo count: {val.count}"));
                        }
                    },
                    Error = () => GetLocalizedString(ELocalizedStringIndex.MagNotFull)
                };
            }
            //{
            //    if (mag.Count == 0)
            //    {
            //        yield break;
            //    }
            //    (string, int) magSet = ((mag.FirstRealAmmo() as BulletClass).Caliber, mag.MaxCount);
            //    bool foundPresets = true;
            //    if (savedPresets.ContainsKey(magSet))
            //    {
            //        foundPresets = false;
            //    }
            //    yield return new CustomInteraction()
            //    {
            //        Caption = () => GetLocalizedString(ELocalizedStringIndex.LoadRound),
            //        Icon = () => StaticIcons.GetAttributeIcon(EItemInfoButton.LoadAmmo),
            //        Enabled = () => mag.Count == 0 && foundPresets,
            //        SubMenu = () => new LoadPresetSubInteractions(uiContext, magSet),
            //        Error = () => GetLocalizedString(mag.Count == 0 ? ELocalizedStringIndex.MagNotEmpty : ELocalizedStringIndex.PresetNotFound)
            //    };
            //}
        }
    }
    internal class LoadPresetSubInteractions : CustomSubInteractions
    {
        public LoadPresetSubInteractions(ItemUiContext uiContext, (string, int) magSet) : base(uiContext)
        {
            foreach (IReadOnlyList<(string id, int count)> item in savedPresets[magSet])
            {
                Add(new CustomInteraction()
                {
                    Caption = () => "",
                    SubMenu = () => new LoadPresetSubSubInteraction(uiContext, magSet, false)
                });
            }
        }
    }
    internal class LoadPresetSubSubInteraction : CustomSubInteractions
    {
        public LoadPresetSubSubInteraction(ItemUiContext uiContext, (string, int) magSet, bool haveItem) : base(uiContext)
        {
            Add(new CustomInteraction()
            {
                Caption = () => GetLocalizedString(ELocalizedStringIndex.Apply),
                Icon = () => CustomInteractionsProvider.StaticIcons.GetAttributeIcon(EItemInfoButton.LoadAmmo),
                Enabled = () => haveItem,
                Error = () => GetLocalizedString(ELocalizedStringIndex.NoAmmo)
            });
            Add(new CustomInteraction()
            {
                Caption = () => GetLocalizedString(ELocalizedStringIndex.Delete),
                Icon = () => CustomInteractionsProvider.StaticIcons.GetAttributeIcon(EItemInfoButton.Discard)
            });
        }
    }
    //internal static class Algorithm
    //{
    //    internal static bool ListIsLoop<T>(IReadOnlyList<T> list) where T : System.Tuple<string, int>
    //    {
    //        for (int i = 0; i < list.Count; i++)
    //        {

    //        }
    //    }
    //    internal static IReadOnlyList<T> ClampListLoop<T>(IReadOnlyList<T> list) where T : System.Tuple<string, int>
    //    {
    //        if (!ListIsLoop(list))
    //        {
    //            return list;
    //        }
    //        int[] dp = new int[list.Count];

    //    }
    //}
}

//new[] { Color.red, new Color(1f, 0.41f, 0f), Color.yellow, Color.green, Color.cyan, Color.blue, new Color(0.58f, 0f, 0.82f), Color.black, Color.white }