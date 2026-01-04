using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

/// <summary>
/// Controls Python-based agent behavior using batch processing.
/// Each agent has this component, but Python is called once for ALL agents.
/// 
/// How it works:
/// - Each instance registers itself on Start()
/// - One instance is elected as "batch master" (first to register)
/// - The master collects all agents' perception data and calls Python once
/// - Decisions are distributed back to each agent
/// </summary>
public class PythonBehaviorController : MonoBehaviour
{
    #region Configuration
    [Header("Python Behavior Settings")]
    [SerializeField] private string pythonScriptName = "batch_behavior";
    [SerializeField] private float decisionInterval = 0.5f;
    #endregion

    #region Instance References
    private AgentActionManager actionManager;
    private BaseAgent baseAgent;

    #endregion

    #region Static Batch Management
    // Shared across ALL instances
    private static bool pythonInitialized = false;
    public static bool IsPythonReady => pythonInitialized;
    private static bool hasShutdown = false;
    private static PyObject pythonModule;
    private static float nextDecisionTime;
    private static string currentScriptName;
    private string currentTargetID = "0";
    public string CurrentTargetID => currentTargetID;

    // Registry of all active agents
    private static Dictionary<string, PythonBehaviorController> registeredAgents =
        new Dictionary<string, PythonBehaviorController>();

    // The instance responsible for batch processing
    private static PythonBehaviorController batchMaster = null;
    #endregion

    #region Cached Python Keys
    // Cached PyString keys - created once, reused forever
    private static Dictionary<string, PyString> _cachedKeys = new Dictionary<string, PyString>();
    private static bool _keysInitialized = false;

    /// <summary>
    /// Gets or creates a cached PyString for a key.
    /// Call only within GIL!
    /// </summary>
    private static PyString GetCachedKey(string key)
    {
        if (!_cachedKeys.TryGetValue(key, out PyString pyKey))
        {
            pyKey = new PyString(key);
            _cachedKeys[key] = pyKey;
        }
        return pyKey;
    }

    /// <summary>
    /// Pre-initialize all commonly used keys.
    /// </summary>
    private static void InitializeCachedKeys()
    {
        if (_keysInitialized) return;

        string[] commonKeys = {
        "my_id", "my_x", "my_z", "my_type", "my_faction", "my_state",
        "health", "infection_name", "infection_stage", "mortality_rate",
        "recovery_rate", "infectivity", "is_contagious", "is_immune",
        "incubation_period", "contagious_duration", "symptoms",
        "visible_agents", "visible_count", "heard_agents", "heard_count",
        "x", "z", "distance", "current_action", "action_start_time",
        "faction", "state", "movement", "action", "type", "target_x",
        "target_z", "target_id", "parameters",
        "hunger","visible_food", "visible_food_count",
        "items", "deposites", "obstacles", "radius", "id", "is_carrying", "current_target_id",
        "spawn_x", "spawn_z"
    };

        foreach (string key in commonKeys)
        {
            _cachedKeys[key] = new PyString(key);
        }

        _keysInitialized = true;
    }
    #endregion

    #region Decision Data Structures
    public struct AgentDecisionData
    {
        public MovementDecision movement;
        public ActionDecision action;
    }

    public struct MovementDecision
    {
        public string movementType;
        public float targetX;
        public float targetZ;
    }

    public struct ActionDecision
    {
        public string actionType;
        public string targetID;
        public Dictionary<string, object> parameters;
    }
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        actionManager = GetComponent<AgentActionManager>();
        baseAgent = GetComponent<BaseAgent>();

        // Initialize Python once (first agent to awake does this)
        if (!pythonInitialized && !hasShutdown)
        {
            pythonInitialized = InitializePython();
        }
    }

    void Start()
    {

        // Skip if no script name provided
        if (string.IsNullOrEmpty(pythonScriptName))
        {
            enabled = false;
            return;
        }

        // Register this agent for batch processing
        RegisterSelf();

        // Load Python module if needed (first agent or script changed)
        if (pythonModule == null || currentScriptName != pythonScriptName)
        {
            LoadPythonBehavior(pythonScriptName);
            currentScriptName = pythonScriptName;
        }
    }

    void Update()
    {
        // Only the batch master processes decisions
        if (batchMaster != this) return;
        if (pythonModule == null) return;

        if (Time.time >= nextDecisionTime)
        {
            ProcessAllAgentsBatch();
            nextDecisionTime = Time.time + decisionInterval;
        }
    }

    void OnDestroy()
    {
        UnregisterSelf();
    }

    void OnApplicationQuit()
    {
        if (!hasShutdown && pythonInitialized)
        {
            hasShutdown = true;
            pythonModule?.Dispose();
            pythonModule = null;
            PythonEngine.Shutdown();
            pythonInitialized = false;
        }
    }
    #endregion

    #region Registration
    /// <summary>
    /// Registers this agent instance for batch processing.
    /// First agent to register becomes the batch master.
    /// </summary>
    private void RegisterSelf()
    {
        if (baseAgent == null || actionManager == null) return;

        string id = baseAgent.InstanceID;

        if (!registeredAgents.ContainsKey(id))
        {
            registeredAgents[id] = this;
            Debug.Log($"Registered agent {id} for batch processing. Total: {registeredAgents.Count}");
        }

        // First registered agent becomes batch master
        if (batchMaster == null)
        {
            batchMaster = this;
            nextDecisionTime = Time.time + UnityEngine.Random.Range(0f, decisionInterval);
            Debug.Log($"Batch master elected: {id}");
        }
    }

    /// <summary>
    /// Removes this agent from batch processing.
    /// If this was the batch master, elects a new one.
    /// </summary>
    private void UnregisterSelf()
    {
        if (baseAgent == null) return;

        string id = baseAgent.InstanceID;
        registeredAgents.Remove(id);

        // If I was the master, elect a new one
        if (batchMaster == this)
        {
            batchMaster = null;

            // Find a new master from remaining agents
            foreach (var kvp in registeredAgents)
            {
                if (kvp.Value != null && kvp.Value.enabled)
                {
                    batchMaster = kvp.Value;
                    Debug.Log($"New batch master elected: {kvp.Key}");
                    break;
                }
            }
        }
    }
    #endregion

    #region Python Initialization
    private bool InitializePython()
    {
        try
        {
            string pythonDll = FindPythonDLL();

            if (!string.IsNullOrEmpty(pythonDll))
            {
                Runtime.PythonDLL = pythonDll;
                Debug.Log($"Found Python DLL: {pythonDll}");
            }
            else
            {
                Debug.LogError("Could not find Python DLL. Make sure Python is installed.");
                return false;
            }

            PythonEngine.Initialize();
            Debug.Log("Python engine initialized for batch processing");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Python: {e.Message}");
            return false;
        }
    }

    private string FindPythonDLL()
    {
        string[] possibleVersions = { "313", "312", "311", "310", "39", "38" };
        string[] commonPaths = {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "C:\\Python"
        };

        foreach (string basePath in commonPaths)
        {
            foreach (string version in possibleVersions)
            {
                string[] patterns = {
                    Path.Combine(basePath, $"Programs\\Python\\Python{version}\\python{version}.dll"),
                    Path.Combine(basePath, $"Python\\Python{version}\\python{version}.dll"),
                    Path.Combine(basePath, $"Python{version}\\python{version}.dll")
                };

                foreach (string dllPath in patterns)
                {
                    if (File.Exists(dllPath)) return dllPath;
                }
            }
        }

        // Try PATH-based detection
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
                string[] dllFiles = Directory.GetFiles(output, "python*.dll");
                if (dllFiles.Length > 0) return dllFiles[0];
            }
        }
        catch { }

        return null;
    }

    private void LoadPythonBehavior(string scriptName)
    {
        try
        {
            using (Py.GIL())
            {
                PyObject sys = Py.Import("sys");
                string pythonScriptsPath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "Scripts/Student")
                );

                PyList path = new PyList(sys.GetAttr("path"));
                using (PyString pathStr = new PyString(pythonScriptsPath))
                {
                    if (!path.Contains(pathStr))
                    {
                        path.InvokeMethod("append", pathStr);
                    }
                }

                PyObject importlib = Py.Import("importlib");

                // Dispose old module if exists
                pythonModule?.Dispose();
                pythonModule = importlib.InvokeMethod("reload", Py.Import(scriptName));

                Debug.Log($"Loaded Python behavior: {scriptName}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load '{scriptName}': {e.Message}");
            Debug.LogError($"Make sure {scriptName}.py exists in Assets/Scripts/Student/");
        }
    }
    #endregion

    #region Batch Processing
    /// <summary>
    /// Main batch processing method. Called by batch master only.
    /// Gathers all perception data, calls Python once, distributes decisions.
    /// </summary>
    private void ProcessAllAgentsBatch()
    {
        if (registeredAgents.Count == 0) return;

        var totalWatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using (Py.GIL())
            {
                // Measure building perception data
                var buildWatch = System.Diagnostics.Stopwatch.StartNew();
                using (PyDict batchPerception = BuildBatchPerceptionDict())
                {
                    buildWatch.Stop();

                    // Measure Python execution
                    var pythonWatch = System.Diagnostics.Stopwatch.StartNew();
                    using (PyObject decideAllFunc = pythonModule.GetAttr("decide_all"))
                    using (PyObject batchResults = decideAllFunc.Invoke(batchPerception))
                    {
                        pythonWatch.Stop();

                        // Measure distributing decisions
                        var distributeWatch = System.Diagnostics.Stopwatch.StartNew();
                        DistributeDecisions(batchResults);
                        distributeWatch.Stop();

                        totalWatch.Stop();

                        // Log every 10th call to avoid spam
                        if (Time.frameCount % 10 == 0)
                        {
                            Debug.Log($"[Perf] Build: {buildWatch.ElapsedMilliseconds}ms | " +
                                      $"Python: {pythonWatch.ElapsedMilliseconds}ms | " +
                                      $"Distribute: {distributeWatch.ElapsedMilliseconds}ms | " +
                                      $"Total: {totalWatch.ElapsedMilliseconds}ms");
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Batch decision error: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Builds dictionary containing ALL agents' perception data.
    /// Structure: { agent_id: { perception_data }, ... }
    /// </summary>
    private PyDict BuildBatchPerceptionDict()
    {
        PyDict batchDict = new PyDict();

        PyDict allAgentsDict = new PyDict();

        foreach (var kvp in registeredAgents)
        {
            string agentID = kvp.Key;
            PythonBehaviorController controller = kvp.Value;

            if (controller == null) continue;

            Vector3 pos = controller.transform.position;

            // Build position dict for this agent
            using (PyString agentKey = new PyString(agentID))
            {
                PyDict posDict = new PyDict();

                using (PyFloat x = new PyFloat(pos.x))
                using (PyFloat z = new PyFloat(pos.z))
                using (PyString targetId = new PyString(controller.currentTargetID ?? "0"))
                using (PyInt isCarrying = new PyInt(controller.HasItemAttached() ? 1 : 0))
                {
                    posDict[GetCachedKey("x")] = x;
                    posDict[GetCachedKey("z")] = z;
                    posDict[GetCachedKey("current_target_id")] = targetId;
                    posDict[GetCachedKey("is_carrying")] = isCarrying;
                }

                allAgentsDict[agentKey] = posDict;
                // Note: posDict is NOT disposed - it's now owned by allAgentsDict
            }
        }

        foreach (var kvp in registeredAgents)
        {
            string agentID = kvp.Key;
            PythonBehaviorController controller = kvp.Value;

            if (controller == null || controller.actionManager == null) continue;

            AgentActionManager.AgentPerceptionData data = controller.actionManager.GetPerceptionData();

            // Agent IDs are dynamic, can't cache
            using (PyString agentKey = new PyString(agentID))
            {
                // Don't use 'using' on agentPerception - it gets stored in batchDict
                PyDict agentPerception = BuildSingleAgentPerception(data, agentID);
                agentPerception[GetCachedKey("all_agents")] = allAgentsDict;
                batchDict[agentKey] = agentPerception;
            }
        }

        return batchDict;
    }

    /// <summary>
    /// Builds perception dictionary for a single agent with proper memory management.
    /// </summary>
    private PyDict BuildSingleAgentPerception(
    AgentActionManager.AgentPerceptionData data,
    string agentID)
    {
        // Initialize keys on first call
        if (!_keysInitialized) InitializeCachedKeys();

        PyDict perception = new PyDict();

        // Helper using cached keys - note: keys are NOT disposed (they're cached)
        void SetString(PyDict dict, string key, string value)
        {
            using (PyString v = new PyString(value ?? ""))
                dict[GetCachedKey(key)] = v;
        }

        void SetFloat(PyDict dict, string key, float value)
        {
            using (PyFloat v = new PyFloat(value))
                dict[GetCachedKey(key)] = v;
        }

        void SetInt(PyDict dict, string key, int value)
        {
            using (PyInt v = new PyInt(value))
                dict[GetCachedKey(key)] = v;
        }

        // ----- IDENTITY -----
        SetString(perception, "my_id", data.myInstanceID);
        SetFloat(perception, "my_x", data.myPosition.x);
        SetFloat(perception, "my_z", data.myPosition.z);
        SetString(perception, "my_type", data.myType);
        SetString(perception, "my_faction", data.myFaction);
        SetString(perception, "my_state", data.myState);

        // ----- HEALTH/STATUS -----
        SetFloat(perception, "health", data.health);
        SetString(perception, "infection_name", data.infectionName);
        SetInt(perception, "infection_stage", data.infectionStage);
        SetFloat(perception, "mortality_rate", data.mortalityRate);
        SetFloat(perception, "recovery_rate", data.recoveryRate);
        SetFloat(perception, "infectivity", data.infectivity);
        SetInt(perception, "is_contagious", data.isContagious ? 1 : 0);
        SetInt(perception, "is_immune", data.isImmune ? 1 : 0);
        SetFloat(perception, "incubation_period", data.incubationPeriod);
        SetFloat(perception, "contagious_duration", data.contagiousDuration);
        SetFloat(perception, "hunger", data.hunger);

        // ----- SYMPTOMS -----
        using (PyList symptomsList = new PyList())
        {
            if (data.symptoms != null)
            {
                foreach (string symptom in data.symptoms)
                {
                    using (PyString s = new PyString(symptom))
                        symptomsList.Append(s);
                }
            }
            perception[GetCachedKey("symptoms")] = symptomsList;
        }

        // ----- VISIBLE AGENTS -----
        using (PyDict visibleDict = new PyDict())
        {
            if (data.visibleAgents != null)
            {
                foreach (var agent in data.visibleAgents)
                {
                    // Agent ID keys can't be cached (dynamic), but inner keys can
                    using (PyString agentKey = new PyString(agent.Key))
                    using (PyDict agentData = new PyDict())
                    {
                        SetFloat(agentData, "x", agent.Value.position.x);
                        SetFloat(agentData, "z", agent.Value.position.z);
                        SetFloat(agentData, "distance", agent.Value.distance);
                        SetString(agentData, "current_action", agent.Value.currentAction);
                        SetFloat(agentData, "action_start_time", agent.Value.actionStartTime);
                        SetString(agentData, "faction", agent.Value.faction);
                        SetString(agentData, "state", agent.Value.state);
                        visibleDict[agentKey] = agentData;
                    }
                }
            }
            perception[GetCachedKey("visible_agents")] = visibleDict;
        }
        SetInt(perception, "visible_count", data.visibleCount);

        // ----- HEARD AGENTS -----
        using (PyDict heardDict = new PyDict())
        {
            if (data.heardAgents != null)
            {
                foreach (var agent in data.heardAgents)
                {
                    using (PyString agentKey = new PyString(agent.Key))
                    using (PyDict agentData = new PyDict())
                    {
                        SetFloat(agentData, "x", agent.Value.position.x);
                        SetFloat(agentData, "z", agent.Value.position.z);
                        SetFloat(agentData, "distance", agent.Value.distance);
                        heardDict[agentKey] = agentData;
                    }
                }
            }
            perception[GetCachedKey("heard_agents")] = heardDict;
        }
        SetInt(perception, "heard_count", data.heardCount);

        // ----- VISIBLE FOOD -----
        using (PyDict foodDict = new PyDict())
        {
            if (data.visibleFood != null)
            {
                foreach (var food in data.visibleFood)
                {
                    using (PyString foodKey = new PyString(food.Key))
                    using (PyDict foodData = new PyDict())
                    {
                        SetFloat(foodData, "x", food.Value.position.x);
                        SetFloat(foodData, "z", food.Value.position.z);
                        SetFloat(foodData, "distance", food.Value.distance);
                        foodDict[foodKey] = foodData;
                    }
                }
            }
            perception[GetCachedKey("visible_food")] = foodDict;
        }
        SetInt(perception, "visible_food_count", data.visibleFoodCount);

        // ----- ROBOT-SPECIFIC DATA -----

        // Spawn position (for returning home)
        SetFloat(perception, "spawn_x", data.mySpawn.x); 
        SetFloat(perception, "spawn_z", data.mySpawn.z); 

        // Carrying state
        SetInt(perception, "is_carrying", HasItemAttached() ? 1 : 0);

        // Current target (for reservation system)
        SetString(perception, "current_target_id", currentTargetID);

        // ----- ITEMS (for robots) -----
        using (PyList itemList = new PyList())
        {
            Item[] allItems = Item.GetAllItems();  // Uses your Item registry
            foreach (Item item in allItems)
            {
                if (item.IsBeingCarried) continue;  // Skip items already picked up

                using (PyString itemKey = new PyString(item.InstanceID))
                using (PyDict itemData = new PyDict())
                {
                    SetFloat(itemData, "x", item.Position.x);
                    SetFloat(itemData, "z", item.Position.z);
                    SetString(itemData, "id", item.InstanceID);
                    itemList.Append(itemData);
                }
            }
            perception[GetCachedKey("items")] = itemList;
        }

        // ----- DEPOSITS (for robots) -----
        using (PyList depositList = new PyList())
        {
            DepositZone[] allDeposits = DepositZone.GetAllDeposits();
            foreach (DepositZone deposit in allDeposits)
            {
                using (PyDict depositData = new PyDict())
                {
                    SetFloat(depositData, "x", deposit.Position.x);
                    SetFloat(depositData, "z", deposit.Position.z);
                    SetString(depositData, "id", deposit.InstanceID);
                    depositList.Append(depositData);
                }
            }
            perception[GetCachedKey("deposites")] = depositList;  // Note: matches robot.py spelling
        }

        // ----- OBSTACLES (for robots) -----
        using (PyList obstacleList = new PyList())
        {
            Obstacle[] allObstacles = Obstacle.GetAllObstacles();
            foreach (Obstacle obs in allObstacles)
            {
                using (PyDict obsData = new PyDict())
                {
                    SetFloat(obsData, "x", obs.Position.x);
                    SetFloat(obsData, "z", obs.Position.z);
                    SetFloat(obsData, "radius", obs.AvoidanceRadius);
                    obstacleList.Append(obsData);
                }
            }
            perception[GetCachedKey("obstacles")] = obstacleList;
        }

        return perception;
    }

    /// <summary>
    /// Parses batch results from Python and executes decisions for each agent.
    /// </summary>
    private void DistributeDecisions(PyObject batchResults)
    {
        PyDict resultsDict = new PyDict(batchResults);

        foreach (var kvp in registeredAgents)
        {
            string agentID = kvp.Key;
            PythonBehaviorController controller = kvp.Value;

            if (controller == null || controller.actionManager == null) continue;

            // Skip dead agents - they don't need decisions
            if (controller.baseAgent.CurrentState == BaseAgent.AgentState.Dead) continue;

            using (PyString agentKey = new PyString(agentID))
            {
                if (!resultsDict.HasKey(agentKey)) continue;

                using (PyObject agentDecision = resultsDict[agentKey])
                {
                    AgentDecisionData decision = ParseDecisionResponse(agentDecision);
                    Debug.Log($"[Agent {agentID}] Action: {decision.action.actionType} | Move: {decision.movement.movementType} | Target: {decision.action.targetID}");

                    controller.currentTargetID = decision.action.targetID ?? "0";
                    ExecuteDecision(controller.actionManager, decision);
                }
            }
        }
    }

    /// <summary>
    /// Parses a single agent's decision from Python dict.
    /// </summary>
    private AgentDecisionData ParseDecisionResponse(PyObject response)
    {
        AgentDecisionData decision = new AgentDecisionData
        {
            movement = new MovementDecision
            {
                movementType = "none",
                targetX = 0f,
                targetZ = 0f
            },
            action = new ActionDecision
            {
                actionType = "none",
                targetID = "",
                parameters = new Dictionary<string, object>()
            }
        };

        Console.WriteLine(decision.movement.ToString());

        try
        {
            PyDict responseDict = new PyDict(response);

            // Parse movement
            if (responseDict.HasKey("movement"))
            {
                using (PyDict movementDict = new PyDict(responseDict["movement"]))
                {
                    if (movementDict.HasKey("type"))
                        decision.movement.movementType = movementDict["type"].As<string>().ToLower();
                    if (movementDict.HasKey("target_x"))
                        decision.movement.targetX = movementDict["target_x"].As<float>();
                    if (movementDict.HasKey("target_z"))
                        decision.movement.targetZ = movementDict["target_z"].As<float>();
                }
            }

            // Parse action
            if (responseDict.HasKey("action"))
            {
                using (PyDict actionDict = new PyDict(responseDict["action"]))
                {
                    if (actionDict.HasKey("type"))
                        decision.action.actionType = actionDict["type"].As<string>().ToLower();
                    if (actionDict.HasKey("target_id"))
                        decision.action.targetID = actionDict["target_id"].As<string>();
                    if (actionDict.HasKey("parameters"))
                    {
                        using (PyDict paramsDict = new PyDict(actionDict["parameters"]))
                        {
                            foreach (PyObject key in paramsDict.Keys())
                            {
                                string keyStr = key.As<string>();
                                decision.action.parameters[keyStr] =
                                    paramsDict[key].AsManagedObject(typeof(object));
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to parse decision: {e.Message}");
        }

        return decision;
    }

    /// <summary>
    /// Executes a parsed decision through the agent's action manager.
    /// </summary>
    private void ExecuteDecision(AgentActionManager actionManager, AgentDecisionData decision)
    {
        // Execute movement
        ExecuteMovement(actionManager, decision.movement);

        // Execute action
        ExecuteAction(actionManager, decision.action);
    }

    private void ExecuteMovement(AgentActionManager actionManager, MovementDecision movement)
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
                // No movement command - continue current behavior
                break;
        }
    }

    private void ExecuteAction(AgentActionManager actionManager, ActionDecision action)
    {
        switch (action.actionType)
        {
            case "none":
                break;

            // Combat
            case "attack":
                actionManager.Attack(action.targetID);
                break;
            case "claw":
                actionManager.Claw(action.targetID);
                break;
            case "bite":
                actionManager.Bite(action.targetID);
                break;

            // Infection spread
            case "sneeze":
                actionManager.Sneeze();
                break;
            case "cough":
                actionManager.Cough();
                break;

            // Special
            case "kill":
                actionManager.Kill(action.targetID);
                break;
            case "quarantine":
                actionManager.Quarantine();
                break;

            case "avoid":
                ExecuteAvoidAction(actionManager, action);
                break;

            case "modify_health":
                if (action.parameters.TryGetValue("amount", out object amountObj))
                {
                    actionManager.ModifyHealth(Convert.ToSingle(amountObj));
                }
                break;

            // Hunger
            case "eat":
                actionManager.Eat(action.targetID);
                break;

            default:
                Debug.LogWarning($"Unknown action type: {action.actionType}");
                break;

            // Robot
            case "pick_up":
                ExecutePickUp(actionManager);
                break;

            case "drop_off":
                ExecuteDropOff(actionManager);
                break;
        }
    }

    private void ExecuteAvoidAction(AgentActionManager actionManager, ActionDecision action)
    {
        bool hasTarget1 = action.parameters.TryGetValue("target_id_1", out object target1Obj);
        bool hasTarget2 = action.parameters.TryGetValue("target_id_2", out object target2Obj);

        if (hasTarget1 && hasTarget2)
        {
            actionManager.Avoid(target1Obj.ToString(), target2Obj.ToString());
        }
        else if (hasTarget1)
        {
            actionManager.Avoid(target1Obj.ToString());
        }
        else if (!string.IsNullOrEmpty(action.targetID))
        {
            actionManager.Avoid(action.targetID);
        }
    }
    #endregion

    #region Public Utility
    /// <summary>
    /// Returns the number of agents currently registered for batch processing.
    /// Useful for debugging.
    /// </summary>
    public static int GetRegisteredAgentCount() => registeredAgents.Count;

    /// <summary>
    /// Returns whether this instance is the batch master.
    /// </summary>
    public bool IsBatchMaster => batchMaster == this;
    #endregion

    #region Robot Item Handling

    /// <summary>
    /// Checks if this robot has an item attached as a child object.
    /// </summary>
    private bool HasItemAttached()
    {
        return GetAttachedItem() != null;
    }

    /// <summary>
    /// Gets the Transform of the item being carried (if any).
    /// </summary>
    private Transform GetAttachedItem()
    {
        foreach (Transform child in transform)
        {
            if (child.CompareTag("Item")) return child;
        }
        return null;
    }

    /// <summary>
    /// Finds the nearest GameObject with a specific tag within radius.
    /// </summary>
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

    private void ExecutePickUp(AgentActionManager actionManager)
    {
        if (HasItemAttached()) return; // Already carrying

        GameObject nearestItem = FindNearestWithTag("Item", 1.5f);
        if (nearestItem != null)
        {
            // Attach item to robot
            nearestItem.transform.SetParent(this.transform);
            nearestItem.transform.localPosition = new Vector3(0, 0.5f, 0); // On top of robot
            Debug.Log($"{baseAgent.InstanceID} picked up item");
        }
    }

    private void ExecuteDropOff(AgentActionManager actionManager)
    {
        Transform carriedItem = GetAttachedItem();
        if (carriedItem == null) return; // Not carrying anything

        // Detach and place on ground
        carriedItem.SetParent(null);
        carriedItem.position = new Vector3(
            carriedItem.position.x,
            0f,  // Ground level
            carriedItem.position.z
        );

        // Optional: Change tag so it's no longer pickable
        carriedItem.gameObject.tag = "Untagged";

        Debug.Log($"{baseAgent.InstanceID} dropped off item");
    }



    #endregion
}