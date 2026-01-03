using System.Collections.Generic;
using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    #region Variables
    [Header("Food Settings")]
    [SerializeField] private FoodPlate foodPrefab;
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private int maxFood = 20;

    [Header("Spawn Area")]
    [SerializeField] private float spawnRadius = 40f;
    [SerializeField] private float minSpawnDistance = 2f;
    [SerializeField] private int maxSpawnAttempts = 50;

    private List<Vector3> spawnedPositions = new List<Vector3>();
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        if (foodPrefab == null)
        {
            Debug.LogError("No food prefab assigned to FoodSpawner!");
            return;
        }

        InvokeRepeating(nameof(TrySpawnFood), 0f, spawnInterval);
    }
    #endregion

    #region Spawning
    private void TrySpawnFood()
    {
        FoodPlate[] allFood = FoodPlate.GetAllFood();
        if (allFood.Length >= maxFood)
            return;

        Vector3? spawnPos = FindValidSpawnPosition();
        if (spawnPos.HasValue)
        {
            Instantiate(foodPrefab, spawnPos.Value, RandomRotation());
            spawnedPositions.Add(spawnPos.Value);
        }
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

    private Quaternion RandomRotation()
    {
        return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }
    #endregion

    #region Editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
    #endregion
}