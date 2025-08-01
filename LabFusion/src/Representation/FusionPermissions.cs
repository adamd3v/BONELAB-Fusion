﻿using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Player;
using UnityEngine;

namespace LabFusion.Representation;

public enum PermissionLevel : sbyte
{
    /// <summary>
    /// Someone with less permissions than the normal user.
    /// </summary>
    GUEST = -1,

    /// <summary>
    /// The default permission level for a user.
    /// </summary>
    DEFAULT = 0,

    /// <summary>
    /// Permissions of a moderator or operator on the server.
    /// </summary>
    OPERATOR = 1,

    /// <summary>
    /// Permissions of an owner on the server.
    /// </summary>
    OWNER = 2,
}

public static class FusionPermissions
{
    public static void OnInitializeMelon()
    {
        LocalPlayer.Metadata.PermissionLevel.SetValue(PermissionLevel.DEFAULT.ToString());

        LocalPlayer.OnApplyInitialMetadata += OnUpdateInitialMetadata;
    }

    private static void OnUpdateInitialMetadata()
    {
        var permissionLevel = NetworkInfo.IsHost ? PermissionLevel.OWNER.ToString() : PermissionLevel.DEFAULT.ToString();
        LocalPlayer.Metadata.PermissionLevel.SetValue(permissionLevel);
    }

    public static void FetchPermissionLevel(ulong longId, out PermissionLevel level, out Color color)
    {
        level = PermissionLevel.DEFAULT;
        color = Color.white;

        // Get server level permissions
        if (NetworkInfo.IsHost)
        {
            if (longId == PlayerIDManager.LocalPlatformID)
            {
                level = PermissionLevel.OWNER;
            }
            else
            {
                foreach (var tuple in PermissionList.PermittedUsers)
                {
                    if (tuple.Item1 == longId)
                    {
                        level = tuple.Item3;
                    }
                }
            }
        }
        // Get client side permissions
        else
        {
            var id = PlayerIDManager.GetPlayerID(longId);

            if (id == null)
            {
                return;
            }

            var rawLevel = id.Metadata.PermissionLevel.GetValue();

            Enum.TryParse(rawLevel, out level);
        }
    }

    public static void TrySetPermission(ulong longId, string username, PermissionLevel level)
    {
        // Set in file
        PermissionList.SetPermission(longId, username, level);

        // Set in server
        var playerId = PlayerIDManager.GetPlayerID(longId);

        if (playerId != null && NetworkInfo.IsHost)
        {
            playerId.Metadata.PermissionLevel.SetValue(level.ToString());
        }
    }

    public static bool HasSufficientPermissions(PermissionLevel level, PermissionLevel requirement)
    {
        return level >= requirement;
    }

    public static bool HasHigherPermissions(PermissionLevel level, PermissionLevel requirement)
    {
        return level > requirement;
    }
}