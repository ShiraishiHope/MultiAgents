using UnityEngine;

/// <summary>
/// Extension methods for Unity GameObjects.
/// Place in any .cs file in your project.
/// </summary>
public static class GameObjectExtensions
{
    /// <summary>
    /// Gets a consistent entity ID for any GameObject.
    /// Checks for known components first, falls back to instance ID.
    /// </summary>
    public static int GetEntityId(this GameObject obj)
    {
        // Check for our registry components
        Item item = obj.GetComponent<Item>();
        if (item != null)
            return Mathf.Abs(obj.GetInstanceID());

        DepositZone deposit = obj.GetComponent<DepositZone>();
        if (deposit != null)
            return Mathf.Abs(obj.GetInstanceID());

        Obstacle obstacle = obj.GetComponent<Obstacle>();
        if (obstacle != null)
            return Mathf.Abs(obj.GetInstanceID());

        // Fallback for any object
        return Mathf.Abs(obj.GetInstanceID());
    }
}