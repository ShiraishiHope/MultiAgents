using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a static obstacle that robots should avoid.
/// Uses a static registry pattern for efficient batch lookups.
/// 
/// Setup:
/// - Attach to any GameObject robots should avoid
/// - Tag the GameObject as "Obstacle"
/// - Set the avoidance radius based on object size
/// </summary>
public class Obstacle : MonoBehaviour
{
    #region Static Registry

    private static Dictionary<string, Obstacle> obstacleRegistry = new Dictionary<string, Obstacle>();

    /// <summary>
    /// Retrieves an obstacle by its unique ID.
    /// </summary>
    public static Obstacle GetObstacleByID(string id)
    {
        obstacleRegistry.TryGetValue(id, out Obstacle obstacle);
        return obstacle;
    }

    /// <summary>
    /// Returns an array of all registered obstacles.
    /// </summary>
    public static Obstacle[] GetAllObstacles()
    {
        Obstacle[] obstacles = new Obstacle[obstacleRegistry.Count];
        obstacleRegistry.Values.CopyTo(obstacles, 0);
        return obstacles;
    }

    /// <summary>
    /// Returns count of registered obstacles.
    /// </summary>
    public static int GetObstacleCount() => obstacleRegistry.Count;

    #endregion

    #region Instance Data

    [Header("Obstacle Settings")]
    [SerializeField] private float avoidanceRadius = 1.5f;  // How far robots should stay away
    [SerializeField] private bool isDynamic = false;        // Can this obstacle move?

    private string instanceID;

    // Public accessors
    public string InstanceID => instanceID;
    public float AvoidanceRadius => avoidanceRadius;
    public bool IsDynamic => isDynamic;
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
        instanceID = $"Obstacle_{Mathf.Abs(GetInstanceID())}";
    }

    private void RegisterSelf()
    {
        if (!obstacleRegistry.ContainsKey(instanceID))
        {
            obstacleRegistry[instanceID] = this;
            Debug.Log($"[Obstacle] Registered: {instanceID}");
        }
        else
        {
            Debug.LogWarning($"[Obstacle] Duplicate ID detected: {instanceID}");
        }
    }

    private void UnregisterSelf()
    {
        if (obstacleRegistry.ContainsKey(instanceID))
        {
            obstacleRegistry.Remove(instanceID);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Checks if a position is too close to this obstacle.
    /// </summary>
    public bool IsPositionBlocked(Vector3 position)
    {
        float distance = Vector3.Distance(transform.position, position);
        return distance < avoidanceRadius;
    }

    /// <summary>
    /// Returns the closest point on the obstacle's avoidance boundary.
    /// Useful for calculating avoidance vectors.
    /// </summary>
    public Vector3 GetClosestBoundaryPoint(Vector3 fromPosition)
    {
        Vector3 direction = (fromPosition - transform.position).normalized;
        return transform.position + direction * avoidanceRadius;
    }

    /// <summary>
    /// Calculates a repulsion vector for steering away from this obstacle.
    /// Strength increases as robot gets closer.
    /// </summary>
    public Vector3 GetRepulsionVector(Vector3 fromPosition, float maxForce = 3f)
    {
        Vector3 toRobot = fromPosition - transform.position;
        float distance = toRobot.magnitude;

        if (distance >= avoidanceRadius || distance < 0.01f)
            return Vector3.zero;

        // Stronger repulsion when closer
        float strength = (avoidanceRadius - distance) / avoidanceRadius;
        return toRobot.normalized * strength * maxForce;
    }

    #endregion

    #region Editor Visualization

    void OnDrawGizmos()
    {
        // Draw avoidance radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, avoidanceRadius);

        // Draw center marker
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.2f);
    }

    void OnDrawGizmosSelected()
    {
        // When selected, show solid sphere
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        Gizmos.DrawSphere(transform.position, avoidanceRadius);
    }

    #endregion
}