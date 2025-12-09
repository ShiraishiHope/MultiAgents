using System.Collections.Generic;
using UnityEngine;

public class AgentSpawner : MonoBehaviour
{
    #region variables
    [Header("Population Control")]
    [SerializeField] private int agentNumber = 50;
    [Range(0f, 1f)][SerializeField] private float humanRatio = 0.5f; // 50% humans, 50% skeletons

    [Header("Human Characters")]
    [SerializeField] private FactionSpawnSettings humanFaction;

    [Header("Skeleton Characters")]
    [SerializeField] private FactionSpawnSettings skeletonFaction;

    [Header("Spawn Distance")]
    [SerializeField] private float spawnDistance = 3f; // Distance between agents

    [Header("Spawn Settings")]
    [SerializeField] private int maxSpawnAttempts = 100;

    // Local list to track spawned positions
    private List<Vector3> spawnedPositions = new List<Vector3>();

    #endregion variables

    #region methods

    [System.Serializable]
    public class CharacterSpawnData
    {
        public BaseAgent prefab;
        [Range(0f, 1f)] public float spawnWeight = 0.25f; // Equal distribution by default
    }

    [System.Serializable]
    public class FactionSpawnSettings
    {
        [Header("Faction Characters")]
        public CharacterSpawnData[] characters;
    }
    private Vector3 GenerateCenterBiasedPosition()
    {
        //Generate position biased toward center
        Vector2 randomCircle = Random.insideUnitCircle * 60f;

        //Convert to 3D position
        Vector3 spawnPosition = new Vector3(randomCircle.x, 0f, randomCircle.y);

        return spawnPosition;
    }

    private bool IsValidSpawnPosition(Vector3 position)
    {
        // Check against all previously spawned positions
        foreach (Vector3 spawnedPos in spawnedPositions)
        {
            if (Vector3.Distance(position, spawnedPos) < spawnDistance)
            {
                return false;
            }
        }
        return true;
    }

    private Vector3? FindValidSpawnPosition()
    {
        for (int attempts = 0; attempts < maxSpawnAttempts; attempts++)
        {
            Vector3 candidatePosition = GenerateCenterBiasedPosition();

            if (IsValidSpawnPosition(candidatePosition))
            {
                return candidatePosition;
            }
        }

        // Failed to find valid position after max attempts
        Debug.LogWarning($"Could not find valid spawn position after {maxSpawnAttempts} attempts");
        return null;
    }

    private Quaternion RandomRotation()
    {
        return Quaternion.Euler(0, Random.Range(0f, 360f), 0);
    }

    void Start()
    {
        // Calculate exact counts
        int humanCount = Mathf.RoundToInt(agentNumber * humanRatio);
        int skeletonCount = agentNumber - humanCount;

        // Spawn exact number of humans
        for (int i = 0; i < humanCount; i++)
        {
            BaseAgent selectedPrefab = SelectWeightedCharacter(humanFaction.characters);
            SpawnAgent(selectedPrefab);
        }

        // Spawn exact number of skeletons
        for (int i = 0; i < skeletonCount; i++)
        {
            BaseAgent selectedPrefab = SelectWeightedCharacter(skeletonFaction.characters);
            SpawnAgent(selectedPrefab);
        }
    }

    private void SpawnAgent(BaseAgent prefab)
    {
        if (prefab == null) return;

        Vector3? spawnPos = FindValidSpawnPosition();
        if (spawnPos.HasValue)
        {
            Instantiate(prefab, spawnPos.Value, RandomRotation());
            spawnedPositions.Add(spawnPos.Value);
        }
    }

    private BaseAgent SelectWeightedCharacter(CharacterSpawnData[] characters)
    {
        // Handle empty array
        if (characters.Length == 0)
        {
            Debug.LogWarning("No characters available for selection!");
            return null;
        }

        // Calculate total weight
        float totalWeight = 0f;
        foreach (CharacterSpawnData character in characters)
        {
            totalWeight += character.spawnWeight;
        }

        // Handle zero weights
        if (totalWeight <= 0f)
        {
            Debug.LogWarning("All character weights are zero! Using first character.");
            return characters[0].prefab;
        }

        // Generate random value within total weight
        float randomValue = Random.Range(0f, totalWeight);

        // Find selected character using cumulative weights
        float cumulativeWeight = 0f;
        foreach (CharacterSpawnData character in characters)
        {
            cumulativeWeight += character.spawnWeight;
            if (randomValue <= cumulativeWeight)
            {
                return character.prefab;
            }
        }

        // Fallback (shouldn't reach here, but safety net)
        return characters[characters.Length - 1].prefab;
    }
    #endregion methods
}


