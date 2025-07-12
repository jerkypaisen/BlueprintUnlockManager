using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("BlueprintUnlockManager", "jerky", "2.0.0")]
    [Description("This plugin manages blueprint unlocks in Rust by limiting the number of learners per item and enforcing a queue system to rotate blueprint ownership.")]
    public class BlueprintUnlockManager : RustPlugin
    {
        private const string ConfigFileName = "BlueprintUnlockManager";
        private const string DataFileName = "BlueprintUnlockManagerQueue";
        private ConfigData config;
        private Dictionary<string, Queue<ulong>> itemQueues = new Dictionary<string, Queue<ulong>>();

        #region Config Classes
        public class ItemConfig
        {
            public string ShortName { get; set; }
            public int MaxLearners { get; set; }
        }

        public class ConfigData
        {
            public List<ItemConfig> Items { get; set; } = new List<ItemConfig>();
        }
        #endregion

        #region Oxide Hooks
        protected override void LoadDefaultConfig()
        {
            ConfigData defaultConfig = new ConfigData
            {
                Items = new List<ItemConfig>
                {
                    new ItemConfig { ShortName = "rifle.ak", MaxLearners = 2 },
                    new ItemConfig { ShortName = "smg.mp5", MaxLearners = 3 }
                }
            };
            Config.WriteObject(defaultConfig, true);
            PrintWarning("Default config generated.");
        }

        private void Init()
        {
            LoadConfigData();
            LoadQueueData();
            RegisterLang();

            // Check blueprint ownership for all online players at plugin initialization
            foreach (var player in BasePlayer.activePlayerList)
            {
                foreach (var item in config.Items)
                {
                    var shortName = item.ShortName;
                    var queue = GetQueue(shortName);

                    ItemDefinition def = ItemManager.FindItemDefinition(shortName);
                    if (def == null) continue;

                    if (HasPlayerBlueprint(player, def))
                    {
                        if (!queue.Contains(player.userID))
                        {
                            RemovePlayerBlueprint(player, def);
                            PrintToChatLang(player, "LostBlueprintQueueEnforcement", shortName);
                        }
                    }
                }
            }
        }

        private void RegisterLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LostBlueprintQueueEnforcement"] = "You have lost the blueprint for {0} (queue enforcement).",
                ["LostBlueprintQueueRotation"] = "You have lost the blueprint for {0} (queue rotation).",
                ["AddedToBlueprintQueue"] = "You have been added to the blueprint queue for {0}.",
                ["RemovedFromBlueprintQueue"] = "You have been removed from the blueprint queue for {0}.",
                ["UsageLearnBP"] = "Usage: /learnbp <shortname>",
                ["UsageUnlearnBP"] = "Usage: /unlearnbp <shortname>"
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LostBlueprintQueueEnforcement"] = "{0} のBlueprintの所有資格を失いました。",
                ["LostBlueprintQueueRotation"] = "{0} のBlueprintの所有資格を失いました。",
                ["AddedToBlueprintQueue"] = "{0} のBlueprintの所有資格を得ました。",
                ["RemovedFromBlueprintQueue"] = "{0} のBlueprintキューから削除されました。",
                ["UsageLearnBP"] = "使い方: /learnbp <shortname>",
                ["UsageUnlearnBP"] = "使い方: /unlearnbp <shortname>"
            }, this, "ja");
        }

        private void PrintToChatLang(BasePlayer player, string key, params object[] args)
        {
            string message = string.Format(lang.GetMessage(key, this, player.UserIDString), args);
            PrintToChat(player, message);
        }

        private void OnServerSave()
        {
            SaveQueueData();
        }

        private void Unload()
        {
            SaveQueueData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            foreach (var item in config.Items)
            {
                var shortName = item.ShortName;
                var queue = GetQueue(shortName);

                ItemDefinition def = ItemManager.FindItemDefinition(shortName);
                if (def == null) continue;

                if (HasPlayerBlueprint(player, def))
                {
                    if (!queue.Contains(player.userID))
                    {
                        RemovePlayerBlueprint(player, def);
                        PrintToChatLang(player, "LostBlueprintQueueEnforcement", shortName);
                    }
                }
            }
        }
        #endregion

        #region Queue Management
        private Queue<ulong> GetQueue(string shortName)
        {
            if (!itemQueues.ContainsKey(shortName))
                itemQueues[shortName] = new Queue<ulong>();
            return itemQueues[shortName];
        }

        // Blueprintを習得した時に呼び出す（OnItemActionで使用）
        private void TryAddToQueue(BasePlayer player, string shortName)
        {
            var itemConfig = config.Items.FirstOrDefault(i => i.ShortName == shortName);
            if (itemConfig == null) return;

            var queue = GetQueue(shortName);

            if (queue.Contains(player.userID))
            {
                // 既にキューにいる場合は何もしない
                return;
            }

            if (queue.Count >= itemConfig.MaxLearners)
            {
                // キュー満員なら先頭のプレイヤーを追い出す
                ulong removedUserID = queue.Dequeue();
                var removedPlayer = BasePlayer.FindByID(removedUserID);
                ItemDefinition def = ItemManager.FindItemDefinition(shortName);
                if (removedPlayer != null && def != null && HasPlayerBlueprint(removedPlayer, def))
                {
                    RemovePlayerBlueprint(removedPlayer, def);
                    PrintToChatLang(removedPlayer, "LostBlueprintQueueRotation", shortName);
                }
            }

            // 新しいプレイヤーをキューに追加
            queue.Enqueue(player.userID);
            PrintToChatLang(player, "AddedToBlueprintQueue", shortName);
            SaveQueueData();
        }

        // Blueprintを失った時に呼び出す（コマンドやイベントで使用可能）
        private void RemoveFromQueue(BasePlayer player, string shortName)
        {
            var queue = GetQueue(shortName);
            if (queue.Contains(player.userID))
            {
                var newQueue = new Queue<ulong>(queue.Where(id => id != player.userID));
                itemQueues[shortName] = newQueue;
                PrintToChatLang(player, "RemovedFromBlueprintQueue", shortName);
            }
        }
        #endregion

        #region Blueprint Helper
        private bool HasPlayerBlueprint(BasePlayer player, ItemDefinition def)
        {
            return player.PersistantPlayerInfo.unlockedItems.Contains(def.itemid);
        }

        private void RemovePlayerBlueprint(BasePlayer player, ItemDefinition def)
        {
            var persistantPlayerInfo = player.PersistantPlayerInfo;
            if (persistantPlayerInfo.unlockedItems.Contains(def.itemid))
            {
                persistantPlayerInfo.unlockedItems.Remove(def.itemid);
                player.PersistantPlayerInfo = persistantPlayerInfo;
                player.SendNetworkUpdateImmediate();
            }
        }
        #endregion

        #region Config/Data Load/Save
        private void LoadConfigData()
        {
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config?.Items == null || config.Items.Count == 0)
                {
                    PrintWarning("Config is empty, generating default config.");
                    LoadDefaultConfig();
                    config = Config.ReadObject<ConfigData>();
                }
            }
            catch
            {
                PrintWarning("Config error, generating default config.");
                LoadDefaultConfig();
                config = Config.ReadObject<ConfigData>();
            }
        }

        private void LoadQueueData()
        {
            var data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<ulong>>>(DataFileName);
            itemQueues.Clear();
            foreach (var kvp in data)
            {
                itemQueues[kvp.Key] = new Queue<ulong>(kvp.Value);
            }
        }

        private void SaveQueueData()
        {
            var data = itemQueues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
            Interface.Oxide.DataFileSystem.WriteObject(DataFileName, data);
        }
        #endregion

        #region ItemAction Hook
        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (action == "study" && item.IsBlueprint())
            {
                ItemDefinition blueprintTargetDef = item.blueprintTargetDef;
                string shortName = blueprintTargetDef.shortname;

                // 設定対象アイテムのみ処理
                var itemConfig = config.Items.FirstOrDefault(i => i.ShortName == shortName);
                if (itemConfig == null)
                    return null;

                TryAddToQueue(player, shortName);
            }

            return null;
        }
        #endregion

        #region Chat Command Example
        [ChatCommand("learnbp")]
        private void CmdLearnBP(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                PrintToChatLang(player, "UsageLearnBP");
                return;
            }
            var shortName = args[0];
            TryAddToQueue(player, shortName);
        }

        [ChatCommand("unlearnbp")]
        private void CmdUnlearnBP(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                PrintToChatLang(player, "UsageUnlearnBP");
                return;
            }
            var shortName = args[0];
            RemoveFromQueue(player, shortName);
        }
        #endregion
    }
}