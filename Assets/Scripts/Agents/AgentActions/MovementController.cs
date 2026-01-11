using UnityEngine;

//Monobevahior is a base unity class that lets scripts attack to gameobjects and use unity methods like Start() and update()
public class MovementController : MonoBehaviour
{
    #region References
    // References to other components this controller needs
    private BaseAgent baseAgent;
    private CharacterController characterController;
    #endregion

    #region Movement Parameters
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 3f;      // Normal walking speed
    [SerializeField] private float runSpeed = 5f;       // Running/sprinting speed
    [SerializeField] private float breakSpeed = 2f;
    [SerializeField] private float stoppingDistance = 0.1f;  // How close to target before stopping

    // Avoid action fixed distance
    private const float AVOID_DISTANCE = 8f;

    // Current movement state
    private Vector3 targetPosition;
    private float currentSpeed;
    private bool isMoving = false;

    // Quarantine state
    private bool isQuarantined = false;
    #endregion

    #region Public Properties
    // Allow other scripts to check movement status
    public bool IsMoving => isMoving;
    public Vector3 TargetPosition => targetPosition;
    public bool IsQuarantined => isQuarantined;
    #endregion

    #region Initialization
    public void Initialize(BaseAgent agent)
    {
        // Store references passed from the action manager
        baseAgent = agent;

        //get Character Controller
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError($"No CharacterController on {agent.AgentName}! Add one to the prefab.");
        }

        // Set initial target to current position (not moving)
        targetPosition = transform.position;
    }
    #endregion Initialization

    #region Unity Lifecycle
    void Update()
    {
        if (baseAgent == null) return;

        //Handle dead agents
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead)
        {
            isMoving = false;
            return;
        }

        // Only process movement if we're supposed to be moving
        if (!isMoving) return;
        if (characterController == null) return;

        // Calculate direction to target
        Vector3 direction = (targetPosition - transform.position).normalized;

        // Move toward target at current speed
        characterController.Move(direction * currentSpeed * Time.deltaTime);

        // Check if we've reached the target
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        if (distanceToTarget <= stoppingDistance)
        {
            // Stop when close enough
            StopMoving();

            // If quarantined, stay idle at destination
            if (isQuarantined)
            {
                baseAgent.SetCurrentAction("quarantined");
            }
        }
        else
        {
            // Face the direction we're moving
            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);

            if (flatDirection.sqrMagnitude > 0.001f)
            {
                transform.forward = flatDirection.normalized;
            }
        }
        //print(baseAgent.AgentName + " - " + baseAgent.InstanceID + " - " + transform.forward);
    }

    #endregion Unity Lifecycle

    #region Basic Movement Commands
    // Tell the agent to walk to a specific position

    public void BreakTo(Vector3 position)
    {
        targetPosition = position;
        currentSpeed = breakSpeed;
        isMoving = true;

        baseAgent.ChangeState(BaseAgent.AgentState.Walking);
        baseAgent.SetCurrentAction("break");
    }

    public void WalkTo(Vector3 position)
    {
        //handle dead agents
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead) return;

        // Cancel quarantine if given new movement command
        isQuarantined = false;

        targetPosition = position;
        currentSpeed = walkSpeed;
        isMoving = true;

        baseAgent.ChangeState(BaseAgent.AgentState.Walking);
        baseAgent.SetCurrentAction("walk");
    }

    // Tell the agent to run to a specific position
    public void RunTo(Vector3 position)
    {
        //handle dead agents
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead) return;

        isQuarantined = false;

        targetPosition = position;
        currentSpeed = runSpeed;
        isMoving = true;

        baseAgent.ChangeState(BaseAgent.AgentState.Running);
        baseAgent.SetCurrentAction("run");
    }

    // Stop all movement
    public void StopMoving()
    {
        //handle dead agents
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead) return;

        isMoving = false;
        currentSpeed = 0f;
        targetPosition = transform.position;

        baseAgent.ChangeState(BaseAgent.AgentState.Idle);
        baseAgent.SetCurrentAction("idle");
    }
    #endregion Basic Movement Commands

    #region Special Movement Commands

    public bool Quarantine()
    {
        //handle dead agents
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead) return false;

        // Find all objects tagged "QuarantineTent"
        GameObject[] tents = GameObject.FindGameObjectsWithTag("QuarantineTent");

        if (tents.Length == 0)
        {
            Debug.LogWarning($"{baseAgent.InstanceID} tried to quarantine but no QuarantineTent found");
            return false;
        }

        // Find closest tent
        GameObject closestTent = null;
        float closestDistance = float.MaxValue;

        foreach (GameObject tent in tents)
        {
            float distance = Vector3.Distance(transform.position, tent.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTent = tent;
            }
        }

        // Set quarantine state and move to tent
        isQuarantined = true;
        targetPosition = closestTent.transform.position;
        currentSpeed = walkSpeed;
        isMoving = true;

        baseAgent.ChangeState(BaseAgent.AgentState.Walking);
        baseAgent.SetCurrentAction("quarantine");

        Debug.Log($"{baseAgent.InstanceID} heading to quarantine (distance: {closestDistance:F1})");
        return true;
    }

    /// <summary>
    /// Avoid a single agent by moving in the opposite direction.
    /// Moves a fixed distance away.
    /// </summary>
    public void Avoid(string targetID)
    {
        BaseAgent target = BaseAgent.GetAgentByInstanceID(targetID);

        if (target == null)
        {
            Debug.LogWarning($"{baseAgent.InstanceID} tried to avoid {targetID} but target not found");
            return;
        }

        // Calculate opposite direction
        Vector3 awayDirection = (transform.position - target.transform.position).normalized;

        // Set destination at fixed distance in opposite direction
        targetPosition = transform.position + awayDirection * AVOID_DISTANCE;
        currentSpeed = runSpeed;  // Run when avoiding
        isMoving = true;

        baseAgent.ChangeState(BaseAgent.AgentState.Running);
        baseAgent.SetCurrentAction("avoid");

        Debug.Log($"{baseAgent.InstanceID} avoiding {targetID}");
    }

    /// <summary>
    /// Avoid two agents by moving in the averaged opposite direction.
    /// Calculates opposite direction from each, then averages them.
    /// </summary>
    public void Avoid(string targetID1, string targetID2)
    {
        BaseAgent target1 = BaseAgent.GetAgentByInstanceID(targetID1);
        BaseAgent target2 = BaseAgent.GetAgentByInstanceID(targetID2);

        // Validate both targets exist
        if (target1 == null)
        {
            Debug.LogWarning($"{baseAgent.InstanceID} tried to avoid {targetID1} but target not found");
            // Fall back to single avoid if we have target2
            if (target2 != null)
            {
                Avoid(targetID2);
            }
            return;
        }

        if (target2 == null)
        {
            Debug.LogWarning($"{baseAgent.InstanceID} tried to avoid {targetID2} but target not found");
            // Fall back to single avoid with target1
            Avoid(targetID1);
            return;
        }

        // Calculate opposite direction from each agent
        Vector3 awayFrom1 = (transform.position - target1.transform.position).normalized;
        Vector3 awayFrom2 = (transform.position - target2.transform.position).normalized;

        // Average the two opposite directions
        Vector3 averageAwayDirection = (awayFrom1 + awayFrom2).normalized;

        // Handle edge case where vectors cancel out (agents on opposite sides)
        if (averageAwayDirection.magnitude < 0.01f)
        {
            // Pick perpendicular direction
            averageAwayDirection = Vector3.Cross(awayFrom1, Vector3.up).normalized;
        }

        // Set destination at fixed distance in averaged direction
        targetPosition = transform.position + averageAwayDirection * AVOID_DISTANCE;
        currentSpeed = runSpeed;
        isMoving = true;

        baseAgent.ChangeState(BaseAgent.AgentState.Running);
        baseAgent.SetCurrentAction("avoid");

        Debug.Log($"{baseAgent.InstanceID} avoiding {targetID1} and {targetID2}");
    }

    #endregion Special Movement Commands
}
