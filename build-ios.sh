#!/bin/bash
set -e

APP="$HOME/Developer/BlueChatBuild/bin/Debug/net10.0-ios/ios-arm64/BlueChat.app"
SIGN="Apple Development: kkmin123446@icloud.com (96CSNF5L6K)"
PROFILE="$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles/8c87bacd-41e4-4112-9735-8f71143e8327.mobileprovision"
ENTITLEMENTS="Platforms/iOS/Entitlements.plist"

rm -rf "$APP"

# Build (ignore codesign failure)
set +e
dotnet build -f net10.0-ios -p:RuntimeIdentifier=ios-arm64 --no-incremental 2>&1
set -e

if [ ! -d "$APP" ]; then
    echo "ERROR: Build output not found at $APP"
    exit 1
fi

echo ">> Embedding provisioning profile..."
cp "$PROFILE" "$APP/embedded.mobileprovision"

echo ">> Clearing xattrs recursively..."
find "$APP" -exec xattr -c {} \; 2>/dev/null || true

echo ">> Signing nested frameworks and dylibs..."
find "$APP/Frameworks" -name "*.framework" -o -name "*.dylib" 2>/dev/null | while read f; do
    codesign --force --sign "$SIGN" --timestamp=none "$f" 2>/dev/null || true
done

echo ">> Signing app bundle..."
codesign --force --sign "$SIGN" \
         --entitlements "$ENTITLEMENTS" \
         --timestamp=none \
         --deep \
         "$APP"

echo ">> Codesign verify..."
codesign --verify --deep --strict "$APP" && echo ">> Signature valid" || echo ">> WARNING: Signature verification failed"
