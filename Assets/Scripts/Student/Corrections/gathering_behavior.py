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

# Points d'intérêt dans le monde
POINTS_OF_INTEREST = [
    {'x': 15.0, 'z': 15.0, 'name': 'Marché', 'attraction': 0.8},
    {'x': -15.0, 'z': 15.0, 'name': 'Fontaine', 'attraction': 0.6},
    {'x': 15.0, 'z': -15.0, 'name': 'Temple', 'attraction': 0.5},
    {'x': -15.0, 'z': -15.0, 'name': 'Taverne', 'attraction': 0.7},
]

# Durée de séjour aux POI
STAY_DURATION_MIN = 5.0
STAY_DURATION_MAX = 15.0

DECISION_INTERVAL = 0.5  # Intervalle entre décisions (ne pas modifier)

POI_SPREAD_RADIUS = 4.0       # Rayon de dispersion
ARRIVAL_THRESHOLD = 2.5       # Distance pour considérer qu'on est arrivé

# Comportements contagieux
SNEEZE_CHANCE_BASE = 0.2      # Chance de base d'éternuer
COUGH_CHANCE_BASE = 0.15      # Chance de base de tousser
CROWD_BONUS = 0.1             # Bonus par agent proche

# Évitement
HEALTHY_AVOIDANCE_ENABLED = True
AVOIDANCE_DISTANCE = 5.0
FLEE_CHANCE = 0.3

# Quarantaine
SICK_SEEK_QUARANTINE = True
QUARANTINE_HEALTH_THRESHOLD = 50

agent_states = {}  # {agent_id: {'target_x', 'target_z', 'has_target'}}

# =========================
# ===== BATCH WRAPPER =====
# =========================

def decide_all(all_perceptions):
    """
    Point d'entrée appelé par Unity.
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
    my_id = perception['my_id']
    my_x = perception['my_x']
    my_z = perception['my_z']
    health = perception['health']
    is_contagious = perception['is_contagious'] == 1
    infection_stage = perception['infection_stage']

    
    # Initialiser l'état
    if my_id not in agent_states:
        agent_states[my_id] = {
            'current_poi': None,
            'target_x': my_x,
            'target_z': my_z,
            'is_traveling': False,
            'time_at_poi': 0.0,
            'stay_duration': 0.0
        }
    
    state = agent_states[my_id]

    # === PRIORITÉ 1: Malades cherchent quarantaine ===
    if SICK_SEEK_QUARANTINE and is_contagious and health < QUARANTINE_HEALTH_THRESHOLD:
        return build_response(my_x, my_z, "stop", "quarantine")
    
    # === PRIORITÉ 2: Sains évitent les contagieux ===
    if HEALTHY_AVOIDANCE_ENABLED and not is_contagious and infection_stage == 0:
        contagious_nearby = detect_contagious_nearby(perception)
        if contagious_nearby and random.random() < FLEE_CHANCE:
            flee_x, flee_z = calculate_flee_direction(my_x, my_z, contagious_nearby, perception)
            return build_response(flee_x, flee_z, "run", "none")
    
    # Vérifier si on est arrivé à destination
    dist_to_target = calculate_distance(my_x, my_z, state['target_x'], state['target_z'])
    
    if dist_to_target < ARRIVAL_THRESHOLD and state['is_traveling']:
        # On vient d'arriver
        state['is_traveling'] = False
        state['time_at_poi'] = 0.0
        state['target_x'] = my_x
        state['target_z'] = my_z
    
    # Incrémenter le temps si on est à un POI
    if not state['is_traveling']:
        state['time_at_poi'] += DECISION_INTERVAL
    
    # Temps écoulé ? Choisir nouveau POI
    if not state['is_traveling']:
        if state['current_poi'] is None or state['time_at_poi'] >= state['stay_duration']:
            # Choisir nouveau POI
            new_poi = choose_new_poi(my_x, my_z, state['current_poi'])
            state['current_poi'] = new_poi
            
            # Position dispersée autour du POI
            state['target_x'], state['target_z'] = generate_random_position_around(
                new_poi['x'],
                new_poi['z'],
                POI_SPREAD_RADIUS
            )
            
            state['is_traveling'] = True
            state['time_at_poi'] = 0.0
            state['stay_duration'] = random.uniform(STAY_DURATION_MIN, STAY_DURATION_MAX)
    
    # Déterminer le type de mouvement
    if state['is_traveling']:
        movement_type = "run"
    elif dist_to_target < 0.5:
        movement_type = "stop"
    else:
        movement_type = "walk"

    # Déterminer l'action
    action_type = "none"
    if is_contagious:
        action_type = decide_contagious_action(perception)
    
    return build_response(state['target_x'], state['target_z'], movement_type, action_type)


# =========================
# ===== UTILITIES =========
# =========================

def build_response(target_x, target_z, movement_type, action_type):
    """
    Construit la réponse pour Unity.
    """
    return {
        "movement": {
            "type": movement_type,
            "target_x": target_x,
            "target_z": target_z
        },
        "action": {
            "type": action_type,
            "target_id": "",
            "parameters": {}
        }
    }

def generate_random_position_around(center_x, center_z, radius):
    """
    Génère une position aléatoire dans un rayon autour d'un centre.
    """
    angle = random.uniform(0, 2 * math.pi)
    distance = random.uniform(0, radius)
    
    offset_x = math.cos(angle) * distance
    offset_z = math.sin(angle) * distance
    
    return center_x + offset_x, center_z + offset_z


def calculate_distance(x1, z1, x2, z2):
    """Distance euclidienne 2D."""
    dx = x2 - x1
    dz = z2 - z1
    return math.sqrt(dx * dx + dz * dz)


def choose_random_poi():
    """Choisit un point d'intérêt au hasard."""
    return random.choice(POINTS_OF_INTEREST)

def decide_contagious_action(perception):
    """
    Décider si l'agent doit éternuer ou tousser.
    """
    heard_count = perception['heard_count']
    visible_count = perception['visible_count']
    
    # Pas de monde ? Pas besoin d'action
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

def choose_new_poi(current_x, current_z, current_poi):
    """
    Choisir un POI basé sur distance, attraction et variété.
    """
    weights = []
    
    for poi in POINTS_OF_INTEREST:
        # Ne pas rechoisir le POI actuel
        if current_poi and poi['name'] == current_poi['name']:
            weights.append(0.0)
            continue
        
        # Score basé sur distance (plus proche = mieux)
        dist = calculate_distance(current_x, current_z, poi['x'], poi['z'])
        dist_score = 1.0 / (1.0 + dist * 0.1)
        
        # Score basé sur attraction
        attraction_score = poi['attraction']
        
        # Score total
        total_score = dist_score * attraction_score
        weights.append(total_score)
    
    # Choix pondéré
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

def detect_contagious_nearby(perception):
    """
    Détecte les agents contagieux à proximité.
    Retourne une liste de positions ou None.
    """
    contagious = []
    
    visible_agents = perception.get('visible_agents', {})
    for agent_id, agent_data in visible_agents.items():
        if agent_data['distance'] < AVOIDANCE_DISTANCE:
            # Heuristique : si l'agent éternue/tousse, il est contagieux
            if agent_data['current_action'] in ['sneeze', 'cough']:
                contagious.append((agent_data['x'], agent_data['z']))
    
    return contagious if contagious else None

def calculate_flee_direction(my_x, my_z, contagious_positions, perception):
    """
    Calcule une direction de fuite opposée aux contagieux.
    """
    # Position moyenne des contagieux
    avg_x = sum(pos[0] for pos in contagious_positions) / len(contagious_positions)
    avg_z = sum(pos[1] for pos in contagious_positions) / len(contagious_positions)
    
    # Direction opposée
    flee_dir_x = my_x - avg_x
    flee_dir_z = my_z - avg_z
    
    # Normaliser
    magnitude = math.sqrt(flee_dir_x**2 + flee_dir_z**2)
    if magnitude > 0:
        flee_dir_x /= magnitude
        flee_dir_z /= magnitude
    else:
        # Direction aléatoire si pile dessus
        angle = random.uniform(0, 2 * math.pi)
        flee_dir_x = math.cos(angle)
        flee_dir_z = math.sin(angle)
    
    # Fuir à 10 unités
    flee_x = my_x + flee_dir_x * 10.0
    flee_z = my_z + flee_dir_z * 10.0
    
    return flee_x, flee_z