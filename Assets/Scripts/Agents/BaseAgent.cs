using System.ComponentModel;
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System;

public class BaseAgent : MonoBehaviour
{
    #region serializedVariables
    [Header("Agent Identity")]
    [SerializeField] private string agentName = "Agent";
    [SerializeField] private string instanceID = "";
    [SerializeField] private TextMeshProUGUI nameText;
    // Character type
    [Header("Character Classification")]
    [SerializeField] private CharacterType characterType;
    [SerializeField] private FactionType faction;
    // Disease status
    [Header("Infection Parameters")]
    [SerializeField] private string infectionName = "None";
    [SerializeField][Range(0f, 1f)] private float infectionMortalityRate = 0f;
    [SerializeField] private float incubationPeriod = 20f;
    [SerializeField] private float contagiousDuration = 30f;
    [SerializeField] private List<string> symptoms = new List<string>();
    [SerializeField][Range(0f, 1f)] private float infectivity = 0.5f;
    [SerializeField] private bool isContagious = false;
    [SerializeField][Range(0f, 1f)] private float recoveryRate = 0f;
    [Header("Agent Infection Status")]
    [SerializeField] private bool isImmune = false;
    [SerializeField][Range(0f, 100f)] private float health = 100f;
    [SerializeField] private InfectionStage infectionStage = InfectionStage.Healthy;
    [Header("Hunger")]
    [SerializeField] private float hunger = 100f;
    [SerializeField] private bool isCarrying = false;
    [SerializeField] private string targetId = "0";

    #endregion serializedVariables

    #region variables
    //Static registry for all agents
    private static Dictionary<string, BaseAgent> agentRegistry = new Dictionary<string, BaseAgent>();
    // Action tracking - allows other agents to see what this agent is doing
    private string currentAction = "none";
    private float actionStartTime = -1f;
    public float Hunger => hunger;

    //public properties for controlled access
    public string AgentName => agentName;
    public string InstanceID => instanceID;
    public CharacterType Type => characterType;
    public FactionType Faction => faction;
    public void SetFaction(FactionType newFaction)
    {
        faction = newFaction;
    }
    //Enum of different possibles states for the agent
    public enum AgentState { Walking, Running, Idle,Busy, Sleeping, Dead }
    AgentState currentState = AgentState.Idle;
    public string CurrentAction => currentAction;
    public float ActionStartTime => actionStartTime;
    private Vector3 spawnPosition;
    public Vector3 SpawnPosition => spawnPosition;

    //Enum of different possibles
    public enum CharacterType { Barbarian, Knight, Mage, Rogue, Skeleton_Mage, Skeleton_Minion, Skeleton_Rogue, Skeleton_Warrior,
        Robot
    }
    public enum FactionType
    {
        Human,
        Skeleton,
        Predator,
        Prey
    }
    // Access current state
    public AgentState CurrentState => currentState;

    public string InfectionName => infectionName;
    public float InfectionMortalityRate => infectionMortalityRate;
    public List<string> Symptoms => symptoms;
    public bool IsContagious => isContagious;
    public float RecoveryRate => recoveryRate;
    public bool IsImmune => isImmune;
    public float Health => health;
    public InfectionStage CurrentInfectionStage => infectionStage;
    public float Infectivity => infectivity;
    public float IncubationPeriod => incubationPeriod;
    public float ContagiousDuration => contagiousDuration;
    public bool IsCarrying => isCarrying;
    public string TargetId => targetId;


    public enum InfectionStage
    {
        Healthy,      // No infection
        Exposed,      // Infected but not yet contagious (incubation)
        Contagious,   // Can spread to others
        Recovered,    // No longer sick, gained immunity
        Dead          // Succumbed to infection
    }

    #endregion variables

    #region agentMethods
    //Static access to registry
    public static BaseAgent GetAgentByInstanceID(string id)
    {
        agentRegistry.TryGetValue(id, out BaseAgent agent);
        return agent;
    }

    //Get all agents
    public static BaseAgent[] GetAllAgents()
    {
        BaseAgent[] agents = new BaseAgent[agentRegistry.Count];
        agentRegistry.Values.CopyTo(agents, 0);
        return agents;
    }

    //Get all agents positions
    public static Dictionary<string, Vector3> GetAllAgentsPosition()
    {
        Dictionary<string, Vector3> agentPositions = new Dictionary<string, Vector3>();
        foreach (var kvp in agentRegistry)
        {
            agentPositions[kvp.Key] = kvp.Value.transform.position;
        }
        return agentPositions;
    }

    //Get all agents by Faction
    public static BaseAgent[] GetAgentsByFaction(FactionType factionType)
    {
        List<BaseAgent> matchingAgents = new List<BaseAgent>();
        foreach (var agent in agentRegistry.Values)
        {
            if (agent.faction == factionType)
                matchingAgents.Add(agent);
        }
        return matchingAgents.ToArray();
    }

    //Get all agents types
    public static BaseAgent[] GetAgentsByType(CharacterType type)
    {
        List<BaseAgent> matchingAgents = new List<BaseAgent>();
        foreach (var agent in agentRegistry.Values)
        {
            if (agent.characterType == type)
                matchingAgents.Add(agent);
        }
        return matchingAgents.ToArray();
    }

    /// <summary>
    /// Modify health by a positive (heal) or negative (damage) amount.
    /// Triggers death if health drops to 0 or below.
    /// </summary>
    public void ModifyHealth(float amount)
    {
        if (infectionStage == InfectionStage.Dead) return;

        health += amount;

        // Clamp health between 0 and 100
        health = Mathf.Clamp(health, 0f, 100f);

        if (health <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Convenience method for damage. Calls ModifyHealth with negative value.
    /// </summary>
    public void TakeDamage(float damage)
    {
        ModifyHealth(-damage);
    }

    #region Health & Infection Modification

    /// <summary>
    /// Infect this agent with a disease. Starts in Exposed stage.
    /// Copies all disease parameters from the infecting agent.
    /// </summary>
    public void BecomeInfected(string name, float mortality, List<string> newSymptoms,
                               float recovery, float newInfectivity,
                               float incubation, float contagiousDur)
    {
        if (isImmune) return;
        if (infectionStage != InfectionStage.Healthy) return;

        // Copy disease parameters
        infectionName = name;
        infectionMortalityRate = mortality;
        symptoms = new List<string>(newSymptoms);  // Create copy to avoid reference issues
        recoveryRate = recovery;
        infectivity = newInfectivity;
        incubationPeriod = incubation;
        contagiousDuration = contagiousDur;

        // Start in Exposed stage (not yet contagious)
        isContagious = false;
        infectionStage = InfectionStage.Exposed;

        Debug.Log($"{instanceID} has been exposed to {name}!");
    }

    /// <summary>
    /// Progress from Exposed to Contagious stage.
    /// Called by InfectionSystem when incubation period ends.
    /// </summary>
    public void BecomeContagious()
    {
        if (infectionStage != InfectionStage.Exposed) return;

        infectionStage = InfectionStage.Contagious;
        isContagious = true;

        Debug.Log($"{instanceID} is now contagious with {infectionName}!");
    }

    /// <summary>
    /// Recover from infection. Grants permanent immunity.
    /// Called by InfectionSystem when contagious duration ends.
    /// </summary>
    public void Recover()
    {
        if (infectionStage != InfectionStage.Contagious) return;

        infectionStage = InfectionStage.Recovered;
        isContagious = false;
        isImmune = true;

        // Clear symptoms but keep infection name for history
        symptoms.Clear();

        Debug.Log($"{instanceID} has recovered from {infectionName} and is now immune!");
    }

    public void ModifyHunger(float amount)
    {
        if (currentState == AgentState.Dead) return;

        hunger += amount;
        hunger = Mathf.Clamp(hunger, 0f, 100f);

        if (hunger <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Called when health reaches 0.
    /// </summary>
    public void Die()
    {
        if (currentState == AgentState.Dead || infectionStage == InfectionStage.Dead) return;  // Prevent double death

        health = 0f;
        infectionStage = InfectionStage.Dead;
        isContagious = false;
        ChangeState(AgentState.Dead);

        Debug.Log($"{instanceID} has died!");
    }

    #endregion Health & Infection Modification

    #region Action Tracking

    /// <summary>
    /// Updates the current action being performed by this agent.
    /// Called by all action methods so other agents can observe behavior.
    /// </summary>
    public void SetCurrentAction(string action)
    {
        //handle dead agents
        if (currentState == AgentState.Dead) return;

        currentAction = action;
        actionStartTime = Time.time;
    }

    /// <summary>
    /// Met à jour l'état de transport de l'agent (si le robot porte un objet).
    /// </summary>
    /// <param name="carrying">True si l'agent porte un objet, false sinon.</param>
    public void SetIsCarrying(bool carrying , string newTargetId)
    {
        isCarrying = carrying;
        targetId = newTargetId;
    }

    #endregion Action Tracking

    #endregion agentMethods

    #region State Management
    //Change state of an agent
    public void ChangeState(AgentState state)
    {
        if (currentState == AgentState.Dead) return;
        currentState = state;
    }

    #endregion State Management

    #region Unity Lifecycle
    private void Awake()
    {
        GenerateInstanceID();
        // Register this agent in the static registry
        if (!agentRegistry.ContainsKey(instanceID))
        {
            agentRegistry[instanceID] = this;
            spawnPosition = transform.position;
            targetId = "0";
        }
        else
        {
            Debug.LogWarning($"Agent with instance ID {instanceID} already exists. This may indicate a naming conflict.");
        }
    }

    private void GenerateInstanceID()
    {
        instanceID = $"{characterType}_{Mathf.Abs(GetInstanceID())}";
    }


    private void Start()
    {
        SetupNameplate(); 
    }

    private void Update()
    {
        //Keep nameplate facing camera
        if (nameText != null && Camera.main != null)
        {
            nameText.transform.LookAt(Camera.main.transform);
            nameText.transform.Rotate(0, 180, 0);
        }
    }
    private void OnDestroy()
    {
        // Remove this agent from the registry when destroyed
        if (agentRegistry.ContainsKey(instanceID))
        {
            agentRegistry.Remove(instanceID);
        }
    }
    #endregion Unity Lifecycle

    #region Initialization Helpers
    private void SetupNameplate()
    {
        if(nameText == null)
            nameText = GetComponentInChildren<TextMeshProUGUI>();

        if (nameText != null) {
            nameText.text = instanceID;
            Debug.Log($"Agent nameplate set to: {nameText.text}");
            //make nameplate always face camera
            nameText.transform.LookAt(Camera.main.transform);
            nameText.transform.Rotate(0, 180, 0);
        }
        else
        {
            Debug.LogWarning($"No TextMeshProUGUI component found in children of {agentName}");
        }
    }

    // Automatically determine faction based on character type
    private void DetermineFaction()
    {
        // Skeleton types belong to Skeleton faction
        if (characterType == CharacterType.Skeleton_Mage ||
            characterType == CharacterType.Skeleton_Minion ||
            characterType == CharacterType.Skeleton_Rogue ||
            characterType == CharacterType.Skeleton_Warrior)
        {
            faction = FactionType.Skeleton;
        }
        else
        {
            faction = FactionType.Human;
        }
    }
    #endregion methods
}
