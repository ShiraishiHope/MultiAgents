"""
Gathering Behavior - Points d'Intérêt
Les agents se rassemblent autour de points d'intérêt pour favoriser la propagation.
Démontre l'impact des rassemblements sur la transmission d'épidémie.
"""

import random
import math

# =========================
# ===== CONFIGURATION =====
# =========================

# Points d'intérêt dans le monde (à ajuster selon votre scène Unity)
POINTS_OF_INTEREST = [
    {'x': 0.0, 'z': 0.0, 'name': 'Centre Village', 'attraction': 1.0},
    {'x': 15.0, 'z': 15.0, 'name': 'Marché', 'attraction': 0.8},
    {'x': -15.0, 'z': 15.0, 'name': 'Fontaine', 'attraction': 0.6},
    {'x': 15.0, 'z': -15.0, 'name': 'Temple', 'attraction': 0.5},
    {'x': -15.0, 'z': -15.0, 'name': 'Taverne', 'attraction': 0.7},
]

# Paramètres de comportement
STAY_DURATION_MIN = 5.0       # Temps minimum à un point d'intérêt
STAY_DURATION_MAX = 15.0      # Temps maximum à un point d'intérêt
DECISION_INTERVAL = 0.5       # Intervalle de décision (doit matcher C#)
ARRIVAL_THRESHOLD = 2.5       # Distance pour considérer qu'on est arrivé
POI_SPREAD_RADIUS = 4.0       # Rayon de dispersion autour du POI (évite empilements)

# Comportements contagieux
SNEEZE_CHANCE_BASE = 0.2      # Chance de base d'éternuer
COUGH_CHANCE_BASE = 0.15      # Chance de base de tousser
CROWD_BONUS = 0.1             # Bonus par agent proche (max +50%)

# Comportement des agents sains
HEALTHY_AVOIDANCE_ENABLED = True   # Les agents sains évitent les contagieux
AVOIDANCE_DISTANCE = 5.0          # Distance de détection des contagieux
FLEE_CHANCE = 0.3                 # Chance de fuir si contagieux détecté

# Comportement des agents malades
SICK_SEEK_QUARANTINE = True       # Les malades cherchent la quarantaine
QUARANTINE_HEALTH_THRESHOLD = 50  # Santé en dessous de laquelle on cherche la quarantaine


# =========================
# ===== GLOBAL STATE ======
# =========================

agent_states = {}  # {agent_id: {'current_poi', 'time_at_poi', 'is_traveling', ...}}


# =========================
# ===== BATCH WRAPPER =====
# =========================

def decide_all(all_perceptions):
    """
    Point d'entrée appelé par Unity avec toutes les perceptions des agents.
    """
    all_decisions = {}
    
    for agent_id, perception in all_perceptions.items():
        try:
            decision = decide_action(perception)
            all_decisions[agent_id] = decision
        except Exception as e:
            print(f"[ERROR] Agent {agent_id}: {e}")
            all_decisions[agent_id] = build_response(
                perception['my_x'], 
                perception['my_z'], 
                "stop", 
                "none"
            )
    
    return all_decisions


# =========================
# ===== AGENT LOGIC =======
# =========================

def decide_action(perception):
    """
    Décision principale pour un agent.
    
    Comportement:
    1. Si contagieux et santé basse → chercher quarantaine
    2. Si sain et contagieux détecté → éviter
    3. Sinon → voyager entre points d'intérêt
    4. Aux points d'intérêt → éternuer/tousser si contagieux
    """
    my_id = perception['my_id']
    my_x = perception['my_x']
    my_z = perception['my_z']
    health = perception['health']
    is_contagious = perception['is_contagious'] == 1
    infection_stage = perception['infection_stage']
    my_spawn_x = perception['spawn_x']
    my_spawn_z = perception['spawn_z']
    
    # Initialiser l'état de l'agent si nécessaire
    if my_id not in agent_states:
        agent_states[my_id] = {
            'current_poi': None,
            'time_at_poi': 0.0,
            'is_traveling': False,
            'target_x': my_x,
            'target_z': my_z
        }
    
    state = agent_states[my_id]
    #state['target_x'] = my_spawn_x
    #state['target_z'] = my_spawn_z

    # === PRIORITÉ 1: Agents malades avec santé basse cherchent quarantaine ===
    if SICK_SEEK_QUARANTINE and is_contagious and health < QUARANTINE_HEALTH_THRESHOLD:
        return build_response(my_x, my_z, "stop", "quarantine")
    
    # === PRIORITÉ 2: Agents sains évitent les contagieux ===
    if HEALTHY_AVOIDANCE_ENABLED and not is_contagious and infection_stage == 0:
        contagious_nearby = detect_contagious_nearby(perception)
        if contagious_nearby and random.random() < FLEE_CHANCE:
            flee_x, flee_z = calculate_flee_direction(
                my_x, my_z, contagious_nearby, perception
            )
            return build_response(flee_x, flee_z, "run", "none")
    
    # === COMPORTEMENT NORMAL: Rassemblement aux points d'intérêt ===
    
    # Vérifier si on est arrivé à destination
    dist_to_target = calculate_distance(my_x, my_z, state['target_x'], state['target_z'])
    
    if dist_to_target < ARRIVAL_THRESHOLD and state['is_traveling']:
        state['is_traveling'] = False
        state['time_at_poi'] = 0.0
        # Fixer la position exacte pour éviter les micro-mouvements
        state['target_x'] = my_x
        state['target_z'] = my_z
    
    # Incrémenter le temps passé au POI seulement si on n'est pas en train de voyager
    if not state['is_traveling']:
        state['time_at_poi'] += DECISION_INTERVAL
    
    # Temps de rester au point d'intérêt écoulé ? Choisir nouveau POI
    if not state['is_traveling']:
        if state['current_poi'] is None or state['time_at_poi'] >= state.get('stay_duration', 0):
            # Choisir un nouveau point d'intérêt
            new_poi = choose_new_poi(my_x, my_z, state['current_poi'])
            state['current_poi'] = new_poi
            
            # Ajouter un offset aléatoire autour du POI pour éviter les empilements
            angle = random.uniform(0, 2 * math.pi)
            radius = random.uniform(0, POI_SPREAD_RADIUS)
            offset_x = math.cos(angle) * radius
            offset_z = math.sin(angle) * radius
            
            state['target_x'] = new_poi['x'] + offset_x
            state['target_z'] = new_poi['z'] + offset_z
            state['is_traveling'] = True
            state['time_at_poi'] = 0.0
            state['stay_duration'] = random.uniform(STAY_DURATION_MIN, STAY_DURATION_MAX)
            
            # Debug
            # print(f"{my_id} heading to {new_poi['name']}")
    
    # Déterminer l'action (sneeze/cough si contagieux et du monde autour)
    action_type = "none"
    if is_contagious:
        action_type = decide_contagious_action(perception)
    
    # Mouvement
    if state['is_traveling']:
        movement_type = "run"
    elif dist_to_target < 0.5:  # Vraiment arrivé, on s'arrête complètement
        movement_type = "stop"
    else:
        movement_type = "walk"  # Petits ajustements si nécessaire
    
    return build_response(state['target_x'], state['target_z'], movement_type, action_type)


# =========================
# ===== POI SELECTION =====
# =========================

def choose_new_poi(current_x, current_z, current_poi):
    """
    Choisir un nouveau point d'intérêt basé sur:
    - Distance (préférer ceux pas trop loin)
    - Attraction (certains POI plus populaires)
    - Ne pas revenir au même immédiatement
    """
    weights = []
    
    for poi in POINTS_OF_INTEREST:
        # Ne pas choisir le POI actuel
        if current_poi and poi['name'] == current_poi['name']:
            weights.append(0.0)
            continue
        
        # Calculer score basé sur distance et attraction
        dist = calculate_distance(current_x, current_z, poi['x'], poi['z'])
        
        # Score inversement proportionnel à la distance, pondéré par l'attraction
        # Distance normalisée pour éviter les divisions par zéro
        dist_score = 1.0 / (1.0 + dist * 0.1)  # Plus proche = meilleur score
        attraction_score = poi['attraction']
        
        total_score = dist_score * attraction_score
        weights.append(total_score)
    
    # Choix pondéré aléatoire
    total_weight = sum(weights)
    if total_weight == 0:
        return random.choice(POINTS_OF_INTEREST)
    
    rand = random.uniform(0, total_weight)
    cumulative = 0
    
    for poi, weight in zip(POINTS_OF_INTEREST, weights):
        cumulative += weight
        if rand <= cumulative:
            return poi
    
    return POINTS_OF_INTEREST[-1]


# =========================
# ===== CONTAGION =========
# =========================

def decide_contagious_action(perception):
    """
    Décider si l'agent contagieux doit éternuer/tousser.
    Plus il y a de monde autour, plus il a de chances de le faire.
    """
    heard_count = perception['heard_count']
    visible_count = perception['visible_count']
    
    # Pas de monde autour ? Pas besoin d'éternuer
    if heard_count == 0 and visible_count == 0:
        return "none"
    
    # Bonus de probabilité basé sur la foule
    crowd_size = max(heard_count, visible_count)
    crowd_multiplier = min(1.5, 1.0 + (crowd_size * CROWD_BONUS))
    
    sneeze_chance = SNEEZE_CHANCE_BASE * crowd_multiplier
    cough_chance = COUGH_CHANCE_BASE * crowd_multiplier
    
    roll = random.random()
    
    if roll < sneeze_chance:
        return "sneeze"
    elif roll < sneeze_chance + cough_chance:
        return "cough"
    
    return "none"


# =========================
# ===== AVOIDANCE =========
# =========================

def detect_contagious_nearby(perception):
    """
    Détecte si des agents contagieux sont à proximité.
    Retourne une liste de positions d'agents contagieux.
    """
    contagious = []
    
    # Vérifier les agents visibles
    visible_agents = perception.get('visible_agents', {})
    for agent_id, agent_data in visible_agents.items():
        if agent_data['distance'] < AVOIDANCE_DISTANCE:
            # On ne peut pas voir directement si l'agent est contagieux
            # mais on peut utiliser des heuristiques (action = sneeze/cough)
            if agent_data['current_action'] in ['sneeze', 'cough']:
                contagious.append((agent_data['x'], agent_data['z']))
    
    return contagious if contagious else None


def calculate_flee_direction(my_x, my_z, contagious_positions, perception):
    """
    Calcule une direction de fuite opposée aux agents contagieux.
    """
    # Calculer le vecteur moyen vers les contagieux
    avg_x = sum(pos[0] for pos in contagious_positions) / len(contagious_positions)
    avg_z = sum(pos[1] for pos in contagious_positions) / len(contagious_positions)
    
    # Direction opposée
    flee_dir_x = my_x - avg_x
    flee_dir_z = my_z - avg_z
    
    # Normaliser et étendre
    magnitude = math.sqrt(flee_dir_x**2 + flee_dir_z**2)
    if magnitude > 0:
        flee_dir_x /= magnitude
        flee_dir_z /= magnitude
    else:
        # Si on est pile sur la position, fuir dans une direction aléatoire
        angle = random.uniform(0, 2 * math.pi)
        flee_dir_x = math.cos(angle)
        flee_dir_z = math.sin(angle)
    
    # Fuir à une distance de 10 unités
    flee_x = my_x + flee_dir_x * 10.0
    flee_z = my_z + flee_dir_z * 10.0
    
    return flee_x, flee_z


# =========================
# ===== UTILITIES =========
# =========================

def calculate_distance(x1, z1, x2, z2):
    """Distance euclidienne 2D."""
    dx = x2 - x1
    dz = z2 - z1
    return math.sqrt(dx * dx + dz * dz)


def build_response(target_x, target_z, movement_type, action_type, target_id=""):
    """
    Construit la réponse dans le format attendu par Unity.
    
    Movement types: "walk", "run", "stop", "none"
    Action types: "none", "sneeze", "cough", "quarantine", etc.
    """
    return {
        "movement": {
            "type": movement_type,
            "target_x": target_x,
            "target_z": target_z
        },
        "action": {
            "type": action_type,
            "target_id": target_id,
            "parameters": {}
        }
    }


# =========================
# ===== DEBUG =============
# =========================

def print_statistics():
    """Affiche des statistiques sur l'état des agents (optionnel)."""
    if not agent_states:
        return
    
    traveling = sum(1 for s in agent_states.values() if s['is_traveling'])
    at_poi = len(agent_states) - traveling
    
    print(f"[Stats] Agents: {len(agent_states)} | Traveling: {traveling} | At POI: {at_poi}")
