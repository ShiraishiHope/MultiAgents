using UnityEngine;

/// <summary>
/// Visualise l'état d'infection d'un agent en changeant sa couleur.
/// Attacher ce script à chaque prefab d'agent.
/// 
/// Couleurs:
/// - VERT: Sain (Healthy)
/// - JAUNE: Exposé/Incubation (Exposed)
/// - ROUGE: Contagieux (Contagious)
/// - BLEU: Récupéré/Immunisé (Recovered)
/// - GRIS: Mort (Dead)
/// </summary>
public class InfectionVisualizer : MonoBehaviour
{
    #region Configuration
    [Header("Couleurs d'état")]
    [SerializeField] private Color healthyColor = Color.green;
    [SerializeField] private Color exposedColor = Color.yellow;
    [SerializeField] private Color contagiousColor = Color.red;
    [SerializeField] private Color recoveredColor = Color.cyan;
    [SerializeField] private Color deadColor = Color.gray;

    [Header("Options de visualisation")]
    [SerializeField] private bool colorizeBody = true;
    [SerializeField] private bool showGlowEffect = true;
    [SerializeField] private float glowIntensity = 2f;
    #endregion

    #region References
    private BaseAgent baseAgent;
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    #endregion

    #region State Tracking
    private BaseAgent.InfectionStage lastStage = BaseAgent.InfectionStage.Healthy;
    #endregion

    void Awake()
    {
        baseAgent = GetComponent<BaseAgent>();
        if (baseAgent == null)
        {
            Debug.LogError($"InfectionVisualizer on {gameObject.name} requires a BaseAgent component!");
            enabled = false;
            return;
        }

        // Récupérer tous les renderers (corps, armes, accessoires, etc.)
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"No renderers found on {gameObject.name}. Cannot visualize infection.");
            enabled = false;
            return;
        }

        // Utiliser MaterialPropertyBlock pour éviter de créer des instances de matériaux
        propertyBlock = new MaterialPropertyBlock();

        // Initialiser la couleur
        UpdateVisualization();
    }

    void Update()
    {
        // Vérifier si l'état a changé
        if (baseAgent.CurrentInfectionStage != lastStage)
        {
            UpdateVisualization();
            lastStage = baseAgent.CurrentInfectionStage;
        }
    }

    /// <summary>
    /// Met à jour la couleur en fonction de l'état d'infection.
    /// </summary>
    private void UpdateVisualization()
    {
        Color targetColor = GetColorForStage(baseAgent.CurrentInfectionStage);
        
        if (colorizeBody)
        {
            ApplyColorToRenderers(targetColor);
        }

        // Log le changement pour debug
        Debug.Log($"{baseAgent.InstanceID} is now {baseAgent.CurrentInfectionStage} (color: {targetColor})");
    }

    /// <summary>
    /// Retourne la couleur correspondant à un état d'infection.
    /// </summary>
    private Color GetColorForStage(BaseAgent.InfectionStage stage)
    {
        switch (stage)
        {
            case BaseAgent.InfectionStage.Healthy:
                return healthyColor;
            
            case BaseAgent.InfectionStage.Exposed:
                return exposedColor;
            
            case BaseAgent.InfectionStage.Contagious:
                return contagiousColor;
            
            case BaseAgent.InfectionStage.Recovered:
                return recoveredColor;
            
            case BaseAgent.InfectionStage.Dead:
                return deadColor;
            
            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Applique la couleur à tous les renderers de l'agent.
    /// </summary>
    private void ApplyColorToRenderers(Color color)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            // Récupérer les propriétés actuelles
            renderer.GetPropertyBlock(propertyBlock);

            // Appliquer la couleur
            if (showGlowEffect)
            {
                // Effet de "glow" en augmentant l'émission
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_EmissionColor", color * glowIntensity);
            }
            else
            {
                // Juste la couleur de base
                propertyBlock.SetColor("_Color", color);
            }

            // Appliquer les changements
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    /// <summary>
    /// Méthode publique pour forcer une mise à jour (utile pour debug).
    /// </summary>
    public void ForceUpdate()
    {
        UpdateVisualization();
    }

    #region Editor Gizmos
    void OnDrawGizmos()
    {
        // Afficher une sphère colorée autour de l'agent dans l'éditeur
        if (baseAgent != null)
        {
            Gizmos.color = GetColorForStage(baseAgent.CurrentInfectionStage);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
    #endregion
}
