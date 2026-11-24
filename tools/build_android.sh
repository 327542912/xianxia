#!/usr/bin/env bash
# 本机 Unity 构建 APK（需要 Unity 6000.0.85f2 + Android Build Support + 已激活授权）
# 用法：UNITY_BIN=/path/to/Unity VERSION=0.7.0 BUILD_NUMBER=1 bash tools/build_android.sh
set -euo pipefail
cd "$(dirname "$0")/.."

UNITY_BIN="${UNITY_BIN:-}"
if [ -z "$UNITY_BIN" ]; then
  for c in \
    "/Applications/Unity/Hub/Editor/6000.0.85f2/Unity.app/Contents/MacOS/Unity" \
    "$HOME/Unity/Hub/Editor/6000.0.85f2/Editor/Unity" \
    "/opt/unity/Editor/Unity" \
    "/opt/Unity/Editor/Unity"; do
    if [ -x "$c" ]; then UNITY_BIN="$c"; break; fi
  done
fi

if [ -z "$UNITY_BIN" ] || [ ! -x "$UNITY_BIN" ]; then
  echo "未找到 Unity。请设置 UNITY_BIN=/path/to/Unity" >&2
  echo "版本必须与 ProjectSettings/ProjectVersion.txt 一致（当前 6000.0.85f2）" >&2
  exit 1
fi

export VERSION="${VERSION:-0.7.0}"
export BUILD_NUMBER="${BUILD_NUMBER:-1}"

echo "Unity      : $UNITY_BIN"
echo "版本       : $VERSION (versionCode=$BUILD_NUMBER)"
echo "工程       : $PWD"
echo "日志       : build.log"
echo

"$UNITY_BIN" -batchmode -quit -projectPath "$PWD" \
  -executeMethod Xianxia.EditorTools.BuildAndroid.Build \
  -buildTarget Android \
  -logFile build.log

APK="Build/xianxia.apk"
if [ ! -f "$APK" ]; then
  echo "构建未产出 APK，请检查 build.log 里的 error CS / BuildResult。" >&2
  exit 1
fi

ls -lh "$APK"
echo "完成：$APK"