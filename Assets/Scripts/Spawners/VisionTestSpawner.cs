using UnityEngine;

public class VisionTestSpawner : MonoBehaviour
{
    [Header("Test Prefabs")]
    [SerializeField] private BaseAgent magePrefab;
    [SerializeField] private BaseAgent minionPrefab;

    [Header("Test Settings")]
    [SerializeField] private float testDistance = 10f; // Distance of test agents from center

    void Start()
    {
        // Spawn central mage facing forward (0° rotation)
        Vector3 centerPos = Vector3.zero;
        Instantiate(magePrefab, centerPos, Quaternion.identity);

        // Test angles - these should test the 45° boundary of your 90° FOV
        float[] testAngles = { 45.1f, 45.0f, 44.9f, -44.9f, -45.0f, -45.1f };

        foreach (float angle in testAngles)
        {
            // Calculate position at the given angle
            float radians = angle * Mathf.Deg2Rad;
            Vector3 spawnPos = new Vector3(
                Mathf.Sin(radians) * testDistance,
                0f,
                Mathf.Cos(radians) * testDistance
            );

            // Spawn minion
            GameObject minion = Instantiate(minionPrefab.gameObject, spawnPos, Quaternion.identity);
            minion.name = $"TestMinion_{angle}°";

            Debug.Log($"Spawned minion at angle {angle}° - Position: {spawnPos}");
        }

        // Test distance minions at 0° angle
        float[] testDistances = { 5f, 10f, 15f };

        foreach (float distance in testDistances)
        {
            // Spawn directly in front (0° angle)
            Vector3 spawnPos = new Vector3(0f, 0f, distance);

            GameObject minion = Instantiate(minionPrefab.gameObject, spawnPos, Quaternion.identity);
            minion.name = $"TestMinion_0°_{distance}m";

            Debug.Log($"Spawned minion at 0° angle, distance {distance}m - Position: {spawnPos}");
        }
    }
}