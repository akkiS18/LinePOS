#!/bin/sh
# Har bir slaydni 1080x1350 PNG qilib chiqaradi (Chrome headless)
cd "$(dirname "$0")"
CH="/c/Program Files/Google/Chrome/Application/chrome.exe"
URL="file:///$(pwd -W)/karusel.html"
for i in 1 2 3 4 5 6 7; do
  "$CH" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 \
    --window-size=1080,1350 --virtual-time-budget=6000 \
    --screenshot="$(pwd -W)/slayd_$i.png" "$URL?s=$i" 2>/dev/null
done
ls -la slayd_*.png
