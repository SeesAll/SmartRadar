using Facepunch;
using HarmonyLib;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using Rust.Ai;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SmartRadar", "SeesAll", "1.1.1")]
    [Description("Unified administrative vanish and high-performance radar for Rust")]
    public class SmartRadar : RustPlugin
    {
        #region Constants and references

        private const string PermUse = "smartradar.use";
        private const string PermPlayers = "smartradar.players";
        private const string PermStashes = "smartradar.stashes";
        private const string PermCupboards = "smartradar.cupboards";
        private const string PermArrows = "smartradar.arrows";
        private const string PermVoice = "smartradar.voice";
        private const string PermSleepers = "smartradar.sleepers";
        private const string PermExtendedRange = "smartradar.extendedrange";
        private const string PermSeeVanished = "smartradar.seevanished";
        private const string PermSeeOwners = "smartradar.seeowners";
        private const string PermVanish = "smartradar.vanish";
        private const string PermVanishPermanent = "smartradar.vanish.permanent";
        private const string PermVanishUnlock = "smartradar.vanish.unlock";
        private const string PermVanishDamage = "smartradar.vanish.damage";
        private const string PermVanishInventory = "smartradar.vanish.inventory";
        private const string PermVanishTeleport = "smartradar.vanish.teleport";

        private const string ModePlayers = "players";
        private const string ModeStashes = "stashes";
        private const string ModeCupboards = "tcs";
        private const string ModeAll = "all";

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
        private readonly Dictionary<int, long> _stashCells = new Dictionary<int, long>();
        private readonly Dictionary<int, long> _cupboardCells = new Dictionary<int, long>();

        private readonly List<PlayerCandidate> _playerCandidates = new List<PlayerCandidate>(256);
        private readonly List<StaticCandidate<StashContainer>> _stashCandidates = new List<StaticCandidate<StashContainer>>(256);
        private readonly List<StaticCandidate<BuildingPrivlidge>> _cupboardCandidates = new List<StaticCandidate<BuildingPrivlidge>>(256);
        private readonly List<ulong> _sessionRemovalBuffer = new List<ulong>();
        private readonly StringBuilder _labelBuilder = new StringBuilder(256);
        private readonly Dictionary<ulong, string> _teamColorCache = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, VanishCacheEntry> _vanishStateCache = new Dictionary<ulong, VanishCacheEntry>();
        private readonly HashSet<ulong> _vanishedPlayers = new HashSet<ulong>();
        private readonly Dictionary<ulong, VanishRuntimeState> _vanishRuntime = new Dictionary<ulong, VanishRuntimeState>();

        private float _nextPlayerIndexRebuild;
        private float _nextSleeperIndexRebuild;
        private float _nextStaticIndexRebuild;
        private float _nextVoicePrune;
        private int _staggerSequence;
        private int _voiceWatcherCount;
        private bool _vanishHooksSubscribed;
        private bool _networkGroupCompatibilityResolved;
        private bool _networkGroupCompatibilityWarningShown;
        private MethodInfo _networkUpdateGroupsMethod;
        private MemberInfo _playerNetworkRangeMember;

        private static SmartRadar Instance;

        private Color _playerDrawColor;
        private Color _stashDrawColor;
        private Color _cupboardDrawColor;
        private Color _arrowDrawColor;

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
        }

        private sealed class GeneralSettings
        {
            [JsonProperty("Command aliases", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] CommandAliases = { "radar", "sradar", "smartradar" };

            [JsonProperty("Rust moderators and owners bypass SmartRadar permissions")]
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

            [JsonProperty("Maximum distance with smartradar.extendedrange")]
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
        }

        private sealed class PrivacySettings
        {
            [JsonProperty("Hide vanished players unless explicitly enabled and permitted")]
            public bool HideVanishedPlayers = true;

            [JsonProperty("Treat any Rust limited-networking player as vanished")]
            public bool TreatLimitedNetworkingAsVanished = true;

            [JsonProperty("Mark visible vanished players with [V]")]
            public bool MarkVanishedPlayers = true;

            [JsonProperty("Hide owners from moderators unless they have smartradar.seeowners")]
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

            [JsonProperty("Allow lock bypass with smartradar.vanish.unlock")]
            public bool EnableLockBypass = true;

            [JsonProperty("Enable inventory inspection commands")]
            public bool EnableInventoryInspection = true;

            [JsonProperty("Enable reload-key investigative interaction while vanished")]
            public bool EnableReloadInteraction = true;

            [JsonProperty("Enable reload plus map-marker teleport with smartradar.vanish.teleport")]
            public bool EnableMapMarkerTeleport = false;

            [JsonProperty("Show Rust's native invisibility indicator")]
            public bool ShowNativeIndicator = true;

            [JsonProperty("Show vanish chat notifications")]
            public bool EnableNotifications = true;

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

            if (_config.General.CommandAliases == null || _config.General.CommandAliases.Length == 0)
                _config.General.CommandAliases = new[] { "radar", "sradar", "smartradar" };
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
            _config.Limits.MaximumDrawCommandsPerCycle = Mathf.Clamp(_config.Limits.MaximumDrawCommandsPerCycle, 1, 1500);

            _config.Display.VoiceIndicatorDuration = Mathf.Clamp(_config.Display.VoiceIndicatorDuration, 0.1f, 30f);
            _config.Display.PlayerLabelHeight = Mathf.Clamp(_config.Display.PlayerLabelHeight, 0f, 10f);
            _config.Display.StaticLabelHeight = Mathf.Clamp(_config.Display.StaticLabelHeight, 0f, 10f);
            _config.Display.ArrowLength = Mathf.Clamp(_config.Display.ArrowLength, 0.5f, 25f);
            _config.Display.ArrowHeadRadius = Mathf.Clamp(_config.Display.ArrowHeadRadius, 0.01f, 2f);
            _config.Display.PlayerLabelScale = Mathf.Clamp(_config.Display.PlayerLabelScale, 0.25f, 3f);

            _playerDrawColor = ParseColor(_config.Display.PlayerDrawingColor, Color.white);
            _stashDrawColor = ParseColor(_config.Display.StashDrawingColor, new Color(0.9f, 0.26f, 0.96f));
            _cupboardDrawColor = ParseColor(_config.Display.CupboardDrawingColor, new Color(0.02f, 0.96f, 0.9f));
            _arrowDrawColor = ParseColor(_config.Display.ArrowDrawingColor, Color.white);
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
                NameFilter = string.Empty,
                TeamFilter = "all",
                AuthorizationFilter = "all",
                SafeZoneFilter = "all"
            };
        }

        private void NormalizePreferences(RadarPreferences preferences)
        {
            preferences.Mode = NormalizeMode(preferences.Mode) ?? _config.General.DefaultMode;
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
                ["NoPermission"] = "You do not have permission to use SmartRadar.",
                ["FeaturePermission"] = "You do not have permission to use the '{0}' radar feature.",
                ["Enabled"] = "SmartRadar enabled: {0} mode, {1:0.#}m, {2:0.##}s player refresh.",
                ["Disabled"] = "SmartRadar disabled.",
                ["AlreadyDisabled"] = "SmartRadar is already disabled.",
                ["StatusOn"] = "SmartRadar: ON | mode={0} | distance={1:0.#}m | rate={2:0.##}s | arrows={3} | voice={4} | sleepers={5} | vanished={6} | team={7} | auth={8} | safezone={9} | name={10} | expires={11}",
                ["StatusOff"] = "SmartRadar: OFF | saved mode={0}, distance={1:0.#}m, rate={2:0.##}s.",
                ["InvalidMode"] = "Invalid mode. Use players, stashes, tcs, or all.",
                ["InvalidNumber"] = "'{0}' must be a positive finite number.",
                ["DistanceTooHigh"] = "Maximum permitted radar distance is {0:0.#}m.",
                ["RateOutOfRange"] = "Refresh rate must be between {0:0.##} and {1:0.##} seconds.",
                ["SettingChanged"] = "SmartRadar {0} set to {1}.",
                ["FilterChanged"] = "SmartRadar {0} filter set to {1}.",
                ["SettingsReset"] = "SmartRadar settings reset to defaults.",
                ["Help"] = "SmartRadar commands:\n/radar - toggle\n/radar <players|stashes|tcs|all> [distance] [rate]\n/radar on|off|status|reset\n/radar mode <mode>\n/radar distance <meters>\n/radar rate <seconds>\n/radar for <seconds>\n/radar arrows|voice|sleepers|vanished [on|off]\n/radar filter name <text|off>\n/radar filter team <all|mine|others|solo>\n/radar filter auth <all|players|staff|moderators|owners>\n/radar filter safezone <all|inside|outside>\nLegacy: /radar <rate> <distance> <mode>",
                ["VanishedUnavailable"] = "Viewing vanished players is disabled or not permitted.",
                ["ConsolePlayerOnly"] = "SmartRadar must be controlled by an in-game player.",
                ["DurationSet"] = "SmartRadar will automatically disable in {0:0.#} seconds.",
                ["DurationTooHigh"] = "Maximum temporary radar duration is {0:0.#} seconds.",
                ["Expired"] = "SmartRadar's temporary duration expired.",
                ["VanishEnabled"] = "SmartRadar vanish enabled. Investigative radar: {0}.",
                ["VanishDisabled"] = "SmartRadar vanish disabled. Investigative radar stopped.",
                ["VanishAlreadyEnabled"] = "SmartRadar vanish is already enabled.",
                ["VanishAlreadyDisabled"] = "SmartRadar vanish is already disabled.",
                ["VanishPermanent"] = "Your permanent-vanish permission prevents reappearing.",
                ["VanishStatus"] = "SmartRadar vanish: {0} | radar: {1} | arrows: {2}.",
                ["VanishHelp"] = "SmartRadar vanish commands:\n/vanish - toggle\n/vanish on|off|status\n/inv <name|steamid> - inspect a player's inventory",
                ["InventoryNoTarget"] = "No matching active or sleeping player was found for '{0}'.",
                ["InventoryUsage"] = "Usage: /inv <name or Steam ID>, or look directly at a nearby player and use /inv.",
                ["VanishRadarUnavailable"] = "Vanish enabled, but investigative radar could not start because its command or mode permissions are missing."
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

            List<BasePlayer> hidden = new List<BasePlayer>();
            foreach (ulong userId in _vanishedPlayers)
            {
                BasePlayer player = BasePlayer.FindByID(userId);
                if (player != null) hidden.Add(player);
            }
            for (int i = 0; i < hidden.Count; i++) ExitVanish(hidden[i], false, true, true);

            _sessions.Clear();
            _voiceActivity.Clear();
            _teamColorCache.Clear();
            _vanishStateCache.Clear();
            _vanishedPlayers.Clear();
            _vanishRuntime.Clear();
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
                if (cupboard != null) IndexCupboard(cupboard);
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
            if (cupboard != null) RemoveCupboard(cupboard);
        }

        private void OnUserPermissionRevoked(string id, string permissionName)
        {
            ulong userId;
            if (!ulong.TryParse(id, out userId)) return;
            BasePlayer player = BasePlayer.FindByID(userId);
            if (player == null) return;

            if (string.Equals(permissionName, PermUse, StringComparison.OrdinalIgnoreCase) && !HasPermission(player, PermUse))
                StopRadar(player, true);
            else if (string.Equals(permissionName, PermVanish, StringComparison.OrdinalIgnoreCase) &&
                     !HasPermission(player, PermVanish) && IsBuiltInVanished(player))
                ExitVanish(player, true, false, false);
        }

        private void OnUserPermissionGranted(string id, string permissionName)
        {
            if (!string.Equals(permissionName, PermVanishPermanent, StringComparison.OrdinalIgnoreCase)) return;
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
                    if (!CanUseMode(player, preferences.Mode, out resetDeniedFeature))
                    {
                        string permittedMode = GetFirstPermittedMode(player);
                        if (permittedMode == null)
                        {
                            Reply(player, "FeaturePermission", resetDeniedFeature);
                            return;
                        }
                        preferences.Mode = permittedMode;
                    }
                    preferences.Distance = Mathf.Min(preferences.Distance, HasPermission(player, PermExtendedRange)
                        ? _config.General.MaximumExtendedDistance
                        : _config.General.MaximumStandardDistance);
                    _storedData.Preferences[player.userID] = preferences;
                    if (session != null) session.Preferences = preferences;
                    MarkPreferencesChanged(session);
                    RefreshVoiceWatcherCount();
                    Reply(player, "SettingsReset");
                    return;
                case "mode":
                    if (args.Length < 2 || !TrySetMode(player, preferences, args[1])) return;
                    MarkPreferencesChanged(session);
                    Reply(player, "SettingChanged", "mode", preferences.Mode);
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

            float legacyRate;
            if (args.Length >= 2 && TryParsePositiveFloat(args[0], out legacyRate))
            {
                string legacyMode = args.Length >= 3 ? args[2] : ModePlayers;
                RadarPreferences candidate = ClonePreferences(preferences);
                if (!TrySetRate(player, candidate, args[0])) return;
                if (!TrySetDistance(player, candidate, args[1])) return;
                if (!TrySetMode(player, candidate, legacyMode)) return;
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
            Reply(player, "SettingChanged", label, next ? "on" : "off");
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
            public bool ForcedArrows;
        }

        private void StartRadar(BasePlayer player, RadarPreferences preferences)
        {
            StartRadar(player, preferences, true);
        }

        private void StartRadar(BasePlayer player, RadarPreferences preferences, bool notify)
        {
            string deniedFeature;
            if (!CanUseMode(player, preferences.Mode, out deniedFeature))
            {
                Reply(player, "FeaturePermission", deniedFeature);
                return;
            }

            float maximumDistance = HasPermission(player, PermExtendedRange)
                ? _config.General.MaximumExtendedDistance
                : _config.General.MaximumStandardDistance;
            if (preferences.Distance > maximumDistance) preferences.Distance = maximumDistance;
            NormalizePreferences(preferences);

            RadarSession session;
            if (!_sessions.TryGetValue(player.userID, out session))
            {
                session = new RadarSession { Viewer = player, Preferences = preferences };
                _sessions[player.userID] = session;
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
            session.ForcedArrows = false;
            if (ModeIncludesPlayers(preferences.Mode)) _nextPlayerIndexRebuild = 0f;
            if (preferences.ShowSleepers) _nextSleeperIndexRebuild = 0f;
            RefreshVoiceWatcherCount();

            if (_config.General.LogUsage)
                Puts(player.displayName + " (" + player.UserIDString + ") enabled SmartRadar in " + preferences.Mode + " mode.");

            if (notify) Reply(player, "Enabled", preferences.Mode, preferences.Distance, preferences.RefreshRate);
        }

        private void StopRadar(BasePlayer player, bool notify)
        {
            if (player == null) return;
            bool removed = _sessions.Remove(player.userID);
            if (removed) RefreshVoiceWatcherCount();
            if (removed && _config.General.LogUsage)
                Puts(player.displayName + " (" + player.UserIDString + ") disabled SmartRadar.");

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
                session.ExpiresAt > 0f ? Mathf.Max(0f, session.ExpiresAt - Time.realtimeSinceStartup).ToString("0.#", CultureInfo.InvariantCulture) + "s" : "off");
        }

        private void SchedulerTick()
        {
            if (!_serverInitialized) return;
            float now = Time.realtimeSinceStartup;

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

            foreach (KeyValuePair<ulong, RadarSession> pair in _sessions)
            {
                RadarSession session = pair.Value;
                BasePlayer viewer = session == null ? null : session.Viewer;
                if (session != null && session.ExpiresAt > 0f && now >= session.ExpiresAt)
                {
                    if (viewer != null && viewer.IsConnected) Reply(viewer, "Expired");
                    _sessionRemovalBuffer.Add(pair.Key);
                    continue;
                }
                if (viewer == null || !viewer.IsConnected || !HasPermission(viewer, PermUse))
                {
                    _sessionRemovalBuffer.Add(pair.Key);
                    continue;
                }

                string deniedFeature;
                if (!CanUseMode(viewer, session.Preferences.Mode, out deniedFeature))
                {
                    Reply(viewer, "FeaturePermission", deniedFeature);
                    _sessionRemovalBuffer.Add(pair.Key);
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

                bool playerDue = ModeIncludesPlayers(session.Preferences.Mode) && now >= session.NextPlayerUpdate;
                bool staticDue = ModeIncludesStatic(session.Preferences.Mode) && now >= session.NextStaticUpdate;
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
            }

            for (int i = 0; i < _sessionRemovalBuffer.Count; i++)
                _sessions.Remove(_sessionRemovalBuffer[i]);

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
                if (session != null && ModeIncludesPlayers(session.Preferences.Mode)) return true;
            }
            return false;
        }

        private bool HasSleeperRadarSessions()
        {
            foreach (RadarSession session in _sessions.Values)
            {
                if (session != null && ModeIncludesPlayers(session.Preferences.Mode) && session.Preferences.ShowSleepers) return true;
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
                if (player == null || !player.IsConnected) continue;
                AddToIndex(_activePlayerIndex, GetCellKey(player.transform.position), player);
            }
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
            _stashCells.Clear();
            _cupboardCells.Clear();

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
                if (cupboard != null) IndexCupboard(cupboard);
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

        private void ClearIndexes()
        {
            _activePlayerIndex.Clear();
            _sleepingPlayerIndex.Clear();
            _stashIndex.Clear();
            _cupboardIndex.Clear();
            _stashCells.Clear();
            _cupboardCells.Clear();
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
            if (!HasPermission(viewer, PermPlayers)) return 0;

            _playerCandidates.Clear();
            BasePlayer spectatingTarget = GetSpectatingTarget(viewer);
            Vector3 origin = spectatingTarget != null && spectatingTarget.IsConnected
                ? spectatingTarget.transform.position
                : viewer.transform.position;
            ulong ignoredTargetId = spectatingTarget != null ? spectatingTarget.userID : viewer.userID;
            float radiusSqr = preferences.Distance * preferences.Distance;
            int minX, maxX, minZ, maxZ;
            GetCellBounds(origin, preferences.Distance, out minX, out maxX, out minZ, out maxZ);

            CollectPlayerCandidates(_activePlayerIndex, viewer, preferences, origin, radiusSqr, false, ignoredTargetId, minX, maxX, minZ, maxZ);
            if (preferences.ShowSleepers && HasPermission(viewer, PermSleepers))
                CollectPlayerCandidates(_sleepingPlayerIndex, viewer, preferences, origin, radiusSqr, true, ignoredTargetId, minX, maxX, minZ, maxZ);

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
                viewer.SendConsoleCommand("ddraw.text", lifetime, _playerDrawColor, localLabelPosition, label,
                    _config.Display.DistanceFade, _config.Display.DepthTest, _config.Display.PlayerLabelScale, target.net.ID);
                draws++;

                if ((preferences.ShowArrows || session.ForcedArrows) && HasPermission(viewer, PermArrows) && draws < budget)
                {
                    Vector3 startWorld = target.eyes.position;
                    Vector3 endWorld = startWorld + target.eyes.HeadRay().direction * _config.Display.ArrowLength;
                    Vector3 localStart = target.transform.InverseTransformPoint(startWorld);
                    Vector3 localEnd = target.transform.InverseTransformPoint(endWorld);
                    viewer.SendConsoleCommand("ddraw.arrow", lifetime, _arrowDrawColor, localStart, localEnd,
                        _config.Display.ArrowHeadRadius, _config.Display.DistanceFade, _config.Display.DepthTest, target.net.ID);
                    draws++;
                }
            }

            return draws;
        }

        private void CollectPlayerCandidates(Dictionary<long, List<BasePlayer>> index, BasePlayer viewer, RadarPreferences preferences,
            Vector3 origin, float radiusSqr, bool sleeping, ulong ignoredTargetId, int minX, int maxX, int minZ, int maxZ)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    List<BasePlayer> players;
                    if (!index.TryGetValue(MakeCellKey(x, z), out players)) continue;
                    for (int i = 0; i < players.Count; i++)
                    {
                        BasePlayer target = players[i];
                        if (!ShouldIncludePlayer(viewer, target, preferences, sleeping, ignoredTargetId)) continue;
                        float sqrDistance = (target.transform.position - origin).sqrMagnitude;
                        if (sqrDistance > radiusSqr) continue;

                        bool vanished = IsPlayerVanished(target);
                        if (vanished && _config.Privacy.HideVanishedPlayers)
                        {
                            if (!preferences.ShowVanished || !HasPermission(viewer, PermSeeVanished)) continue;
                        }

                        _playerCandidates.Add(new PlayerCandidate
                        {
                            Player = target,
                            SqrDistance = sqrDistance,
                            Sleeping = sleeping,
                            Vanished = vanished
                        });
                    }
                }
            }
        }

        private bool ShouldIncludePlayer(BasePlayer viewer, BasePlayer target, RadarPreferences preferences, bool sleeping, ulong ignoredTargetId)
        {
            if (target == null || target.userID == viewer.userID || target.userID == ignoredTargetId) return false;
            if (!sleeping && !target.IsConnected) return false;
            if (!_config.Display.IncludeNpcPlayers && target is NPCPlayer) return false;

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
            else if (target is NPCPlayer) _labelBuilder.Append(" | <color=#AAAAAA>NPC</color>");

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

            _labelBuilder.Append("</size>");
            return _labelBuilder.ToString();
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

        private int DrawStaticRadar(RadarSession session, int budget)
        {
            int draws = 0;
            string mode = session.Preferences.Mode;
            Vector3 origin = GetRadarOrigin(session.Viewer);
            float radius = session.Preferences.Distance;
            float lifetime = Mathf.Max(session.Preferences.RefreshRate, _config.Scheduler.MinimumStaticRefresh) + _config.Scheduler.DrawingLifetimePadding;

            if ((mode == ModeStashes || mode == ModeAll) && HasPermission(session.Viewer, PermStashes) && draws < budget)
                draws += DrawStashes(session.Viewer, origin, radius, lifetime, budget - draws);

            if ((mode == ModeCupboards || mode == ModeAll) && HasPermission(session.Viewer, PermCupboards) && draws < budget)
                draws += DrawCupboards(session.Viewer, origin, radius, lifetime, budget - draws);

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

        private int DrawCupboards(BasePlayer viewer, Vector3 origin, float radius, float lifetime, int budget)
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
                string label = "<size=14><color=#05F5E5>TC</color> | <color=#2F6FFF>" +
                    Mathf.RoundToInt(Mathf.Sqrt(candidate.SqrDistance)) + "</color>M</size>";
                viewer.SendConsoleCommand("ddraw.text", lifetime, _cupboardDrawColor,
                    cupboard.transform.position + Vector3.up * _config.Display.StaticLabelHeight, label);
                draws++;
            }
            return draws;
        }

        private void CollectStaticCandidates<T>(Dictionary<long, List<T>> index, List<StaticCandidate<T>> results, Vector3 origin, float radius) where T : BaseEntity
        {
            float radiusSqr = radius * radius;
            int minX, maxX, minZ, maxZ;
            GetCellBounds(origin, radius, out minX, out maxX, out minZ, out maxZ);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    List<T> entities;
                    if (!index.TryGetValue(MakeCellKey(x, z), out entities)) continue;
                    for (int i = 0; i < entities.Count; i++)
                    {
                        T entity = entities[i];
                        if (entity == null || entity.IsDestroyed) continue;
                        float sqrDistance = (entity.transform.position - origin).sqrMagnitude;
                        if (sqrDistance > radiusSqr) continue;
                        results.Add(new StaticCandidate<T> { Entity = entity, SqrDistance = sqrDistance });
                    }
                }
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
            foreach (Connection connection in Net.sv.connections)
            {
                if (connection != null && connection.connected && connection.isAuthenticated &&
                    connection.player is BasePlayer && connection.player != player)
                    connections.Add(connection);
            }
            player.OnNetworkSubscribersLeave(connections);
            Pool.FreeUnmanaged(ref connections);

            if (ServerOcclusion.OcclusionEnabled) player.OcclusionMakeSubscribersForget();
            if (player.GetComponent<SmartVanishController>() == null)
                player.gameObject.AddComponent<SmartVanishController>();

            if (_config.Vanish.EnableNoclip && !player.IsFlying && !player.isMounted)
            {
                state.EnabledNoclip = true;
                player.SendConsoleCommand("noclip");
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

            if (_config.Vanish.LogUsage)
                Puts(player.displayName + " (" + player.UserIDString + ") entered SmartRadar vanish.");
            if (notify && _config.Vanish.EnableNotifications)
                Reply(player, "VanishEnabled", radarStarted ? "ON" : "OFF");
            if (notify && _config.Investigation.StartRadarOnVanish && !radarStarted)
                Reply(player, "VanishRadarUnavailable");
            return true;
        }

        private bool ExitVanish(BasePlayer player, bool notify, bool preservePersistedState, bool force)
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

            if (_config.Investigation.StopRadarOnReappear) StopRadar(player, false);
            if (_config.Vanish.LogUsage)
                Puts(player.displayName + " (" + player.UserIDString + ") left SmartRadar vanish.");
            if (notify && _config.Vanish.EnableNotifications) Reply(player, "VanishDisabled");
            return true;
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
            SmartVanishController controller = player.GetComponent<SmartVanishController>();
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
            if (!_config.Investigation.StartRadarOnVanish || !HasPermission(player, PermUse)) return false;

            RadarPreferences preferences = _config.Investigation.UseSavedRadarPreferences
                ? GetPreferences(player.userID)
                : CreateDefaultPreferences();
            if (!_config.Investigation.UseSavedRadarPreferences)
                preferences.Mode = _config.Investigation.RadarMode;

            string deniedFeature;
            if (!CanUseMode(player, preferences.Mode, out deniedFeature))
            {
                string fallbackMode = GetFirstPermittedMode(player);
                if (fallbackMode == null) return false;
                preferences.Mode = fallbackMode;
            }

            bool forceArrows = _config.Investigation.ForceVisionArrows && HasPermission(player, PermArrows);
            StartRadar(player, preferences, false);
            RadarSession session;
            if (_sessions.TryGetValue(player.userID, out session))
            {
                session.StartedByVanish = true;
                session.ForcedArrows = forceArrows;
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
            Subscribe(nameof(OnPlayerSpectate));
            Subscribe(nameof(OnPlayerSpectateEnd));
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
            Unsubscribe(nameof(OnPlayerSpectate));
            Unsubscribe(nameof(OnPlayerSpectateEnd));
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
            if (IsBuiltInVanished(player)) DetachVanishRuntime(player);
        }

        private void OnPlayerSpectateEnd(BasePlayer player, string spectateFilter)
        {
            if (!IsBuiltInVanished(player)) return;
            NextTick(delegate
            {
                if (player != null && player.IsConnected && player.GetComponent<SmartVanishController>() == null)
                    player.gameObject.AddComponent<SmartVanishController>();
            });
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
            if (!IsBuiltInVanished(player) || note == null || player.isMounted ||
                !HasPermission(player, PermVanishTeleport) || !player.serverInput.IsDown(BUTTON.RELOAD))
                return null;
            player.serverInput.Clear();
            Vector3 destination = new Vector3(note.worldPosition.x, player.transform.position.y, note.worldPosition.z);
            player.MovePosition(destination, false);
            player.ClientRPC(RpcTarget.Player("ForcePositionTo", player), destination);
            note.Dispose();
            return true;
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

        public sealed class SmartVanishController : FacepunchBehaviour
        {
            private BasePlayer _player;
            private Vector3 _originalScale;
            private float _nextNetworkGroupUpdate;

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                if (_player == null) return;
                _originalScale = _player.transform.localScale;
                _player.transform.localScale = Vector3.zero;
                _nextNetworkGroupUpdate = 0f;
            }

            private void FixedUpdate()
            {
                if (_player == null || Instance == null) return;
                Instance.MaintainVanishMetabolism(_player);
                if (Time.realtimeSinceStartup >= _nextNetworkGroupUpdate)
                {
                    Instance.UpdateVanishNetworkGroup(_player);
                    _nextNetworkGroupUpdate = Time.realtimeSinceStartup + 2f;
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
        }

        private bool HasExplicitPermission(BasePlayer player, string permissionName)
        {
            return player != null && permission.UserHasPermission(player.UserIDString, permissionName);
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
                default: return null;
            }
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

        private static bool ModeIncludesPlayers(string mode)
        {
            return mode == ModePlayers || mode == ModeAll;
        }

        private static bool ModeIncludesStatic(string mode)
        {
            return mode == ModeStashes || mode == ModeCupboards || mode == ModeAll;
        }

        #endregion

        #region Helpers

        private static Vector3 GetRadarOrigin(BasePlayer viewer)
        {
            BasePlayer target = GetSpectatingTarget(viewer);
            return target != null && target.IsConnected ? target.transform.position : viewer.transform.position;
        }

        private static readonly PropertyInfo SpectatingTargetProperty = typeof(BasePlayer).GetProperty("spectatingTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo SpectatingTargetField = typeof(BasePlayer).GetField("spectatingTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static BasePlayer GetSpectatingTarget(BasePlayer viewer)
        {
            if (viewer == null) return null;
            try
            {
                if (SpectatingTargetProperty != null) return SpectatingTargetProperty.GetValue(viewer, null) as BasePlayer;
                if (SpectatingTargetField != null) return SpectatingTargetField.GetValue(viewer) as BasePlayer;
            }
            catch
            {
                // Rust has changed the spectating target member between releases. Falling back to the viewer is safe.
            }
            return null;
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
                return effect == null || effect.source == 0 || Instance == null ||
                    !Instance._vanishedPlayers.Contains(effect.source);
            }
        }

        #endregion
    }
}
