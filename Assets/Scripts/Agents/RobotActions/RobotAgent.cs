using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class RobotAgent : MonoBehaviour
{
    #region serializedVariables
    [Header("Robot Identity")]
    [SerializeField] private string robotName = "Kiva_Bot";
    [SerializeField] private string instanceID = "";
    [SerializeField] private TextMeshProUGUI statusDisplay;

    [Header("Robot Specs")]
    [SerializeField] private RobotModel modelType;
    [SerializeField] private FleetType fleet;

    [Header("Maintenance & Battery")]
    [SerializeField] private string errorLog = "None";
    [SerializeField][Range(0f, 100f)] private float batteryLevel = 100f;
    [SerializeField] private RobotStatus status = RobotStatus.Operational;
    [SerializeField] private bool needsMaintenance = false;
    #endregion

    #region variables
    private static Dictionary<string, RobotAgent> robotRegistry = new Dictionary<string, RobotAgent>();

    public enum RobotModel { Picker, Lifter, Packer, Transporter }
    public enum FleetType { Storage_A, Storage_B, Loading_Dock }
    public enum RobotStatus { Operational, Charging, MaintenanceRequired, CriticalError, Deactivated }

    public RobotStatus CurrentStatus => status;
    public float Battery => batteryLevel;
    #endregion

    #region Registry Methods
    public static RobotAgent GetRobotByID(string id)
    {
        robotRegistry.TryGetValue(id, out RobotAgent robot);
        return robot;
    }

    public static RobotAgent[] GetAllRobots()
    {
        RobotAgent[] robots = new RobotAgent[robotRegistry.Count];
        robotRegistry.Values.CopyTo(robots, 0);
        return robots;
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        instanceID = $"{modelType}_{Mathf.Abs(GetInstanceID())}";
        if (!robotRegistry.ContainsKey(instanceID))
            robotRegistry[instanceID] = this;
    }

    private void Start()
    {
        if (statusDisplay == null)
            statusDisplay = GetComponentInChildren<TextMeshProUGUI>();

        UpdateDisplay();
    }

    private void Update()
    {
        // Simulation de la batterie qui baisse (exemple)
        if (batteryLevel > 0) batteryLevel -= Time.deltaTime * 0.1f;

        // Met à jour l'affichage au-dessus du robot
        if (statusDisplay != null && Camera.main != null)
        {
            statusDisplay.transform.LookAt(Camera.main.transform);
            statusDisplay.transform.Rotate(0, 180, 0);
            statusDisplay.text = $"{instanceID}\nBat: {Mathf.Round(batteryLevel)}%";
        }
    }
    #endregion

    private void UpdateDisplay()
    {
        if (statusDisplay != null) statusDisplay.text = instanceID;
    }
}