using BoneLib.BoneMenu.Elements;

using LabFusion.Extensions;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.SDK.Points;
using LabFusion.Senders;
using LabFusion.Utilities;

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LabFusion.SDK.Gamemodes
{
    public class TeamDeathmatch : Gamemode
    {
        public static TeamDeathmatch Instance { get; private set; }

        public TeamManager TeamManager { get; private set; }

        public bool OverrideValues { get => _overrideValues; }

        // Prefix
        public const string DefaultPrefix = "FusionTDM";

        // Default metadata keys
        public const string TeamScoreKey = TeamKey + ".Score";
        public const string TeamKey = DefaultPrefix + ".Team";

        public override string GamemodeCategory => "Fusion";
        public override string GamemodeName => "Team Deathmatch";

        public override bool DisableDevTools => true;
        public override bool DisableSpawnGun => true;
        public override bool DisableManualUnragdoll => true;

        public override bool PreventNewJoins => !_enabledLateJoining;

        private const int _minPlayerBits = 30;
        private const int _maxPlayerBits = 250;

        private const int _defaultMinutes = 3;
        private const int _minMinutes = 2;
        private const int _maxMinutes = 60;

        private float _timeOfStart;
        private bool _oneMinuteLeft;

        private bool _overrideValues;

        private int _savedMinutes = _defaultMinutes;
        private int _totalMinutes = _defaultMinutes;

        private string _avatarOverride = null;
        private float? _vitalityOverride = null;

        private bool _enabledLateJoining = true;

        public override void OnBoneMenuCreated(MenuCategory category)
        {
            base.OnBoneMenuCreated(category);

            category.CreateIntElement("Round Minutes", Color.white, _totalMinutes, 1, _minMinutes, _maxMinutes, (v) =>
            {
                _totalMinutes = v;
                _savedMinutes = v;
            });
        }

        public void SetLateJoining(bool enabled)
        {
            _enabledLateJoining = enabled;
        }

        public void SetRoundLength(int minutes)
        {
            _totalMinutes = minutes;
        }

        public void SetAvatarOverride(string barcode)
        {
            _avatarOverride = barcode;

            if (IsActive())
            {
                FusionPlayer.SetAvatarOverride(barcode);
            }
        }

        public void SetPlayerVitality(float vitality)
        {
            _vitalityOverride = vitality;

            if (IsActive())
            {
                FusionPlayer.SetPlayerVitality(vitality);
            }
        }

        public float GetTimeElapsed()
        {
            return Time.realtimeSinceStartup - _timeOfStart;
        }

        public float GetMinutesLeft()
        {
            float elapsed = GetTimeElapsed();
            return _totalMinutes - (elapsed / 60f);
        }

        public override void OnGamemodeRegistered()
        {
            base.OnGamemodeRegistered();

            Instance = this;

            MultiplayerHooking.OnPlayerJoin += OnPlayerJoin;
            MultiplayerHooking.OnPlayerLeave += OnPlayerLeave;
            MultiplayerHooking.OnPlayerAction += OnPlayerAction;
            FusionOverrides.OnValidateNametag += OnValidateNametag;

            TeamManager = new TeamManager();

            SetDefaultValues();
        }

        public override void OnGamemodeUnregistered()
        {
            base.OnGamemodeUnregistered();

            if (Instance == this)
            {
                TeamManager = null;
                Instance = null;
            }

            MultiplayerHooking.OnPlayerJoin -= OnPlayerJoin;
            MultiplayerHooking.OnPlayerLeave -= OnPlayerLeave;
            MultiplayerHooking.OnPlayerAction -= OnPlayerAction;
            FusionOverrides.OnValidateNametag -= OnValidateNametag;
        }

        public override void OnMainSceneInitialized()
        {
            if (!_overrideValues)
            {
                SetDefaultValues();
            }
            else
            {
                _overrideValues = false;
            }
        }

        public override void OnLoadingBegin()
        {
            _overrideValues = false;
        }

        public void SetDefaultValues()
        {
            _totalMinutes = _savedMinutes;
            SetPlaylist(DefaultMusicVolume, FusionContentLoader.CombatPlaylist);

            TeamManager.AddDefaultTeams();

            _avatarOverride = null;
            _vitalityOverride = null;

            _enabledLateJoining = true;
        }

        public void SetOverriden()
        {
            if (FusionSceneManager.IsLoading())
            {
                if (!_overrideValues)
                {
                    SetDefaultValues();
                }

                _overrideValues = true;
            }
        }

        protected bool OnValidateNametag(PlayerId id)
        {
            if (!IsActive())
            {
                return true;
            }

            return TeamManager.GetTeamFromMember(id) == TeamManager.LocalTeam;
        }

        protected void OnPlayerAction(PlayerId player, PlayerActionType type, PlayerId otherPlayer = null)
        {
            if (IsActive() && NetworkInfo.IsServer)
            {
                if (type != PlayerActionType.DYING_BY_OTHER_PLAYER)
                {
                    return;
                }

                if(otherPlayer == null)
                {
                    return;
                }

                if(otherPlayer == player)
                {
                    return;
                }

                var killerTeam = TeamManager.GetTeamFromMember(otherPlayer);
                var killedTeam = TeamManager.GetTeamFromMember(player);

                if (killerTeam != killedTeam)
                {
                    TeamManager.IncrementScore(killerTeam);
                }
            }
        }

        protected void OnPlayerJoin(PlayerId id)
        {
            if (NetworkInfo.IsServer && IsActive())
            {
                TeamManager.AssignTeam(id);
            }
        }

        protected void OnPlayerLeave(PlayerId id)
        {
            if (TeamManager.LogoInstances.TryGetValue(id, out var instance))
            {
                instance.Cleanup();
                TeamManager.LogoInstances.Remove(id);
            }
        }

        protected override void OnStartGamemode()
        {
            base.OnStartGamemode();

            if (NetworkInfo.IsServer)
            {
                TeamManager.ResetTeams();
                TeamManager.SetTeams();
            }

            _timeOfStart = Time.realtimeSinceStartup;
            _oneMinuteLeft = false;

            // Invoke player changes on level load
            FusionSceneManager.HookOnLevelLoad(() =>
            {
                // Force mortality
                FusionPlayer.SetMortality(true);

                // Setup ammo
                FusionPlayer.SetAmmo(1000);

                // Push nametag updates
                FusionOverrides.ForceUpdateOverrides();

                // Apply vitality and avatar overrides
                if (_avatarOverride != null)
                {
                    FusionPlayer.SetAvatarOverride(_avatarOverride);
                }

                if (_vitalityOverride.HasValue)
                {
                    FusionPlayer.SetPlayerVitality(_vitalityOverride.Value);
                }
            });
        }

        protected override void OnStopGamemode()
        {
            base.OnStopGamemode();

            List<Team> leaders = TeamManager.SortTeamsByScore();

            Team winningTeam = leaders.First();
            Team secondPlaceTeam = leaders[1];

            string message = "";

            int firstTeamScore = TeamManager.GetScoreFromTeam(winningTeam);
            int secondTeamScore = TeamManager.GetScoreFromTeam(secondPlaceTeam);
            int thirdTeamScore = 0;

            message = $"First Place: {winningTeam.TeamName} (Score: {firstTeamScore}) \n";
            message += $"Second Place: {secondPlaceTeam.TeamName} (Score: {secondTeamScore}) \n";

            if (leaders.Count > 2)
            {
                Team thirdPlaceTeam = leaders[2];
                thirdTeamScore = TeamManager.GetScoreFromTeam(thirdPlaceTeam);
                message += $"Third Place: {thirdPlaceTeam.TeamName} (Score: {thirdTeamScore}) \n";
            }

            bool tied = leaders.All((team) => team.TeamScore == TeamManager.GetScoreFromTeam(winningTeam));

            if (tied)
            {
                message += "Tie! (All teams scored the same score!)";
                TeamManager.OnTeamTied();
            }
            else
            {
                message += TeamManager.GetTeamStatus(winningTeam);
            }

            // Show the winners in a notification
            FusionNotifier.Send(new FusionNotification()
            {
                title = "Team Deathmatch Completed",
                showTitleOnPopup = true,

                message = message,

                popupLength = 6f,

                isMenuItem = false,
                isPopup = true,
            });

            _timeOfStart = 0f;
            _oneMinuteLeft = false;

            // Reset mortality
            FusionPlayer.ResetMortality();

            // Remove ammo
            FusionPlayer.SetAmmo(0);

            // Remove all team logos
            TeamManager.RemoveLogos();

            // Push nametag updates
            FusionOverrides.ForceUpdateOverrides();

            // Reset overrides
            FusionPlayer.ClearAvatarOverride();
            FusionPlayer.ClearPlayerVitality();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (!IsActive() || !NetworkInfo.IsServer)
            {
                return;
            }

            UpdateLogos();

            // Get time left
            float minutesLeft = GetMinutesLeft();

            // Check for minute barrier
            if (!_oneMinuteLeft)
            {
                if (minutesLeft <= 1f)
                {
                    TryInvokeTrigger("OneMinuteLeft");
                    _oneMinuteLeft = true;
                }
            }

            // Should the gamemode end?
            if (minutesLeft <= 0f)
            {
                StopGamemode();
                TryInvokeTrigger("NaturalEnd");
            }
        }

        protected void UpdateLogos()
        {
            // Update logos
            foreach (var logo in TeamManager.LogoInstances.Values)
            {
                // Change visibility
                bool visible = logo.team == TeamManager.LocalTeam;
                if (visible != logo.IsShown())
                {
                    logo.Toggle(visible);
                }

                // Update position
                logo.Update();
            }
        }

        protected override void OnEventTriggered(string value)
        {
            FusionNotification oneMinuteNotification = new FusionNotification()
            {
                title = "Team Deathmatch Timer",
                showTitleOnPopup = true,
                message = "One minute left!",
                isMenuItem = false,
                isPopup = true,
            };

            FusionNotification bitRewardNotification = new FusionNotification()
            {
                title = "Bits Rewarded",
                showTitleOnPopup = true,
                popupLength = 3f,
                isMenuItem = false,
                isPopup = true,
            };

            if (value == "OneMinuteLeft")
            {
                FusionNotifier.Send(oneMinuteNotification);
            }

            if(value == "NaturalEnd")
            {
                int bitReward = GetRewardedBits();
                string message = bitReward == 1 ? "Bit" : "Bits";

                bitRewardNotification.message = $"You Won {bitReward}" + message;
                PointItemManager.RewardBits(bitReward);
            }
        }

        protected override void OnMetadataChanged(string key, string value)
        {
            base.OnMetadataChanged(key, value);

            bool isScoreRequest = key.StartsWith(TeamScoreKey);
            bool isTeamRequest = key.StartsWith(TeamKey);

            if (isScoreRequest)
            {
                TeamManager.OnRequestTeamPoint(key, value, int.Parse(value));
            }

            if (isTeamRequest) 
            {
                Team team = TeamManager.GetTeamFromValue(value);
                TeamManager.OnRequestTeamChanged(key, value, team);
            }
        }

        private int GetRewardedBits()
        {
            // Change the max bit count based on player count
            int playerCount = PlayerIdManager.PlayerCount - 1;

            // 10 and 100 are the min and max values for the max bit count
            float playerPercent = (float)playerCount / 4f;
            int maxBits = Mathf.FloorToInt(Mathf.Lerp(_minPlayerBits, _maxPlayerBits, playerPercent));
            int maxRand = maxBits / 10;

            // Get the scores
            int score = TeamManager.GetScoreFromTeam(TeamManager.LocalTeam);
            int totalScore = TeamManager.GetTotalScore();

            // Prevent divide by 0
            if (totalScore <= 0)
                return 0;

            float percent = Mathf.Clamp01((float)score / (float)totalScore);
            int reward = Mathf.FloorToInt((float)maxBits * percent);

            // Add randomness
            reward += UnityEngine.Random.Range(-maxRand, maxRand);

            // Make sure the reward isn't invalid
            if (reward.IsNaN())
            {
                FusionLogger.ErrorLine("Prevented attempt to give invalid bit reward. Please notify a Fusion developer and send them your log.");
                return 0;
            }

            return reward;
        }
    }
}