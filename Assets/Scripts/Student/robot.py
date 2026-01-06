import math
import random
import UnityEngine # type: ignore

# -----------------------------
# Fonctions utilitaires
# -----------------------------

def near_item(robot_x, robot_z, items, threshold=1.0): 
    for item in items:
        if math.hypot(item['x'] - robot_x, item['z'] - robot_z) < threshold:
            return True
    return False

def near_delivery(robot_x, robot_z, delivery_x, delivery_z, threshold=3): 
    return math.hypot(delivery_x - robot_x, delivery_z - robot_z) < threshold


# -----------------------------
# Fonction principale
# -----------------------------

def decide_action(perception):
    # -----------------------------
    # RÉCUPÉRATION DES DONNÉES
    # -----------------------------
    robot_id = str(perception.get('my_id', ''))
    robot_x = float(perception.get('my_x', 0.0))
    robot_z = float(perception.get('my_z', 0.0))

    spawn_x = float(perception.get('spawn_x', 0.0))
    spawn_z = float(perception.get('spawn_z', 0.0))

    carrying_item = perception.get('is_carrying', 0) == 1

    visible_items = perception.get('items', [])
    visible_items_by_id = {str(item['id']): item for item in visible_items}

    delivery_zones = perception.get('deposites', [])
    all_robots = perception.get('all_agents', {})
    obstacles = perception.get('obstacles', [])

    current_target_id = str(perception.get('current_target_id', "0"))

    target_id = "0"
    target_pos_x = spawn_x
    target_pos_z = spawn_z

    # -----------------------------
    # ITEMS RÉSERVÉS
    # -----------------------------
    reserved_items = {
        str(robot_data.get('current_target_id'))
        for other_robot_id, robot_data in all_robots.items()
        if str(other_robot_id) != robot_id
        and str(robot_data.get('current_target_id', "0")) != "0"
    }

    # =============================
    # MODE : TRANSPORTE → DÉPÔT
    # =============================
    if carrying_item and delivery_zones:
        closest_delivery = min(
            delivery_zones,
            key=lambda d: (d['x'] - robot_x) ** 2 + (d['z'] - robot_z) ** 2
        )
        target_pos_x = closest_delivery['x']
        target_pos_z = closest_delivery['z']

    # =============================
    # MODE : CHERCHE UN ITEM
    # =============================
    elif not carrying_item:
        # Si la cible courante est visible
        if current_target_id != "0":
            item = visible_items_by_id[current_target_id]
            target_id = current_target_id
            target_pos_x = item['x']
            target_pos_z = item['z']
        else:
            # Robots sans cible
            available_other_robots_without_target = {
                other_robot_id: other_robot_data
                for other_robot_id, other_robot_data in all_robots.items()
                if other_robot_id != robot_id
                and not other_robot_data.get('is_carrying') 
                and other_robot_data.get('current_target_id') == "0"
            }

            # Items libres
            available_items = [
                item for item_id, item in visible_items_by_id.items()
                if item_id not in reserved_items
            ]

            # Tri par distance
            available_items.sort(
                key=lambda item: (item['x'] - robot_x) ** 2 + (item['z'] - robot_z) ** 2
            )
            
            # Sélection polie
            for item in available_items:
                my_distance_sq = (float(item['x']) - robot_x) ** 2 + (float(item['z']) - robot_z) ** 2
                closer = True

                for other_robot_id, other_robot in available_other_robots_without_target.items():

                    other_x = float(other_robot.get('x'))
                    other_z = float(other_robot.get('z'))

                    item_x = float(item['x'])
                    item_z = float(item['z'])

                    dx = item_x - other_x
                    dz = item_z - other_z
                    other_distance_sq = dx * dx + dz * dz

                    is_other_closer = other_distance_sq < my_distance_sq

                    is_same_distance = math.isclose(
                        other_distance_sq,
                        my_distance_sq,
                        rel_tol=0.01
                    )

                    other_has_priority = str(other_robot_id) < robot_id

                    other_robot_wins = is_other_closer or (is_same_distance and other_has_priority)

                    if other_robot_wins:
                        closer = False
                        break

                if closer :
                    target_id = str(item['id'])
                    target_pos_x = item['x']
                    target_pos_z = item['z']
                    break      

    # =============================
    # ACTIONS
    # =============================
    distance_to_target = math.hypot(target_pos_x - robot_x, target_pos_z - robot_z)
    movement_type = "walk"
    action_type = "none"

    if not carrying_item and target_id != "0" and near_item(robot_x, robot_z, visible_items):
        action_type = "pick_up"
        movement_type = "stop"

    if carrying_item and distance_to_target < 0.6:
        action_type = "drop_off"
        movement_type = "stop"

    # =============================
    # ÉVITEMENT OBSTACLES
    # =============================
    avoidance_x, avoidance_z = 0.0, 0.0

    for obstacle in obstacles:
        dx = robot_x - obstacle['x']
        dz = robot_z - obstacle['z']
        distance = math.hypot(dx, dz)
        if 0 < distance < 2.5:
            strength = (2.5 - distance) / 2.5
            avoidance_x += (dx / distance) * strength * 2.5
            avoidance_z += (dz / distance) * strength * 2.5

    # =============================
    # ÉVITEMENT AGENTS
    # =============================
    for other_id, other_data in all_robots.items():
        if str(other_id) == robot_id:
            continue
        dx = robot_x - other_data.get('x', 0.0)
        dz = robot_z - other_data.get('z', 0.0)
        distance = math.hypot(dx, dz)
        if 0 < distance < 1.5:
            strength = (1.5 - distance)
            avoidance_x += (dx / distance) * strength * 2
            avoidance_z += (dz / distance) * strength * 2

    return {
        "movement": {
            "type": movement_type,
            "target_x": target_pos_x + avoidance_x,
            "target_z": target_pos_z + avoidance_z
        },
        "action": {
            "type": action_type,
            "target_id": target_id
        }
    }


# ========================= 
# ===== BATCH WRAPPER ===== 
# ========================= 

def decide_all(all_perceptions): 
    all_decisions = {} 
    for agent_id, perception in all_perceptions.items(): 
        try: 
            decision = decide_action(perception) 
            all_decisions[agent_id] = decision 
        except Exception as e: 
            UnityEngine.Debug.LogError(f"Error processing {agent_id}: {e}") 
            all_decisions[agent_id] = {
                "movement": {"type": "stop", "target_x": 0.0, "target_z": 0.0},
                "action": {"type": "none", "target_id": "0"}
            }
    return all_decisions
