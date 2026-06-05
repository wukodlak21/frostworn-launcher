#!/bin/bash

CONFIG_FILE="$HOME/.frostworn_launcher"
REALMLIST_SERVER="logon.frostworn.com"

# ── Učitaj sačuvanu putanju ──────────────────────────────────────────────────
if [ -f "$CONFIG_FILE" ]; then
    WOW_PATH=$(cat "$CONFIG_FILE")
fi

# ── Provjeri da li WoW postoji na sačuvanoj putanji ─────────────────────────
wow_exists() {
    [ -f "$1/WoW" ] || [ -f "$1/Wow.exe" ] || [ -d "$1/World of Warcraft.app" ]
}

if [ -z "$WOW_PATH" ] || ! wow_exists "$WOW_PATH"; then
    # Pitaj korisnika da odabere folder
    WOW_PATH=$(osascript -e 'tell app "Finder" to POSIX path of (choose folder with prompt "Select your World of Warcraft 3.3.5a folder:")')

    if [ -z "$WOW_PATH" ]; then
        osascript -e 'display dialog "No folder selected. Launcher will exit." buttons {"OK"} default button "OK" with icon caution'
        exit 1
    fi

    WOW_PATH="${WOW_PATH%/}"

    if ! wow_exists "$WOW_PATH"; then
        osascript -e 'display dialog "World of Warcraft not found in the selected folder.\n\nMake sure you selected the correct folder (it should contain WoW or Wow.exe)." buttons {"OK"} default button "OK" with icon stop'
        exit 1
    fi

    echo "$WOW_PATH" > "$CONFIG_FILE"
fi

# ── Setuj realmlist.wtf ──────────────────────────────────────────────────────
for LOCALE in enUS esMX ptBR deDE enGB esES frFR itIT ruRU koKR zhTW zhCN; do
    REALMLIST_FILE="$WOW_PATH/Data/$LOCALE/realmlist.wtf"
    if [ -f "$REALMLIST_FILE" ]; then
        chmod 644 "$REALMLIST_FILE"
        echo "set realmlist \"$REALMLIST_SERVER\"" > "$REALMLIST_FILE"
        chmod 444 "$REALMLIST_FILE"
    fi
done

# ── Setuj WTF/Config.wtf ─────────────────────────────────────────────────────
CONFIG_WTF="$WOW_PATH/WTF/Config.wtf"
if [ -f "$CONFIG_WTF" ]; then
    TMP=$(grep -iv "realmlist" "$CONFIG_WTF")
    printf '%s\nSET realmList "%s"\n' "$TMP" "$REALMLIST_SERVER" > "$CONFIG_WTF"
fi

# ── Pokreni WoW ─────────────────────────────────────────────────────────────
if [ -d "$WOW_PATH/World of Warcraft.app" ]; then
    open "$WOW_PATH/World of Warcraft.app"
elif [ -f "$WOW_PATH/WoW" ]; then
    "$WOW_PATH/WoW" &
elif [ -f "$WOW_PATH/Wow.exe" ]; then
    # Wine/CrossOver
    open "$WOW_PATH/Wow.exe"
else
    osascript -e 'display dialog "Could not launch World of Warcraft.\n\nTry selecting the folder again by holding Option and clicking the dock icon." buttons {"OK"} default button "OK" with icon stop'
    rm -f "$CONFIG_FILE"
fi
