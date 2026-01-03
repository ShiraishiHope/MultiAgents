"""
Hunger Behavior - Agents wander randomly and seek food when hungry
"""

import random
import math

# =========================
# ===== CONFIGURATION =====
# =========================

HUNGER_THRESHOLD = 50.0
EAT_RANGE = 1.5
WANDER_RADIUS = 10.0
DECISION_INTERVAL = 0.5

# =========================
# ===== GLOBAL STATE ======
# =========================

wander_targets = {}
behavior_timers = {}


# =========================
# ===== BATCH ENTRY =======
# =========================

def decide_all(all_perceptions):
    all_decisions = {}
    
    for agent_id, perception in all_perceptions.items():
        try:
            decision = decide_action(perception)
            all_decisions[agent_id] = decision
        except Exception as e:
            print(f"Error processing {agent_id}: {e}")
            all_decisions[agent_id] = build_response(0, 0, "stop", "none", "")
    
    return all_decisions


# =========================
# ===== AGENT LOGIC =======
# =========================

def decide_action(perception):
    my_id = perception["my_id"]
    my_x = perception["my_x"]
    my_z = perception["my_z"]
    hunger = perception["hunger"]
    visible_food = perception["visible_food"]
    my_state = perception["my_state"]
    
    if my_state == "Dead":
        return build_response(0, 0, "stop", "none", "")
    
    # Hungry - look for food
    if hunger < HUNGER_THRESHOLD:
        if visible_food:
            closest_food = find_closest_food(my_x, my_z, visible_food)
            
            if closest_food:
                dist = closest_food["distance"]
                
                if dist <= EAT_RANGE:
                    return build_response(my_x, my_z, "stop", "eat", closest_food["id"])
                else:
                    return build_response(closest_food["x"], closest_food["z"], "run", "none", "")
    
    # Not hungry or no food visible - random behavior
    return random_behavior(my_id, my_x, my_z)


def find_closest_food(my_x, my_z, visible_food):
    closest = None
    best_dist = float('inf')
    
    for food_id, food_data in visible_food.items():
        dist = food_data["distance"]
        if dist < best_dist:
            best_dist = dist
            closest = {
                "id": food_id,
                "x": food_data["x"],
                "z": food_data["z"],
                "distance": dist
            }
    
    return closest


def random_behavior(agent_id, current_x, current_z):
    if agent_id not in behavior_timers:
        behavior_timers[agent_id] = 0.0
        wander_targets[agent_id] = generate_wander_target(current_x, current_z)
    
    behavior_timers[agent_id] -= DECISION_INTERVAL
    
    if behavior_timers[agent_id] <= 0:
        behavior_timers[agent_id] = random.uniform(2.0, 5.0)
        wander_targets[agent_id] = generate_wander_target(current_x, current_z)
     
    choice = random.random()
    target = wander_targets[agent_id]
    
    if choice < 0.2:
        return build_response(current_x, current_z, "stop", "none", "")
    elif choice < 0.6:
        return build_response(target[0], target[1], "walk", "none", "")
    else:
        return build_response(target[0], target[1], "run", "none", "")


def generate_wander_target(current_x, current_z):
    offset_x = random.uniform(-WANDER_RADIUS, WANDER_RADIUS)
    offset_z = random.uniform(-WANDER_RADIUS, WANDER_RADIUS)
    return (current_x + offset_x, current_z + offset_z)


# =========================
# ===== RESPONSE BUILDER ==
# =========================

def build_response(target_x, target_z, movement_type, action_type, target_id):
    return {
        "movement": {
            "type": movement_type,
            "target_x": target_x,
            "target_z": target_z
        },
        "action": {
            "type": action_type,
            "target_id": target_id,
            "parameters": {}
        }
    }