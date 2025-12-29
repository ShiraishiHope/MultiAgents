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