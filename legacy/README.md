# legacy：旧 Godot 版（仅存档参考）

这个目录保留的是 2026-11-24 之前的 **Godot 4.7.2-stable.mono + C#** 版本，用于对照与考古。`main` 的构建与发布已经全部转向 Unity 6，**旧版不再维护、CI 不再出包**。

## 里面有什么

| 路径 | 说明 |
|---|---|
| `godot-game/project.godot` | Godot 工程配置（4.7.2 mono） |
| `godot-game/SimCore/*.cs` | 旧版纯 C# 内核：Model / World / Player / Scale |
| `godot-game/scripts/Main.cs` | 旧版自绘世界（等距斜视、人物、地形） |
| `godot-game/scripts/UI.cs`、`Play.cs`、`Actions.cs` | 旧版界面（多面板叠加的那一版） |
| `godot-game/export_presets.cfg`、`android/` | 旧版 Android 导出配置 |
| `godot-game/assets/world_bg.png` | 旧版 AI 青绿山水底图 |

## 想跑旧版

需要 Godot 4.7.2 mono（本环境下载被墙，未验证），以及 .NET 8：

```bash
cd legacy/godot-game
godot --path . --headless   # 仅加载工程
```

旧版 CI（`.github/workflows/android.yml`）与打包脚本（`tools/build_apk.sh`）已经删除；要恢复请从 git 历史里取：

```bash
git log --all -- .github/workflows/android.yml
git show <commit>:.github/workflows/android.yml > /tmp/android.yml
```

## 与新版的关系

- 新版 `Assets/Scripts/Core/` 是从这里**移植并扩展**的：世界观、组织演化、庇护链、抽成、货币、判词等规则保留，新增品阶/三体系/小世界代价/印象双账/世代链。
- 旧版界面是“顶栏 + 状态卡 + 行囊条 + 12 键命令 + 3×3 面板 + 立绘面板 + 方向键 + 事件流”各画一套，是“丑”的结构性原因；新版重做为单一版式，差异见 `docs/UI设计规范.md`。
- 对照迁移细节见 `docs/迁移说明.md`。

## 提醒

- 不要在 `legacy/` 上继续做功能；它只是证据与参考。
- 旧版与新版的 `StateHash`、存档格式不兼容，不能互相读档。
- 旧版包含的历史提交不影响新版构建；Unity 工程只编译 `Assets/` 下的脚本。