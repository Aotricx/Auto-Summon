// ============================================================================
// File: AutoSummonSystem.cs
// Description: ModSystem that handles game update logic for summon maintenance.
//              Monitors slot changes and maintains minion/sentry counts.
//              Based on the structure of Lanboost's AutoSummon.
// ============================================================================
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria.DataStructures;
using System.Linq;
using Terraria.ModLoader.IO;
using AutoSummon.UI;
using System;
using Terraria.GameContent.UI.Elements;

namespace AutoSummon
{
    /// <summary>
    /// ModSystem class that handles game update logic for summon maintenance.
    /// Monitors slot changes and maintains minion/sentry counts.
    /// </summary>
    /// <remarks>
    /// This class is used in the following locations:
    /// - DraggableUIPanel.cs line: 1057, 1073 (SummonWithItem calls)
    /// - AutoSummonPlayer.cs line: 658 (SummonWithItem in RefreshSentries)
    /// </remarks>
    public class AutoSummonSystem : ModSystem
    {
        /// <summary>
        /// Tracks the last known max minion count to detect changes.
        /// </summary>
        private int lastMaxMinions = 0;

        /// <summary>
        /// Tracks the last known max sentry/turret count to detect changes.
        /// </summary>
        private int lastMaxTurrets = 0;

        /// <summary>
        /// Called every frame after all other updates. Monitors and maintains summons.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Checks if player is dead, ghost, or summon is disabled - returns early if so
        /// - Detects changes in maxMinions and calls HandleSlotChange for minion panels
        /// - Detects changes in maxTurrets and calls HandleSlotChange for sentry panels
        /// - Calls MaintainMinions/MaintainSentries to replenish anything configured in the
        ///   UI panels that died mid-game (the panels are the source of truth for what's
        ///   configured, not any per-player quantity field)
        ///
        /// Called automatically by tModLoader every frame.
        /// </remarks>
        public override void PostUpdateEverything()
        {
            var player = Main.LocalPlayer;
            var autoSummonPlayer = player.GetModPlayer<AutoSummonPlayer>();
            var draggableUIPanel = AutoSummon.DraggableUIPanelInstance;

            if (player.dead || player.ghost || autoSummonPlayer.tempSummonDisabled || draggableUIPanel == null)
            {
                autoSummonPlayer.tempSummonDisabled = false; // Reset the temporary summon disabled flag
                return;
            }

            // Check for changes in max minions
            if (player.maxMinions != lastMaxMinions)
            {
                lastMaxMinions = player.maxMinions;
                HandleSlotChange(draggableUIPanel.interactionPanels, player.maxMinions, draggableUIPanel.GetTotalMinions());
            }

            // Check for changes in max sentries
            if (player.maxTurrets != lastMaxTurrets)
            {
                lastMaxTurrets = player.maxTurrets;
                HandleSlotChange(draggableUIPanel.sentryPanels, player.maxTurrets, draggableUIPanel.GetTotalSentries());
            }

            if (player.maxMinions > 0)
            {
                MaintainMinions(player, draggableUIPanel);
            }

            if (player.maxTurrets > 0)
            {
                MaintainSentries(player, draggableUIPanel);
            }
        }

        /// <summary>
        /// Handles changes in max minion/sentry slots by adjusting filled panel quantities.
        /// </summary>
        /// <param name="panels">The list of UI panels (interactionPanels or sentryPanels).</param>
        /// <param name="maxSlots">The new maximum number of slots.</param>
        /// <param name="totalUsedSlots">The total currently used slots.</param>
        /// <remarks>
        /// This function:
        /// - Iterates through panels with IsFilled=true
        /// - Recalculates quantities based on new max slots
        /// - Updates the quantity label text
        /// - Calls RefreshSummons to apply changes
        ///
        /// Called from:
        /// - PostUpdateEverything() in this file line: 70, 77 (when max slots change)
        /// </remarks>
        private void HandleSlotChange(List<UIPanel> panels, int maxSlots, int totalUsedSlots)
        {
            foreach (var panel in panels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || data.ItemSlot.Item == null || data.ItemSlot.Item.IsAir)
                    continue;

                // Adjust only filled panels
                if (data.IsFilled)
                {
                    // Recalculate the new quantity for the panel
                    int currentQuantity = int.Parse(data.QuantityLabel.Text.Replace("Minions: ", "").Replace("Sentries: ", ""));
                    int remainingSlots = maxSlots - (totalUsedSlots - currentQuantity);
                    int newQuantity = Math.Clamp(remainingSlots, 0, maxSlots);

                    // Update the panel's quantity
                    data.QuantityLabel.SetText($"{(panels == AutoSummon.DraggableUIPanelInstance.sentryPanels ? "Sentries" : "Minions")}: {newQuantity}");
                }
            }

            // Refresh summons to ensure in-game projectiles reflect updated quantities
            AutoSummon.DraggableUIPanelInstance.RefreshSummons();
        }

        /// <summary>
        /// Maintains minion counts by summoning more if slots are available.
        /// </summary>
        /// <param name="player">The player whose minions to maintain.</param>
        /// <param name="draggableUIPanel">The UI panel holding the configured minion items/quantities.</param>
        /// <remarks>
        /// This function:
        /// - Reads configured items/quantities directly from interactionPanels (the source of
        ///   truth for what the player configured), per item type
        /// - For each panel, tops up only the shortfall between the configured quantity and
        ///   how many of that specific projectile type are currently alive
        /// - Stops once player.maxMinions worth of minion slots are in use
        ///
        /// Called from:
        /// - PostUpdateEverything() in this file line: 84 (if player.maxMinions > 0)
        /// </remarks>
        private void MaintainMinions(Player player, DraggableUIPanel draggableUIPanel)
        {
            foreach (var panel in draggableUIPanel.interactionPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data?.ItemSlot?.Item == null || data.ItemSlot.Item.IsAir)
                    continue;

                var item = data.ItemSlot.Item;
                if (item.shoot <= ProjectileID.None)
                    continue;

                if (!int.TryParse(data.QuantityLabel.Text.Replace("Minions: ", ""), out int desiredQuantity) || desiredQuantity <= 0)
                    continue;

                int aliveOfType = CountActiveMinionsOfType(player, item.shoot);
                float totalMinionSlotsUsed = CountActiveMinionSlots(player);

                while (aliveOfType < desiredQuantity && totalMinionSlotsUsed < player.maxMinions)
                {
                    SummonWithItem(player, item);
                    aliveOfType++;

                    // Recalculate actual slot usage since minionSlots cost varies per minion type
                    totalMinionSlotsUsed = CountActiveMinionSlots(player);
                }
            }
        }

        /// <summary>
        /// Counts active minion projectiles of a specific type owned by the player.
        /// </summary>
        private static int CountActiveMinionsOfType(Player player, int projectileType)
        {
            int count = 0;
            foreach (Projectile proj in Main.projectile)
            {
                if (proj.active && proj.owner == player.whoAmI && proj.minion && proj.type == projectileType)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Sums minionSlots used by all active minions owned by the player.
        /// </summary>
        private static float CountActiveMinionSlots(Player player)
        {
            float slots = 0f;
            foreach (Projectile proj in Main.projectile)
            {
                if (proj.active && proj.owner == player.whoAmI && proj.minion)
                {
                    slots += proj.minionSlots;
                }
            }
            return slots;
        }

        /// <summary>
        /// Maintains sentry counts by summoning more if slots are available.
        /// </summary>
        /// <param name="player">The player whose sentries to maintain.</param>
        /// <param name="draggableUIPanel">The UI panel holding the configured sentry items/quantities.</param>
        /// <remarks>
        /// This function:
        /// - Reads configured items/quantities directly from sentryPanels (the source of truth
        ///   for what the player configured), per item type
        /// - For each panel, tops up only the shortfall between the configured quantity and how
        ///   many of that specific projectile type are currently alive
        /// - Stops once player.maxTurrets sentries are active in total
        ///
        /// Called from:
        /// - PostUpdateEverything() in this file line: 89 (if player.maxTurrets > 0)
        /// </remarks>
        private void MaintainSentries(Player player, DraggableUIPanel draggableUIPanel)
        {
            foreach (var panel in draggableUIPanel.sentryPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data?.ItemSlot?.Item == null || data.ItemSlot.Item.IsAir)
                    continue;

                var item = data.ItemSlot.Item;
                if (item.shoot <= ProjectileID.None)
                    continue;

                if (!int.TryParse(data.QuantityLabel.Text.Replace("Sentries: ", ""), out int desiredQuantity) || desiredQuantity <= 0)
                    continue;

                int aliveOfType = 0;
                int totalSentries = 0;
                foreach (Projectile proj in Main.projectile)
                {
                    if (proj.active && proj.owner == player.whoAmI && proj.sentry)
                    {
                        totalSentries++;
                        if (proj.type == item.shoot)
                        {
                            aliveOfType++;
                        }
                    }
                }

                while (aliveOfType < desiredQuantity && totalSentries < player.maxTurrets)
                {
                    SummonWithItem(player, item);
                    aliveOfType++;
                    totalSentries++;
                }
            }
        }

        /// <summary>
        /// Summons a minion or sentry projectile using the specified item.
        /// </summary>
        /// <param name="player">The player to summon for.</param>
        /// <param name="summonItem">The item to use for summoning.</param>
        /// <remarks>
        /// This function:
        /// - Validates that the item is not null/air and has a valid projectile type
        /// - Creates a test projectile to check if it's a minion or sentry
        /// - For minions: Creates the projectile and adds the buff
        /// - For sentries: Checks if max sentries reached before creating
        /// - Sets originalDamage and adds a 1-minute buff (3600 ticks) as an initial icon;
        /// the minion/sentry's own AI continually refreshes it while alive
        ///
        /// Called from:
        /// - DraggableUIPanel.cs line: 1057 (SummonAllItems - minions)
        /// - DraggableUIPanel.cs line: 1073 (SummonAllItems - sentries)
        /// - AutoSummonPlayer.cs line: 658 (RefreshSentries)
        /// - MaintainMinions() in this file line: 165 (maintaining minion count)
        /// - MaintainSentries() in this file line: 220 (maintaining sentry count)
        /// </remarks>
        public static void SummonWithItem(Player player, Item summonItem)
        {
            if (summonItem == null || summonItem.IsAir)
                return;

            // Ensure the item has a valid projectile to summon
            int projectileType = summonItem.shoot;
            if (projectileType <= ProjectileID.None)
            {
                return;
            }

            // Get the projectile defaults
            Projectile projectile = new Projectile();
            projectile.SetDefaults(projectileType);

            Vector2 spawnPosition = player.Center;

            // Minion Summoning Logic
            if (projectile.minion)
            {
                int projIndex = Projectile.NewProjectile(
                    player.GetSource_ItemUse(summonItem), // Source of the projectile
                    spawnPosition,                        // Spawn position
                    Vector2.Zero,                         // Velocity
                    projectileType,                       // Projectile type
                    summonItem.damage,                    // Damage
                    summonItem.knockBack,                 // Knockback
                    player.whoAmI                         // Owner (the player)
                );

                // Tie the projectile to the player and add the buff
                if (projIndex != Main.maxProjectiles)
                {
                    Main.projectile[projIndex].originalDamage = summonItem.damage;
                    player.AddBuff(summonItem.buffType, 3600); // Add buff for 1 minute (refreshed by the minion/sentry while alive)
                }
            }

            // Sentry Summoning Logic
            if (projectile.sentry)
            {
                // Count current sentries
                int activeSentries = 0;
                foreach (var proj in Main.projectile)
                {
                    if (proj.active && proj.owner == player.whoAmI && proj.sentry)
                    {
                        activeSentries++;
                    }
                }

                if (activeSentries >= player.maxTurrets)
                {
                    return;
                }

                int projIndex = Projectile.NewProjectile(
                    player.GetSource_ItemUse(summonItem),
                    spawnPosition,
                    Vector2.Zero,
                    projectileType,
                    summonItem.damage,
                    summonItem.knockBack,
                    player.whoAmI
                );

                // Tie the projectile to the player and add the buff
                if (projIndex != Main.maxProjectiles)
                {
                    Main.projectile[projIndex].originalDamage = summonItem.damage;
                    player.AddBuff(summonItem.buffType, 3600); // Add buff for 1 minute (refreshed by the minion/sentry while alive)
                }
            }
        }
    }
}