using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

//Monobevahior is a base unity class that lets scripts attack to gameobjects and use unity methods like Start() and update()
public class AgentActionManager : MonoBehaviour
{
    #region Controller References
    [Header("Action Controllers")]
    // Attribute. Allows variable to be visible in the inspector window to be able to drag and drop components.
    // private variable only accessible within this class. Of Type MovementController.
    [SerializeField] private MovementController movementController;
    [SerializeField] private VisionController visionController;
    [SerializeField] private HearingController hearingController;


    private BaseAgent baseAgent;

    //TO Develop
    // [SerializeField] private CommunicationController communicationController;
    // [SerializeField] private ResourceController resourceController;
    // [SerializeField] private SensorController sensorController;
    // [SerializeField] private SocialController socialController;

    // Store reference to the base agent of this game object
    #endregion Controller References

    #region Data Struct

    public struct AgentPerceptionData
    {
        //Agent Data
        public Vector3 myPosition;
        public Vector3 myForward;
        public string myType;
        public string myFaction;
        public string myInstanceID;

        //Vision Data
        public Dictionary<string, Vector3> visibleAgents;
        public int visibleCount;

        //Hearing Data
        public Dictionary<string, Vector3> heardAgents;
        public int heardCount;

        // Infection Data
        public string infectionName;
        public float mortalityRate;
        public string[] symptoms;
        public bool isContagious;
        public float recoveryRate;
        public bool isImmune;
        public float health;
        // 0=Healthy, 1=Exposed, 2=Infectious, 3=Recovered, 4=Dead
        public int infectionStage;
        public float infectivity;
    }

    #endregion Data Struct

    #region Public Interface
    // Public property to access the controllers from other scripts
    public MovementController Movement => movementController;
    public VisionController Vision => visionController;
    public HearingController Hearing => hearingController;
    // add others as needed

    #endregion Public Interface

    #region Unity Methods
    //Method called when the agent is created.
    void Awake()
    {
        // Get the BaseAgent component and set up all controllers
        baseAgent = GetComponent<BaseAgent>();

        InitializeControllers();
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Check that all required controllers are properly assigned
        ValidateControllers();

    }

    #endregion Unity Methods

    #region Initialization
    // Initialize all controllers, adding them if they are missing
    private void InitializeControllers()
    {

        // MovementController - Try to find an existing MovementController on this game object
        movementController = EnsureController<MovementController>();
        movementController.Initialize(baseAgent);

        // VisionController - Try to find an existing VisionController on this game object
        visionController = EnsureController<VisionController>();
        visionController.Initialize(baseAgent);

        // HearingController - Try to find an existing VisionController on this game object
        hearingController = EnsureController<HearingController>();
        hearingController.Initialize(baseAgent);
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

    // Log errors for any missing controllers
    private void ValidateControllers()
    {
        ValidateController(movementController, "MovementController");
        ValidateController(visionController, "VisionController");
        ValidateController(hearingController, "HearingController");
    }

    private void ValidateController(MonoBehaviour controller, string name)
    {
        if (controller == null)
            Debug.LogError($"{name} is not assigned or found on {baseAgent.AgentName}");
    }
    #endregion Initialization 

    #region Public Interface Methods
    // Wrapper methods to expose controller functionalities
    public AgentPerceptionData GetPerceptionData()
    {
        return new AgentPerceptionData
        {
            // Get vision data from VisionController
            visibleAgents = visionController.GetAgentsWithinSights(),
            visibleCount = visionController.GetAgentsWithinSights().Count,

            // Get hearing data from HearingController  
            heardAgents = hearingController.GetAgentsWithinHearing(),
            heardCount = hearingController.GetAgentsWithinHearing().Count,

            // Get self data
            myPosition = transform.position,
            myForward = transform.forward,
            myType = baseAgent.Type.ToString(),      
            myFaction = baseAgent.Faction.ToString(), 
            myInstanceID = baseAgent.InstanceID,

            // Infection data
            infectionName = baseAgent.InfectionName,
            mortalityRate = baseAgent.InfectionMortalityRate,
            symptoms = baseAgent.Symptoms.ToArray(),           // Convert List to Array
            isContagious = baseAgent.IsContagious,
            recoveryRate = baseAgent.RecoveryRate,
            isImmune = baseAgent.IsImmune,
            health = baseAgent.Health,
            infectionStage = (int)baseAgent.CurrentInfectionStage,  // Enum to int
            infectivity = baseAgent.Infectivity
        };
    }

    // Walking method
    public void MoveTo(Vector3 position) => Movement.WalkTo(position);

    // Running method
    public void RunTo(Vector3 position) => Movement.RunTo(position);

    // Stop movement method
    public void Stop() => Movement.StopMoving();

    // Get targets within sight
    public void GetAgentsWithinSights() => Vision.GetAgentsWithinSights();

    // Query methods to check agent status

    // Check if the agent is currently moving
    public bool IsMoving => Movement != null && Movement.IsMoving;

    // Get the agent's current position in the world
    public Vector3 CurrentPosition => transform.position;

    // Get the position the agent is moving toward
    public Vector3 TargetPosition => Movement != null ? Movement.TargetPosition : transform.position;

    #endregion Public Interface Methods
}
