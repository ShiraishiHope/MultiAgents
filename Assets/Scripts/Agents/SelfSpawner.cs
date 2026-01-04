using UnityEngine;

public class ShelfSpawner : MonoBehaviour
{
    public GameObject itemPrefab;
    public float spawnInterval = 20f;
    public float timeVariation = 5f;
    public Vector3 offset = new(0, 0.2f, 0);

    private float nextSpawnTime = -1f; // -1 indique qu'aucun chrono n'est en cours
    private GameObject currentItem;     // Référence à l'item actuellement sur l'étagère

    void Update()
    {
        // 1. Vérifier si l'étagère est devenue vide
        if (currentItem == null || currentItem.transform.parent != null)
        {
            // Si l'étagère vient de se vider et qu'aucun chrono n'est lancé
            if (nextSpawnTime < 0)
            {
                SetNextSpawnTime();
            }
        }

        // 2. Si un chrono est en cours et que le temps est écoulé
        if (nextSpawnTime > 0 && Time.time >= nextSpawnTime)
        {
            SpawnItem();
            nextSpawnTime = -1f; // On stoppe le chrono car l'item est présent
        }
    }

    void SetNextSpawnTime()
    {
        float randomDelay = Random.Range(spawnInterval - timeVariation, spawnInterval + timeVariation);
        nextSpawnTime = Time.time + randomDelay;
    }

    void SpawnItem()
    {
        currentItem = Instantiate(itemPrefab, transform.position + offset, Quaternion.identity);
        currentItem.tag = "Item"; // Requis pour la détection de proximité du robot

        // S'assure que le script Item est présent pour le registre Python
        if (currentItem.GetComponent<Item>() == null)
        {
            currentItem.AddComponent<Item>();
        }

    }
}