#!/usr/bin/env bash
# 一把梭：从干净仓库到可安装 APK。本地与 CI 完全同一套逻辑。
# 依赖：JDK17、.NET8 SDK(+android workload)、Android SDK(platform-36 / build-tools 36.1)、Godot 4.7.2 mono、mono 导出模板
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME="$ROOT/game"
OUT="${OUT:-$ROOT/build}"
VERSION="${VERSION:-0.2.0}"
GODOT="${GODOT:-$ROOT/gd/tools/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64}"
TPL_VER="4.7.2.stable.mono"

mkdir -p "$OUT"

echo "== 1. 环境检查"
: "${ANDROID_HOME:?需要 ANDROID_HOME}"
: "${JAVA_HOME:?需要 JAVA_HOME}"
: "${DOTNET_ROOT:?需要 DOTNET_ROOT}"
command -v dotnet >/dev/null || { echo "dotnet 不在 PATH"; exit 1; }
[ -x "$GODOT" ] || { echo "找不到 Godot: $GODOT"; exit 1; }

echo "== 2. Android 构建模板（干净检出时自动还原）"
TPL_DIR="$HOME/.local/share/godot/export_templates/$TPL_VER"
[ -f "$TPL_DIR/android_source.zip" ] || { echo "缺导出模板: $TPL_DIR/android_source.zip"; exit 1; }
if [ ! -f "$GAME/android/build/build.gradle" ]; then
  mkdir -p "$GAME/android/build"
  unzip -q -o "$TPL_DIR/android_source.zip" -d "$GAME/android/build"
fi
echo "$TPL_VER" > "$GAME/android/.build_version"
touch "$GAME/android/build/.gdignore"

echo "== 3. 编辑器设置（SDK / JDK / 调试签名）"
SETTINGS="$HOME/.config/godot/editor_settings-4.7.tres"
if [ ! -f "$SETTINGS" ]; then
  "$GODOT" --headless --path "$GAME" --import >/dev/null 2>&1 || true
fi
python3 - "$SETTINGS" "$ANDROID_HOME" "$JAVA_HOME" "$ROOT" <<'PY'
import sys, re
p, sdk, jdk, root = sys.argv[1:5]
s = open(p).read()
def setk(k, v):
    global s
    line = f'{k} = "{v}"'
    if re.search(rf'^{re.escape(k)} = .*$', s, re.M):
        s = re.sub(rf'^{re.escape(k)} = .*$', line, s, flags=re.M)
    else:
        s = s.replace('[resource]', '[resource]\n' + line, 1)
setk('export/android/android_sdk_path', sdk)
setk('export/android/java_sdk_path', jdk)
setk('export/android/debug_keystore', f'{root}/gd/game-debug.keystore')
setk('export/android/debug_keystore_user', 'androiddebugkey')
setk('export/android/debug_keystore_pass', 'android')
open(p, 'w').write(s)
PY

echo "== 4. 生成调试签名（幂等）"
KS="$ROOT/gd/game-debug.keystore"
if [ ! -f "$KS" ]; then
  "$JAVA_HOME/bin/keytool" -keyalg RSA -genkeypair -alias androiddebugkey -keypass android \
    -keystore "$KS" -storepass android -dname "CN=Xianxia Debug,O=SimCore,C=CN" -validity 9999 \
    -deststoretype pkcs12 >/dev/null 2>&1
fi

echo "== 5. 导入资源 + 编译 C#"
"$GODOT" --headless --path "$GAME" --import >/dev/null
dotnet build "$GAME/Xianxia.csproj" -c Debug -v q

echo "== 6. 导出 APK"
APK="$OUT/xianxia-$VERSION.apk"
rm -f "$APK"
"$GODOT" --headless --path "$GAME" --export-debug "Android" "$APK"

echo "== 7. 校验（不合格就红）"
[ -s "$APK" ] || { echo "APK 未生成"; exit 1; }
AAPT="$(ls "$ANDROID_HOME"/build-tools/*/aapt2 | tail -1)"
BADGING="$("$AAPT" dump badging "$APK")"
LISTING="$(unzip -l "$APK")"
# 注意：不能把 grep -q 直接接管管道 —— SIGPIPE 会让 pipefail 误判
case "$BADGING" in *"package: name='com.simcore.xianxia'"*) ;; *) echo "包名不符"; exit 1;; esac
case "$BADGING" in *"native-code: 'arm64-v8a'"*) ;; *) echo "缺 arm64 架构"; exit 1;; esac
case "$LISTING" in *"Xianxia.dll"*) ;; *) echo "缺 C# 程序集"; exit 1;; esac
case "$LISTING" in *"SimCore.dll"*) ;; *) echo "缺 SimCore 程序集"; exit 1;; esac
case "$LISTING" in *"libgodot_android.so"*) ;; *) echo "缺引擎 .so"; exit 1;; esac
SIZE=$(stat -c%s "$APK")
SHA=$(sha256sum "$APK" | cut -d' ' -f1)
echo "APK: $APK"
echo "SIZE: $SIZE bytes"
echo "SHA256: $SHA"
