# PartyPatcher
Tool to simply patch and modify GCN Mario Party files.

To use. Make MP4.json or run the tool once.

Paste in your game path

```json
{
  "GamePath": "D:\Games\Mario Party 4\files"
}
```

Run the tool to patch in mods.

Check SampleMP4 for example to mod in sprites, bins, and audio.

## Guide

Create a new mod folder in mods. Create Meta.json inside it.

Paste this inside

```json
{
  "PatchFile": {
    "FileName": null,
    "BinPatches": [],
    "SoundPatches": [],
    "MessagesPatches": [],
  },
  "Name": "My Mod",
  "Description": "Mod Info",
  "Author": "Author Name Here"
}
```

### Message Patch

To start with a message patch, drag/drop mess/board_e.dat to PartyPatcher.exe to make board_e.dat.json so you can extract messages.

<img width="457" height="501" alt="image" src="https://github.com/user-attachments/assets/c32f36af-4cc8-4c7c-b4c7-692ce84bb3ae" />

Go to entry you want to replace. For example, "DK" message text is 1_13. First number is group idx, second is message idx.
Set those values and MessageFilePath as board_e.dat or the name of the mess file for other games.

```json
    "MessagesPatches": [
      {
        "MessageFilePath": "board_e.dat",
        "Message": "DK TEST",
        "ReplaceGroupIdx": 1,
        "ReplaceMessageIdx": 13
      }
    ]
```

### Sound Patch

Drag/drop sound/mpgcsnd.msm from the game dump to the exe. This will dump the sounds as wav.

<img width="387" height="509" alt="image" src="https://github.com/user-attachments/assets/17c173c3-603f-4ad1-85e0-88283621b3b6" />

GroupId is the folder the sound is in. Then sound id is the ####.wav name.
For a list of what groups are used for what by number, check this https://github.com/KillzXGaming/PartyPatcher/wiki/Sound-IDs-MP4

```json
    "SoundPatches": [
      {
        "MsmPath": "mpgcsnd.msm",
        "FilePath": "sound.wav",
        "Label": "",
        "GroupId": 4,
        "SoundId": 2
      }
    ],
```

### Bin Patch

Drag/drop a data/ .bin to the exe.

Edit any of the files. You can name them however you want or if it is hsf, you can import/export with the blender plugin.
Make sure ReplaceID is the # number the file was named as.

<img width="199" height="183" alt="image" src="https://github.com/user-attachments/assets/bd476204-2962-4bb5-8f42-5defc0373945" />

```
    "BinPatches": [
      {
        "$type": "file",
        "BinFile": "board.bin",
        "FilePath": "Toad.hsf",
        "Name": "Edited Toad Host",
        "ReplaceID": 0
      }
    ],
```

### Sprite Patch

Same as bin patch but you can use png directly.
These will dump into a folder when a bin is drag/dropped where you can freely edit.
Add image file names in "Images".

```
    "BinPatches": [
      {
        "$type": "sprite",
        "Images": [
          "Texture0.png"
        ],
        "BinFile": "board.bin",
        "Name": "New Space Sprites",
        "ReplaceID": 28
      }
    ],
```

Or with custom sprite sheet, set "SpriteSheetFile".

```
    "BinPatches": [
      {
        "$type": "sprite",
        "Images": [
          "Texture0.png"
        ],
        "SpriteSheetFile": "Sheet.json",
        "BinFile": "board.bin",
        "Name": "New Space Sprites",
        "ReplaceID": 28
      }
    ],
```
