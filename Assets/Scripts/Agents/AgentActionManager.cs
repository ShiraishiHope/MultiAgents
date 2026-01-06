using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

public class AgentActionManager : MonoBehaviour
{
    #region Controller References
    [Header("Action Controllers")]
    [SerializeField] private MovementController movementController;
    [SerializeField] private VisionController visionController;
    [SerializeField] private HearingController hearingController;
    [SerializeField] private ActionController actionController;

    private BaseAgent baseAgent;
    #endregion

    #region Data Structs

    /// <summary>
    /// Data about a visible agent - sent to Python.
    /// </summary>
    public struct VisibleAgentInfo
    {
        public Vector3 position;
        public float distance;
        public string currentAction;
        public float actionStartTime;
        public string faction;
        public string state;
    }

    // Data about visible food.
    public struct VisibleFoodInfo
    {
        public Vector3 position;
        public float distance;
    }

    /// <summary>
    /// Data about a heard agent - sent to Python.
    /// </summary>
    public struct HeardAgentInfo
    {
        public Vector3 position;
        public float distance;
    }

    /// <summary>
    /// Complete perception data package sent to Python each decision cycle.
    /// </summary>
    public struct AgentPerceptionData
    {
        // Agent Identity
        public Vector3 myPosition;
        public Vector3 myForward;
        public string myType;
        public string myFaction;
        public string myInstanceID;
        public string myState;
        public Vector3 mySpawn;

        // Vision Data - now with complete info
        public Dictionary<string, VisibleAgentInfo> visibleAgents;
        public int visibleCount;
        public Dictionary<string, VisibleFoodInfo> visibleFood;
        public int visibleFoodCount;

        // Hearing Data - now with distance
        public Dictionary<string, HeardAgentInfo> heardAgents;
        public int heardCount;

        // Infection Data
        public string infectionName;
        public float mortalityRate;
        public string[] symptoms;
        public bool isContagious;
        public float recoveryRate;
        public bool isImmune;
        public float health;
        public int infectionStage;
        public float infectivity;

        // Disease Timing (new)
        public float incubationPeriod;
        public float contagiousDuration;

        //Hunger
        public float hunger;

        //Robot
        public bool isCarrying;
        public string targetId;

    }

    #endregion

    #region Public Interface
    public MovementController Movement => movementController;
    public VisionController Vision => visionController;
    public HearingController Hearing => hearingController;
    public ActionController Action => actionController;
    #endregion

    #region Unity Methods
    void Awake()
    {
        baseAgent = GetComponent<BaseAgent>();
        InitializeControllers();
    }

    void Start()
    {
        ValidateControllers();
    }
    #endregion

    #region Initialization
    private void InitializeControllers()
    {
        movementController = EnsureController<MovementController>();
        movementController.Initialize(baseAgent);

        visionController = EnsureController<VisionController>();
        visionController.Initialize(baseAgent);

        hearingController = EnsureController<HearingController>();
        hearingController.Initialize(baseAgent);

        actionController = EnsureController<ActionController>();
        actionController.Initialize(baseAgent);
    }

    private T EnsureController<T>() where T : MonoBehaviour
    {
        T controller = GetComponent<T>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<T>();
            Debug.Log($"Added {typeof(T).Name} to {baseAgent.AgentName}");
        }
        return controller;
    }

    private void ValidateControllers()
    {
        ValidateController(movementController, "MovementController");
        ValidateController(visionController, "VisionController");
        ValidateController(hearingController, "HearingController");
        ValidateController(actionController, "ActionController");
    }

    private void ValidateController(MonoBehaviour controller, string name)
    {
        if (controller == null)
            Debug.LogError($"{name} is not assigned or found on {baseAgent.AgentName}");
    }
    #endregion

    #region Public Interface Methods

    /// <summary>
    /// Builds complete perception data for Python.
    /// Called each decision cycle by PythonBehaviorController.
    /// </summary>
    public AgentPerceptionData GetPerceptionData()
    {
        // Get vision data with full details
        var visionData = visionController.GetVisibleAgentsData();
        Dictionary<string, VisibleAgentInfo> visibleInfo = new Dictionary<string, VisibleAgentInfo>();

        foreach (var kvp in visionData)
        {
            visibleInfo[kvp.Key] = new VisibleAgentInfo
            {
                position = kvp.Value.position,
                distance = kvp.Value.distance,
                currentAction = kvp.Value.currentAction,
                actionStartTime = kvp.Value.actionStartTime,
                faction = kvp.Value.faction,
                state = kvp.Value.state,
            };
        }

        // Get hearing data with distance
        var hearingData = hearingController.GetHeardAgentsData();
        Dictionary<string, HeardAgentInfo> heardInfo = new Dictionary<string, HeardAgentInfo>();

        foreach (var kvp in hearingData)
        {
            heardInfo[kvp.Key] = new HeardAgentInfo
            {
                position = kvp.Value.position,
                distance = kvp.Value.distance
            };
        }

        var foodData = visionController.GetFoodWithinSights();
        Dictionary<string, VisibleFoodInfo> foodInfo = new Dictionary<string, VisibleFoodInfo>();

        foreach (var kvp in foodData)
        {
            foodInfo[kvp.Key] = new VisibleFoodInfo
            {
                position = kvp.Value.position,
                distance = kvp.Value.distance
            };
        }

        return new AgentPerceptionData
        {
            // Identity
            myPosition = transform.position,
            myForward = transform.forward,
            myType = baseAgent.Type.ToString(),
            myFaction = baseAgent.Faction.ToString(),
            myInstanceID = baseAgent.InstanceID,
            myState = baseAgent.CurrentState.ToString(),
            mySpawn = baseAgent.SpawnPosition,

            // Vision - complete data
            visibleAgents = visibleInfo,
            visibleCount = visibleInfo.Count,

            // Hearing - with distance
            heardAgents = heardInfo,
            heardCount = heardInfo.Count,

            // Infection
            infectionName = baseAgent.InfectionName,
            mortalityRate = baseAgent.InfectionMortalityRate,
            symptoms = baseAgent.Symptoms.ToArray(),
            isContagious = baseAgent.IsContagious,
            recoveryRate = baseAgent.RecoveryRate,
            isImmune = baseAgent.IsImmune,
            health = baseAgent.Health,
            infectionStage = (int)baseAgent.CurrentInfectionStage,
            infectivity = baseAgent.Infectivity,
            isCarrying = baseAgent.IsCarrying,
            targetId = baseAgent.TargetId,
            // Disease Timing
            incubationPeriod = baseAgent.IncubationPeriod,
            contagiousDuration = baseAgent.ContagiousDuration,

            //Hunger data
            hunger = baseAgent.Hunger,

            // Food data
            visibleFood = foodInfo,
            visibleFoodCount = foodInfo.Count
        };
    }

    // ===== Movement Wrappers =====

    public void MoveTo(Vector3 position) => Movement.WalkTo(position);
    public void RunTo(Vector3 position) => Movement.RunTo(position);
    public void Stop() => Movement.StopMoving();
    public bool Quarantine() => Movement.Quarantine();
    public void Avoid(string targetID) => Movement.Avoid(targetID);
    public void Avoid(string targetID1, string targetID2) => Movement.Avoid(targetID1, targetID2);

    // ===== Combat Wrappers =====

    public ActionController.ActionResult Attack(string targetID) => Action.Attack(targetID);
    public ActionController.ActionResult Claw(string targetID) => Action.Claw(targetID);
    public ActionController.ActionResult Bite(string targetID) => Action.Bite(targetID);
    public ActionController.ActionResult Sneeze() => Action.Sneeze();
    public ActionController.ActionResult Cough() => Action.Cough();

    // ===== Predator Kill Action =====
    public ActionController.ActionResult Kill(string targetID) => Action.Kill(targetID);

    // ===== Health Wrapper =====

    public void ModifyHealth(float amount) => Action.ModifyHealth(amount);

    // ===== Action Wrapper =====

    public ActionController.ActionResult Eat(string foodID) => Action.Eat(foodID);
    
    // ===== Robot Actions Wrappers =====

    public ActionController.ActionResult PickUp(string itemID) => Action.PickUp(itemID);

    public ActionController.ActionResult DropOff() => Action.DropOff();

    // ===== Query Properties =====

    public bool IsMoving => Movement != null && Movement.IsMoving;
    public bool IsQuarantined => Movement != null && Movement.IsQuarantined;
    public Vector3 CurrentPosition => transform.position;
    public Vector3 TargetPosition => Movement != null ? Movement.TargetPosition : transform.position;

    #endregion
}