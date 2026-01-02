import math

# -----------------------------
# Fonctions utilitaires
# -----------------------------

def near_shelf(my_x, my_z, shelves, threshold=0.7):
    for s in shelves:
        if math.hypot(s['x'] - my_x, s['z'] - my_z) < threshold:
            return True
    return False


def near_delivery(my_x, my_z, dx, dz, threshold=0.7):
    return math.hypot(dx - my_x, dz - my_z) < threshold


# -----------------------------
# Fonction principale
# -----------------------------

def decide_action(perception):
    print("PYTHON VERSION AAAAA")

    # 1. RÉCUPÉRATION DES DONNÉES
    my_x = float(perception.get('my_x', 0.0))
    my_z = float(perception.get('my_z', 0.0))

    is_carrying = perception.get('is_carrying', 0) == 1
    shelves = perception.get('shelves', [])
    all_agents = perception.get('all_agents', {})

    # 2. ZONE DE DÉPÔT
    delivery_zone_x = -8.0
    delivery_zone_z = 0.0

    # 3. CIBLE PAR DÉFAUT
    target_x, target_z = my_x, my_z

    # 4. LOGIQUE DE MISSION
    if not is_carrying:
        if shelves:
            closest_shelf = min(
                shelves,
                key=lambda s: (s['x'] - my_x) ** 2 + (s['z'] - my_z) ** 2
            )
            target_x = closest_shelf['x']
            target_z = closest_shelf['z']
        else:
            target_x, target_z = 0.0, 0.0
    else:
        target_x = delivery_zone_x
        target_z = delivery_zone_z

    # 5. DISTANCE À LA CIBLE
    dist = math.hypot(target_x - my_x, target_z - my_z)

    # 6. ACTION & MOUVEMENT (RÈGLE D’OR UNITY)
    movement_type = "walk"
    action_type = "none"

    if dist < 0.7:
        if not is_carrying and near_shelf(my_x, my_z, shelves):
            movement_type = "stop"
            action_type = "pick_up"

        elif is_carrying and near_delivery(
            my_x, my_z,
            delivery_zone_x, delivery_zone_z
        ):
            movement_type = "stop"
            action_type = "drop_off"

    # 7. ÉVITEMENT DES AUTRES ROBOTS
    sep_x, sep_z = 0.0, 0.0
    for aid, pos in all_agents.items():
        ox = pos.get('x', 0.0)
        oz = pos.get('z', 0.0)

        dx = my_x - ox
        dz = my_z - oz
        d = math.hypot(dx, dz)

        if 0 < d < 1.3:
            strength = (1.3 - d) / 1.3
            sep_x += (dx / d) * strength * 1.5
            sep_z += (dz / d) * strength * 1.5

    # 8. RETOUR VERS UNITY
    return {
        "movement": {
            "type": movement_type,
            "target_x": float(target_x + sep_x),
            "target_z": float(target_z + sep_z)
        },
        "action": {
            "type": action_type,
            "target_id": ""
        }
    }
