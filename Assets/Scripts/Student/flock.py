"""
Simple Flocking Behavior - Batch Processing Version
All agents walk at normal speed, forming groups based on proximity

Entry point: decide_all() is called by Unity once per decision cycle
"""

import random


# =========================
# ===== CONSTANTS =========
# =========================

MEETING_DISTANCE = 4.0
DECISION_INTERVAL = 0.5  # Must match C# decisionInterval!
FOLLOW_DISTANCE = 2.0


# =========================
# ===== GLOBAL STATE ======
# =========================
# Persists between decision cycles

group_memberships = {}  # {follower_id: leader_id}
leader_data = {}        # {leader_id: {'target_x', 'target_z', 'time_left', 'group_size'}}


# =========================
# ===== BATCH ENTRY POINT =
# =========================

def decide_all(all_perceptions):
    """
    Called by Unity once per decision cycle with ALL agents' perception data.
    
    Args:
        all_perceptions: dict of {agent_id: perception_data}
    
    Returns:
        dict of {agent_id: decision}
    """
    all_decisions = {}
    
    for agent_id, perception in all_perceptions.items():
        try:
            decision = decide_action(perception)
            all_decisions[agent_id] = decision
        except Exception as e:
            # On error, stop the agent
            all_decisions[agent_id] = build_response(0, 0, "stop")
    
    return all_decisions


# =========================
# ===== AGENT LOGIC =======
# =========================

def decide_action(perception):
    """
    Decision logic for a single agent.
    
    Args:
        perception: dict with this agent's sensory data including 'all_agents'
        
    Returns:
        dict with 'movement' and 'action' keys
    """
    my_id = perception['my_id']
    my_x = perception['my_x']
    my_z = perception['my_z']
    all_agents = perception['all_agents']  # Shared dict built by C#
    
    target_x, target_z, movement_type = determine_movement(my_id, my_x, my_z, all_agents)
    
    return build_response(target_x, target_z, movement_type)


def determine_movement(my_id, my_x, my_z, all_agents):
    """
    Core flocking logic - determines where the agent should move.
    
    Returns:
        (target_x, target_z, movement_type)
    """
    
    # === FOLLOWERS: Follow their leader ===
    if my_id in group_memberships:
        leader_id = group_memberships[my_id]
        
        if leader_id in all_agents:
            leader_pos = all_agents[leader_id]
            return (leader_pos['x'], leader_pos['z'], "walk")
        else:
            # Leader gone - become independent
            del group_memberships[my_id]
            become_leader(my_id, my_x, my_z)
            data = leader_data[my_id]
            return (data['target_x'], data['target_z'], "walk")
    
    # === LEADERS: Check for merges, then wander ===
    
    # Initialize as leader if needed
    if my_id not in leader_data:
        become_leader(my_id, my_x, my_z)
    
    # Update group size
    update_group_size(my_id)
    my_group_size = leader_data[my_id]['group_size']
    
    # Find nearby leaders for potential merging
    nearby_leaders = find_nearby_leaders(my_id, my_x, my_z, all_agents)
    
    # Try to merge with a larger group
    if nearby_leaders:
        merge_target = find_merge_target(my_id, my_group_size, nearby_leaders)
        
        if merge_target is not None:
            merge_into_group(my_id, merge_target)
            
            if merge_target in all_agents:
                pos = all_agents[merge_target]
                return (pos['x'], pos['z'], "walk")
    
    # No merge - continue wandering as leader
    return leader_wander(my_id, my_x, my_z)


def find_nearby_leaders(my_id, my_x, my_z, all_agents):
    """
    Find other leaders within MEETING_DISTANCE.
    
    Returns:
        list of (leader_id, position, distance, group_size)
    """
    nearby = []
    
    for agent_id, pos in all_agents.items():
        # Skip self
        if agent_id == my_id:
            continue
        
        # Skip my followers
        if agent_id in group_memberships and group_memberships[agent_id] == my_id:
            continue
        
        # Only consider leaders
        if agent_id not in leader_data:
            continue
        
        dist = calculate_distance(my_x, my_z, pos['x'], pos['z'])
        
        if dist <= MEETING_DISTANCE:
            size = leader_data[agent_id]['group_size']
            nearby.append((agent_id, pos, dist, size))
    
    return nearby


def find_merge_target(my_id, my_group_size, nearby_leaders):
    """
    Determine which leader to merge with, if any.
    
    Rules:
    1. Merge into strictly larger groups
    2. For equal-sized groups, lower ID wins (deterministic tie-breaker)
    
    Returns:
        leader_id to merge with, or None
    """
    # First: look for strictly larger group
    for leader_id, pos, dist, size in nearby_leaders:
        if size > my_group_size:
            return leader_id
    
    # Second: handle equal-sized groups
    equal_sized = [
        (leader_id, pos)
        for leader_id, pos, dist, size in nearby_leaders
        if size == my_group_size
    ]
    
    if equal_sized:
        # Find lowest ID among equals (including myself)
        lowest_id = my_id
        
        for leader_id, pos in equal_sized:
            if leader_id < lowest_id:
                lowest_id = leader_id
        
        # Only merge if someone else has lower ID
        if lowest_id != my_id:
            return lowest_id
    
    return None


# =========================
# ===== GROUP MANAGEMENT ==
# =========================

def become_leader(agent_id, current_x, current_z):
    """Initialize agent as a leader with a random wander target."""
    target_x, target_z = generate_random_destination(current_x, current_z)
    leader_data[agent_id] = {
        'target_x': target_x,
        'target_z': target_z,
        'time_left': 3.0,
        'group_size': 1
    }


def merge_into_group(my_id, new_leader_id):
    """Transfer myself and all my followers to a new leader."""
    # Transfer my followers to new leader
    if my_id in leader_data:
        for follower_id in list(group_memberships.keys()):
            if group_memberships[follower_id] == my_id:
                group_memberships[follower_id] = new_leader_id
        
        # Remove my leader status
        del leader_data[my_id]
    
    # I become a follower
    group_memberships[my_id] = new_leader_id
    
    # Update new leader's count
    update_group_size(new_leader_id)


def update_group_size(leader_id):
    """Recalculate a leader's group size."""
    if leader_id not in leader_data:
        return
    
    follower_count = sum(
        1 for lid in group_memberships.values()
        if lid == leader_id
    )
    
    leader_data[leader_id]['group_size'] = follower_count + 1


# =========================
# ===== LEADER MOVEMENT ===
# =========================

def leader_wander(leader_id, current_x, current_z):
    """
    Leaders wander randomly, picking new destinations periodically.
    
    Returns:
        (target_x, target_z, movement_type)
    """
    data = leader_data[leader_id]
    
    # Countdown timer
    data['time_left'] -= DECISION_INTERVAL
    
    # Pick new destination when timer expires
    if data['time_left'] <= 0:
        data['target_x'], data['target_z'] = generate_random_destination(current_x, current_z)
        data['time_left'] = 3.0
    
    # Pick new destination when close to current target
    dist_to_target = calculate_distance(
        current_x, current_z,
        data['target_x'], data['target_z']
    )
    
    if dist_to_target < 1.0:
        data['target_x'], data['target_z'] = generate_random_destination(current_x, current_z)
        data['time_left'] = 3.0
    
    return (data['target_x'], data['target_z'], "walk")


# =========================
# ===== UTILITIES =========
# =========================

def generate_random_destination(current_x, current_z):
    """Generate a random point within a square around current position."""
    offset_x = random.uniform(-12.0, 12.0)
    offset_z = random.uniform(-12.0, 12.0)
    return (current_x + offset_x, current_z + offset_z)


def calculate_distance(x1, z1, x2, z2):
    """Euclidean distance between two 2D points."""
    dx = x2 - x1
    dz = z2 - z1
    return (dx * dx + dz * dz) ** 0.5


def build_response(target_x, target_z, movement_type):
    """
    Build response dict in the format C# expects.
    
    Args:
        target_x, target_z: destination coordinates
        movement_type: "walk", "run", "stop", or "none"
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