import random

def decide_action(perception):
    # 1. RÉCUPÉRATION DE LA POSITION ACTUELLE
    my_x = perception.get('my_x', 0.0)
    my_z = perception.get('my_z', 0.0)
    
    # 2. DÉFINITION DES LIMITES (Basées sur ton sol centré en 0,0)
    # Puisque ton sol a une Scale de 2, il fait 20 unités (-10 à +10)
    limit_min = -9.5  # On garde une petite marge de sécurité
    limit_max = 9.5
    
    # 3. GÉNÉRATION D'UN MOUVEMENT ALÉATOIRE
    # On choisit un déplacement entre -2 et +2 unités sur chaque axe
    target_x = my_x + random.uniform(-2.0, 2.0)
    target_z = my_z + random.uniform(-2.0, 2.0)

    # 4. VÉRIFICATION DU CONFINEMENT (Clamping)
    # Si la cible sort des limites, on la "pousse" vers l'intérieur
    if target_x < limit_min: target_x = limit_min + 1.0
    if target_x > limit_max: target_x = limit_max - 1.0
    
    if target_z < limit_min: target_z = limit_min + 1.0
    if target_z > limit_max: target_z = limit_max - 1.0

    # 5. RÉPONSE À UNITY
    return {
        "movement": {
            "type": "walk",
            "target_x": float(target_x),
            "target_z": float(target_z)
        },
        "action": {
            "type": "none"
        }
    }