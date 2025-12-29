using System.Collections.Generic;
using TMPro;
using UnityEngine;

//Monobevahior is a base unity class that lets scripts attack to gameobjects and use unity methods like Start() and update()
public class VisionController : MonoBehaviour
{
    #region References
    // References to other components this controller needs
    private BaseAgent baseAgent;
    #endregion

    #region Vision Parameters
    
    [SerializeField] private float sightDistance = 11f;
    [SerializeField] private float sightAngle = 90f;

    // Current movement state
    private Vector3 targetPosition;
    #endregion

    #region Data Structures

    // Complete data about a visible agent.
    public struct VisibleAgentData
    {
        public Vector3 position;
        public float distance;
        public string currentAction;
        public float actionStartTime;
        public string faction;
        public string state;
    }

    #endregion Data Structures

    #region Initialization

    void Start()
    {
        baseAgent = GetComponent<BaseAgent>();
    }

    public void Initialize(BaseAgent agent)
    {
        baseAgent = agent;
    }
    #endregion

    #region Vision Methods

    /// <summary>
    /// Returns all agents within sight cone with complete data.
    /// Includes position, distance, current action, and action start time.
    /// </summary>
    public Dictionary<string, VisibleAgentData> GetVisibleAgentsData()
    {
        Dictionary<string, VisibleAgentData> visibleAgents = new Dictionary<string, VisibleAgentData>();
        BaseAgent[] allAgents = BaseAgent.GetAllAgents();

        foreach (BaseAgent agent in allAgents)
        {
            // Skip self
            if (agent.InstanceID == baseAgent.InstanceID)
                continue;

            // Calculate distance
            Vector3 targetPosition = agent.transform.position;
            float distance = Vector3.Distance(transform.position, targetPosition);

            // Check if within sight distance
            if (distance <= sightDistance)
            {
                // Calculate angle to target
                Vector3 targetDir = targetPosition - transform.position;
                float angle = Vector3.Angle(targetDir, transform.forward);

                // Check if within field of view
                if (angle <= sightAngle / 2f)
                {
                    // Build complete data struct
                    VisibleAgentData data = new VisibleAgentData
                    {
                        position = targetPosition,
                        distance = distance,
                        currentAction = agent.CurrentAction,
                        actionStartTime = agent.ActionStartTime,
                        faction = agent.Faction.ToString(),
                        state = agent.CurrentState.ToString()
                    };

                    visibleAgents.Add(agent.InstanceID, data);
                }
            }
        }

        return visibleAgents;
    }

    /// <summary>
    /// Legacy method - returns just positions for backward compatibility.
    /// Consider using GetVisibleAgentsData() for complete information.
    /// </summary>
    public Dictionary<string, Vector3> GetAgentsWithinSights()
    {
        Dictionary<string, Vector3> visibleAgents = new Dictionary<string, Vector3>();
        var fullData = GetVisibleAgentsData();

        foreach (var kvp in fullData)
        {
            visibleAgents.Add(kvp.Key, kvp.Value.position);
        }

        return visibleAgents;
    }

    /// <summary>
    /// Legacy method - calls GetAgentsWithinSights().
    /// </summary>
    public Dictionary<string, Vector3> GetAgentsWithinRange()
    {
        return GetAgentsWithinSights();
    }
    #endregion
    #region Vision Commands


    #endregion
}
