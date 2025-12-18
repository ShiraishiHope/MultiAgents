"""
Predator / Prey behavior
Compatible with Unity PythonBehaviorController API
"""

import random
import math

# =========================
# ===== PARAMETERS =======
# =========================

PREDATOR_FACTION = "Predator"
PREY_FACTION = "Prey"

# Vision
PREDATOR_VISION_RADIUS = 10.0
PREY_VISION_RADIUS = 8.0

# Movement
WANDER_RADIUS = 10.0
FLEE_DISTANCE = 12.0
CAPTURE_DISTANCE = 1.8

# Energy
PREDATOR_INITIAL_ENERGY = 100.0
ENERGY_DECAY_PER_DECISION = 1.5
ENERGY_GAIN_ON_EAT = 40.0

# =========================
# ===== GLOBAL STATE ======
# =========================

predator_energy = {}      # {predator_id: energy}
dead_agents = set()       # agents that should no longer act
wander_targets = {}       # {agent_id: (x, z)}

# =========================
# ===== ENTRY POINT ======
# =========================

def decide_action(perception):
    my_id = perception["my_id"]
    my_x = perception["my_x"]
    my_z = perception["my_z"]
    my_faction = perception["my_faction"]
    visible_agents = perception["visible_agents"]

    # Dead agents do nothing
    if my_id in dead_agents:
        return stop_response()

    # -------------------------
    # Prey behavior
    if my_faction == PREY_FACTION:
        predator = find_closest_agent(my_x, my_z, visible_agents, PREY_VISION_RADIUS, PREDATOR_FACTION)
        if predator:
            flee_x, flee_z = flee_from(my_x, my_z, predator["x"], predator["z"])
            return move_response(flee_x, flee_z, "run")
        target = get_wander_target(my_id, my_x, my_z)
        return move_response(target[0], target[1], "walk")

    # -------------------------
    # Predator behavior
    if my_faction == PREDATOR_FACTION:
        if my_id not in predator_energy:
            predator_energy[my_id] = PREDATOR_INITIAL_ENERGY

        predator_energy[my_id] -= ENERGY_DECAY_PER_DECISION
        if predator_energy[my_id] <= 0:
            dead_agents.add(my_id)
            return stop_response()

        prey = find_closest_agent(my_x, my_z, visible_agents, PREDATOR_VISION_RADIUS, PREY_FACTION)
        if prey:
            dist = distance(my_x, my_z, prey["x"], prey["z"])
            if dist <= CAPTURE_DISTANCE:
                predator_energy[my_id] += ENERGY_GAIN_ON_EAT
                dead_agents.add(prey["agent_id"])  # The prey is dead
                return move_response(my_x, my_z, "walk")  # continue walking until finding new prey target
            else:
                return move_response(prey["x"], prey["z"], "run")

        # If no prey target, wander
        target = get_wander_target(my_id, my_x, my_z)
        return move_response(target[0], target[1], "walk")

    # Unknown faction
    return stop_response()

# =========================
# ===== HELPERS ==========
# =========================

def find_closest_agent(my_x, my_z, agents, max_radius, target_faction):
    closest = None
    best_dist = max_radius
    for agent_id, pos in agents.items():
        if agent_id in dead_agents:  # ignore dead agents
            continue
        if pos.get("faction") != target_faction:
            continue
        d = distance(my_x, my_z, pos["x"], pos["z"])
        if d < best_dist:
            best_dist = d
            closest = {
                "agent_id": agent_id,
                "x": pos["x"],
                "z": pos["z"]
            }
    return closest

def flee_from(my_x, my_z, threat_x, threat_z):
    dx = my_x - threat_x
    dz = my_z - threat_z
    length = math.sqrt(dx*dx + dz*dz) or 0.01
    dx /= length
    dz /= length
    return (my_x + dx*FLEE_DISTANCE, my_z + dz*FLEE_DISTANCE)

def get_wander_target(agent_id, x, z):
    if agent_id not in wander_targets:
        wander_targets[agent_id] = generate_random_target(x, z)
    tx, tz = wander_targets[agent_id]
    if distance(x, z, tx, tz) < 1.0:
        wander_targets[agent_id] = generate_random_target(x, z)
    return wander_targets[agent_id]

def generate_random_target(x, z):
    return (x + random.uniform(-WANDER_RADIUS, WANDER_RADIUS),
            z + random.uniform(-WANDER_RADIUS, WANDER_RADIUS))

def distance(x1, z1, x2, z2):
    dx = x2 - x1
    dz = z2 - z1
    return math.sqrt(dx*dx + dz*dz)

# =========================
# ===== RESPONSE FORMATS ===
# =========================

def move_response(x, z, movement_type):
    return {
        "movement": {"type": movement_type, "target_x": x, "target_z": z},
        "action": {"type": "none", "target_id": "", "parameters": {}}
    }

def stop_response():
    return {
        "movement": {"type": "stop", "target_x": 0.0, "target_z": 0.0},
        "action": {"type": "none", "target_id": "", "parameters": {}}
    }

def kill_response(target_id):
    return {
        "movement": {"type": "stop", "target_x": 0.0, "target_z": 0.0},
        "action": {"type": "kill", "target_id": target_id, "parameters": {}}
    }
