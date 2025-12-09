"""
Simple Flocking Behavior - WALK ONLY VERSION
All agents walk at normal speed
"""

import random

# Global state (persists between calls)
group_memberships = {}  # {follower_id: leader_id}
leader_data = {}  # {leader_id: {'target_x', 'target_z', 'time_left', 'group_size'}}

MEETING_DISTANCE = 4.0
DECISION_INTERVAL = 0.1
FOLLOW_DISTANCE = 2.0

def decide_action(perception_data):
    """
    Main decision function called by Unity every DECISION_INTERVAL seconds.
    
    Returns: (target_x, target_z, should_run)
    - target_x, target_z: World position to move toward
    - should_run: Always False (walk only)
    """
    my_id = perception_data['my_id']
    my_x = perception_data['my_x']
    my_z = perception_data['my_z']
    all_agents = perception_data['all_agents']
    
    # === FOLLOWERS: Just follow leader ===
    if my_id in group_memberships:
        leader_id = group_memberships[my_id]
        
        if leader_id in all_agents:
            leader_pos = all_agents[leader_id]
            # Always walk (False)
            return (leader_pos[0], leader_pos[1], False)
        else:
            # Leader disappeared - become independent
            del group_memberships[my_id]
            become_leader(my_id, my_x, my_z)
            return (leader_data[my_id]['target_x'], 
                    leader_data[my_id]['target_z'], 
                    False)
    
    # === LEADERS: Check for merges, then move ===
    
    # Ensure I'm initialized as a leader
    if my_id not in leader_data:
        become_leader(my_id, my_x, my_z)
    
    # Update my group size
    update_group_size(my_id)
    my_group_size = leader_data[my_id]['group_size']
    
    # Find nearby leaders
    nearby_leaders = []
    for agent_id, pos in all_agents.items():
        if agent_id == my_id:
            continue
        
        # Skip my own followers
        if agent_id in group_memberships and group_memberships[agent_id] == my_id:
            continue
        
        # Only consider other leaders
        if agent_id not in leader_data:
            continue
        
        distance = calculate_distance(my_x, my_z, pos[0], pos[1])
        if distance <= MEETING_DISTANCE:
            other_size = leader_data[agent_id]['group_size']
            nearby_leaders.append((agent_id, pos, distance, other_size))
    
    # Check for merges
    if len(nearby_leaders) > 0:
        # Find if there's a STRICTLY LARGER group
        best_leader = None
        best_size = my_group_size
        best_pos = None
        
        for leader_id, pos, dist, size in nearby_leaders:
            if size > best_size:
                best_leader = leader_id
                best_size = size
                best_pos = pos
        
        # Merge into larger group
        if best_leader is not None:
            merge_into_group(my_id, best_leader)
            
            # Refresh position from all_agents
            if best_leader in all_agents:
                fresh_pos = all_agents[best_leader]
                return (fresh_pos[0], fresh_pos[1], False)
        
        # Handle equal-sized groups - find the one with LOWEST ID
        equal_sized = [
            (leader_id, pos) 
            for leader_id, pos, dist, size in nearby_leaders 
            if size == my_group_size
        ]
        
        if len(equal_sized) > 0:
            # Find the leader with the LOWEST ID among all equal-sized groups
            lowest_id = my_id
            lowest_pos = None
            
            for leader_id, pos in equal_sized:
                if leader_id < lowest_id:
                    lowest_id = leader_id
                    lowest_pos = pos
            
            # If someone has a lower ID than me, I join them
            if lowest_id != my_id:
                merge_into_group(my_id, lowest_id)
                
                # Refresh position
                if lowest_id in all_agents:
                    fresh_pos = all_agents[lowest_id]
                    return (fresh_pos[0], fresh_pos[1], False)
    
    # No merges - continue as leader with random movement
    return leader_random_movement(my_id, my_x, my_z)


def become_leader(agent_id, current_x, current_z):
    """
    Initialize agent as leader with random destination.
    """
    target_x, target_z = generate_random_destination(current_x, current_z)
    leader_data[agent_id] = {
        'target_x': target_x,
        'target_z': target_z,
        'time_left': 3.0,
        'group_size': 1
    }


def merge_into_group(my_id, new_leader_id):
    """
    Merge my group into another group.
    """
    # Transfer all my followers to the new leader
    if my_id in leader_data:
        for follower_id in list(group_memberships.keys()):
            if group_memberships[follower_id] == my_id:
                group_memberships[follower_id] = new_leader_id
        
        # Remove my leader status
        del leader_data[my_id]
    
    # I become a follower
    group_memberships[my_id] = new_leader_id
    
    # Update new leader's group size
    update_group_size(new_leader_id)


def update_group_size(leader_id):
    """
    Count followers + leader to get total group size.
    """
    if leader_id not in leader_data:
        return
    
    follower_count = sum(
        1 for their_leader in group_memberships.values() 
        if their_leader == leader_id
    )
    
    leader_data[leader_id]['group_size'] = follower_count + 1


def leader_random_movement(leader_id, current_x, current_z):
    """
    Leaders wander randomly, changing direction every 3 seconds.
    Always walk (never run).
    """
    if leader_id not in leader_data:
        become_leader(leader_id, current_x, current_z)
    
    data = leader_data[leader_id]
    
    # Decrease timer
    data['time_left'] -= DECISION_INTERVAL
    
    # Time to change direction?
    if data['time_left'] <= 0:
        data['target_x'], data['target_z'] = generate_random_destination(current_x, current_z)
        data['time_left'] = 3.0
    
    # Reached destination?
    distance_to_target = calculate_distance(current_x, current_z, data['target_x'], data['target_z'])
    if distance_to_target < 1.0:
        data['target_x'], data['target_z'] = generate_random_destination(current_x, current_z)
        data['time_left'] = 3.0
    
    # Always walk (False)
    return (data['target_x'], data['target_z'], False)


def generate_random_destination(current_x, current_z):
    """
    Generate a random destination 8-12 units away from current position.
    """
    offset_x = random.uniform(-12.0, 12.0)
    offset_z = random.uniform(-12.0, 12.0)
    return (current_x + offset_x, current_z + offset_z)


def calculate_distance(x1, z1, x2, z2):
    """
    Calculate Euclidean distance between two points.
    """
    dx = x2 - x1
    dz = z2 - z1
    return (dx * dx + dz * dz) ** 0.5