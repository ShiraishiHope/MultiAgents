"""
Predator / Prey Behavior - Batch Processing Version
Compatible with Unity PythonBehaviorController batch API

How it works:
- Unity calls decide_all() once per decision cycle with ALL agents' data
- decide_all() calls decide_action() for each agent individually
- Each agent still makes decisions based on its own perception data

Students write their logic in decide_action() - same as before!
"""

import random
import math

# =========================
# ===== PARAMETERS =======
# =========================

PREDATOR_FACTION = "Predator"
PREY_FACTION = "Prey"

# Vision ranges
PREDATOR_VISION_RADIUS = 10.0
PREY_VISION_RADIUS = 8.0

# Movement
WANDER_RADIUS = 10.0
FLEE_DISTANCE = 12.0
CAPTURE_DISTANCE = 1.8

# Predator energy system
PREDATOR_INITIAL_ENERGY = 100.0
ENERGY_DECAY_PER_DECISION = 1.5
ENERGY_GAIN_ON_EAT = 40.0


# =========================
# ===== GLOBAL STATE ======
# =========================
# These persist between decision cycles, tracking state across time

predator_energy = {}      # {predator_id: current_energy}
dead_agents = set()       # agents that have died (stop processing them)
wander_targets = {}       # {agent_id: (target_x, target_z)}


# =========================
# ===== BATCH WRAPPER =====
# =========================

def decide_all(all_perceptions):
    """
    Called by Unity once per decision cycle with ALL agents' perception data.
    
    Args:
        all_perceptions: dict of {agent_id: perception_data}
            Each perception_data contains that agent's unique view of the world:
            - my_id, my_x, my_z: this agent's identity and position
            - my_faction: "Predator" or "Prey"
            - visible_agents: dict of agents THIS agent can see
            - heard_agents: dict of agents THIS agent can hear
            - health, infection status, etc.
    
    Returns:
        dict of {agent_id: decision}
            Each decision contains movement and action for that specific agent
    
    Note: Even though all data comes in one call, each agent's perception
    is still individualized - a predator at position (10,20) sees different
    agents than a prey at position (50,60).
    """
    all_decisions = {}
    
    for agent_id, perception in all_perceptions.items():
        try:
            # Each agent gets its own decision based on its own perception
            decision = decide_action(perception)
            all_decisions[agent_id] = decision
        except Exception as e:
            # On error, return a safe "do nothing" decision
            # This prevents one broken agent from crashing everyone
            print(f"Error processing {agent_id}: {e}")
            all_decisions[agent_id] = stop_response()
    
    return all_decisions


# =========================
# ===== AGENT LOGIC =======
# =========================

def decide_action(perception):
    """
    Decision logic for a single agent. Called by decide_all() for each agent.
    
    This is where students write their behavior logic!
    The perception dict contains everything this specific agent knows:
    - Its own position, faction, health
    - What it can see (based on its position and facing direction)
    - What it can hear (based on its position)
    
    Args:
        perception: dict with this agent's sensory data
        
    Returns:
        dict with 'movement' and 'action' keys
    """
    my_id = perception["my_id"]
    my_x = perception["my_x"]
    my_z = perception["my_z"]
    my_faction = perception["my_faction"]
    visible_agents = perception["visible_agents"]

    # Dead agents do nothing
    if my_id in dead_agents:
        return stop_response()

    # -------------------------
    # PREY BEHAVIOR
    # -------------------------
    if my_faction == PREY_FACTION:
        # Look for predators in what THIS prey can see
        predator = find_closest_agent(
            my_x, my_z, 
            visible_agents, 
            PREY_VISION_RADIUS, 
            PREDATOR_FACTION
        )
        
        if predator:
            # Predator spotted! Run away!
            flee_x, flee_z = flee_from(my_x, my_z, predator["x"], predator["z"])
            return move_response(flee_x, flee_z, "run")
        
        # No threat - wander peacefully
        target = get_wander_target(my_id, my_x, my_z)
        return move_response(target[0], target[1], "walk")

    # -------------------------
    # PREDATOR BEHAVIOR
    # -------------------------
    if my_faction == PREDATOR_FACTION:
        # Initialize energy for new predators
        if my_id not in predator_energy:
            predator_energy[my_id] = PREDATOR_INITIAL_ENERGY

        # Lose energy over time (hunger)
        predator_energy[my_id] -= ENERGY_DECAY_PER_DECISION
        
        # Starved to death
        if predator_energy[my_id] <= 0:
            dead_agents.add(my_id)
            return stop_response()

        # Hunt for prey in what THIS predator can see
        prey = find_closest_agent(
            my_x, my_z, 
            visible_agents, 
            PREDATOR_VISION_RADIUS, 
            PREY_FACTION
        )
        
        if prey:
            dist = distance(my_x, my_z, prey["x"], prey["z"])
            
            if dist <= CAPTURE_DISTANCE:
                # Close enough to kill!
                predator_energy[my_id] += ENERGY_GAIN_ON_EAT
                dead_agents.add(prey["agent_id"])  # Mark prey as dead
                return kill_response(prey["agent_id"])
            else:
                # Chase the prey
                return move_response(prey["x"], prey["z"], "run")

        # No prey visible - wander and search
        target = get_wander_target(my_id, my_x, my_z)
        return move_response(target[0], target[1], "walk")

    # Unknown faction - do nothing
    return stop_response()


# =========================
# ===== HELPER FUNCTIONS ==
# =========================

def find_closest_agent(my_x, my_z, visible_agents, max_radius, target_faction):
    """
    Find the closest agent of a specific faction within range.
    
    Args:
        my_x, my_z: this agent's position
        visible_agents: dict of agents this agent can see
        max_radius: maximum distance to consider
        target_faction: faction to look for ("Predator" or "Prey")
    
    Returns:
        dict with agent_id, x, z of closest matching agent, or None
    """
    closest = None
    best_dist = max_radius
    
    for agent_id, agent_data in visible_agents.items():
        # Skip dead agents
        if agent_id in dead_agents:
            continue
        
        # Skip wrong faction
        if agent_data.get("faction") != target_faction:
            continue
        
        # Check distance
        d = distance(my_x, my_z, agent_data["x"], agent_data["z"])
        if d < best_dist:
            best_dist = d
            closest = {
                "agent_id": agent_id,
                "x": agent_data["x"],
                "z": agent_data["z"]
            }
    
    return closest


def flee_from(my_x, my_z, threat_x, threat_z):
    """
    Calculate a position that flees away from a threat.
    
    Returns:
        (flee_x, flee_z) - position FLEE_DISTANCE units away from threat
    """
    # Direction vector from threat to me
    dx = my_x - threat_x
    dz = my_z - threat_z
    
    # Normalize (avoid division by zero)
    length = math.sqrt(dx * dx + dz * dz) or 0.01
    dx /= length
    dz /= length
    
    # Move FLEE_DISTANCE in that direction
    return (my_x + dx * FLEE_DISTANCE, my_z + dz * FLEE_DISTANCE)


def get_wander_target(agent_id, current_x, current_z):
    """
    Get or generate a wander target for an agent.
    Generates new target when agent reaches current one.
    
    Returns:
        (target_x, target_z)
    """
    # Generate initial target if needed
    if agent_id not in wander_targets:
        wander_targets[agent_id] = generate_random_target(current_x, current_z)
    
    # Check if we've reached the target
    tx, tz = wander_targets[agent_id]
    if distance(current_x, current_z, tx, tz) < 1.0:
        # Pick a new target
        wander_targets[agent_id] = generate_random_target(current_x, current_z)
    
    return wander_targets[agent_id]


def generate_random_target(x, z):
    """
    Generate a random point within WANDER_RADIUS of current position.
    """
    return (
        x + random.uniform(-WANDER_RADIUS, WANDER_RADIUS),
        z + random.uniform(-WANDER_RADIUS, WANDER_RADIUS)
    )


def distance(x1, z1, x2, z2):
    """
    Euclidean distance between two points.
    """
    dx = x2 - x1
    dz = z2 - z1
    return math.sqrt(dx * dx + dz * dz)


# =========================
# ===== RESPONSE BUILDERS =
# =========================

def move_response(target_x, target_z, movement_type):
    """
    Build a response that moves the agent.
    
    Args:
        target_x, target_z: destination coordinates
        movement_type: "walk" or "run"
    """
    return {
        "movement": {
            "type": movement_type,
            "target_x": target_x,
            "target_z": target_z
        },
        "action": {
            "type": "none",
            "target_id": "",
            "parameters": {}
        }
    }


def stop_response():
    """
    Build a response that stops the agent (do nothing).
    """
    return {
        "movement": {
            "type": "stop",
            "target_x": 0.0,
            "target_z": 0.0
        },
        "action": {
            "type": "none",
            "target_id": "",
            "parameters": {}
        }
    }


def kill_response(target_id):
    """
    Build a response that kills a target agent.
    
    Args:
        target_id: the instance ID of the agent to kill
    """
    return {
        "movement": {
            "type": "stop",
            "target_x": 0.0,
            "target_z": 0.0
        },
        "action": {
            "type": "kill",
            "target_id": target_id,
            "parameters": {}
        }
    }