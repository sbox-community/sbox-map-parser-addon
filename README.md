# sbox-map-parser-addon
Map Parser for s&amp;box
For now, only supported to GoldSrc
There are ~17k map you can spawn and play them via the map mirror.

![11](https://user-images.githubusercontent.com/48884110/212428265-cf7c902b-eb8e-41bf-9e13-e2ca85624ad2.png)
![22](https://user-images.githubusercontent.com/48884110/212428295-a7270f2a-004c-4646-9405-5c65943157f8.png)
![33](https://user-images.githubusercontent.com/48884110/212428304-21b57477-164a-4311-997d-de318f038c05.png)

Known issues;
- Physical and walking animation issues on the map
- Lighting and non-transparent texture problems ( when figuring out to implement the shader with map's lightmap, will be fixed )
- Freezing until creating textures are finished ( waiting for above shader implementation ) 
- UI delay/freezing problems ( when addons system get fixed, might be fixed )

Posible updates;
- Lightmap ( already implemented, waiting for the shader )
- Removing of env_sprite, water or trigger's physics
- Functionality of some entities ( like ambient sounds, buttons, doors or lights )
- Sky ( need a shader or render hook )
- Demo player ( for goldsrc )
- Model and Particle parser ( for goldsrc )
- Another game map parsers ( maybe source engine? )

How to publish my map;
TODO

Credits;
- Mainly BSP Parser code from https://github.com/magcius/noclip.website/
