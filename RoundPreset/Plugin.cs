using Aki.Reflection.Utils;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using IcyClawz.CustomInteractions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OutsiderH.RoundPreset
{
    using static Plugin;
    using AddItemEventArgs = GEventArgs2;
    using BaseItemEventArgs = GEventArgs1;
    using ItemJobResult = GStruct370;
    using ItemManager = GClass2672;
    using MagazinePtr = GClass2666;
    using MenuInventoryController = GClass2662;

    [BepInPlugin("outsiderh.roundpreset", "RoundPreset", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public ConfigEntry<bool> SaveButton { get; set; }
        public ConfigEntry<bool> LoadButton { get; set; }
        internal static ISession Session
        {
            get
            {
                _session ??= ClientAppUtils.GetMainApp().GetClientBackEndSession();
                return _session;
            }
        }
        private string SavesPath
        {
            get
            {
                _savesPath ??= $@".\BepInEx\plugins\RoundPreset\{Session.Profile.AccountId}.json";
                return _savesPath;
            }
        }
        internal static ManualLogSource internalLogger;
        internal static readonly Dictionary<string, string[]> localizeTable = new()
        {
            {"en", new[]{"Save round", "Load round", "This magazine is not full", "This magazine is not empty", "No preset found", "preset", "apply", "delete", "ammo not include", "Item Operation failed(local change)", "Item Operation failed(uploading)", "Save succeed", "Load succeed", "File not found"} },
            {"ch", new[]{"保存弹药预设", "加载弹药预设", "首先填充弹匣", "首先清空弹匣", "没有找到预设", "预设", "应用", "删除", "弹药不足", "物品操作错误(本地)", "物品操作错误(同步时)", "保存成功", "加载成功", "没有预设存档" } }
        };
        internal static Dictionary<MagazineKey, IList<IReadOnlyList<PresetAmmo>>> savedPresets = new();
        private static ISession _session;
        private string _savesPath;
        private void Awake()
        {
            SaveButton = Config.Bind("Save and load", "Save", false);
            LoadButton = Config.Bind("Save and load", "Load", false);
            SaveButton.SettingChanged += async (_sender, _e) =>
            {
                if (SaveButton.Value == false)
                {
                    return;
                }
                if (savedPresets.Count == 0)
                {
                    return;
                }
                JsonItem.Root root = savedPresets.ToJsonObject();
                string jsonStr = JsonConvert.SerializeObject(root, Formatting.Indented);
                internalLogger.LogMessage(jsonStr);
                if (File.Exists(SavesPath))
                {
                    File.Delete(SavesPath);
                }
                if (!Directory.Exists(@".\BepInEx\plugins\RoundPreset"))
                {
                    Directory.CreateDirectory(@".\BepInEx\plugins\RoundPreset");
                }
                File.Create(SavesPath).Close();
                StreamWriter sw = new(SavesPath);
                await sw.WriteAsync(jsonStr);
                sw.Close();
                NotificationManagerClass.DisplayMessageNotification(GetLocalizedString(ELocalizedStringIndex.FileSaveDone));
                SaveButton.Value = false;
            };
            LoadButton.SettingChanged += (_sender, _e) =>
            {
                if (LoadButton.Value == false)
                {
                    return;
                }
                if (!File.Exists(SavesPath))
                {
                    NotificationManagerClass.DisplayWarningNotification(GetLocalizedString(ELocalizedStringIndex.FileNotFound));
                    LoadButton.Value = false;
                    return;
                }
                StreamReader sr = new(SavesPath);
                JsonItem.Root root = JsonConvert.DeserializeObject<JsonItem.Root>(sr.ReadToEnd());
                sr.Close();
                savedPresets = root.ToOriginObject();
                NotificationManagerClass.DisplayMessageNotification(GetLocalizedString(ELocalizedStringIndex.FileLoadDone));
                LoadButton.Value = false;
            };
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
        internal static async Task WaitEventFinish(MenuInventoryController controller, int id)
        {
            bool finished = false;
            controller.ActiveEventsChanged += args =>
            {
                if (args.EventId == id)
                {
                    finished = true;
                }
            };
            while (!finished)
            {
                await Task.Delay(10);
            }
            return;
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
            NoAmmo = 8,
            OpFailClient = 9,
            OpFailServer = 10,
            FileSaveDone = 11,
            FileLoadDone = 12,
            FileNotFound = 13
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
                    Caption = () => GetLocalizedString(ELocalizedStringIndex.SaveRound),
                    Icon = () => StaticIcons.GetAttributeIcon(EItemAttributeId.CenterOfImpact),
                    Enabled = () => (mag.Count == mag.MaxCount),
                    Action = () =>
                    {
                        Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.ButtonClick);
                        IReadOnlyList<PresetAmmo> ammos = mag.Cartridges.Items.Select(val => new PresetAmmo(val.TemplateId, val.StackObjectsCount, val.LocalizedShortName())).ToList();
                        if (savedPresets.ContainsKey(key))
                        {
                            savedPresets[key].Add(ammos);
                        }
                        else
                        {
                            savedPresets.Add(key, new List<IReadOnlyList<PresetAmmo>> { ammos });
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
                    Error = () => GetLocalizedString(isEmpty ? ELocalizedStringIndex.PresetNotFound : ELocalizedStringIndex.MagNotEmpty)
                };
            }
        }
    }
    internal sealed class LoadPresetSubInteractions : CustomSubInteractions
    {
        public LoadPresetSubInteractions(ItemUiContext uiContext, MagazineClass mag, MagazineKey key) : base(uiContext)
        {
            foreach (IReadOnlyList<PresetAmmo> item in savedPresets[key])
            {
                Add(new CustomInteraction()
                {
                    Caption = () => new ClampedList(item).ToString(),
                    SubMenu = () => new LoadPresetSubSubInteraction(uiContext, mag, item, key)
                });
            }
        }
    }
    internal sealed class LoadPresetSubSubInteraction : CustomSubInteractions
    {
        public LoadPresetSubSubInteraction(ItemUiContext uiContext, MagazineClass mag, IReadOnlyList<PresetAmmo> preset, MagazineKey key) : base(uiContext)
        {
            IReadOnlyList<PresetAmmo> requireAmmos = preset.Merge();
            IEnumerable<Item> itemList = Session.Profile.Inventory.NonQuestItems.ToList();
            List<Item> availableAmmos = itemList.Where(val => val is BulletClass bullet && val.Parent.Container is not StackSlot && val.Parent.Container is not Slot && requireAmmos.Contains(val.TemplateId)).ToList();
            Add(new CustomInteraction()
            {
                Caption = () => GetLocalizedString(ELocalizedStringIndex.Apply),
                Icon = () => CustomInteractionsProvider.StaticIcons.GetAttributeIcon(EItemAttributeId.Caliber),
                Enabled = () => requireAmmos.GetRequire(availableAmmos),
                Action = async () =>
                {
                    Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.ButtonClick);
                    Queue<PresetAmmo> remainingTasks = new();
                    foreach (PresetAmmo item in preset)
                    {
                        remainingTasks.Enqueue(item);
                    }
                    PresetAmmo? currentTask = null;
                    do
                    {
                        if (!currentTask.HasValue)
                        {
                            currentTask = remainingTasks.Dequeue();
                        }
                        int indexWillApply = availableAmmos.FindIndex(val => val.TemplateId == currentTask.Value.id);
                        BulletClass ammoWillApply = availableAmmos[indexWillApply] as BulletClass;
                        int countWillApply = Math.Min(ammoWillApply.StackObjectsCount, currentTask.Value.count);
                        bool willMove = ammoWillApply.StackObjectsCount == countWillApply;
                        MenuInventoryController controller = ammoWillApply.Owner as MenuInventoryController;
                        ItemJobResult res;
                        if (mag.Count == 0 || mag.Cartridges.Last.Id != ammoWillApply.TemplateId)
                        {
                            MagazinePtr ptr = new(mag.Cartridges);
                            if (willMove)
                            {
                                res = ItemManager.Move(ammoWillApply, ptr, controller, true);
                            }
                            else
                            {
                                res = ItemManager.SplitExact(ammoWillApply, countWillApply, ptr, controller, controller, true);
                            }
                        }
                        else
                        {
                            if (willMove)
                            {
                                res = ItemManager.Merge(ammoWillApply, mag.Cartridges.Last, controller, true);
                            }
                            else
                            {
                                res = ItemManager.TransferExact(ammoWillApply, countWillApply, mag.Cartridges.Last, controller, true);
                            }
                        }
                        if (res.Failed)
                        {
                            NotificationManagerClass.DisplayWarningNotification(GetLocalizedString(ELocalizedStringIndex.OpFailClient));
                            break;
                        }
                        if (!controller.CanExecute(res.Value))
                        {
                            int? unfinishedEventId = ((List<BaseItemEventArgs>)AccessTools.Property(typeof(MenuInventoryController), "List_0").GetValue(controller)).Find(val => val is AddItemEventArgs val1 && val1.To.Container.ParentItem == mag)?.EventId;
                            if (unfinishedEventId != null)
                            {
                                await WaitEventFinish(controller, unfinishedEventId.Value);
                            }
                            else
                            {
                                NotificationManagerClass.DisplayWarningNotification(GetLocalizedString(ELocalizedStringIndex.OpFailServer));
                                return;
                            }
                        }
                        Task<IResult> task = controller.TryRunNetworkTransaction(res);
                        if (willMove)
                        {
                            availableAmmos.RemoveAt(indexWillApply);
                        }
                        PresetAmmo remainingCount = currentTask.Value;
                        if (remainingCount.count - countWillApply <= 0)
                        {
                            currentTask = null;
                        }
                        else
                        {
                            currentTask = new(currentTask.Value.id, currentTask.Value.count - countWillApply);
                        }
                        await task;
                    }
                    while (remainingTasks.Count > 0 || currentTask.HasValue);
                    Singleton<GUISounds>.Instance.PlayUILoadSound();
                },
                Error = () => GetLocalizedString(ELocalizedStringIndex.NoAmmo)
            });
            Add(new CustomInteraction()
            {

                Caption = () => GetLocalizedString(ELocalizedStringIndex.Delete),
                Icon = () => CustomInteractionsProvider.StaticIcons.GetAttributeIcon(EItemAttributeId.Caliber),
                Action = () =>
                {
                    Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.ButtonClick);
                    savedPresets[key].Remove(preset);
                    if (savedPresets[key].Count == 0)
                    {
                        savedPresets.Remove(key);
                    }
                }
            });
        }
    }
    internal static class ExtFunc
    {
        internal static IReadOnlyList<PresetAmmo> Merge(this IReadOnlyList<PresetAmmo> origin)
        {
            List<PresetAmmo> result = new();
            foreach (PresetAmmo item in origin)
            {
                int index = result.FindIndex(val => val.id == item.id);
                if (index == -1)
                {
                    result.Add(item);
                }
                else
                {
                    result[index] = new(result[index].id, result[index].count + item.count);
                }
            }
            return result;
        }
        internal static bool Contains(this IReadOnlyList<PresetAmmo> origin, string id)
        {
            foreach (PresetAmmo item in origin)
            {
                if (item.id == id)
                {
                    return true;
                }
            }
            return false;
        }
        internal static bool GetRequire(this IReadOnlyList<PresetAmmo> require, in List<Item> items)
        {
            Dictionary<string, int> requireRemaining = new();
            foreach (PresetAmmo item in require)
            {
                if (requireRemaining.ContainsKey(item.id))
                {
                    requireRemaining[item.id] += item.count;
                }
                else
                {
                    requireRemaining.Add(item.id, item.count);
                }
            }
            foreach (Item item in items)
            {
                requireRemaining[item.TemplateId] -= item.StackObjectsCount;
                foreach (var item2 in requireRemaining)
                {
                    if (item2.Value > 0)
                    {
                        break;
                    }
                    return true;
                }
            }
            return false;
        }
        internal static JsonItem.Root ToJsonObject(this Dictionary<MagazineKey, IList<IReadOnlyList<PresetAmmo>>> presets)
        {
            JsonItem.Root result = new()
            {
                AllPresets = new()
            };
            foreach(var uniqueMagPresets in presets)
            {
                JsonItem.PresetsItem presetsJson = new()
                {
                    Presets = new()
                };
                foreach (var preset in uniqueMagPresets.Value)
                {
                    JsonItem.PresetItem presetJson = new()
                    {
                        Chunks = new()
                    };
                    foreach(var chunk in preset)
                    {
                        presetJson.Chunks.Add(new(chunk.id, chunk.count, chunk.sName));
                    }
                    presetsJson.Presets.Add(presetJson);
                }
                result.AllPresets.Add(uniqueMagPresets.Key.ToString(), presetsJson);
            }
            return result;
        }
        internal static Dictionary<MagazineKey, IList<IReadOnlyList<PresetAmmo>>> ToOriginObject(this JsonItem.Root jsonObject)
        {
            Dictionary<MagazineKey, IList<IReadOnlyList<PresetAmmo>>> result = new();
            foreach (var uniqueMagPresetItem in jsonObject.AllPresets)
            {
                List<IReadOnlyList<PresetAmmo>> presets = new();
                foreach(var presetItem in uniqueMagPresetItem.Value.Presets)
                {
                    List<PresetAmmo> preset = new();
                    foreach(var chunkItem in presetItem.Chunks)
                    {
                        preset.Add(new(chunkItem.Id, chunkItem.Count, chunkItem.Name));
                    }
                    presets.Add(preset);
                }
                result.Add(new MagazineKey(uniqueMagPresetItem.Key), presets);
            }
            return result;
        }
    }
    internal struct MagazineKey
    {
        public string caliber;
        public int size;
        public override readonly string ToString()
        {
            return $"{caliber}({size})mag";
        }
        internal MagazineKey(string jsonStr)
        {
            int sizeStartIndex = -1;
            for (int i = 0; i < jsonStr.Length; i++)
            {
                if (jsonStr[i] == '(')
                {
                    caliber = jsonStr.Substring(0, i);
                    sizeStartIndex = i + 1;
                }
                else if (jsonStr[i] == ')')
                {
                    size = int.Parse(jsonStr.Substring(sizeStartIndex, i - sizeStartIndex));
                    break;
                }
            }
        }
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
        public string sName;
        public override readonly bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override readonly int GetHashCode()
        {
            return base.GetHashCode();
        }
        public static bool operator ==(PresetAmmo a, PresetAmmo b)
        {
            return a.id == b.id && a.count == b.count;
        }
        public static bool operator !=(PresetAmmo a, PresetAmmo b)
        {
            return a.id != b.id || a.count != b.count;
        }
        internal PresetAmmo(string id, int count, string sName = "")
        {
            this.id = id;
            this.count = count;
            this.sName = sName;
        }
    }
    internal struct ClampedList
    {
        public IReadOnlyList<PresetAmmo> list;
        public List<int[]> loops;
        public override readonly string ToString()
        {
            StringBuilder sb = new();
            for (int i = 0; i < list.Count; i++)
            {
                int[] loop = loops.Find(val => val[0] == i);
                if (loop != null)
                {
                    sb.Append("*(");
                    for (int j = 0; j < loop[1]; j++)
                    {
                        sb.Append($"{list[i + j].sName} x{list[i + j].count}|");
                    }
                    i += loop[1] - 1;
                    sb.Remove(sb.Length - 1, 1);
                    sb.Append(")");
                }
                else
                {
                    sb.Append($"{list[i].sName} x{list[i].count}|");
                }
            }
            if (sb[sb.Length - 1] == '|')
            {
                sb.Remove(sb.Length - 1, 1);
            }
            return sb.ToString();
        }
        internal ClampedList(IReadOnlyList<PresetAmmo> origin)
        {
            List<PresetAmmo> list = new();
            List<int[]> loops = new();
            List<PresetAmmo> chunk = new();
            bool inChunk = false;
            for (int i = 0; i < origin.Count; i++)
            {
                bool isLoop = false;
                for (int j = i; j < origin.Count; j++)
                {
                    if (!inChunk)
                    {
                        chunk.Add(origin[j]);
                        inChunk = true;
                        continue;
                    }
                    if (origin[j] == chunk.First())
                    {
                        if (origin.Count - j < chunk.Count)
                        {
                            break;
                        }
                        int loopCnt = 1;
                        while (j < origin.Count && origin.Count - j >= chunk.Count)
                        {
                            if (!origin.Skip(j).Take(chunk.Count).SequenceEqual(chunk))
                            {
                                break;
                            }
                            loopCnt++;
                            j += chunk.Count;
                        }
                        j--;
                        if (loopCnt > 1)
                        {
                            list.AddRange(chunk);
                            loops.Add(new[] { list.Count - chunk.Count, chunk.Count });
                            isLoop = true;
                            i = j;
                            break;
                        }
                        else
                        {
                            chunk.Add(origin[j + 1]);
                            continue;
                        }
                    }
                    chunk.Add(origin[j]);
                }
                if (!isLoop)
                {
                    list.Add(origin[i]);
                }
                chunk.Clear();
                inChunk = false;
            }
            this.list = list;
            this.loops = loops;
        }
    }
}
namespace OutsiderH.RoundPreset.JsonItem
{
    public class Root
    {
        public Dictionary<string, PresetsItem> AllPresets { get; set; }
    }
    public class PresetsItem
    {
        public List<PresetItem> Presets { get; set; }
    }
    public class PresetItem
    {
        public List<PresetChunkItem> Chunks { get; set; }
    }
    public class PresetChunkItem
    {
        public string Id { get; set; }
        public int Count { get; set; }
        public string Name { get; set; }
        [JsonConstructor]
        internal PresetChunkItem(string id, int count, string name)
        {
            Id = id;
            Count = count;
            Name = name;
        }
    }
}
