import math

def decide_action(perception):
    # 1. RÉCUPÉRATION DES DONNÉES DEPUIS UNITY
    my_x = float(perception.get('my_x', 0.0))
    my_z = float(perception.get('my_z', 0.0))

    # État : 1 si le robot porte un cube, 0 sinon
    is_carrying = perception.get('is_carrying', 0) == 1

    # Liste des étagères disponibles au sol
    shelves = perception.get('shelves', [])

    # 2. ZONE DE DÉPÔT (carré vert)
    delivery_zone_x = -8.0
    delivery_zone_z = 0.0

    target_x, target_z = my_x, my_z
    action_type = "none"
    movement_type = "walk"

    # 3. LOGIQUE DE MISSION
    if not is_carrying:
        if shelves:
            # Trouver l'étagère la plus proche (sans modifier la liste originale)
            closest_shelf = min(
                shelves,
                key=lambda s: (s['x'] - my_x) ** 2 + (s['z'] - my_z) ** 2
            )
            target_x = closest_shelf['x']
            target_z = closest_shelf['z']
        else:
            # Retour à la base
            target_x, target_z = 0.0, 0.0
    else:
        # Aller déposer le colis
        target_x = delivery_zone_x
        target_z = delivery_zone_z

    # 4. DISTANCE À LA CIBLE
    dx = target_x - my_x
    dz = target_z - my_z
    dist = math.sqrt(dx * dx + dz * dz)

    if dist < 0.7:
        movement_type = "stop"

    if not is_carrying and near_shelf(my_x, my_z, shelves):
        action_type = "pick_up"

    elif is_carrying and near_delivery(
        my_x, my_z,
        delivery_zone_x, delivery_zone_z
    ):
        action_type = "drop_off"


    # 5. ÉVITEMENT DES AUTRES ROBOTS
    sep_x, sep_z = 0.0, 0.0
    all_agents = perception.get('all_agents', {})

    for aid, pos in all_agents.items():
        ox = pos.get('x', 0.0)
        oz = pos.get('z', 0.0)

        dx = my_x - ox
        dz = my_z - oz
        d = math.sqrt(dx * dx + dz * dz)

        if 0 < d < 1.3:
            strength = (1.3 - d) / 1.3  # évitement progressif
            sep_x += (dx / d) * strength * 1.5
            sep_z += (dz / d) * strength * 1.5

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
def near_shelf(my_x, my_z, shelves, threshold=0.7):
    for s in shelves:
        if math.hypot(s['x'] - my_x, s['z'] - my_z) < threshold:
            return True
    return False

def near_delivery(my_x, my_z, dx, dz, threshold=0.7):
    return math.hypot(dx - my_x, dz - my_z) < threshold
