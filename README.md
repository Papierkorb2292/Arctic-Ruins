# Arctic Ruins
This Shapez2 mod was developed for the Shapez2 modding contest. It features a new game mode "Arctic Ruins", which turns the usual shapez formula on its head: Instead of assembling complex shapes out of simple base shapes, this game mode is all about disassembling complex shapes from the vortex back into simple shapes that can be put into the asteroids.

You can get the mod on the Steam Workshop here: https://steamcommunity.com/sharedfiles/filedetails/?id=3758107959

To make this possible, the mod includes the following few features:
- The "Arctic Ruins" game mode selection in the main menu, with the custom scenario "The Other Side"
- An intro sequence with sound effects that plays when starting the new game mode
- New factory buildings: The Stabilizer (for storing shapes in asteroids), and the Layer Detacher (unstacking shapes)
- New functional buildings placed by the map generator: The Data Fragment (unlocks upgrades), and the Communication Relay (Select which shapes the vortex should output)
- New decorational buildings placed by the map generator: Rubble and Ruin Wall
- All buildings have custom models (and potentially animations)
- Belt Catchers can be placed next to the vortex to receive the configured shapes on a round-robin basis
- A snow storm that covers most of the map, making it impossible to interact with buildings beneath it. It is pushed away by storing shapes in asteroids
- Every asteroid has a progress bar that fills up as shapes are stored in the asteroid. The storm surrounding the asteroid up until the next asteroids around it slowly disappears while the progress bar fills up
- The map generator randomly places ruin blueprints on the map and randomly adds Data Fragments to them
- A custom shape generator for asteroids that takes into account which shapes can be made with the available milestones
- Milestones can be unlocked if all their upgrades have been found, and they reward the player with new shapes (instead of the other way around)
- While playing the custom game mode, the orange color of all building and island models is replaced with cyan 

### AI Usage

The code for this mod was written by me with the help of inline autocompletion by AI (like the original GitHub Copilot). This means I came up with the logic, I did the reverse engineering, and throughout the entire project I did not write a single prompt.

Additionally, I sometimes used my search engine's AI overview if it happened to be useful. Also, it's very likely that some online articles I read (about C#, Unity, Blender, Gimp, Audacity, whatever) were AI generated, but that's the internet we live in..

All models/textures/shaders/sounds/... were made by me using the appropriate tools (and sometimes based on assets from the base game) without any AI involvement.

### License

Everything I made for this mod is licensed under the MIT License (see [LICENSE](LICENSE))

Also, the mod is based on the [Shapez2 example mods](https://github.com/tobspr-games/shapez2-mod-samples), and it still includes a little bit of code and assets from them. Those are also licensed under the MIT License by tobspr Games ([Mod Samples License](LICENSE-shapez2-mod-samples))  