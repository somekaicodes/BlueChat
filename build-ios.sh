#!/bin/bash
# Build iOS, auto-clear macOS xattrs that block codesign, then re-sign.

APP="bin/Debug/net10.0-ios/ios-arm64/BlueChat.app"
SIGN="Apple Development: kkmin123446@icloud.com (96CSNF5L6K)"
PROVISION="iOS Team Provisioning Profile: com.kaikim.BlueChat"

rm -rf "$APP"

dotnet build -f net10.0-ios -p:RuntimeIdentifier=ios-arm64 --no-incremental 2>&1

# If codesign failed but the .app exists, clear xattrs and re-sign
if [ -d "$APP" ]; then
    echo ">> Clearing xattrs..."
    xattr -cr "$APP"
    echo ">> Re-codesigning..."
    codesign --force --sign "$SIGN" --entitlements Platforms/iOS/Entitlements.plist "$APP"
    echo ">> Done"
fi
