// ============================================================================
// File: AutoSummonConfig.cs
// Description: Configuration settings for the AutoSummon mod.
//              Allows players to customize auto-summon behavior through
//              tModLoader's mod configuration menu.
// ============================================================================
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace AutoSummon
{
    /// <summary>
    /// Configuration class for AutoSummon mod settings.
    /// Provides user-configurable options for auto-summon behavior.
    /// </summary>
    /// <remarks>
    /// This class is used in the following locations:
    /// - AutoSummonPlayer.cs line: 401 (OnEnterWorld - reads AutoSummonOnWorldEnter)
    /// - AutoSummonPlayer.cs line: 586 (OnRespawn - reads AutoSummonOnRespawn)
    /// </remarks>
    public class AutoSummonConfig : ModConfig
    {
        /// <summary>
        /// Specifies this is a client-side configuration (per-player, not server-wide).
        /// </summary>
        public override ConfigScope Mode => ConfigScope.ClientSide;

        /// <summary>
        /// When enabled, minions and sentries are automatically summoned when the player respawns.
        /// Default: true
        /// </summary>
        /// <remarks>
        /// Used in:
        /// - AutoSummonPlayer.cs line: 586 (checked in OnRespawn to set isWaitingForRespawn)
        /// </remarks>
        [Header("GeneralSettings")]
        [DefaultValue(true)]
        [Tooltip("When enabled, minions and sentries will automatically be summoned when you respawn")]
        public bool AutoSummonOnRespawn;

        /// <summary>
        /// When enabled, minions and sentries are automatically summoned when entering a world.
        /// Default: true
        /// </summary>
        /// <remarks>
        /// Used in:
        /// - AutoSummonPlayer.cs line: 401 (checked in OnEnterWorld to trigger SummonAllItemsIfPossible)
        /// </remarks>
        [DefaultValue(true)]
        [Tooltip("When enabled, minions and sentries will automatically be summoned when you enter a world")]
        public bool AutoSummonOnWorldEnter;
    }
}
