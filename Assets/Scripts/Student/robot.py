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

def get_id_int(robot_id_str):
    try:
        # On enlève "Robot_" et on transforme ce qui reste en entier
        return int(str(robot_id_str).replace("Robot_", ""))
    except ValueError:
        return 999999

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
    # -----------------------------
    # RÉCUPÉRATION DES DONNÉES
    # -----------------------------
    
    obstacles = perception.get('obstacles', [])
    current_target_id = str(perception.get('current_target_id', "0"))

    target_id = "0"
    target_pos_x = spawn_x
    target_pos_z = spawn_z

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
        if current_target_id != "0" and current_target_id in visible_items_by_id:
            item = visible_items_by_id[current_target_id]
            dist = math.hypot(item['x'] - robot_x, item['z'] - robot_z)
            target_id = current_target_id
            target_pos_x = item['x']
            target_pos_z = item['z']
        else:
            # 2️ SÉLECTION POLIE D'UNE NOUVELLE CIBLE
            available_other_robots_without_target = {
                other_robot_id: other_robot_data
                for other_robot_id, other_robot_data in all_robots.items()
                if other_robot_id != robot_id
                and other_robot_data.get('current_target_id', '0') == '0'
                and not other_robot_data.get('is_carrying')
            }
            # On liste les IDs des items déjà ciblés par les autres robots
            reserved_items = {
                str(robot_data.get('current_target_id'))
                for other_robot_id, robot_data in all_robots.items()
                if str(other_robot_id) != robot_id
                and str(robot_data.get('current_target_id', "0")) != "0"
            }

            # On crée la liste des items disponibles (ceux qui ne sont pas réservés)
            available_items = [
                item for item in visible_items 
                if str(item['id']) not in reserved_items
            ]

            available_items.sort(
                key=lambda item: (item['x'] - robot_x) ** 2 + (item['z'] - robot_z) ** 2
            )
            
            for item in available_items:
                my_distance_sq = (item['x'] - robot_x) ** 2 + (item['z'] - robot_z) ** 2
                    
                # Trouver le robot le plus proche de cet item (parmi ceux disponibles)
                closest_robot_id = str(robot_id)
                closest_distance_sq = my_distance_sq
                for other_robot_id, other_robot in available_other_robots_without_target.items():
                    other_distance_sq = (
                        (item['x'] - other_robot.get('x')) ** 2 +
                        (item['z'] - other_robot.get('z')) ** 2
                    )
                        
                    # 1. Calcul de la différence de distance
                    is_significantly_closer = other_distance_sq < closest_distance_sq

                    # 2. Vérification de l'égalité (tolérance aux erreurs de flottants)
                    is_effectively_equal = math.isclose(
                        other_distance_sq, 
                        closest_distance_sq, 
                        rel_tol=0.01
                    )
                
                    # 3. Comparaison des IDs en cas d'égalité (Priorité sociale)
                    has_priority_id = str(other_robot_id) < closest_robot_id
                    UnityEngine.Debug.Log(f'{other_robot_id}{closest_robot_id}')

                    # 4. Application de la décision
                    if is_significantly_closer or (is_effectively_equal and has_priority_id):
                        closest_robot_id = str(other_robot_id)
                        closest_distance_sq = other_distance_sq
                    
                # Si je suis le robot le plus proche (avec l'ID le plus bas en cas d'égalité)
                if closest_robot_id == robot_id:
                    target_id = str(item['id'])
                    target_pos_x = item['x']
                    target_pos_z = item['z']
                    break
                else:
                # On vérifie si l'ID existe pour éviter une erreur, puis on le supprime
                    if closest_robot_id in available_other_robots_without_target:
                        del available_other_robots_without_target[closest_robot_id]

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
        if 0 < distance < 0.5:
            # Plus ils sont proches, plus la force est grande
            strength = (0.5 - distance) / 1
            # Normalisation du vecteur (dx/distance) et application de la force
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