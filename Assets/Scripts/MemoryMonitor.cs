using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Monitors memory usage on both C# and Python sides.
/// Displays real-time stats and detects potential memory leaks.
/// 
/// Attach to any GameObject in the scene (or create empty "MemoryMonitor" object).
/// </summary>
public class MemoryMonitor : MonoBehaviour
{
    #region Settings

    [Header("Monitoring Settings")]
    [SerializeField] private float sampleInterval = 2f;      // How often to sample (seconds)
    [SerializeField] private int historySampleCount = 30;    // How many samples to keep for trend analysis
    [SerializeField] private bool showOnScreen = true;       // Display GUI overlay
    [SerializeField] private bool logToConsole = false;      // Log to Unity console
    [SerializeField] private bool trackPythonMemory = true;  // Enable Python tracking

    [Header("Leak Detection")]
    [SerializeField] private float leakThresholdMB = 50f;    // Warn if memory grows by this much
    [SerializeField] private int samplesForTrend = 10;       // Samples to analyze for trend

    [Header("Display Settings")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F3; // Press to toggle display
    private bool firstUpdate = true;
    #endregion

    #region Memory Data Structures

    /// <summary>
    /// Snapshot of memory state at a point in time.
    /// </summary>
    public struct MemorySample
    {
        public float timestamp;

        // C# / Unity memory (bytes)
        public long managedHeap;        // GC.GetTotalMemory
        public long totalProcessMemory; // Working set
        public long unityAllocated;     // Unity's native allocations

        // Python memory (bytes)
        public int pythonObjectCount;   // gc.get_objects() count
        public long pythonTracedMemory; // tracemalloc current
        public long pythonPeakMemory;   // tracemalloc peak
        public int pythonGarbageCount;  // Uncollectable objects
        
    }

    #endregion

    #region Private State

    private List<MemorySample> sampleHistory = new List<MemorySample>();
    private MemorySample currentSample;
    private MemorySample baselineSample;
    private float nextSampleTime;
    private bool pythonMonitorInitialized = false;
    private PyObject pythonMonitorModule;
    private bool isDisplayVisible = true;

    // Trend detection
    private float memoryTrendMBPerMinute = 0f;
    private bool leakWarningActive = false;

    // Process handle for memory reading
    private Process currentProcess;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        currentProcess = Process.GetCurrentProcess();



        

        // Take baseline sample
        TakeSample();
        baselineSample = currentSample;

        nextSampleTime = Time.time + sampleInterval;

        Debug.Log("[MemoryMonitor] Started. Press " + toggleKey + " to toggle display.");
    }

    void Update()
    {

        if (firstUpdate)
        {
            firstUpdate = false;

            if (trackPythonMemory)
            {
                if (PythonBehaviorController.IsPythonReady)
                {
                    InitializePythonMonitor();
                }
                else
                {
                    Debug.LogWarning("[MemoryMonitor] Python not initialized, disabling Python tracking.");
                    trackPythonMemory = false;
                }
            }
        }

        // Toggle display
        if (Input.GetKeyDown(toggleKey))
        {
            isDisplayVisible = !isDisplayVisible;
        }

        // Take periodic samples
        if (Time.time >= nextSampleTime)
        {
            TakeSample();
            AnalyzeTrend();

            if (logToConsole)
            {
                LogCurrentStats();
            }

            nextSampleTime = Time.time + sampleInterval;
        }
    }

    void OnDestroy()
    {
        pythonMonitorModule?.Dispose();
        pythonMonitorModule = null;
        currentProcess?.Dispose();
    }

    #endregion

    #region Python Integration

    private void InitializePythonMonitor()
    {
        try
        {
            using (Py.GIL())
            {
                // Import our memory monitor module
                pythonMonitorModule = Py.Import("memory_monitor");

                // Start tracemalloc tracking
                using (PyObject startFunc = pythonMonitorModule.GetAttr("start_tracking"))
                using (PyObject result = startFunc.Invoke())
                {
                    bool started = result.As<bool>();
                    if (started)
                    {
                        Debug.Log("[MemoryMonitor] Python tracemalloc tracking started.");
                    }
                    else
                    {
                        Debug.LogWarning("[MemoryMonitor] Python tracemalloc not available.");
                    }
                }

                pythonMonitorInitialized = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MemoryMonitor] Failed to initialize Python monitor: {e.Message}");
            Debug.LogWarning("Make sure memory_monitor.py is in Assets/Scripts/Student/");
            pythonMonitorInitialized = false;
        }
    }

    private void GetPythonMemoryStats(ref MemorySample sample)
    {
        if (!pythonMonitorInitialized || pythonMonitorModule == null)
        {
            sample.pythonObjectCount = -1;
            sample.pythonTracedMemory = -1;
            sample.pythonPeakMemory = -1;
            sample.pythonGarbageCount = -1;
            return;
        }

        try
        {
            using (Py.GIL())
            {
                // Call get_memory_stats()
                using (PyObject getStatsFunc = pythonMonitorModule.GetAttr("get_memory_stats"))
                using (PyObject statsObj = getStatsFunc.Invoke())
                using (PyDict stats = new PyDict(statsObj))
                {
                    using (PyObject gcObjCount = stats["gc_objects"])
                    {
                        sample.pythonObjectCount = gcObjCount.As<int>();
                    }

                    using (PyObject gcGarbage = stats["gc_garbage"])
                    {
                        sample.pythonGarbageCount = gcGarbage.As<int>();
                    }

                    using (PyObject traceCurrent = stats["tracemalloc_current"])
                    {
                        sample.pythonTracedMemory = traceCurrent.As<long>();
                    }

                    using (PyObject tracePeak = stats["tracemalloc_peak"])
                    {
                        sample.pythonPeakMemory = tracePeak.As<long>();
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MemoryMonitor] Error getting Python stats: {e.Message}");
            sample.pythonObjectCount = -1;
            sample.pythonTracedMemory = -1;
            sample.pythonPeakMemory = -1;
            sample.pythonGarbageCount = -1;
        }
    }

    /// <summary>
    /// Force Python garbage collection. Call to get accurate readings.
    /// </summary>
    public void ForcePythonGC()
    {
        if (!pythonMonitorInitialized || pythonMonitorModule == null) return;

        try
        {
            using (Py.GIL())
            {
                using (PyObject gcFunc = pythonMonitorModule.GetAttr("force_gc"))
                using (PyObject result = gcFunc.Invoke())
                {
                    int collected = result.As<int>();
                    Debug.Log($"[MemoryMonitor] Python GC collected {collected} objects.");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MemoryMonitor] Error forcing Python GC: {e.Message}");
        }
    }

    #endregion

    #region Sampling

    private void TakeSample()
    {
        currentSample = new MemorySample
        {
            timestamp = Time.time
        };

        // C# Memory
        currentSample.managedHeap = GC.GetTotalMemory(false);
        currentSample.totalProcessMemory = currentProcess.WorkingSet64;

        // Unity memory (if available)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        currentSample.unityAllocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
#else
        currentSample.unityAllocated = -1;
#endif

        // Python memory
        if (trackPythonMemory)
        {
            GetPythonMemoryStats(ref currentSample);
        }

        // Add to history
        sampleHistory.Add(currentSample);

        // Trim history if too long
        while (sampleHistory.Count > historySampleCount)
        {
            sampleHistory.RemoveAt(0);
        }
    }

    #endregion

    #region Trend Analysis

    private void AnalyzeTrend()
    {
        if (sampleHistory.Count < samplesForTrend) return;

        // Get first and last samples in analysis window
        int startIndex = sampleHistory.Count - samplesForTrend;
        MemorySample oldest = sampleHistory[startIndex];
        MemorySample newest = sampleHistory[sampleHistory.Count - 1];

        // Calculate memory change
        float timeDelta = newest.timestamp - oldest.timestamp;
        if (timeDelta <= 0) return;

        long memoryDelta = newest.totalProcessMemory - oldest.totalProcessMemory;
        float memoryDeltaMB = memoryDelta / (1024f * 1024f);

        // Convert to MB per minute
        memoryTrendMBPerMinute = (memoryDeltaMB / timeDelta) * 60f;

        // Check for leak
        float totalGrowthMB = (newest.totalProcessMemory - baselineSample.totalProcessMemory) / (1024f * 1024f);
        leakWarningActive = totalGrowthMB > leakThresholdMB && memoryTrendMBPerMinute > 1f;

        if (leakWarningActive)
        {
            Debug.LogWarning($"[MemoryMonitor] POTENTIAL LEAK: Memory grew {totalGrowthMB:F1} MB since start. Trend: +{memoryTrendMBPerMinute:F1} MB/min");
        }
    }

    #endregion

    #region Logging

    private void LogCurrentStats()
    {
        string log = $"[Memory] C#: {FormatBytes(currentSample.managedHeap)} managed, " +
                     $"{FormatBytes(currentSample.totalProcessMemory)} total";

        if (currentSample.pythonObjectCount >= 0)
        {
            log += $" | Python: {currentSample.pythonObjectCount} objects, " +
                   $"{FormatBytes(currentSample.pythonTracedMemory)} traced";
        }

        log += $" | Trend: {memoryTrendMBPerMinute:+0.0;-0.0;0} MB/min";

        Debug.Log(log);
    }

    #endregion

    #region GUI Display

    void OnGUI()
    {
        if (!showOnScreen || !isDisplayVisible) return;

        // Background box
        float boxWidth = 320f;
        float boxHeight = trackPythonMemory ? 220f : 140f;

        if (leakWarningActive)
        {
            boxHeight += 25f;
        }

        Rect boxRect = new Rect(10, 10, boxWidth, boxHeight);
        GUI.Box(boxRect, "");

        // Style for text
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = Color.white }
        };

        GUIStyle headerStyle = new GUIStyle(labelStyle)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 14
        };

        GUIStyle warningStyle = new GUIStyle(labelStyle)
        {
            normal = { textColor = Color.red },
            fontStyle = FontStyle.Bold
        };

        float y = 15f;
        float x = 15f;
        float lineHeight = 18f;

        // Header
        GUI.Label(new Rect(x, y, boxWidth, 20), "Memory Monitor (F3 to toggle)", headerStyle);
        y += lineHeight + 5;

        // Leak warning
        if (leakWarningActive)
        {
            GUI.Label(new Rect(x, y, boxWidth, 20), "⚠ POTENTIAL MEMORY LEAK DETECTED", warningStyle);
            y += lineHeight + 5;
        }

        // C# Section
        GUI.Label(new Rect(x, y, boxWidth, 20), "── C# / Unity ──", headerStyle);
        y += lineHeight;

        GUI.Label(new Rect(x, y, boxWidth, 20),
            $"Managed Heap: {FormatBytes(currentSample.managedHeap)}", labelStyle);
        y += lineHeight;

        GUI.Label(new Rect(x, y, boxWidth, 20),
            $"Process Total: {FormatBytes(currentSample.totalProcessMemory)}", labelStyle);
        y += lineHeight;

        if (currentSample.unityAllocated >= 0)
        {
            GUI.Label(new Rect(x, y, boxWidth, 20),
                $"Unity Allocated: {FormatBytes(currentSample.unityAllocated)}", labelStyle);
            y += lineHeight;
        }

        // Python Section
        if (trackPythonMemory && currentSample.pythonObjectCount >= 0)
        {
            y += 5;
            GUI.Label(new Rect(x, y, boxWidth, 20), "── Python ──", headerStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, boxWidth, 20),
                $"GC Objects: {currentSample.pythonObjectCount:N0}", labelStyle);
            y += lineHeight;

            if (currentSample.pythonTracedMemory >= 0)
            {
                GUI.Label(new Rect(x, y, boxWidth, 20),
                    $"Traced Memory: {FormatBytes(currentSample.pythonTracedMemory)}", labelStyle);
                y += lineHeight;

                GUI.Label(new Rect(x, y, boxWidth, 20),
                    $"Peak Memory: {FormatBytes(currentSample.pythonPeakMemory)}", labelStyle);
                y += lineHeight;
            }

            if (currentSample.pythonGarbageCount > 0)
            {
                GUI.Label(new Rect(x, y, boxWidth, 20),
                    $"Uncollectable: {currentSample.pythonGarbageCount} (circular refs!)", warningStyle);
                y += lineHeight;
            }
        }

        // Trend
        y += 5;
        GUI.Label(new Rect(x, y, boxWidth, 20), "── Trend ──", headerStyle);
        y += lineHeight;

        Color trendColor = memoryTrendMBPerMinute > 5f ? Color.red :
                          memoryTrendMBPerMinute > 1f ? Color.yellow : Color.green;
        GUIStyle trendStyle = new GUIStyle(labelStyle) { normal = { textColor = trendColor } };

        string trendText = memoryTrendMBPerMinute >= 0
            ? $"+{memoryTrendMBPerMinute:F1} MB/min"
            : $"{memoryTrendMBPerMinute:F1} MB/min";
        GUI.Label(new Rect(x, y, boxWidth, 20), $"Memory Trend: {trendText}", trendStyle);
        y += lineHeight;

        // Growth since start
        float growthMB = (currentSample.totalProcessMemory - baselineSample.totalProcessMemory) / (1024f * 1024f);
        GUI.Label(new Rect(x, y, boxWidth, 20),
            $"Growth Since Start: {growthMB:+0.0;-0.0;0} MB", labelStyle);
    }

    #endregion

    #region Utility

    private string FormatBytes(long bytes)
    {
        if (bytes < 0) return "N/A";

        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024f:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024f * 1024f):F1} MB";
        return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
    }

    /// <summary>
    /// Reset baseline to current memory state.
    /// Call after initial load stabilizes.
    /// </summary>
    public void ResetBaseline()
    {
        TakeSample();
        baselineSample = currentSample;
        sampleHistory.Clear();
        sampleHistory.Add(currentSample);
        leakWarningActive = false;
        Debug.Log("[MemoryMonitor] Baseline reset.");
    }

    /// <summary>
    /// Force both C# and Python garbage collection.
    /// </summary>
    public void ForceFullGC()
    {
        // C# GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Python GC
        ForcePythonGC();

        // Take new sample
        TakeSample();

        Debug.Log("[MemoryMonitor] Forced full GC on both C# and Python.");
    }

    #endregion
}