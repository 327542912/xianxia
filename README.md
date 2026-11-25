# 小说引擎 · Novel Engine v1.0.0（隔离分支 `app/novel-engine-v1`）

这是一个**独立分支**上的原生 Android 小说创作应用，与仓库主项目（Unity 修仙模拟）互不重叠：本分支只有本应用，main / dev-xianxia 未被改动。

目标：把此前几轮讨论定下的**成熟体系**落成真流程，而不是提示词里的祈使句。所有关键约束都在代码里判定，判不了的明确标注，不假装通过。

## 一、应用做了什么

一句话：配置三要素 → 生成策划与 10 章大纲 → 逐章写、逐章判、不合格自动改 → 台账留痕 → 导出。

- **L0 配置锁**：作品名、题材、平台、主角人设、核心冲突、字数、风格包、模型档位。未锁定不能生成。
- **策划（内置提示词）**：把「题材 / 主角人设 / 核心冲突」扩成一句话故事、主线、势力、人物关系、爽点、反转伏笔，以及**正好 10 章**大纲；每章强制给出核心事件与章末悬念。结构不合格会自动带问题清单修复一次，仍不合格则如实报错。
- **生成**：单遍生成，七笔法 + 五红线 + 风格包注入（≤2500 字）+ 当章条款（C/M/G 三组）+ 四槽注入（≤40 行）+ 上一章账本。
- **闸门（代码判定，模型不自报）**：G01 字数双口径、G02 段长 ≤5 句、G03 句长分位、G04 对话占比、G05 禁用清单、G06 比喻密度、G07 账本六字段、G08 章号连续、G09 实体一致、G10 回显检测、G11 跨章复制、G12 线头阈值。不通过自动重写，最多两次，仍不过则挂起，不进下一章。
- **模型判定**：M01–M08（因果链、动机、信息越界、说明书对话、结构同构、人性灰度、章末钩子、复述倾向），只标问题不改稿；判定失败会明确写「判定未执行」，不伪造通过。
- **状态**：六字段账本（事件/物件/人物/时间/信息/线索）硬格式解析，入库 SQLite；四核对；四槽注入由库渲染，体量不随章号增长。
- **热修台账**：闸门不通过可一键记入，字段含样本定位、症状、归因、条款动作、生效层、回归状态。
- **模型接入**：本地（llama.cpp / Ollama / LM Studio 的 `/v1/chat/completions`）与 API（任何 OpenAI 兼容服务）同一套适配器，可切换、可测试连通性、失败重试。API 密钥只存本机 SharedPreferences，不上传、不入库、不进版本库。

## 二、界面（原生控件，无 WebView）

1. **首页**：作品列表、当前模型、入口。
2. **配置页**：三要素 + 风格包 + 模型档位；保存即锁定。
3. **策划页**：生成/查看主线、关系、爽点、反转、10 章大纲；点章节进入写作。
4. **写作页**：生成、闸门逐项结果（PASS/FAIL + 具体数值）、账本、模型判定、正文；未通过可重写或记入台账；通过才允许下一章。
5. **质检页**：交付/挂起统计、闸门通过率、成本估算、热修台账、导出全书 TXT。
6. **模型接入页**：本地与 API 参数、连通性测试。

## 三、安装包在哪

- 分支内直链（public 仓库，免登录）：
  `https://raw.githubusercontent.com/327542912/xianxia/app/novel-engine-v1/dist/novel-engine-1.0.0.apk`
- Release 附件：同仓库 Releases 中最新 `novel-app-v1.0.0-r*`，含已签名 APK、AAB、debug APK 与 SHA256SUMS。
- 签名：内测密钥 `config/internal-release.keystore`（指纹固定，可覆盖升级）；上架前换 repo secrets 并轮换。

## 四、构建

本仓库不含构建产物。Android 安装包由 GitHub Actions 在 `app/novel-engine-v1` 分支上构建：

```bash
# 本地（需 JDK 17 + Android SDK 34）
gradle testDebugUnitTest
gradle assembleDebug        # 产物：app/build/outputs/apk/debug/app-debug.apk
```

CI：`.github/workflows/novel-app.yml`，push 到本分支即触发；步骤为「单元测试 → 构建 debug/release → 产物 artifact → 发布 Release 附件」。Release 里的 `novel-engine-v0.1.0-debug.apk` 可直接安装（debug 签名，仅供内测）。

正式上架需要在仓库 Secrets 配置 `RELEASE_KEYSTORE_PATH` 等签名信息（见 `app/build.gradle` 的 signingConfigs），并改用 AAB。

## 五、代码结构

```
app/src/main/java/com/novelengine/
├── core/                     纯 Java 引擎，零第三方依赖，可在 JVM 单测
│   ├── Json.java             极小 JSON 解析/序列化（Android 与 JVM 通用）
│   ├── Models.java           配置、风格包、条款、大纲、账本、闸门结果、热修条目
│   ├── Prompts.java          策划/写作/修复/判定提示词（单点定义）
│   ├── Planner.java          策划生成 + 结构校验 + 一次修复
│   ├── Provider.java         Provider 契约
│   ├── OpenAiCompatProvider  本地与 API 共用实现（重试、超时、usage 解析）
│   ├── ProviderFactory       按设置装配
│   ├── Gates.java            G01–G12
│   ├── Judge.java            M01–M08
│   ├── LedgerParser.java     六字段硬格式解析
│   ├── Slots.java            四槽渲染（≤40 行恒定）
│   ├── Pipeline.java         单章流水线（生成→闸门→重写≤2→判定）
│   ├── ThreadItem.java       线头登记
│   ├── CostMeter.java        成本与预算
│   ├── Defaults.java         基线约束 + C/M/G 条款 + 三个风格包
│   ├── Repository.java       存储抽象
│   └── InMemoryRepository    测试实现
└── app/                      Android 层（Activity + XML 布局 + SQLite）
```

## 六、已知边界（不粉饰）

- **K 层长跑未实测**：四槽注入体量恒定已在单测里验证，但「千章不掉分」需要真实长跑数据。
- **M01–M08 依赖模型自判**：判据文本已给死，但判定质量随模型档位变化；判定失败会标注而不是默认通过。
- **本地 4B 级模型**：流程能跑通，文笔上限受模型限制；建议本地用于草稿、API 用于定稿。
- **签名**：当前为内测密钥（提交在仓库内以便 CI 稳定出包），上架前必须换成 secrets 里的正式密钥并轮换旧密钥。
- **语料与风格包**：仓库内风格包为参数化成品（含自造示例），不含任何在版作品原文。
