# ThirdParty

External assets imported into the project.

## Rules

1. **One folder per vendor, then per pack**:
   `Assets/ThirdParty/<Vendor>/<Pack>/`
   Example: `Assets/ThirdParty/Kenney/BoardGameIcons/`

2. **Never mix** third-party content with `Assets/_Project/`.
   This separation lets us update, replace or delete a pack without
   touching our own content.

3. **Every import requires a license record** in
   `Assets/ThirdParty/LICENSES/` (see that folder's README).

4. **Selective import**: do not import a pack's `Demo/`, `Examples/`,
   `Documentation/` folders or demo scenes. For large packs, import into a
   scratch project first and copy over only the prefabs and materials we
   actually use.

5. **An asset enters the project only once it is used** in a scene or a
   prefab. No "just in case" storage.

## Forbidden sources

No asset extracted from Blizzard games (Hearthstone, World of Warcraft):
no texture, model, sound, font or artwork. No illegally redistributed
content, and no resource whose license cannot be determined.
