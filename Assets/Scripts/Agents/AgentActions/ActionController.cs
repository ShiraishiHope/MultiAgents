using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;

/// <summary>
/// Handles all combat and health-related actions for an agent.
/// Actions: Attack, Claw, Bite, Sneeze, Cough, ModifyHealth
/// </summary>
public class ActionController : MonoBehaviour
{
    #region References
    private BaseAgent baseAgent;
    #endregion

    #region Action Constants
    // Range constants - same for all agents
    private const float ATTACK_RANGE = 2f;
    private const float CLAW_RANGE = 1.5f;
    private const float BITE_RANGE = 1f;
    private const float SNEEZE_RADIUS = 3f;
    private const float COUGH_RADIUS = 5f;
    private const float COUGH_ANGLE = 60f;
    private const float EAT_RANGE = 1.5f;
    private const float PICKUP_RANGE = 1.5f;

    // Damage constants
    private const float ATTACK_BASE_DAMAGE = 25f;
    private const float CLAW_BASE_DAMAGE = 10f;
    private const float BITE_BASE_DAMAGE = 30f;

    // Infection constants
    private const float MAX_HEALTH = 100f;
    private const float MINIMUM_INFECTION_CHANCE = 0.05f;
    #endregion

    #region Result Tracking
    private ActionResult lastActionResult;

    /// <summary>
    /// Stores the outcome of the last action performed.
    /// Useful for debugging and potential Python feedback.
    /// </summary>
    public struct ActionResult
    {
        public bool success;
        public float damageDealt;
        public int targetsInfected;
        public string failReason;
    }

    public ActionResult LastActionResult => lastActionResult;
    #endregion

    #region Initialization

    /// <summary>
    /// Called by AgentActionManager to set up references.
    /// </summary>
    public void Initialize(BaseAgent agent)
    {
        baseAgent = agent;
    }
    #endregion

    #region Actions

    public ActionResult Eat(string foodID)
    {
        lastActionResult = new ActionResult();

        // Skip dead agents - they don't need decisions
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead)
        {
            lastActionResult.failReason = "Agent is dead";
            return lastActionResult;
        }

        baseAgent.SetCurrentAction("eat");

        FoodPlate food = FoodPlate.GetFoodByID(foodID);

        if (food == null)
        {
            lastActionResult.failReason = "Food not found";
            return lastActionResult;
        }

        float distance = Vector3.Distance(transform.position, food.transform.position);
        if (distance > EAT_RANGE)
        {
            lastActionResult.failReason = $"Food out of range ({distance:F1} > {EAT_RANGE})";
            return lastActionResult;
        }

        if (food.IsEmpty)
        {
            lastActionResult.failReason = "Food already empty";
            return lastActionResult;
        }

        int countBefore = food.FoodCount;
        bool consumed = food.Consume();

        if (!consumed)
        {
            lastActionResult.failReason = "Failed to consume";
            return lastActionResult;
        }

        float hungerGain = (countBefore == 2) ? 50f : 25f;
        baseAgent.ModifyHunger(hungerGain);

        lastActionResult.success = true;
        Debug.Log($"{baseAgent.InstanceID} ate from {foodID}, gained {hungerGain} hunger");
        return lastActionResult;
    }

    #endregion Actions

    #region Direct Attack Actions

    /// <summary>
    /// Basic attack: 25 × random(1-2) damage. No infection chance.
    /// </summary>
    public ActionResult Attack(string targetID)
    {
        lastActionResult = new ActionResult();

        // Skip dead agents - they don't need decisions
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead)
        {
            lastActionResult.failReason = "Agent is dead";
            return lastActionResult;
        }

        baseAgent.SetCurrentAction("attack");

        BaseAgent target = ValidateTarget(targetID, ATTACK_RANGE);
        if (target == null) return lastActionResult;

        // Damage formula: base × random multiplier between 1 and 2
        float multiplier = Random.Range(1f, 2f);
        float damage = ATTACK_BASE_DAMAGE * multiplier;

        target.TakeDamage(damage);

        lastActionResult.success = true;
        lastActionResult.damageDealt = damage;

        Debug.Log($"{baseAgent.InstanceID} attacked {targetID} for {damage:F1} damage");
        return lastActionResult;
    }

    /// <summary>
    /// Claw attack: 10 × random(1-3) damage. Has chance to infect.
    /// </summary>
    public ActionResult Claw(string targetID)
    {
        lastActionResult = new ActionResult();

        // Skip dead agents - they don't need decisions
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead)
        {
            lastActionResult.failReason = "Agent is dead";
            return lastActionResult;
        }

        baseAgent.SetCurrentAction("claw");

        BaseAgent target = ValidateTarget(targetID, CLAW_RANGE);
        if (target == null) return lastActionResult;

        // Damage formula: base × random multiplier between 1 and 3
        float multiplier = Random.Range(1f, 3f);
        float damage = CLAW_BASE_DAMAGE * multiplier;

        target.TakeDamage(damage);
        lastActionResult.damageDealt = damage;

        // Attempt to infect the target
        if (TryInfectTarget(target))
        {
            lastActionResult.targetsInfected = 1;
        }

        lastActionResult.success = true;

        Debug.Log($"{baseAgent.InstanceID} clawed {targetID} for {damage:F1} damage");
        return lastActionResult;
    }

    /// <summary>
    /// Bite attack: 30 × random(1-3) damage. Has chance to infect.
    /// </summary>
    public ActionResult Bite(string targetID)
    {
        lastActionResult = new ActionResult();

        // Skip dead agents - they don't need decisions
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead)
        {
            lastActionResult.failReason = "Agent is dead";
            return lastActionResult;
        }

        baseAgent.SetCurrentAction("bite");

        BaseAgent target = ValidateTarget(targetID, BITE_RANGE);
        if (target == null) return lastActionResult;

        // Damage formula: base × random multiplier between 1 and 3
        float multiplier = Random.Range(1f, 3f);
        float damage = BITE_BASE_DAMAGE * multiplier;

        target.TakeDamage(damage);
        lastActionResult.damageDealt = damage;

        // Attempt to infect the target
        if (TryInfectTarget(target))
        {
            lastActionResult.targetsInfected = 1;
        }

        lastActionResult.success = true;

        Debug.Log($"{baseAgent.InstanceID} bit {targetID} for {damage:F1} damage");
        return lastActionResult;
    }

    /// <summary>
    /// Instantly kills the target agent. Used by predators.
    /// No damage calculation - immediate death.
    /// </summary>
    public ActionResult Kill(string targetID)
    {
        lastActionResult = new ActionResult();

        // Skip dead agents - they don't need decisions
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead)
        {
            lastActionResult.failReason = "Agent is dead";
            return lastActionResult;
        }

        baseAgent.SetCurrentAction("kill");

        // Look up target - no range check for kill (assume already validated proximity)
        BaseAgent target = BaseAgent.GetAgentByInstanceID(targetID);

        if (target == null)
        {
            lastActionResult.failReason = "Target not found";
            Debug.LogWarning($"{baseAgent.InstanceID} tried to kill {targetID} but target not found");
            return lastActionResult;
        }

        // Instant death
        target.Die();

        lastActionResult.success = true;
        Debug.Log($"{baseAgent.InstanceID} killed {targetID}!");

        return lastActionResult;
    }

    #endregion

    #region Area Effect Actions

    /// <summary>
    /// Sneeze: Attempts to infect all agents within a radius around self.
    /// No damage, pure infection spread. Visible to other agents.
    /// </summary>
    public ActionResult Sneeze()
    {
        lastActionResult = new ActionResult();

        // Skip dead agents - they don't need decisions
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead)
        {
            lastActionResult.failReason = "Agent is dead";
            return lastActionResult;
        }

        baseAgent.SetCurrentAction("sneeze");

        // Must be contagious to spread via sneeze
        if (!baseAgent.IsContagious)
        {
            lastActionResult.failReason = "Not contagious";
            return lastActionResult;
        }

        // Find all agents within sneeze radius
        List<BaseAgent> nearbyAgents = GetAgentsInRadius(SNEEZE_RADIUS);

        int infected = 0;
        foreach (BaseAgent target in nearbyAgents)
        {
            if (TryInfectTarget(target))
            {
                infected++;
            }
        }

        lastActionResult.success = true;
        lastActionResult.targetsInfected = infected;

        Debug.Log($"{baseAgent.InstanceID} sneezed, infected {infected} agents");
        return lastActionResult;
    }

    /// <summary>
    /// Cough: Attempts to infect agents in a cone in front of the agent.
    /// Longer range than sneeze but directional. Visible to other agents.
    /// </summary>
    public ActionResult Cough()
    {
        lastActionResult = new ActionResult();

        // Skip dead agents - they don't need decisions
        if (baseAgent.CurrentState == BaseAgent.AgentState.Dead)
        {
            lastActionResult.failReason = "Agent is dead";
            return lastActionResult;
        }

        baseAgent.SetCurrentAction("cough");

        // Must be contagious to spread via cough
        if (!baseAgent.IsContagious)
        {
            lastActionResult.failReason = "Not contagious";
            return lastActionResult;
        }

        // Find all agents within cone in front of agent
        List<BaseAgent> targetsInCone = GetAgentsInCone(COUGH_RADIUS, COUGH_ANGLE);

        int infected = 0;
        foreach (BaseAgent target in targetsInCone)
        {
            if (TryInfectTarget(target))
            {
                infected++;
            }
        }

        lastActionResult.success = true;
        lastActionResult.targetsInfected = infected;

        Debug.Log($"{baseAgent.InstanceID} coughed, infected {infected} agents");
        return lastActionResult;
    }
    #endregion

    #region Health Modification

    /// <summary>
    /// Modify this agent's health by a specified amount.
    /// Positive values heal, negative values damage.
    /// Called by Python to implement symptom effects, healing, etc.
    /// </summary>
    public void ModifyHealth(float amount)
    {
        // Don't set action for health modification - it's a passive effect
        baseAgent.ModifyHealth(amount);

        if (amount < 0)
        {
            Debug.Log($"{baseAgent.InstanceID} took {-amount:F1} damage from effect");
        }
        else if (amount > 0)
        {
            Debug.Log($"{baseAgent.InstanceID} healed {amount:F1} HP");
        }
    }
    #endregion

    #region Infection Logic

    /// <summary>
    /// Attempts to infect a target based on:
    /// - Attacker's infectivity
    /// - Target's current health (lower = more susceptible)
    /// 
    /// Formula: max(0.05, infectivity × (1 - targetHealth/100))
    /// 
    /// Example: infectivity 0.5, target at 40 health
    /// → max(0.05, 0.5 × (1 - 0.4)) = max(0.05, 0.3) = 30% chance
    /// 
    /// Example: infectivity 0.5, target at 100 health
    /// → max(0.05, 0.5 × 0) = max(0.05, 0) = 5% minimum chance
    /// </summary>
    private bool TryInfectTarget(BaseAgent target)
    {
        // Attacker must be contagious to spread infection
        if (!baseAgent.IsContagious) return false;

        // Target must not already be immune
        if (target.IsImmune) return false;

        // Target must be healthy (not already infected/recovered/dead)
        if (target.CurrentInfectionStage != BaseAgent.InfectionStage.Healthy) return false;

        // Calculate susceptibility: lower health = higher susceptibility
        float healthRatio = target.Health / MAX_HEALTH;
        float susceptibility = 1f - healthRatio;

        // Calculate infection chance with minimum floor
        float infectionChance = baseAgent.Infectivity * susceptibility;
        infectionChance = Mathf.Max(MINIMUM_INFECTION_CHANCE, infectionChance);

        // Roll for infection
        float roll = Random.value;
        if (roll <= infectionChance)
        {
            // Transfer complete disease data from attacker to target
            target.BecomeInfected(
                baseAgent.InfectionName,
                baseAgent.InfectionMortalityRate,
                baseAgent.Symptoms,
                baseAgent.RecoveryRate,
                baseAgent.Infectivity,
                baseAgent.IncubationPeriod,
                baseAgent.ContagiousDuration
            );

            Debug.Log($"{target.InstanceID} infected by {baseAgent.InstanceID} (chance was {infectionChance:P0}, rolled {roll:F2})");
            return true;
        }

        return false;
    }
    #endregion

    #region Helper Methods

    /// <summary>
    /// Validates that target exists and is within range.
    /// Returns null and sets failReason if invalid.
    /// </summary>
    private BaseAgent ValidateTarget(string targetID, float maxRange)
    {
        // Look up target in global registry
        BaseAgent target = BaseAgent.GetAgentByInstanceID(targetID);

        if (target == null)
        {
            lastActionResult.failReason = "Target not found";
            Debug.LogWarning($"{baseAgent.InstanceID} tried to target {targetID} but target not found");
            return null;
        }

        // Check if target is within range
        float distance = Vector3.Distance(transform.position, target.transform.position);
        if (distance > maxRange)
        {
            lastActionResult.failReason = $"Target out of range ({distance:F1} > {maxRange})";
            Debug.LogWarning($"{baseAgent.InstanceID} tried to target {targetID} but out of range");
            return null;
        }

        return target;
    }

    /// <summary>
    /// Returns all agents within a radius of this agent (for Sneeze).
    /// Excludes self.
    /// </summary>
    private List<BaseAgent> GetAgentsInRadius(float radius)
    {
        List<BaseAgent> result = new List<BaseAgent>();
        BaseAgent[] allAgents = BaseAgent.GetAllAgents();

        foreach (BaseAgent agent in allAgents)
        {
            // Skip self
            if (agent == baseAgent) continue;

            float distance = Vector3.Distance(transform.position, agent.transform.position);
            if (distance <= radius)
            {
                result.Add(agent);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns all agents within a cone in front of this agent (for Cough).
    /// Uses same angle logic as VisionController.
    /// </summary>
    private List<BaseAgent> GetAgentsInCone(float radius, float angle)
    {
        List<BaseAgent> result = new List<BaseAgent>();
        BaseAgent[] allAgents = BaseAgent.GetAllAgents();

        foreach (BaseAgent agent in allAgents)
        {
            // Skip self
            if (agent == baseAgent) continue;

            // Calculate direction and distance to target
            Vector3 toTarget = agent.transform.position - transform.position;
            float distance = toTarget.magnitude;

            // Check if within range
            if (distance <= radius)
            {
                // Check if within angle (same as VisionController logic)
                float angleToTarget = Vector3.Angle(toTarget, transform.forward);
                if (angleToTarget <= angle / 2f)
                {
                    result.Add(agent);
                }
            }
        }

        return result;
    }
    #endregion

    #region Robot Actions

    /// <summary>
    /// Tente de ramasser l'item spécifié.
    /// L'item doit être à portée.
    /// </summary>
    public ActionResult PickUp(string itemID)
    {
        lastActionResult = new ActionResult();

        // 2. Recherche de l'objet dans le registre
        Item item = Item.GetItemByID(itemID);
        if (item == null)
        {
            lastActionResult.failReason = "Item not found";
            return lastActionResult;
        }

        // 3. Vérification de la distance
        float distance = Vector3.Distance(transform.position, item.Position);
        if (distance > PICKUP_RANGE)
        {
            lastActionResult.failReason = $"Item out of range ({distance:F1} > {PICKUP_RANGE})";
            return lastActionResult;
        }

        // 4. Vérification si l'objet est déjà porté par quelqu'un d'autre
        if (item.IsBeingCarried)
        {
            lastActionResult.failReason = "Item already being carried";
            return lastActionResult;
        }

        // 5. Exécution du ramassage
        item.OnPickedUp(); // Change l'état interne de l'item

        // Attachement physique (devient enfant du robot)
        item.transform.SetParent(this.transform);
        item.transform.localPosition = new Vector3(0, 0.5f, 0); // Positionné sur le dessus

        baseAgent.SetIsCarrying(true);
        baseAgent.SetCurrentAction("pick_up");
        lastActionResult.success = true;

        Debug.Log($"{baseAgent.InstanceID} picked up {itemID}");
        return lastActionResult;
    }

    /// <summary>
    /// Dépose l'objet actuellement porté au sol ou le livre s'il est dans une zone.
    /// </summary>
    public ActionResult DropOff()
    {
        lastActionResult = new ActionResult();

        // 1. Trouver l'item porté parmi les enfants
        Item carriedItem = GetComponentInChildren<Item>();

        if (carriedItem == null)
        {
            lastActionResult.failReason = "No item being carried";
            return lastActionResult;
        }

        // 2. Détachement physique
        carriedItem.transform.SetParent(null);
        carriedItem.transform.position = new Vector3(transform.position.x, 0f, transform.position.z);

        carriedItem.CompleteDelivery();

        baseAgent.SetCurrentAction("drop_off");
        baseAgent.SetIsCarrying(false);
        lastActionResult.success = true;

        Debug.Log($"{baseAgent.InstanceID} dropped off an item");
        return lastActionResult;
    }

    /// <summary>
    /// MAJ de la target du robot
    /// </summary>
    public ActionResult SetTargetId(string targetId)
    {
        lastActionResult = new ActionResult();

        baseAgent.SetTargetId(targetId);
        lastActionResult.success = true;

        return lastActionResult;
    }

    #endregion
}