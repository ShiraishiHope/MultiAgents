using System.Collections.Generic;
using TMPro;
using UnityEngine;

//Monobevahior is a base unity class that lets scripts attack to gameobjects and use unity methods like Start() and update()
public class VisionController : MonoBehaviour
{
    #region References
    // References to other components this controller needs
    private BaseAgent baseAgent;
    #endregion

    #region Movement Parameters
    
    [SerializeField] private float sightDistance = 11f;
    [SerializeField] private float sightAngle = 90f;

    // Current movement state
    private Vector3 targetPosition;
    #endregion

    #region Public Properties

    void Start()
    {
        baseAgent = GetComponent<BaseAgent>();
        GetAgentsWithinSights();
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

    public Dictionary<string, Vector3> GetAgentsWithinRange()
    {
        Dictionary<string, Vector3> visibleAgents = new Dictionary<string, Vector3>();
        Dictionary<string, Vector3> allAgents = BaseAgent.GetAllAgentsPosition();

        foreach (KeyValuePair <string, Vector3> targetAgent in allAgents)
        {
            if (targetAgent.Key == baseAgent.InstanceID) 
                continue;

            float dist = Vector3.Distance(targetAgent.Value, transform.position);
            
            if (dist <= sightDistance)
            {
                Vector3 targetDir = targetAgent.Value - transform.position;
                float angle = Vector3.Angle(targetDir, transform.forward);
                // Angle check - if the target is wihtin the field of view
                if (angle <= sightAngle / 2f)
                {
                    visibleAgents.Add(targetAgent.Key, targetAgent.Value);
                    //print(baseAgent.AgentName + " - " + baseAgent.InstanceID + " - seen targets: " + targetAgent.Key + "Pos" + targetAgent.Value);
                }
                    
            }
        }
        return visibleAgents;
    }

    public Dictionary<string, Vector3> GetAgentsWithinSights()
    {

        return GetAgentsWithinRange();
    }

#endregion

    #region Vision Commands


    #endregion
}
