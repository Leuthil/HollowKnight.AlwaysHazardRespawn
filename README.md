# Always Hazard Respawn
Hollow Knight mod to always respawn at hazard respawn locations after death.

Hazard respawn locations are the places you respawn after dying due to "hazards", such as spikes. This mod essentially respawns you as close as possible to where you died by leveraging these hazard spawn locations.

## Installation
This mod is supported by both HK Modding API and BepInEx5. The same .dll file will work with both.

## Notes
**Be aware:** On death, it is possible to respawn "ahead" of your Shade. You may need to backtrack to get to it. This is especially common for boss fights. Please note this before rushing into a boss fight, as you may get locked in with the boss and be unable to get your lost geo.

***Disclaimer:** This mod has not been thoroughly tested throughout all areas of the game. For this mod to work, it temporarily overwrites the game's respawn information during death. This respawn information gets saved as part of your save file. It is therefore possible for this mod to corrupt your save game. Many measures have been taken to ensure this does not happen. If corruption does occur, it can likely be remedied with a mod like Benchwarp. In order to minimize the chance of corruption, this mod constantly monitors when the game writes new respawn information and keeps track of it. Whenever a game save is performed, the original game's respawn information is restored. In addition, respawn information is only modified during the death and respawn process, and the original game's respawn information is subsequently restored.*