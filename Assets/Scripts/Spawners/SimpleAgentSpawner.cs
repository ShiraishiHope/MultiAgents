using System.Collections.Generic;
using UnityEngine;

public class SimpleAgentSpawner : MonoBehaviour
{
    #region Configuration
    [Header("Agent Settings")]
    [SerializeField] private BaseAgent[] agentPrefabs;
    [SerializeField] private int agentCount = 30;

    [Header("Spawn Area")]
    [SerializeField] private float spawnRadius = 30f;
    [SerializeField] private float minSpawnDistance = 2f;
    [SerializeField] private int maxSpawnAttempts = 50;

    private List<Vector3> spawnedPositions = new List<Vector3>();
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        if (agentPrefabs == null || agentPrefabs.Length == 0)
        {
            Debug.LogError("No agent prefabs assigned to SimpleAgentSpawner!");
            return;
        }

        for (int i = 0; i < agentCount; i++)
        {
            SpawnAgent();
        }

        Debug.Log($"Spawned {spawnedPositions.Count}/{agentCount} agents");
    }
    #endregion

    #region Spawning
    private void SpawnAgent()
    {
        Vector3? spawnPos = FindValidSpawnPosition();
        if (!spawnPos.HasValue) return;

        BaseAgent prefab = agentPrefabs[Random.Range(0, agentPrefabs.Length)];
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Instantiate(prefab, spawnPos.Value, rotation);
        spawnedPositions.Add(spawnPos.Value);
    }

    private Vector3? FindValidSpawnPosition()
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (IsValidSpawnPosition(candidate))
                return candidate;
        }
        return null;
    }

    private bool IsValidSpawnPosition(Vector3 position)
    {
        foreach (Vector3 existing in spawnedPositions)
        {
            if (Vector3.Distance(position, existing) < minSpawnDistance)
                return false;
        }
        return true;
    }
    #endregion

    #region Editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
    #endregion
}