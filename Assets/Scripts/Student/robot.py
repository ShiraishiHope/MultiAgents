import math
import random
import UnityEngine # type: ignore

# -----------------------------
# Fonctions utilitaires
# -----------------------------

def near_item(my_x, my_z, items, threshold=1.2): 
    for s in items:
        if math.hypot(s['x'] - my_x, s['z'] - my_z) < threshold:
            return True
    return False

def near_delivery(my_x, my_z, dx, dz, threshold=3): 
    return math.hypot(dx - my_x, dz - my_z) < threshold

# -----------------------------
# Fonction principale
# -----------------------------

def decide_action(perception):
    UnityEngine.Debug.Log("Version: 4")
    # 1. RÉCUPÉRATION DES DONNÉES
    my_id = str(perception.get('my_id', ''))
    my_x = float(perception.get('my_x', 0.0))
    my_z = float(perception.get('my_z', 0.0))
    is_carrying = perception.get('is_carrying', 0) == 1
    items = perception.get('items', [])
    deposites = perception.get('deposites',[])
    all_agents = perception.get('all_agents', {})
    obstacles = perception.get('obstacles', [])
    
    # RECUPÉRATION DE LA CIBLE ACTUELLE (Verrouillage)
    # On récupère l'ID de ce que le robot visait à la frame précédente
    current_target_id = str(perception.get('current_target_id', "0"))

    # 2. DETECTION DES RÉSERVATIONS ET AGENTS LIBRES
    reserved_ids = []
    free_agents_data = []

    for a_id, data in all_agents.items():
        t_id = str(data.get('current_target_id', "0"))
        if t_id != "0":
            reserved_ids.append(t_id)
        
        if not data.get('is_carrying', False) and t_id == "0":
            free_agents_data.append({
                'id': str(a_id),
                'x': float(data.get('x', 0.0)),
                'z': float(data.get('z', 0.0))
            })

    # 3. LOGIQUE DE MISSION (AVEC VERROUILLAGE)
    target_x = perception.get("spawn_x", 0.0)
    target_z = perception.get("spawn_z", 0.0)
    target_id = current_target_id
    
    if not is_carrying:
        # --- ÉTAPE A : VÉRIFIER SI MA CIBLE ACTUELLE EST TOUJOURS VALIDE ---
        target_item = next((it for it in items if str(it.get('id')) == current_target_id), None)
        
        if target_item:
            # Je garde ma cible actuelle, je ne change pas !
            target_x, target_z, target_id = target_item['x'], target_item['z'], target_item['id']
        
        else:
            # --- ÉTAPE B : SI PAS DE CIBLE OU CIBLE DISPARUE, EN CHERCHER UNE NOUVELLE ---
            available_items = [it for it in items if str(it.get('id')) not in reserved_ids]
            if available_items:
                available_items.sort(key=lambda it: (it['x'] - my_x)**2 + (it['z'] - my_z)**2)
                
                for it in available_items:
                    my_dist_sq = (it['x'] - my_x)**2 + (it['z'] - my_z)**2
                    am_i_the_closest = True
                    
                    for other in free_agents_data:
                        if other['id'] == my_id: continue
                        other_dist_sq = (it['x'] - other['x'])**2 + (it['z'] - other['z'])**2
                        if other_dist_sq < my_dist_sq:
                            am_i_the_closest = False
                            break
                    
                    if am_i_the_closest:
                        target_x, target_z, target_id = it['x'], it['z'], it['id']
                        break             
    else:
        # LOGIQUE DE DÉPOSE (Une fois l'item ramassé)
        target_id = "0" # On reset l'ID de l'item car on le porte
        if deposites:
            closest_deposite = min(deposites, key=lambda s: (s['x'] - my_x)**2 + (s['z'] - my_z)**2)
            target_x, target_z = closest_deposite['x'], closest_deposite['z']

    # 4. CALCUL ACTIONS ET ÉVITEMENT (Reste identique)
    dist = math.hypot(target_x - my_x, target_z - my_z)
    movement_type = "walk"
    action_type = "none"

    if dist < 0.3: 
        if not is_carrying and near_item(my_x, my_z, items):
            movement_type = "stop"
            action_type = "pick_up"
        elif is_carrying and near_delivery(my_x, my_z, target_x, target_z):
            movement_type = "stop"
            action_type = "drop_off"

    # Évitement
    sep_x, sep_z = 0.0, 0.0
    if dist > 1.2: 
        for obs in obstacles:
            dx, dz = my_x - obs.get('x', 0.0), my_z - obs.get('z', 0.0)
            d = math.hypot(dx, dz)
            if 0 < d < 2.5:
                strength = (2.5 - d) / 2.5
                sep_x += (dx / d) * strength * 3.0
                sep_z += (dz / d) * strength * 3.0

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

# =========================
# ===== BATCH WRAPPER =====
# =========================

def decide_all(all_perceptions):
    """
    Called by Unity once per decision cycle with ALL agents' perception data.
    Wraps the existing decide_action function for batch compatibility.
    """
    all_decisions = {}
    
    for agent_id, perception in all_perceptions.items():
        try:
            decision = decide_action(perception)
            all_decisions[agent_id] = decision
        except Exception as e:
            UnityEngine.Debug.LogError(f"Error processing {agent_id}: {e}")
            # Return safe default on error
            all_decisions[agent_id] = {
                "movement": {
                    "type": "stop",
                    "target_x": 0.0,
                    "target_z": 0.0
                },
                "action": {
                    "type": "none",
                    "target_id": "0"
                }
            }
    
    return all_decisions