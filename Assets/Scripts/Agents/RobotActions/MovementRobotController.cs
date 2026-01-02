using UnityEngine;

public class MovementRobotController : MonoBehaviour
{
    public float baseSpeed = 5.0f;    // Vitesse normale
    public float turnSpeed = 100.0f;

    private RobotAgent robotStats;    // Référence vers l'autre script

    void Start()
    {
        // On récupère le script RobotAgent qui est sur le même robot
        robotStats = GetComponent<RobotAgent>();
    }

    void Update()
    {
        if (robotStats == null) return;

        // --- GESTION DE LA VITESSE SELON LA BATTERIE ---
        float currentSpeed = baseSpeed;

        // Si la batterie est basse (ex: < 20%), on divise la vitesse par 3
        if (robotStats.Battery < 20f)
        {
            currentSpeed = baseSpeed / 3f;
        }

        // Si la batterie est vide ou le robot en erreur, on ne bouge plus
        if (robotStats.Battery <= 0 || robotStats.CurrentStatus == RobotAgent.RobotStatus.CriticalError)
        {
            currentSpeed = 0;
        }

        // --- MOUVEMENT CLASSIQUE ---
        float move = Input.GetAxis("Vertical") * currentSpeed * Time.deltaTime;
        float rotation = Input.GetAxis("Horizontal") * turnSpeed * Time.deltaTime;

        transform.Translate(0, 0, move);
        transform.Rotate(0, rotation, 0);
    }
}