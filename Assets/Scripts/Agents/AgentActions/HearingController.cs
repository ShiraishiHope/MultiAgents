using System.Collections.Generic;
using TMPro;
using UnityEngine;

//Monobevahior is a base unity class that lets scripts attack to gameobjects and use unity methods like Start() and update()
public class HearingController : MonoBehaviour
{
    #region References
    // References to other components this controller needs
    private BaseAgent baseAgent;
    #endregion

    #region Movement Parameters

    [SerializeField] private float hearingDistance = 6f;

    // Current movement state
    private Vector3 targetPosition;
    #endregion

    #region Public Properties

    void Start()
    {
        baseAgent = GetComponent<BaseAgent>();
        GetAgentsWithinHearing();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Initialize(BaseAgent agent)
    {
        // Store references passed from the action manager
        baseAgent = agent;

        // Set initial target to current position (not moving)
        targetPosition = transform.position;
    }

    public Dictionary<string, Vector3> GetAgentsWithinHearing()
    {
        Dictionary<string, Vector3> heardAgents = new Dictionary<string, Vector3>();
        Dictionary<string, Vector3> allAgents = BaseAgent.GetAllAgentsPosition();

        foreach (KeyValuePair<string, Vector3> targetAgent in allAgents)
        {
            if (targetAgent.Key == baseAgent.InstanceID)
                continue;

            float dist = Vector3.Distance(targetAgent.Value, transform.position);

            if (dist <= hearingDistance)
            {
                // range check - if the target is within range
                heardAgents.Add(targetAgent.Key, targetAgent.Value);
                //print(baseAgent.AgentName + " - " + baseAgent.InstanceID + " - heard targets: " + targetAgent.Key + "Pos" + targetAgent.Value);
            }
        }
        return heardAgents;
    }

    #endregion

    #region Hearing Commands


    #endregion
}
