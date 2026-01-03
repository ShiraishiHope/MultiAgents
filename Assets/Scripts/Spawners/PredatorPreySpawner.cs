using System.Collections.Generic;
using UnityEngine;

public class PredatorPreySpawner : MonoBehaviour
{
    #region Variables

    [Header("Population Control")]
    [SerializeField] private int totalAgents = 50;

    [Range(0f, 1f)]
    [SerializeField] private float predatorRatio = 0.3f; // 30% predators, 70% prey

    [Header("Predator Settings")]
    [SerializeField] private BaseAgent predatorPrefab;

    [Header("Prey Settings")]
    [SerializeField] private BaseAgent preyPrefab;

    [Header("Spawn Area")]
    [SerializeField] private float spawnRadius = 60f;
    [SerializeField] private float minSpawnDistance = 3f;
    [SerializeField] private int maxSpawnAttempts = 100;

    private List<Vector3> spawnedPositions = new List<Vector3>();

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        int predatorCount = Mathf.RoundToInt(totalAgents * predatorRatio);
        int preyCount = totalAgents - predatorCount;

        for (int i = 0; i < predatorCount; i++)
        {
            SpawnAgent(predatorPrefab, BaseAgent.FactionType.Predator);
        }

        for (int i = 0; i < preyCount; i++)
        {
            SpawnAgent(preyPrefab, BaseAgent.FactionType.Prey);
        }
    }

    #endregion

    #region Spawning Logic

    private void SpawnAgent(BaseAgent prefab, BaseAgent.FactionType faction)
    {
        if (prefab == null) return;

        Vector3? spawnPos = FindValidSpawnPosition();
        if (!spawnPos.HasValue) return;

        BaseAgent agent = Instantiate(
            prefab,
            spawnPos.Value,
            RandomRotation()
        );

        agent.SetFaction(faction);

        spawnedPositions.Add(spawnPos.Value);
    }

    private Vector3? FindValidSpawnPosition()
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 candidate = GenerateRandomPosition();

            if (IsValidSpawnPosition(candidate))
                return candidate;
        }

        Debug.LogWarning("Failed to find valid spawn position.");
        return null;
    }

    private Vector3 GenerateRandomPosition()
    {
        Vector2 circle = Random.insideUnitCircle * spawnRadius;
        return new Vector3(circle.x, 0f, circle.y);
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

    private Quaternion RandomRotation()
    {
        return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    #endregion
}
