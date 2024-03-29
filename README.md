# sbox-map-parser-addon
Map Parser for s&amp;box

For now, only supported to GoldSrc. There are ~17k map you can spawn and play them via the map mirror.

![11](https://user-images.githubusercontent.com/48884110/212428265-cf7c902b-eb8e-41bf-9e13-e2ca85624ad2.png)
![22](https://user-images.githubusercontent.com/48884110/212428295-a7270f2a-004c-4646-9405-5c65943157f8.png)

![hh](https://user-images.githubusercontent.com/48884110/219905268-1642e9ef-374d-4c0f-b445-ad54bdbd8d9f.png)
![222](https://user-images.githubusercontent.com/48884110/219905269-87aef5d9-aa49-4085-8276-1a9e8387c3bc.png)
![33333](https://user-images.githubusercontent.com/48884110/219905271-8e3ccf91-bffd-4de7-b1ba-cb5a9230b136.png)


Known issues;
- Physical and walking animation issues on the spawned map
- Some issues on physics collision and mesh rendering
- Need HTTP request allowed for the mirrors [https://wiki.facepunch.com/sbox/http](https://wiki.facepunch.com/sbox/http)
___

Possible updates;
- Functionality of some entities ( like ambient sounds, buttons, doors or lights )
- Frustum and backface culling
- Demo player ( for goldsrc )
- Particle parser ( for goldsrc )
- Another game map parsers ( maybe source engine? )

___

How to publish my map;
- Create new addon project on s&box
- Copy & paste your .bsp and .wad ( and also the other relevelant files like .res, .txt, .mdl, .tga, overview files or the all parent files, because in the future we can use them for functionalities of the map ) files into the addon folder.
- Rename all your copied files as ".txt" like; "fy_iceworld.bsp.txt" (do not rename .txt files) For now, there is no way to upload custom content files as far as I know.
- Create a text file as named as "organizationIdent.addonIdent" to addon's root folder, for example (sboxcommunity.mp__fy_poolday.txt);

```json
[
    {
        "name": "Fy_poolday From CS 1.6",
        "desp": "Have fun!",
        "engine": "goldsrc",
        "game": "cs16",
        "bsp": "/maps/fy_poolday.bsp",
        "offset": "0,0,0",
        "angles": "0,0,0",
        "dependencies": ""
    }
]
```
- Upload and add these tags from your project page's the section of "Edit details" on Asset.party; "mapparser cs16 goldsrc"
- Release your addon and finish. An example https://asset.party/sboxcommunity/mp__fy_iceworld you can also publish multiple maps on an addon project like https://asset.party/alphaceph/mp__fy_poolday

___


Credits;
- BSP Parser implementation from https://github.com/magcius/noclip.website/
