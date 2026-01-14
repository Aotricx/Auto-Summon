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
        /// - Calls MaintainMinions if player has MinionItems configured
        /// - Calls MaintainSentries if player has SentryItems configured
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

            // Optional: Handle resummon logic here (already in place for minions/sentries)
            if (autoSummonPlayer.MinionItems.Count > 0 && player.maxMinions > 0)
            {
                MaintainMinions(player, autoSummonPlayer);
            }

            if (autoSummonPlayer.SentryItems.Count > 0 && player.maxTurrets > 0)
            {
                MaintainSentries(player, autoSummonPlayer);
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
                    int newQuantity = Math.Min(remainingSlots, maxSlots);

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
        /// <param name="autoSummonPlayer">The AutoSummonPlayer data for this player.</param>
        /// <remarks>
        /// This function:
        /// - Calculates current minion slots used by counting active minion projectiles
        /// - For each item in MinionItems, summons more if slots are available
        /// - Recalculates slots after each summon to prevent over-summoning
        ///
        /// Called from:
        /// - PostUpdateEverything() in this file line: 84 (if MinionItems.Count > 0)
        /// </remarks>
        private void MaintainMinions(Player player, AutoSummonPlayer autoSummonPlayer)
        {
            float currentMinionSlotsUsed = 0f;

            // Calculate current minion slots used
            foreach (Projectile proj in Main.projectile)
            {
                if (proj.active && proj.owner == player.whoAmI && proj.minion)
                {
                    currentMinionSlotsUsed += proj.minionSlots;
                }
            }

            // Summon more minions if slots are available
            foreach (var item in autoSummonPlayer.MinionItems)
            {
                while (currentMinionSlotsUsed < player.maxMinions && autoSummonPlayer.MinionQuantity > 0)
                {
                    SummonWithItem(player, item);
                    currentMinionSlotsUsed += item.useAnimation; // Adjusted for actual use behavior
                    autoSummonPlayer.MinionQuantity--;

                    // Recalculate minion slots
                    currentMinionSlotsUsed = 0f;
                    foreach (Projectile proj in Main.projectile)
                    {
                        if (proj.active && proj.owner == player.whoAmI && proj.minion)
                        {
                            currentMinionSlotsUsed += proj.minionSlots;
                        }
                    }

                    if (currentMinionSlotsUsed >= player.maxMinions)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Maintains sentry counts by summoning more if slots are available.
        /// </summary>
        /// <param name="player">The player whose sentries to maintain.</param>
        /// <param name="autoSummonPlayer">The AutoSummonPlayer data for this player.</param>
        /// <remarks>
        /// This function:
        /// - Counts current active sentries owned by the player
        /// - For each item in SentryItems, summons more if slots are available
        /// - Recalculates count after each summon to prevent over-summoning
        ///
        /// Called from:
        /// - PostUpdateEverything() in this file line: 89 (if SentryItems.Count > 0)
        /// </remarks>
        private void MaintainSentries(Player player, AutoSummonPlayer autoSummonPlayer)
        {
            int currentSentryCount = 0;

            // Count active sentries
            for (int i = 0; i < Main.projectile.Length; i++)
            {
                var proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && proj.sentry)
                {
                    currentSentryCount++;
                }
            }

            // Summon more sentries if slots are available
            foreach (var item in autoSummonPlayer.SentryItems)
            {
                while (currentSentryCount < player.maxTurrets)
                {
                    SummonWithItem(player, item);

                    // Recalculate sentry count
                    currentSentryCount = 0;
                    for (int i = 0; i < Main.projectile.Length; i++)
                    {
                        var proj = Main.projectile[i];
                        if (proj.active && proj.owner == player.whoAmI && proj.sentry)
                        {
                            currentSentryCount++;
                        }
                    }

                    if (currentSentryCount >= player.maxTurrets)
                    {
                        break;
                    }
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
        /// - Sets originalDamage and adds a 1-hour buff (3600 ticks)
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
                    player.AddBuff(summonItem.buffType, 3600); // Add buff for 1 hour
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
                    player.AddBuff(summonItem.buffType, 3600); // Add buff for 1 hour
                }
            }
        }
    }
}