import math
import random
import UnityEngine # type: ignore
# -----------------------------
# Fonctions utilitaires
# -----------------------------

def near_item(my_x, my_z, items, threshold=1.2): # Seuil augmenté pour plus de sécurité
    for s in items:
        if math.hypot(s['x'] - my_x, s['z'] - my_z) < threshold:
            return True
    return False

def near_delivery(my_x, my_z, dx, dz, threshold=1.2): # Seuil augmenté
    return math.hypot(dx - my_x, dz - my_z) < threshold
# -----------------------------
# Fonction principale
# -----------------------------

def decide_action(perception):
    
    # 1. RÉCUPÉRATION DES DONNÉES
    my_x = float(perception.get('my_x', 0.0))
    my_z = float(perception.get('my_z', 0.0))
    is_carrying = perception.get('is_carrying', 0) == 1
    items = perception.get('items', [])
    deposites = perception.get('deposites',[])
    all_agents = perception.get('all_agents', {})
    obstacles = perception.get('obstacles', [])

    # 2. DETECTION DES TARGETS DES AGENTS
    reserved_ids = []
    for _, data in all_agents.items():
        t_id = data.get('current_target_id', "")
        if t_id:
            reserved_ids.append(str(t_id))

    # 3. FILTRAGE : Retirer les items réservés de la liste
    # On ne garde que les items dont l'ID n'est PAS dans reserved_ids
    available_items = [it for it in items if str(it.get('id')) not in reserved_ids]

    # 3. LOGIQUE DE MISSION
    target_x = perception.get("spawn_x",0.0)
    target_z = perception.get("spawn_z",0.0)
    target_id = 0
    
    if not is_carrying:
        if available_items:
            closest_item = min(available_items, key=lambda s: (s['x'] - my_x)**2 + (s['z'] - my_z)**2)
            target_x, target_z, target_id = closest_item['x'], closest_item['z'] , closest_item['id']
    else:
        closest_deposite = min(deposites, key=lambda s: (s['x'] - my_x)**2 + (s['z'] - my_z)**2)
        target_x, target_z = closest_deposite['x'], closest_deposite['z']

    # 4. CALCUL DE LA DISTANCE
    dist = math.hypot(target_x - my_x, target_z - my_z)

    # 5. DÉCISION ACTION & MOUVEMENT
    movement_type = "walk"
    action_type = "none"
  

    # On utilise un seuil légèrement plus large que la distance d'arrêt d'Unity
    if dist < 0.3: 
        if not is_carrying and near_item(my_x, my_z, items):
            movement_type = "stop"
            action_type = "pick_up"
        elif is_carrying and near_delivery(my_x, my_z, target_x, target_z):
            movement_type = "stop"
            action_type = "drop_off"

# 6. ÉVITEMENT 
    sep_x, sep_z = 0.0, 0.0
    
    # Calcul de la distance à la cible actuelle
    dist_to_final_target = math.hypot(target_x - my_x, target_z - my_z)

    # On désactive l'évitement global quand on est très proche du but (1.2m)
    # Sinon le robot ne pourra jamais toucher l'étagère ou la zone de dépose
    if dist_to_final_target > 1.2: 
        
        # --- ÉVITEMENT OBSTACLES (Murs/Étagères) ---
        for obs in obstacles:
            dx, dz = my_x - obs.get('x', 0.0), my_z - obs.get('z', 0.0)
            d = math.hypot(dx, dz)
            if 0 < d < 3.0:
                strength = (3.0 - d) / 3.0
                sep_x += (dx / d) * strength * 3.0 # Force forte pour le décor
                sep_z += (dz / d) * strength * 3.0

        # --- ÉVITEMENT AGENTS ---
        for _, pos in all_agents.items():
            dx, dz = my_x - pos.get('x', 0.0), my_z - pos.get('z', 0.0)
            d = math.hypot(dx, dz)
            if 0 < d < 3.0:
                strength = (3.0 - d) / 3.0
                sep_x += (dx / d) * strength * 2.0 # Force moyenne pour les mobiles
                sep_z += (dz / d) * strength * 2.0

    # 7. CALCUL FINAL DU MOUVEMENT
    final_target_x = target_x + sep_x
    final_target_z = target_z + sep_z

    return {
        "movement": {
            "type": movement_type,
            "target_x": float(target_x + sep_x),
            "target_z": float(target_z + sep_z)
        },
        "action": {
            "type": action_type,
            "target_id": str(target_id)
        }
    }