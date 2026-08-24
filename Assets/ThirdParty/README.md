# ThirdParty

Assets externes importes dans le projet.

## Regles

1. **Un dossier par editeur, puis par pack** :
   `Assets/ThirdParty/<Vendor>/<Pack>/`
   Exemple : `Assets/ThirdParty/Kenney/BoardGameIcons/`

2. **Ne jamais melanger** du contenu tiers avec `Assets/_Project/`.
   Cette separation permet de mettre a jour, remplacer ou supprimer un
   pack sans toucher a notre propre contenu.

3. **Toute importation exige une fiche de licence** dans
   `Assets/ThirdParty/LICENSES/` (voir le README de ce dossier).

4. **Importation selective** : ne pas importer les dossiers `Demo/`,
   `Examples/`, `Documentation/` et scenes de demonstration des packs.
   Pour les gros packs, importer d'abord dans un projet bac a sable et
   ne recopier ici que les prefabs/materiaux reellement utilises.

5. **Un asset entre dans le projet uniquement quand il est utilise**
   dans une scene ou un prefab. Pas de stockage "au cas ou".

## Sources interdites

Aucun asset extrait des jeux Blizzard (Hearthstone, World of Warcraft) :
ni texture, ni modele, ni son, ni police, ni illustration. Aucune
redistribution illegale, aucune ressource dont la licence ne peut pas
etre determinee.
