# 修仙世界 · 洪荒以降

2D 写实（等距斜视 2.5D）修仙世界仿真。一切由随机 + 组合 + 时间演化生成：
个体 → 交易 → 群体 → 组织 → 帝国 → 崩解；时间单向不可逆，死者进入下一世。

## 技术栈（已定，冻结点见需求文档）

| 层 | 内容 | 铁律 |
|---|---|---|
| SimCore | 纯 C# 类库（`game/SimCore`） | **不含任何 Godot/Unity 依赖**，dotnet 可独立跑，将来直接搬进服务器 |
| Presentation | Godot 4.7.2 (.NET) | 只读 SimCore 状态，**不得写世界逻辑** |
| 构建 | 全 GitHub Actions，无人值守出 APK | 本地与 CI 同一脚本 `tools/build_apk.sh` |

选 Godot 而非 Unity 的唯一理由：**Unity 的授权与二进制场景让"全 CI"在物理上无法成立**（必须有一次带 GUI 的真机激活，场景/Shader 是二进制无法 diff）。
Godot 的场景（.tscn）、着色器（.gdshader）、导出模板全部是文本或可脚本生成，导出走 Linux headless，**CI 跑的就是我本地跑的这一条命令**。

## 目录

```
game/                 Godot 工程
  SimCore/            世界内核（纯 C#，确定性）
  scripts/Main.cs     呈现层（等距斜视绘制、输入、HUD）
  android/build/      Android 构建模板（由 export_templates 生成，不入库）
tools/build_apk.sh    一把梭：环境→签名→导入→编译→导出→校验
.github/workflows/    android-apk 流水线
需求文档-v0.1.md      需求与验收（v0.8）
sim/                  P0 无头世界模拟器（Java，4 种子 × 500 年实测）
```

## 本机构建

```bash
export JAVA_HOME=<jdk17>
export DOTNET_ROOT=<dotnet8>
export ANDROID_HOME=<android-sdk>     # platform-36 + build-tools 36.1.0
export PATH=$DOTNET_ROOT:$JAVA_HOME/bin:$PATH
export GODOT=<Godot_v4.7.2-stable_mono_linux.x86_64>
bash tools/build_apk.sh
```

## 已验证（2026-11-24）

- `dotnet build` 通过；`SimCore` 与呈现层分离，无循环依赖。
- Godot headless 导出 APK 成功：`com.simcore.xianxia` / 0.1.0 / minSdk 24 / arm64-v8a。
- APK 内含 `Xianxia.dll` + `SimCore.dll` + `libgodot_android.so`（缺任一即构建失败）。
- Xvfb 下真实渲染并截图，世界推演到第 41 年、17 人存活、2 个组织、恩怨 2/30。

## 已知边界

- 首版仅单机；联机接口（确定性哈希 + 事件总线 + 世界时间戳）已留位，未实现。
- 人物为程序化绘制的占位小人，不是成品立绘；立绘/衣片拆装走后续 PixelLab/手绘资产。
- 地图 16×16，等距取景一屏约 10 格；大地图与 TileSet 手绘块面未接入。
