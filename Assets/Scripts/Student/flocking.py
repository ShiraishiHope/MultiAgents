"""
Advanced Flocking Behavior with Group Formation and Leadership
Works with AgentPerceptionData from AgentActionManager
"""

import random

# Global dictionary to track group leaders (persists between calls)
# Format: {agent_id: leader_id} - maps followers to their leader
group_memberships = {}
# Format: {leader_id: (target_x, target_z, time_counter)} - tracks leader destinations
leader_destinations = {}

def decide_action(perception_data):
    """
    Advanced flocking with group formation, merging, and leader-following
    
    Args:
        perception_data: dict with:
            - my_id: my unique agent ID
            - my_x, my_z: my current position
            - my_type: my character type
            - my_faction: my faction ("Human" or "Skeleton")
            - visible_agents: dict of {agent_id: (x, z)} for agents I can see
    
    Returns:
        tuple: (target_x, target_z, should_run)
    """
    
    my_id = perception_data['my_id']
    my_x = perception_data['my_x']
    my_z = perception_data['my_z']
    my_faction = perception_data['my_faction']
    visible_agents = perception_data['visible_agents']
    
    # Filter visible agents to only same-faction agents
    same_faction_agents = {}
    for agent_id, pos in visible_agents.items():
        agent_faction = "Skeleton" if "Skeleton" in agent_id else "Human"
        if agent_faction == my_faction:
            same_faction_agents[agent_id] = pos
    
    # === SCENARIO 1: No allies visible ===
    if len(same_faction_agents) == 0:
        # Become a leader and move randomly
        return become_leader(my_id, my_x, my_z)
    
    # === SCENARIO 2: Check if I'm already following a leader ===
    if my_id in group_memberships:
        my_leader = group_memberships[my_id]
        
        # Check if my leader is still visible
        if my_leader in same_faction_agents:
            leader_pos = same_faction_agents[my_leader]
            distance_to_leader = calculate_distance(my_x, my_z, leader_pos[0], leader_pos[1])
            
            # Follow the leader
            # Walk if close, run if far
            should_run = distance_to_leader > 3.0
            return (leader_pos[0], leader_pos[1], should_run)
        else:
            # Leader no longer visible - leave the group
            if my_id in group_memberships:
                del group_memberships[my_id]
    
    # === SCENARIO 3: Check if any visible agent is a known leader ===
    visible_leaders = []
    for agent_id in same_faction_agents.keys():
        if agent_id in leader_destinations:
            visible_leaders.append(agent_id)
    
    if len(visible_leaders) > 0:
        # Join the nearest leader's group
        nearest_leader = None
        nearest_leader_distance = float('inf')
        
        for leader_id in visible_leaders:
            leader_pos = same_faction_agents[leader_id]
            distance = calculate_distance(my_x, my_z, leader_pos[0], leader_pos[1])
            if distance < nearest_leader_distance:
                nearest_leader_distance = distance
                nearest_leader = leader_id
        
        # Join this leader's group
        group_memberships[my_id] = nearest_leader
        leader_pos = same_faction_agents[nearest_leader]
        should_run = nearest_leader_distance > 3.0
        return (leader_pos[0], leader_pos[1], should_run)
    
    # === SCENARIO 4: Calculate group dynamics ===
    # Find nearest ally and calculate group center
    nearest_distance = float('inf')
    nearest_x = my_x
    nearest_z = my_z
    nearest_id = None
    
    total_x = 0.0
    total_z = 0.0
    
    for agent_id, pos in same_faction_agents.items():
        total_x += pos[0]
        total_z += pos[1]
        
        distance = calculate_distance(my_x, my_z, pos[0], pos[1])
        if distance < nearest_distance:
            nearest_distance = distance
            nearest_x = pos[0]
            nearest_z = pos[1]
            nearest_id = agent_id
    
    group_center_x = total_x / len(same_faction_agents)
    group_center_z = total_z / len(same_faction_agents)
    group_size = len(same_faction_agents)
    
    # Calculate if I'm close to the group center (already grouped)
    distance_to_center = calculate_distance(my_x, my_z, group_center_x, group_center_z)
    
    # === SCENARIO 5: Determine if group is formed (agents are clustered) ===
    # Check if most agents are close together
    agents_in_cluster = 0
    cluster_radius = 4.0  # Agents within 4 units are considered "grouped"
    
    for pos in same_faction_agents.values():
        dist_to_center = calculate_distance(pos[0], pos[1], group_center_x, group_center_z)
        if dist_to_center <= cluster_radius:
            agents_in_cluster += 1
    
    # If 75% of visible agents are clustered, the group is formed
    cluster_ratio = agents_in_cluster / len(same_faction_agents)
    group_is_formed = cluster_ratio >= 0.75 and group_size >= 3
    
    if group_is_formed and distance_to_center <= cluster_radius:
        # GROUP IS FORMED - designate leader
        # The agent with the "smallest" ID becomes leader (deterministic)
        all_grouped_agents = [my_id] + list(same_faction_agents.keys())
        all_grouped_agents.sort()  # Sort alphabetically
        designated_leader = all_grouped_agents[0]
        
        if designated_leader == my_id:
            # I AM THE LEADER
            return become_leader(my_id, my_x, my_z)
        else:
            # I AM A FOLLOWER
            group_memberships[my_id] = designated_leader
            
            # Follow the designated leader if visible
            if designated_leader in same_faction_agents:
                leader_pos = same_faction_agents[designated_leader]
                distance_to_leader = calculate_distance(my_x, my_z, leader_pos[0], leader_pos[1])
                should_run = distance_to_leader > 3.0
                return (leader_pos[0], leader_pos[1], should_run)
    
    # === SCENARIO 6: Still forming group - move toward center ===
    if group_size >= 3:
        # Move to group center to form the cluster
        should_run = distance_to_center > 5.0
        return (group_center_x, group_center_z, should_run)
    else:
        # Small group - seek nearest ally
        should_run = nearest_distance > 5.0
        return (nearest_x, nearest_z, should_run)


def become_leader(my_id, my_x, my_z):
    """
    Become a leader and move to a random destination
    
    Returns:
        tuple: (target_x, target_z, should_run)
    """
    # Check if I already have a destination
    if my_id in leader_destinations:
        target_x, target_z, time_counter = leader_destinations[my_id]
        
        # Check if I've reached my destination
        distance_to_target = calculate_distance(my_x, my_z, target_x, target_z)
        
        if distance_to_target < 2.0 or time_counter >= 20:
            # Reached destination or timeout - pick new destination
            target_x, target_z = generate_random_destination(my_x, my_z)
            leader_destinations[my_id] = (target_x, target_z, 0)
        else:
            # Still moving to destination - increment counter
            leader_destinations[my_id] = (target_x, target_z, time_counter + 1)
    else:
        # First time as leader - pick destination
        target_x, target_z = generate_random_destination(my_x, my_z)
        leader_destinations[my_id] = (target_x, target_z, 0)
    
    target_x, target_z, _ = leader_destinations[my_id]
    distance_to_target = calculate_distance(my_x, my_z, target_x, target_z)
    should_run = distance_to_target > 10.0  # Run if far from destination
    
    return (target_x, target_z, should_run)


def generate_random_destination(current_x, current_z):
    """
    Generate a random destination within reasonable distance
    
    Returns:
        tuple: (target_x, target_z)
    """
    # Random angle
    angle = random.uniform(0, 2 * 3.14159)  # 0 to 2pi radians
    
    # Random distance (10 to 30 units away)
    distance = random.uniform(10.0, 30.0)
    
    # Calculate new position
    target_x = current_x + distance * (angle ** 0.5)  # Using angle for x offset
    target_z = current_z + distance * ((2 * 3.14159 - angle) ** 0.5)  # Using complement for z
    
    # Simple trig-free approach using random offsets
    offset_x = random.uniform(-20.0, 20.0)
    offset_z = random.uniform(-20.0, 20.0)
    
    return (current_x + offset_x, current_z + offset_z)


def calculate_distance(x1, z1, x2, z2):
    """
    Calculate Euclidean distance between two points
    
    Returns:
        float: distance
    """
    dx = x2 - x1
    dz = z2 - z1
    return (dx * dx + dz * dz) ** 0.5