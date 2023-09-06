using BepInEx;
using EFT.InventoryLogic;
using EFT.UI;
using IcyClawz.CustomInteractions;
using System.Collections.Generic;
using BepInEx.Logging;
using Comfort.Common;
using System.Linq;
using Aki.Reflection.Utils;
using EFT;

namespace OutsiderH.RoundPreset
{
    using static Plugin;
    [BepInPlugin("outsiderh.roundpreset", "RoundPreset", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ISession Session
        {
            get
            {
                _session ??= ClientAppUtils.GetMainApp().GetClientBackEndSession();
                return _session;
            }
        }
        internal static readonly Dictionary<string, string[]> localizeTable = new()
        {
            {"en", new[]{"Save round", "Load round", "This magazine is not full", "This magazine is not empty", "No preset found", "preset", "apply", "delete", "ammo not include" } },
            {"ch", new[]{"保存弹药预设", "加载弹药预设", "首先填充弹匣", "首先清空弹匣", "没有找到预设", "预设", "应用", "删除", "弹药不足"} }
        };
        internal static ManualLogSource internalLogger;
        internal static Dictionary<MagazineKey, IList<IReadOnlyList<PresetAmmo>>> savedPresets = new();
        private static ISession _session;
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
            MagazineKey key = new((Singleton<ItemFactory>.Instance.CreateItem(MongoID.Generate(), mag.Cartridges.Filters.First().Filter.First(), null) as BulletClass).Caliber, mag.MaxCount);
            {
                yield return new CustomInteraction()
                {
                    Caption = () => "test",
                    Icon = () => StaticIcons.GetAttributeIcon(EItemAttributeId.CenterOfImpact),
                    Action = () =>
                    {
                        
                    }
                };
            }
            {
                yield return new CustomInteraction()
                {
                    Caption = () => GetLocalizedString(ELocalizedStringIndex.SaveRound),
                    Icon = () => StaticIcons.GetAttributeIcon(EItemAttributeId.CenterOfImpact),
                    Enabled = () => (mag.Count == mag.MaxCount),
                    Action = () =>
                    {
                        IReadOnlyList<PresetAmmo> ammos = mag.Cartridges.Items.Select(val => new PresetAmmo(val.TemplateId, val.StackObjectsCount)).ToList();
                        if (savedPresets.ContainsKey(key))
                        {
                            savedPresets[key].Add(ammos);
                            internalLogger.LogMessage($"Add a preset to exist magazine template 'caliber: {key.caliber}, size: {key.size}'");
                            ammos.ExecuteForEach((PresetAmmo val) => internalLogger.LogMessage($"ammo id: {val.id}, ammo count: {val.count}"));
                        }
                        else
                        {
                            savedPresets.Add(key, new List<IReadOnlyList<PresetAmmo>> { ammos });
                            internalLogger.LogMessage($"Add new magazine template 'caliber: {key.caliber}, size: {key.size}' with preset");
                            ammos.ExecuteForEach((PresetAmmo val) => internalLogger.LogMessage($"ammo id: {val.id}, ammo count: {val.count}"));
                        }
                    },
                    Error = () => GetLocalizedString(ELocalizedStringIndex.MagNotFull)
                };
            }
            {
                bool isEmpty = mag.Count == 0;
                yield return new CustomInteraction()
                {
                    Caption = () => GetLocalizedString(ELocalizedStringIndex.LoadRound),
                    Icon = () => StaticIcons.GetAttributeIcon(EItemAttributeId.CenterOfImpact),
                    Enabled = () => isEmpty && savedPresets.ContainsKey(key),
                    SubMenu = () => new LoadPresetSubInteractions(uiContext, mag, key),
                    Error = () => GetLocalizedString(isEmpty ? ELocalizedStringIndex.MagNotEmpty : ELocalizedStringIndex.PresetNotFound)
                };
            }
        }
    }
    internal class LoadPresetSubInteractions : CustomSubInteractions
    {
        public LoadPresetSubInteractions(ItemUiContext uiContext, MagazineClass mag, MagazineKey key) : base(uiContext)
        {
            foreach (IReadOnlyList<PresetAmmo> item in savedPresets[key])
            {
                
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
    internal struct MagazineKey
    {
        public string caliber;
        public int size;
        internal MagazineKey(string caliber, int size)
        {
            this.caliber = caliber;
            this.size = size;
        }
    }
    internal struct PresetAmmo
    {
        public string id;
        public int count;
        internal PresetAmmo(string id, int count)
        {
            this.id = id;
            this.count = count;
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