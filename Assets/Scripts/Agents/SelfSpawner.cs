using UnityEngine;

public class ShelfSpawner : MonoBehaviour
{
    public GameObject itemPrefab;
    public float spawnInterval = 20f; // Temps de base (20s)
    public float timeVariation = 5f;  // Variation de +/- 5s pour le réalisme
    public Vector3 offset = new (0, 0.2f, 0);

    private float nextSpawnTime;

    void Start()
    {
        // On définit le premier moment d'apparition au hasard
        SetNextSpawnTime();
    }

    void Update()
    {
        // Si le temps actuel dépasse le moment prévu pour le spawn
        if (Time.time >= nextSpawnTime)
        {
            if (!IsOccupied())
            {
                SpawnItem();
            }
            // On calcule le prochain moment d'apparition
            SetNextSpawnTime();
        }
    }

    void SetNextSpawnTime()
    {
        // Calcule un intervalle entre 15s (20-5) et 25s (20+5)
        float randomDelay = Random.Range(spawnInterval - timeVariation, spawnInterval + timeVariation);
        nextSpawnTime = Time.time + randomDelay;
    }

    void SpawnItem()
    {
        Instantiate(itemPrefab, transform.position + offset, Quaternion.identity);
    }

    bool IsOccupied()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position + offset, 1f);
        foreach (var col in colliders)
        {
            if (col.CompareTag("Item")) return true;
        }
        return false;
    }
}