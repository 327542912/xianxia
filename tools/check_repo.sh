#!/usr/bin/env bash
# 仓库自检：确认接手所需文件齐全、manifest 合法、版本号一致、内核没有引擎依赖。
# CI 的 preflight 任务会跑它；本地也可随时跑。
set -euo pipefail
cd "$(dirname "$0")/.."

fail=0
need() {
  if [ -e "$1" ]; then
    echo "OK   $1"
  else
    echo "MISS $1" >&2
    fail=1
  fi
}

echo "== 关键文件 =="
need Packages/manifest.json
need ProjectSettings/ProjectVersion.txt
need Assets/Scripts/Core/Model.cs
need Assets/Scripts/Core/World.cs
need Assets/Scripts/Core/Player.cs
need Assets/Scripts/Core/Scale.cs
need Assets/Scripts/Core/Cultivation.cs
need Assets/Scripts/Core/Grade.cs
need Assets/Scripts/Core/Impression.cs
need Assets/Scripts/Core/Life.cs
need Assets/Scripts/Core/Treasure.cs
need Assets/Scripts/Game/GameApp.cs
need Assets/Scripts/Game/WorldView.cs
need Assets/Scripts/UI/UiKit.cs
need Assets/Scripts/UI/Hud.cs
need Assets/Shaders/FlatVC.shader
need Assets/Resources/Fonts/NotoSansSC-Regular.ttf
need Assets/Editor/BuildAndroid.cs
need simcli/Program.cs
need simcli/SimCli.csproj
need .github/workflows/unity-android.yml
need docs/接手手册.md
need docs/制作流程.md
need docs/构建与发布.md
need docs/验收与需求映射.md
need docs/常见问题.md
need docs/UI设计规范.md
need README.md
need CONTRIBUTING.md
need LICENSE
need CHANGELOG.md

echo
echo "== Packages/manifest.json =="
python3 - <<'PY'
import json, sys
try:
    d = json.load(open('Packages/manifest.json'))
    deps = d.get('dependencies', {})
    assert 'com.unity.ugui' in deps, '缺少 com.unity.ugui'
    print(f"OK   dependencies={len(deps)}  ugui={deps['com.unity.ugui']}  urp={deps.get('com.unity.render-pipelines.universal','-')}")
except Exception as e:
    print(f"FAIL manifest.json: {e}", file=sys.stderr)
    sys.exit(1)
PY

echo
echo "== 版本一致性 =="
PV=$(grep -oE 'm_EditorVersion: [0-9.]+[a-z0-9]*' ProjectSettings/ProjectVersion.txt | awk '{print $2}')
WV=$(grep -oE 'UNITY_VERSION: [0-9.]+[a-z0-9]*' .github/workflows/unity-android.yml | awk '{print $2}')
echo "ProjectVersion=$PV  workflow=$WV"
if [ "$PV" != "$WV" ]; then
  echo "FAIL 版本不一致：ProjectVersion.txt 与 workflow 的 UNITY_VERSION 必须相同" >&2
  fail=1
fi

echo
echo "== 内核不得依赖引擎 =="
if grep -rn "using UnityEngine\|using UnityEditor" Assets/Scripts/Core/ >/dev/null 2>&1; then
  echo "FAIL Core 里出现了 UnityEngine/UnityEditor 引用" >&2
  grep -rn "using UnityEngine\|using UnityEditor" Assets/Scripts/Core/ >&2
  fail=1
else
  echo "OK   Core 无引擎依赖"
fi

echo
echo "== 内核随机源 =="
python3 - <<'PY'
import pathlib, re, sys
bad = []
pat = re.compile(r'System\.Random|new\s+Random\s*\(|DateTime\.Now|Guid\.NewGuid')
for p in pathlib.Path('Assets/Scripts/Core').rglob('*.cs'):
    for i, line in enumerate(p.read_text(encoding='utf-8').splitlines(), 1):
        code = line.split('//')[0]          # 注释里提到不算
        if pat.search(code):
            bad.append(f"{p}:{i}: {line.strip()}")
if bad:
    print("FAIL Core 里出现了不确定随机源：", file=sys.stderr)
    print("\n".join(bad), file=sys.stderr)
    sys.exit(1)
print("OK   未使用 System.Random / DateTime.Now / Guid.NewGuid")
PY
RAND_RC=$?
if [ "$RAND_RC" -ne 0 ]; then fail=1; fi

echo
if [ "$fail" -ne 0 ]; then
  echo "自检 FAIL" >&2
  exit 1
fi
echo "自检 OK"