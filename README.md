# UpuGuiAvalonia 

unpack Unity Packages from [Unity 3D](http://www.unity.com/ "Unity 3D"), without the unity editor

<img width="668" height="554" alt="image" src="https://github.com/user-attachments/assets/e3e198b9-74c2-4505-a53d-55c4af30cbcd" />


Usage
---
UpuGuiAvalonia.exe [options]

Usage:
UpuGuiAvalonia [<output-directory>] [options]

Arguments:
<output-directory>  Optional parent directory for the unpacked package

**Options:**<br />
--extract <extract>  Unity package to extract<br />
--extract-metadata   Extract Unity .meta files<br />
--register           Register context menu handler.<br />
--unregister         Unregister context menu handler.<br />
--about              Show information about UpuGuiAvalonia<br />
-?, -h, --help       Show help and usage information<br />
--version            Show version information<br />

Supports
---
- Windows
- Linux

Download
---
Latest Version: https://github.com/Myrkie/UpuGuiAvalonia/releases


## Build Instructions ##

Release Win
```code
./PublishWindowsAot.ps1
```

Release Linux
```code
./PublishLinuxAot.ps1
```

Search Engine Keywords:
unity3d, unitypackage, unpack, extract, deflate, assets, UPUGui
