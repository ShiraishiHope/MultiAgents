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
    # RÉSERVATIONS DES AUTRES ROBOTS
    # -----------------------------
    reserved_items = {
        str(robot_data.get('current_target_id'))
        for other_robot_id, robot_data in all_robots.items()
        if str(other_robot_id) != robot_id
        and str(robot_data.get('current_target_id', "0")) != "0"
    }

    # =============================
    # MODE : JE TRANSPORTE → DÉPÔT
    # =============================
    if carrying_item and delivery_zones:
        closest_delivery = min(
            delivery_zones,
            key=lambda d: (d['x'] - robot_x) ** 2 + (d['z'] - robot_z) ** 2
        )
        target_pos_x = closest_delivery['x']
        target_pos_z = closest_delivery['z']

    # =============================
    # MODE : JE CHERCHE UN ITEM
    # =============================
    elif not carrying_item:

        # 1 VERROUILLAGE DE LA CIBLE EXISTANTE
        LOCK_DISTANCE = 6.0

        if current_target_id != "0" and current_target_id in visible_items_by_id:
            item = visible_items_by_id[current_target_id]
            dist = math.hypot(item['x'] - robot_x, item['z'] - robot_z)

            if dist < LOCK_DISTANCE:
                target_id = current_target_id
                target_pos_x = item['x']
                target_pos_z = item['z']
            else:
                current_target_id = "0"

        # 2️ SÉLECTION POLIE D’UNE NOUVELLE CIBLE
        if current_target_id == "0":

            candidate_items = [
                item for item in visible_items
                if str(item['id']) not in reserved_items
            ]

            available_other_robots_without_target = {
                other_robot_id: other_robot_data
                for other_robot_id, other_robot_data in all_robots.items()
                if other_robot_id != robot_id
                and other_robot_data.get('current_target_id', '0') == '0'
                and not other_robot_data.get('is_carrying')
            }

            candidate_items.sort(
                key=lambda item: (item['x'] - robot_x) ** 2 + (item['z'] - robot_z) ** 2
            )

            for item in candidate_items:
                my_distance_sq = (item['x'] - robot_x) ** 2 + (item['z'] - robot_z) ** 2
                better_robot_found = False

                for other_robot_id, other_robot in available_other_robots_without_target.items():
                    other_distance_sq = (
                        (item['x'] - other_robot['x']) ** 2 +
                        (item['z'] - other_robot['z']) ** 2
                    )

                    if (
                        other_distance_sq < my_distance_sq or
                        (
                            math.isclose(other_distance_sq, my_distance_sq, rel_tol=0.01)
                            and str(other_robot_id) < robot_id
                        )
                    ):
                        better_robot_found = True
                        break

                if not better_robot_found:
                    target_id = str(item['id'])
                    target_pos_x = item['x']
                    target_pos_z = item['z']
                    break

    # =============================
    # ACTIONS
    # =============================
    distance_to_target = math.hypot(
        target_pos_x - robot_x,
        target_pos_z - robot_z
    )

    movement_type = "walk"
    action_type = "none"

    # PICK UP PLUS STABLE
    if not carrying_item and target_id != "0" and near_item(robot_x, robot_z, visible_items):
        action_type = "pick_up"
        movement_type = "stop"

    # DROP OFF
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

        if 0 < distance < 1.5:
            strength = (1.5 - distance) / 1.5
            avoidance_x += (dx / distance) * strength * 1.5
            avoidance_z += (dz / distance) * strength * 1.5

    # =============================
    # ÉVITEMENT AGENTS
    # =============================
    # On boucle sur les valeurs (les données de chaque robot)
    for other_id, other_data in all_robots.items():
        
        # SÉCURITÉ : On n'évite pas soi-même !
        if str(other_id) == robot_id:
            continue
            
        dx = robot_x - other_data.get('x', 0.0)
        dz = robot_z - other_data.get('z', 0.0)
        distance = math.hypot(dx, dz)

        # Si un autre robot est trop proche (rayon de 1.5 unité)
        if 0 < distance < 1.5:
            # Plus ils sont proches, plus la force est grande
            strength = (1.5 - distance) / 1.5
            # Normalisation du vecteur (dx/distance) et application de la force
            avoidance_x += (dx / distance) * strength * 2.0 
            avoidance_z += (dz / distance) * strength * 2.0

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
    """ Called by Unity once per decision cycle with ALL agents' perception data. """ 

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