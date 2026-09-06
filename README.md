# Asset Migration Utility
Migrate assets from one source to another seamlessly.

Since the game update 1.5.2f1, every placed assets are linked to a mod ID. This means another version of the same asset (either local or from the PDX Mods) will not be recognized as the same asset by the game.

This mod helps you migrate your assets from one source to another by scanning through all assets on your save and replace it with assets with the same name.

This is especially useful when you want to switch from local assets to PDX Mods assets or vice versa, or when a mod author has changed the mod ID of their mod. For example, when Static Ploppables moved from "Code Mod" variants to "Asset Pack" variants.

Additionally, this mod also helps you clean up transport routes that are linked to unsubscribed/moved assets. The route's vehicle selection dropdown will no longer show the missing items, and if there's no available vehicles already selected, you will need to reselect one, or the game will use one from the pool of all currently available vehicles.

### How to use
First unsubscribe from the old version of a mod, and subscribe to the new version of the mod. As long as the Prefab names are same (or the new Prefab has the old name as Obsolete Identifiers), this mod will replace the missing items placed on the map with new corresponding items of the same name.
This is also helpful if you're using a PDX Mods version of a mod and now wants to use local version of the assets.

### Notes
This mod is not designed to remove references to all missing assets. It is only for migrating to new version of an asset. A button to cleanup obsolete entites is provided in the Options menu, but it triggers the same process as the Developer Mode's "Cleanup Obsolete Entities" function.

## License Notes
This project is licensed under GPLv3 (see LICENSE). A quick summary of what that means in practice:

- You're free to use, modify, and redistribute this code.
- Any modified version you distribute must also be licensed under GPLv3 (copyleft). This means you can't use this code in a closed-source project.
- Please retain attribution to the original author when redistributing or forking.

### Forks and Redistribution
This mod is distributed exclusively via Paradox Mods, and I actively maintain it there.

If you'd like to contribute, please consider submitting a PR or reaching out instead of publishing a separate copy or fork.

I kindly ask that you do **not**:
- Upload this mod, or any fork of it, to Nexus Mods or any platform other than Paradox Mods, under any circumstances.
- Publish it as a separate listing on Paradox Mods, unless the original mod is abandoned and I'm unresponsive to contact for an extended period.

If you do publish a fork under those circumstances, please:
- Clearly mark it as a fork/unofficial version (not the original),
- Link back to this repository and credit the original work,
- Follow GPLv3's requirements for source availability and licensing.

This is a request from the maintainer, not an added legal restriction beyond what GPLv3 already requires. It does not modify or limit any rights granted under the GPLv3 license, which remains the sole binding license for this Software.

Reposting an actively maintained mod under a new listing, without need, fragments the community and support for users.