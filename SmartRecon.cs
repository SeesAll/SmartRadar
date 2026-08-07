using Facepunch;
using HarmonyLib;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using Rust.Ai;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SmartRecon", "SeesAll", "2.1.1")]
    [Description("Unified administrative reconnaissance, vanish, radar, inspection, and rapid movement for Rust")]
    public class SmartRecon : RustPlugin
    {
        #region Constants and references

        private const string PermUse = "smartrecon.use";
        private const string PermPlayers = "smartrecon.players";
        private const string PermStashes = "smartrecon.stashes";
        private const string PermCupboards = "smartrecon.cupboards";
        private const string PermArrows = "smartrecon.arrows";
        private const string PermVoice = "smartrecon.voice";
        private const string PermSleepers = "smartrecon.sleepers";
        private const string PermExtendedRange = "smartrecon.extendedrange";
        private const string PermSeeVanished = "smartrecon.seevanished";
        private const string PermSeeOwners = "smartrecon.seeowners";
        private const string PermVanish = "smartrecon.vanish";
        private const string PermVanishPermanent = "smartrecon.vanish.permanent";
        private const string PermVanishUnlock = "smartrecon.vanish.unlock";
        private const string PermVanishDamage = "smartrecon.vanish.damage";
        private const string PermVanishInventory = "smartrecon.vanish.inventory";
        private const string PermVanishTeleport = "smartrecon.vanish.teleport";
        private const string PermNpcs = "smartrecon.npcs";
        private const string PermLoot = "smartrecon.loot";
        private const string PermExtended = "smartrecon.extended";
        private const string PermTcInfo = "smartrecon.tcinfo";
        private const string PermUi = "smartrecon.ui";
        private const string PermForensics = "smartrecon.forensics";
        private const string ModePlayers = "players";
        private const string ModeStashes = "stashes";
        private const string ModeCupboards = "tcs";
        private const string ModeAll = "all";
        private const string ModeCustom = "custom";
        private const string RadarUiName = "SmartRecon.InvestigationUI";
        private const string DefaultUiAnchorMin = "0.835 0.305";
        private const string DefaultUiAnchorMax = "0.985 0.695";
        private const string PreviousDefaultUiAnchorMin = "0.815 0.275";
        private const string PreviousDefaultUiAnchorMax = "0.985 0.725";

        #endregion

        #region Runtime state

        private PluginConfig _config;
        private DynamicConfigFile _dataFile;
        private StoredData _storedData;
        private bool _dataDirty;
        private bool _serverInitialized;

        private readonly Dictionary<ulong, RadarSession> _sessions = new Dictionary<ulong, RadarSession>();
        private readonly Dictionary<ulong, float> _voiceActivity = new Dictionary<ulong, float>();

        private readonly Dictionary<long, List<BasePlayer>> _activePlayerIndex = new Dictionary<long, List<BasePlayer>>();
        private readonly Dictionary<long, List<BasePlayer>> _sleepingPlayerIndex = new Dictionary<long, List<BasePlayer>>();
        private readonly Dictionary<long, List<StashContainer>> _stashIndex = new Dictionary<long, List<StashContainer>>();
        private readonly Dictionary<long, List<BuildingPrivlidge>> _cupboardIndex = new Dictionary<long, List<BuildingPrivlidge>>();
        private readonly Dictionary<long, List<BaseEntity>> _lootIndex = new Dictionary<long, List<BaseEntity>>();
        private readonly Dictionary<long, List<BaseEntity>> _npcEntityIndex = new Dictionary<long, List<BaseEntity>>();
        private readonly Dictionary<int, BaseEntity> _trackedNpcEntities = new Dictionary<int, BaseEntity>();
        private readonly Dictionary<int, long> _stashCells = new Dictionary<int, long>();
        private readonly Dictionary<int, long> _cupboardCells = new Dictionary<int, long>();
        private readonly Dictionary<int, long> _lootCells = new Dictionary<int, long>();

        private readonly List<PlayerCandidate> _playerCandidates = new List<PlayerCandidate>(256);
        private readonly List<StaticCandidate<StashContainer>> _stashCandidates = new List<StaticCandidate<StashContainer>>(256);
        private readonly List<StaticCandidate<BuildingPrivlidge>> _cupboardCandidates = new List<StaticCandidate<BuildingPrivlidge>>(256);
        private readonly List<StaticCandidate<BaseEntity>> _lootCandidates = new List<StaticCandidate<BaseEntity>>(256);
        private readonly List<StaticCandidate<BaseEntity>> _npcEntityCandidates = new List<StaticCandidate<BaseEntity>>(128);
        private readonly List<ulong> _sessionIterationBuffer = new List<ulong>();
        private readonly List<ulong> _sessionRemovalBuffer = new List<ulong>();
        private readonly List<int> _npcRemovalBuffer = new List<int>();
        private readonly StringBuilder _labelBuilder = new StringBuilder(256);
        private readonly Dictionary<ulong, string> _teamColorCache = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, VanishCacheEntry> _vanishStateCache = new Dictionary<ulong, VanishCacheEntry>();
        private readonly HashSet<ulong> _vanishedPlayers = new HashSet<ulong>();
        private readonly Dictionary<ulong, VanishRuntimeState> _vanishRuntime = new Dictionary<ulong, VanishRuntimeState>();
        private readonly Dictionary<ulong, float> _forensicCooldowns = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _mapTeleportCooldowns = new Dictionary<ulong, float>();

        private float _nextPlayerIndexRebuild;
        private float _nextSleeperIndexRebuild;
        private float _nextStaticIndexRebuild;
        private float _nextVoicePrune;
        private float _nextSpectateReconcile;
        private int _staggerSequence;
        private int _schedulerCursor;
        private int _voiceWatcherCount;
        private int _vanishFeedbackEffectDepth;
        private bool _vanishHooksSubscribed;
        private bool _networkGroupCompatibilityResolved;
        private bool _networkGroupCompatibilityWarningShown;
        private MethodInfo _networkUpdateGroupsMethod;
        private MemberInfo _playerNetworkRangeMember;

        private static SmartRecon Instance;

        private Color _playerDrawColor;
        private Color _stashDrawColor;
        private Color _cupboardDrawColor;
        private Color _arrowDrawColor;
        private Color _lootDrawColor;
        private Color _npcDrawColor;

        #endregion

        #region Configuration

        private sealed class PluginConfig
        {
            [JsonProperty("General settings")]
            public GeneralSettings General = new GeneralSettings();

            [JsonProperty("Scheduler and spatial index")]
            public SchedulerSettings Scheduler = new SchedulerSettings();

            [JsonProperty("Result limits")]
            public LimitSettings Limits = new LimitSettings();

            [JsonProperty("Display settings")]
            public DisplaySettings Display = new DisplaySettings();

            [JsonProperty("Vanish and owner privacy")]
            public PrivacySettings Privacy = new PrivacySettings();

            [JsonProperty("Built-in vanish")]
            public VanishSettings Vanish = new VanishSettings();

            [JsonProperty("Investigative vanish and radar workflow")]
            public InvestigationSettings Investigation = new InvestigationSettings();

            [JsonProperty("Investigation user interface")]
            public UserInterfaceSettings UserInterface = new UserInterfaceSettings();
        }

        private sealed class GeneralSettings
        {
            [JsonProperty("Command aliases", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] CommandAliases = { "radar", "recon", "smartrecon" };

            [JsonProperty("Rust moderators and owners bypass SmartRecon permissions")]
            public bool AdminsBypassPermissions = true;

            [JsonProperty("Persist each administrator's last settings")]
            public bool PersistPreferences = true;

            [JsonProperty("Log radar start and stop events")]
            public bool LogUsage = false;

            [JsonProperty("Default mode (players, stashes, tcs, all)")]
            public string DefaultMode = ModePlayers;

            [JsonProperty("Default distance")]
            public float DefaultDistance = 250f;

            [JsonProperty("Default refresh rate in seconds")]
            public float DefaultRefreshRate = 1f;

            [JsonProperty("Minimum refresh rate in seconds")]
            public float MinimumRefreshRate = 0.5f;

            [JsonProperty("Maximum refresh rate in seconds")]
            public float MaximumRefreshRate = 30f;

            [JsonProperty("Maximum standard distance")]
            public float MaximumStandardDistance = 250f;

            [JsonProperty("Maximum distance with smartrecon.extendedrange")]
            public float MaximumExtendedDistance = 1000f;

            [JsonProperty("Maximum temporary radar duration in seconds")]
            public float MaximumTemporaryDuration = 86400f;
        }

        private sealed class SchedulerSettings
        {
            [JsonProperty("Scheduler tick interval in seconds")]
            public float TickInterval = 0.1f;

            [JsonProperty("Maximum radar sessions updated per scheduler tick")]
            public int MaximumSessionsPerTick = 4;

            [JsonProperty("Active player spatial index refresh in seconds")]
            public float PlayerIndexRefresh = 0.25f;

            [JsonProperty("Sleeping player spatial index refresh in seconds")]
            public float SleeperIndexRefresh = 5f;

            [JsonProperty("Full static entity index rebuild in seconds")]
            public float StaticIndexRebuild = 1800f;

            [JsonProperty("Spatial cell size in meters")]
            public float CellSize = 100f;

            [JsonProperty("Minimum stash and cupboard refresh in seconds")]
            public float MinimumStaticRefresh = 2f;

            [JsonProperty("Extra drawing lifetime in seconds")]
            public float DrawingLifetimePadding = 0.2f;
        }

        private sealed class LimitSettings
        {
            [JsonProperty("Maximum player labels per update")]
            public int MaximumPlayers = 100;

            [JsonProperty("Maximum stash labels per update")]
            public int MaximumStashes = 75;

            [JsonProperty("Maximum cupboard labels per update")]
            public int MaximumCupboards = 75;

            [JsonProperty("Maximum loot labels per update")]
            public int MaximumLoot = 60;

            [JsonProperty("Maximum animal and non-player NPC labels per update")]
            public int MaximumNpcEntities = 75;

            [JsonProperty("Maximum total draw commands per session cycle")]
            public int MaximumDrawCommandsPerCycle = 180;
        }

        private sealed class DisplaySettings
        {
            [JsonProperty("Enable player vision arrows by default")]
            public bool DefaultArrows = false;

            [JsonProperty("Enable voice indicators by default")]
            public bool DefaultVoiceIndicator = false;

            [JsonProperty("Include sleepers by default")]
            public bool DefaultSleepers = false;

            [JsonProperty("Include NPC players")]
            public bool IncludeNpcPlayers = true;

            [JsonProperty("Enable NPC layer by default")]
            public bool DefaultNpcs = false;

            [JsonProperty("Enable loot layer by default")]
            public bool DefaultLoot = false;

            [JsonProperty("Enable extended player information by default")]
            public bool DefaultExtended = false;

            [JsonProperty("Enable TC authorization links by default")]
            public bool DefaultTcLinks = false;

            [JsonProperty("Voice indicator duration in seconds")]
            public float VoiceIndicatorDuration = 2f;

            [JsonProperty("Player label vertical offset")]
            public float PlayerLabelHeight = 2f;

            [JsonProperty("Static entity label vertical offset")]
            public float StaticLabelHeight = 0.5f;

            [JsonProperty("Vision arrow length")]
            public float ArrowLength = 3f;

            [JsonProperty("Vision arrow head radius")]
            public float ArrowHeadRadius = 0.12f;

            [JsonProperty("Use distance fade for parented drawings")]
            public bool DistanceFade = true;

            [JsonProperty("Depth test parented drawings")]
            public bool DepthTest = false;

            [JsonProperty("Player label scale")]
            public float PlayerLabelScale = 1f;

            [JsonProperty("Player drawing color")]
            public string PlayerDrawingColor = "#FFFFFF";

            [JsonProperty("Stash drawing color")]
            public string StashDrawingColor = "#E642F5";

            [JsonProperty("Cupboard drawing color")]
            public string CupboardDrawingColor = "#05F5E5";

            [JsonProperty("Vision arrow color")]
            public string ArrowDrawingColor = "#FFFFFF";

            [JsonProperty("Loot drawing color")]
            public string LootDrawingColor = "#F2C94C";

            [JsonProperty("NPC and animal drawing color")]
            public string NpcDrawingColor = "#FFB347";
        }

        private sealed class PrivacySettings
        {
            [JsonProperty("Hide vanished players unless explicitly enabled and permitted")]
            public bool HideVanishedPlayers = true;

            [JsonProperty("Treat any Rust limited-networking player as vanished")]
            public bool TreatLimitedNetworkingAsVanished = true;

            [JsonProperty("Mark visible vanished players with [V]")]
            public bool MarkVanishedPlayers = true;

            [JsonProperty("Hide owners from moderators unless they have smartrecon.seeowners")]
            public bool HideOwnersFromModerators = true;
        }

        private sealed class VanishSettings
        {
            [JsonProperty("Command aliases", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] CommandAliases = { "vanish", "v" };

            [JsonProperty("Inventory inspection command aliases", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] InventoryCommandAliases = { "inv", "invspy" };

            [JsonProperty("Automatically vanish permitted administrators when they connect")]
            public bool VanishOnConnect = false;

            [JsonProperty("Keep vanish enabled across disconnects and plugin reloads")]
            public bool PersistVanishState = true;

            [JsonProperty("Enable noclip when entering vanish")]
            public bool EnableNoclip = true;

            [JsonProperty("Pause and protect metabolism while vanished")]
            public bool PauseMetabolism = true;

            [JsonProperty("Pause anti-hack checks while vanished")]
            public bool BypassAntiHack = true;

            [JsonProperty("Prevent vanished administrators from receiving damage")]
            public bool PreventIncomingDamage = true;

            [JsonProperty("Prevent vanished administrators from dealing damage without permission")]
            public bool PreventOutgoingDamage = true;

            [JsonProperty("Allow lock bypass with smartrecon.vanish.unlock")]
            public bool EnableLockBypass = true;

            [JsonProperty("Enable inventory inspection commands")]
            public bool EnableInventoryInspection = true;

            [JsonProperty("Enable reload-key investigative interaction while vanished")]
            public bool EnableReloadInteraction = true;

            [JsonProperty("Enable vanish-only map-marker teleport with smartrecon.vanish.teleport")]
            public bool EnableMapMarkerTeleport = true;

            [JsonProperty("Remove map marker after a successful vanish teleport")]
            public bool RemoveTeleportMarker = true;

            [JsonProperty("Preserve current noclip altitude when above the destination")]
            public bool PreserveNoclipAltitude = true;

            [JsonProperty("Map-marker teleport height offset")]
            public float MapTeleportHeightOffset = 2f;

            [JsonProperty("Minimum seconds between map-marker teleports")]
            public float MapTeleportCooldown = 0.5f;

            [JsonProperty("Log successful map-marker teleports to a separate audit file")]
            public bool LogMapMarkerTeleports = true;

            [JsonProperty("Show Rust's native invisibility indicator")]
            public bool ShowNativeIndicator = true;

            [JsonProperty("Show vanish chat notifications")]
            public bool EnableNotifications = true;

            [JsonProperty("Enable private vanish and reappear sounds")]
            public bool EnableSoundEffects = true;

            [JsonProperty("Make vanish and reappear sounds audible to nearby players")]
            public bool PublicSoundEffects = false;

            [JsonProperty("Sound effect used when vanishing")]
            public string VanishSoundEffect = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";

            [JsonProperty("Sound effect used when reappearing")]
            public string ReappearSoundEffect = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";

            [JsonProperty("Log vanish and reappear events")]
            public bool LogUsage = false;
        }

        private sealed class InvestigationSettings
        {
            [JsonProperty("Automatically start radar when entering vanish")]
            public bool StartRadarOnVanish = true;

            [JsonProperty("Automatically stop radar when leaving vanish")]
            public bool StopRadarOnReappear = true;

            [JsonProperty("Use the administrator's saved radar mode and filters")]
            public bool UseSavedRadarPreferences = true;

            [JsonProperty("Radar mode when saved preferences are not used")]
            public string RadarMode = ModePlayers;

            [JsonProperty("Force player vision arrows on while vanish started radar")]
            public bool ForceVisionArrows = true;

            [JsonProperty("Automatically start radar when entering native spectating")]
            public bool StartRadarOnSpectate = true;

            [JsonProperty("Automatically stop spectate-started radar when native spectating ends")]
            public bool StopRadarOnSpectateEnd = true;

            [JsonProperty("Force player vision arrows on during native spectating")]
            public bool ForceVisionArrowsOnSpectate = true;
        }

        private sealed class UserInterfaceSettings
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("Show automatically when radar starts")]
            public bool ShowOnRadarStart = true;

            [JsonProperty("Anchor minimum")]
            public string AnchorMin = DefaultUiAnchorMin;

            [JsonProperty("Anchor maximum")]
            public string AnchorMax = DefaultUiAnchorMax;

            [JsonProperty("Panel color")]
            public string PanelColor = "0.035 0.045 0.055 0.94";

            [JsonProperty("Accent color")]
            public string AccentColor = "0.10 0.78 0.72 1.0";

            [JsonProperty("Enabled button color")]
            public string EnabledColor = "0.08 0.62 0.56 0.95";

            [JsonProperty("Disabled button color")]
            public string DisabledColor = "0.16 0.19 0.22 0.92";

            [JsonProperty("Text color")]
            public string TextColor = "0.94 0.97 0.98 1.0";
        }

        protected override void LoadDefaultConfig()
        {
            _config = new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                {
                    throw new JsonException("Configuration deserialized to null");
                }
            }
            catch (Exception exception)
            {
                PrintWarning("The configuration was invalid; defaults have been loaded.");
                PrintError(exception.Message);
                LoadDefaultConfig();
            }

            ValidateConfig();
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void ValidateConfig()
        {
            if (_config.General == null) _config.General = new GeneralSettings();
            if (_config.Scheduler == null) _config.Scheduler = new SchedulerSettings();
            if (_config.Limits == null) _config.Limits = new LimitSettings();
            if (_config.Display == null) _config.Display = new DisplaySettings();
            if (_config.Privacy == null) _config.Privacy = new PrivacySettings();
            if (_config.Vanish == null) _config.Vanish = new VanishSettings();
            if (_config.Investigation == null) _config.Investigation = new InvestigationSettings();
            if (_config.UserInterface == null) _config.UserInterface = new UserInterfaceSettings();

            if (string.Equals(_config.UserInterface.AnchorMin, PreviousDefaultUiAnchorMin, StringComparison.Ordinal) &&
                string.Equals(_config.UserInterface.AnchorMax, PreviousDefaultUiAnchorMax, StringComparison.Ordinal))
            {
                _config.UserInterface.AnchorMin = DefaultUiAnchorMin;
                _config.UserInterface.AnchorMax = DefaultUiAnchorMax;
            }

            if (_config.General.CommandAliases == null || _config.General.CommandAliases.Length == 0)
                _config.General.CommandAliases = new[] { "radar", "recon", "smartrecon" };
            if (_config.Vanish.CommandAliases == null || _config.Vanish.CommandAliases.Length == 0)
                _config.Vanish.CommandAliases = new[] { "vanish", "v" };
            if (_config.Vanish.InventoryCommandAliases == null || _config.Vanish.InventoryCommandAliases.Length == 0)
                _config.Vanish.InventoryCommandAliases = new[] { "inv", "invspy" };
            _config.Investigation.RadarMode = NormalizeMode(_config.Investigation.RadarMode) ?? ModePlayers;

            _config.General.MinimumRefreshRate = Mathf.Clamp(_config.General.MinimumRefreshRate, 0.1f, 30f);
            _config.General.MaximumRefreshRate = Mathf.Max(_config.General.MinimumRefreshRate, _config.General.MaximumRefreshRate);
            _config.General.MaximumStandardDistance = Mathf.Clamp(_config.General.MaximumStandardDistance, 25f, 5000f);
            _config.General.MaximumExtendedDistance = Mathf.Clamp(_config.General.MaximumExtendedDistance, _config.General.MaximumStandardDistance, 5000f);
            _config.General.MaximumTemporaryDuration = Mathf.Clamp(_config.General.MaximumTemporaryDuration, 10f, 604800f);
            _config.General.DefaultDistance = Mathf.Clamp(_config.General.DefaultDistance, 1f, _config.General.MaximumStandardDistance);
            _config.General.DefaultRefreshRate = Mathf.Clamp(_config.General.DefaultRefreshRate, _config.General.MinimumRefreshRate, _config.General.MaximumRefreshRate);
            _config.General.DefaultMode = NormalizeMode(_config.General.DefaultMode) ?? ModePlayers;

            _config.Scheduler.TickInterval = Mathf.Clamp(_config.Scheduler.TickInterval, 0.05f, 1f);
            _config.Scheduler.MaximumSessionsPerTick = Mathf.Clamp(_config.Scheduler.MaximumSessionsPerTick, 1, 64);
            _config.Scheduler.PlayerIndexRefresh = Mathf.Clamp(_config.Scheduler.PlayerIndexRefresh, 0.1f, 5f);
            _config.Scheduler.SleeperIndexRefresh = Mathf.Clamp(_config.Scheduler.SleeperIndexRefresh, 1f, 60f);
            _config.Scheduler.StaticIndexRebuild = Mathf.Clamp(_config.Scheduler.StaticIndexRebuild, 30f, 3600f);
            _config.Scheduler.CellSize = Mathf.Clamp(_config.Scheduler.CellSize, 25f, 250f);
            _config.Scheduler.MinimumStaticRefresh = Mathf.Clamp(_config.Scheduler.MinimumStaticRefresh, 0.5f, 60f);
            _config.Scheduler.DrawingLifetimePadding = Mathf.Clamp(_config.Scheduler.DrawingLifetimePadding, 0.05f, 2f);

            _config.Limits.MaximumPlayers = Mathf.Clamp(_config.Limits.MaximumPlayers, 1, 500);
            _config.Limits.MaximumStashes = Mathf.Clamp(_config.Limits.MaximumStashes, 1, 500);
            _config.Limits.MaximumCupboards = Mathf.Clamp(_config.Limits.MaximumCupboards, 1, 500);
            _config.Limits.MaximumLoot = Mathf.Clamp(_config.Limits.MaximumLoot, 1, 500);
            _config.Limits.MaximumNpcEntities = Mathf.Clamp(_config.Limits.MaximumNpcEntities, 1, 500);
            _config.Limits.MaximumDrawCommandsPerCycle = Mathf.Clamp(_config.Limits.MaximumDrawCommandsPerCycle, 1, 1500);

            _config.Display.VoiceIndicatorDuration = Mathf.Clamp(_config.Display.VoiceIndicatorDuration, 0.1f, 30f);
            _config.Display.PlayerLabelHeight = Mathf.Clamp(_config.Display.PlayerLabelHeight, 0f, 10f);
            _config.Display.StaticLabelHeight = Mathf.Clamp(_config.Display.StaticLabelHeight, 0f, 10f);
            _config.Display.ArrowLength = Mathf.Clamp(_config.Display.ArrowLength, 0.5f, 25f);
            _config.Display.ArrowHeadRadius = Mathf.Clamp(_config.Display.ArrowHeadRadius, 0.01f, 2f);
            _config.Display.PlayerLabelScale = Mathf.Clamp(_config.Display.PlayerLabelScale, 0.25f, 3f);
            _config.Vanish.MapTeleportHeightOffset = Mathf.Clamp(_config.Vanish.MapTeleportHeightOffset, 0f, 20f);
            _config.Vanish.MapTeleportCooldown = Mathf.Clamp(_config.Vanish.MapTeleportCooldown, 0.1f, 10f);

            _playerDrawColor = ParseColor(_config.Display.PlayerDrawingColor, Color.white);
            _stashDrawColor = ParseColor(_config.Display.StashDrawingColor, new Color(0.9f, 0.26f, 0.96f));
            _cupboardDrawColor = ParseColor(_config.Display.CupboardDrawingColor, new Color(0.02f, 0.96f, 0.9f));
            _arrowDrawColor = ParseColor(_config.Display.ArrowDrawingColor, Color.white);
            _lootDrawColor = ParseColor(_config.Display.LootDrawingColor, new Color(0.95f, 0.79f, 0.3f));
            _npcDrawColor = ParseColor(_config.Display.NpcDrawingColor, new Color(1f, 0.7f, 0.28f));
        }

        private static Color ParseColor(string value, Color fallback)
        {
            Color parsed;
            if (!string.IsNullOrWhiteSpace(value) && ColorUtility.TryParseHtmlString(value, out parsed))
                return parsed;
            return fallback;
        }

        #endregion

        #region Data

        private sealed class StoredData
        {
            [JsonProperty("Player preferences")]
            public Dictionary<ulong, RadarPreferences> Preferences = new Dictionary<ulong, RadarPreferences>();

            [JsonProperty("Administrators who should remain vanished")]
            public HashSet<ulong> VanishedUsers = new HashSet<ulong>();
        }

        private sealed class RadarPreferences
        {
            public string Mode;
            public float Distance;
            public float RefreshRate;
            public bool ShowArrows;
            public bool ShowVoice;
            public bool ShowSleepers;
            public bool ShowVanished;
            public bool? PlayersLayer;
            public bool? StashesLayer;
            public bool? CupboardsLayer;
            public bool? NpcsLayer;
            public bool? LootLayer;
            public bool ShowExtended;
            public bool ShowTcLinks;
            public bool? ShowUi;
            public string NameFilter;
            public string TeamFilter;
            public string AuthorizationFilter;
            public string SafeZoneFilter;
        }

        private void LoadData()
        {
            _dataFile = Interface.Oxide.DataFileSystem.GetFile(Name);
            try
            {
                _storedData = _dataFile.ReadObject<StoredData>();
                if (_storedData == null) _storedData = new StoredData();
                if (_storedData.Preferences == null)
                    _storedData.Preferences = new Dictionary<ulong, RadarPreferences>();
                if (_storedData.VanishedUsers == null)
                    _storedData.VanishedUsers = new HashSet<ulong>();
            }
            catch (Exception exception)
            {
                PrintWarning("Preference data could not be read; a new data file will be used.");
                PrintError(exception.Message);
                _storedData = new StoredData();
            }
        }

        private void SaveData()
        {
            if (_dataFile == null || _storedData == null)
                return;

            StoredData snapshot = new StoredData
            {
                Preferences = _config.General.PersistPreferences
                    ? _storedData.Preferences
                    : new Dictionary<ulong, RadarPreferences>(),
                VanishedUsers = _config.Vanish.PersistVanishState
                    ? _storedData.VanishedUsers
                    : new HashSet<ulong>()
            };
            _dataFile.WriteObject(snapshot);
            _dataDirty = false;
        }

        private RadarPreferences GetPreferences(ulong userId)
        {
            RadarPreferences preferences;
            if (_config.General.PersistPreferences && _storedData.Preferences.TryGetValue(userId, out preferences) && preferences != null)
            {
                NormalizePreferences(preferences);
                return preferences;
            }

            preferences = CreateDefaultPreferences();
            _storedData.Preferences[userId] = preferences;
            _dataDirty = true;
            return preferences;
        }

        private RadarPreferences CreateDefaultPreferences()
        {
            return new RadarPreferences
            {
                Mode = _config.General.DefaultMode,
                Distance = _config.General.DefaultDistance,
                RefreshRate = _config.General.DefaultRefreshRate,
                ShowArrows = _config.Display.DefaultArrows,
                ShowVoice = _config.Display.DefaultVoiceIndicator,
                ShowSleepers = _config.Display.DefaultSleepers,
                ShowVanished = false,
                PlayersLayer = _config.General.DefaultMode == ModePlayers || _config.General.DefaultMode == ModeAll,
                StashesLayer = _config.General.DefaultMode == ModeStashes || _config.General.DefaultMode == ModeAll,
                CupboardsLayer = _config.General.DefaultMode == ModeCupboards || _config.General.DefaultMode == ModeAll,
                NpcsLayer = _config.Display.DefaultNpcs,
                LootLayer = _config.Display.DefaultLoot,
                ShowExtended = _config.Display.DefaultExtended,
                ShowTcLinks = _config.Display.DefaultTcLinks,
                ShowUi = true,
                NameFilter = string.Empty,
                TeamFilter = "all",
                AuthorizationFilter = "all",
                SafeZoneFilter = "all"
            };
        }

        private void NormalizePreferences(RadarPreferences preferences)
        {
            string normalizedMode = NormalizeMode(preferences.Mode) ?? _config.General.DefaultMode;
            if (preferences.PlayersLayer == null || preferences.StashesLayer == null || preferences.CupboardsLayer == null)
                ApplyModePreset(preferences, normalizedMode);
            preferences.Mode = normalizedMode;
            if (preferences.NpcsLayer == null) preferences.NpcsLayer = _config.Display.DefaultNpcs;
            if (preferences.LootLayer == null) preferences.LootLayer = _config.Display.DefaultLoot;
            if (preferences.ShowUi == null) preferences.ShowUi = true;
            if (!IsFinitePositive(preferences.Distance)) preferences.Distance = _config.General.DefaultDistance;
            if (!IsFinitePositive(preferences.RefreshRate)) preferences.RefreshRate = _config.General.DefaultRefreshRate;
            preferences.Distance = Mathf.Clamp(preferences.Distance, 1f, _config.General.MaximumExtendedDistance);
            preferences.RefreshRate = Mathf.Clamp(preferences.RefreshRate, _config.General.MinimumRefreshRate, _config.General.MaximumRefreshRate);
            if (preferences.NameFilter == null) preferences.NameFilter = string.Empty;
            preferences.TeamFilter = NormalizeTeamFilter(preferences.TeamFilter) ?? "all";
            preferences.AuthorizationFilter = NormalizeAuthorizationFilter(preferences.AuthorizationFilter) ?? "all";
            preferences.SafeZoneFilter = NormalizeSafeZoneFilter(preferences.SafeZoneFilter) ?? "all";
        }

        private void MarkPreferencesChanged(RadarSession session)
        {
            _dataDirty = true;
            if (session != null)
            {
                session.NextPlayerUpdate = 0f;
                session.NextStaticUpdate = 0f;
            }
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You do not have permission to use SmartRecon.",
                ["FeaturePermission"] = "You do not have permission to use the '{0}' radar feature.",
                ["Enabled"] = "SmartRecon enabled: {0} mode, {1:0.#}m, {2:0.##}s player refresh.",
                ["Disabled"] = "SmartRecon disabled.",
                ["AlreadyDisabled"] = "SmartRecon is already disabled.",
                ["StatusOn"] = "SmartRecon: ON | mode={0} | layers={12} | distance={1:0.#}m | rate={2:0.##}s | arrows={3} | voice={4} | sleepers={5} | vanished={6} | extended={13} | tc-links={14} | ui={15} | team={7} | auth={8} | safezone={9} | name={10} | expires={11}",
                ["StatusOff"] = "SmartRecon: OFF | saved mode={0}, distance={1:0.#}m, rate={2:0.##}s.",
                ["InvalidMode"] = "Invalid mode. Use players, stashes, tcs, all, or custom.",
                ["InvalidNumber"] = "'{0}' must be a positive finite number.",
                ["DistanceTooHigh"] = "Maximum permitted radar distance is {0:0.#}m.",
                ["RateOutOfRange"] = "Refresh rate must be between {0:0.##} and {1:0.##} seconds.",
                ["SettingChanged"] = "SmartRecon {0} set to {1}.",
                ["FilterChanged"] = "SmartRecon {0} filter set to {1}.",
                ["SettingsReset"] = "SmartRecon settings reset to defaults.",
                ["Help"] = "SmartRecon commands:\n/radar - toggle\n/radar <players|stashes|tcs|all> [distance] [rate]\n/radar on|off|status|reset|ui\n/radar mode <mode>\n/radar layer <players|npcs|loot|stashes|tcs> [on|off]\n/radar distance <meters>\n/radar rate <seconds>\n/radar for <seconds>\n/radar arrows|voice|sleepers|vanished|extended|tclinks [on|off]\n/radar filter name <text|off>\n/radar filter team <all|mine|others|solo>\n/radar filter auth <all|players|staff|moderators|owners>\n/radar filter safezone <all|inside|outside>\n/radar findid <steamid>\n/radar buildings <twig|unprivileged>\n/radar drops [distance]",
                ["VanishedUnavailable"] = "Viewing vanished players is disabled or not permitted.",
                ["ConsolePlayerOnly"] = "SmartRecon must be controlled by an in-game player.",
                ["DurationSet"] = "SmartRecon will automatically disable in {0:0.#} seconds.",
                ["DurationTooHigh"] = "Maximum temporary radar duration is {0:0.#} seconds.",
                ["Expired"] = "SmartRecon's temporary duration expired.",
                ["VanishEnabled"] = "SmartRecon vanish enabled. Investigative radar: {0}.",
                ["VanishDisabled"] = "SmartRecon vanish disabled. Investigative radar stopped.",
                ["VanishAlreadyEnabled"] = "SmartRecon vanish is already enabled.",
                ["VanishAlreadyDisabled"] = "SmartRecon vanish is already disabled.",
                ["VanishPermanent"] = "Your permanent-vanish permission prevents reappearing.",
                ["VanishStatus"] = "SmartRecon vanish: {0} | radar: {1} | arrows: {2}.",
                ["VanishHelp"] = "SmartRecon vanish commands:\n/vanish - toggle\n/vanish on|off|status\n/inv <name|steamid> - inspect a player's inventory",
                ["InventoryNoTarget"] = "No matching active or sleeping player was found for '{0}'.",
                ["InventoryUsage"] = "Usage: /inv <name or Steam ID>, or look directly at a nearby player and use /inv.",
                ["VanishRadarUnavailable"] = "Vanish enabled, but investigative radar could not start because its command or mode permissions are missing.",
                ["ForensicCooldown"] = "Please wait {0:0.#} seconds before starting another forensic search.",
                ["ForensicFindUsage"] = "Usage: /radar findid <Steam ID>",
                ["ForensicBuildingUsage"] = "Usage: /radar buildings <twig|unprivileged>",
                ["ForensicComplete"] = "Forensic drawing complete: {0} results (maximum 250, visible for 30 seconds)."
            }, this);
        }

        private string MessageText(string key, string playerId, params object[] args)
        {
            string value = lang.GetMessage(key, this, playerId);
            return args == null || args.Length == 0 ? value : string.Format(CultureInfo.InvariantCulture, value, args);
        }

        private void Reply(BasePlayer player, string key, params object[] args)
        {
            SendReply(player, MessageText(key, player.UserIDString, args));
        }

        #endregion

        #region Lifecycle and hooks

        private void Init()
        {
            Instance = this;
            RegisterPermissions();
            LoadData();
            AddCovalenceCommand(_config.General.CommandAliases, nameof(CommandRadar));
            AddCovalenceCommand(_config.Vanish.CommandAliases, nameof(CommandVanish));
            if (_config.Vanish.EnableInventoryInspection)
                AddCovalenceCommand(_config.Vanish.InventoryCommandAliases, nameof(CommandInventory));
            Unsubscribe(nameof(OnPlayerVoice));
            UnsubscribeVanishHooks();
            Subscribe(nameof(OnPlayerSpectate));
            Subscribe(nameof(OnPlayerSpectateEnd));
        }

        private void OnServerInitialized()
        {
            _serverInitialized = true;
            RebuildStaticIndexes();
            RebuildActivePlayerIndex();
            RebuildSleepingPlayerIndex();

            float now = Time.realtimeSinceStartup;
            _nextPlayerIndexRebuild = now + _config.Scheduler.PlayerIndexRefresh;
            _nextSleeperIndexRebuild = now + _config.Scheduler.SleeperIndexRefresh;
            _nextStaticIndexRebuild = now + _config.Scheduler.StaticIndexRebuild;
            _nextVoicePrune = now + 10f;
            _nextSpectateReconcile = now;
            timer.Every(_config.Scheduler.TickInterval, SchedulerTick);

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (ShouldRestoreVanish(player))
                    NextTick(delegate { if (player != null && player.IsConnected) EnterVanish(player, false); });
            }
        }

        private void Unload()
        {
            if (_dataDirty) SaveData();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                DestroyRadarUi(player);

            List<BasePlayer> hidden = new List<BasePlayer>();
            foreach (ulong userId in _vanishedPlayers)
            {
                BasePlayer player = BasePlayer.FindByID(userId);
                if (player != null) hidden.Add(player);
            }
            for (int i = 0; i < hidden.Count; i++) ExitVanish(hidden[i], false, true, true);

            _sessions.Clear();
            _sessionIterationBuffer.Clear();
            _voiceActivity.Clear();
            _teamColorCache.Clear();
            _vanishStateCache.Clear();
            _vanishedPlayers.Clear();
            _vanishRuntime.Clear();
            _mapTeleportCooldowns.Clear();
            ClearIndexes();
            UnsubscribeVanishHooks();
            Instance = null;
            _serverInitialized = false;
        }

        private void OnServerSave()
        {
            if (_dataDirty) SaveData();
        }

        private void OnNewSave(string filename)
        {
            if (!_serverInitialized) return;
            NextTick(delegate
            {
                RebuildStaticIndexes();
                RebuildActivePlayerIndex();
                RebuildSleepingPlayerIndex();
            });
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            StopRadar(player, false);

            if (IsBuiltInVanished(player))
            {
                if (_config.Vanish.PersistVanishState || HasExplicitPermission(player, PermVanishPermanent))
                {
                    _storedData.VanishedUsers.Add(player.userID);
                    _dataDirty = true;
                    DetachVanishRuntime(player);
                    _vanishedPlayers.Remove(player.userID);
                    if (_vanishedPlayers.Count == 0) UnsubscribeVanishHooks();
                }
                else ExitVanish(player, false, false, true);
            }

            _voiceActivity.Remove(player.userID);
            _vanishStateCache.Remove(player.userID);
            _mapTeleportCooldowns.Remove(player.userID);
            _forensicCooldowns.Remove(player.userID);
            if (_dataDirty) SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            timer.Once(2f, delegate
            {
                if (player == null || !player.IsConnected) return;
                if (ShouldRestoreVanish(player)) EnterVanish(player, false);
                else if (player._limitedNetworking && _storedData.VanishedUsers.Contains(player.userID))
                    ExitVanish(player, false, false, true);
            });
        }

        private void OnPlayerVoice(BasePlayer player, byte[] data)
        {
            if (player == null || !_serverInitialized || _voiceWatcherCount <= 0) return;
            _voiceActivity[player.userID] = Time.realtimeSinceStartup;
        }

        private void OnEntitySpawned(BaseNetworkable networkable)
        {
            if (!_serverInitialized || networkable == null) return;
            NextTick(delegate
            {
                if (networkable == null || networkable.IsDestroyed) return;
                StashContainer stash = networkable as StashContainer;
                if (stash != null)
                {
                    IndexStash(stash);
                    return;
                }

                BuildingPrivlidge cupboard = networkable as BuildingPrivlidge;
                if (cupboard != null)
                {
                    IndexCupboard(cupboard);
                    return;
                }

                BaseEntity entity = networkable as BaseEntity;
                if (IsTrackedNpcEntity(entity))
                {
                    RegisterNpcEntity(entity);
                    return;
                }
                if (IsTrackedLoot(entity)) IndexLoot(entity);
            });
        }

        private void OnEntityKill(BaseNetworkable networkable)
        {
            if (networkable == null) return;
            StashContainer stash = networkable as StashContainer;
            if (stash != null)
            {
                RemoveStash(stash);
                return;
            }

            BuildingPrivlidge cupboard = networkable as BuildingPrivlidge;
            if (cupboard != null)
            {
                RemoveCupboard(cupboard);
                return;
            }

            BaseEntity entity = networkable as BaseEntity;
            RemoveNpcEntity(entity);
            RemoveLoot(entity);
        }

        private void OnUserPermissionRevoked(string id, string permissionName)
        {
            ulong userId;
            if (!ulong.TryParse(id, out userId)) return;
            BasePlayer player = BasePlayer.FindByID(userId);
            if (player == null) return;

            if (PermissionMatches(permissionName, PermUse) && !HasPermission(player, PermUse))
                StopRadar(player, true);
            else if (PermissionMatches(permissionName, PermVanish) &&
                     !HasPermission(player, PermVanish) && IsBuiltInVanished(player))
                ExitVanish(player, true, false, false);
        }

        private void OnUserPermissionGranted(string id, string permissionName)
        {
            if (!PermissionMatches(permissionName, PermVanishPermanent)) return;
            ulong userId;
            if (!ulong.TryParse(id, out userId)) return;
            BasePlayer player = BasePlayer.FindByID(userId);
            if (player != null && player.IsConnected && HasPermission(player, PermVanish) && !IsBuiltInVanished(player))
                EnterVanish(player, true);
        }

        #endregion

        #region Commands

        private void CommandVanish(IPlayer caller, string command, string[] args)
        {
            BasePlayer player = caller.Object as BasePlayer;
            if (player == null)
            {
                caller.Reply(MessageText("ConsolePlayerOnly", caller.Id));
                return;
            }
            if (!HasPermission(player, PermVanish))
            {
                Reply(player, "NoPermission");
                return;
            }

            bool currentlyVanished = IsBuiltInVanished(player);
            if (args != null && args.Length > 0)
            {
                string action = args[0].ToLowerInvariant();
                if (action == "status")
                {
                    RadarSession activeSession;
                    _sessions.TryGetValue(player.userID, out activeSession);
                    Reply(player, "VanishStatus", currentlyVanished ? "ON" : "OFF", activeSession != null ? "ON" : "OFF",
                        activeSession != null && (activeSession.Preferences.ShowArrows || activeSession.ForcedArrows) ? "ON" : "OFF");
                    return;
                }
                if (action == "help")
                {
                    Reply(player, "VanishHelp");
                    return;
                }

                bool requested;
                if (action == "on" || action == "true" || action == "1") requested = true;
                else if (action == "off" || action == "false" || action == "0") requested = false;
                else
                {
                    Reply(player, "VanishHelp");
                    return;
                }

                if (requested)
                {
                    if (currentlyVanished) Reply(player, "VanishAlreadyEnabled");
                    else EnterVanish(player, true);
                }
                else
                {
                    if (HasExplicitPermission(player, PermVanishPermanent)) Reply(player, "VanishPermanent");
                    else if (!currentlyVanished) Reply(player, "VanishAlreadyDisabled");
                    else ExitVanish(player, true, false, false);
                }
                return;
            }

            if (currentlyVanished)
            {
                if (HasExplicitPermission(player, PermVanishPermanent)) Reply(player, "VanishPermanent");
                else ExitVanish(player, true, false, false);
            }
            else EnterVanish(player, true);
        }

        private void CommandInventory(IPlayer caller, string command, string[] args)
        {
            BasePlayer viewer = caller.Object as BasePlayer;
            if (viewer == null)
            {
                caller.Reply(MessageText("ConsolePlayerOnly", caller.Id));
                return;
            }
            if (!HasPermission(viewer, PermVanishInventory))
            {
                Reply(viewer, "NoPermission");
                return;
            }

            BasePlayer target = null;
            if (args != null && args.Length > 0)
                target = FindPlayer(args[0]);
            else
                target = RaycastPlayer(viewer, 5f);

            if (target == null)
            {
                if (args == null || args.Length == 0) Reply(viewer, "InventoryUsage");
                else Reply(viewer, "InventoryNoTarget", args[0]);
                return;
            }
            OpenPlayerInventory(viewer, target);
        }

        private void CommandRadar(IPlayer caller, string command, string[] args)
        {
            BasePlayer player = caller.Object as BasePlayer;
            if (player == null)
            {
                caller.Reply(MessageText("ConsolePlayerOnly", caller.Id));
                return;
            }

            if (!HasPermission(player, PermUse))
            {
                Reply(player, "NoPermission");
                return;
            }

            RadarPreferences preferences = GetPreferences(player.userID);
            RadarSession session;
            _sessions.TryGetValue(player.userID, out session);

            if (args == null || args.Length == 0)
            {
                if (session != null) StopRadar(player, true);
                else StartRadar(player, preferences);
                return;
            }

            string action = args[0].ToLowerInvariant();
            switch (action)
            {
                case "on":
                    StartRadar(player, preferences);
                    return;
                case "off":
                    StopRadar(player, true);
                    return;
                case "status":
                    SendStatus(player, preferences, session);
                    return;
                case "help":
                    Reply(player, "Help");
                    return;
                case "reset":
                    preferences = CreateDefaultPreferences();
                    string resetDeniedFeature;
                    if (!CanUsePreferences(player, preferences, out resetDeniedFeature))
                    {
                        string permittedMode = GetFirstPermittedMode(player);
                        if (permittedMode == null)
                        {
                            Reply(player, "FeaturePermission", resetDeniedFeature);
                            return;
                        }
                        preferences.Mode = permittedMode;
                        ApplyModePreset(preferences, permittedMode);
                    }
                    preferences.Distance = Mathf.Min(preferences.Distance, HasPermission(player, PermExtendedRange)
                        ? _config.General.MaximumExtendedDistance
                        : _config.General.MaximumStandardDistance);
                    _storedData.Preferences[player.userID] = preferences;
                    if (session != null) session.Preferences = preferences;
                    MarkPreferencesChanged(session);
                    RefreshVoiceWatcherCount();
                    if (session != null) ShowRadarUi(player, session);
                    Reply(player, "SettingsReset");
                    return;
                case "mode":
                    if (args.Length < 2 || !TrySetMode(player, preferences, args[1])) return;
                    MarkPreferencesChanged(session);
                    if (session != null) ShowRadarUi(player, session);
                    Reply(player, "SettingChanged", "mode", preferences.Mode);
                    return;
                case "ui":
                    if (!HasPermission(player, PermUi))
                    {
                        Reply(player, "FeaturePermission", "ui");
                        return;
                    }
                    preferences.ShowUi = session != null ? !session.UiVisible : !(preferences.ShowUi ?? true);
                    MarkPreferencesChanged(session);
                    if (session != null)
                    {
                        if (preferences.ShowUi == true) ShowRadarUi(player, session);
                        else
                        {
                            session.UiVisible = false;
                            DestroyRadarUi(player);
                        }
                    }
                    Reply(player, "SettingChanged", "ui", preferences.ShowUi == true ? "on" : "off");
                    return;
                case "distance":
                    if (args.Length < 2 || !TrySetDistance(player, preferences, args[1])) return;
                    MarkPreferencesChanged(session);
                    Reply(player, "SettingChanged", "distance", preferences.Distance.ToString("0.#", CultureInfo.InvariantCulture) + "m");
                    return;
                case "rate":
                    if (args.Length < 2 || !TrySetRate(player, preferences, args[1])) return;
                    MarkPreferencesChanged(session);
                    Reply(player, "SettingChanged", "refresh rate", preferences.RefreshRate.ToString("0.##", CultureInfo.InvariantCulture) + "s");
                    return;
                case "for":
                    SetTemporaryDuration(player, preferences, session, args);
                    return;
                case "arrows":
                    TogglePreference(player, preferences, session, args, "arrows", PermArrows, delegate(bool value) { preferences.ShowArrows = value; }, preferences.ShowArrows);
                    return;
                case "voice":
                    TogglePreference(player, preferences, session, args, "voice", PermVoice, delegate(bool value) { preferences.ShowVoice = value; }, preferences.ShowVoice);
                    return;
                case "sleepers":
                    TogglePreference(player, preferences, session, args, "sleepers", PermSleepers, delegate(bool value) { preferences.ShowSleepers = value; }, preferences.ShowSleepers);
                    return;
                case "layer":
                    HandleLayerCommand(player, preferences, session, args);
                    return;
                case "extended":
                    TogglePreference(player, preferences, session, args, "extended", PermExtended, delegate(bool value) { preferences.ShowExtended = value; }, preferences.ShowExtended);
                    return;
                case "tclinks":
                    TogglePreference(player, preferences, session, args, "tc links", PermTcInfo, delegate(bool value) { preferences.ShowTcLinks = value; }, preferences.ShowTcLinks);
                    return;
                case "vanished":
                    if (!HasPermission(player, PermSeeVanished))
                    {
                        Reply(player, "VanishedUnavailable");
                        return;
                    }
                    TogglePreference(player, preferences, session, args, "vanished", PermSeeVanished, delegate(bool value) { preferences.ShowVanished = value; }, preferences.ShowVanished);
                    return;
                case "filter":
                    HandleFilterCommand(player, preferences, session, args);
                    return;
                case "findid":
                    StartFindById(player, args);
                    return;
                case "buildings":
                    StartBuildingSearch(player, args);
                    return;
                case "drops":
                    DrawForensicDrops(player, preferences, args);
                    return;
            }

            string directMode = NormalizeMode(action);
            if (directMode != null)
            {
                RadarPreferences candidate = ClonePreferences(preferences);
                if (!TrySetMode(player, candidate, directMode)) return;
                if (args.Length > 1 && !TrySetDistance(player, candidate, args[1])) return;
                if (args.Length > 2 && !TrySetRate(player, candidate, args[2])) return;
                CopyPreferences(candidate, preferences);
                MarkPreferencesChanged(session);
                StartRadar(player, preferences);
                return;
            }

            Reply(player, "Help");
        }

        private void HandleFilterCommand(BasePlayer player, RadarPreferences preferences, RadarSession session, string[] args)
        {
            if (args.Length < 3)
            {
                Reply(player, "Help");
                return;
            }

            string filter = args[1].ToLowerInvariant();
            if (filter == "name")
            {
                string value = string.Join(" ", args, 2, args.Length - 2).Trim();
                preferences.NameFilter = string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
                MarkPreferencesChanged(session);
                Reply(player, "FilterChanged", "name", string.IsNullOrEmpty(preferences.NameFilter) ? "off" : preferences.NameFilter);
                return;
            }

            if (filter == "team")
            {
                string value = NormalizeTeamFilter(args[2]);
                if (value == null)
                {
                    Reply(player, "Help");
                    return;
                }
                preferences.TeamFilter = value;
                MarkPreferencesChanged(session);
                Reply(player, "FilterChanged", "team", value);
                return;
            }

            if (filter == "auth")
            {
                string value = NormalizeAuthorizationFilter(args[2]);
                if (value == null)
                {
                    Reply(player, "Help");
                    return;
                }
                preferences.AuthorizationFilter = value;
                MarkPreferencesChanged(session);
                Reply(player, "FilterChanged", "authorization", value);
                return;
            }

            if (filter == "safezone")
            {
                string value = NormalizeSafeZoneFilter(args[2]);
                if (value == null)
                {
                    Reply(player, "Help");
                    return;
                }
                preferences.SafeZoneFilter = value;
                MarkPreferencesChanged(session);
                Reply(player, "FilterChanged", "safe-zone", value);
                return;
            }

            Reply(player, "Help");
        }

        private void TogglePreference(BasePlayer player, RadarPreferences preferences, RadarSession session, string[] args, string label, string requiredPermission, Action<bool> setter, bool current)
        {
            if (!HasPermission(player, requiredPermission))
            {
                Reply(player, "FeaturePermission", label);
                return;
            }

            bool next;
            if (args.Length < 2) next = !current;
            else if (!TryParseToggle(args[1], current, out next))
            {
                Reply(player, "Help");
                return;
            }

            setter(next);
            MarkPreferencesChanged(session);
            if (label == "voice") RefreshVoiceWatcherCount();
            if (session != null) ShowRadarUi(player, session);
            Reply(player, "SettingChanged", label, next ? "on" : "off");
        }

        private void ToggleLayerPreference(BasePlayer player, RadarPreferences preferences, RadarSession session, string[] args,
            string label, string requiredPermission, bool current, Action<bool> setter)
        {
            if (!HasPermission(player, requiredPermission))
            {
                Reply(player, "FeaturePermission", label);
                return;
            }

            bool next;
            if (args.Length < 2) next = !current;
            else if (!TryParseToggle(args[1], current, out next))
            {
                Reply(player, "Help");
                return;
            }

            setter(next);
            preferences.Mode = ModeCustom;
            MarkPreferencesChanged(session);
            if (session != null) ShowRadarUi(player, session);
            Reply(player, "SettingChanged", label, next ? "on" : "off");
        }

        private void HandleLayerCommand(BasePlayer player, RadarPreferences preferences, RadarSession session, string[] args)
        {
            if (args.Length < 2)
            {
                Reply(player, "Help");
                return;
            }

            string[] toggleArgs = new string[args.Length - 1];
            Array.Copy(args, 1, toggleArgs, 0, toggleArgs.Length);
            switch (args[1].ToLowerInvariant())
            {
                case "players":
                    ToggleLayerPreference(player, preferences, session, toggleArgs, "players", PermPlayers, LayerPlayers(preferences), delegate(bool value) { preferences.PlayersLayer = value; });
                    return;
                case "npcs":
                    ToggleLayerPreference(player, preferences, session, toggleArgs, "npcs", PermNpcs, LayerNpcs(preferences), delegate(bool value) { preferences.NpcsLayer = value; });
                    return;
                case "loot":
                    ToggleLayerPreference(player, preferences, session, toggleArgs, "loot", PermLoot, LayerLoot(preferences), delegate(bool value) { preferences.LootLayer = value; });
                    return;
                case "stashes":
                    ToggleLayerPreference(player, preferences, session, toggleArgs, "stashes", PermStashes, LayerStashes(preferences), delegate(bool value) { preferences.StashesLayer = value; });
                    return;
                case "tc":
                case "tcs":
                case "cupboards":
                    ToggleLayerPreference(player, preferences, session, toggleArgs, "tcs", PermCupboards, LayerCupboards(preferences), delegate(bool value) { preferences.CupboardsLayer = value; });
                    return;
            }
            Reply(player, "Help");
        }

        [ConsoleCommand("smartrecon.ui")]
        private void CommandRadarUi(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg == null ? null : arg.Player();
            if (player == null || !HasPermission(player, PermUse) || !HasPermission(player, PermUi)) return;

            RadarSession session;
            if (!_sessions.TryGetValue(player.userID, out session) || session == null) return;

            string action = arg.GetString(0, string.Empty).ToLowerInvariant();
            if (action == "close")
            {
                session.Preferences.ShowUi = false;
                session.UiVisible = false;
                MarkPreferencesChanged(session);
                DestroyRadarUi(player);
                return;
            }

            RadarPreferences preferences = session.Preferences;
            switch (action)
            {
                case "players":
                    if (!HasPermission(player, PermPlayers)) return;
                    bool playersEnabled = LayerPlayers(preferences) || session.ForcedPlayersLayer;
                    session.ForcedPlayersLayer = false;
                    preferences.PlayersLayer = !playersEnabled;
                    preferences.Mode = ModeCustom;
                    break;
                case "npcs":
                    if (!HasPermission(player, PermNpcs)) return;
                    preferences.NpcsLayer = !LayerNpcs(preferences);
                    preferences.Mode = ModeCustom;
                    break;
                case "loot":
                    if (!HasPermission(player, PermLoot)) return;
                    preferences.LootLayer = !LayerLoot(preferences);
                    preferences.Mode = ModeCustom;
                    break;
                case "stashes":
                    if (!HasPermission(player, PermStashes)) return;
                    preferences.StashesLayer = !LayerStashes(preferences);
                    preferences.Mode = ModeCustom;
                    break;
                case "tcs":
                    if (!HasPermission(player, PermCupboards)) return;
                    preferences.CupboardsLayer = !LayerCupboards(preferences);
                    preferences.Mode = ModeCustom;
                    break;
                case "sleepers":
                    if (!HasPermission(player, PermSleepers)) return;
                    preferences.ShowSleepers = !preferences.ShowSleepers;
                    if (preferences.ShowSleepers) _nextSleeperIndexRebuild = 0f;
                    break;
                case "vision":
                    if (!HasPermission(player, PermArrows)) return;
                    bool visionEnabled = preferences.ShowArrows || session.ForcedArrows;
                    session.ForcedArrows = false;
                    preferences.ShowArrows = !visionEnabled;
                    break;
                case "extended":
                    if (!HasPermission(player, PermExtended)) return;
                    preferences.ShowExtended = !preferences.ShowExtended;
                    break;
                case "tclinks":
                    if (!HasPermission(player, PermTcInfo)) return;
                    preferences.ShowTcLinks = !preferences.ShowTcLinks;
                    break;
                case "voice":
                    if (!HasPermission(player, PermVoice)) return;
                    preferences.ShowVoice = !preferences.ShowVoice;
                    RefreshVoiceWatcherCount();
                    break;
                default:
                    return;
            }

            MarkPreferencesChanged(session);
            if (HasPlayerLayers(preferences)) _nextPlayerIndexRebuild = 0f;
            ShowRadarUi(player, session);
        }

        private void ShowRadarUi(BasePlayer player, RadarSession session)
        {
            DestroyRadarUi(player);
            if (session != null) session.UiVisible = false;
            if (player == null || session == null || !_config.UserInterface.Enabled || session.Preferences.ShowUi != true ||
                !HasPermission(player, PermUi)) return;

            UserInterfaceSettings settings = _config.UserInterface;
            bool spectating = player.IsSpectating();
            string workflowStatus = spectating ? "SPECTATE ON" : IsBuiltInVanished(player) ? "VANISH ON" : "VANISH OFF";
            CuiElementContainer elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image = { Color = settings.PanelColor },
                RectTransform = { AnchorMin = settings.AnchorMin, AnchorMax = settings.AnchorMax },
                CursorEnabled = spectating
            }, "Hud", RadarUiName);

            elements.Add(new CuiLabel
            {
                Text = { Text = "SMARTRECON", FontSize = 13, Align = TextAnchor.MiddleLeft, Color = settings.TextColor },
                RectTransform = { AnchorMin = "0.06 0.895", AnchorMax = "0.84 0.985" }
            }, RadarUiName);
            elements.Add(new CuiLabel
            {
                Text = { Text = workflowStatus + "  •  RADAR ON", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = settings.AccentColor },
                RectTransform = { AnchorMin = "0.06 0.83", AnchorMax = "0.92 0.90" }
            }, RadarUiName);
            elements.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = "smartrecon.ui close" },
                RectTransform = { AnchorMin = "0.86 0.91", AnchorMax = "0.97 0.98" },
                Text = { Text = "×", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = settings.TextColor }
            }, RadarUiName);

            AddUiToggle(elements, "PLAYERS", "players", LayerPlayers(session.Preferences) || session.ForcedPlayersLayer, 0, 0, settings);
            AddUiToggle(elements, "NPCS", "npcs", LayerNpcs(session.Preferences), 0, 1, settings);
            AddUiToggle(elements, "LOOT", "loot", LayerLoot(session.Preferences), 1, 0, settings);
            AddUiToggle(elements, "STASHES", "stashes", LayerStashes(session.Preferences), 1, 1, settings);
            AddUiToggle(elements, "TOOL CUPBOARDS", "tcs", LayerCupboards(session.Preferences), 2, 0, settings);
            AddUiToggle(elements, "SLEEPERS", "sleepers", session.Preferences.ShowSleepers, 2, 1, settings);
            AddUiToggle(elements, "VISION / ARROWS", "vision", session.Preferences.ShowArrows || session.ForcedArrows, 3, 0, settings);
            AddUiToggle(elements, "EXTENDED INFO", "extended", session.Preferences.ShowExtended, 3, 1, settings);
            AddUiToggle(elements, "TC LINKS", "tclinks", session.Preferences.ShowTcLinks, 4, 0, settings);
            AddUiToggle(elements, "VOICE", "voice", session.Preferences.ShowVoice, 4, 1, settings);

            elements.Add(new CuiLabel
            {
                Text = { Text = spectating ? "Click controls • × closes • /radar ui reopens" : "Open inventory to click • /radar ui hides panel", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "0.62 0.68 0.72 1" },
                RectTransform = { AnchorMin = "0.04 0.015", AnchorMax = "0.96 0.095" }
            }, RadarUiName);
            CuiHelper.AddUi(player, elements);
            session.UiVisible = true;
        }

        private static void AddUiToggle(CuiElementContainer elements, string label, string action, bool enabled, int row, int column, UserInterfaceSettings settings)
        {
            const float top = 0.80f;
            const float rowHeight = 0.13f;
            float yMax = top - row * rowHeight;
            float yMin = yMax - 0.095f;
            float xMin = column == 0 ? 0.05f : 0.515f;
            float xMax = column == 0 ? 0.485f : 0.95f;
            elements.Add(new CuiButton
            {
                Button = { Color = enabled ? settings.EnabledColor : settings.DisabledColor, Command = "smartrecon.ui " + action },
                RectTransform = { AnchorMin = xMin.ToString("0.###", CultureInfo.InvariantCulture) + " " + yMin.ToString("0.###", CultureInfo.InvariantCulture), AnchorMax = xMax.ToString("0.###", CultureInfo.InvariantCulture) + " " + yMax.ToString("0.###", CultureInfo.InvariantCulture) },
                Text = { Text = (enabled ? "●  " : "○  ") + label, FontSize = 9, Align = TextAnchor.MiddleCenter, Color = settings.TextColor }
            }, RadarUiName);
        }

        private static void DestroyRadarUi(BasePlayer player)
        {
            if (player != null) CuiHelper.DestroyUi(player, RadarUiName);
        }

        private void SetTemporaryDuration(BasePlayer player, RadarPreferences preferences, RadarSession session, string[] args)
        {
            float seconds;
            if (args.Length < 2 || !TryParsePositiveFloat(args[1], out seconds))
            {
                Reply(player, "InvalidNumber", "duration");
                return;
            }
            if (seconds > _config.General.MaximumTemporaryDuration)
            {
                Reply(player, "DurationTooHigh", _config.General.MaximumTemporaryDuration);
                return;
            }
            if (session == null)
            {
                StartRadar(player, preferences);
                _sessions.TryGetValue(player.userID, out session);
            }
            if (session == null) return;
            session.ExpiresAt = Time.realtimeSinceStartup + seconds;
            Reply(player, "DurationSet", seconds);
        }

        private bool CanRunForensics(BasePlayer player)
        {
            if (!HasPermission(player, PermForensics))
            {
                Reply(player, "FeaturePermission", "forensics");
                return false;
            }
            float now = Time.realtimeSinceStartup;
            float availableAt;
            if (_forensicCooldowns.TryGetValue(player.userID, out availableAt) && now < availableAt)
            {
                Reply(player, "ForensicCooldown", availableAt - now);
                return false;
            }
            _forensicCooldowns[player.userID] = now + 10f;
            return true;
        }

        private void StartFindById(BasePlayer player, string[] args)
        {
            ulong userId;
            if (args.Length < 2 || !ulong.TryParse(args[1], out userId))
            {
                Reply(player, "ForensicFindUsage");
                return;
            }
            if (!CanRunForensics(player)) return;
            ServerMgr.Instance.StartCoroutine(FindByIdRoutine(player, userId));
        }

        private IEnumerator FindByIdRoutine(BasePlayer viewer, ulong userId)
        {
            List<BaseNetworkable> snapshot = new List<BaseNetworkable>();
            foreach (BaseNetworkable networkable in BaseNetworkable.serverEntities) snapshot.Add(networkable);
            int found = 0;
            for (int inspected = 0; inspected < snapshot.Count; inspected++)
            {
                if (viewer == null || !viewer.IsConnected) yield break;
                BaseNetworkable networkable = snapshot[inspected];
                BaseEntity entity = networkable as BaseEntity;
                if (entity != null && !entity.IsDestroyed && IsEntityAssociatedWithUser(entity, userId))
                {
                    viewer.SendConsoleCommand("ddraw.text", 30f, Color.cyan, entity.transform.position + Vector3.up,
                        "<size=13><color=#7BDFF2>ID MATCH</color> " + EscapeRichText(entity.ShortPrefabName) + "</size>");
                    if (++found >= 250) break;
                }
                if ((inspected + 1) % 200 == 0) yield return null;
            }
            if (viewer != null && viewer.IsConnected) Reply(viewer, "ForensicComplete", found);
        }

        private static bool IsEntityAssociatedWithUser(BaseEntity entity, ulong userId)
        {
            if (entity.OwnerID == userId) return true;
            BuildingPrivlidge cupboard = entity as BuildingPrivlidge;
            if (cupboard != null && cupboard.authorizedPlayers != null && cupboard.authorizedPlayers.Contains(userId)) return true;
            SleepingBag bag = entity as SleepingBag;
            if (bag != null && bag.deployerUserID == userId) return true;
            CodeLock codeLock = entity as CodeLock;
            return codeLock != null && codeLock.whitelistPlayers != null && codeLock.whitelistPlayers.Contains(userId);
        }

        private void StartBuildingSearch(BasePlayer player, string[] args)
        {
            string filter = args.Length > 1 ? args[1].ToLowerInvariant() : "twig";
            if (filter != "twig" && filter != "unprivileged")
            {
                Reply(player, "ForensicBuildingUsage");
                return;
            }
            if (!CanRunForensics(player)) return;
            ServerMgr.Instance.StartCoroutine(BuildingSearchRoutine(player, filter));
        }

        private IEnumerator BuildingSearchRoutine(BasePlayer viewer, string filter)
        {
            List<BaseNetworkable> snapshot = new List<BaseNetworkable>();
            foreach (BaseNetworkable networkable in BaseNetworkable.serverEntities) snapshot.Add(networkable);
            int found = 0;
            for (int inspected = 0; inspected < snapshot.Count; inspected++)
            {
                if (viewer == null || !viewer.IsConnected) yield break;
                BaseNetworkable networkable = snapshot[inspected];
                BuildingBlock block = networkable as BuildingBlock;
                if (block != null && !block.IsDestroyed)
                {
                    bool match = filter == "twig"
                        ? block.grade == BuildingGrade.Enum.Twigs
                        : block.GetBuildingPrivilege() == null;
                    if (match)
                    {
                        viewer.SendConsoleCommand("ddraw.text", 30f, Color.yellow, block.transform.position + Vector3.up,
                            "<size=13><color=#F2C94C>" + filter.ToUpperInvariant() + " BUILDING</color></size>");
                        if (++found >= 250) break;
                    }
                }
                if ((inspected + 1) % 200 == 0) yield return null;
            }
            if (viewer != null && viewer.IsConnected) Reply(viewer, "ForensicComplete", found);
        }

        private void DrawForensicDrops(BasePlayer viewer, RadarPreferences preferences, string[] args)
        {
            float distance = preferences.Distance;
            if (args.Length > 1 && (!TryParsePositiveFloat(args[1], out distance) || distance > _config.General.MaximumExtendedDistance))
            {
                Reply(viewer, "InvalidNumber", "distance");
                return;
            }
            if (!CanRunForensics(viewer)) return;
            if (!HasPermission(viewer, PermExtendedRange)) distance = Mathf.Min(distance, _config.General.MaximumStandardDistance);
            int found = DrawLoot(viewer, GetRadarOrigin(viewer), distance, 30f, Mathf.Min(250, _config.Limits.MaximumDrawCommandsPerCycle));
            Reply(viewer, "ForensicComplete", found);
        }

        private bool TrySetMode(BasePlayer player, RadarPreferences preferences, string input)
        {
            string mode = NormalizeMode(input);
            if (mode == null)
            {
                Reply(player, "InvalidMode");
                return false;
            }

            string deniedFeature;
            if (!CanUseMode(player, mode, out deniedFeature))
            {
                Reply(player, "FeaturePermission", deniedFeature);
                return false;
            }

            preferences.Mode = mode;
            ApplyModePreset(preferences, mode);
            return true;
        }

        private bool TrySetDistance(BasePlayer player, RadarPreferences preferences, string input)
        {
            float value;
            if (!TryParsePositiveFloat(input, out value))
            {
                Reply(player, "InvalidNumber", "distance");
                return false;
            }

            float maximum = HasPermission(player, PermExtendedRange)
                ? _config.General.MaximumExtendedDistance
                : _config.General.MaximumStandardDistance;

            if (value > maximum)
            {
                Reply(player, "DistanceTooHigh", maximum);
                return false;
            }

            preferences.Distance = value;
            return true;
        }

        private bool TrySetRate(BasePlayer player, RadarPreferences preferences, string input)
        {
            float value;
            if (!TryParsePositiveFloat(input, out value))
            {
                Reply(player, "InvalidNumber", "refresh rate");
                return false;
            }

            if (value < _config.General.MinimumRefreshRate || value > _config.General.MaximumRefreshRate)
            {
                Reply(player, "RateOutOfRange", _config.General.MinimumRefreshRate, _config.General.MaximumRefreshRate);
                return false;
            }

            preferences.RefreshRate = value;
            return true;
        }

        private static bool TryParsePositiveFloat(string input, out float value)
        {
            return float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && IsFinitePositive(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryParseToggle(string input, bool current, out bool value)
        {
            switch (input.ToLowerInvariant())
            {
                case "on":
                case "true":
                case "1":
                    value = true;
                    return true;
                case "off":
                case "false":
                case "0":
                    value = false;
                    return true;
                case "toggle":
                    value = !current;
                    return true;
                default:
                    value = current;
                    return false;
            }
        }

        private static RadarPreferences ClonePreferences(RadarPreferences source)
        {
            return new RadarPreferences
            {
                Mode = source.Mode,
                Distance = source.Distance,
                RefreshRate = source.RefreshRate,
                ShowArrows = source.ShowArrows,
                ShowVoice = source.ShowVoice,
                ShowSleepers = source.ShowSleepers,
                ShowVanished = source.ShowVanished,
                PlayersLayer = source.PlayersLayer,
                StashesLayer = source.StashesLayer,
                CupboardsLayer = source.CupboardsLayer,
                NpcsLayer = source.NpcsLayer,
                LootLayer = source.LootLayer,
                ShowExtended = source.ShowExtended,
                ShowTcLinks = source.ShowTcLinks,
                ShowUi = source.ShowUi,
                NameFilter = source.NameFilter,
                TeamFilter = source.TeamFilter,
                AuthorizationFilter = source.AuthorizationFilter,
                SafeZoneFilter = source.SafeZoneFilter
            };
        }

        private static void CopyPreferences(RadarPreferences source, RadarPreferences destination)
        {
            destination.Mode = source.Mode;
            destination.Distance = source.Distance;
            destination.RefreshRate = source.RefreshRate;
            destination.ShowArrows = source.ShowArrows;
            destination.ShowVoice = source.ShowVoice;
            destination.ShowSleepers = source.ShowSleepers;
            destination.ShowVanished = source.ShowVanished;
            destination.PlayersLayer = source.PlayersLayer;
            destination.StashesLayer = source.StashesLayer;
            destination.CupboardsLayer = source.CupboardsLayer;
            destination.NpcsLayer = source.NpcsLayer;
            destination.LootLayer = source.LootLayer;
            destination.ShowExtended = source.ShowExtended;
            destination.ShowTcLinks = source.ShowTcLinks;
            destination.ShowUi = source.ShowUi;
            destination.NameFilter = source.NameFilter;
            destination.TeamFilter = source.TeamFilter;
            destination.AuthorizationFilter = source.AuthorizationFilter;
            destination.SafeZoneFilter = source.SafeZoneFilter;
        }

        #endregion

        #region Sessions and scheduler

        private sealed class RadarSession
        {
            public BasePlayer Viewer;
            public RadarPreferences Preferences;
            public float NextPlayerUpdate;
            public float NextStaticUpdate;
            public float ExpiresAt;
            public bool StartedByVanish;
            public bool StartedBySpectate;
            public bool ForcedPlayersLayer;
            public bool ForcedArrows;
            public bool UiVisible;
        }

        private void StartRadar(BasePlayer player, RadarPreferences preferences)
        {
            StartRadar(player, preferences, true);
        }

        private void StartRadar(BasePlayer player, RadarPreferences preferences, bool notify)
        {
            NormalizePreferences(preferences);
            string deniedFeature;
            if (!CanUsePreferences(player, preferences, out deniedFeature))
            {
                Reply(player, "FeaturePermission", deniedFeature);
                return;
            }

            float maximumDistance = HasPermission(player, PermExtendedRange)
                ? _config.General.MaximumExtendedDistance
                : _config.General.MaximumStandardDistance;
            if (preferences.Distance > maximumDistance) preferences.Distance = maximumDistance;

            RadarSession session;
            bool newlyStarted = false;
            if (!_sessions.TryGetValue(player.userID, out session))
            {
                session = new RadarSession { Viewer = player, Preferences = preferences };
                _sessions[player.userID] = session;
                newlyStarted = true;
            }
            else
            {
                session.Viewer = player;
                session.Preferences = preferences;
            }

            float stagger = (_staggerSequence++ % Math.Max(1, _config.Scheduler.MaximumSessionsPerTick)) * _config.Scheduler.TickInterval;
            session.NextPlayerUpdate = Time.realtimeSinceStartup + stagger;
            session.NextStaticUpdate = Time.realtimeSinceStartup + stagger;
            session.ExpiresAt = 0f;
            session.StartedByVanish = false;
            session.StartedBySpectate = false;
            session.ForcedPlayersLayer = false;
            session.ForcedArrows = false;
            if (player.IsSpectating()) ApplySpectateSessionDefaults(player, session);
            if (HasPlayerLayers(preferences)) _nextPlayerIndexRebuild = 0f;
            if (session.ForcedPlayersLayer) _nextPlayerIndexRebuild = 0f;
            if (preferences.ShowSleepers) _nextSleeperIndexRebuild = 0f;
            RefreshVoiceWatcherCount();
            if (_config.UserInterface.ShowOnRadarStart) ShowRadarUi(player, session);

            if (_config.General.LogUsage)
                Puts(player.displayName + " (" + player.UserIDString + ") enabled SmartRecon in " + preferences.Mode + " mode.");

            if (notify) Reply(player, "Enabled", preferences.Mode, preferences.Distance, preferences.RefreshRate);
            if (newlyStarted)
            {
                Interface.CallHook("OnSmartReconActivated", player);
            }
        }

        private void StopRadar(BasePlayer player, bool notify)
        {
            if (player == null) return;
            RadarSession session;
            _sessions.TryGetValue(player.userID, out session);
            bool removed = _sessions.Remove(player.userID);
            if (session != null) session.UiVisible = false;
            DestroyRadarUi(player);
            if (removed) RefreshVoiceWatcherCount();
            if (removed)
            {
                Interface.CallHook("OnSmartReconDeactivated", player);
            }
            if (removed && _config.General.LogUsage)
                Puts(player.displayName + " (" + player.UserIDString + ") disabled SmartRecon.");

            if (!notify) return;
            Reply(player, removed ? "Disabled" : "AlreadyDisabled");
        }

        private void SendStatus(BasePlayer player, RadarPreferences preferences, RadarSession session)
        {
            if (session == null)
            {
                Reply(player, "StatusOff", preferences.Mode, preferences.Distance, preferences.RefreshRate);
                return;
            }

            Reply(player, "StatusOn",
                preferences.Mode,
                preferences.Distance,
                preferences.RefreshRate,
                preferences.ShowArrows || session.ForcedArrows ? "on" : "off",
                preferences.ShowVoice ? "on" : "off",
                preferences.ShowSleepers ? "on" : "off",
                preferences.ShowVanished ? "on" : "off",
                preferences.TeamFilter,
                preferences.AuthorizationFilter,
                preferences.SafeZoneFilter,
                string.IsNullOrEmpty(preferences.NameFilter) ? "off" : preferences.NameFilter,
                session.ExpiresAt > 0f ? Mathf.Max(0f, session.ExpiresAt - Time.realtimeSinceStartup).ToString("0.#", CultureInfo.InvariantCulture) + "s" : "off",
                BuildLayerStatus(preferences, session.ForcedPlayersLayer),
                preferences.ShowExtended ? "on" : "off",
                preferences.ShowTcLinks ? "on" : "off",
                preferences.ShowUi == true ? "on" : "off");
        }

        private static string BuildLayerStatus(RadarPreferences preferences, bool forcedPlayersLayer)
        {
            List<string> enabled = new List<string>(5);
            if (LayerPlayers(preferences) || forcedPlayersLayer) enabled.Add("players");
            if (LayerNpcs(preferences)) enabled.Add("npcs");
            if (LayerLoot(preferences)) enabled.Add("loot");
            if (LayerStashes(preferences)) enabled.Add("stashes");
            if (LayerCupboards(preferences)) enabled.Add("tcs");
            return enabled.Count == 0 ? "none" : string.Join(",", enabled.ToArray());
        }

        private void SchedulerTick()
        {
            if (!_serverInitialized) return;
            float now = Time.realtimeSinceStartup;

            ReconcileSpectateSessions(now);

            bool needsPlayerIndex = HasPlayerRadarSessions();
            bool needsSleeperIndex = needsPlayerIndex && HasSleeperRadarSessions();

            if (now >= _nextPlayerIndexRebuild)
            {
                if (needsPlayerIndex) RebuildActivePlayerIndex();
                _nextPlayerIndexRebuild = now + _config.Scheduler.PlayerIndexRefresh;
            }

            if (now >= _nextSleeperIndexRebuild)
            {
                if (needsSleeperIndex) RebuildSleepingPlayerIndex();
                _nextSleeperIndexRebuild = now + _config.Scheduler.SleeperIndexRefresh;
            }

            if (now >= _nextStaticIndexRebuild)
            {
                RebuildStaticIndexes();
                _nextStaticIndexRebuild = now + _config.Scheduler.StaticIndexRebuild;
            }

            int updatedSessions = 0;
            _sessionRemovalBuffer.Clear();

            _sessionIterationBuffer.Clear();
            foreach (ulong userId in _sessions.Keys)
                _sessionIterationBuffer.Add(userId);

            int sessionCount = _sessionIterationBuffer.Count;
            int startIndex = sessionCount == 0 ? 0 : _schedulerCursor % sessionCount;
            int nextStartIndex = sessionCount == 0 ? 0 : (startIndex + 1) % sessionCount;
            for (int visited = 0; visited < sessionCount; visited++)
            {
                int iterationIndex = (startIndex + visited) % sessionCount;
                ulong userId = _sessionIterationBuffer[iterationIndex];
                RadarSession session;
                if (!_sessions.TryGetValue(userId, out session)) continue;
                BasePlayer viewer = session == null ? null : session.Viewer;
                if (session != null && session.ExpiresAt > 0f && now >= session.ExpiresAt)
                {
                    if (viewer != null && viewer.IsConnected) Reply(viewer, "Expired");
                    _sessionRemovalBuffer.Add(userId);
                    continue;
                }
                if (viewer == null || !viewer.IsConnected || !HasPermission(viewer, PermUse))
                {
                    _sessionRemovalBuffer.Add(userId);
                    continue;
                }

                string deniedFeature;
                if (!CanUsePreferences(viewer, session.Preferences, out deniedFeature))
                {
                    Reply(viewer, "FeaturePermission", deniedFeature);
                    _sessionRemovalBuffer.Add(userId);
                    continue;
                }

                float permittedDistance = HasPermission(viewer, PermExtendedRange)
                    ? _config.General.MaximumExtendedDistance
                    : _config.General.MaximumStandardDistance;
                if (session.Preferences.Distance > permittedDistance)
                {
                    session.Preferences.Distance = permittedDistance;
                    _dataDirty = true;
                }

                bool playerDue = (HasPlayerLayers(session.Preferences) || session.ForcedPlayersLayer) &&
                    now >= session.NextPlayerUpdate;
                bool staticDue = HasStaticLayers(session.Preferences) && now >= session.NextStaticUpdate;
                if (!playerDue && !staticDue) continue;
                if (updatedSessions >= _config.Scheduler.MaximumSessionsPerTick) continue;

                int budget = _config.Limits.MaximumDrawCommandsPerCycle;
                if (playerDue)
                {
                    budget -= DrawPlayerRadar(session, now, budget);
                    session.NextPlayerUpdate = now + session.Preferences.RefreshRate;
                }

                if (staticDue && budget > 0)
                {
                    DrawStaticRadar(session, budget);
                    session.NextStaticUpdate = now + Mathf.Max(session.Preferences.RefreshRate, _config.Scheduler.MinimumStaticRefresh);
                }

                updatedSessions++;
                nextStartIndex = (iterationIndex + 1) % sessionCount;
            }

            if (sessionCount > 0)
                _schedulerCursor = nextStartIndex;

            for (int i = 0; i < _sessionRemovalBuffer.Count; i++)
            {
                RadarSession removedSession;
                if (!_sessions.TryGetValue(_sessionRemovalBuffer[i], out removedSession)) continue;
                BasePlayer removedViewer = removedSession == null ? null : removedSession.Viewer;
                if (removedSession != null) removedSession.UiVisible = false;
                DestroyRadarUi(removedViewer);
                if (!_sessions.Remove(_sessionRemovalBuffer[i])) continue;
                if (removedViewer != null)
                {
                    Interface.CallHook("OnSmartReconDeactivated", removedViewer);
                    if (_config.General.LogUsage)
                        Puts(removedViewer.displayName + " (" + removedViewer.UserIDString + ") disabled SmartRecon.");
                }
            }

            if (_sessionRemovalBuffer.Count > 0) RefreshVoiceWatcherCount();

            if (now >= _nextVoicePrune)
            {
                PruneVoiceActivity(now);
                _nextVoicePrune = now + 10f;
            }
        }

        private bool HasPlayerRadarSessions()
        {
            foreach (RadarSession session in _sessions.Values)
            {
                if (session != null && (HasPlayerLayers(session.Preferences) || session.ForcedPlayersLayer)) return true;
            }
            return false;
        }

        private bool HasSleeperRadarSessions()
        {
            foreach (RadarSession session in _sessions.Values)
            {
                if (session != null && (LayerPlayers(session.Preferences) || session.ForcedPlayersLayer) &&
                    session.Preferences.ShowSleepers) return true;
            }
            return false;
        }

        private void RefreshVoiceWatcherCount()
        {
            int previousCount = _voiceWatcherCount;
            int count = 0;
            foreach (RadarSession session in _sessions.Values)
            {
                if (session != null && session.Preferences.ShowVoice && session.Viewer != null && HasPermission(session.Viewer, PermVoice)) count++;
            }
            _voiceWatcherCount = count;
            if (previousCount == 0 && count > 0) Subscribe(nameof(OnPlayerVoice));
            else if (previousCount > 0 && count == 0) Unsubscribe(nameof(OnPlayerVoice));
            if (count == 0) _voiceActivity.Clear();
        }

        private void PruneVoiceActivity(float now)
        {
            if (_voiceActivity.Count == 0) return;
            _sessionRemovalBuffer.Clear();
            float cutoff = now - Mathf.Max(10f, _config.Display.VoiceIndicatorDuration * 4f);
            foreach (KeyValuePair<ulong, float> pair in _voiceActivity)
            {
                if (pair.Value < cutoff) _sessionRemovalBuffer.Add(pair.Key);
            }
            for (int i = 0; i < _sessionRemovalBuffer.Count; i++)
                _voiceActivity.Remove(_sessionRemovalBuffer[i]);
        }

        #endregion

        #region Spatial indexes

        private static long MakeCellKey(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        private long GetCellKey(Vector3 position)
        {
            float size = _config.Scheduler.CellSize;
            int x = Mathf.FloorToInt(position.x / size);
            int z = Mathf.FloorToInt(position.z / size);
            return MakeCellKey(x, z);
        }

        private void GetCellBounds(Vector3 position, float radius, out int minX, out int maxX, out int minZ, out int maxZ)
        {
            float size = _config.Scheduler.CellSize;
            minX = Mathf.FloorToInt((position.x - radius) / size);
            maxX = Mathf.FloorToInt((position.x + radius) / size);
            minZ = Mathf.FloorToInt((position.z - radius) / size);
            maxZ = Mathf.FloorToInt((position.z + radius) / size);
        }

        private static void AddToIndex<T>(Dictionary<long, List<T>> index, long cell, T item)
        {
            List<T> list;
            if (!index.TryGetValue(cell, out list))
            {
                list = new List<T>();
                index[cell] = list;
            }
            list.Add(item);
        }

        private void RebuildActivePlayerIndex()
        {
            _activePlayerIndex.Clear();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected || IsHumanoidNpc(player)) continue;
                AddToIndex(_activePlayerIndex, GetCellKey(player.transform.position), player);
            }
            RebuildNpcEntityIndex();
        }

        private void RebuildSleepingPlayerIndex()
        {
            _sleepingPlayerIndex.Clear();
            foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
            {
                if (player == null || player.IsConnected) continue;
                AddToIndex(_sleepingPlayerIndex, GetCellKey(player.transform.position), player);
            }
        }

        private void RebuildStaticIndexes()
        {
            _stashIndex.Clear();
            _cupboardIndex.Clear();
            _lootIndex.Clear();
            _npcEntityIndex.Clear();
            _trackedNpcEntities.Clear();
            _stashCells.Clear();
            _cupboardCells.Clear();
            _lootCells.Clear();

            foreach (BaseNetworkable networkable in BaseNetworkable.serverEntities)
            {
                if (networkable == null || networkable.IsDestroyed) continue;
                StashContainer stash = networkable as StashContainer;
                if (stash != null)
                {
                    IndexStash(stash);
                    continue;
                }

                BuildingPrivlidge cupboard = networkable as BuildingPrivlidge;
                if (cupboard != null)
                {
                    IndexCupboard(cupboard);
                    continue;
                }

                BaseEntity entity = networkable as BaseEntity;
                if (IsTrackedNpcEntity(entity)) RegisterNpcEntity(entity);
                else if (IsTrackedLoot(entity)) IndexLoot(entity);
            }
        }

        private void IndexStash(StashContainer stash)
        {
            if (stash == null || stash.IsDestroyed) return;
            RemoveStash(stash);
            long cell = GetCellKey(stash.transform.position);
            AddToIndex(_stashIndex, cell, stash);
            _stashCells[stash.GetInstanceID()] = cell;
        }

        private void IndexCupboard(BuildingPrivlidge cupboard)
        {
            if (cupboard == null || cupboard.IsDestroyed) return;
            RemoveCupboard(cupboard);
            long cell = GetCellKey(cupboard.transform.position);
            AddToIndex(_cupboardIndex, cell, cupboard);
            _cupboardCells[cupboard.GetInstanceID()] = cell;
        }

        private void IndexLoot(BaseEntity entity)
        {
            if (!IsTrackedLoot(entity) || entity.IsDestroyed) return;
            RemoveLoot(entity);
            long cell = GetCellKey(entity.transform.position);
            AddToIndex(_lootIndex, cell, entity);
            _lootCells[entity.GetInstanceID()] = cell;
        }

        private static bool IsTrackedLoot(BaseEntity entity)
        {
            return entity is DroppedItem || entity is DroppedItemContainer || entity is LootContainer || entity is PlayerCorpse;
        }

        private static bool IsTrackedNpcEntity(BaseEntity entity)
        {
            if (entity == null) return false;
            BasePlayer humanoidNpc = entity as BasePlayer;
            if (humanoidNpc != null) return IsHumanoidNpc(humanoidNpc);
            return entity is BaseNpc || HasTypeInHierarchy(entity.GetType(), "BaseNPC2") || entity is FarmableAnimal ||
                entity is WildlifeHazard || entity is SimpleShark || entity is RidableHorse ||
                entity is TravellingVendor;
        }

        private static bool HasTypeInHierarchy(Type type, string typeName)
        {
            while (type != null)
            {
                if (string.Equals(type.Name, typeName, StringComparison.Ordinal)) return true;
                type = type.BaseType;
            }
            return false;
        }

        private void RegisterNpcEntity(BaseEntity entity)
        {
            if (!IsTrackedNpcEntity(entity) || entity.IsDestroyed) return;
            int instanceId = entity.GetInstanceID();
            if (_trackedNpcEntities.ContainsKey(instanceId)) return;
            _trackedNpcEntities[instanceId] = entity;
            AddToIndex(_npcEntityIndex, GetCellKey(entity.transform.position), entity);
        }

        private void RemoveNpcEntity(BaseEntity entity)
        {
            if (ReferenceEquals(entity, null)) return;
            _trackedNpcEntities.Remove(entity.GetInstanceID());
        }

        private void RebuildNpcEntityIndex()
        {
            _npcEntityIndex.Clear();
            if (_trackedNpcEntities.Count == 0) return;
            _npcRemovalBuffer.Clear();
            foreach (KeyValuePair<int, BaseEntity> pair in _trackedNpcEntities)
            {
                BaseEntity entity = pair.Value;
                if (entity == null || entity.IsDestroyed)
                {
                    _npcRemovalBuffer.Add(pair.Key);
                    continue;
                }
                AddToIndex(_npcEntityIndex, GetCellKey(entity.transform.position), entity);
            }
            for (int i = 0; i < _npcRemovalBuffer.Count; i++)
                _trackedNpcEntities.Remove(_npcRemovalBuffer[i]);
        }

        private void RemoveStash(StashContainer stash)
        {
            if (ReferenceEquals(stash, null)) return;
            int id = stash.GetInstanceID();
            long cell;
            if (!_stashCells.TryGetValue(id, out cell)) return;
            List<StashContainer> list;
            if (_stashIndex.TryGetValue(cell, out list))
            {
                list.Remove(stash);
                if (list.Count == 0) _stashIndex.Remove(cell);
            }
            _stashCells.Remove(id);
        }

        private void RemoveCupboard(BuildingPrivlidge cupboard)
        {
            if (ReferenceEquals(cupboard, null)) return;
            int id = cupboard.GetInstanceID();
            long cell;
            if (!_cupboardCells.TryGetValue(id, out cell)) return;
            List<BuildingPrivlidge> list;
            if (_cupboardIndex.TryGetValue(cell, out list))
            {
                list.Remove(cupboard);
                if (list.Count == 0) _cupboardIndex.Remove(cell);
            }
            _cupboardCells.Remove(id);
        }

        private void RemoveLoot(BaseEntity entity)
        {
            if (ReferenceEquals(entity, null)) return;
            int id = entity.GetInstanceID();
            long cell;
            if (!_lootCells.TryGetValue(id, out cell)) return;
            List<BaseEntity> list;
            if (_lootIndex.TryGetValue(cell, out list))
            {
                list.Remove(entity);
                if (list.Count == 0) _lootIndex.Remove(cell);
            }
            _lootCells.Remove(id);
        }

        private void ClearIndexes()
        {
            _activePlayerIndex.Clear();
            _sleepingPlayerIndex.Clear();
            _stashIndex.Clear();
            _cupboardIndex.Clear();
            _lootIndex.Clear();
            _npcEntityIndex.Clear();
            _trackedNpcEntities.Clear();
            _stashCells.Clear();
            _cupboardCells.Clear();
            _lootCells.Clear();
        }

        #endregion

        #region Player radar

        private struct PlayerCandidate
        {
            public BasePlayer Player;
            public float SqrDistance;
            public bool Sleeping;
            public bool Vanished;
        }

        private static int ComparePlayerCandidates(PlayerCandidate left, PlayerCandidate right)
        {
            return left.SqrDistance.CompareTo(right.SqrDistance);
        }

        private int DrawPlayerRadar(RadarSession session, float now, int budget)
        {
            BasePlayer viewer = session.Viewer;
            RadarPreferences preferences = session.Preferences;
            if (!HasPlayerLayers(preferences) && !session.ForcedPlayersLayer) return 0;

            _playerCandidates.Clear();
            BasePlayer spectatingTarget = GetSpectatingTarget(viewer);
            Vector3 origin = spectatingTarget != null && spectatingTarget.IsConnected
                ? spectatingTarget.transform.position
                : viewer.transform.position;
            // Keep the watched player in spectate results so their own vision arrow remains visible.
            // Only the administrator's entity is excluded, matching normal radar behavior.
            ulong ignoredTargetId = viewer.userID;
            ulong watchedTargetId = spectatingTarget != null ? spectatingTarget.userID : 0UL;
            float radiusSqr = preferences.Distance * preferences.Distance;
            int minX, maxX, minZ, maxZ;
            GetCellBounds(origin, preferences.Distance, out minX, out maxX, out minZ, out maxZ);

            CollectPlayerCandidates(_activePlayerIndex, viewer, preferences, origin, radiusSqr, false,
                session.ForcedPlayersLayer, ignoredTargetId, watchedTargetId, minX, maxX, minZ, maxZ);
            if ((LayerPlayers(preferences) || session.ForcedPlayersLayer) && preferences.ShowSleepers &&
                HasPermission(viewer, PermSleepers))
                CollectPlayerCandidates(_sleepingPlayerIndex, viewer, preferences, origin, radiusSqr, true,
                    session.ForcedPlayersLayer, ignoredTargetId, watchedTargetId, minX, maxX, minZ, maxZ);

            _playerCandidates.Sort(ComparePlayerCandidates);
            int maximum = Mathf.Min(_config.Limits.MaximumPlayers, _playerCandidates.Count);
            int draws = 0;
            float lifetime = preferences.RefreshRate + _config.Scheduler.DrawingLifetimePadding;

            for (int i = 0; i < maximum && draws < budget; i++)
            {
                PlayerCandidate candidate = _playerCandidates[i];
                BasePlayer target = candidate.Player;
                if (target == null) continue;

                string label = BuildPlayerLabel(viewer, target, candidate, preferences, now);
                if (candidate.Sleeping || target.net == null || !target.IsConnected)
                {
                    Vector3 position = target.transform.position + Vector3.up * _config.Display.PlayerLabelHeight;
                    viewer.SendConsoleCommand("ddraw.text", lifetime, _playerDrawColor, position, label);
                    draws++;
                    continue;
                }

                Vector3 localLabelPosition = target.transform.InverseTransformPoint(target.transform.position + Vector3.up * _config.Display.PlayerLabelHeight);
                if (spectatingTarget != null)
                {
                    Vector3 worldLabelPosition = target.transform.position + Vector3.up * _config.Display.PlayerLabelHeight;
                    viewer.SendConsoleCommand("ddraw.text", lifetime, _playerDrawColor, worldLabelPosition, label);
                }
                else
                {
                    viewer.SendConsoleCommand("ddraw.text", lifetime, _playerDrawColor, localLabelPosition, label,
                        _config.Display.DistanceFade, _config.Display.DepthTest, _config.Display.PlayerLabelScale, target.net.ID);
                }
                draws++;

                if ((preferences.ShowArrows || session.ForcedArrows) && HasPermission(viewer, PermArrows) && draws < budget)
                {
                    Ray headRay = target.eyes.HeadRay();
                    Vector3 startWorld = headRay.origin + Vector3.up * 0.115f;
                    Vector3 endWorld = startWorld + headRay.direction * _config.Display.ArrowLength;

                    // Player-root rotation can differ between server and client, so parenting a locally
                    // transformed eye ray can reverse it. Keep authoritative eye-ray coordinates in world space.
                    if (spectatingTarget != null)
                        viewer.SendConsoleCommand("ddraw.arrow", lifetime, _arrowDrawColor, startWorld, endWorld,
                            _config.Display.ArrowHeadRadius);
                    else
                        viewer.SendConsoleCommand("ddraw.arrow", lifetime, _arrowDrawColor, startWorld, endWorld,
                            _config.Display.ArrowHeadRadius, _config.Display.DistanceFade, _config.Display.DepthTest);
                    draws++;
                }

                if (preferences.ShowTcLinks && HasPermission(viewer, PermTcInfo) && draws < budget)
                    draws += DrawNearestAuthorizedCupboardLink(viewer, target, lifetime, budget - draws,
                        spectatingTarget != null);
            }

            if (LayerNpcs(preferences) && HasPermission(viewer, PermNpcs) && draws < budget)
                draws += DrawNpcEntities(viewer, origin, preferences.Distance, lifetime, budget - draws);

            return draws;
        }

        private int DrawNpcEntities(BasePlayer viewer, Vector3 origin, float radius, float lifetime, int budget)
        {
            _npcEntityCandidates.Clear();
            CollectStaticCandidates(_npcEntityIndex, _npcEntityCandidates, origin, radius);
            _npcEntityCandidates.Sort(CompareLootCandidates);
            int maximum = Mathf.Min(_config.Limits.MaximumNpcEntities, budget);
            int draws = 0;

            for (int i = 0; i < _npcEntityCandidates.Count && draws < maximum; i++)
            {
                StaticCandidate<BaseEntity> candidate = _npcEntityCandidates[i];
                BaseEntity entity = candidate.Entity;
                if (entity == null || entity.IsDestroyed) continue;
                string health = string.Empty;
                BaseCombatEntity combatEntity = entity as BaseCombatEntity;
                if (combatEntity != null)
                    health = " | <color=#7ED957>" + Mathf.CeilToInt(combatEntity.Health()) + "</color>HP";
                string label = "<size=13><color=#FFB347>" + GetNpcEntityLabel(entity) + "</color>" + health +
                    " | <color=#2F6FFF>" + Mathf.RoundToInt(Mathf.Sqrt(candidate.SqrDistance)) + "</color>M</size>";
                viewer.SendConsoleCommand("ddraw.text", lifetime, _npcDrawColor,
                    entity.transform.position + Vector3.up * (entity is BasePlayer
                        ? _config.Display.PlayerLabelHeight
                        : Mathf.Max(1f, _config.Display.StaticLabelHeight)), label);
                draws++;
            }
            return draws;
        }

        private static string GetNpcEntityLabel(BaseEntity entity)
        {
            BasePlayer humanoidNpc = entity as BasePlayer;
            if (humanoidNpc != null && !string.IsNullOrWhiteSpace(humanoidNpc.displayName) &&
                !string.Equals(humanoidNpc.displayName, humanoidNpc.UserIDString, StringComparison.Ordinal))
                return EscapeRichText(humanoidNpc.displayName).ToUpperInvariant();
            string name = entity == null ? string.Empty : entity.ShortPrefabName;
            if (string.IsNullOrEmpty(name)) return "NPC";
            string lower = name.ToLowerInvariant();
            if (lower.Contains("polarbear")) return "POLAR BEAR";
            if (lower.Contains("bear")) return "BEAR";
            if (lower.Contains("wolf")) return "WOLF";
            if (lower.Contains("boar")) return "BOAR";
            if (lower.Contains("stag")) return "STAG";
            if (lower.Contains("chicken")) return "CHICKEN";
            if (lower.Contains("shark")) return "SHARK";
            if (lower.Contains("horse")) return "HORSE";
            if (lower.Contains("vendor")) return "VENDOR";
            return EscapeRichText(name.Replace('_', ' ').Replace('.', ' ')).ToUpperInvariant();
        }

        private void CollectPlayerCandidates(Dictionary<long, List<BasePlayer>> index, BasePlayer viewer, RadarPreferences preferences,
            Vector3 origin, float radiusSqr, bool sleeping, bool forcedPlayersLayer, ulong ignoredTargetId,
            ulong watchedTargetId, int minX, int maxX, int minZ, int maxZ)
        {
            if (index.Count == 0) return;
            long cellCount = (long)(maxX - minX + 1) * (maxZ - minZ + 1);
            if (cellCount > index.Count)
            {
                foreach (List<BasePlayer> players in index.Values)
                    CollectPlayerCandidatesFromList(players, viewer, preferences, origin, radiusSqr, sleeping,
                        forcedPlayersLayer, ignoredTargetId, watchedTargetId);
                return;
            }

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    List<BasePlayer> players;
                    if (!index.TryGetValue(MakeCellKey(x, z), out players)) continue;
                    CollectPlayerCandidatesFromList(players, viewer, preferences, origin, radiusSqr, sleeping,
                        forcedPlayersLayer, ignoredTargetId, watchedTargetId);
                }
            }
        }

        private void CollectPlayerCandidatesFromList(List<BasePlayer> players, BasePlayer viewer,
            RadarPreferences preferences, Vector3 origin, float radiusSqr, bool sleeping, bool forcedPlayersLayer,
            ulong ignoredTargetId, ulong watchedTargetId)
        {
            for (int i = 0; i < players.Count; i++)
            {
                BasePlayer target = players[i];
                if (!ShouldIncludePlayer(viewer, target, preferences, sleeping, forcedPlayersLayer,
                        ignoredTargetId, watchedTargetId)) continue;
                float sqrDistance = (target.transform.position - origin).sqrMagnitude;
                if (sqrDistance > radiusSqr) continue;

                bool vanished = IsPlayerVanished(target);
                if (vanished && _config.Privacy.HideVanishedPlayers &&
                    (!preferences.ShowVanished || !HasPermission(viewer, PermSeeVanished))) continue;

                _playerCandidates.Add(new PlayerCandidate
                {
                    Player = target,
                    SqrDistance = sqrDistance,
                    Sleeping = sleeping,
                    Vanished = vanished
                });
            }
        }

        private bool ShouldIncludePlayer(BasePlayer viewer, BasePlayer target, RadarPreferences preferences, bool sleeping,
            bool forcedPlayersLayer, ulong ignoredTargetId, ulong watchedTargetId)
        {
            if (target == null || target.userID == viewer.userID || target.userID == ignoredTargetId) return false;
            if (!sleeping && !target.IsConnected && !IsHumanoidNpc(target)) return false;
            bool npc = IsHumanoidNpc(target);
            if (npc)
            {
                if (!_config.Display.IncludeNpcPlayers || !LayerNpcs(preferences) || !HasPermission(viewer, PermNpcs)) return false;
            }
            else if ((!LayerPlayers(preferences) && !forcedPlayersLayer) || !HasPermission(viewer, PermPlayers)) return false;

            // The watched player anchors native spectating and must retain their vision arrow even
            // when a saved name, team, authorization, or safe-zone filter would hide other targets.
            if (watchedTargetId != 0UL && target.userID == watchedTargetId) return true;

            if (!string.IsNullOrEmpty(preferences.NameFilter) &&
                target.displayName.IndexOf(preferences.NameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            int viewerAuth = GetAuthLevel(viewer);
            int targetAuth = GetAuthLevel(target);
            if (_config.Privacy.HideOwnersFromModerators && viewerAuth == 1 && targetAuth >= 2 && !HasPermission(viewer, PermSeeOwners))
                return false;

            if (!PassesAuthorizationFilter(targetAuth, preferences.AuthorizationFilter)) return false;
            if (!PassesTeamFilter(viewer, target, preferences.TeamFilter)) return false;
            if (!PassesSafeZoneFilter(target, preferences.SafeZoneFilter)) return false;
            return true;
        }

        private string BuildPlayerLabel(BasePlayer viewer, BasePlayer target, PlayerCandidate candidate, RadarPreferences preferences, float now)
        {
            _labelBuilder.Length = 0;
            _labelBuilder.Append("<size=15>").Append(EscapeRichText(target.displayName)).Append(" | ");

            float health = Mathf.Max(0f, target.health);
            string healthColor = health <= 0f ? "#FF0000" : health < 51f ? "#FFB300" : "#168503";
            _labelBuilder.Append("<color=").Append(healthColor).Append('>').Append(Mathf.RoundToInt(health)).Append("</color>HP | ");
            _labelBuilder.Append("<color=#2F6FFF>").Append(Mathf.RoundToInt(Mathf.Sqrt(candidate.SqrDistance))).Append("</color>M");

            string state = GetPlayerState(target, candidate.Sleeping);
            if (!string.IsNullOrEmpty(state)) _labelBuilder.Append(" | ").Append(state);
            if (target.InSafeZone()) _labelBuilder.Append(" | <color=#5FD35F>SAFE</color>");

            int auth = GetAuthLevel(target);
            if (auth >= 2) _labelBuilder.Append(" | <color=#FF5555>OWNER</color>");
            else if (auth == 1) _labelBuilder.Append(" | <color=#FFAA55>MOD</color>");
            else if (IsHumanoidNpc(target)) _labelBuilder.Append(" | <color=#AAAAAA>NPC</color>");

            if (target.currentTeam != 0)
            {
                string teamColor = GetTeamColorHex(target.currentTeam);
                _labelBuilder.Append(" | <color=").Append(teamColor).Append(">T</color>");
            }

            float lastVoice;
            if (preferences.ShowVoice && HasPermission(viewer, PermVoice) && _voiceActivity.TryGetValue(target.userID, out lastVoice) &&
                now - lastVoice <= _config.Display.VoiceIndicatorDuration)
                _labelBuilder.Append(" | <color=#9EF507>VOICE</color>");

            if (candidate.Vanished && _config.Privacy.MarkVanishedPlayers)
                _labelBuilder.Append(" | <color=#B76CFF>[V]</color>");

            if (preferences.ShowExtended && HasPermission(viewer, PermExtended))
                AppendExtendedPlayerInfo(target);

            _labelBuilder.Append("</size>");
            return _labelBuilder.ToString();
        }

        private void AppendExtendedPlayerInfo(BasePlayer target)
        {
            Item activeItem = target.GetActiveItem();
            if (activeItem == null || activeItem.info == null) return;

            string itemName = activeItem.info.displayName == null
                ? activeItem.info.shortname
                : activeItem.info.displayName.english;
            _labelBuilder.Append("\n<size=12><color=#7BDFF2>HELD</color> ").Append(EscapeRichText(itemName));

            if (activeItem.contents != null && activeItem.contents.itemList != null && activeItem.contents.itemList.Count > 0)
            {
                _labelBuilder.Append(" <color=#AAB8C2>[");
                int shown = 0;
                for (int i = 0; i < activeItem.contents.itemList.Count && shown < 3; i++)
                {
                    Item attachment = activeItem.contents.itemList[i];
                    if (attachment == null || attachment.info == null) continue;
                    if (shown++ > 0) _labelBuilder.Append(", ");
                    _labelBuilder.Append(EscapeRichText(attachment.info.displayName == null
                        ? attachment.info.shortname
                        : attachment.info.displayName.english));
                }
                _labelBuilder.Append("]</color>");
            }
            _labelBuilder.Append("</size>");
        }

        private static string GetPlayerState(BasePlayer target, bool sleeping)
        {
            if (sleeping) return "<color=#8EA1B5>SLEEP</color>";
            if (target.IsWounded()) return "<color=#FF8C00>WOUNDED</color>";
            if (target.IsDead()) return "<color=#FF0000>DEAD</color>";
            if (target.isMounted) return "<color=#66CCFF>MOUNTED</color>";
            return string.Empty;
        }

        #endregion

        #region Static entity radar

        private struct StaticCandidate<T>
        {
            public T Entity;
            public float SqrDistance;
        }

        private static int CompareStashCandidates(StaticCandidate<StashContainer> left, StaticCandidate<StashContainer> right)
        {
            return left.SqrDistance.CompareTo(right.SqrDistance);
        }

        private static int CompareCupboardCandidates(StaticCandidate<BuildingPrivlidge> left, StaticCandidate<BuildingPrivlidge> right)
        {
            return left.SqrDistance.CompareTo(right.SqrDistance);
        }

        private static int CompareLootCandidates(StaticCandidate<BaseEntity> left, StaticCandidate<BaseEntity> right)
        {
            return left.SqrDistance.CompareTo(right.SqrDistance);
        }

        private int DrawStaticRadar(RadarSession session, int budget)
        {
            int draws = 0;
            Vector3 origin = GetRadarOrigin(session.Viewer);
            float radius = session.Preferences.Distance;
            float lifetime = Mathf.Max(session.Preferences.RefreshRate, _config.Scheduler.MinimumStaticRefresh) + _config.Scheduler.DrawingLifetimePadding;

            if (LayerStashes(session.Preferences) && HasPermission(session.Viewer, PermStashes) && draws < budget)
                draws += DrawStashes(session.Viewer, origin, radius, lifetime, budget - draws);

            if (LayerCupboards(session.Preferences) && HasPermission(session.Viewer, PermCupboards) && draws < budget)
                draws += DrawCupboards(session.Viewer, session.Preferences, origin, radius, lifetime, budget - draws);

            if (LayerLoot(session.Preferences) && HasPermission(session.Viewer, PermLoot) && draws < budget)
                draws += DrawLoot(session.Viewer, origin, radius, lifetime, budget - draws);

            return draws;
        }

        private int DrawStashes(BasePlayer viewer, Vector3 origin, float radius, float lifetime, int budget)
        {
            _stashCandidates.Clear();
            CollectStaticCandidates(_stashIndex, _stashCandidates, origin, radius);
            _stashCandidates.Sort(CompareStashCandidates);
            int maximum = Mathf.Min(_config.Limits.MaximumStashes, budget);
            int draws = 0;

            for (int i = 0; i < _stashCandidates.Count && draws < maximum; i++)
            {
                StaticCandidate<StashContainer> candidate = _stashCandidates[i];
                StashContainer stash = candidate.Entity;
                if (stash == null || stash.IsDestroyed) continue;
                string hidden = stash.IsHidden() ? " | <color=#E642F5>HIDDEN</color>" : " | <color=#FFAA55>EXPOSED</color>";
                string label = "<size=13><color=#E642F5>STASH</color> | <color=#2F6FFF>" +
                    Mathf.RoundToInt(Mathf.Sqrt(candidate.SqrDistance)) + "</color>M" + hidden + "</size>";
                viewer.SendConsoleCommand("ddraw.text", lifetime, _stashDrawColor,
                    stash.transform.position + Vector3.up * _config.Display.StaticLabelHeight, label);
                draws++;
            }
            return draws;
        }

        private int DrawCupboards(BasePlayer viewer, RadarPreferences preferences, Vector3 origin, float radius, float lifetime, int budget)
        {
            _cupboardCandidates.Clear();
            CollectStaticCandidates(_cupboardIndex, _cupboardCandidates, origin, radius);
            _cupboardCandidates.Sort(CompareCupboardCandidates);
            int maximum = Mathf.Min(_config.Limits.MaximumCupboards, budget);
            int draws = 0;

            for (int i = 0; i < _cupboardCandidates.Count && draws < maximum; i++)
            {
                StaticCandidate<BuildingPrivlidge> candidate = _cupboardCandidates[i];
                BuildingPrivlidge cupboard = candidate.Entity;
                if (cupboard == null || cupboard.IsDestroyed) continue;
                string details = string.Empty;
                if ((preferences.ShowExtended || preferences.ShowTcLinks) && HasPermission(viewer, PermTcInfo))
                    details = " | <color=#B9CAD3>AUTH " + (cupboard.authorizedPlayers == null ? 0 : cupboard.authorizedPlayers.Count) + "</color>";
                string label = "<size=14><color=#05F5E5>TC</color> | <color=#2F6FFF>" +
                    Mathf.RoundToInt(Mathf.Sqrt(candidate.SqrDistance)) + "</color>M" + details + "</size>";
                viewer.SendConsoleCommand("ddraw.text", lifetime, _cupboardDrawColor,
                    cupboard.transform.position + Vector3.up * _config.Display.StaticLabelHeight, label);
                draws++;
            }
            return draws;
        }

        private int DrawNearestAuthorizedCupboardLink(BasePlayer viewer, BasePlayer target, float lifetime, int budget,
            bool spectatorSafe)
        {
            if (budget <= 0 || target == null) return 0;
            _cupboardCandidates.Clear();
            CollectStaticCandidates(_cupboardIndex, _cupboardCandidates, target.transform.position, Mathf.Min(150f, _config.General.MaximumStandardDistance));
            _cupboardCandidates.Sort(CompareCupboardCandidates);
            for (int i = 0; i < _cupboardCandidates.Count; i++)
            {
                BuildingPrivlidge cupboard = _cupboardCandidates[i].Entity;
                if (cupboard == null || cupboard.IsDestroyed || !cupboard.IsAuthed(target)) continue;
                if (spectatorSafe)
                    viewer.SendConsoleCommand("ddraw.arrow", lifetime, _cupboardDrawColor,
                        target.transform.position + Vector3.up, cupboard.transform.position + Vector3.up,
                        _config.Display.ArrowHeadRadius);
                else
                    viewer.SendConsoleCommand("ddraw.arrow", lifetime, _cupboardDrawColor,
                        target.transform.position + Vector3.up, cupboard.transform.position + Vector3.up,
                        _config.Display.ArrowHeadRadius, _config.Display.DistanceFade, _config.Display.DepthTest);
                return 1;
            }
            return 0;
        }

        private int DrawLoot(BasePlayer viewer, Vector3 origin, float radius, float lifetime, int budget)
        {
            _lootCandidates.Clear();
            CollectStaticCandidates(_lootIndex, _lootCandidates, origin, radius);
            _lootCandidates.Sort(CompareLootCandidates);
            int maximum = Mathf.Min(_config.Limits.MaximumLoot, budget);
            int draws = 0;

            for (int i = 0; i < _lootCandidates.Count && draws < maximum; i++)
            {
                StaticCandidate<BaseEntity> candidate = _lootCandidates[i];
                BaseEntity entity = candidate.Entity;
                if (entity == null || entity.IsDestroyed) continue;
                string label = "<size=12><color=#F2C94C>" + GetLootLabel(entity) + "</color> | <color=#2F6FFF>" +
                    Mathf.RoundToInt(Mathf.Sqrt(candidate.SqrDistance)) + "</color>M</size>";
                viewer.SendConsoleCommand("ddraw.text", lifetime, _lootDrawColor,
                    entity.transform.position + Vector3.up * _config.Display.StaticLabelHeight, label);
                draws++;
            }
            return draws;
        }

        private static string GetLootLabel(BaseEntity entity)
        {
            DroppedItem dropped = entity as DroppedItem;
            if (dropped != null && dropped.item != null && dropped.item.info != null)
                return EscapeRichText(dropped.item.info.displayName == null ? dropped.item.info.shortname : dropped.item.info.displayName.english).ToUpperInvariant();
            if (entity is PlayerCorpse) return "PLAYER CORPSE";
            if (entity is DroppedItemContainer) return "DROPPED LOOT";
            string name = entity.ShortPrefabName;
            if (string.IsNullOrEmpty(name)) return "LOOT";
            return EscapeRichText(name.Replace('_', ' ').Replace('.', ' ')).ToUpperInvariant();
        }

        private void CollectStaticCandidates<T>(Dictionary<long, List<T>> index, List<StaticCandidate<T>> results, Vector3 origin, float radius) where T : BaseEntity
        {
            if (index.Count == 0) return;
            float radiusSqr = radius * radius;
            int minX, maxX, minZ, maxZ;
            GetCellBounds(origin, radius, out minX, out maxX, out minZ, out maxZ);

            long cellCount = (long)(maxX - minX + 1) * (maxZ - minZ + 1);
            if (cellCount > index.Count)
            {
                foreach (List<T> entities in index.Values)
                    CollectStaticCandidatesFromList(entities, results, origin, radiusSqr);
                return;
            }

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    List<T> entities;
                    if (!index.TryGetValue(MakeCellKey(x, z), out entities)) continue;
                    CollectStaticCandidatesFromList(entities, results, origin, radiusSqr);
                }
            }
        }

        private static void CollectStaticCandidatesFromList<T>(List<T> entities,
            List<StaticCandidate<T>> results, Vector3 origin, float radiusSqr) where T : BaseEntity
        {
            for (int i = 0; i < entities.Count; i++)
            {
                T entity = entities[i];
                if (entity == null || entity.IsDestroyed) continue;
                float sqrDistance = (entity.transform.position - origin).sqrMagnitude;
                if (sqrDistance > radiusSqr) continue;
                results.Add(new StaticCandidate<T> { Entity = entity, SqrDistance = sqrDistance });
            }
        }

        #endregion

        #region Built-in vanish

        private sealed class VanishRuntimeState
        {
            public bool EnabledNoclip;
            public float Calories;
            public float Hydration;
            public float Temperature;
            public float Radiation;
            public float Oxygen;
            public float Wetness;
        }

        private bool ShouldRestoreVanish(BasePlayer player)
        {
            if (player == null || !HasPermission(player, PermVanish)) return false;
            if (HasExplicitPermission(player, PermVanishPermanent)) return true;
            if (_config.Vanish.VanishOnConnect) return true;
            return _config.Vanish.PersistVanishState && _storedData.VanishedUsers.Contains(player.userID);
        }

        private bool EnterVanish(BasePlayer player, bool notify)
        {
            return EnterVanish(player, notify, false);
        }

        private bool EnterVanish(BasePlayer player, bool notify, bool bypassPermission)
        {
            if (player == null || !player.IsConnected || IsBuiltInVanished(player)) return false;
            if (!bypassPermission && !HasPermission(player, PermVanish))
            {
                if (notify) Reply(player, "NoPermission");
                return false;
            }
            if (Interface.CallHook("OnVanishDisappear", player) != null) return false;

            VanishRuntimeState state = CaptureVanishRuntime(player);
            _vanishRuntime[player.userID] = state;
            _vanishedPlayers.Add(player.userID);
            _vanishStateCache.Remove(player.userID);

            if (_config.Vanish.BypassAntiHack)
                player.PauseFlyHackDetection(float.MaxValue);

            SimpleAIMemory.AddIgnorePlayer(player);
            BaseEntity.Query.Server.RemovePlayer(player);
            player.syncPosition = false;
            player.limitNetworking = true;
            player.isInvisible = true;
            player.GetHeldEntity()?.SetHeld(false);
            player.DisablePlayerCollider();

            List<Connection> connections = Pool.Get<List<Connection>>();
            try
            {
                foreach (Connection connection in Net.sv.connections)
                {
                    if (connection != null && connection.connected && connection.isAuthenticated &&
                        connection.player is BasePlayer && connection.player != player)
                        connections.Add(connection);
                }
                player.OnNetworkSubscribersLeave(connections);
            }
            finally
            {
                Pool.FreeUnmanaged(ref connections);
            }

            if (ServerOcclusion.OcclusionEnabled) player.OcclusionMakeSubscribersForget();
            if (player.GetComponent<SmartReconVanishController>() == null)
                player.gameObject.AddComponent<SmartReconVanishController>();

            if (_config.Vanish.EnableNoclip && !player.isMounted)
            {
                state.EnabledNoclip = true;
                EnsureVanishNoclip(player);
                NextTick(delegate { EnsureVanishNoclip(player); });
                timer.Once(0.25f, delegate { EnsureVanishNoclip(player); });
            }

            if (_config.Vanish.ShowNativeIndicator)
                player.SendConsoleCommand("debug.setinvis_ui true");

            if (_config.Vanish.PersistVanishState || HasExplicitPermission(player, PermVanishPermanent))
            {
                _storedData.VanishedUsers.Add(player.userID);
                _dataDirty = true;
                SaveData();
            }

            if (_vanishedPlayers.Count == 1) SubscribeVanishHooks();
            bool radarStarted = StartInvestigationRadar(player);
            Interface.CallHook("OnSmartInvestigationStarted", player, radarStarted);
            PlayVanishFeedbackSound(player, true);

            if (_config.Vanish.LogUsage)
                Puts(player.displayName + " (" + player.UserIDString + ") entered SmartRecon vanish.");
            if (notify && _config.Vanish.EnableNotifications)
                Reply(player, "VanishEnabled", radarStarted ? "ON" : "OFF");
            if (notify && _config.Investigation.StartRadarOnVanish && !radarStarted)
                Reply(player, "VanishRadarUnavailable");
            return true;
        }

        private bool ExitVanish(BasePlayer player, bool notify, bool preservePersistedState, bool force,
            bool keepRadar = false, bool playFeedback = true)
        {
            if (player == null) return false;
            bool managed = IsBuiltInVanished(player);
            if (!managed && !player._limitedNetworking) return false;
            if (!force && Interface.CallHook("OnVanishReappear", player) != null) return false;

            VanishRuntimeState state;
            _vanishRuntime.TryGetValue(player.userID, out state);
            DetachVanishRuntime(player);

            if (_config.Vanish.BypassAntiHack)
                player.PauseFlyHackDetection(0f);

            SimpleAIMemory.RemoveIgnorePlayer(player);
            BaseEntity.Query.Server.RemovePlayer(player);
            BaseEntity.Query.Server.AddPlayer(player);
            player.syncPosition = true;
            player.limitNetworking = false;
            player._limitedNetworking = false;
            player.isInvisible = false;
            player.EnablePlayerCollider();
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate();
            player.GetHeldEntity()?.SendNetworkUpdate();

            if (state != null)
            {
                RestoreMetabolism(player, state);
                if (state.EnabledNoclip && player.IsFlying) player.SendConsoleCommand("noclip");
            }

            if (_config.Vanish.ShowNativeIndicator)
                player.SendConsoleCommand("debug.setinvis_ui false");

            _vanishedPlayers.Remove(player.userID);
            _vanishRuntime.Remove(player.userID);
            _vanishStateCache.Remove(player.userID);
            if (!preservePersistedState && !HasExplicitPermission(player, PermVanishPermanent))
            {
                _storedData.VanishedUsers.Remove(player.userID);
                _dataDirty = true;
                SaveData();
            }
            if (_vanishedPlayers.Count == 0) UnsubscribeVanishHooks();

            if (!keepRadar && _config.Investigation.StopRadarOnReappear) StopRadar(player, false);
            Interface.CallHook("OnSmartInvestigationEnded", player);
            if (playFeedback) PlayVanishFeedbackSound(player, false);
            if (_config.Vanish.LogUsage)
                Puts(player.displayName + " (" + player.UserIDString + ") left SmartRecon vanish.");
            if (notify && _config.Vanish.EnableNotifications) Reply(player, "VanishDisabled");
            return true;
        }

        private void EnsureVanishNoclip(BasePlayer player)
        {
            if (player == null || !player.IsConnected || !IsBuiltInVanished(player) ||
                !_config.Vanish.EnableNoclip || player.isMounted || player.IsFlying) return;
            VanishRuntimeState state;
            if (_vanishRuntime.TryGetValue(player.userID, out state) && state != null)
                state.EnabledNoclip = true;
            player.SendConsoleCommand("noclip");
        }

        private void PlayVanishFeedbackSound(BasePlayer player, bool vanishing)
        {
            if (!_config.Vanish.EnableSoundEffects || player == null || player.net == null) return;
            string effectPath = vanishing ? _config.Vanish.VanishSoundEffect : _config.Vanish.ReappearSoundEffect;
            if (string.IsNullOrWhiteSpace(effectPath)) return;

            if (_config.Vanish.PublicSoundEffects)
            {
                Effect.server.Run(effectPath, player.transform.position);
                return;
            }

            if (player.net.connection == null) return;
            try
            {
                _vanishFeedbackEffectDepth++;
                EffectNetwork.Send(new Effect(effectPath, player, 0, Vector3.zero, Vector3.forward), player.net.connection);
            }
            finally
            {
                _vanishFeedbackEffectDepth--;
            }
        }

        private VanishRuntimeState CaptureVanishRuntime(BasePlayer player)
        {
            VanishRuntimeState state = new VanishRuntimeState();
            if (player == null || player.metabolism == null) return state;
            state.Calories = player.metabolism.calories.value;
            state.Hydration = player.metabolism.hydration.value;
            state.Temperature = player.metabolism.temperature.value;
            state.Radiation = player.metabolism.radiation_poison.value;
            state.Oxygen = player.metabolism.oxygen.value;
            state.Wetness = player.metabolism.wetness.value;
            return state;
        }

        private void MaintainVanishMetabolism(BasePlayer player)
        {
            if (!_config.Vanish.PauseMetabolism || player == null || player.metabolism == null) return;
            player.metabolism.calories.value = player.metabolism.calories.max;
            player.metabolism.hydration.value = player.metabolism.hydration.max;
            player.metabolism.temperature.value = 20f;
            player.metabolism.radiation_poison.value = 0f;
            player.metabolism.oxygen.value = player.metabolism.oxygen.max;
            player.metabolism.wetness.value = 0f;
        }

        private void RestoreMetabolism(BasePlayer player, VanishRuntimeState state)
        {
            if (!_config.Vanish.PauseMetabolism || player == null || player.metabolism == null || state == null) return;
            player.metabolism.calories.value = state.Calories;
            player.metabolism.hydration.value = state.Hydration;
            player.metabolism.temperature.value = state.Temperature;
            player.metabolism.radiation_poison.value = state.Radiation;
            player.metabolism.oxygen.value = state.Oxygen;
            player.metabolism.wetness.value = state.Wetness;
            player.SendNetworkUpdate();
        }

        private void DetachVanishRuntime(BasePlayer player)
        {
            if (player == null) return;
            SmartReconVanishController controller = player.GetComponent<SmartReconVanishController>();
            if (controller != null) UnityEngine.Object.Destroy(controller);
        }

        private void UpdateVanishNetworkGroup(BasePlayer player)
        {
            if (player == null || player.net == null) return;
            try
            {
                if (!_networkGroupCompatibilityResolved)
                {
                    MethodInfo oneArgument = null;
                    MethodInfo twoArguments = null;
                    MethodInfo[] methods = player.net.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        if (methods[i].Name != "UpdateGroups") continue;
                        ParameterInfo[] parameters = methods[i].GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Vector3)) oneArgument = methods[i];
                        else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(Vector3)) twoArguments = methods[i];
                    }
                    _networkUpdateGroupsMethod = twoArguments ?? oneArgument;

                    for (Type type = player.GetType(); type != null && _playerNetworkRangeMember == null; type = type.BaseType)
                    {
                        FieldInfo field = type.GetField("networkRange", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field != null) _playerNetworkRangeMember = field;
                        else
                        {
                            PropertyInfo property = type.GetProperty("networkRange", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (property != null) _playerNetworkRangeMember = property;
                        }
                    }
                    _networkGroupCompatibilityResolved = true;
                }

                if (_networkUpdateGroupsMethod == null) return;
                ParameterInfo[] updateParameters = _networkUpdateGroupsMethod.GetParameters();
                if (updateParameters.Length == 1)
                {
                    _networkUpdateGroupsMethod.Invoke(player.net, new object[] { player.transform.position });
                    return;
                }

                object range = null;
                FieldInfo rangeField = _playerNetworkRangeMember as FieldInfo;
                if (rangeField != null) range = rangeField.GetValue(player);
                else
                {
                    PropertyInfo rangeProperty = _playerNetworkRangeMember as PropertyInfo;
                    if (rangeProperty != null) range = rangeProperty.GetValue(player, null);
                }
                if (range == null && updateParameters[1].ParameterType.IsValueType)
                    range = Activator.CreateInstance(updateParameters[1].ParameterType);
                _networkUpdateGroupsMethod.Invoke(player.net, new[] { (object)player.transform.position, range });
            }
            catch (Exception exception)
            {
                if (_networkGroupCompatibilityWarningShown) return;
                _networkGroupCompatibilityWarningShown = true;
                PrintWarning("Vanish network-group compatibility update failed; radar and vanish remain active. " + exception.Message);
            }
        }

        private bool StartInvestigationRadar(BasePlayer player)
        {
            RadarSession session;
            if (!TryStartAutomaticRadar(player, _config.Investigation.StartRadarOnVanish,
                    _config.Investigation.ForceVisionArrows, out session)) return false;
            session.StartedByVanish = true;
            return true;
        }

        private bool StartSpectateRadar(BasePlayer player)
        {
            RadarSession session;
            if (!TryStartAutomaticRadar(player, _config.Investigation.StartRadarOnSpectate,
                    _config.Investigation.ForceVisionArrowsOnSpectate, out session)) return false;
            ApplySpectateSessionDefaults(player, session);
            return true;
        }

        private void ApplySpectateSessionDefaults(BasePlayer player, RadarSession session)
        {
            if (player == null || session == null) return;
            session.StartedBySpectate = true;
            session.ForcedArrows = _config.Investigation.ForceVisionArrowsOnSpectate &&
                HasPermission(player, PermArrows) && HasPermission(player, PermPlayers);
            session.ForcedPlayersLayer = session.ForcedArrows && !LayerPlayers(session.Preferences);
            session.NextPlayerUpdate = 0f;
            session.NextStaticUpdate = 0f;
            if (session.ForcedPlayersLayer) _nextPlayerIndexRebuild = 0f;
        }

        private void ReconcileSpectateSessions(float now)
        {
            if (now < _nextSpectateReconcile) return;
            _nextSpectateReconcile = now + 0.5f;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected || !player.IsSpectating()) continue;

                if (IsBuiltInVanished(player))
                    ExitVanish(player, false, false, true, true, false);

                RadarSession session;
                if (_sessions.TryGetValue(player.userID, out session) && session != null)
                {
                    if (!session.StartedBySpectate)
                    {
                        ApplySpectateSessionDefaults(player, session);
                        if (_config.UserInterface.ShowOnRadarStart) ShowRadarUi(player, session);
                    }
                    continue;
                }

                StartSpectateRadar(player);
            }

            _sessionRemovalBuffer.Clear();
            foreach (KeyValuePair<ulong, RadarSession> pair in _sessions)
            {
                RadarSession session = pair.Value;
                if (session == null || !session.StartedBySpectate ||
                    (session.Viewer != null && session.Viewer.IsConnected && session.Viewer.IsSpectating())) continue;
                _sessionRemovalBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _sessionRemovalBuffer.Count; i++)
            {
                RadarSession session;
                if (!_sessions.TryGetValue(_sessionRemovalBuffer[i], out session) || session == null) continue;
                if (_config.Investigation.StopRadarOnSpectateEnd)
                    StopRadar(session.Viewer, false);
                else
                {
                    session.StartedBySpectate = false;
                    session.ForcedPlayersLayer = false;
                    session.ForcedArrows = false;
                    if (session.Viewer != null && session.Preferences.ShowUi == true)
                        ShowRadarUi(session.Viewer, session);
                }
            }
            _sessionRemovalBuffer.Clear();
        }

        private bool TryStartAutomaticRadar(BasePlayer player, bool enabled, bool forceVisionArrows,
            out RadarSession session)
        {
            session = null;
            if (!enabled || player == null || !player.IsConnected || !HasPermission(player, PermUse)) return false;

            RadarPreferences preferences = _config.Investigation.UseSavedRadarPreferences
                ? GetPreferences(player.userID)
                : CreateDefaultPreferences();
            if (!_config.Investigation.UseSavedRadarPreferences)
            {
                preferences.Mode = _config.Investigation.RadarMode;
                ApplyModePreset(preferences, preferences.Mode);
            }

            string deniedFeature;
            if (!CanUsePreferences(player, preferences, out deniedFeature))
            {
                string fallbackMode = GetFirstPermittedMode(player);
                if (fallbackMode == null) return false;
                preferences.Mode = fallbackMode;
                ApplyModePreset(preferences, fallbackMode);
                preferences.NpcsLayer = false;
                preferences.LootLayer = false;
            }

            bool canForceVision = forceVisionArrows && HasPermission(player, PermArrows) &&
                HasPermission(player, PermPlayers);

            StartRadar(player, preferences, false);
            if (_sessions.TryGetValue(player.userID, out session))
            {
                session.ForcedArrows = canForceVision;
                session.ForcedPlayersLayer = canForceVision && !LayerPlayers(preferences);
                if (session.ForcedPlayersLayer) _nextPlayerIndexRebuild = 0f;
                if (_config.UserInterface.ShowOnRadarStart) ShowRadarUi(player, session);
            }
            return session != null;
        }

        private bool IsBuiltInVanished(BasePlayer player)
        {
            return player != null && _vanishedPlayers.Contains(player.userID);
        }

        private void SubscribeVanishHooks()
        {
            if (_vanishHooksSubscribed) return;
            Subscribe(nameof(OnPlayerColliderEnable));
            if (_config.Vanish.PreventIncomingDamage || _config.Vanish.PreventOutgoingDamage)
                Subscribe(nameof(OnEntityTakeDamage));
            if (_config.Vanish.EnableLockBypass) Subscribe(nameof(CanUseLockedEntity));
            if (_config.Vanish.BypassAntiHack) Subscribe(nameof(OnPlayerViolation));
            if (_config.Vanish.EnableMapMarkerTeleport) Subscribe(nameof(OnMapMarkerAdd));
            _vanishHooksSubscribed = true;
        }

        private void UnsubscribeVanishHooks()
        {
            Unsubscribe(nameof(OnPlayerColliderEnable));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(CanUseLockedEntity));
            Unsubscribe(nameof(OnPlayerViolation));
            Unsubscribe(nameof(OnMapMarkerAdd));
            _vanishHooksSubscribed = false;
        }

        private object OnPlayerColliderEnable(BasePlayer player, CapsuleCollider collider)
        {
            return IsBuiltInVanished(player) ? (object)true : null;
        }

        private void OnPlayerSpectate(BasePlayer player, string spectateFilter)
        {
            if (player == null) return;

            // Rust's spectator subscriber and SmartRecon limited networking must never overlap.
            if (IsBuiltInVanished(player))
                ExitVanish(player, false, false, true, true, false);

            NextTick(delegate
            {
                if (player == null || !player.IsConnected || !player.IsSpectating()) return;
                StartSpectateRadar(player);
            });
        }

        private void OnPlayerSpectateEnd(BasePlayer player, string spectateFilter)
        {
            if (player == null) return;
            RadarSession session;
            if (!_sessions.TryGetValue(player.userID, out session) || session == null || !session.StartedBySpectate)
                return;

            if (_config.Investigation.StopRadarOnSpectateEnd)
            {
                StopRadar(player, false);
                return;
            }

            session.StartedBySpectate = false;
            session.ForcedPlayersLayer = false;
            session.ForcedArrows = false;
            if (session.Preferences.ShowUi == true) ShowRadarUi(player, session);
        }

        private object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            return IsBuiltInVanished(player) ? (object)true : null;
        }

        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (!IsBuiltInVanished(player)) return null;
            return HasPermission(player, PermVanishUnlock) ? (object)true : null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            BasePlayer attacker = info.InitiatorPlayer;
            BasePlayer victim = entity.ToPlayer();
            bool attackerVanished = IsBuiltInVanished(attacker);
            bool victimVanished = IsBuiltInVanished(victim);
            if (!attackerVanished && !victimVanished) return null;
            if (victimVanished && _config.Vanish.PreventIncomingDamage) return true;
            if (attackerVanished && _config.Vanish.PreventOutgoingDamage && !HasPermission(attacker, PermVanishDamage))
                return true;
            return null;
        }

        private object OnMapMarkerAdd(BasePlayer player, ProtoBuf.MapNote note)
        {
            if (!IsBuiltInVanished(player) || note == null || player == null || !player.IsConnected ||
                player.IsSpectating() || player.isMounted || !player.IsAlive() || !HasPermission(player, PermVanishTeleport))
                return null;

            float now = Time.realtimeSinceStartup;
            float availableAt;
            if (_mapTeleportCooldowns.TryGetValue(player.userID, out availableAt) && now < availableAt)
            {
                if (_config.Vanish.RemoveTeleportMarker)
                {
                    RemoveTeleportMapNote(player, note);
                    return true;
                }
                return null;
            }
            _mapTeleportCooldowns[player.userID] = now + _config.Vanish.MapTeleportCooldown;

            Vector3 destination = note.worldPosition;
            destination.y = GetMapTeleportHeight(destination);
            if (_config.Vanish.PreserveNoclipAltitude && player.IsFlying)
                destination.y = Mathf.Max(destination.y, player.transform.position.y);
            destination.y += _config.Vanish.MapTeleportHeightOffset;

            Vector3 origin = player.transform.position;
            player.Teleport(destination);
            player.RemoveFromTriggers();
            player.ForceUpdateTriggers();
            UpdateVanishNetworkGroup(player);
            if (_config.Vanish.LogMapMarkerTeleports)
                LogMapMarkerTeleport(player, origin, destination);
            if (_config.Vanish.RemoveTeleportMarker)
            {
                RemoveTeleportMapNote(player, note);
                return true;
            }
            return null;
        }

        private void LogMapMarkerTeleport(BasePlayer player, Vector3 origin, Vector3 destination)
        {
            string entry = string.Format(CultureInfo.InvariantCulture,
                "[{0:O}] {1} ({2}) teleported while vanished from ({3:0.0}, {4:0.0}, {5:0.0}) to ({6:0.0}, {7:0.0}, {8:0.0}).",
                DateTime.UtcNow, player.displayName, player.UserIDString,
                origin.x, origin.y, origin.z, destination.x, destination.y, destination.z);
            LogToFile("teleports", entry, this, false);
        }

        private static float GetMapTeleportHeight(Vector3 position)
        {
            float terrainHeight = TerrainMeta.HeightMap.GetHeight(position);
            RaycastHit hit;
            Vector3 rayOrigin = new Vector3(position.x, Mathf.Max(terrainHeight, position.y) + 500f, position.z);
            int layerMask = Rust.Layers.Mask.Vehicle_Large | Rust.Layers.Solid | Rust.Layers.Mask.Water;
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 1000f, layerMask))
                return Mathf.Max(terrainHeight, hit.point.y);
            return terrainHeight;
        }

        private static void RemoveTeleportMapNote(BasePlayer player, ProtoBuf.MapNote note)
        {
            if (note == null) return;
            if (player != null && player.State != null)
            {
                if (player.State.pointsOfInterest != null) player.State.pointsOfInterest.Remove(note);
                player.DirtyPlayerState();
                player.SendMarkersToClient();
            }
            note.Dispose();
        }

        private BasePlayer FindPlayer(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;
            ulong userId;
            if (ulong.TryParse(query, out userId)) return BasePlayer.FindAwakeOrSleepingByID(userId);
            BasePlayer partial = null;
            foreach (BasePlayer player in BasePlayer.allPlayerList)
            {
                if (player == null || string.IsNullOrEmpty(player.displayName)) continue;
                if (string.Equals(player.displayName, query, StringComparison.OrdinalIgnoreCase)) return player;
                if (partial == null && player.displayName.StartsWith(query, StringComparison.OrdinalIgnoreCase)) partial = player;
            }
            return partial;
        }

        private BasePlayer RaycastPlayer(BasePlayer viewer, float distance)
        {
            RaycastHit hit;
            int mask = LayerMask.GetMask(LayerMask.LayerToName((int)Layer.Player_Server));
            if (!Physics.Raycast(viewer.eyes.HeadRay(), out hit, distance, mask)) return null;
            return hit.GetEntity() as BasePlayer;
        }

        private void OpenPlayerInventory(BasePlayer viewer, BasePlayer target)
        {
            if (viewer == null || target == null) return;
            viewer.inventory.loot.Clear();
            viewer.inventory.loot.AddContainer(target.inventory.containerMain);
            viewer.inventory.loot.AddContainer(target.inventory.containerWear);
            viewer.inventory.loot.AddContainer(target.inventory.containerBelt);
            viewer.inventory.loot.entitySource = RelationshipManager.ServerInstance;
            viewer.inventory.loot.PositionChecks = false;
            viewer.inventory.loot.MarkDirty();
            viewer.inventory.loot.SendImmediate();
            viewer.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", viewer), "player_corpse");
        }

        private void HandleVanishInteraction(BasePlayer player)
        {
            if (!_config.Vanish.EnableReloadInteraction || player == null || !IsBuiltInVanished(player)) return;
            RaycastHit hit;
            int mask = LayerMask.GetMask(
                LayerMask.LayerToName((int)Layer.Construction),
                LayerMask.LayerToName((int)Layer.Deployed),
                LayerMask.LayerToName((int)Layer.Vehicle_World),
                LayerMask.LayerToName((int)Layer.Player_Server));
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 5f, mask)) return;
            BaseEntity entity = hit.GetEntity() as BaseEntity;
            if (entity == null) return;

            BasePlayer target = entity as BasePlayer;
            if (target != null)
            {
                if (HasPermission(player, PermVanishInventory)) OpenPlayerInventory(player, target);
                return;
            }

            StorageContainer container = entity as StorageContainer;
            if (container != null)
            {
                if (!HasPermission(player, PermVanishInventory)) return;
                player.inventory.loot.Clear();
                player.inventory.loot.AddContainer(container.inventory);
                player.inventory.loot.entitySource = container;
                player.inventory.loot.PositionChecks = false;
                player.inventory.loot.MarkDirty();
                player.inventory.loot.SendImmediate();
                player.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", player), "generic_resizable");
                return;
            }

            if (!HasPermission(player, PermVanishUnlock)) return;
            Door door = entity as Door;
            if (door != null)
            {
                door.SetOpen(!door.IsOpen(), false);
                return;
            }
            BaseMountable mountable = entity.GetComponent<BaseMountable>();
            if (mountable != null) mountable.AttemptMount(player, true);
        }

        public sealed class SmartReconVanishController : FacepunchBehaviour
        {
            private BasePlayer _player;
            private Vector3 _originalScale;
            private float _nextMetabolismUpdate;
            private float _nextNetworkGroupUpdate;

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                if (_player == null) return;
                _originalScale = _player.transform.localScale;
                _player.transform.localScale = Vector3.zero;
                _nextMetabolismUpdate = 0f;
                _nextNetworkGroupUpdate = 0f;
            }

            private void FixedUpdate()
            {
                if (_player == null || Instance == null) return;
                float now = Time.realtimeSinceStartup;
                if (now >= _nextMetabolismUpdate)
                {
                    Instance.MaintainVanishMetabolism(_player);
                    _nextMetabolismUpdate = now + 0.25f;
                }
                if (now >= _nextNetworkGroupUpdate)
                {
                    Instance.UpdateVanishNetworkGroup(_player);
                    _nextNetworkGroupUpdate = now + 2f;
                }
                if (_player.serverInput != null && _player.serverInput.IsDown(BUTTON.RELOAD) &&
                    !_player.serverInput.WasDown(BUTTON.RELOAD))
                    Instance.HandleVanishInteraction(_player);
            }

            private void OnDestroy()
            {
                if (_player != null) _player.transform.localScale = _originalScale == Vector3.zero ? Vector3.one : _originalScale;
            }
        }

        public void Disappear(BasePlayer player) { EnterVanish(player, false, true); }
        public void Reappear(BasePlayer player) { ExitVanish(player, false, false, false); }
        public bool IsInvisible(BasePlayer player) { return IsBuiltInVanished(player) || (player != null && player._limitedNetworking); }
        public void _Disappear(BasePlayer player) { Disappear(player); }
        public void _Reappear(BasePlayer player) { Reappear(player); }
        public bool _IsInvisible(BasePlayer player) { return IsInvisible(player); }

        public bool IsRadarEnabled(BasePlayer player)
        {
            return player != null && _sessions.ContainsKey(player.userID);
        }

        public bool EnableRadar(BasePlayer player)
        {
            if (player == null || !player.IsConnected || !HasPermission(player, PermUse)) return false;
            StartRadar(player, GetPreferences(player.userID), false);
            return _sessions.ContainsKey(player.userID);
        }

        public bool DisableRadar(BasePlayer player)
        {
            if (player == null || !_sessions.ContainsKey(player.userID)) return false;
            StopRadar(player, false);
            return true;
        }

        public bool IsRadarLayerEnabled(BasePlayer player, string layer)
        {
            RadarSession session;
            if (player == null || !_sessions.TryGetValue(player.userID, out session) || session == null) return false;
            switch ((layer ?? string.Empty).ToLowerInvariant())
            {
                case "players": return LayerPlayers(session.Preferences) || session.ForcedPlayersLayer;
                case "npcs": return LayerNpcs(session.Preferences);
                case "loot": return LayerLoot(session.Preferences);
                case "stashes": return LayerStashes(session.Preferences);
                case "tc":
                case "tcs":
                case "cupboards": return LayerCupboards(session.Preferences);
                default: return false;
            }
        }

        #endregion

        #region Permissions, privacy, and filters

        private void RegisterPermissions()
        {
            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermPlayers, this);
            permission.RegisterPermission(PermStashes, this);
            permission.RegisterPermission(PermCupboards, this);
            permission.RegisterPermission(PermArrows, this);
            permission.RegisterPermission(PermVoice, this);
            permission.RegisterPermission(PermSleepers, this);
            permission.RegisterPermission(PermExtendedRange, this);
            permission.RegisterPermission(PermSeeVanished, this);
            permission.RegisterPermission(PermSeeOwners, this);
            permission.RegisterPermission(PermVanish, this);
            permission.RegisterPermission(PermVanishPermanent, this);
            permission.RegisterPermission(PermVanishUnlock, this);
            permission.RegisterPermission(PermVanishDamage, this);
            permission.RegisterPermission(PermVanishInventory, this);
            permission.RegisterPermission(PermVanishTeleport, this);
            permission.RegisterPermission(PermNpcs, this);
            permission.RegisterPermission(PermLoot, this);
            permission.RegisterPermission(PermExtended, this);
            permission.RegisterPermission(PermTcInfo, this);
            permission.RegisterPermission(PermUi, this);
            permission.RegisterPermission(PermForensics, this);
        }

        private static bool PermissionMatches(string suppliedPermission, string currentPermission)
        {
            return string.Equals(suppliedPermission, currentPermission, StringComparison.OrdinalIgnoreCase);
        }

        private bool HasExplicitPermission(BasePlayer player, string permissionName)
        {
            if (player == null) return false;
            string[] directPermissions = permission.GetUserPermissions(player.UserIDString);
            if (directPermissions == null) return false;
            for (int i = 0; i < directPermissions.Length; i++)
            {
                if (string.Equals(directPermissions[i], permissionName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private bool HasPermission(BasePlayer player, string permissionName)
        {
            if (player == null) return false;
            bool explicitPermission = permission.UserHasPermission(player.UserIDString, permissionName);
            if (permissionName == PermSeeVanished || permissionName == PermSeeOwners)
                return explicitPermission || (_config.General.AdminsBypassPermissions && GetAuthLevel(player) >= 2);
            if (_config.General.AdminsBypassPermissions && GetAuthLevel(player) > 0) return true;
            return explicitPermission;
        }

        private bool CanUseMode(BasePlayer player, string mode, out string deniedFeature)
        {
            deniedFeature = null;
            if ((mode == ModePlayers || mode == ModeAll) && !HasPermission(player, PermPlayers)) deniedFeature = ModePlayers;
            else if ((mode == ModeStashes || mode == ModeAll) && !HasPermission(player, PermStashes)) deniedFeature = ModeStashes;
            else if ((mode == ModeCupboards || mode == ModeAll) && !HasPermission(player, PermCupboards)) deniedFeature = ModeCupboards;
            return deniedFeature == null;
        }

        private bool CanUsePreferences(BasePlayer player, RadarPreferences preferences, out string deniedFeature)
        {
            deniedFeature = null;
            if (LayerPlayers(preferences) && !HasPermission(player, PermPlayers)) deniedFeature = ModePlayers;
            else if (LayerStashes(preferences) && !HasPermission(player, PermStashes)) deniedFeature = ModeStashes;
            else if (LayerCupboards(preferences) && !HasPermission(player, PermCupboards)) deniedFeature = ModeCupboards;
            else if (LayerNpcs(preferences) && !HasPermission(player, PermNpcs)) deniedFeature = "npcs";
            else if (LayerLoot(preferences) && !HasPermission(player, PermLoot)) deniedFeature = "loot";
            return deniedFeature == null;
        }

        private string GetFirstPermittedMode(BasePlayer player)
        {
            if (HasPermission(player, PermPlayers)) return ModePlayers;
            if (HasPermission(player, PermStashes)) return ModeStashes;
            if (HasPermission(player, PermCupboards)) return ModeCupboards;
            return null;
        }

        private struct VanishCacheEntry
        {
            public bool IsVanished;
            public float ExpiresAt;
        }

        private bool IsPlayerVanished(BasePlayer target)
        {
            if (target == null) return false;
            float now = Time.realtimeSinceStartup;
            VanishCacheEntry cached;
            if (_vanishStateCache.TryGetValue(target.userID, out cached) && now < cached.ExpiresAt)
                return cached.IsVanished;

            bool isVanished = IsBuiltInVanished(target) ||
                (_config.Privacy.TreatLimitedNetworkingAsVanished && target._limitedNetworking);

            _vanishStateCache[target.userID] = new VanishCacheEntry
            {
                IsVanished = isVanished,
                ExpiresAt = now + Mathf.Max(0.1f, _config.Scheduler.PlayerIndexRefresh)
            };
            return isVanished;
        }

        private static int GetAuthLevel(BasePlayer player)
        {
            if (player == null) return 0;
            if (player.Connection == null) return player.IsAdmin ? 2 : 0;
            return (int)player.Connection.authLevel;
        }

        private static bool IsHumanoidNpc(BasePlayer player)
        {
            return player != null && (player is NPCPlayer || !player.userID.IsSteamId());
        }

        private static bool PassesAuthorizationFilter(int authLevel, string filter)
        {
            switch (filter)
            {
                case "players": return authLevel == 0;
                case "staff": return authLevel > 0;
                case "moderators": return authLevel == 1;
                case "owners": return authLevel >= 2;
                default: return true;
            }
        }

        private static bool PassesTeamFilter(BasePlayer viewer, BasePlayer target, string filter)
        {
            bool sameTeam = viewer.currentTeam != 0 && viewer.currentTeam == target.currentTeam;
            switch (filter)
            {
                case "mine": return sameTeam;
                case "others": return !sameTeam;
                case "solo": return target.currentTeam == 0;
                default: return true;
            }
        }

        private static bool PassesSafeZoneFilter(BasePlayer target, string filter)
        {
            bool inSafeZone = target.InSafeZone();
            switch (filter)
            {
                case "inside": return inSafeZone;
                case "outside": return !inSafeZone;
                default: return true;
            }
        }

        private static string NormalizeMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return null;
            switch (mode.Trim().ToLowerInvariant())
            {
                case "player":
                case "players": return ModePlayers;
                case "stash":
                case "stashes": return ModeStashes;
                case "tc":
                case "tcs":
                case "cupboard":
                case "cupboards": return ModeCupboards;
                case "all": return ModeAll;
                case "custom": return ModeCustom;
                default: return null;
            }
        }

        private static void ApplyModePreset(RadarPreferences preferences, string mode)
        {
            if (preferences == null || mode == ModeCustom) return;
            preferences.PlayersLayer = mode == ModePlayers || mode == ModeAll;
            preferences.StashesLayer = mode == ModeStashes || mode == ModeAll;
            preferences.CupboardsLayer = mode == ModeCupboards || mode == ModeAll;
        }

        private static bool LayerPlayers(RadarPreferences preferences)
        {
            return preferences != null && (preferences.PlayersLayer ?? (preferences.Mode == ModePlayers || preferences.Mode == ModeAll));
        }

        private static bool LayerStashes(RadarPreferences preferences)
        {
            return preferences != null && (preferences.StashesLayer ?? (preferences.Mode == ModeStashes || preferences.Mode == ModeAll));
        }

        private static bool LayerCupboards(RadarPreferences preferences)
        {
            return preferences != null && (preferences.CupboardsLayer ?? (preferences.Mode == ModeCupboards || preferences.Mode == ModeAll));
        }

        private static bool LayerNpcs(RadarPreferences preferences)
        {
            return preferences != null && preferences.NpcsLayer == true;
        }

        private static bool LayerLoot(RadarPreferences preferences)
        {
            return preferences != null && preferences.LootLayer == true;
        }

        private static bool HasPlayerLayers(RadarPreferences preferences)
        {
            return LayerPlayers(preferences) || LayerNpcs(preferences);
        }

        private static bool HasStaticLayers(RadarPreferences preferences)
        {
            return LayerStashes(preferences) || LayerCupboards(preferences) || LayerLoot(preferences);
        }

        private static string NormalizeTeamFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return null;
            switch (filter.Trim().ToLowerInvariant())
            {
                case "all": return "all";
                case "mine":
                case "team": return "mine";
                case "others":
                case "other": return "others";
                case "solo":
                case "noteam": return "solo";
                default: return null;
            }
        }

        private static string NormalizeAuthorizationFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return null;
            switch (filter.Trim().ToLowerInvariant())
            {
                case "all": return "all";
                case "player":
                case "players": return "players";
                case "staff":
                case "admins": return "staff";
                case "moderator":
                case "moderators":
                case "mods": return "moderators";
                case "owner":
                case "owners": return "owners";
                default: return null;
            }
        }

        private static string NormalizeSafeZoneFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return null;
            switch (filter.Trim().ToLowerInvariant())
            {
                case "all": return "all";
                case "inside":
                case "in": return "inside";
                case "outside":
                case "out": return "outside";
                default: return null;
            }
        }

        #endregion

        #region Helpers

        private static Vector3 GetRadarOrigin(BasePlayer viewer)
        {
            BasePlayer target = GetSpectatingTarget(viewer);
            return target != null && target.IsConnected ? target.transform.position : viewer.transform.position;
        }

        private static BasePlayer GetSpectatingTarget(BasePlayer viewer)
        {
            if (viewer == null || !viewer.IsSpectating()) return null;
            BasePlayer target = viewer.SpectatingTarget;
            return target != null && target.IsConnected ? target : null;
        }

        private static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Unknown";
            return value.Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private string GetTeamColorHex(ulong teamId)
        {
            string cached;
            if (_teamColorCache.TryGetValue(teamId, out cached)) return cached;
            uint hash = (uint)(teamId ^ (teamId >> 32));
            float hue = (hash % 360u) / 360f;
            Color color = Color.HSVToRGB(hue, 0.72f, 1f);
            cached = "#" + ColorUtility.ToHtmlStringRGB(color);
            _teamColorCache[teamId] = cached;
            return cached;
        }

        #endregion

        #region Vanish sound isolation patches

        [HarmonyPatch(typeof(BaseNetworkable), "GetConnectionsWithin", typeof(Vector3), typeof(float), typeof(bool)), AutoPatch]
        private static class GetConnectionsWithinPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ref List<Connection> __result, Vector3 position, float distance)
            {
                if (Instance == null || __result == null || Instance._vanishedPlayers.Count == 0) return;
                float distanceSquared = distance * distance;
                foreach (ulong userId in Instance._vanishedPlayers)
                {
                    BasePlayer player = BasePlayer.FindByID(userId);
                    if (player == null || !player.IsConnected || player.Connection == null ||
                        (player.transform.position - position).sqrMagnitude > distanceSquared)
                        continue;
                    bool exists = false;
                    for (int i = 0; i < __result.Count; i++)
                    {
                        if (__result[i] == player.Connection)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) __result.Add(player.Connection);
                }
            }
        }

        [HarmonyPatch(typeof(BaseEntity), "SignalBroadcast", typeof(BaseEntity.Signal), typeof(string), typeof(Connection), typeof(string), typeof(float)), AutoPatch]
        private static class SignalBroadcastPatch
        {
            [HarmonyPrefix]
            private static bool Prefix([HarmonyArgument(2)] Connection sourceConnection)
            {
                return sourceConnection == null || Instance == null ||
                    !Instance._vanishedPlayers.Contains(sourceConnection.userid);
            }
        }

        [HarmonyPatch, AutoPatch]
        private static class EffectNetworkSendPatch
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> TargetMethods()
            {
                MethodInfo[] methods = typeof(EffectNetwork).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                for (int i = 0; i < methods.Length; i++)
                {
                    if (methods[i].Name != "Send") continue;
                    ParameterInfo[] parameters = methods[i].GetParameters();
                    if (parameters.Length > 0 && parameters[0].ParameterType == typeof(Effect)) yield return methods[i];
                }
            }

            [HarmonyPrefix]
            private static bool Prefix([HarmonyArgument(0)] Effect effect)
            {
                return effect == null || effect.source == 0 || Instance == null || Instance._vanishFeedbackEffectDepth > 0 ||
                    !Instance._vanishedPlayers.Contains(effect.source);
            }
        }

        #endregion
    }
}
