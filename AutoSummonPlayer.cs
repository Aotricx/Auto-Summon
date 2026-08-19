// ============================================================================
// File: AutoSummonPlayer.cs
// Description: Per-player data management for the AutoSummon mod.
//              Handles save/load of panel configurations, auto-summon on
//              respawn/world entry, and sentry refresh when off-screen.
// ============================================================================
using System.Collections.Generic;
using AutoSummon.UI;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AutoSummon
{
    /// <summary>
    /// ModPlayer class that stores per-player data for the AutoSummon mod.
    /// Handles saving/loading panel configurations and auto-summon behavior.
    /// </summary>
    /// <remarks>
    /// This class is used in the following locations:
    /// - AutoSummonSystem.cs line: 27 (GetModPlayer to access player data)
    /// - AutoSummonSystem.cs line: 30-32 (checks tempSummonDisabled flag)
    /// - DraggableUIPanel.cs line: 256 (GetModPlayer in shouldSummon)
    /// - DraggableUIPanel.cs line: 483 (TriggerSave static method)
    /// </remarks>
    public class AutoSummonPlayer : ModPlayer
    {
        /// <summary>
        /// Flag indicating if the player is currently spawning.
        /// </summary>
        /// <remarks>
        /// Set to true in PlayerConnect() line: 359
        /// </remarks>
        public bool isSpawning;

        /// <summary>
        /// Master flag for auto-summoning behavior (not currently used in code).
        /// </summary>
        public bool autoSummonEnabled = true;

        /// <summary>
        /// Temporary flag to disable summoning (e.g., during certain game states).
        /// </summary>
        /// <remarks>
        /// Used in:
        /// - AutoSummonSystem.cs line: 30, 32 (checked and reset in PostUpdateEverything)
        /// - DraggableUIPanel.cs line: 261, 277 (checked and reset in shouldSummon)
        /// </remarks>
        public bool tempSummonDisabled = false;

        /// <summary>
        /// Flag synced with UI toggle for respawning sentries when off-screen.
        /// </summary>
        public bool respawnSentriesEnabled = DraggableUIPanel.respawnSentriesEnabled;

        /// <summary>
        /// Saved minion panel data (persisted with player file using TagCompound).
        /// </summary>
        private List<SavedItemData> savedMinionPanels = new();

        /// <summary>
        /// Saved sentry panel data (persisted with player file using TagCompound).
        /// </summary>
        private List<SavedItemData> savedSentryPanels = new();

        /// <summary>
        /// Called when the player is initialized. Clears all data fields.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Creates new empty lists for savedMinionPanels and savedSentryPanels
        /// </remarks>
        public override void Initialize()
        {
            savedMinionPanels = new();
            savedSentryPanels = new();
        }

        /// <summary>
        /// Saves player's panel configuration data to the player file.
        /// </summary>
        /// <param name="tag">The TagCompound to save data to.</param>
        /// <remarks>
        /// This function:
        /// - Only collects panel data if this is the local player
        /// - Saves minion panels with item data (using ItemIO) and quantity
        /// - Saves sentry panels with item data (using ItemIO) and quantity
        /// - Data is stored in "minionPanels" and "sentryPanels" keys
        ///
        /// Called automatically by tModLoader when saving player data.
        /// </remarks>
        public override void SaveData(TagCompound tag)
        {
            // Only collect panel data if this is the local player currently in the world
            // This prevents saving another player's UI data to this player's file
            if (Player.whoAmI == Main.myPlayer && Main.LocalPlayer == Player)
            {
                CollectPanelData();
            }

            // Save minion panels
            var minionList = new List<TagCompound>();
            foreach (var panelData in savedMinionPanels)
            {
                var itemTag = new TagCompound();
                if (panelData.Item != null && !panelData.Item.IsAir)
                {
                    // Use ItemIO to save the complete item (including prefix and mod data)
                    itemTag.Add("item", ItemIO.Save(panelData.Item));
                    itemTag.Add("quantity", panelData.Quantity);
                    minionList.Add(itemTag);
                }
            }
            tag["minionPanels"] = minionList;

            // Save sentry panels
            var sentryList = new List<TagCompound>();
            foreach (var panelData in savedSentryPanels)
            {
                var itemTag = new TagCompound();
                if (panelData.Item != null && !panelData.Item.IsAir)
                {
                    // Use ItemIO to save the complete item (including prefix and mod data)
                    itemTag.Add("item", ItemIO.Save(panelData.Item));
                    itemTag.Add("quantity", panelData.Quantity);
                    sentryList.Add(itemTag);
                }
            }
            tag["sentryPanels"] = sentryList;
        }

        /// <summary>
        /// Loads player's panel configuration data from the player file.
        /// </summary>
        /// <param name="tag">The TagCompound containing saved data.</param>
        /// <remarks>
        /// This function:
        /// - Clears existing savedMinionPanels and savedSentryPanels
        /// - Loads minion panels from "minionPanels" key (using ItemIO)
        /// - Loads sentry panels from "sentryPanels" key (using ItemIO)
        /// - Only loads valid (non-air) items
        ///
        /// Called automatically by tModLoader when loading player data.
        /// </remarks>
        public override void LoadData(TagCompound tag)
        {
            savedMinionPanels.Clear();
            savedSentryPanels.Clear();

            // Load minion panels
            if (tag.ContainsKey("minionPanels"))
            {
                var minionList = tag.GetList<TagCompound>("minionPanels");
                foreach (var itemTag in minionList)
                {
                    if (itemTag.ContainsKey("item"))
                    {
                        var item = ItemIO.Load(itemTag.GetCompound("item"));
                        int quantity = itemTag.GetInt("quantity");
                        if (item != null && !item.IsAir)
                        {
                            savedMinionPanels.Add(new SavedItemData { Item = item, Quantity = quantity });
                        }
                    }
                }
            }

            // Load sentry panels
            if (tag.ContainsKey("sentryPanels"))
            {
                var sentryList = tag.GetList<TagCompound>("sentryPanels");
                foreach (var itemTag in sentryList)
                {
                    if (itemTag.ContainsKey("item"))
                    {
                        var item = ItemIO.Load(itemTag.GetCompound("item"));
                        int quantity = itemTag.GetInt("quantity");
                        if (item != null && !item.IsAir)
                        {
                            savedSentryPanels.Add(new SavedItemData { Item = item, Quantity = quantity });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Collects current panel data from the UI and stores it for saving.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Leaves savedMinionPanels/savedSentryPanels untouched if the UI panel instance
        ///   isn't available yet, so a transient null never wipes out previously-saved data
        /// - Iterates through all interactionPanels (minions) and sentryPanels
        /// - For each panel with a valid item, extracts the item and quantity
        /// - Clones items to preserve all data (prefix, mod data, etc.)
        ///
        /// Called from:
        /// - SaveData() in this file line: 141 (when saving player data)
        /// - SavePanelDataNow() in this file line: 271 (when triggered manually)
        /// - PlayerDisconnect() in this file line: 347 (before disconnecting)
        /// </remarks>
        private void CollectPanelData()
        {
            var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
            if (draggableUIPanel == null)
                return; // UI isn't available - keep whatever was last saved/loaded instead of erasing it

            savedMinionPanels.Clear();
            savedSentryPanels.Clear();

            // Collect minion panel data
            foreach (var panel in draggableUIPanel.interactionPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data?.ItemSlot?.Item != null && !data.ItemSlot.Item.IsAir)
                {
                    int quantity = GetQuantityFromPanel(panel);
                    // Clone the item to preserve all data
                    savedMinionPanels.Add(new SavedItemData
                    {
                        Item = data.ItemSlot.Item.Clone(),
                        Quantity = quantity
                    });
                }
            }

            // Collect sentry panel data
            foreach (var panel in draggableUIPanel.sentryPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data?.ItemSlot?.Item != null && !data.ItemSlot.Item.IsAir)
                {
                    int quantity = GetQuantityFromPanel(panel);
                    // Clone the item to preserve all data
                    savedSentryPanels.Add(new SavedItemData
                    {
                        Item = data.ItemSlot.Item.Clone(),
                        Quantity = quantity
                    });
                }
            }
        }

        /// <summary>
        /// Saves panel data immediately when called.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Only runs for the local player (Player.whoAmI == Main.myPlayer)
        /// - Calls CollectPanelData() to gather current UI state
        ///
        /// Called from:
        /// - TriggerSave() in this file line: 291 (static helper)
        /// </remarks>
        public void SavePanelDataNow()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            CollectPanelData();
        }

        /// <summary>
        /// Static helper to save panel data for the local player.
        /// Call this from UI code when slots change.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Gets the local player's AutoSummonPlayer instance
        /// - Calls SavePanelDataNow() if the player exists
        ///
        /// Called from:
        /// - DraggableUIPanel.cs line: 483 (RefreshSummons - after any slot changes)
        /// </remarks>
        public static void TriggerSave()
        {
            var player = Main.LocalPlayer?.GetModPlayer<AutoSummonPlayer>();
            player?.SavePanelDataNow();
        }

        /// <summary>
        /// Extracts the quantity value from a panel's quantity label.
        /// </summary>
        /// <param name="panel">The UI panel containing the quantity label.</param>
        /// <returns>The quantity as an integer, or 0 if parsing fails.</returns>
        /// <remarks>
        /// This function:
        /// - Gets the InteractionPanelData from the panel
        /// - Parses the quantity from the label text (removes "Minions: " or "Sentries: " prefix)
        ///
        /// Called from:
        /// - CollectPanelData() in this file line: 254, 268 (when collecting panel data)
        /// </remarks>
        private int GetQuantityFromPanel(UIPanel panel)
        {
            var data = panel.GetTag<InteractionPanelData>();
            if (data?.QuantityLabel != null)
            {
                string text = data.QuantityLabel.Text.Replace("Minions: ", "").Replace("Sentries: ", "");
                if (int.TryParse(text, out int quantity))
                {
                    return quantity;
                }
            }
            return 0;
        }

        /// <summary>
        /// Called when the player enters a world. Sets up UI and triggers auto-summon.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Hides the UI panel on world enter
        /// - Loads saved panel data into the UI
        /// - Creates default panels if none exist after loading
        /// - Recalculates panel positions
        /// - Auto-summons if AutoSummonOnWorldEnter config is enabled
        ///
        /// Called automatically by tModLoader when entering a world.
        /// </remarks>
        public override void OnEnterWorld()
        {
            base.OnEnterWorld();

            // Hide the UI on world enter
            AutoSummon.Instance?.HideUI();

            var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
            if (draggableUIPanel == null)
            {
                return;
            }

            // Load saved data into UI panels
            var config = ModContent.GetInstance<AutoSummonConfig>();
            LoadPanelsFromSavedData();

            // Only create default panels if no panels exist after loading
            if (draggableUIPanel.interactionPanels.Count == 0)
            {
                draggableUIPanel.CreateInteractionPanel();
            }

            if (draggableUIPanel.sentryPanels.Count == 0)
            {
                draggableUIPanel.CreateSentryPanel();
            }

            // Recalculate positions after all panels are created
            draggableUIPanel.RecalculateSentryPanelPositions();
            draggableUIPanel.UpdateMainPanelHeight();

            // Auto-summon on world enter if enabled
            if (config.AutoSummonOnWorldEnter)
            {
                SummonAllItemsIfPossible(Player);
            }
        }

        /// <summary>
        /// Called when the player disconnects. Saves panel data before disconnect.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Only runs for the local player (same identity check as SaveData - PlayerDisconnect
        ///   fires on the server and on remote clients, so whoAmI alone isn't a reliable enough
        ///   check for "this is actually my own character")
        /// - Calls CollectPanelData() to ensure data is saved before disconnecting
        ///
        /// Called automatically by tModLoader when player disconnects.
        /// </remarks>
        public override void PlayerDisconnect()
        {
            base.PlayerDisconnect();

            // Collect panel data before disconnecting so it's saved properly
            if (Player.whoAmI == Main.myPlayer && Main.LocalPlayer == Player)
            {
                CollectPanelData();
            }
        }

        /// <summary>
        /// Loads saved panel data into the UI panels.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Clears all existing UI panels
        /// - Creates new minion panels from savedMinionPanels data
        /// - Creates new sentry panels from savedSentryPanels data
        /// - Clones items to prevent reference issues
        ///
        /// Called from:
        /// - OnEnterWorld() in this file line: 385 (when entering a world)
        /// </remarks>
        private void LoadPanelsFromSavedData()
        {
            var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
            if (draggableUIPanel == null)
                return;

            draggableUIPanel.ClearPanels();

            // Load minion panels from saved data
            foreach (var panelData in savedMinionPanels)
            {
                if (panelData.Item != null && !panelData.Item.IsAir)
                {
                    draggableUIPanel.CreateInteractionPanel(
                        draggableUIPanel.interactionPanels.Count,
                        panelData.Item.Clone(),
                        panelData.Quantity
                    );
                }
            }

            // Load sentry panels from saved data
            foreach (var panelData in savedSentryPanels)
            {
                if (panelData.Item != null && !panelData.Item.IsAir)
                {
                    draggableUIPanel.CreateSentryPanel(
                        draggableUIPanel.sentryPanels.Count,
                        panelData.Item.Clone(),
                        panelData.Quantity
                    );
                }
            }
        }

        /// <summary>
        /// Data class to store item and quantity for save/load operations.
        /// </summary>
        public class SavedItemData
        {
            /// <summary>The item stored in the panel slot.</summary>
            public Item Item { get; set; }
            /// <summary>The quantity configured for this item.</summary>
            public int Quantity { get; set; }
        }

        /// <summary>
        /// Called every frame after all other updates. Handles sentry respawn and respawn waiting.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Checks if respawnSentriesEnabled is on, returns early if not
        /// - Iterates through all active sentries owned by the local player
        /// - If a sentry is off-screen, refreshes it with a 2-second cooldown (120 ticks)
        /// - Checks if waiting for respawn and triggers SummonAllItemsIfPossible when player is alive
        ///
        /// Called automatically by tModLoader every frame.
        /// </remarks>
        public override void PostUpdate()
        {
            if (!DraggableUIPanel.respawnSentriesEnabled)
                return; // Don't refresh sentries if the toggle is off

            var player = Main.LocalPlayer;

            foreach (var projectile in Main.projectile)
            {
                // Skip non-active, non-sentry, or non-local player's projectiles
                if (!projectile.active || !projectile.sentry || projectile.owner != player.whoAmI)
                    continue;

                if (IsOffScreen(projectile))
                {
                    int currentTime = (int)Main.GameUpdateCount; // Explicitly cast from uint to int

                    // Check cooldown before refreshing
                    if (!sentryRefreshCooldowns.TryGetValue(projectile.whoAmI, out int lastRefreshTime) ||
                        currentTime - lastRefreshTime > 120) // Cooldown of 2 seconds (120 ticks)
                    {
                        RefreshSentries(player); // Resummon sentries
                        sentryRefreshCooldowns[projectile.whoAmI] = currentTime; // Update cooldown
                    }
                }
            }

            // Check if we are waiting for the player to fully respawn
            if (isWaitingForRespawn)
            {
                if (!player.dead && player.statLife > 0) // Fully respawned
                {
                    isWaitingForRespawn = false; // Reset the flag
                    SummonAllItemsIfPossible(player); // Summon items when fully respawned
                }
            }
        }

        /// <summary>
        /// Summons all configured minions and sentries for the player.
        /// </summary>
        /// <param name="player">The player to summon for.</param>
        /// <remarks>
        /// This function:
        /// - Gets the DraggableUIPanel instance
        /// - Calls SummonAllItems on the panel to summon all configured items
        ///
        /// Called from:
        /// - PostUpdate() in this file line: 524 (when player respawns)
        /// - OnEnterWorld() in this file line: 405 (if AutoSummonOnWorldEnter enabled)
        /// - PlayerConnect() in this file line: 559 (when player connects)
        /// </remarks>
        private void SummonAllItemsIfPossible(Player player)
        {
            // Reference the DraggableUIPanel instance
            var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;

            if (draggableUIPanel == null)
            {
                return;
            }

            // Call the SummonAllItems function
            draggableUIPanel.SummonAllItems(player);
        }

        /// <summary>
        /// Flag to track if we're waiting for player to fully respawn.
        /// </summary>
        private bool isWaitingForRespawn = false;

        /// <summary>
        /// Called when the player respawns. Triggers auto-summon if configured.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Gets the AutoSummonConfig
        /// - If AutoSummonOnRespawn is enabled, sets isWaitingForRespawn to true
        /// - PostUpdate will then trigger SummonAllItemsIfPossible when player is alive
        ///
        /// Called automatically by tModLoader when player respawns.
        /// </remarks>
        public override void OnRespawn()
        {
            base.OnRespawn();
            var config = ModContent.GetInstance<AutoSummonConfig>();
            if (config.AutoSummonOnRespawn)
            {
                isWaitingForRespawn = true;
            }
        }

        /// <summary>
        /// Called when the player connects. Triggers initial summon.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Sets isSpawning to true
        /// - Calls SummonAllItemsIfPossible to summon configured minions/sentries
        ///
        /// Called automatically by tModLoader when player connects.
        /// </remarks>
        public override void PlayerConnect()
        {
            base.PlayerConnect();
            isSpawning = true;
            SummonAllItemsIfPossible(Player);
        }

        /// <summary>
        /// Refreshes sentries by killing off-screen ones and resummoning them.
        /// </summary>
        /// <param name="player">The player whose sentries to refresh.</param>
        /// <remarks>
        /// This function:
        /// - Gets the DraggableUIPanel instance
        /// - Kills all off-screen sentries owned by the player
        /// - Tops up each panel's configured sentry type only up to its own configured
        ///   quantity (counting sentries of that specific projectile type that are still
        ///   alive on-screen), rather than blindly resummoning the full configured amount -
        ///   otherwise every off-screen refresh would keep stacking extra sentries on top
        ///   of the ones that were never killed
        ///
        /// Called from:
        /// - PostUpdate() in this file line: 512 (when sentry is detected off-screen)
        /// </remarks>
        private void RefreshSentries(Player player)
        {
            // Reference the DraggableUIPanel instance
            var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;
            if (draggableUIPanel == null)
            {
                return;
            }

            // Iterate through all projectiles and refresh only local player's off-screen sentries
            foreach (var projectile in Main.projectile)
            {
                if (projectile.active && projectile.sentry && projectile.owner == player.whoAmI)
                {
                    if (IsOffScreen(projectile)) // Refresh only off-screen sentries
                    {
                        projectile.Kill(); // Kill the off-screen sentry
                    }
                }
            }

            // Access sentry panels from DraggableUIPanel
            var sentryPanels = draggableUIPanel.sentryPanels;

            foreach (var panel in sentryPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || data.ItemSlot.Item == null || data.ItemSlot.Item.IsAir)
                    continue;

                var summonItem = data.ItemSlot.Item;
                if (!int.TryParse(data.QuantityLabel.Text.Replace("Sentries: ", ""), out int quantity))
                    continue;

                // Only replace what's missing for this specific sentry type, not the full quantity
                int aliveOfType = 0;
                foreach (var proj in Main.projectile)
                {
                    if (proj.active && proj.owner == player.whoAmI && proj.sentry && proj.type == summonItem.shoot)
                        aliveOfType++;
                }

                for (int i = aliveOfType; i < quantity; i++)
                {
                    AutoSummonSystem.SummonWithItem(player, summonItem);
                }
            }
        }

        /// <summary>
        /// Checks if a projectile is off-screen (with 100px buffer).
        /// </summary>
        /// <param name="projectile">The projectile to check.</param>
        /// <returns>True if the projectile is outside the screen bounds plus 100px buffer.</returns>
        /// <remarks>
        /// This function:
        /// - Compares projectile position against screen bounds
        /// - Uses a 100 pixel buffer around the screen
        ///
        /// Called from:
        /// - PostUpdate() in this file line: 505 (checking if sentry is off-screen)
        /// - RefreshSentries() in this file line: 638 (checking which sentries to kill)
        /// </remarks>
        private bool IsOffScreen(Projectile projectile)
        {
            return projectile.position.X < Main.screenPosition.X - 100 ||
                   projectile.position.X > Main.screenPosition.X + Main.screenWidth + 100 ||
                   projectile.position.Y < Main.screenPosition.Y - 100 ||
                   projectile.position.Y > Main.screenPosition.Y + Main.screenHeight + 100;
        }

        /// <summary>
        /// Dictionary to track cooldowns for sentry refresh to prevent spam.
        /// Key: projectile whoAmI, Value: last refresh time in game ticks.
        /// </summary>
        private Dictionary<int, int> sentryRefreshCooldowns = new();

    }
}
