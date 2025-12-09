using UnityEngine;

//Monobevahior is a base unity class that lets scripts attack to gameobjects and use unity methods like Start() and update()
public class MovementController : MonoBehaviour
{
    #region References
    // References to other components this controller needs
    private BaseAgent baseAgent;
    #endregion

    #region Movement Parameters
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 3f;      // Normal walking speed
    [SerializeField] private float runSpeed = 5f;       // Running/sprinting speed
    [SerializeField] private float stoppingDistance = 0.1f;  // How close to target before stopping

    // Current movement state
    private Vector3 targetPosition;
    private float currentSpeed;
    private bool isMoving = false;
    #endregion

    #region Public Properties
    // Allow other scripts to check movement status
    public bool IsMoving => isMoving;
    public Vector3 TargetPosition => targetPosition;
    #endregion

    public void Initialize(BaseAgent agent)
    {
        // Store references passed from the action manager
        baseAgent = agent;

        // Set initial target to current position (not moving)
        targetPosition = transform.position;
    }

    void Update()
    {
        // Only process movement if we're supposed to be moving
        if (!isMoving) return;

        // Calculate direction to target
        Vector3 direction = (targetPosition - transform.position).normalized;

        // Move toward target at current speed
        transform.position += direction * currentSpeed * Time.deltaTime;

        // Check if we've reached the target
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        if (distanceToTarget <= stoppingDistance)
        {
            // Stop when close enough
            StopMoving();
        }
        else
        {
            // Face the direction we're moving
            transform.forward = direction;
        }
        //print(baseAgent.AgentName + " - " + baseAgent.InstanceID + " - " + transform.forward);
    }

    #region Movement Commands
    // Tell the agent to walk to a specific position
    public void WalkTo(Vector3 position)
    {
        targetPosition = position;
        currentSpeed = walkSpeed;
        isMoving = true;

        // Update agent state to Walking
        baseAgent.ChangeState(BaseAgent.AgentState.Walking);
        
    }

    // Tell the agent to run to a specific position
    public void RunTo(Vector3 position)
    {
        targetPosition = position;
        currentSpeed = runSpeed;
        isMoving = true;

        // Update agent state to Running
        baseAgent.ChangeState(BaseAgent.AgentState.Running);
    }

    // Stop all movement
    public void StopMoving()
    {
        isMoving = false;
        currentSpeed = 0f;
        targetPosition = transform.position;

        // Update agent state to Idle
        baseAgent.ChangeState(BaseAgent.AgentState.Idle);
    }
    #endregion

}
