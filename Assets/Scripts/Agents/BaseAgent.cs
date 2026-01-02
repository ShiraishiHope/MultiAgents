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
    // Character status
    [Header("Infection Status")]
    [SerializeField] private string infectionName = "None";
    [SerializeField][Range(0f, 1f)] private float infectionMortalityRate = 0f;
    [SerializeField] private List<string> symptoms = new List<string>();
    [SerializeField] private bool isContagious = false;
    [SerializeField][Range(0f, 1f)] private float recoveryRate = 0f;
    [SerializeField] private bool isImmune = false;
    [SerializeField][Range(0f, 100f)] private float health = 100f;
    [SerializeField] private InfectionStage infectionStage = InfectionStage.Healthy;
    [SerializeField][Range(0f, 1f)] private float infectivity = 0.5f;

    #endregion serializedVariables

    #region variables
    //Static registry for all agents
    private static Dictionary<string, BaseAgent> agentRegistry = new Dictionary<string, BaseAgent>();
    private Vector3 spawnPosition;
    //public properties for controlled access
    public string AgentName => agentName;
    public string InstanceID => instanceID;
    public Vector3 SpawnPosition => spawnPosition;
    public CharacterType Type => characterType;
    public FactionType Faction => faction;
    //Enum of different possibles states for the agent
    public enum AgentState { Walking, Running, Idle, Sleeping, Dead }
    AgentState currentState = AgentState.Idle;


    //Enum of different possibles
    public enum CharacterType { Barbarian, Knight, Mage, Rogue, Skeleton_Mage, Skeleton_Minion, Skeleton_Rogue, Skeleton_Warrior , Robot }
    public enum FactionType
    {
        Human,
        Skeleton,
        None
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
    #endregion agentMethods

    #region State Management
    //Change state of an agent
    public void ChangeState(AgentState state)
    {
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
            Debug.LogError($"No TextMeshProUGUI component found in children of {agentName}");
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
