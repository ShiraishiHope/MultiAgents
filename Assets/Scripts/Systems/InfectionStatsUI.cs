using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Affiche des statistiques en temps réel sur la propagation de l'épidémie.
/// Crée automatiquement un UI Canvas si nécessaire.
/// </summary>
public class InfectionStatsUI : MonoBehaviour
{
    #region Configuration
    [Header("UI Settings")]
    [SerializeField] private bool autoCreateUI = true;
    [SerializeField] private float updateInterval = 0.5f;
    
    [Header("Display Options")]
    [SerializeField] private bool showHealthyCount = true;
    [SerializeField] private bool showExposedCount = true;
    [SerializeField] private bool showContagiousCount = true;
    [SerializeField] private bool showRecoveredCount = true;
    [SerializeField] private bool showDeadCount = true;
    [SerializeField] private bool showTotalAgents = true;
    [SerializeField] private bool showInfectionRate = true;
    #endregion

    #region UI References
    private TextMeshProUGUI statsText;
    private Canvas canvas;
    #endregion

    #region Timing
    private float nextUpdateTime;
    #endregion

    #region Statistics
    private struct InfectionStats
    {
        public int healthy;
        public int exposed;
        public int contagious;
        public int recovered;
        public int dead;
        public int total;
        public float infectionRate;
    }
    #endregion

    void Start()
    {
        if (autoCreateUI)
        {
            CreateStatsUI();
        }
        else
        {
            // Chercher un TextMeshProUGUI existant
            statsText = GetComponentInChildren<TextMeshProUGUI>();
            if (statsText == null)
            {
                Debug.LogError("No TextMeshProUGUI found and autoCreateUI is disabled!");
                enabled = false;
            }
        }

        nextUpdateTime = Time.time;
    }

    void Update()
    {
        if (Time.time >= nextUpdateTime)
        {
            UpdateStatistics();
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    /// <summary>
    /// Crée automatiquement un Canvas et un TextMeshProUGUI pour afficher les stats.
    /// </summary>
    private void CreateStatsUI()
    {
        // Créer un Canvas
        GameObject canvasObj = new GameObject("InfectionStatsCanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Créer un panneau de fond
        GameObject panelObj = new GameObject("StatsPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(10, -10);
        panelRect.sizeDelta = new Vector2(350, 280);

        UnityEngine.UI.Image panelImage = panelObj.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);

        // Créer le texte
        GameObject textObj = new GameObject("StatsText");
        textObj.transform.SetParent(panelObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        statsText = textObj.AddComponent<TextMeshProUGUI>();
        statsText.fontSize = 18;
        statsText.color = Color.white;
        statsText.alignment = TextAlignmentOptions.TopLeft;
        statsText.margin = new Vector4(10, 10, 10, 10);
        statsText.text = "Initializing...";

        Debug.Log("InfectionStatsUI created successfully!");
    }

    /// <summary>
    /// Collecte et affiche les statistiques de propagation.
    /// </summary>
    private void UpdateStatistics()
    {
        if (statsText == null) return;

        InfectionStats stats = CollectStatistics();
        statsText.text = FormatStatistics(stats);
    }

    /// <summary>
    /// Compte tous les agents par état d'infection.
    /// </summary>
    private InfectionStats CollectStatistics()
    {
        InfectionStats stats = new InfectionStats();
        BaseAgent[] allAgents = BaseAgent.GetAllAgents();
        
        stats.total = allAgents.Length;

        foreach (BaseAgent agent in allAgents)
        {
            switch (agent.CurrentInfectionStage)
            {
                case BaseAgent.InfectionStage.Healthy:
                    stats.healthy++;
                    break;
                case BaseAgent.InfectionStage.Exposed:
                    stats.exposed++;
                    break;
                case BaseAgent.InfectionStage.Contagious:
                    stats.contagious++;
                    break;
                case BaseAgent.InfectionStage.Recovered:
                    stats.recovered++;
                    break;
                case BaseAgent.InfectionStage.Dead:
                    stats.dead++;
                    break;
            }
        }

        // Calculer le taux d'infection (infectés / total)
        int infected = stats.exposed + stats.contagious;
        stats.infectionRate = stats.total > 0 ? (float)infected / stats.total * 100f : 0f;

        return stats;
    }

    /// <summary>
    /// Formate les statistiques en une chaîne lisible avec couleurs.
    /// </summary>
    private string FormatStatistics(InfectionStats stats)
    {
        string output = "<b>=== INFECTION STATISTICS ===</b>\n\n";

        if (showTotalAgents)
            output += $"<b>Total Agents:</b> {stats.total}\n\n";

        if (showHealthyCount)
            output += $"<color=green>\u2022 Healthy:</color> {stats.healthy} ({GetPercentage(stats.healthy, stats.total)})\n";
        
        if (showExposedCount)
            output += $"<color=yellow>\u2022 Exposed:</color> {stats.exposed} ({GetPercentage(stats.exposed, stats.total)})\n";
        
        if (showContagiousCount)
            output += $"<color=red>\u2022 Contagious:</color> {stats.contagious} ({GetPercentage(stats.contagious, stats.total)})\n";
        
        if (showRecoveredCount)
            output += $"<color=#00FFFF>\u2022 Recovered:</color> {stats.recovered} ({GetPercentage(stats.recovered, stats.total)})\n";
        
        if (showDeadCount)
            output += $"<color=#000000>\u2022 Dead:</color> {stats.dead} ({GetPercentage(stats.dead, stats.total)})\n";

        if (showInfectionRate)
            output += $"\n<b>Infection Rate:</b> <color=orange>{stats.infectionRate:F1}%</color>";

        return output;
    }

    /// <summary>
    /// Calcule un pourcentage formaté.
    /// </summary>
    private string GetPercentage(int value, int total)
    {
        if (total == 0) return "0.0%";
        float percentage = (float)value / total * 100f;
        return $"{percentage:F1}%";
    }

    #region Public API
    /// <summary>
    /// Force une mise à jour immédiate des statistiques.
    /// </summary>
    public void ForceUpdate()
    {
        UpdateStatistics();
    }

    /// <summary>
    /// Active/désactive l'affichage des stats.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (canvas != null)
            canvas.enabled = visible;
    }
    #endregion
}
