using System.Collections.Generic;
using UnityEngine;

public class HearingController : MonoBehaviour
{
    #region References
    private BaseAgent baseAgent;
    #endregion

    #region Hearing Parameters
    [SerializeField] private float hearingDistance = 6f;
    #endregion

    #region Data Structures

    /// <summary>
    /// Data about a heard agent.
    /// Includes position and distance.
    /// </summary>
    public struct HeardAgentData
    {
        public Vector3 position;
        public float distance;
    }

    #endregion

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

    #region Hearing Methods

    /// <summary>
    /// Returns all agents within hearing range with position and distance.
    /// </summary>
    public Dictionary<string, HeardAgentData> GetHeardAgentsData()
    {
        Dictionary<string, HeardAgentData> heardAgents = new Dictionary<string, HeardAgentData>();
        BaseAgent[] allAgents = BaseAgent.GetAllAgents();

        foreach (BaseAgent agent in allAgents)
        {
            // Skip self
            if (agent.InstanceID == baseAgent.InstanceID)
                continue;

            Vector3 targetPosition = agent.transform.position;
            float distance = Vector3.Distance(transform.position, targetPosition);

            // Check if within hearing range
            if (distance <= hearingDistance)
            {
                HeardAgentData data = new HeardAgentData
                {
                    position = targetPosition,
                    distance = distance
                };

                heardAgents.Add(agent.InstanceID, data);
            }
        }

        return heardAgents;
    }

    /// <summary>
    /// Legacy method - returns just positions for backward compatibility.
    /// </summary>
    public Dictionary<string, Vector3> GetAgentsWithinHearing()
    {
        Dictionary<string, Vector3> heardAgents = new Dictionary<string, Vector3>();
        var fullData = GetHeardAgentsData();

        foreach (var kvp in fullData)
        {
            heardAgents.Add(kvp.Key, kvp.Value.position);
        }

        return heardAgents;
    }
    #endregion
}