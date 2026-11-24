# 修仙世界 · 洪荒以降（Unity 重做版）

[![unity-android](https://github.com/327542912/xianxia/actions/workflows/unity-android.yml/badge.svg)](https://github.com/327542912/xianxia/actions/workflows/unity-android.yml)

2.5D 正交等距修仙世界仿真。**世界里没有剧本，只有规则**：个体 → 交易 → 群体 → 组织 → 帝国 → 崩解，全靠「随机 + 任意组合 + 时间演化」。时间从洪荒起单向不可逆；死者按生平判词进入下一世，换一具身体再来。

> 当前：**v0.7.0 · Unity 6 LTS（6000.0.85f2）+ URP**｜内核验收 **58/58**（500 年 × 4 种子，CI 另跑 1500 年）｜旧 Godot 版归档在 `legacy/`
> 能玩的动线：投生 → 行走 → 修炼 → 帮人/拒人/夺灵 → 占府 → 入宗 → 开辟小世界 → 改内时流速 → 过界门 → 升品/炼宝/重炼 → 死出判词 → 转世
> **APK 状态**：源码与流水线已就绪；仓库缺 Unity 授权 Secret，构建任务按设计跳过。加授权后自动出 `v0.7.0` APK。

## 零、接手先读（任何人 30 分钟能开工）

| 你想做什么 | 先读 |
|---|---|
| 第一次接触这个项目 | [`docs/接手手册.md`](docs/接手手册.md)（目录地图、三条铁律、30 分钟清单） |
| 要改规则/加玩法 | [`docs/制作流程.md`](docs/制作流程.md)（需求→规则→验收→呈现→构建→发布） |
| 要出 APK / 配 Unity 授权 | [`docs/构建与发布.md`](docs/构建与发布.md) |
| 想知道某条验收对应哪段代码 | [`docs/验收与需求映射.md`](docs/验收与需求映射.md) |
| 报错/装不上/帧率低 | [`docs/常见问题.md`](docs/常见问题.md) |
| 要改界面 | [`docs/UI设计规范.md`](docs/UI设计规范.md) |
| 想对照旧版 | [`docs/迁移说明.md`](docs/迁移说明.md)、[`legacy/README.md`](legacy/README.md) |
| 想看每个版本改了什么 | [`CHANGELOG.md`](CHANGELOG.md) |
| 提交代码前 | [`CONTRIBUTING.md`](CONTRIBUTING.md) |

### 不需要 Unity 就能验证

```bash
# 只需要 .NET 8 SDK
bash tools/verify.sh 200     # 冒烟（约 3 分钟，58 条）
bash tools/verify.sh 500     # 全量 58 条
bash tools/verify.sh 1500    # 长跑（CI 同款）
# 或者
dotnet run -c Release --project simcli -- 500
```

正常结尾：`--- PASS 58 / FAIL 0 ---`。

### 需要 Unity 才能出包

```bash
bash tools/build_android.sh          # 需要 Unity 6000.0.85f2 + Android Build Support + 已激活授权
# 或推 main / 手动 Run workflow（GitHub Actions）
```

## 一、这次重做解决了什么

1. **引擎换成 Unity 6**：`main` 根目录即 Unity 工程，`Packages/manifest.json` 固定 URP 17.0.4。
2. **界面重做**：旧版「顶栏 + 状态卡 + 行囊条 + 12 键命令 + 3×3 面板 + 立绘面板 + 方向键 + 事件流」各自定位、互相遮挡；新版只有一套版式：**顶栏 → 人物卡 → 地图 → 事件条 → 指令坞 → 卷宗弹层**。
3. **补齐需求 v1.1 新增条款**：品阶、三套修炼体系、洪荒开局、小世界流速代价、印象来源权重、世代链不可回退、功法/法宝品阶封顶。
4. **源码与安装包同一提交发布**：Actions 先仓库自检与内核验收，再 Unity 构建 APK，最后发 Release。

## 二、架构

```
Assets/Scripts/Core/     SimCore — 纯 C#，零 UnityEngine 依赖，dotnet 可直接跑
  Model.cs               数据模型（人/兽/草木、品阶、印象来源、洞府、法宝…）
  World.cs               世界演化（组织/庇护链/货币/劫掠/功法/环境/婚配）
  Player.cs              一世循环与唯一命令通道 Act(verb)
  Scale.cs               世界节点：面积档位、开辟代价、内时流速、不稳与崩解
  Cultivation.cs         三套境界链与寿元表（人 / 兽 / 草木）
  Grade.cs               凡/灵/神：一生一升、单向降、分解重炼、品阶封顶
  Impression.cs          亲见 / 转述 / 传闻三档权重 + 重复传播放大上限
  Life.cs                世代链档案、纪元判定、状态哈希、存档摘要（无回退）
  Treasure.cs            法宝：炼制、升阶、降阶、重炼
Assets/Scripts/Game/     呈现层：WorldView（地形/地物/人物网格）、GameApp（时钟/相机/触控）
Assets/Scripts/UI/       界面：UiKit（令牌与组件）、Hud（版式、卷宗、判词）
Assets/Editor/           BuildAndroid：CI 构建入口（生成场景、竖屏、ARM64、URP）
simcli/                  无头验收器（直接编译 Assets/Scripts/Core 的同一份源码）
tools/                   verify.sh / build_android.sh / check_repo.sh
```

**铁律**：呈现层只读世界；一切状态改变只走 `World.Act`；`World.Tick(1)` 按月推进；同种子同命令必得同一 `StateHash()`；`Core` 不许引用 UnityEngine。

## 三、需求 v1.1 条款落地

| 条款 | 落点 | 验收 |
|---|---|---|
| 品阶凡/灵/神，一生一升、可单向降、分解重炼唯一重置 | `Grade.cs` | 49 |
| 功法品阶受创造者与底本封顶 | `World.StepTechniques` / `Grades.Cap` | 50 |
| 人·草木·兽三套独立境界与寿元表 | `Cultivation.cs` | 51 |
| 草木扎根、兽不通人间礼法 | `Cultivation.CanAct` | 52 |
| 小世界开辟与自定义流速必须有代价，过猛崩成遗府 | `Scale.cs` | 53 |
| 第一印象：亲见≫转述≫传闻 | `Impression.cs` | 54 |
| 重复传播放大，封顶 1.6 倍 | `Impression.cs` | 55 |
| 每一世开局都在洪荒早期（方圆五格无香火、地脉未破） | `Life.cs` | 56 |
| 世代链只增不改，存档无回退入口 | `Life.cs` | 57 |
| 法宝品阶 = min(炼制者, 材料)，升阶只一次 | `Treasure.cs` | 58 |

原有 48 条（组织涌现、帝国崩解、核心—边缘、货币涌现、五层庇护链、抽成率、离心分裂、四象限、恩债、判词、洞府阵法、功法谱系、四季物候、地理生境、破坏不可逆、婚配血脉、确定性……）全部保留并跑绿。逐条对应见 [`docs/验收与需求映射.md`](docs/验收与需求映射.md)。

## 四、怎么玩

打开即投生。地图可单指拖、双指缩放；点小人选中，点地面寻路走过去。

| 指令 | 作用 |
|---|---|
| 修行 | 打坐一月，按体系/品阶结算进度，突破可能招天妒 |
| 行善 / 拒人 / 夺灵 | 恩怨双向记账；亲见印象权重最高，还会被邻里转述 |
| 洞府 / 入宗 | 灵气与内时、抽成与护持 |
| 开辟 | 小世界，面积由境界与灵石定，折寿换空间 |
| 渡门 | 走界门；阵纹不合或境界不足过不去 |
| 游历 / 种灵 / 采脉 | 随机际遇 / 回补地气 / 破坏不可逆地换灵石 |
| 升品 / 炼宝 / 改流速 | 品阶一生一次；法宝上限受你与材料封顶；内时越快折寿越多 |
| 待月 / 卷宗 | 推一个月 / 打开身世·洞天·宗门·世系·设定 |

死亡即出全屏判词（不露分数），按「再入轮回」进入下一世；来世可能是人、树、草、狗、鹰、鱼，草木不能走路、兽不能入宗。

## 五、CI 与发布

`.github/workflows/unity-android.yml`：

```
preflight（仓库自检） → acceptance（500 + 1500 年） → license → build（APK） → Release
```

- 手动触发：`Actions → unity-android → Run workflow`，可填 `version`、是否跑 `long_run`；
- 版本号：默认 `0.7.0`，打 tag `v*` 用 tag 名，手动可覆盖；
- 授权：`UNITY_LICENSE`（推荐）或 `UNITY_EMAIL` + `UNITY_PASSWORD`；没配置时 `build` 跳过并在摘要里写明缺什么。

> 为什么不能在“随便一台虚拟机”装 Unity：Unity 编辑器下载源在本环境实测 404，且要求 ≥12 GB 磁盘、≥16 GB 内存（IL2CPP），并且**无论装在哪都要账号激活**。GitHub runner 预装了 Unity 与 Android SDK，是当前唯一现实路径。细节见 `docs/构建与发布.md`。

## 六、已知边界（诚实清单）

- **Unity 层未在本地编译**：本环境无 Unity，只做了 Roslyn 语法扫描（无语法错误码）与 API 人工审查；首次 CI 构建仍可能需修一轮编译错误。
- **APK 未产出**：缺 Unity 授权 Secret。
- **未做真机验证**：UI 观感、触控、60 帧待装包复核。
- APK 为调试签名，个人测试用；上架需补 keystore 与 AAB。
- 美术为运行时程序化低模 + 顶点色，无手绘资产、无骨骼动画；无音频。
- 存档只有摘要（seed/months/hash），无读档；按设计不提供回退。
- 10 万在册 = 个体 + 统计层凡人；L0 详细个体仍为几十人。
- 旧 Godot 版留在 `legacy/godot-game/`，不再维护。

## 七、许可

代码 MIT（见 [`LICENSE`](LICENSE)）。需求文档与世界观版权归作者。字体为 Droid Sans Fallback（Apache-2.0，见 `Assets/Resources/Fonts/`）。