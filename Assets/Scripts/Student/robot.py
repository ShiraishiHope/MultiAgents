import math
import random
import UnityEngine # type: ignore

# -----------------------------
# Fonctions utilitaires
# -----------------------------

def near_item(robot_x, robot_z, items, threshold=2): 
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

        # 1️⃣ Garder la cible actuelle si elle existe encore
        current_target_item = next(
            (item for item in visible_items if str(item['id']) == current_target_id),
            None
        )

        if current_target_item and current_target_id not in reserved_items:
            target_id = current_target_id
            target_pos_x = current_target_item['x']
            target_pos_z = current_target_item['z']

        else:
            # 2️⃣ Sélection polie d’un nouvel item
            candidate_items = [
                item for item in visible_items
                if str(item['id']) not in reserved_items
            ]

            candidate_items.sort(
                key=lambda item: (item['x'] - robot_x) ** 2 + (item['z'] - robot_z) ** 2
            )

            for item in candidate_items:
                my_distance_sq = (item['x'] - robot_x) ** 2 + (item['z'] - robot_z) ** 2
                better_robot_found = False

                for other_robot_id, other_robot in all_robots.items():
                    if str(other_robot_id) == robot_id:
                        continue

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

    if not carrying_item and target_id != "0" and distance_to_target < 0.6:
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
