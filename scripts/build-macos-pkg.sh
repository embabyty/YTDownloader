#!/usr/bin/env bash
# Builds a universal (x86_64 + arm64) macOS PKG installer for YoutubeDownloader.
# The installer places YoutubeDownloader.app in /Applications and a universal
# ffmpeg binary in both the app bundle and /usr/local/bin.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${1:-$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$repo_root/Directory.Build.props" | head -n 1)}"
ffmpeg_version="$(sed -n 's:.*const string Version = "\([^"]*\)".*:\1:p' "$repo_root/YoutubeDownloader.Core/Downloading/FFmpeg.cs" | head -n 1)"

if [[ -z "$version" || -z "$ffmpeg_version" ]]; then
    echo "Failed to determine app or FFmpeg version." >&2
    exit 1
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/ytd-pkg.XXXXXX")"
trap 'rm -rf "$work_dir"' EXIT

app_exe="YoutubeDownloader"

echo "Building YoutubeDownloader $version with FFmpeg $ffmpeg_version (universal)"

for rid in osx-x64 osx-arm64; do
    echo "Publishing $rid..."
    dotnet publish "$repo_root/YoutubeDownloader" \
        -p:Version="$version" \
        -p:CSharpier_Bypass=true \
        -p:PublishSingleFile=true \
        --configuration Release \
        --runtime "$rid" \
        --self-contained \
        --output "$work_dir/publish-$rid" \
        --nologo
done

for arch in x64 arm64; do
    echo "Downloading FFmpeg for $arch..."
    curl -fL --retry 2 \
        -o "$work_dir/ffmpeg-$arch.zip" \
        "https://github.com/Tyrrrz/FFmpegBin/releases/download/$ffmpeg_version/ffmpeg-osx-$arch.zip"
    mkdir -p "$work_dir/ffmpeg-$arch"
    unzip -q -o "$work_dir/ffmpeg-$arch.zip" ffmpeg -d "$work_dir/ffmpeg-$arch"
done

payload="$work_dir/payload"
mkdir -p "$payload/Applications" "$payload/usr/local/bin"

cp -R "$work_dir/publish-osx-x64/YTD.app" "$payload/Applications/YoutubeDownloader.app"
app="$payload/Applications/YoutubeDownloader.app"
rm -f "$app/Contents/MacOS/YoutubeDownloader.Core.pdb"

echo "Merging the two architectures..."
lipo -create -output "$work_dir/$app_exe-universal" \
    "$work_dir/publish-osx-x64/YTD.app/Contents/MacOS/$app_exe" \
    "$work_dir/publish-osx-arm64/YTD.app/Contents/MacOS/$app_exe"
lipo -create -output "$work_dir/ffmpeg-universal" \
    "$work_dir/ffmpeg-x64/ffmpeg" \
    "$work_dir/ffmpeg-arm64/ffmpeg"

# lipo invalidates the original signatures, so sign the binaries as standalone
# files before copying them into the bundle.
chmod +x "$work_dir/$app_exe-universal" "$work_dir/ffmpeg-universal"
codesign --force --sign - --timestamp=none "$work_dir/$app_exe-universal"
codesign --force --sign - --timestamp=none "$work_dir/ffmpeg-universal"

cp "$work_dir/$app_exe-universal" "$app/Contents/MacOS/$app_exe"
cp "$work_dir/ffmpeg-universal" "$app/Contents/MacOS/ffmpeg"
cp "$work_dir/ffmpeg-universal" "$payload/usr/local/bin/ffmpeg"

codesign --force --sign - --timestamp=none "$app"
codesign --verify --deep --strict "$app"

component_pkg="$work_dir/YoutubeDownloader-component.pkg"
pkgbuild \
    --root "$payload" \
    --install-location / \
    --identifier me.tyrrrz.youtubedownloader \
    --version "$version" \
    --ownership recommended \
    "$component_pkg"

cat > "$work_dir/Distribution" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<installer-gui-script minSpecVersion="2">
    <title>YoutubeDownloader</title>
    <organization>embabyty</organization>
    <options customize="never" require-scripts="false" rootVolumeOnly="true" />
    <pkg-ref id="me.tyrrrz.youtubedownloader" />
    <choices-outline>
        <line choice="me.tyrrrz.youtubedownloader" />
    </choices-outline>
    <choice id="me.tyrrrz.youtubedownloader" visible="false" title="YoutubeDownloader">
        <pkg-ref id="me.tyrrrz.youtubedownloader" />
    </choice>
    <pkg-ref id="me.tyrrrz.youtubedownloader" version="$version" onConclusion="none">YoutubeDownloader-component.pkg</pkg-ref>
</installer-gui-script>
EOF

output="$repo_root/YoutubeDownloader-$version-universal.pkg"
productbuild \
    --distribution "$work_dir/Distribution" \
    --package-path "$work_dir" \
    "$output"

echo "Created $output"
