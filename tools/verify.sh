#!/usr/bin/env bash
# 一键内核验收：不需要 Unity，只需要 .NET 8 SDK。
# 用法：bash tools/verify.sh [年数]    默认 500；CI 用 500 与 1500。
set -euo pipefail
cd "$(dirname "$0")/.."

YEARS="${1:-500}"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "缺少 .NET 8 SDK。安装：https://dotnet.microsoft.com/download/dotnet/8.0" >&2
  exit 1
fi

if [ "$YEARS" -lt 200 ]; then
  echo "提示：少于 200 年只能验证短周期条目（组织类型、功法改良、四象限样本等会长线 FAIL），" >&2
  echo "      这不是规则坏了。要看 58 条全绿，请跑 200 或 500。" >&2
fi

echo "== 内核验收：${YEARS} 年 × 4 种子（42 / 7 / 123 / 999）=="
dotnet run -c Release --project simcli -- "$YEARS"
RC=$?

# dotnet run 会把程序退出码透传：0 = 全绿，1 = 有 FAIL
exit $RC