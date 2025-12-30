"""
Flu Behavior - Batch Processing Version
Agents wander randomly and sneeze/cough when contagious
"""

import random

# =========================
# ===== GLOBAL STATE ======
# =========================

wander_data = {}  # {agent_id: {'target_x', 'target_z', 'time_left'}}

WANDER_INTERVAL = 3.0      # Change direction every 3 seconds
DECISION_INTERVAL = 0.5    # How often decide_all is called (match C#)
SNEEZE_CHANCE = 0.3        # 30% chance to sneeze when contagious
COUGH_CHANCE = 0.2         # 20% chance to cough when contagious


# =========================
# ===== BATCH WRAPPER =====
# =========================

def decide_all(all_perceptions):
    """
    Called by Unity once per decision cycle with ALL agents' perception data.
    
    For flu behavior, agents don't need to know about ALL other agents -
    they just use their local perception (visible/heard agents).
    So we don't need to build a shared all_agents dict here.
    """
    all_decisions = {}
    
    for agent_id, perception in all_perceptions.items():
        try:
            decision = decide_action(perception)
            all_decisions[agent_id] = decision
        except Exception as e:
            print(f"Error processing {agent_id}: {e}")
            all_decisions[agent_id] = build_response(0, 0, "stop", "none")
    
    return all_decisions


# =========================
# ===== AGENT LOGIC =======
# =========================

def decide_action(perception):
    """
    Main decision function for a single agent.
    Agents wander randomly. When contagious, they sneeze/cough.
    
    This behavior demonstrates:
    - Using local perception (heard_agents) instead of global knowledge
    - Infection spread mechanics via sneeze/cough actions
    """
    my_id = perception['my_id']
    my_x = perception['my_x']
    my_z = perception['my_z']
    is_contagious = perception['is_contagious'] == 1
    infection_stage = perception['infection_stage']
    
    # Determine movement (always wander)
    target_x, target_z = get_wander_target(my_id, my_x, my_z)
    
    # Determine action (sneeze/cough if contagious and near others)
    action_type = "none"
    
    if is_contagious:
        # Check if anyone is nearby using heard_agents
        # heard_count tells us how many agents are within hearing range
        heard_count = perception['heard_count']
        
        if heard_count > 0:
            # Roll for sneeze or cough
            roll = random.random()
            if roll < SNEEZE_CHANCE:
                action_type = "sneeze"
            elif roll < SNEEZE_CHANCE + COUGH_CHANCE:
                action_type = "cough"
    
    return build_response(target_x, target_z, "walk", action_type)


# =========================
# ===== HELPER FUNCTIONS ==
# =========================

def get_wander_target(agent_id, current_x, current_z):
    """
    Returns a wander target, generating new one if needed.
    Uses global wander_data to persist targets between calls.
    """
    # Initialize if new agent
    if agent_id not in wander_data:
        target_x, target_z = generate_random_target(current_x, current_z)
        wander_data[agent_id] = {
            'target_x': target_x,
            'target_z': target_z,
            'time_left': WANDER_INTERVAL
        }
    
    data = wander_data[agent_id]
    
    # Decrease timer
    data['time_left'] -= DECISION_INTERVAL
    
    # Time to pick new target?
    if data['time_left'] <= 0:
        data['target_x'], data['target_z'] = generate_random_target(current_x, current_z)
        data['time_left'] = WANDER_INTERVAL
    
    # Reached target? Pick new one
    dist = calculate_distance(current_x, current_z, data['target_x'], data['target_z'])
    if dist < 1.0:
        data['target_x'], data['target_z'] = generate_random_target(current_x, current_z)
        data['time_left'] = WANDER_INTERVAL
    
    return data['target_x'], data['target_z']


def generate_random_target(current_x, current_z):
    """Generate a random point 5-10 units away."""
    offset_x = random.uniform(-10.0, 10.0)
    offset_z = random.uniform(-10.0, 10.0)
    return current_x + offset_x, current_z + offset_z


def calculate_distance(x1, z1, x2, z2):
    """Euclidean distance between two points."""
    dx = x2 - x1
    dz = z2 - z1
    return (dx * dx + dz * dz) ** 0.5


def build_response(target_x, target_z, movement_type, action_type):
    """
    Build the response dict in the format C# expects.
    
    Movement types: "walk", "run", "stop", "none"
    Action types: "none", "sneeze", "cough", "attack", "bite", "claw", etc.
    """
    return {
        "movement": {
            "type": movement_type,
            "target_x": target_x,
            "target_z": target_z
        },
        "action": {
            "type": action_type,
            "target_id": "",
            "parameters": {}
        }
    }
