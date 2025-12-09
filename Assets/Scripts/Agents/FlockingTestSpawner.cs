using UnityEngine;

public class FlockingTestSpawner : MonoBehaviour
{
    [Header("Test Prefabs")]
    [SerializeField] private BaseAgent[] agentPrefabs; // Drag multiple agent types here

    [Header("Spawn Settings")]
    [SerializeField] private int agentCount = 100;
    [SerializeField] private float spawnAreaRadius = 25f; // Agents spawn within this radius
    [SerializeField] private float minSpawnDistance = 1.5f; // Minimum distance between agents
    [SerializeField] private int maxSpawnAttempts = 50; // Attempts per agent

    private System.Collections.Generic.List<Vector3> spawnedPositions = new System.Collections.Generic.List<Vector3>();

    void Start()
    {
        if (agentPrefabs == null || agentPrefabs.Length == 0)
        {
            Debug.LogError("No agent prefabs assigned to FlockingTestSpawner!");
            return;
        }

        Debug.Log($"Spawning {agentCount} agents in a {spawnAreaRadius * 2}m x {spawnAreaRadius * 2}m area");

        int successfulSpawns = 0;

        for (int i = 0; i < agentCount; i++)
        {
            Vector3? spawnPos = FindValidSpawnPosition();

            if (spawnPos.HasValue)
            {
                // Pick random agent type
                BaseAgent prefab = agentPrefabs[Random.Range(0, agentPrefabs.Length)];

                // Spawn with random rotation
                Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                GameObject agent = Instantiate(prefab.gameObject, spawnPos.Value, randomRotation);
                agent.name = $"FlockTest_{prefab.name}_{i}";

                spawnedPositions.Add(spawnPos.Value);
                successfulSpawns++;
            }
            else
            {
                Debug.LogWarning($"Could not find valid spawn position for agent {i}");
            }
        }

        Debug.Log($"Successfully spawned {successfulSpawns}/{agentCount} agents");
        Debug.Log($"Average distance between agents: ~{CalculateAverageDensity():F2}m");
    }

    private Vector3? FindValidSpawnPosition()
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Generate random position within spawn area (circle)
            Vector2 randomCircle = Random.insideUnitCircle * spawnAreaRadius;
            Vector3 candidatePos = new Vector3(randomCircle.x, 0f, randomCircle.y);

            // Check if position is valid (not too close to others)
            if (IsValidSpawnPosition(candidatePos))
            {
                return candidatePos;
            }
        }

        return null; // Failed to find valid position
    }

    private bool IsValidSpawnPosition(Vector3 position)
    {
        // Check against all previously spawned positions
        foreach (Vector3 spawnedPos in spawnedPositions)
        {
            if (Vector3.Distance(position, spawnedPos) < minSpawnDistance)
            {
                return false; // Too close to another agent
            }
        }
        return true; // Position is valid
    }

    private float CalculateAverageDensity()
    {
        // Calculate approximate average distance based on spawn area and count
        float area = Mathf.PI * spawnAreaRadius * spawnAreaRadius;
        float areaPerAgent = area / agentCount;
        return Mathf.Sqrt(areaPerAgent);
    }

    // Visualize spawn area in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Vector3.zero, spawnAreaRadius);

        Gizmos.color = Color.green;
        foreach (Vector3 pos in spawnedPositions)
        {
            Gizmos.DrawWireSphere(pos, minSpawnDistance / 2f);
        }
    }
}