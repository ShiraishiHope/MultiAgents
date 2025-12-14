using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized system that manages disease progression for all agents.
/// Handles: Exposed → Contagious → Recovered transitions based on timing.
/// 
/// Runs as a single manager object, not per-agent, for efficiency.
/// </summary>
public class InfectionSystem : MonoBehaviour
{
    #region Singleton
    // Simple singleton pattern - only one infection system needed
    public static InfectionSystem Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    #region Tracking Data

    /// <summary>
    /// Tracks when each agent entered their current infection stage.
    /// Key: Agent InstanceID, Value: Time.time when stage started
    /// </summary>
    private Dictionary<string, float> stageStartTimes = new Dictionary<string, float>();

    /// <summary>
    /// Agents we're currently tracking (in Exposed or Contagious stage).
    /// Updated each frame to catch newly infected agents.
    /// </summary>
    private HashSet<string> trackedAgents = new HashSet<string>();

    #endregion

    #region Unity Lifecycle

    void Update()
    {
        // Get all agents and check their infection status
        BaseAgent[] allAgents = BaseAgent.GetAllAgents();

        foreach (BaseAgent agent in allAgents)
        {
            ProcessAgentInfection(agent);
        }
    }
    #endregion

    #region Infection Processing

    /// <summary>
    /// Process a single agent's infection state.
    /// Handles stage transitions based on elapsed time.
    /// </summary>
    private void ProcessAgentInfection(BaseAgent agent)
    {
        string agentID = agent.InstanceID;
        BaseAgent.InfectionStage stage = agent.CurrentInfectionStage;

        switch (stage)
        {
            case BaseAgent.InfectionStage.Healthy:
            case BaseAgent.InfectionStage.Recovered:
            case BaseAgent.InfectionStage.Dead:
                // Not actively infected - remove from tracking if present
                StopTracking(agentID);
                break;

            case BaseAgent.InfectionStage.Exposed:
                ProcessExposedStage(agent);
                break;

            case BaseAgent.InfectionStage.Contagious:
                ProcessContagiousStage(agent);
                break;
        }
    }

    /// <summary>
    /// Handle agent in Exposed stage.
    /// Transitions to Contagious when incubation period ends.
    /// </summary>
    private void ProcessExposedStage(BaseAgent agent)
    {
        string agentID = agent.InstanceID;

        // Start tracking if not already
        if (!trackedAgents.Contains(agentID))
        {
            StartTracking(agentID);
            Debug.Log($"[InfectionSystem] Now tracking {agentID} in Exposed stage");
        }

        // Check if incubation period has elapsed
        float timeInStage = Time.time - stageStartTimes[agentID];

        if (timeInStage >= agent.IncubationPeriod)
        {
            // Transition to Contagious
            agent.BecomeContagious();

            // Reset timer for contagious stage
            stageStartTimes[agentID] = Time.time;

            Debug.Log($"[InfectionSystem] {agentID} progressed to Contagious after {timeInStage:F1}s");
        }
    }

    /// <summary>
    /// Handle agent in Contagious stage.
    /// Transitions to Recovered when contagious duration ends.
    /// (Death only happens when HP reaches 0, handled elsewhere)
    /// </summary>
    private void ProcessContagiousStage(BaseAgent agent)
    {
        string agentID = agent.InstanceID;

        // Ensure we're tracking (might have just become contagious)
        if (!trackedAgents.Contains(agentID))
        {
            StartTracking(agentID);
        }

        // Check if contagious duration has elapsed
        float timeInStage = Time.time - stageStartTimes[agentID];

        if (timeInStage >= agent.ContagiousDuration)
        {
            // Automatic recovery
            agent.Recover();

            // Stop tracking - agent is now immune
            StopTracking(agentID);

            Debug.Log($"[InfectionSystem] {agentID} recovered after {timeInStage:F1}s contagious");
        }
    }

    /// <summary>
    /// Start tracking an agent's infection timer.
    /// </summary>
    private void StartTracking(string agentID)
    {
        trackedAgents.Add(agentID);
        stageStartTimes[agentID] = Time.time;
    }

    /// <summary>
    /// Stop tracking an agent (healthy, recovered, or dead).
    /// </summary>
    private void StopTracking(string agentID)
    {
        trackedAgents.Remove(agentID);
        stageStartTimes.Remove(agentID);
    }
    #endregion

    #region Debug Info

    /// <summary>
    /// Returns count of currently tracked (infected) agents.
    /// Useful for debugging/UI.
    /// </summary>
    public int GetTrackedCount()
    {
        return trackedAgents.Count;
    }

    /// <summary>
    /// Returns time remaining in current stage for an agent.
    /// Returns -1 if agent is not being tracked.
    /// </summary>
    public float GetTimeRemainingInStage(string agentID)
    {
        if (!trackedAgents.Contains(agentID)) return -1f;
        if (!stageStartTimes.ContainsKey(agentID)) return -1f;

        BaseAgent agent = BaseAgent.GetAgentByInstanceID(agentID);
        if (agent == null) return -1f;

        float timeInStage = Time.time - stageStartTimes[agentID];

        switch (agent.CurrentInfectionStage)
        {
            case BaseAgent.InfectionStage.Exposed:
                return Mathf.Max(0f, agent.IncubationPeriod - timeInStage);

            case BaseAgent.InfectionStage.Contagious:
                return Mathf.Max(0f, agent.ContagiousDuration - timeInStage);

            default:
                return -1f;
        }
    }
    #endregion
}