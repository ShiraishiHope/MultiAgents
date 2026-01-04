using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a deposit/delivery zone where robots drop off items.
/// Uses a static registry pattern for efficient batch lookups.
/// 
/// Setup:
/// - Attach to a GameObject marking the delivery area
/// - Tag the GameObject as "Deposite" (matching robot.py spelling)
/// - Optionally add a trigger collider for automatic detection
/// </summary>
public class DepositZone : MonoBehaviour
{
    #region Static Registry

    private static Dictionary<string, DepositZone> depositRegistry = new Dictionary<string, DepositZone>();

    /// <summary>
    /// Retrieves a deposit zone by its unique ID.
    /// </summary>
    public static DepositZone GetDepositByID(string id)
    {
        depositRegistry.TryGetValue(id, out DepositZone deposit);
        return deposit;
    }

    /// <summary>
    /// Returns an array of all registered deposit zones.
    /// </summary>
    public static DepositZone[] GetAllDeposits()
    {
        DepositZone[] deposits = new DepositZone[depositRegistry.Count];
        depositRegistry.Values.CopyTo(deposits, 0);
        return deposits;
    }

    /// <summary>
    /// Returns count of registered deposit zones.
    /// </summary>
    public static int GetDepositCount() => depositRegistry.Count;

    #endregion

    #region Instance Data

    [Header("Deposit Zone Settings")]
    [SerializeField] private float dropOffRadius = 2f;      // How close robot must be to drop off
    [SerializeField] private int capacity = -1;             // Max items (-1 = unlimited)
    [SerializeField] private string zoneType = "Default";   // Optional: categorize zones

    private string instanceID;
    private int itemsDeposited = 0;

    // Public accessors
    public string InstanceID => instanceID;
    public string ZoneType => zoneType;
    public float DropOffRadius => dropOffRadius;
    public int ItemsDeposited => itemsDeposited;
    public Vector3 Position => transform.position;

    /// <summary>
    /// Returns true if this zone can accept more items.
    /// </summary>
    public bool CanAcceptItems => capacity < 0 || itemsDeposited < capacity;

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
        instanceID = $"Deposit_{Mathf.Abs(GetInstanceID())}";
    }

    private void RegisterSelf()
    {
        if (!depositRegistry.ContainsKey(instanceID))
        {
            depositRegistry[instanceID] = this;
            Debug.Log($"[DepositZone] Registered: {instanceID}");
        }
        else
        {
            Debug.LogWarning($"[DepositZone] Duplicate ID detected: {instanceID}");
        }
    }

    private void UnregisterSelf()
    {
        if (depositRegistry.ContainsKey(instanceID))
        {
            depositRegistry.Remove(instanceID);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Called when a robot deposits an item here.
    /// Returns true if deposit was accepted.
    /// </summary>
    public bool AcceptDeposit(Item item)
    {
        if (!CanAcceptItems)
        {
            Debug.LogWarning($"[DepositZone] {instanceID} is at capacity!");
            return false;
        }

        itemsDeposited++;
        Debug.Log($"[DepositZone] {instanceID} received item. Total: {itemsDeposited}");
        return true;
    }

    /// <summary>
    /// Checks if a position is within drop-off range of this zone.
    /// </summary>
    public bool IsInDropOffRange(Vector3 position)
    {
        float distance = Vector3.Distance(transform.position, position);
        return distance <= dropOffRadius;
    }

    /// <summary>
    /// Resets the deposit counter (useful for new rounds/levels).
    /// </summary>
    public void ResetCounter()
    {
        itemsDeposited = 0;
    }

    #endregion

    #region Editor Visualization

    void OnDrawGizmos()
    {
        // Draw the drop-off radius
        Gizmos.color = CanAcceptItems ? Color.blue : Color.red;
        Gizmos.DrawWireSphere(transform.position, dropOffRadius);

        // Draw a marker at center
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);
    }

    void OnDrawGizmosSelected()
    {
        // When selected, show solid sphere
        Gizmos.color = new Color(0, 0, 1, 0.2f);
        Gizmos.DrawSphere(transform.position, dropOffRadius);
    }

    #endregion
}