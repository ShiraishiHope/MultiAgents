using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

public class PythonBehaviorController : MonoBehaviour
{
    [Header("Python Behavior Settings")]
    [SerializeField] private string pythonScriptName = "";
    [SerializeField] private float decisionInterval = 2f;

    private AgentActionManager actionManager;
    private BaseAgent baseAgent;

    private static bool pythonInitialized = false;
    private PyObject pythonModule;
    private float nextDecisionTime;
    private string currentTargetID = "";

    public string CurrentTargetID => currentTargetID;

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
                PyObject importlib = Py.Import("importlib");
                pythonModule = importlib.InvokeMethod("reload", pythonModule);
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
                currentTargetID = currentTargetID = decision.action.targetID;

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
        // On récupère les données de base sans passer par Vision/Hearing pour éviter les NullReference
        PyDict perceptionDict = new PyDict();
        Vector3 spawn = baseAgent.SpawnPosition;


        // 1. Identité et Position
        perceptionDict["my_id"] = new PyString(baseAgent.InstanceID);
        perceptionDict["my_x"] = new PyFloat(transform.position.x);
        perceptionDict["my_z"] = new PyFloat(transform.position.z);
        perceptionDict["spawn_x"] = new PyFloat(spawn.x);
        perceptionDict["spawn_z"] = new PyFloat(spawn.z);

        perceptionDict["is_carrying"] = new PyInt(HasItemAttached() ? 1 : 0);

        // 3. Limites de l'usine (Sol centré en 0,0)
        perceptionDict["factory_min_x"] = new PyFloat(-10f);
        perceptionDict["factory_max_x"] = new PyFloat(10f);
        perceptionDict["factory_min_z"] = new PyFloat(-10f);
        perceptionDict["factory_max_z"] = new PyFloat(10f);

        // 4. Liste des items (Tag: Item)
        using (PyList itemList = new PyList())
        {
            GameObject[] items = GameObject.FindGameObjectsWithTag("Item");
            foreach (var item in items)
            {
                // On ne prend que les étagères qui n'ont pas de parent (posées au sol)
                if (item.transform.parent == null)
                {
                    PyDict itemData = new PyDict();
                    itemData["x"] = new PyFloat(item.transform.position.x);
                    itemData["z"] = new PyFloat(item.transform.position.z);
                    itemData["id"] = new PyInt(item.GetEntityId());
                    itemList.Append(itemData);
                }
            }
            perceptionDict["items"] = itemList;
        }

        // 4. Liste des items (Tag: Item)
        using (PyList depositeList = new PyList())
        {
            GameObject[] deposites = GameObject.FindGameObjectsWithTag("Deposite");
            foreach (var deposite in deposites)
            {
                // On ne prend que les étagères qui n'ont pas de parent (posées au sol)
                if (deposite.transform.parent == null)
                {
                    PyDict depositeData = new PyDict();
                    depositeData["x"] = new PyFloat(deposite.transform.position.x);
                    depositeData["z"] = new PyFloat(deposite.transform.position.z);
                    depositeData["id"] = new PyInt(deposite.GetEntityId());
                    depositeList.Append(depositeData);
                }
            }
            perceptionDict["deposites"] = depositeList;
        }

        // 5. Positions des autres robots
        using (PyDict allAgentsDict = new PyDict()) {
                    // On a besoin d'accéder aux autres PythonBehaviorController
                    PythonBehaviorController[] allControllers = FindObjectsByType<PythonBehaviorController>(FindObjectsSortMode.None);
                    foreach (var controller in allControllers) {
                    if (controller == this) continue;
                    using (PyDict agentData = new PyDict()) {
                        agentData["x"] = new PyFloat(controller.transform.position.x);
                        agentData["z"] = new PyFloat(controller.transform.position.z);
                        // ON ENVOIE LA RÉSERVATION DE L'AUTRE ROBOT
                        agentData["current_target_id"] = new PyString(controller.currentTargetID); 
                        allAgentsDict[new PyString(controller.baseAgent.InstanceID)] = agentData;
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
        Vector3 targetPosition = new Vector3(
        movement.targetX,
        transform.position.y,   // ✅ garde la hauteur
        movement.targetZ
        );


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
                // On ne fait rien, c'est l'état normal quand le robot roule
                break;

            case "pick_up":
                if (!HasItemAttached())
                {
                    // Trouve l'étagère la plus proche dans un rayon de 1 unité
                    GameObject nearestShelf = FindNearestWithTag("Item", 1.0f);
                    if (nearestShelf != null)
                    {
                        nearestShelf.transform.SetParent(this.transform);
                        // On place l'étagère "sur le dos" du robot
                        nearestShelf.transform.localPosition = new Vector3(0, 0.2f, 0);
                        Debug.Log("Objet ramassé !");
                    }
                }
                break;

            case "drop_off":
                if (HasItemAttached())
                {
                    // On détache l'étagère
                    Transform shelf = GetAttachedItem();
                    shelf.SetParent(null);
                    shelf.gameObject.tag = "Untagged";
                    // On la pose proprement au sol
                    shelf.position = new Vector3(shelf.position.x, -0.08f, shelf.position.z);
                    Debug.Log("Objet déposé !");
                }
                break;
            default:
                Debug.LogWarning($"⚠ Unknown action type '{action.actionType}' for {baseAgent.InstanceID}");
                break;
        }
    }

    // Fonction utilitaire pour trouver l'étagère
    private GameObject FindNearestWithTag(string tag, float radius)
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
        GameObject nearest = null;
        float minDist = radius;
        foreach (GameObject obj in objects)
        {
            float dist = Vector3.Distance(transform.position, obj.transform.position);
            if (dist < minDist)
            {
                nearest = obj;
                minDist = dist;
            }
        }
        return nearest;
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

    // Vérifie si une étagère est déjà portée
    private bool HasItemAttached()
    {
        return GetAttachedItem() != null;
    }

    // Récupère le Transform de l'étagère portée
    private Transform GetAttachedItem()
    {
        foreach (Transform child in transform)
        {
            if (child.CompareTag("Item")) return child;
        }
        return null;
    }
}