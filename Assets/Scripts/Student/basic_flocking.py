"""
Flocking Behavior Using Vision/Hearing System
Works with AgentPerceptionData from AgentActionManager
"""

def decide_action(perception_data):
    """
    Make flocking decision based on what the agent can see/hear
    
    Args:
        perception_data: dict with:
            - my_x, my_z: my current position
            - my_type: my character type (e.g., "Barbarian", "Skeleton_Mage")
            - my_faction: my faction ("Human" or "Skeleton")
            - visible_agents: dict of {agent_id: (x, z)} for agents I can see
            - heard_agents: dict of {agent_id: (x, z)} for agents I can hear
    
    Returns:
        tuple: (target_x, target_z, should_run)
    """
    
    my_x = perception_data['my_x']
    my_z = perception_data['my_z']
    my_faction = perception_data['my_faction']
    visible_agents = perception_data['visible_agents']
    
    # Filter visible agents to only same-faction agents
    same_faction_agents = {}
    for agent_id, pos in visible_agents.items():
        # Agent IDs are formatted like "Barbarian_12345" or "Skeleton_Mage_67890"
        agent_faction = "Skeleton" if "Skeleton" in agent_id else "Human"
        if agent_faction == my_faction:
            same_faction_agents[agent_id] = pos
    
    # If no allies visible, stay put
    if len(same_faction_agents) == 0:
        return (my_x, my_z, False)
    
    # Calculate group center (average position of all visible allies)
    total_x = 0.0
    total_z = 0.0
    for pos in same_faction_agents.values():
        total_x += pos[0]  # x coordinate
        total_z += pos[1]  # z coordinate
    
    group_center_x = total_x / len(same_faction_agents)
    group_center_z = total_z / len(same_faction_agents)
    
    # Find nearest ally
    nearest_distance = float('inf')
    nearest_x = my_x
    nearest_z = my_z
    
    for pos in same_faction_agents.values():
        # Calculate distance using Pythagorean theorem
        dx = pos[0] - my_x
        dz = pos[1] - my_z
        distance = (dx*dx + dz*dz) ** 0.5  # sqrt(dx² + dz²)
        
        if distance < nearest_distance:
            nearest_distance = distance
            nearest_x = pos[0]
            nearest_z = pos[1]
    
    # Decision logic: form groups
    group_size = len(same_faction_agents)
    
    if group_size >= 3:
        # Already in a decent group - move to group center
        target_x = group_center_x
        target_z = group_center_z
        should_run = False  # Walk calmly when in group
    else:
        # Small group or alone - seek nearest ally
        target_x = nearest_x
        target_z = nearest_z
        # Run if far away, walk if close
        should_run = nearest_distance > 5.0
    
    return (target_x, target_z, should_run)