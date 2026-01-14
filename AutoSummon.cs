// ============================================================================
// File: AutoSummon.cs
// Description: Main mod system entry point. Manages UI lifecycle, keybind
//              registration, and provides static references for other classes.
// ============================================================================
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using Microsoft.Xna.Framework;
using Terraria.GameInput;
using AutoSummon.UI;
using Terraria.ID;

namespace AutoSummon
{
    /// <summary>
    /// Main ModSystem class for the AutoSummon mod.
    /// Handles UI initialization, keybind registration, and UI rendering.
    /// </summary>
    /// <remarks>
    /// This class is used in the following locations:
    /// - AutoSummonPlayer.cs line: 126, 205, 253, 333, 367 (accesses DraggableUIPanelInstance)
    /// - AutoSummonPlayer.cs line: 203 (accesses Instance for HideUI)
    /// - AutoSummonSystem.cs line: 28, 84 (accesses DraggableUIPanelInstance)
    /// </remarks>
    public class AutoSummon : ModSystem
    {
        /// <summary>
        /// The UserInterface instance that manages UI state and interactions.
        /// </summary>
        private UserInterface draggableUI;

        /// <summary>
        /// The main UI panel instance (local reference).
        /// </summary>
        private DraggableUIPanel draggableUIPanel;

        /// <summary>
        /// Static reference to the DraggableUIPanel for access from other classes.
        /// </summary>
        /// <remarks>
        /// Used in:
        /// - AutoSummonPlayer.cs line: 126 (CollectPanelData)
        /// - AutoSummonPlayer.cs line: 205, 253 (OnEnterWorld, LoadPanelsFromSavedData)
        /// - AutoSummonPlayer.cs line: 333, 367 (SummonAllItemsIfPossible, RefreshSentries)
        /// - AutoSummonSystem.cs line: 28 (PostUpdateEverything)
        /// - AutoSummonSystem.cs line: 84 (HandleSlotChange)
        /// </remarks>
        public static DraggableUIPanel DraggableUIPanelInstance;

        /// <summary>
        /// The keybind used to toggle the UI panel visibility.
        /// Default key: K
        /// </summary>
        private static ModKeybind toggleUIKeybind;

        /// <summary>
        /// Tracks whether the UI is currently visible.
        /// </summary>
        private bool uiVisible;

        /// <summary>
        /// Static instance of this ModSystem for access from other classes.
        /// </summary>
        /// <remarks>
        /// Used in:
        /// - AutoSummonPlayer.cs line: 203 (calls Instance.HideUI())
        /// </remarks>
        public static AutoSummon Instance;

        /// <summary>
        /// Called when the mod is loaded. Initializes UI and keybind.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Creates the DraggableUIPanel and UserInterface instances
        /// - Assigns static references (DraggableUIPanelInstance, Instance)
        /// - Registers the "Toggle UI" keybind with default key "K"
        /// - Only runs on client (skipped on dedicated server)
        /// </remarks>
        public override void OnModLoad()
        {
            Instance = this;

            if (!Main.dedServ)
            {
                draggableUIPanel = new DraggableUIPanel();
                draggableUI = new UserInterface();
                draggableUI.SetState(draggableUIPanel);

                // Assign the static reference
                DraggableUIPanelInstance = draggableUIPanel;

                // Register keybind
                toggleUIKeybind = KeybindLoader.RegisterKeybind(Mod, "Toggle UI", "K");
            }
        }

        /// <summary>
        /// Called when the mod is unloaded. Cleans up all references.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Sets all UI references to null to allow garbage collection
        /// - Clears static references (DraggableUIPanelInstance, Instance, toggleUIKeybind)
        /// </remarks>
        public override void Unload()
        {
            draggableUIPanel = null;
            draggableUI = null;
            toggleUIKeybind = null;
            DraggableUIPanelInstance = null;
            Instance = null;
        }

        /// <summary>
        /// Hides the UI panel programmatically.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Sets uiVisible to false
        /// - Sets the UserInterface state to null to hide the panel
        ///
        /// Used in:
        /// - AutoSummonPlayer.cs line: 203 (OnEnterWorld - hides UI when entering a world)
        /// </remarks>
        public void HideUI()
        {
            uiVisible = false;
            draggableUI?.SetState(null);
        }

        /// <summary>
        /// Called every frame to update UI and check for keybind input.
        /// </summary>
        /// <param name="gameTime">The current game time.</param>
        /// <remarks>
        /// This function:
        /// - Updates the UI if it's visible
        /// - Checks if the toggle keybind was just pressed and toggles UI visibility
        /// </remarks>
        public override void UpdateUI(GameTime gameTime)
        {
            if (uiVisible)
            {
                draggableUI?.Update(gameTime);
            }

            if (toggleUIKeybind?.JustPressed == true)
            {
                ToggleUI();
            }
        }

        /// <summary>
        /// Inserts the mod's UI layer into the game's interface layer system.
        /// </summary>
        /// <param name="layers">The list of game interface layers.</param>
        /// <remarks>
        /// This function:
        /// - Finds the "Vanilla: Inventory" layer
        /// - Inserts the AutoSummon UI layer just after the inventory layer
        /// - Only draws the UI when uiVisible is true
        /// </remarks>
        public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
        {
            int inventoryLayerIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryLayerIndex != -1)
            {
                layers.Insert(inventoryLayerIndex + 1, new LegacyGameInterfaceLayer(
                    "AutoSummon: Draggable UI",
                    delegate
                    {
                        if (uiVisible)
                        {
                            draggableUI?.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }

        /// <summary>
        /// Toggles the UI panel visibility on/off.
        /// </summary>
        /// <remarks>
        /// This function:
        /// - Inverts the uiVisible flag
        /// - Sets the UserInterface state to the panel (show) or null (hide)
        ///
        /// Called from:
        /// - UpdateUI() in this file line: 63 (when keybind is pressed)
        /// </remarks>
        private void ToggleUI()
        {
            uiVisible = !uiVisible;
            if (uiVisible)
            {
                draggableUI.SetState(draggableUIPanel); // Show UI
            }
            else
            {
                draggableUI.SetState(null); // Hide UI
            }
        }
    }
}
