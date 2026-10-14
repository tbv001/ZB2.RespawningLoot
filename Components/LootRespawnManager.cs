using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RespawningLoot.Components;

public class LootRespawnManager : MonoBehaviour
{
    private static readonly RoomLootID[] RoomLootIds = (RoomLootID[])System.Enum.GetValues(typeof(RoomLootID));
    private readonly List<LootClusterGroup> _groups = [];
    private float _respawnTimer;
    private bool _ready;

    private void Update()
    {
        if (MultiplayerController.instance != null && !MultiplayerController.instance.IsServer())
            return;

        var generated = Map.Generated;
        switch (generated)
        {
            case true when !_ready:
                Collect();
                break;

            case false when _ready:
                _groups.Clear();
                _respawnTimer = 0f;
                _ready = false;
                break;
        }

        if (!generated || !_ready)
            return;

        _respawnTimer += Time.deltaTime;
        if (_respawnTimer >= RespawningLoot.RespawnInterval.Value)
        {
            _respawnTimer = 0f;
            Respawn();
        }
    }

    private void Collect()
    {
        if (MultiplayerController.instance != null && !MultiplayerController.instance.IsServer())
            return;

        _groups.Clear();

        var seen = new HashSet<LootClusterGroup>();
        var hash = MapHash.instance;
        if (hash == null || !hash.hashGenerated)
            return;

        for (var x = 0; x < hash.width; x++)
        {
            for (var y = 0; y < hash.height; y++)
            {
                var cell = hash.GetCell(x, y);
                if (cell == null)
                    continue;

                // Collect all loot cluster groups in buildings
                if (cell.polygons != null)
                {
                    foreach (var poly in cell.polygons)
                    {
                        var group = poly?.building?.lootClusterGroup;
                        if (group != null && seen.Add(group))
                        {
                            _groups.Add(group);
                        }
                    }
                }

                if (cell.edges == null)
                    continue;

                // Collect all loot cluster groups outside buildings
                foreach (var edge in cell.edges)
                {
                    var group = edge?.lootClusterGroup;
                    if (group != null && seen.Add(group))
                    {
                        _groups.Add(group);
                    }
                }
            }
        }

        _ready = true;
        RespawningLoot.Logger?.LogInfo($"Cached {_groups.Count} LootClusterGroups!");
    }

    private void Respawn()
    {
        if (MultiplayerController.instance != null && !MultiplayerController.instance.IsServer())
            return;

        var loot = LootController.Instance;
        var hash = MapHash.instance;
        if (loot == null || hash == null || !hash.hashGenerated)
            return;

        // Collect all loot in cluster so we can distinguish dropped loot and spawned loot from map generation
        var clusterLoot = new HashSet<DroppedLoot>();
        foreach (var group in _groups)
        {
            var spawnedLootInCluster = Traverse.Create(group).Field("spawnedLoot").GetValue<List<DroppedLoot>>();
            foreach (var spawned in spawnedLootInCluster)
            {
                if (spawned != null)
                {
                    clusterLoot.Add(spawned);
                }
            }
        }

        // Remove cluster loot
        var lootTraverse = Traverse.Create(loot);
        for (var x = 0; x < hash.width; x++)
        {
            for (var y = 0; y < hash.height; y++)
            {
                var cell = hash.GetCell(x, y);
                if (cell?.loot == null)
                    continue;

                for (var i = cell.loot.Count - 1; i >= 0; i--)
                {
                    var drop = cell.loot[i];
                    if (drop == null || drop.IsSack || drop.reservedPlayerID != -1 || !clusterLoot.Contains(drop))
                        continue;

                    lootTraverse.Method("RemoveLoot", cell, i).GetValue();
                }
            }
        }

        // Randomize loot type
        if (RespawningLoot.RandomizeLootTypes.Value)
        {
            foreach (var group in _groups)
            {
                foreach (var cluster in group.Clusters)
                {
                    Traverse.Create(cluster).Property("ClusterType")
                        .SetValue(RoomLootIds[Random.Range(0, RoomLootIds.Length)]);
                }
            }
        }

        // Spawn the loot cluster
        for (var x = 0; x < hash.width; x++)
        {
            for (var y = 0; y < hash.height; y++)
            {
                var cell = hash.GetCell(x, y);
                if (cell == null)
                    continue;

                loot.GenerateAllLootForCell(cell);
            }
        }

        RespawningLoot.Logger?.LogInfo("Respawned loot!");
    }
}
