import numpy as np
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
import time

def main():
    print("Connecting to Unity...")
    
    # Connect to Unity Editor (must be in Play mode)
    env = UnityEnvironment(file_name=None, seed=1, side_channels=[])
    env.reset()
    
    # Get the behavior
    behavior_name = list(env.behavior_specs)[0]
    
    print(f"✓ Connected to: {behavior_name}")
    print("Sending random movement commands...\n")
    
    step_count = 0
    
    try:
        while True:
            # Get agents that need decisions
            decision_steps, terminal_steps = env.get_steps(behavior_name)
            
            if len(decision_steps) > 0:
                n_agents = len(decision_steps)
                
                # Create random actions for each agent
                actions = np.zeros((n_agents, 3))
                
                for i in range(n_agents):
                    actions[i, 0] = np.random.uniform(-20, 20)  # Random X
                    actions[i, 1] = np.random.uniform(-20, 20)  # Random Z
                    actions[i, 2] = np.random.choice([0, 1])    # Random walk/run
                
                # Send to Unity
                action_tuple = ActionTuple(continuous=actions)
                env.set_actions(behavior_name, action_tuple)
                
                # Status update
                if step_count % 50 == 0:
                    print(f"Step {step_count}: {n_agents} agents moving randomly")
                
                step_count += 1
            
            env.step()
            time.sleep(0.02)  # Small delay
            
    except KeyboardInterrupt:
        print("\n\nStopping...")
    finally:
        env.close()
        print("Done!")

if __name__ == "__main__":
    main()