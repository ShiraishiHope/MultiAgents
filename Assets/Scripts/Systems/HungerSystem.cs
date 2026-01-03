using UnityEngine;

public class HungerSystem : MonoBehaviour
{
    #region Singleton
    public static HungerSystem Instance { get; private set; }

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

    #region Configuration
    [Header("Hunger Drain Rates (per second)")]
    [SerializeField] private float idleDrain = 1f;
    [SerializeField] private float walkDrain = 2f;
    [SerializeField] private float runDrain = 3f;
    #endregion

    #region Unity Lifecycle
    void Update()
    {
        BaseAgent[] allAgents = BaseAgent.GetAllAgents();

        foreach (BaseAgent agent in allAgents)
        {
            if (agent.CurrentState == BaseAgent.AgentState.Dead)
                continue;

            float drain = GetDrainRate(agent.CurrentState);
            agent.ModifyHunger(-drain * Time.deltaTime);
        }
    }
    #endregion

    #region Drain Calculation
    private float GetDrainRate(BaseAgent.AgentState state)
    {
        switch (state)
        {
            case BaseAgent.AgentState.Walking:
                return walkDrain;
            case BaseAgent.AgentState.Running:
                return runDrain;
            case BaseAgent.AgentState.Idle:
            default:
                return idleDrain;
        }
    }
    #endregion
}