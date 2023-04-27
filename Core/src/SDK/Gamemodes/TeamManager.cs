using LabFusion.Extensions;
using LabFusion.MarrowIntegration;
using LabFusion.Representation;
using LabFusion.Utilities;

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LabFusion.SDK.Gamemodes
{
    public class TeamManager
    {
        public IReadOnlyList<Team> Teams { get => _teams.AsReadOnly(); }

        public readonly Dictionary<PlayerId, TeamLogoInstance> LogoInstances = new Dictionary<PlayerId, TeamLogoInstance>();

        private List<Team> _teams;

        private Team _lastTeam;
        private Team _localTeam;

        

        public void AddTeam(Team team)
        {
            _teams.Add(team);
        }

        public void AddLogo(PlayerId id, Team team)
        {
            var logo = new TeamLogoInstance(id, team);
            LogoInstances.Add(id, logo);
        }

        public void RemoveLogos()
        {
            foreach (var logo in LogoInstances.Values)
            {
                logo.Cleanup();
            }

            LogoInstances.Clear();
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

        public void AssignTeam(PlayerId id)
        {
            Team newTeam = _lastTeam;

            // Assign a random team
            newTeam = _teams[UnityEngine.Random.Range(0, _teams.Count)];

            // Assign it
            SetTeam(id, newTeam);

            // Save the team
            _lastTeam = newTeam;

            // Add the player to the team members list
            newTeam.AddPlayer(id);
        }

        public void SetTeams()
        {
            // Shuffle the player teams
            var players = new List<PlayerId>(PlayerIdManager.PlayerIds);
            players.Shuffle();

            // Assign every team
            foreach (var player in players)
            {
                AssignTeam(player);
            }
        }

        public void ResetTeams()
        {
            // Reset the last team
            _lastTeam = null;

            // Set every team to none
            foreach (var player in PlayerIdManager.PlayerIds)
            {
                SetTeam(player, null);
            }

            // Set every score to 0
            foreach (var team in _teams)
            {
                SetScore(team, 0);
            }
        }

        public Team GetTeam(string teamName)
        {
            foreach (Team team in _teams)
            {
                if (team.TeamName == teamName)
                {
                    return team;
                }
            }

            return null;
        }

        public void OnRequestTeamPoint(string key, string value, int score)
        {
            var ourKey = GetScoreKey(_localTeam);

            if (ourKey == key && score != 0)
            {
                FusionNotifier.Send(new FusionNotification()
                {
                    title = "Team Deathmatch Point",
                    showTitleOnPopup = true,
                    message = $"{_localTeam.TeamName}'s score is {value}!",
                    isMenuItem = false,
                    isPopup = true,
                    popupLength = 0.7f,
                });
            }
        }

        public void OnRequestTeamChanged(string key, string value, Team team)
        {
            // Find the player that changed
            foreach (var playerId in PlayerIdManager.PlayerIds)
            {
                var playerKey = GetTeamMemberKey(playerId);

                if (playerKey == key)
                {
                    // Check who this is
                    if (playerId.IsSelf)
                    {
                        OnTeamReceived(team);
                    }
                    else if (team != null)
                    {
                        AddLogo(playerId, team);
                    }

                    // Push nametag updates
                    FusionOverrides.ForceUpdateOverrides();

                    break;
                }
            }
        }

        public List<Team> SortTeamsByScore()
        {
            return _teams.OrderBy(team => GetScoreFromTeam(team)).Reverse().ToList();
        }

        public void SetScore(Team team, int score)
        {
            TrySetMetadata(GetScoreKey(team), score.ToString());
        }

        public int GetScoreFromTeam(Team team)
        {
            TryGetMetadata(GetScoreKey(team), out string teamKey);
            int score = int.Parse(teamKey);

            return score;
        }

        public int GetTotalScore()
        {
            int accumulatedScore = 0;

            for (int i = 0; i < _teams.Count; i++)
            {
                accumulatedScore = accumulatedScore + _teams[i].TeamScore;
            }

            return accumulatedScore;
        }

        protected void IncrementScore(Team team)
        {
            var currentScore = GetScoreFromTeam(team);
            SetScore(team, currentScore + 1);
        }

        private void InitializeTeamSpawns(Team team)
        {
            // Get all spawn points
            List<Transform> transforms = new List<Transform>();

            if (team.TeamName == "Sabrelake")
            {
                foreach (var point in SabrelakeSpawnpoint.Cache.Components)
                {
                    transforms.Add(point.transform);
                }
            }
            else if (team.TeamName == "Lava Gang")
            {
                foreach (var point in LavaGangSpawnpoint.Cache.Components)
                {
                    transforms.Add(point.transform);
                }
            }
            else
            {
                // Likely a custom event for a team
                foreach (var point in TeamSpawnpoint.Cache.Components)
                {
                    if (team.TeamName != point.TeamName)
                    {
                        continue;
                    }

                    transforms.Add(point.transform);
                }
            }

            FusionPlayer.SetSpawnPoints(transforms.ToArray());

            // Teleport to a random spawn point
            if (FusionPlayer.TryGetSpawnPoint(out var spawn))
            {
                FusionPlayer.Teleport(spawn.position, spawn.forward);
            }
        }

        protected string GetTeamStatus(Team winner)
        {
            if (_localTeam == winner)
            {
                OnTeamVictory(_localTeam);
                return "You Won!";
            }
            else
            {
                OnTeamLost(_localTeam);
                return "You Lost...";
            }
        }

        protected void OnTeamReceived(Team team)
        {
            if (team == null)
            {
                _localTeam = null;
                return;
            }

            FusionNotification assignmentNotification = new FusionNotification()
            {
                title = "Team Deathmatch Assignment",
                showTitleOnPopup = true,
                message = $"Your team is: {team.TeamName}",
                isMenuItem = false,
                isPopup = true,
                popupLength = 5f,
            };

            FusionNotifier.Send(assignmentNotification);

            _localTeam = team;

            // Invoke ult events
            if (team.TeamName == "Sabrelake")
            {
                foreach (var ultEvent in InvokeUltEventIfTeamSabrelake.Cache.Components)
                {
                    ultEvent.Invoke();
                }
            }
            else if (team.TeamName == "Lava Gang")
            {
                foreach (var ultEvent in InvokeUltEventIfTeamLavaGang.Cache.Components)
                {
                    ultEvent.Invoke();
                }
            }
            else
            {
                // Likely a custom event for a team
                foreach (var holder in InvokeUltEventIfTeam.Cache.Components)
                {
                    if (team.TeamName != holder.TeamName)
                    {
                        continue;
                    }

                    holder.Invoke();
                }
            }

            // Invoke spawn point changes on level load
            FusionSceneManager.HookOnLevelLoad(() => InitializeTeamSpawns(team));
        }

        protected void OnTeamVictory(Team team)
        {
            AudioClip randomChoice = UnityEngine.Random.Range(0, 4) % 2 == 0 ? FusionContentLoader.LavaGangVictory : FusionContentLoader.SabrelakeVictory;

            AudioClip winMusic = team.WinMusic != null ? team.WinMusic : randomChoice;
            FusionAudio.Play2D(winMusic, TeamDeathmatch.DefaultMusicVolume);
        }

        protected void OnTeamLost(Team team)
        {
            AudioClip randomChoice = UnityEngine.Random.Range(0, 4) % 2 == 0 ? FusionContentLoader.LavaGangFailure : FusionContentLoader.SabrelakeFailure;

            AudioClip lossMusic = team.LossMusic != null ? team.LossMusic : randomChoice;
            FusionAudio.Play2D(lossMusic, TeamDeathmatch.DefaultMusicVolume);
        }

        protected void OnTeamTied()
        {
            FusionAudio.Play2D(FusionContentLoader.DMTie, TeamDeathmatch.DefaultMusicVolume);
        }

        public void SetTeam(PlayerId id, Team team)
        {
            if (team == null)
            {
                return;
            }

            TrySetMetadata(GetTeamMemberKey(id), team.TeamName);
        }

        public Team GetTeamFromValue(string nameValue)
        {
            foreach (Team team in _teams)
            {
                if (team.TeamName == nameValue)
                {
                    return team;
                }
            }

            return null;
        }

        public Team GetTeamFromMember(PlayerId id)
        {
            TryGetMetadata(GetTeamMemberKey(id), out string teamName);

            foreach (Team team in _teams)
            {
                if (team.TeamName == teamName)
                {
                    return team;
                }
            }

            return null;
        }

        protected string GetScoreKey(Team team)
        {
            return $"{TeamDeathmatch.TeamScoreKey}.{team?.TeamName}";
        }

        protected string GetTeamMemberKey(PlayerId id)
        {
            return $"{TeamDeathmatch.TeamKey}.{id.LongId}";
        }
    }
}
