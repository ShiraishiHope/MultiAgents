"""
Hunger Behavior - Agents seek food when hungry, wander when satisfied

Search Logic:
- 90° FOV means 4 directions cover full 360°
- Agent faces movement direction (handled by MovementController)
- Each search step = look in one 90° sector
- After 4 steps: full area scanned, move elsewhere and repeat
"""

import random
import math

# =========================
# ===== CONFIGURATION =====
# =========================

HUNGER_THRESHOLD = 50.0
WANDER_RADIUS = 10.0
DECISION_INTERVAL = 0.5

# Search pattern settings
SEARCH_STEP_DISTANCE = 2.0   # How far to step in each direction
SEARCH_DIRECTIONS = 4        # 360° ÷ 90° FOV = 4 directions needed
ANGLE_PER_STEP = math.pi / 2 # 90° in radians (π/2)

# =========================
# ===== GLOBAL STATE ======
# =========================

wander_targets = {}
behavior_timers = {}
search_data = {}  # {agent_id: {'center_x', 'center_z', 'step', 'base_angle'}}


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
    
    # Dead agents do nothing
    if my_state == "Dead":
        return build_response(0, 0, "stop", "none", "")
    
    # === HUNGRY: Find and eat food ===
    if hunger < HUNGER_THRESHOLD:
        
        # Clear wander state when switching to hungry mode
        if my_id in wander_targets:
            del wander_targets[my_id]
        if my_id in behavior_timers:
            del behavior_timers[my_id]
        
        if visible_food:
            # Food found! Clear search state and go eat
            if my_id in search_data:
                del search_data[my_id]
            
            closest = find_closest_food(my_x, my_z, visible_food)
            return build_response(closest["x"], closest["z"], "run", "eat", closest["id"])
        
        else:
            # No food visible - search systematically
            target_x, target_z = search_for_food(my_id, my_x, my_z)
            return build_response(target_x, target_z, "walk", "none", "")
    
    # === NOT HUNGRY: Wander randomly ===
    # Clear search state when no longer hungry
    if my_id in search_data:
        del search_data[my_id]
    
    return wander(my_id, my_x, my_z)


# =========================
# ===== FOOD SEARCH =======
# =========================

def find_closest_food(my_x, my_z, visible_food):
    """Find the nearest visible food plate."""
    closest = None
    best_dist = float('inf')
    
    for food_id, food_data in visible_food.items():
        dist = math.hypot(food_data['x'] - my_x, food_data['z'] - my_z)
        if dist < best_dist:
            best_dist = dist
            closest = {"id": food_id, "x": food_data["x"], "z": food_data["z"]}
    
    return closest


def search_for_food(agent_id, my_x, my_z):
    """
    Look in 4 directions (90° apart) to cover full 360°.
    
    How it works:
    1. Pick a random starting angle
    2. Step 0: Move slightly in direction 0° from start
    3. Step 1: Move slightly in direction 90° from start  
    4. Step 2: Move slightly in direction 180° from start
    5. Step 3: Move slightly in direction 270° from start
    6. After 4 steps: Pick new center point and new random angle, repeat
    
    The agent faces wherever it moves (MovementController does this),
    so moving in 4 directions = looking in 4 directions.
    """
    
    # First time searching? Initialize state
    if agent_id not in search_data:
        search_data[agent_id] = {
            'center_x': my_x,
            'center_z': my_z,
            'step': 0,
            'base_angle': random.uniform(0, 2 * math.pi)
        }
    
    data = search_data[agent_id]
    
    # Calculate which direction to look this step
    # step=0 → base_angle, step=1 → base_angle+90°, etc.
    current_angle = data['base_angle'] + (data['step'] * ANGLE_PER_STEP)
    
    # Target point in that direction (agent will face this way)
    target_x = data['center_x'] + math.cos(current_angle) * SEARCH_STEP_DISTANCE
    target_z = data['center_z'] + math.sin(current_angle) * SEARCH_STEP_DISTANCE
    
    # Move to next direction for next cycle
    data['step'] += 1
    
    # Completed all 4 directions? Move to new area
    if data['step'] >= SEARCH_DIRECTIONS:
        data['step'] = 0
        data['center_x'] = my_x
        data['center_z'] = my_z
        data['base_angle'] = random.uniform(0, 2 * math.pi)
    
    return target_x, target_z


# =========================
# ===== WANDERING =========
# =========================

def wander(agent_id, my_x, my_z):
    """Random movement when not hungry."""
    
    # Initialize wander state
    if agent_id not in behavior_timers:
        behavior_timers[agent_id] = 0.0
        wander_targets[agent_id] = random_target(my_x, my_z)
    
    # Count down timer
    behavior_timers[agent_id] -= DECISION_INTERVAL
    
    # Time for new destination?
    if behavior_timers[agent_id] <= 0:
        behavior_timers[agent_id] = random.uniform(2.0, 5.0)
        wander_targets[agent_id] = random_target(my_x, my_z)
    
    target = wander_targets[agent_id]
    
    # Randomly choose behavior: idle, walk, or run
    choice = random.random()
    if choice < 0.2:
        return build_response(my_x, my_z, "stop", "none", "")
    elif choice < 0.6:
        return build_response(target[0], target[1], "walk", "none", "")
    else:
        return build_response(target[0], target[1], "run", "none", "")


def random_target(x, z):
    """Generate random point within wander radius."""
    return (
        x + random.uniform(-WANDER_RADIUS, WANDER_RADIUS),
        z + random.uniform(-WANDER_RADIUS, WANDER_RADIUS)
    )


# =========================
# ===== RESPONSE BUILDER ==
# =========================

def build_response(target_x, target_z, movement_type, action_type, target_id=""):
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