using UnityEngine;

using LabFusion.SDK.Gamemodes;
using LabFusion.Utilities;

using System.Collections.Generic;
using System.Linq;
using LabFusion.Representation;
using LabFusion.Network;
using LabFusion.Senders;

namespace LabFusion.Core.Gamemodes
{
    public class CaptureTheFlag : Gamemode
    {
        public static CaptureTheFlag Instance { get; private set; }

        // Team related
        public IReadOnlyList<Team> Teams { get => _teams.AsReadOnly(); }

        // Gamemode data
        public override string GamemodeCategory => "Fusion";
        public override string GamemodeName => "Capture The Flag";

        // Prefixes for messages
        public const string DefaultPrefix = "FusionCTF";
        public const string TeamKey = DefaultPrefix + ".Team";
        public const string TeamScoreKey = TeamKey + ".Score";
        public const string TeamFlagKey = TeamKey + ".Flag";

        // Options
        public override bool DisableDevTools => true;
        public override bool DisableSpawnGun => true;
        public override bool DisableManualUnragdoll => true;
        public override bool PreventNewJoins => false;

        private List<Team> _teams;
        private Team _lastTeam;
        private Team _localTeam;

        public void AddTeam(Team team)
        {
            _teams.Add(team);
        }

        public void AddDefaultTeams()
        {
            Team sabrelake = new Team("Sabrelake", Color.yellow);
            Team lavaGang = new Team("Lava Gang", Color.magenta);

            sabrelake.SetMusic(FusionContentLoader.SabrelakeVictory, FusionContentLoader.SabrelakeFailure);
            lavaGang.SetMusic(FusionContentLoader.LavaGangVictory, FusionContentLoader.LavaGangFailure);

            sabrelake.SetLogo(FusionContentLoader.SabrelakeLogo);
            lavaGang.SetLogo(FusionContentLoader.LavaGangLogo);

            if (!_teams.Exists((team) => team.TeamName == sabrelake.TeamName))
            {
                AddTeam(sabrelake);
            }
            else if (!_teams.Exists((team) => team.TeamName == lavaGang.TeamName))
            {
                AddTeam(lavaGang);
            }
        }

        public override void OnGamemodeRegistered()
        {
            base.OnGamemodeRegistered();

            Instance = this;

            MultiplayerHooking.OnPlayerJoin     += OnPlayerJoin;
            MultiplayerHooking.OnPlayerLeave    += OnPlayerLeave;
            MultiplayerHooking.OnPlayerAction   += OnPlayerAction;
            FusionOverrides.OnValidateNametag   += OnValidateNametag;

            _teams = new List<Team>();
        }

        public override void OnGamemodeUnregistered()
        {
            base.OnGamemodeUnregistered();

            if(Instance == this)
            {
                Instance = null;
            }

            MultiplayerHooking.OnPlayerJoin     -= OnPlayerJoin;
            MultiplayerHooking.OnPlayerLeave    -= OnPlayerLeave;
            MultiplayerHooking.OnPlayerAction   -= OnPlayerAction;
            FusionOverrides.OnValidateNametag   -= OnValidateNametag;
        }

        protected override void OnStartGamemode()
        {
            base.OnStartGamemode();
        }

        protected override void OnStopGamemode()
        {
            base.OnStopGamemode();
        }

        protected void OnPlayerJoin(PlayerId id)
        {

        }

        protected void OnPlayerLeave(PlayerId id)
        {

        }

        protected void OnPlayerAction(PlayerId player, PlayerActionType type, PlayerId otherPlayer = null)
        {

        }

        protected bool OnValidateNametag(PlayerId id)
        {
            return false;
        }

        protected override void OnMetadataChanged(string key, string value)
        {
            base.OnMetadataChanged(key, value);

            bool isScoreRequest = key.StartsWith(TeamScoreKey);
            bool isTeamRequest = key.StartsWith(TeamKey);
            bool isFlagRequest = key.StartsWith(TeamFlagKey);
        }

        protected void SetTeam(Team team)
        {
            if(team == null)
            {
                return;
            }

            _localTeam = team;
            TrySetMetadata(TeamKey, team.TeamName);
        }

        protected string GetTeamMemberKey(PlayerId id)
        {
            return $"{TeamKey}.{id.LongId}";
        }
    }
}
