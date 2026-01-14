// ============================================================================
// File: DraggableUIPanel.cs
// Description: Main UI panel for the AutoSummon mod. Contains all UI elements
//              including the draggable header, minion/sentry panels, item slots,
//              quantity controls, and refresh/sentry respawn buttons.
// ============================================================================
using Terraria;
using Terraria.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using CustomSlot.UI;
using System;
using System.Collections.Generic;
using Terraria.ID;
using System.Linq;
using Terraria.GameContent;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Humanizer;
using ReLogic.Content;

namespace AutoSummon.UI
{
    /// <summary>
    /// Main UI panel state for the AutoSummon mod.
    /// Contains all UI elements for configuring minion and sentry summons.
    /// </summary>
    /// <remarks>
    /// This class is used in the following locations:
    /// - AutoSummon.cs line: 27, 32, 49 (creates instance, assigns to static reference)
    /// - AutoSummonPlayer.cs line: 252, 333, 367, 443 (accesses interactionPanels, sentryPanels, methods)
    /// - AutoSummonSystem.cs line: 59, 70, 78, 131 (accesses panels and calls RefreshSummons)
    /// </remarks>
    public class DraggableUIPanel : UIState
    {
        /// <summary>The main container panel for all UI elements.</summary>
        private UIPanel mainPanel;

        /// <summary>The draggable header bar at the top of the panel.</summary>
        private UIPanel headerBar;

        /// <summary>The title text displayed in the header ("Kunaii's Auto Summon").</summary>
        private UIText titleText;

        /// <summary>Label showing current/max minion slots (e.g., "Minion Slots: 3/5").</summary>
        private UIText minionSlotsLabel;

        /// <summary>Label showing current/max sentry slots (e.g., "Sentry Slots: 1/2").</summary>
        private UIText sentrySlotsLabel;

        /// <summary>
        /// List of minion interaction panels (each with item slot, buttons, quantity).
        /// </summary>
        /// <remarks>
        /// Used in:
        /// - AutoSummonPlayer.cs line: 257 (CollectPanelData)
        /// - AutoSummonSystem.cs line: 70 (HandleSlotChange for minions)
        /// </remarks>
        public List<UIPanel> interactionPanels = new();

        /// <summary>
        /// List of sentry interaction panels (each with item slot, buttons, quantity).
        /// </summary>
        /// <remarks>
        /// Used in:
        /// - AutoSummonPlayer.cs line: 273 (CollectPanelData)
        /// - AutoSummonSystem.cs line: 78 (HandleSlotChange for sentries)
        /// </remarks>
        public List<UIPanel> sentryPanels = new();

        /// <summary>Dictionary tracking the last known state of each item slot.</summary>
        private Dictionary<CustomItemSlot, Item> lastItemStates = new();

        /// <summary>Offset for dragging the panel.</summary>
        private Vector2 offset;

        /// <summary>Flag indicating if the panel is currently being dragged.</summary>
        private bool dragging = false;

        /// <summary>Flag indicating if the player is spawning (used for auto-summon).</summary>
        public bool isSpawning = false;

        /// <summary>
        /// Called when the UI state is initialized. Creates all UI elements.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Creates the mainPanel container with padding and background color
        /// - Creates the headerBar for dragging (29px height)
        /// - Creates the titleText (0.8f scale), minionSlotsLabel, sentrySlotsLabel
        /// - Calls CreateSentryRespawnButton and CreateRespawnButton
        ///
        /// Called automatically by tModLoader when UI state is created.
        /// </remarks>
        public override void OnInitialize()
        {
            // Dynamically set the width for mainPanel
            const int panelPadding = 6; // Extra padding for the main panel
            const int interactionPanelWidth = 295; // Width of each interaction panel
            const int baseHeight = 65; // Base height for main panel

            mainPanel = new UIPanel
            {
                Width = { Pixels = interactionPanelWidth + panelPadding }, // Adjust mainPanel width
                Height = { Pixels = baseHeight },
                HAlign = 0.5f,
                VAlign = 0.5f,
                BackgroundColor = new Color(50, 50, 70, 200)
            };
            Append(mainPanel);

            // Header bar
            headerBar = new UIPanel
            {
                Width = { Pixels = interactionPanelWidth }, // Match interaction panel width
                Height = { Pixels = 29 },
                BackgroundColor = new Color(30, 30, 50, 255)
            };
            headerBar.OnLeftMouseDown += StartDrag;
            headerBar.OnLeftMouseUp += EndDrag;
            mainPanel.Append(headerBar);

            // Title text
            titleText = new UIText("Kunaii's Auto Summon", 0.8f)
            {
                HAlign = 0.5f,
                VAlign = 0.5f
            };
            headerBar.Append(titleText);

            // Minion slots label (gap is above, not below)
            minionSlotsLabel = new UIText("Minion Slots: 0/0", 0.8f)
            {
                HAlign = 0.5f,
                Top = { Pixels = 50 }
            };
            mainPanel.Append(minionSlotsLabel);

            // Sentry slots label (gap is above, not below)
            sentrySlotsLabel = new UIText("Sentry Slots: 0/0", 0.8f)
            {
                HAlign = 0.5f,
                Top = { Pixels = 68 + interactionPanels.Count * 50 + 24 } // Gap above the label
            };
            mainPanel.Append(sentrySlotsLabel);

            CreateSentryRespawnButton();
            CreateRespawnButton();
        }

        /// <summary>The sentry respawn toggle button (right side of header).</summary>
        private UIImageButton sentryButton;

        /// <summary>
        /// Static flag controlling whether sentries auto-respawn when off-screen.
        /// </summary>
        /// <remarks>
        /// Used in:
        /// - AutoSummonPlayer.cs line: 56 (respawnSentriesEnabled field initialization)
        /// - AutoSummonPlayer.cs line: 380 (set from config in OnEnterWorld)
        /// - AutoSummonPlayer.cs line: 503 (checked in PostUpdate)
        /// </remarks>
        public static bool respawnSentriesEnabled { get; set; } = true;

        /// <summary>The refresh summons button (left side of header).</summary>
        private UIImageButton RefreshButton;

        /// <summary>
        /// Creates the refresh button in the header bar.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Loads the RefreshButton texture from Assets/UI/RefreshButton
        /// - Creates a 14x14 button at position (4, 4)
        /// - Hooks OnLeftClick to call RefreshSummons()
        /// - Sets visibility to 1f active, 0.8f inactive
        ///
        /// Called from:
        /// - OnInitialize() in this file line: 138 (during UI setup)
        /// </remarks>
        private void CreateRespawnButton()
        {
            // Load the refresh icon
            var bookIcon = ModContent.Request<Texture2D>("AutoSummon/Assets/UI/RefreshButton");

            // Create the refresh button
            RefreshButton = new UIImageButton(bookIcon)
            {
                Width = { Pixels = 14 },
                Height = { Pixels = 14 },
                Top = { Pixels = 4 },
                Left = { Pixels = 4 }
            };

            RefreshButton.OnLeftClick += (evt, element) =>
            {
                RefreshSummons();
            };

            RefreshButton.SetVisibility(1f, 0.8f);

            mainPanel.Append(RefreshButton);
        }

        /// <summary>
        /// Creates the sentry respawn toggle button in the header bar.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Loads RespawnSentry and NoRespawnSentry textures
        /// - Creates a 14x14 button positioned at the right side of the header
        /// - Toggles respawnSentriesEnabled on click and swaps button image
        /// - Shows a chat message when toggled
        ///
        /// Called from:
        /// - OnInitialize() in this file line: 137 (during UI setup)
        /// </remarks>
        public void CreateSentryRespawnButton()
        {
            var Yes = ModContent.Request<Texture2D>("AutoSummon/Assets/UI/RespawnSentry");
            var No = ModContent.Request<Texture2D>("AutoSummon/Assets/UI/NoRespawnSentry");

            float titleWidth = headerBar.GetDimensions().Width;
            float titleLeft = headerBar.Left.Pixels;
            float buttonLeftPosition = titleLeft + titleWidth - 20;

            sentryButton = new UIImageButton(Yes)
            {
                Width = { Pixels = 14 },
                Height = { Pixels = 14 },
                Top = { Pixels = 4 },
                Left = { Pixels = buttonLeftPosition - 5 }
            };

            sentryButton.OnLeftClick += (evt, element) =>
            {
                respawnSentriesEnabled = !respawnSentriesEnabled;
                sentryButton.SetImage(respawnSentriesEnabled ? Yes : No);
                if (Main.netMode != NetmodeID.Server)
                {
                    Main.NewText(respawnSentriesEnabled
                        ? "Sentries respawn when off-screen: On"
                        : "Sentries respawn when off-screen: Off", Color.Cyan);
                }
            };

            sentryButton.SetVisibility(1f, 0.8f);

            mainPanel.Append(sentryButton);
        }

        /// <summary>
        /// Creates a sentry interaction panel with item slot, buttons, and quantity label.
        /// </summary>
        /// <param name="index">The index position for the panel (0-based).</param>
        /// <param name="item">Optional item to pre-populate the slot with.</param>
        /// <param name="quantity">Optional starting quantity.</param>
        /// <remarks>
        /// This function:
        /// - Creates a 295x48 panel positioned below minion panels
        /// - Creates a CustomItemSlot (38x38, 0.8f scale) with sentry validation
        /// - Creates -1, +1, Fill buttons and quantity label
        /// - Stores InteractionPanelData tag on the panel
        /// - Calls UpdateMainPanelHeight and UpdateFillButtonText
        ///
        /// Called from:
        /// - AutoSummonPlayer.cs line: 467 (LoadPanelsFromSavedData)
        /// - HandleSentryPanelChanged() in this file line: 861 (when adding new panel)
        /// </remarks>
        public void CreateSentryPanel(int index = 0, Item item = null, int quantity = 0)
        {
            const int itemSlotSize = 38;
            const int buttonWidth = 38;
            const int labelWidth = 75;
            const int spacing = 5;
            const int panelHeight = 48;
            const int buttonHeight = 24;
            const int labelHeight = 24;

            // 68 = minion panels start position
            // + interactionPanels.Count * 50 = total minion panels height
            // + 24 = gap above sentry label
            // + 18 = sentry label height
            // + index * 50 = this panel's offset
            int topOffset = 68 + interactionPanels.Count * 50 + 24 + 18 + index * 50;

            // Create the panel
            var panel = new UIPanel
            {
                Width = { Pixels = 295 },
                Height = { Pixels = panelHeight },
                Top = { Pixels = topOffset },
                BackgroundColor = new Color(35, 35, 50, 200)
            };
            panel.SetPadding(0); // Remove internal padding
            mainPanel.Append(panel);

            // Center the item slot vertically within the panel
            var itemSlot = new CustomItemSlot(ItemSlot.Context.InventoryItem, 0.8f)
            {
                Width = { Pixels = itemSlotSize },
                Height = { Pixels = itemSlotSize },
                Left = { Pixels = 3 },
                Top = { Pixels = 3 }, // Center vertically
                IsValidItem = item => IsSentrySummoningItem(item) // Restrict to valid sentry items
            };
            panel.Append(itemSlot);
            itemSlot.SetPadding(0); // Remove internal padding

            // Store the initial state of the item in the slot
            lastItemStates[itemSlot] = itemSlot.Item.Clone();

            // Create buttons and labels
            var minusButton = CreateButton(
                "-1",
                new Vector2(itemSlot.Left.Pixels + itemSlotSize + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => UpdateSentryQuantity(panel, -1),
                buttonWidth, buttonHeight);

            var plusButton = CreateButton(
                "+1",
                new Vector2(minusButton.Left.Pixels + buttonWidth + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => UpdateSentryQuantity(panel, 1),
                buttonWidth, buttonHeight);

            var fillButton = CreateButton(
                "Fill",
                new Vector2(plusButton.Left.Pixels + buttonWidth + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => ToggleFill(panel),
                buttonWidth, buttonHeight);

            int maxSentries = Main.LocalPlayer.maxTurrets;
            int currentSentryCount = GetCurrentSentryCount();
            if (item != null && !item.IsAir)
            {
                itemSlot.SetItem(item);
            }

            var quantityLabel = new UIText($"Sentries: {quantity}", 0.8f)
            {
                Width = { Pixels = labelWidth },
                Left = { Pixels = fillButton.Left.Pixels + buttonWidth + spacing },
                Top = { Pixels = (panelHeight - labelHeight) / 2 + 3 }
            };
            panel.Append(quantityLabel); // Append the label to the panel

            // Store the panel's metadata
            panel.SetTag(new InteractionPanelData
            {
                MinusButton = minusButton,
                PlusButton = plusButton,
                FillButton = fillButton,
                QuantityLabel = quantityLabel,
                ItemSlot = itemSlot
            });

            // Add the panel to the list
            sentryPanels.Add(panel);
            UpdateMainPanelHeight();
            UpdateFillButtonText();
        }

        /// <summary>
        /// Validates if an item is a sentry summoning weapon.
        /// </summary>
        /// <param name="item">The item to validate.</param>
        /// <returns>True if item shoots a sentry projectile (not a minion).</returns>
        /// <remarks>
        /// This function:
        /// - Checks if item is Summon damage type
        /// - Creates test projectile to check if it's a sentry (not minion)
        ///
        /// Called from:
        /// - CreateSentryPanel() in this file line: 301 (IsValidItem callback)
        /// - HandleSentryPanelChanged() in this file line: 827 (validating item)
        /// </remarks>
        private bool IsSentrySummoningItem(Item item)
        {
            if (item != null && item.DamageType == DamageClass.Summon)
            {
                Projectile projectile = new Projectile();
                projectile.SetDefaults(item.shoot);
                return projectile.sentry && !projectile.minion;
            }
            return false;
        }

        /// <summary>
        /// Checks if summoning should occur based on player state.
        /// </summary>
        /// <returns>True if player can summon (not dead, not holding item, not swinging).</returns>
        /// <remarks>
        /// This function:
        /// - Returns false if player is dead (resets tempSummonDisabled)
        /// - Returns false if player is holding an item (mouseItem not air)
        /// - Returns false if player is in the middle of an item swing animation
        /// - Returns true if tempSummonDisabled is false and isSpawning is true
        /// </remarks>
        protected bool shouldSummon()
        {
            var player = Main.LocalPlayer;
            var player2 = Main.LocalPlayer.GetModPlayer<AutoSummonPlayer>();

            // Do nothing if dead
            if (player.dead)
            {
                player2.tempSummonDisabled = false;
                return false;
            }

            // Do notthing if holding an item
            if (Main.mouseItem != null && !Main.mouseItem.IsAir) {
                return false;
            }

            // Do nothing if in middle of swing
            if(player.itemAnimation != 0)
            {
                return false;
            }


            return !player2.tempSummonDisabled && isSpawning;
        }

        /// <summary>
        /// Counts the current minion slots used by active minion projectiles.
        /// </summary>
        /// <returns>Total minion slots currently in use (float to handle fractional slots).</returns>
        /// <remarks>
        /// This function:
        /// - Iterates through all 1000 projectile slots
        /// - Sums minionSlots for active minions owned by local player
        ///
        /// Called from:
        /// - UpdateMinionSlotsLabel() in this file line: 556 (updating UI label)
        /// - CreateInteractionPanel() in this file line: 692 (initial panel setup)
        /// - HandleItemSlotChanged() in this file line: 990 (when item changes)
        /// </remarks>
        protected float GetCurrentMinionCount()
        {
            float minCount = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (Main.projectile[i].active && Main.projectile[i].minion && Main.projectile[i].owner == Main.myPlayer)
                {
                    minCount += Main.projectile[i].minionSlots;
                }
            }
            return minCount;
        }

        /// <summary>
        /// Counts the current active sentry projectiles.
        /// </summary>
        /// <returns>Number of active sentries owned by local player.</returns>
        /// <remarks>
        /// This function:
        /// - Iterates through all 1000 projectile slots
        /// - Counts WipableTurret projectiles owned by local player
        ///
        /// Called from:
        /// - UpdateSentrySlotsLabel() in this file line: 632 (updating UI label)
        /// - CreateSentryPanel() in this file line: 328 (initial panel setup)
        /// - HandleSentryPanelChanged() in this file line: 831 (when item changes)
        /// </remarks>
        protected int GetCurrentSentryCount()
        {
            int turrets = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (Main.projectile[i].active && Main.projectile[i].WipableTurret && Main.projectile[i].owner == Main.myPlayer)
                {
                    turrets += 1;
                }
            }
            return turrets;
        }

        /// <summary>
        /// Updates the quantity for a minion panel by the specified change amount.
        /// </summary>
        /// <param name="panel">The minion panel to update.</param>
        /// <param name="change">Amount to change (+1 or -1).</param>
        /// <remarks>
        /// This function:
        /// - Validates new quantity doesn't exceed maxMinions
        /// - Updates quantity label text
        /// - Updates Fill/Unfill button state
        /// - Calls RecalculateFilledPanels, RefreshSummons, UpdateMinionSlotsLabel
        ///
        /// Called from:
        /// - CreateInteractionPanel() in this file line: 712, 720 (button callbacks)
        /// </remarks>
        public void UpdateQuantity(UIPanel panel, int change)
        {
            int maxMinions = Main.LocalPlayer.maxMinions;      // Maximum allowed minions
            int totalFromPanels = GetTotalMinions();           // Total minion slots requested from all panels

            foreach (var element in panel.Children)
            {
                if (element is UIText quantityLabel)
                {
                    // Parse the current quantity from the label
                    string currentText = quantityLabel.Text.Replace("Minions: ", "");
                    int currentQuantity = int.TryParse(currentText, out int parsedQuantity) ? parsedQuantity : 0;

                    // Calculate the new total minions if the quantity changes
                    int newQuantity = Math.Max(0, currentQuantity + change);

                    // Validate that the new total minions do not exceed maxMinions
                    if (newQuantity > 0 && totalFromPanels - currentQuantity + newQuantity > maxMinions)
                    {
                        return;
                    }

                    // Update the quantity label
                    quantityLabel.SetText($"Minions: {newQuantity}");

                    // Get the panel's data and update its state
                    var data = panel.GetTag<InteractionPanelData>();
                    if (data == null) return;

                    const int normalButtonWidth = 38;
                    const int unfillButtonWidth = 56;
                    const int spacing = 5;

                    if (change == -1)
                    {
                        data.FillButton.SetText("Fill"); // Update button text
                        data.FillButton.Width.Set(normalButtonWidth, 0f);
                        data.QuantityLabel.Left.Set(data.FillButton.Left.Pixels + normalButtonWidth + spacing, 0f);
                        data.FillButton.Recalculate();
                        data.QuantityLabel.Recalculate();
                        data.IsFilled = false;          // Mark as not filled
                    }
                    else if (change == +1 && newQuantity == maxMinions)
                    {
                        // If incrementing and the quantity fills all remaining slots, mark the panel as filled
                        data.FillButton.SetText("Unfill");
                        data.FillButton.Width.Set(unfillButtonWidth, 0f);
                        data.QuantityLabel.Left.Set(data.FillButton.Left.Pixels + unfillButtonWidth + spacing, 0f);
                        data.FillButton.Recalculate();
                        data.QuantityLabel.Recalculate();
                        data.IsFilled = true;
                    }

                    // Recalculate and adjust quantities for all other filled panels
                    RecalculateFilledPanels();

                    // Trigger summoning/desummoning logic
                    RefreshSummons();
                    UpdateMinionSlotsLabel();
                    break;
                }
            }
        }

        /// <summary>
        /// Recalculates quantities for all panels marked as "filled" based on available slots.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - For minion panels: recalculates quantities based on maxMinions and total used
        /// - For sentry panels: recalculates quantities based on maxTurrets and total used
        /// - Only updates panels where IsFilled is true
        ///
        /// Called from:
        /// - UpdateQuantity() in this file line: 528 (after changing minion quantity)
        /// - UpdateSentryQuantity() in this file line: 605 (after changing sentry quantity)
        /// </remarks>
        private void RecalculateFilledPanels()
        {
            var player = Main.LocalPlayer;

            // Handle Minion Panels
            int maxMinions = player.maxMinions; // Maximum allowed minions
            int totalUsedMinionSlots = GetTotalMinions(); // Total currently used minion slots

            foreach (var panel in interactionPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || !data.IsFilled) continue; // Skip panels that aren't marked as filled

                // Parse the current quantity
                int currentQuantity = int.Parse(data.QuantityLabel.Text.Replace("Minions: ", ""));

                // Calculate the remaining available slots for this panel
                int remainingSlots = maxMinions - (totalUsedMinionSlots - currentQuantity);
                int newQuantity = Math.Min(remainingSlots, maxMinions);

                // Update the panel's quantity if it has changed
                if (newQuantity != currentQuantity)
                {
                    data.QuantityLabel.SetText($"Minions: {newQuantity}");
                }
            }

            // Handle Sentry Panels
            int maxSentries = player.maxTurrets; // Maximum allowed sentries
            int totalUsedSentrySlots = GetTotalSentries(); // Total currently used sentry slots

            foreach (var panel in sentryPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || !data.IsFilled) continue; // Skip panels that aren't marked as filled

                // Parse the current quantity
                int currentQuantity = int.Parse(data.QuantityLabel.Text.Replace("Sentries: ", ""));

                // Calculate the remaining available slots for this panel
                int remainingSlots = maxSentries - (totalUsedSentrySlots - currentQuantity);
                int newQuantity = Math.Min(remainingSlots, maxSentries);

                // Update the panel's quantity if it has changed
                if (newQuantity != currentQuantity)
                {
                    data.QuantityLabel.SetText($"Sentries: {newQuantity}");
                }
            }
        }

        /// <summary>
        /// Updates the minion slots label with current/max values.
        /// </summary>
        /// <remarks>
        /// Called from:
        /// - Update() in this file line: 830 (every frame)
        /// - UpdateQuantity() in this file line: 536 (after changing quantity)
        /// - RefreshSummons() in this file line: 630 (after refreshing)
        /// </remarks>
        private void UpdateMinionSlotsLabel()
        {
            float currentMinions = GetCurrentMinionCount();
            int maxMinions = Main.LocalPlayer.maxMinions;

            minionSlotsLabel.SetText($"Minion Slots: {currentMinions}/{maxMinions}");
        }

        /// <summary>
        /// Updates the quantity for a sentry panel by the specified change amount.
        /// </summary>
        /// <param name="panel">The sentry panel to update.</param>
        /// <param name="change">Amount to change (+1 or -1).</param>
        /// <remarks>
        /// This function:
        /// - Validates new quantity doesn't exceed maxTurrets
        /// - Updates quantity label text
        /// - Updates Fill/Unfill button state
        /// - Calls RecalculateFilledPanels and RefreshSummons
        ///
        /// Called from:
        /// - CreateSentryPanel() in this file line: 312, 319 (button callbacks)
        /// </remarks>
        public void UpdateSentryQuantity(UIPanel panel, int change)
        {
            var data = panel.GetTag<InteractionPanelData>();
            if (data == null) return;

            int maxSentries = Main.LocalPlayer.maxTurrets;     // Maximum allowed sentries
            int totalFromPanels = GetTotalSentries();          // Total sentry slots requested from all panels

            foreach (var element in panel.Children)
            {
                if (element is UIText quantityLabel)
                {
                    // Parse the current quantity from the label
                    string currentText = quantityLabel.Text.Replace("Sentries: ", "");
                    int currentQuantity = int.TryParse(currentText, out int parsedQuantity) ? parsedQuantity : 0;

                    // Calculate the new quantity
                    int newQuantity = Math.Max(0, currentQuantity + change);

                    // Validate against max sentries
                    if (newQuantity > 0 && totalFromPanels - currentQuantity + newQuantity > maxSentries)
                    {
                        return; // Stop processing if the limit is exceeded
                    }

                    // Update the quantity label
                    quantityLabel.SetText($"Sentries: {newQuantity}");

                    const int normalButtonWidth = 38;
                    const int unfillButtonWidth = 56;
                    const int spacing = 5;

                    if (change == -1)
                    {
                        data.FillButton.SetText("Fill"); // Update button text
                        data.FillButton.Width.Set(normalButtonWidth, 0f);
                        data.QuantityLabel.Left.Set(data.FillButton.Left.Pixels + normalButtonWidth + spacing, 0f);
                        data.FillButton.Recalculate();
                        data.QuantityLabel.Recalculate();
                        data.IsFilled = false; // Mark as not filled
                    }
                    else if (change == +1 && newQuantity == maxSentries)
                    {
                        // If incrementing and the quantity fills all remaining slots, mark the panel as filled
                        data.FillButton.SetText("Unfill");
                        data.FillButton.Width.Set(unfillButtonWidth, 0f);
                        data.QuantityLabel.Left.Set(data.FillButton.Left.Pixels + unfillButtonWidth + spacing, 0f);
                        data.FillButton.Recalculate();
                        data.QuantityLabel.Recalculate();
                        data.IsFilled = true;
                    }

                    // Recalculate and adjust quantities for all other filled panels
                    RecalculateFilledPanels();

                    // Trigger updated summoning/desummoning logic
                    RefreshSummons();
                    break;
                }
            }
        }

        /// <summary>
        /// Clears all existing summons and re-summons based on panel configurations.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Calls DesummonAllMinions and DesummonAllSentries to kill all summons
        /// - Calls SummonAllItems to re-summon based on panel configurations
        /// - Updates both minion and sentry slot labels
        /// - Triggers save via AutoSummonPlayer.TriggerSave()
        ///
        /// Called from:
        /// - CreateRespawnButton() in this file line: 197 (refresh button click)
        /// - UpdateQuantity() in this file line: 534 (after minion quantity change)
        /// - UpdateSentryQuantity() in this file line: 680 (after sentry quantity change)
        /// - HandleSentryPanelChanged() in this file line: 934 (when sentry slot changes)
        /// - HandleItemSlotChanged() in this file line: 1056 (when minion slot changes)
        /// - ToggleFill() in this file line: 1135 (when fill button clicked)
        /// - AutoSummonSystem.cs line: 131 (HandleSlotChange)
        /// </remarks>
        public void RefreshSummons()
        {
            // Clears and re-summons all minions and sentries
            DesummonAllMinions();
            DesummonAllSentries();
            SummonAllItems(Main.LocalPlayer);

            // Update the slots labels
            UpdateMinionSlotsLabel();
            UpdateSentrySlotsLabel();

            // Save panel data immediately when slots change
            AutoSummonPlayer.TriggerSave();
        }

        /// <summary>
        /// Updates the sentry slots label with current/max values.
        /// </summary>
        /// <remarks>
        /// Called from:
        /// - Update() in this file line: 831 (every frame)
        /// - RefreshSummons() in this file line: 631 (after refreshing)
        /// </remarks>
        private void UpdateSentrySlotsLabel()
        {
            int currentSentries = GetCurrentSentryCount();
            int maxSentries = Main.LocalPlayer.maxTurrets;

            sentrySlotsLabel?.SetText($"Sentry Slots: {currentSentries}/{maxSentries}");
        }

        /// <summary>
        /// Gets the total minion quantity from all minion panels.
        /// </summary>
        /// <returns>Sum of all minion quantities configured in panels.</returns>
        /// <remarks>
        /// Called from:
        /// - UpdateQuantity() in this file line: 494 (validation)
        /// - CreateInteractionPanel() in this file line: 693 (initial setup)
        /// - RecalculateFilledPanels() in this file line: 562 (recalculation)
        /// - ToggleFill() in this file line: 1123 (fill calculation)
        /// - HandleItemSlotChanged() in this file line: 991 (item change)
        /// - AutoSummonSystem.cs line: 70 (slot change handling)
        /// </remarks>
        public int GetTotalMinions()
        {
            int total = 0;
            foreach (var panel in interactionPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data != null && data.QuantityLabel != null)
                {
                    var currentText = data.QuantityLabel.Text;
                    int quantity = int.Parse(currentText.Replace("Minions: ", ""));
                    total += quantity;
                }
            }
            return total;
        }

        /// <summary>
        /// Gets the total sentry quantity from all sentry panels.
        /// </summary>
        /// <returns>Sum of all sentry quantities configured in panels.</returns>
        /// <remarks>
        /// Called from:
        /// - UpdateSentryQuantity() in this file line: 644 (validation)
        /// - RecalculateFilledPanels() in this file line: 589 (recalculation)
        /// - ToggleFill() in this file line: 1123 (fill calculation)
        /// - HandleSentryPanelChanged() in this file line: 832 (item change)
        /// - AutoSummonSystem.cs line: 78 (slot change handling)
        /// </remarks>
        public int GetTotalSentries()
        {
            int total = 0;
            foreach (var panel in sentryPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data != null && data.QuantityLabel != null)
                {
                    var currentText = data.QuantityLabel.Text;
                    int quantity = int.Parse(currentText.Replace("Sentries: ", ""));
                    total += quantity;
                }
            }
            return total;
        }

        /// <summary>
        /// Creates a minion interaction panel with item slot, buttons, and quantity label.
        /// </summary>
        /// <param name="index">The index position for the panel (0-based).</param>
        /// <param name="item">Optional item to pre-populate the slot with.</param>
        /// <param name="quantity">Optional starting quantity.</param>
        /// <remarks>
        /// This function:
        /// - Creates a 295x48 panel at position based on index
        /// - Creates a CustomItemSlot (38x38, 0.8f scale) with minion validation
        /// - Creates -1, +1, Fill buttons and quantity label (initially removed)
        /// - Stores InteractionPanelData tag on the panel
        /// - Calls UpdateMainPanelHeight and UpdateFillButtonText
        ///
        /// Called from:
        /// - AutoSummonPlayer.cs line: 458 (LoadPanelsFromSavedData)
        /// - AutoSummonPlayer.cs line: 390 (OnEnterWorld - default panel)
        /// - HandleItemSlotChanged() in this file line: 1032 (when adding new panel)
        /// </remarks>
        public void CreateInteractionPanel(int index = 0, Item item = null, int quantity = 0)
        {
            const int itemSlotSize = 38;  // Width/Height of the item slot
            const int buttonWidth = 38;  // Width of buttons
            const int labelWidth = 75;  // Width for the Minions label
            const int spacing = 5;      // Spacing between elements
            const int panelHeight = 48; // Height of the panel
            const int buttonHeight = 24; // Height of buttons
            const int labelHeight = 24; // Height of the Minions label

            // Create panel (starts at 68 = 50px label position + 18px for label height)
            var panel = new UIPanel
            {
                Width = { Pixels = 295 },
                Height = { Pixels = panelHeight },
                Top = { Pixels = 68 + index * 50 },
                BackgroundColor = new Color(35, 35, 50, 200)
            };
            panel.SetPadding(0); // Remove any padding
            mainPanel.Append(panel);

            int maxMinions = Main.LocalPlayer.maxMinions;
            float currentMinionCount = GetCurrentMinionCount();
            int totalFromPanels = GetTotalMinions();

            var summonSlot = new CustomItemSlot(ItemSlot.Context.InventoryItem, 0.8f)
            {
                Width = { Pixels = itemSlotSize },
                Height = { Pixels = itemSlotSize },
                Left = { Pixels = 3 },
                Top = { Pixels = 3 },
                IsValidItem = IsMinionSummoningItem // Validate only minion summoning items
            };
            panel.Append(summonSlot);
            summonSlot.SetPadding(0); // Remove padding

            lastItemStates[summonSlot] = summonSlot.Item.Clone();

            // Add -1 button
            var minusButton = CreateButton(
                "-1",
                new Vector2(summonSlot.Left.Pixels + itemSlotSize + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => UpdateQuantity(panel, -1),
                buttonWidth, buttonHeight);
            minusButton.Remove();

            // Add +1 button
            var plusButton = CreateButton(
                "+1",
                new Vector2(minusButton.Left.Pixels + buttonWidth + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => UpdateQuantity(panel, 1),
                buttonWidth, buttonHeight);
            plusButton.Remove();

            // Add Fill button
            var fillButton = CreateButton(
                "Fill",
                new Vector2(plusButton.Left.Pixels + buttonWidth + spacing, (panelHeight - buttonHeight) / 2),
                (evt, element) => ToggleFill(panel),
                buttonWidth, buttonHeight);
            fillButton.Remove();

            // Add quantity label with fine-tuned vertical alignment
            var quantityLabel = new UIText($"Minions: {quantity}", 0.8f)
            {
                Width = { Pixels = labelWidth },
                Height = { Pixels = labelHeight },
                Left = { Pixels = fillButton.Left.Pixels + buttonWidth + spacing },
                Top = { Pixels = (panelHeight - labelHeight) / 2 + 3 }
            };
            quantityLabel.Remove();

            // Store panel data
            panel.SetTag(new InteractionPanelData
            {
                MinusButton = minusButton,
                PlusButton = plusButton,
                FillButton = fillButton,
                QuantityLabel = quantityLabel,
                ItemSlot = summonSlot
            });
            if (item != null && !item.IsAir)
            {
                summonSlot.SetItem(item);
            }
            interactionPanels.Add(panel);
            UpdateMainPanelHeight();
            UpdateFillButtonText();
        }

        private bool IsMinionSummoningItem(Item item)
        {
            if (item == null || item.IsAir) return false;

            if (item.shoot > ProjectileID.None)
            {
                var projectile = new Projectile();
                projectile.SetDefaults(item.shoot);
                return projectile.minion && !projectile.sentry;
            }

            return false;
        }

        /// <summary>
        /// Called when the UI state is activated. Currently just calls base implementation.
        /// </summary>
        public override void OnActivate()
        {
            base.OnActivate();
        }

        /// <summary>
        /// Kills all active sentry projectiles owned by the local player.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Iterates through all projectiles
        /// - Kills projectiles that are active, owned by local player, and WipableTurret
        ///
        /// Called from:
        /// - RefreshSummons() in this file line: 712 (clearing before re-summon)
        /// </remarks>
        private void DesummonAllSentries()
        {
            int desummonedCount = 0;

            for (int i = 0; i < Main.projectile.Length; i++)
            {
                var proj = Main.projectile[i];

                // Check if the projectile is active, owned by the player, and is a sentry
                if (proj.active && proj.owner == Main.myPlayer && proj.WipableTurret)
                {
                    proj.Kill(); // Despawn the sentry
                    desummonedCount++;
                }
            }
        }

        /// <summary>
        /// Kills all active minion projectiles owned by the local player.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Iterates through all projectiles
        /// - Kills projectiles that are active, owned by local player, and minion
        ///
        /// Called from:
        /// - RefreshSummons() in this file line: 711 (clearing before re-summon)
        /// </remarks>
        private void DesummonAllMinions()
        {
            int desummonedCount = 0;

            for (int i = 0; i < Main.projectile.Length; i++)
            {
                var proj = Main.projectile[i];

                // Check if the projectile is active, owned by the player, and is a minion
                if (proj.active && proj.owner == Main.myPlayer && proj.minion)
                {
                    proj.Kill(); // Despawn the minion
                    desummonedCount++;
                }
            }
        }

        /// <summary>
        /// Called every frame. Updates labels, detects slot changes, handles dragging.
        /// </summary>
        /// <param name="gameTime">The current game time.</param>
        /// <remarks>
        /// This function:
        /// - Updates minion and sentry slot labels
        /// - Checks all minion panels for item changes and calls HandleItemSlotChanged
        /// - Checks all sentry panels for item changes and calls HandleSentryPanelChanged
        /// - Handles panel dragging logic when dragging flag is true
        ///
        /// Called automatically by tModLoader every frame when UI is visible.
        /// </remarks>
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            UpdateMinionSlotsLabel();
            UpdateSentrySlotsLabel();

            // Update Minion Panels
            for (int i = 0; i < interactionPanels.Count; i++)
            {
                var panel = interactionPanels[i];
                var data = panel.GetTag<InteractionPanelData>();
                var slot = data.ItemSlot;

                if (!lastItemStates.TryGetValue(slot, out var lastItem) ||
                    !lastItem.IsAir && slot.Item.IsAir ||
                    slot.Item.type != lastItem.type)
                {
                    lastItemStates[slot] = slot.Item.Clone();
                    HandleItemSlotChanged(i, slot.Item);
                }
            }

            // Update Sentry Panels
            for (int i = 0; i < sentryPanels.Count; i++)
            {
                var panel = sentryPanels[i];
                var data = panel.GetTag<InteractionPanelData>();
                var slot = data.ItemSlot;

                if (!lastItemStates.TryGetValue(slot, out var lastItem) ||
                    !lastItem.IsAir && slot.Item.IsAir ||
                    slot.Item.type != lastItem.type)
                {
                    lastItemStates[slot] = slot.Item.Clone();
                    HandleSentryPanelChanged(i, slot.Item);
                }
            }

            // Dragging Logic
            if (dragging)
            {
                mainPanel.Left.Pixels = Main.mouseX - offset.X;
                mainPanel.Top.Pixels = Main.mouseY - offset.Y;
                Recalculate();
            }
        }

        /// <summary>
        /// Handles changes to a sentry panel's item slot.
        /// </summary>
        /// <param name="index">The index of the panel that changed.</param>
        /// <param name="item">The new item in the slot.</param>
        /// <remarks>
        /// This function:
        /// - If valid sentry item: shows buttons/label, creates new panel if last
        /// - If empty slot: removes panel and recalculates positions
        /// - Calls UpdateMainPanelHeight and RefreshSummons
        ///
        /// Called from:
        /// - Update() in this file line: 1021 (when sentry slot item changes)
        /// </remarks>
        private void HandleSentryPanelChanged(int index, Item item)
        {
            var panel = sentryPanels[index];
            var data = panel.GetTag<InteractionPanelData>();

            if (item != null && !item.IsAir && IsSentrySummoningItem(item)) // Ensure it's a valid sentry item
            {
                // Determine starting quantity
                int maxSentries = Main.LocalPlayer.maxTurrets;
                int currentSentryCount = GetCurrentSentryCount();
                int totalFromPanels = GetTotalSentries();

                int startingQuantity = 0;

                int currentQuantity = int.Parse(data.QuantityLabel.Text.Replace("Sentries: ", ""));
                // Update the quantity label
                if (currentQuantity > 0)
                {
                    data.QuantityLabel.SetText($"Sentries: {currentQuantity}");
                }
                else
                {
                    data.QuantityLabel.SetText($"Sentries: {startingQuantity}");
                }

                // Item added: ensure buttons and label are visible
                data.MinusButton.Recalculate();
                data.PlusButton.Recalculate();
                data.FillButton.Recalculate();
                data.QuantityLabel.Recalculate();

                panel.Append(data.MinusButton);
                panel.Append(data.PlusButton);
                panel.Append(data.FillButton);
                panel.Append(data.QuantityLabel);

                // Dynamically add a new panel if this is the last one
                if (index == sentryPanels.Count - 1)
                {
                    CreateSentryPanel(sentryPanels.Count);
                }
                lastItemStates[data.ItemSlot] = item.Clone();
            }
            else if (lastItemStates[data.ItemSlot]?.IsAir == true)
            {
                // Only remove if the item is genuinely empty
                data.MinusButton.Remove();
                data.PlusButton.Remove();
                data.FillButton.Remove();
                data.QuantityLabel.Remove();

                sentryPanels.RemoveAt(index);
                mainPanel.RemoveChild(panel);

                // Recalculate positions of remaining panels
                for (int i = 0; i < sentryPanels.Count; i++)
                {
                    var remainingPanel = sentryPanels[i];
                    remainingPanel.Top.Set(68 + interactionPanels.Count * 50 + 24 + 18 + i * 50, 0f);
                }
            }
            UpdateMainPanelHeight();
            RefreshSummons();
            lastItemStates[data.ItemSlot] = item?.Clone();
        }

        /// <summary>
        /// Updates all Fill/Unfill button text based on current quantities vs max slots.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - For minion panels: sets "Unfill" if quantity equals maxMinions, else "Fill"
        /// - For sentry panels: sets "Unfill" if quantity equals maxTurrets, else "Fill"
        ///
        /// Called from:
        /// - CreateSentryPanel() in this file line: 356 (after creating panel)
        /// - CreateInteractionPanel() in this file line: 891 (after creating panel)
        /// </remarks>
        public void UpdateFillButtonText()
        {
            var player = Main.LocalPlayer;
            const int normalButtonWidth = 38;
            const int unfillButtonWidth = 56;
            const int spacing = 5;

            // Update Minion Panels
            foreach (var panel in interactionPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || data.QuantityLabel == null || data.FillButton == null)
                    continue;

                // Parse the quantity from the label
                int quantity = int.TryParse(
                    data.QuantityLabel.Text.Replace("Minions: ", ""),
                    out int parsedQuantity) ? parsedQuantity : 0;

                // Update the Fill/Unfill button based on the quantity
                if (quantity == player.maxMinions)
                {
                    data.FillButton.SetText("Unfill");
                    data.FillButton.Width.Set(unfillButtonWidth, 0f);
                    data.QuantityLabel.Left.Set(data.FillButton.Left.Pixels + unfillButtonWidth + spacing, 0f);
                    data.IsFilled = true;
                }
                else
                {
                    data.FillButton.SetText("Fill");
                    data.FillButton.Width.Set(normalButtonWidth, 0f);
                    data.QuantityLabel.Left.Set(data.FillButton.Left.Pixels + normalButtonWidth + spacing, 0f);
                    data.IsFilled = false;
                }
                data.FillButton.Recalculate();
                data.QuantityLabel.Recalculate();
            }

            // Update Sentry Panels
            foreach (var panel in sentryPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || data.QuantityLabel == null || data.FillButton == null)
                    continue;

                // Parse the quantity from the label
                int quantity = int.TryParse(
                    data.QuantityLabel.Text.Replace("Sentries: ", ""),
                    out int parsedQuantity) ? parsedQuantity : 0;

                // Update the Fill/Unfill button based on the quantity
                if (quantity == player.maxTurrets)
                {
                    data.FillButton.SetText("Unfill");
                    data.FillButton.Width.Set(unfillButtonWidth, 0f);
                    data.QuantityLabel.Left.Set(data.FillButton.Left.Pixels + unfillButtonWidth + spacing, 0f);
                    data.IsFilled = true;
                }
                else
                {
                    data.FillButton.SetText("Fill");
                    data.FillButton.Width.Set(normalButtonWidth, 0f);
                    data.QuantityLabel.Left.Set(data.FillButton.Left.Pixels + normalButtonWidth + spacing, 0f);
                    data.IsFilled = false;
                }
                data.FillButton.Recalculate();
                data.QuantityLabel.Recalculate();
            }
        }

        /// <summary>
        /// Handles changes to a minion panel's item slot.
        /// </summary>
        /// <param name="index">The index of the panel that changed.</param>
        /// <param name="item">The new item in the slot.</param>
        /// <remarks>
        /// This function:
        /// - If valid minion item: shows buttons/label, creates new panel if last
        /// - If empty/invalid: removes panel and recalculates positions
        /// - Calls RefreshSummons and RecalculateSentryPanelPositions
        ///
        /// Called from:
        /// - Update() in this file line: 1009 (when minion slot item changes)
        /// </remarks>
        private void HandleItemSlotChanged(int index, Item item)
        {
            var panel = interactionPanels[index];
            var data = panel.GetTag<InteractionPanelData>();

            if (item != null && !item.IsAir && IsMinionSummoningItem(item)) // Ensure it's a valid minion item
            {
                // Determine starting quantity
                int maxMinions = Main.LocalPlayer.maxMinions;
                float currentMinionCount = GetCurrentMinionCount();
                int totalFromPanels = GetTotalMinions();

                int startingQuantity = 0;

                int currentQuantity = int.Parse(data.QuantityLabel.Text.Replace("Minions: ", ""));

                // Update the quantity label
                if (currentQuantity > 0)
                {
                    data.QuantityLabel.SetText($"Minions: {currentQuantity}");
                }
                else
                {
                    data.QuantityLabel.SetText($"Minions: {startingQuantity}");
                }


                // Item added: ensure buttons and label are visible
                data.MinusButton.Recalculate();
                data.PlusButton.Recalculate();
                data.FillButton.Recalculate();
                data.QuantityLabel.Recalculate();

                panel.Append(data.MinusButton);
                panel.Append(data.PlusButton);
                panel.Append(data.FillButton);
                panel.Append(data.QuantityLabel);

                // Dynamically add a new panel if this is the last one
                if (index == interactionPanels.Count - 1)
                {
                    CreateInteractionPanel(interactionPanels.Count);
                }
                lastItemStates[data.ItemSlot] = item.Clone();
            }
            else
            {
                // Remove invalid item and reset the panel
                data.MinusButton.Remove();
                data.PlusButton.Remove();
                data.FillButton.Remove();
                data.QuantityLabel.Remove();

                interactionPanels.RemoveAt(index);
                mainPanel.RemoveChild(panel);

                // Recalculate positions of remaining panels
                for (int i = 0; i < interactionPanels.Count; i++)
                {
                    var remainingPanel = interactionPanels[i];
                    remainingPanel.Top.Set(68 + i * 50, 0f);
                }

                UpdateMainPanelHeight();
            }
            RefreshSummons();
            lastItemStates[data.ItemSlot] = item?.Clone();
            RecalculateSentryPanelPositions();
        }

        /// <summary>
        /// Creates a UITextButton with specified text, position, and click action.
        /// </summary>
        /// <param name="text">The button text.</param>
        /// <param name="position">The position (Left, Top) in pixels.</param>
        /// <param name="action">The click event handler.</param>
        /// <param name="width">Button width in pixels (default 40).</param>
        /// <param name="height">Button height in pixels (default 26).</param>
        /// <returns>The created UITextButton.</returns>
        /// <remarks>
        /// Called from:
        /// - CreateSentryPanel() in this file line: 309-325 (creating panel buttons)
        /// - CreateInteractionPanel() in this file line: 855-875 (creating panel buttons)
        /// </remarks>
        private UITextButton CreateButton(string text, Vector2 position, UIElement.MouseEvent action, int width = 40, int height = 26)
        {
            var button = new UITextButton(text, 0.85f)
            {
                Width = { Pixels = width },
                Height = { Pixels = height },
                Left = { Pixels = position.X },
                Top = { Pixels = position.Y }
            };
            button.OnLeftClick += action;
            return button;
        }

        /// <summary>
        /// Toggles the fill state for a panel (fills all remaining slots or unfills to 0).
        /// </summary>
        /// <param name="panel">The panel to toggle.</param>
        /// <remarks>
        /// This function:
        /// - Determines if panel is minion or sentry type
        /// - If already filled: resets quantity to 0, sets button to "Fill" (38px)
        /// - If not filled: unfills other panels, fills this one to max, sets "Unfill" (52px)
        /// - Calls RefreshSummons to apply changes
        ///
        /// Called from:
        /// - CreateSentryPanel() in this file line: 323 (fill button callback)
        /// - CreateInteractionPanel() in this file line: 873 (fill button callback)
        /// </remarks>
        public void ToggleFill(UIPanel panel)
        {
            var player = Main.LocalPlayer;

            // Determine if it's a minion or sentry panel
            bool isSentryPanel = sentryPanels.Contains(panel);

            // Get the InteractionPanelData for the selected panel
            var data = panel.GetTag<InteractionPanelData>();
            if (data == null || data.ItemSlot.Item == null || data.ItemSlot.Item.IsAir)
                return;

            // Parse the current quantity for this panel
            int currentQuantity = int.Parse(data.QuantityLabel.Text.Replace(isSentryPanel ? "Sentries: " : "Minions: ", ""));

            const int normalButtonWidth = 38;
            const int unfillButtonWidth = 56;
            const int spacing = 5;

            // If the panel is already filled
            if (data.IsFilled)
            {
                // Unfill: Reset the quantity to 0
                data.QuantityLabel.SetText($"{(isSentryPanel ? "Sentries" : "Minions")}: 0");
                data.FillButton.SetText("Fill"); // Update button label
                data.FillButton.Width.Set(normalButtonWidth, 0f); // Reset to original width
                data.QuantityLabel.Left.Set(data.FillButton.Left.Pixels + normalButtonWidth + spacing, 0f); // Move label back
                data.FillButton.Recalculate();
                data.QuantityLabel.Recalculate();
                data.IsFilled = false; // Mark as unfilled
            }
            else
            {
                // Unfill other panels first to ensure only one is filled
                var relevantPanels = isSentryPanel ? sentryPanels : interactionPanels;
                foreach (var otherPanel in relevantPanels)
                {
                    if (otherPanel == panel)
                        continue;

                    var otherData = otherPanel.GetTag<InteractionPanelData>();
                    if (otherData != null && otherData.IsFilled)
                    {
                        otherData.QuantityLabel.SetText($"{(isSentryPanel ? "Sentries" : "Minions")}: 0");
                        otherData.FillButton.SetText("Fill");
                        otherData.FillButton.Width.Set(normalButtonWidth, 0f); // Reset to original width
                        otherData.QuantityLabel.Left.Set(otherData.FillButton.Left.Pixels + normalButtonWidth + spacing, 0f); // Move label back
                        otherData.FillButton.Recalculate();
                        otherData.QuantityLabel.Recalculate();
                        otherData.IsFilled = false;
                    }
                }

                // Fill the current panel
                int maxSlots = isSentryPanel ? player.maxTurrets : player.maxMinions;
                int totalUsedSlots = isSentryPanel ? GetTotalSentries() : GetTotalMinions();
                int remainingSlots = maxSlots - (totalUsedSlots - currentQuantity);
                int newQuantity = Math.Min(remainingSlots, maxSlots);

                data.QuantityLabel.SetText($"{(isSentryPanel ? "Sentries" : "Minions")}: {newQuantity}");
                data.FillButton.SetText("Unfill"); // Update button label
                data.FillButton.Width.Set(unfillButtonWidth, 0f); // Make button wider for "Unfill"
                data.QuantityLabel.Left.Set(data.FillButton.Left.Pixels + unfillButtonWidth + spacing, 0f); // Move label right
                data.FillButton.Recalculate();
                data.QuantityLabel.Recalculate();
                data.IsFilled = true; // Mark as filled
            }

            // Recalculate and resummon
            RefreshSummons();
        }

        /// <summary>
        /// Removes all minion and sentry panels from the UI.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Removes all panels from mainPanel as children
        /// - Clears both interactionPanels and sentryPanels lists
        ///
        /// Called from:
        /// - AutoSummonPlayer.cs line: 447 (LoadPanelsFromSavedData - before loading)
        /// </remarks>
        public void ClearPanels()
        {
            foreach (var panel in interactionPanels)
            {
                mainPanel.RemoveChild(panel);
            }
            foreach (var panel in sentryPanels)
            {
                mainPanel.RemoveChild(panel);
            }
            interactionPanels.Clear();
            sentryPanels.Clear();
        }

        /// <summary>
        /// Starts dragging the panel when left mouse button is pressed on header.
        /// </summary>
        /// <remarks>
        /// Called from:
        /// - OnInitialize() in this file line: 120 (headerBar.OnLeftMouseDown event)
        /// </remarks>
        private void StartDrag(UIMouseEvent evt, UIElement listeningElement)
        {
            dragging = true;
            offset = evt.MousePosition - new Vector2(mainPanel.Left.Pixels, mainPanel.Top.Pixels);
        }

        /// <summary>
        /// Ends dragging the panel when left mouse button is released.
        /// </summary>
        /// <remarks>
        /// Called from:
        /// - OnInitialize() in this file line: 121 (headerBar.OnLeftMouseUp event)
        /// </remarks>
        private void EndDrag(UIMouseEvent evt, UIElement listeningElement)
        {
            dragging = false;
        }

        /// <summary>
        /// Updates the main panel height based on number of minion and sentry panels.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Calculates height: baseHeight (69) + minionPanels*50 + sentryPanels*50 + 24 + bottomPadding (11)
        /// - Positions sentrySlotsLabel below minion panels
        /// - Calls mainPanel.Recalculate()
        ///
        /// Called from:
        /// - CreateSentryPanel() in this file line: 354 (after adding panel)
        /// - CreateInteractionPanel() in this file line: 890 (after adding panel)
        /// - HandleSentryPanelChanged() in this file line: 1119 (after panel changes)
        /// - HandleItemSlotChanged() in this file line: 1264 (after panel changes)
        /// - AutoSummonPlayer.cs line: 398 (OnEnterWorld)
        /// </remarks>
        public void UpdateMainPanelHeight()
        {
            const int baseHeight = 81; // Header (29) + gap (21) + minion label (18) + small gap (2) + top padding (11)
            const int panelHeight = 50;
            const int bottomPadding = 11; // Same as top padding
            const int gapBeforeSentryLabel = 24; // Gap above sentry label
            const int sentryLabelHeight = 18;

            // Calculate total height for minion and sentry panels
            int minionHeight = interactionPanels.Count * panelHeight;
            int sentryHeight = sentryPanels.Count * panelHeight;

            // Update mainPanel height: base + minion panels + gap before sentry label + sentry label + sentry panels + bottom padding
            mainPanel.Height.Set(baseHeight + minionHeight + gapBeforeSentryLabel + sentryLabelHeight + sentryHeight + bottomPadding, 0f);

            // Position the sentry label dynamically below the minion panels (with gap above it)
            sentrySlotsLabel.Top.Set(68 + minionHeight + gapBeforeSentryLabel, 0f);

            // Recalculate the layout
            mainPanel.Recalculate();
        }

        /// <summary>
        /// Recalculates the top positions of all sentry panels based on minion panel count.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Sets each sentry panel's top position based on: 56 + minionPanels*50 + 24 + index*50
        /// - Recalculates each panel and the main panel
        ///
        /// Called from:
        /// - HandleItemSlotChanged() in this file line: 1268 (after minion panel changes)
        /// - AutoSummonPlayer.cs line: 397 (OnEnterWorld)
        /// </remarks>
        public void RecalculateSentryPanelPositions()
        {
            const int panelHeight = 50;
            const int gapBeforeSentryLabel = 24;
            const int sentryLabelHeight = 18;

            for (int i = 0; i < sentryPanels.Count; i++)
            {
                var panel = sentryPanels[i];
                // 68 = minion panels start, + minion panels height + gap before sentry label + sentry label height
                panel.Top.Set(68 + interactionPanels.Count * panelHeight + gapBeforeSentryLabel + sentryLabelHeight + i * panelHeight, 0f);
                panel.Recalculate();
            }

            mainPanel.Recalculate();
        }

        /// <summary>
        /// Summons all configured minions and sentries for the player.
        /// </summary>
        /// <param name="player">The player to summon for.</param>
        /// <remarks>
        /// This function:
        /// - Iterates through all minion panels and summons configured quantity
        /// - Iterates through all sentry panels and summons configured quantity
        /// - Uses AutoSummonSystem.SummonWithItem for actual summoning
        ///
        /// Called from:
        /// - RefreshSummons() in this file line: 713 (after clearing summons)
        /// - AutoSummonPlayer.cs line: 564 (SummonAllItemsIfPossible)
        /// </remarks>
        public void SummonAllItems(Player player)
        {
            foreach (var panel in interactionPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || data.ItemSlot.Item == null || data.ItemSlot.Item.IsAir)
                    continue;

                var summonItem = data.ItemSlot.Item;

                int quantity = int.Parse(data.QuantityLabel.Text.Replace("Minions: ", ""));


                for (int i = 0; i < quantity; i++)
                {
                    AutoSummonSystem.SummonWithItem(player, summonItem);
                }
            }

            // Summon Sentries
            foreach (var panel in sentryPanels)
            {
                var data = panel.GetTag<InteractionPanelData>();
                if (data == null || data.ItemSlot.Item == null || data.ItemSlot.Item.IsAir)
                    continue;

                var summonItem = data.ItemSlot.Item;
                int quantity = int.Parse(data.QuantityLabel.Text.Replace("Sentries: ", ""));

                for (int i = 0; i < quantity; i++)
                {
                    AutoSummonSystem.SummonWithItem(player, summonItem);
                }
            }
        }

        /// <summary>
        /// Static method to check and respawn sentries that have gone off-screen.
        /// </summary>
        /// <param name="player">The player whose sentries to check.</param>
        /// <remarks>
        /// This function:
        /// - Creates screen bounds rectangle with 100px padding
        /// - Iterates through all active sentries owned by the player
        /// - Kills and respawns any sentry outside the screen bounds
        /// - Respawns sentry at player's position with same type/damage/knockback
        ///
        /// Note: This static method is not currently called - sentry respawn is handled
        /// in AutoSummonPlayer.PostUpdate() instead.
        /// </remarks>
        public static void CheckAndRespawnSentries(Player player)
        {
            // Screen bounds (expanded slightly to include buffer space for "off-screen" detection)
            int screenPadding = 100; // Add padding to screen bounds
            Rectangle screenBounds = new Rectangle(
                (int)(Main.screenPosition.X - screenPadding),
                (int)(Main.screenPosition.Y - screenPadding),
                Main.screenWidth + screenPadding * 2,
                Main.screenHeight + screenPadding * 2
            );

            // Iterate through all projectiles to find sentries
            foreach (Projectile proj in Main.projectile)
            {
                if (proj.active && proj.owner == player.whoAmI && proj.sentry)
                {
                    // Check if the sentry is off-screen
                    if (!screenBounds.Contains(proj.Center.ToPoint()))
                    {
                        // Kill the old sentry
                        proj.Kill();

                        // Respawn the sentry at the player's position
                        Projectile.NewProjectile(
                            player.GetSource_Misc("RespawnSentry"), // Source for respawn
                            player.Center,                         // Respawn at player's position
                            Vector2.Zero,                          // No velocity
                            proj.type,                             // Same projectile type
                            proj.originalDamage,                   // Use original damage
                            proj.knockBack,                        // Use original knockback
                            player.whoAmI                          // Owner (player)
                        );
                    }
                }
            }
        }

    }

    /// <summary>
    /// Custom UIPanel-based button with centered text.
    /// </summary>
    /// <remarks>
    /// Used throughout DraggableUIPanel for -1, +1, Fill buttons.
    /// Created via CreateButton() method.
    /// </remarks>
    public class UITextButton : UIPanel
    {
        /// <summary>The text element displayed in the button.</summary>
        private UIText buttonText;

        /// <summary>
        /// Creates a new text button with the specified text and scale.
        /// </summary>
        /// <param name="text">The button text.</param>
        /// <param name="textScale">The text scale (default 1f).</param>
        public UITextButton(string text, float textScale = 1f)
        {
            Width.Set(100, 0);
            Height.Set(40, 0);
            BackgroundColor = new Color(63, 82, 151) * 0.7f;

            buttonText = new UIText(text, textScale)
            {
                HAlign = 0.5f,
                VAlign = 0.5f
            };
            Append(buttonText);
        }

        /// <summary>
        /// Updates the button's text.
        /// </summary>
        /// <param name="text">The new text to display.</param>
        public void SetText(string text) => buttonText.SetText(text);
    }

    /// <summary>
    /// Data class storing references to UI elements for each interaction panel.
    /// </summary>
    /// <remarks>
    /// Stored as a tag on each UIPanel via UIExtensions.SetTag().
    /// Retrieved via UIExtensions.GetTag&lt;InteractionPanelData&gt;().
    /// </remarks>
    public class InteractionPanelData
    {
        /// <summary>The -1 button for decreasing quantity.</summary>
        public UITextButton MinusButton { get; set; }
        /// <summary>The +1 button for increasing quantity.</summary>
        public UITextButton PlusButton { get; set; }
        /// <summary>The Fill/Unfill toggle button.</summary>
        public UITextButton FillButton { get; set; }
        /// <summary>The label showing "Minions: X" or "Sentries: X".</summary>
        public UIText QuantityLabel { get; set; }
        /// <summary>The item slot containing the summon weapon.</summary>
        public CustomItemSlot ItemSlot { get; set; }
        /// <summary>Tracks whether this panel is in "filled" state (auto-fills remaining slots).</summary>
        public bool IsFilled { get; set; } = false;
    }

    /// <summary>
    /// Extension methods for attaching arbitrary data to UIElement instances.
    /// </summary>
    /// <remarks>
    /// Uses a static dictionary to associate tags with UI elements.
    /// Used to store InteractionPanelData on each panel.
    /// </remarks>
    public static class UIExtensions
    {
        /// <summary>Static dictionary storing tags for each UIElement.</summary>
        private static readonly Dictionary<UIElement, object> Tags = new();

        /// <summary>
        /// Sets a tag value on a UIElement.
        /// </summary>
        /// <typeparam name="T">The type of the tag.</typeparam>
        /// <param name="element">The UI element to tag.</param>
        /// <param name="tag">The tag value to store.</param>
        public static void SetTag<T>(this UIElement element, T tag) => Tags[element] = tag;

        /// <summary>
        /// Gets a tag value from a UIElement.
        /// </summary>
        /// <typeparam name="T">The expected type of the tag.</typeparam>
        /// <param name="element">The UI element to get the tag from.</param>
        /// <returns>The tag value, or default(T) if not found or wrong type.</returns>
        public static T GetTag<T>(this UIElement element) =>
            Tags.TryGetValue(element, out var tag) && tag is T value ? value : default;
    }


}
