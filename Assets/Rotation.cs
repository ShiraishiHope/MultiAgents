using UnityEngine;

public class ConveyorTextureScroll : StateMachineBehaviour
{
    public float speed = 0.5f;
    public Vector2 direction = Vector2.up;

    private Renderer rend;
    private Vector2 offset;

    // Quand l'état commence
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        rend = animator.GetComponent<Renderer>();
        offset = rend.material.mainTextureOffset;
    }

    // Tant que l'état est actif
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        offset += direction.normalized * speed * Time.deltaTime;
        rend.material.mainTextureOffset = offset;
    }

    // Quand l'état se termine
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        rend.material.mainTextureOffset = offset; // garde la position
    }
}
