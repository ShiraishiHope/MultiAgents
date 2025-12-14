"""
Flu Behavior - Passive infection spread test
Agents wander randomly and sneeze/cough when contagious
"""

import random

# Track each agent's wander target
wander_data = {}  # {agent_id: {'target_x', 'target_z', 'time_left'}}

WANDER_INTERVAL = 3.0      # Change direction every 3 seconds
DECISION_INTERVAL = 0.1    # How often decide_action is called
SNEEZE_CHANCE = 0.3        # 30% chance to sneeze each decision cycle when contagious
COUGH_CHANCE = 0.2         # 20% chance to cough each decision cycle when contagious


def decide_action(perception):
    """
    Main decision function called by Unity.
    Agents wander randomly. When contagious, they sneeze/cough.
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
        # Check if anyone is nearby (use heard_agents as proxy for "close")
        heard_count = perception['heard_count']
        
        if heard_count > 0:
            # Roll for sneeze or cough
            roll = random.random()
            if roll < SNEEZE_CHANCE:
                action_type = "sneeze"
            elif roll < SNEEZE_CHANCE + COUGH_CHANCE:
                action_type = "cough"
    
    return build_response(target_x, target_z, "walk", action_type)


def get_wander_target(agent_id, current_x, current_z):
    """
    Returns a wander target, generating new one if needed.
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
    """
    Generate a random point 5-10 units away.
    """
    offset_x = random.uniform(-10.0, 10.0)
    offset_z = random.uniform(-10.0, 10.0)
    return current_x + offset_x, current_z + offset_z


def calculate_distance(x1, z1, x2, z2):
    """
    Euclidean distance between two points.
    """
    dx = x2 - x1
    dz = z2 - z1
    return (dx * dx + dz * dz) ** 0.5


def build_response(target_x, target_z, movement_type, action_type):
    """
    Build the response dict in the format C# expects.
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