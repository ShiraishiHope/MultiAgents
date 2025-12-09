"""
Random Movement Behavior
Simple test script - agents move to random positions
"""

import random

def decide_action(my_position):
    """
    Makes random movement decisions
    
    Args:
        my_position: dict with 'x' and 'z' keys (current agent position)
    
    Returns:
        tuple: (target_x, target_z, should_run)
            - target_x: X coordinate to move to
            - target_z: Z coordinate to move to
            - should_run: True to run, False to walk
    """
    
    # Generate random target position within 20 units of origin
    target_x = random.uniform(-20, 20)
    target_z = random.uniform(-20, 20)
    
    # Randomly decide whether to walk or run (50/50 chance)
    should_run = random.choice([True, False])
    
    # Return the decision as a tuple
    return (target_x, target_z, should_run)