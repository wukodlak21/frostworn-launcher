# Oracle-Lite

![image](https://github.com/CodebyVision/Oracle-Lite/assets/7664922/4a52b892-294c-41cd-96e8-8c1d8625ddd0)

## Requirements
- Apache or nginx webserver
- Visual Studio 2019 or 2022 Community Edition with c#

## How to setup

1. Upload `vision_lite` folder on your webserver, example at `https://yourserver.com/vision_lite/`.
2. Open `Oracle Lite.sln` with Visual Studio.
3. Right-click on `Oracle Lite` -> `Properties` -> `Settings`.
4. Set `ApiTarget` to `https://yourserver.com/vision_lite/vision.php`
5. Set `RealmList` to your realmlist address, example `logon.myserver.com`
6. Compile by pressing `CTRL+SHIFT+B` or right-click on solution explorer -> `Build`.
7. Compiled launcher files are in `\Oracle-Lite\bin\` folder, you need all for the launcher to work.

![image](https://github.com/Oracle-Launcher/Oracle-Lite/assets/7664922/e011b0f9-7669-4048-b278-96f03285c68f)

## How to upload game files
Go to your webserver, example `https://yourserver.com/vision_lite/downloads/game/` and drop your game files in `sd` or `hd` folder.
- `sd` folder holds the base client files
- `hd` folder holds optional hd client files that will overwrite base client files

The launcher will automatically detect updates on launcher start-up or when check for updates button is pressed.

## How to edit slider
Go to your webserver, example `https://yourserver.com/vision_lite/configs/slider.php`

## How to edit socials submenu
In Visual Studio go to `Popups` folder -> `Submenu_Popup.xaml`

## How to navbar menu items
In Visual Studio go to `Launcher.xaml`
