using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PythonBehaviorController : MonoBehaviour
{
    [Header("Python Behavior Settings")]
    [SerializeField] private string pythonScriptName = "random_movement";
    [SerializeField] private float decisionInterval = 2f;

    private AgentActionManager actionManager;
    private BaseAgent baseAgent;

    private static bool pythonInitialized = false;
    private PyObject pythonModule;
    private float nextDecisionTime;

    #region Decision Data Structures

    // ===== Output Struct: What python sends back =====
    // All decisions an agent can make

    public struct AgentDecisionData
    {
        public MovementDecision movement;

        public ActionDecision action;
    }

    public struct MovementDecision
    {
        //walk, run, stop, none
        public string movementType;

        //Target's coordinates
        public float targetX;
        public float targetZ;
    }

    public struct ActionDecision
    {
        //action to perform
        public string actionType;

        //action's target
        public string targetID;

        // optional parameters for action
        public Dictionary<string, object> parameters;
    }

    #endregion Decision Data Structures

    void Awake()
    {
        actionManager = GetComponent<AgentActionManager>();
        baseAgent = GetComponent<BaseAgent>();

        if (!pythonInitialized)
        {
            InitializePython();
            pythonInitialized = true;
        }
    }

    void Start()
    {
        // Skip Python loading if no script name provided
        if (string.IsNullOrEmpty(pythonScriptName))
        {
            enabled = false; // Stop Update() from running
            return;
        }

        LoadPythonBehavior(pythonScriptName);
        nextDecisionTime = Time.time + UnityEngine.Random.Range(0f, decisionInterval);
    }

    void Update()
    {
        if (Time.time >= nextDecisionTime && actionManager != null)
        {
            MakeDecision();
            nextDecisionTime = Time.time + decisionInterval;
        }
    }

    void InitializePython()
    {
        try
        {
            // Auto-detect Python DLL location
            string pythonDll = FindPythonDLL();

            if (!string.IsNullOrEmpty(pythonDll))
            {
                Runtime.PythonDLL = pythonDll;
                Debug.Log($"✓ Found Python DLL: {pythonDll}");
            }
            else
            {
                Debug.LogError("❌ Could not find Python DLL. Make sure Python is installed.");
                return;
            }

            PythonEngine.Initialize();
            Debug.Log("✓ Python engine initialized!");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Failed to initialize Python: {e.Message}");
            Debug.LogError("Make sure Python is installed and in your system PATH");
        }
    }

    string FindPythonDLL()
    {
        // Try to find Python DLL using common locations and PATH
        string[] possibleVersions = { "313", "312", "311", "310", "39", "38" };

        // Method 1: Check common installation paths
        string[] commonPaths = {
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "C:\\Python"
    };

        foreach (string basePath in commonPaths)
        {
            foreach (string version in possibleVersions)
            {
                string dllPath = Path.Combine(basePath, $"Programs\\Python\\Python{version}\\python{version}.dll");
                if (File.Exists(dllPath))
                {
                    return dllPath;
                }

                // Try without "Programs" subfolder
                dllPath = Path.Combine(basePath, $"Python\\Python{version}\\python{version}.dll");
                if (File.Exists(dllPath))
                {
                    return dllPath;
                }

                // Try direct in basePath
                dllPath = Path.Combine(basePath, $"Python{version}\\python{version}.dll");
                if (File.Exists(dllPath))
                {
                    return dllPath;
                }
            }
        }

        // Method 2: Try to find Python using PATH
        try
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "python";
            process.StartInfo.Arguments = "-c \"import sys; print(sys.base_prefix)\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(output) && Directory.Exists(output))
            {
                // Try to find python3xx.dll in this directory
                string[] dllFiles = Directory.GetFiles(output, "python*.dll");
                if (dllFiles.Length > 0)
                {
                    return dllFiles[0];
                }
            }
        }
        catch
        {
            // Python not in PATH
        }

        return null;
    }

    public void LoadPythonBehavior(string scriptName)
    {
        try
        {
            using (Py.GIL())
            {
                // Add Assets/Scripts/Student to Python path
                PyObject sys = Py.Import("sys");
                string pythonScriptsPath = Path.Combine(Application.dataPath, "Scripts/Student");
                pythonScriptsPath = Path.GetFullPath(pythonScriptsPath);

                PyList path = new PyList(sys.GetAttr("path"));
                if (!path.Contains(new PyString(pythonScriptsPath)))
                {
                    path.InvokeMethod("append", new PyString(pythonScriptsPath));
                }

                // Import the student's script
                pythonModule = Py.Import(scriptName);
                Debug.Log($"✓ Loaded Python behavior: {scriptName}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Failed to load '{scriptName}': {e.Message}");
            Debug.LogError($"Make sure {scriptName}.py exists in Assets/Scripts/Student/");
        }
    }

    #region Decision Processing

    void MakeDecision()
    {
        if (pythonModule == null) return;

        try
        {
            using (Py.GIL())
            {

                // Build perception data and send to Python
                PyDict perceptionDict = BuildPerceptionDict();

                //Call Python's deceide_action function
                PyObject decideAction = pythonModule.GetAttr("decide_action");
                PyObject result = decideAction.Invoke(perceptionDict);

                AgentDecisionData decision = ParseDecisionResponse(result);

                ExecuteDecision(decision);

                perceptionDict.Dispose();                
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error in Python decision cycle for {baseAgent.InstanceID}: {e.Message}");
        }
    }

    //Build perception dictionnary to send to Python (API Input)
    private PyDict BuildPerceptionDict()
    {
        AgentActionManager.AgentPerceptionData data = actionManager.GetPerceptionData();
        PyDict perceptionDict = new PyDict();

        // Basic identity and position information
        perceptionDict["my_id"] = new PyString(data.myInstanceID);
        perceptionDict["my_x"] = new PyFloat(data.myPosition.x);
        perceptionDict["my_z"] = new PyFloat(data.myPosition.z);
        perceptionDict["my_type"] = new PyString(data.myType);
        perceptionDict["my_faction"] = new PyString(data.myFaction);

        // ----- HEALTH/STATUS DATA -----
        perceptionDict["health"] = new PyFloat(data.health);
        perceptionDict["infection_name"] = new PyString(data.infectionName);
        perceptionDict["infection_stage"] = new PyInt(data.infectionStage);
        perceptionDict["mortality_rate"] = new PyFloat(data.mortalityRate);
        perceptionDict["recovery_rate"] = new PyFloat(data.recoveryRate);
        perceptionDict["infectivity"] = new PyFloat(data.infectivity);
        perceptionDict["is_contagious"] = new PyInt(data.isContagious ? 1 : 0);
        perceptionDict["is_immune"] = new PyInt(data.isImmune ? 1 : 0);

        // Symptoms as a list
        using (PyList symptomsList = new PyList())
        {
            foreach (string symptom in data.symptoms)
            {
                symptomsList.Append(new PyString(symptom));
            }
            perceptionDict["symptoms"] = symptomsList;
        }

        // Agents this agent can currently see (within sight cone)
        using (PyDict visibleDict = new PyDict())
        {
            foreach (var kvp in data.visibleAgents)
            {
                using (PyDict agentData = new PyDict())
                {
                    agentData["x"] = new PyFloat(kvp.Value.x);
                    agentData["z"] = new PyFloat(kvp.Value.z);
                    visibleDict[new PyString(kvp.Key)] = agentData;
                }
            }
            perceptionDict["visible_agents"] = visibleDict;
        }
        perceptionDict["visible_count"] = new PyInt(data.visibleCount);

        // Agents this agent can hear (within hearing radius)
        using (PyDict heardDict = new PyDict())
        {
            foreach (var kvp in data.heardAgents)
            {
                using (PyDict agentData = new PyDict())
                {
                    agentData["x"] = new PyFloat(kvp.Value.x);
                    agentData["z"] = new PyFloat(kvp.Value.z);
                    heardDict[new PyString(kvp.Key)] = agentData;
                }
            }
            perceptionDict["heard_agents"] = heardDict;
        }
        perceptionDict["heard_count"] = new PyInt(data.heardCount);

        // All agents in the simulation (global registry)
        using (PyDict allAgentsDict = new PyDict())
        {
            Dictionary<string, Vector3> allAgents = BaseAgent.GetAllAgentsPosition();
            foreach (var kvp in allAgents)
            {
                if (kvp.Key == data.myInstanceID) continue; // Skip self

                using (PyDict agentData = new PyDict())
                {
                    agentData["x"] = new PyFloat(kvp.Value.x);
                    agentData["z"] = new PyFloat(kvp.Value.z);
                    allAgentsDict[new PyString(kvp.Key)] = agentData;
                }
            }
            perceptionDict["all_agents"] = allAgentsDict;
        }

        return perceptionDict;
    }

    // Parses the Python response dictionary into C# decision struct.
    private AgentDecisionData ParseDecisionResponse(PyObject response)
    {
        AgentDecisionData decision = new AgentDecisionData();

        // Initialize with safe defaults
        decision.movement = new MovementDecision
        {
            movementType = "none",
            targetX = 0f,
            targetZ = 0f
        };

        decision.action = new ActionDecision
        {
            actionType = "none",
            targetID = "",
            parameters = new Dictionary<string, object>()
        };

        try
        {
            PyDict responseDict = new PyDict(response);

            // ----- PARSE MOVEMENT -----
            if (responseDict.HasKey("movement"))
            {
                PyDict movementDict = new PyDict(responseDict["movement"]);

                if (movementDict.HasKey("type"))
                {
                    decision.movement.movementType = movementDict["type"].As<string>().ToLower();
                }

                if (movementDict.HasKey("target_x"))
                {
                    decision.movement.targetX = movementDict["target_x"].As<float>();
                }

                if (movementDict.HasKey("target_z"))
                {
                    decision.movement.targetZ = movementDict["target_z"].As<float>();
                }
            }

            // ----- PARSE ACTION -----
            if (responseDict.HasKey("action"))
            {
                PyDict actionDict = new PyDict(responseDict["action"]);

                if (actionDict.HasKey("type"))
                {
                    decision.action.actionType = actionDict["type"].As<string>().ToLower();
                }

                if (actionDict.HasKey("target_id"))
                {
                    decision.action.targetID = actionDict["target_id"].As<string>();
                }

                // Parse optional parameters if present
                if (actionDict.HasKey("parameters"))
                {
                    PyDict paramsDict = new PyDict(actionDict["parameters"]);
                    foreach (PyObject key in paramsDict.Keys())
                    {
                        string keyStr = key.As<string>();
                        // Store as object - caller will need to cast appropriately
                        decision.action.parameters[keyStr] = paramsDict[key].AsManagedObject(typeof(object));
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠ Failed to parse Python response for {baseAgent.InstanceID}: {e.Message}. Using defaults.");
        }

        return decision;
    }

    /// Executes the parsed decision through the appropriate controllers.


    private void ExecuteDecision(AgentDecisionData decision)
    {
        // ----- EXECUTE MOVEMENT -----
        ExecuteMovement(decision.movement);

        // ----- EXECUTE ACTION -----
        ExecuteAction(decision.action);
    }

    private void ExecuteMovement(MovementDecision movement)
    {
        Vector3 targetPosition = new Vector3(movement.targetX, 0f, movement.targetZ);

        switch (movement.movementType)
        {
            case "walk":
                actionManager.MoveTo(targetPosition);
                break;

            case "run":
                actionManager.RunTo(targetPosition);
                break;

            case "stop":
                actionManager.Stop();
                break;

            case "none":
                // No movement command - agent continues whatever it was doing
                break;

            default:
                Debug.LogWarning($"⚠ Unknown movement type '{movement.movementType}' for {baseAgent.InstanceID}");
                break;
        }
    }

    private void ExecuteAction(ActionDecision action)
    {
        switch (action.actionType)
        {
            case "none":
                // No action this cycle
                break;

            // ===== FUTURE ACTION TYPES =====
            // Add cases here as you implement more agent capabilities
            //
            // case "attack":
            //     actionManager.Attack(action.targetID);
            //     break;
            //
            // case "communicate":
            //     string message = action.parameters["message"] as string;
            //     actionManager.SendMessage(action.targetID, message);
            //     break;

            default:
                Debug.LogWarning($"⚠ Unknown action type '{action.actionType}' for {baseAgent.InstanceID}");
                break;
        }
    }

    #endregion Decision Processing

    void OnDestroy()
    {
        pythonModule?.Dispose();
        pythonModule = null;
    }

    void OnApplicationQuit()
    {
        if (pythonInitialized)
        {
            PythonEngine.Shutdown();
        }
    }
}