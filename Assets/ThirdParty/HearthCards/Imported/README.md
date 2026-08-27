# Imported

`Raw/*.webp` converted to PNG, losslessly, by
`Tools/HearthCards/fetch_card_assets.py convert`.

Unity does not read `.webp`, so this folder is what the project actually uses.
Nothing here is edited by hand: it is generated from `Raw/`, and deleting it
costs one command. The conversion is RGBA and lossless — a frame is mostly
transparent, and a mode that dropped the alpha would fill the window the artwork
shows through.

`Conquest of Hearthstone → Import HearthCards Components` reads the manifest,
sets each file's importer settings and writes the catalog rows.
