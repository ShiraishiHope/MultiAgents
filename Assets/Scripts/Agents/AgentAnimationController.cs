using UnityEngine;

public class AgentAnimationController : MonoBehaviour
{
    private Animator animator;
    private BaseAgent baseAgent;
    private BaseAgent.AgentState lastState;

    void Start()
    {
        animator = GetComponent<Animator>();
        baseAgent = GetComponent<BaseAgent>();

        if (animator == null || baseAgent == null)
            enabled = false;
    }

    void Update()
    {
        if (baseAgent.CurrentState == lastState) return;

        // Log EVERY state change the animator sees
        Debug.Log($"[ANIM] {baseAgent.InstanceID}: {lastState} → {baseAgent.CurrentState}");

        // Extra logging for dead agents
        if (lastState == BaseAgent.AgentState.Dead)
        {
            Debug.LogError($"[ANIM ZOMBIE] {baseAgent.InstanceID} was Dead, now showing as {baseAgent.CurrentState}!");
        }

        lastState = baseAgent.CurrentState;

        switch (lastState)
        {
            case BaseAgent.AgentState.Idle:
                animator.Play("Idle");
                break;
            case BaseAgent.AgentState.Walking:
                animator.Play("Walk");
                break;
            case BaseAgent.AgentState.Running:
                animator.Play("Run");
                break;
            case BaseAgent.AgentState.Dead:
                animator.Play("Death");
                break;
        }
    }
}