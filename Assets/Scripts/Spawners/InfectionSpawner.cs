using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns agents for infection testing.
/// Randomly selects one agent as "patient zero" who starts infected.
/// </summary>
public class InfectionSpawner : MonoBehaviour
{
    [Header("Test Prefabs")]
    [SerializeField] private BaseAgent[] agentPrefabs;

    [Header("Spawn Settings")]
    [SerializeField] private int agentCount = 30;
    [SerializeField] private float spawnAreaRadius = 20f;
    [SerializeField] private float minSpawnDistance = 1.5f;
    [SerializeField] private int maxSpawnAttempts = 50;

    [Header("Disease Settings (Flu - Fast for Testing)")]
    [SerializeField] private string diseaseName = "TestFlu";
    [SerializeField] private float mortalityRate = 0.05f;       // 5% mortality
    [SerializeField] private float infectivity = 0.6f;          // 60% base infection chance
    [SerializeField] private float recoveryRate = 0.9f;         // 90% recovery rate
    [SerializeField] private float incubationPeriod = 5f;       // 5 seconds (fast for testing)
    [SerializeField] private float contagiousDuration = 15f;    // 15 seconds contagious

    private List<Vector3> spawnedPositions = new List<Vector3>();
    private List<BaseAgent> spawnedAgents = new List<BaseAgent>();

    void Start()
    {
        if (agentPrefabs == null || agentPrefabs.Length == 0)
        {
            Debug.LogError("No agent prefabs assigned to InfectionSpawner!");
            return;
        }

        // Spawn all agents
        for (int i = 0; i < agentCount; i++)
        {
            SpawnAgent();
        }

        Debug.Log($"Spawned {spawnedAgents.Count} agents for infection test");

        // Pick random patient zero after short delay (let all agents initialize)
        Invoke(nameof(InfectPatientZero), 0.5f);
    }

    private void SpawnAgent()
    {
        Vector3? spawnPos = FindValidSpawnPosition();
        if (!spawnPos.HasValue) return;

        // Pick random prefab
        BaseAgent prefab = agentPrefabs[Random.Range(0, agentPrefabs.Length)];

        // Spawn with random rotation
        Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        GameObject agentObj = Instantiate(prefab.gameObject, spawnPos.Value, randomRotation);

        // Track spawned agent
        BaseAgent agent = agentObj.GetComponent<BaseAgent>();
        if (agent != null)
        {
            spawnedAgents.Add(agent);
        }

        spawnedPositions.Add(spawnPos.Value);
    }

    private Vector3? FindValidSpawnPosition()
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnAreaRadius;
            Vector3 candidatePos = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (IsValidSpawnPosition(candidatePos))
            {
                return candidatePos;
            }
        }
        return null;
    }

    private bool IsValidSpawnPosition(Vector3 position)
    {
        foreach (Vector3 spawnedPos in spawnedPositions)
        {
            if (Vector3.Distance(position, spawnedPos) < minSpawnDistance)
            {
                return false;
            }
        }
        return true;
    }

    private void InfectPatientZero()
    {
        if (spawnedAgents.Count == 0)
        {
            Debug.LogError("No agents spawned - cannot create patient zero!");
            return;
        }

        // Pick random agent as patient zero
        int randomIndex = Random.Range(0, spawnedAgents.Count);
        BaseAgent patientZero = spawnedAgents[randomIndex];

        // Infect with test flu
        List<string> symptoms = new List<string> { "coughing", "sneezing", "fever" };

        patientZero.BecomeInfected(
            diseaseName,
            mortalityRate,
            symptoms,
            recoveryRate,
            infectivity,
            incubationPeriod,
            contagiousDuration
        );

        Debug.Log($"<color=red>===== PATIENT ZERO: {patientZero.InstanceID} =====</color>");
        Debug.Log($"Disease: {diseaseName} | Infectivity: {infectivity} | Incubation: {incubationPeriod}s | Contagious: {contagiousDuration}s");
    }

    // Visualize spawn area in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnAreaRadius);
    }
}