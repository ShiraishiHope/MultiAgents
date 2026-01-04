using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a pickable item that robots can carry and deliver.
/// Uses a static registry pattern for efficient batch lookups.
/// 
/// Setup:
/// - Attach to any GameObject that robots should pick up
/// - Tag the GameObject as "Item" (for the pickup detection)
/// </summary>
public class Item : MonoBehaviour
{
    #region Static Registry

    // Central registry of all items in the scene
    private static Dictionary<string, Item> itemRegistry = new Dictionary<string, Item>();

    /// <summary>
    /// Retrieves an item by its unique ID.
    /// Returns null if not found.
    /// </summary>
    public static Item GetItemByID(string id)
    {
        itemRegistry.TryGetValue(id, out Item item);
        return item;
    }

    /// <summary>
    /// Returns an array of all registered items.
    /// Used by batch perception building.
    /// </summary>
    public static Item[] GetAllItems()
    {
        Item[] items = new Item[itemRegistry.Count];
        itemRegistry.Values.CopyTo(items, 0);
        return items;
    }

    /// <summary>
    /// Returns count of registered items.
    /// Useful for debugging/UI.
    /// </summary>
    public static int GetItemCount() => itemRegistry.Count;

    #endregion

    #region Instance Data

    [Header("Item Settings")]
    [SerializeField] private string itemType = "Generic";  // Optional: categorize items

    private string instanceID;
    private bool isBeingCarried = false;

    // Public accessors
    public string InstanceID => instanceID;
    public string ItemType => itemType;
    public bool IsBeingCarried => isBeingCarried;
    public Vector3 Position => transform.position;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        GenerateInstanceID();
        RegisterSelf();
    }

    void OnDestroy()
    {
        UnregisterSelf();
    }

    #endregion

    #region Registration

    private void GenerateInstanceID()
    {
        // Unique ID using Unity's instance ID (guaranteed unique per session)
        instanceID = $"Item_{Mathf.Abs(GetInstanceID())}";
    }

    private void RegisterSelf()
    {
        if (!itemRegistry.ContainsKey(instanceID))
        {
            itemRegistry[instanceID] = this;
            Debug.Log($"[Item] Registered: {instanceID}");
        }
        else
        {
            Debug.LogWarning($"[Item] Duplicate ID detected: {instanceID}");
        }
    }

    private void UnregisterSelf()
    {
        if (itemRegistry.ContainsKey(instanceID))
        {
            itemRegistry.Remove(instanceID);
            Debug.Log($"[Item] Unregistered: {instanceID}");
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Called when a robot picks up this item.
    /// Updates internal state and optionally removes from registry.
    /// </summary>
    public void OnPickedUp()
    {
        isBeingCarried = true;
        // Item stays in registry but marked as carried
        // This allows other robots to see it's unavailable
    }

    /// <summary>
    /// Called when a robot drops off this item.
    /// </summary>
    public void OnDroppedOff()
    {
        isBeingCarried = false;
    }

    /// <summary>
    /// Marks this item as delivered and removes it from the game.
    /// Call this when item reaches final destination.
    /// </summary>
    public void CompleteDelivery()
    {
        Debug.Log($"[Item] {instanceID} delivered successfully!");
        Destroy(gameObject);
    }

    #endregion

    #region Editor Visualization

    void OnDrawGizmos()
    {
        // Draw a small cube to visualize item in editor
        Gizmos.color = isBeingCarried ? Color.yellow : Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }

    #endregion
}