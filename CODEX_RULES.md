# CODEX_RULES

## 目的

这份文件是给后续接手本项目的 AI/代理看的 handoff。目标不是重复 `record.md` 的完整聊天历史，而是把“现在这份目录里真实存在的结构、状态、约束、坑点、下一步”压缩成可执行规则。

如果 `record.md`、`README.md`、源码现状三者冲突，优先级请按下面处理：

1. 当前源码与当前工作树状态
2. `record.md` 末尾的最新共识
3. `README.md`

`README.md` 已经过期，不能再当作当前状态说明。

---

## 项目定位

- 项目名：`CatastropheContract`
- 目标：给 Slay the Spire 2 的 `Custom Run / 自定义模式` 增加一套“天灾合约”挑战系统。
- 设计方向：借鉴《明日方舟》危机合约，但第一阶段只做可扩展框架，不做完整赛季制/解锁树。
- 当前长期进度只保留两类：
  - 最近一次预设选择
  - 最高挑战等级记录

明确不做或尚未做：

- 不走“官方已经提供好的 RegisterCustomRunTab 一类稳定 API”思路。
- 当前实现核心仍然是：官方模组加载机制 + `BaseLib` + `Harmony` + 反射桥接。
- 不做自动清空玩家已有进阶/原版自定义词条。
- 如果某条效果短期不稳定，宁可标记为“未实装”，不要假装已支持。

---

## 项目结构

### 根目录关键文件

- `CatastropheContract.json`
  - 模组 manifest，当前 `has_dll` / `has_pck` 都为 `true`。
- `CatastropheContract.dll`
  - 当前已有实物产物。
- `CatastropheContract.pck`
  - 当前已有实物产物。
- `record.md`
  - 历史聊天与设计/调试过程总记录。请用 UTF-8 读取。
- `README.md`
  - 早期脚手架说明，已过期。
- `plan.pdf` / `plan_extracted.txt`
  - 历史规划材料，参考价值低于 `record.md` 最新段落。

### 代码目录

- `src/Bootstrap`
  - 模组入口与初始化。
- `src/Core`
  - 日志、状态、UI view model、合约数据模型。
- `src/Content`
  - 具体 mutator 分发与反射工具。
- `src/Patches`
  - Harmony 补丁入口。
- `src/UI`
  - 当前主 DLL 实际会编译进去的 `ContractPanelNode`。
- `godot`
  - Godot 工程、打包脚本、场景与本地化。
- `godot/src/UI`
  - 另一份 Godot 脚本版 `ContractPanelNode`。

---

## 当前真实运行链路

### 启动

- `src/Bootstrap/ModuleInit.cs`
  - 通过 `[ModuleInitializer]` 初始化。
- `src/Bootstrap/ModEntry.cs`
  - 作为 Godot `Node` 再兜底初始化一次。
- 两处都会做：
  - `ContractDatabase.Initialize()`
  - `ContractStateStore.LoadPersistentState()`
  - `HarmonyBootstrap.Apply()`

### 核心补丁

- `src/Patches/CustomRunScreenPatch.cs`
  - 目标：`NCustomRunScreen._Ready`
  - 作用：把天灾合约 UI 注入到自定义模式界面。
- `src/Patches/CharacterSelectPatch.cs`
  - 目标：`NCharacterSelectScreen.OnEmbarkPressed` / `StartNewSingleplayerRun`
  - 作用：把当前选择写入 `ContractStateStore.CurrentRun`。
- `src/Patches/RunLifecyclePatch.cs`
  - 目标：`RunManager` 与 `CombatManager` 的关键生命周期方法。
  - 作用：在 `RunStart / PreCombat / TurnRule / Reward` 几个阶段应用合约效果。
- `src/Patches/HealingPatch.cs`
  - 扫描 `sts2` 里所有名为 `Heal` 的方法，做“禁疗”回滚拦截。

### 状态与存档

- `src/Core/State/ContractStateStore.cs`
  - 单一真相源。
  - 持久化文件：`user://catastrophe_contract_state.json`
  - 保存内容：
    - `LastEnabled`
    - `LastSelectedContracts`
    - `BestGlobalRisk`
    - `BestRiskByCharacter`

### 效果应用

- `src/Content/ContractMutatorRegistry.cs`
  - 负责按 `ContractApplyPhase` 分发效果。
- `src/Content/ContractRuntimeReflection.cs`
  - 负责从游戏对象里找 Player / Enemy / Creature / HP / Power / Cmd。
  - 这是目前最脆弱、也最值得谨慎修改的文件之一。

---

## 当前数据状态

### 合约库规模

当前 `ContractDatabase` 大致是：

- 6 个大类
- 25 个词条组
- 62 个等级词条

### 代码里标记为已实装的组

- 敌方强化：
  - `thorn`
  - `great_awakening`
  - `activating`
  - `metallization`
  - `industrialization`
- 玩家限制：
  - `erosion`
  - `burning`
  - `secret_battle`
  - `high_valued_object`
- 经济/路线：
  - `economic_crisis`
  - `run_out`
  - `tightened_belt`
- 特殊规则：
  - `ultimate_defense`
  - `countdown`

### 明确标记为未实装的组

- `bloodthirsty`
- `debris_covered`
- `swarming_elites`
- `congregating_bosses`
- `malaise`
- `restriction`
- `secret_action`
- `inefficiency`
- `antidetection`
- `linear_battlefield`
- `counterforce`

### 用户实机已基本确认通过的点

根据 `record.md` 最新调试结论，以下项目已经被用户实机验证到可用或“最后一次复测通过”：

- `activating`
- `burning`
- `secret_battle`
- `high_valued_object`
- `erosion`

### 代码写了但仍应视为“待复核/待实机”的点

- `thorn`
- `great_awakening`
- `metallization`
- `industrialization`
- `economic_crisis`
- `run_out`
- `tightened_belt`
- `ultimate_defense`
- `countdown`

尤其注意：

- `economic_crisis` 在历史记录里一直是 best-effort 奖励挂接，不应默认当成 100% 稳定。
- `run_out` 依赖 `HealingPatch` 扫描全局 `Heal` 方法并回滚 HP，这种实现宽而重，后续一定要谨慎回归测试。

---

## 最新明确但尚未落地的需求

这是 `record.md` 最后阶段已经确认、但当前代码还没完全跟上的内容：

### 1. `burning` 数值要从 `1/2/3` 改成 `2/4/6`

当前源码里 `burning` 仍是：

- I: 1 层虚弱 + 1 层脆弱
- II: 2 层
- III: 3 层

这与最后一轮用户要求不一致。后续改动时请同步更新：

- `ContractDatabase` 文案
- 实际效果值
- 如果需要，风险值重新校准

### 2. 增加“天灾合约 与 进阶/原版自定义词条互斥”的开始前校验

这是当前最明确的下一步规则决策：

- 启用天灾合约时，与 `Ascension` 互斥
- 启用天灾合约时，与原版自定义词条互斥
- 冲突策略不是自动清理
- 而是：在开始 run 前拦截，并给出明确提示

当前代码里还没有这套逻辑。

### 3. 下一批优先推进 `坍缩 / 悖论`

优先顺序已经在聊天里定过：

1. `economic_crisis`
2. `tightened_belt`
3. `run_out`
4. `ultimate_defense`
5. `countdown`

如果实机不稳，继续降级为未实装，不要硬留“已支持”状态。

---

## 当前最重要的已知坑

### 1. UI 有两套实现，而且现在很可能不一致

这是最容易坑后手的地方。

当前仓库里同时存在：

- `src/UI/ContractPanelNode.cs`
  - 主 DLL 会编译这份。
  - 这份更像旧版“大面板”逻辑。
- `godot/src/UI/ContractPanelNode.cs`
  - 这是另一份脚本实现。
  - 这份是“小入口 + 弹窗覆盖层”逻辑，更接近 `record.md` 后期描述。
- `godot/ui/CatastropheContractPanel.tscn`
  - 指向的是打进 pck 的脚本路径 `res://src/UI/ContractPanelNode.cs`。
- `src/Patches/CustomRunScreenPatch.cs`
  - 当前实际是直接 `new ContractPanelNode()` 注入，而不是 `PackedScene.Load().Instantiate()`。

这意味着：

- 当前“源码中真正被 DLL 直接实例化的 UI”
  很可能还是 `src/UI/ContractPanelNode.cs`
- 而不是 `godot/src/UI/ContractPanelNode.cs`

如果后续 AI 要继续做 UI，请先统一这一点，不要在两套脚本上来回改。

建议方向：

- 只保留一个 `ContractPanelNode` 事实来源
- 明确到底是：
  - 直接实例化 C# Node
  - 还是通过 `.tscn` / `PackedScene` 加载

### 2. README 已过期

`README.md` 仍然写着“当前机器没有 .NET SDK 和 Godot 工具链，暂时不能产出二进制”，这已经不是当前状态。

当前目录里已经存在：

- `CatastropheContract.dll`
- `CatastropheContract.pck`

所以不要再按 README 判断项目是否仍停留在脚手架阶段。

### 3. 构建脚本与工程路径有硬编码

以下文件都带有强机器依赖：

- `godot/tools/build_pack.gd`
  - `OUTPUT_PATH` 与 `ROOT_PATH` 都是绝对路径。
- `godot/CatastropheContractGodot.csproj`
  - `RestoreSources` 写死到本机 Godot 安装目录。
- `tools/build.ps1`
  - 只是示意脚本，而且示例里仍用了带空格的旧文件名 `Catastrophe Contract.pck`。

也就是说：

- 构建环境不是通用模板
- 切换机器或目录后，先修这些路径

### 4. 当前工作树是脏的

本次读取时，工作树不是干净状态。请不要默认 HEAD 就代表“正在跑的版本”。

已修改文件包括：

- `CatastropheContract.dll`
- `src/Content/ContractMutatorRegistry.cs`
- `src/Content/ContractRuntimeReflection.cs`
- `src/Core/Contracts/ContractDatabase.cs`
- `src/Core/ModLogger.cs`
- `src/Core/State/ContractRunState.cs`
- `src/Core/State/ContractStateStore.cs`
- `src/Patches/RunLifecyclePatch.cs`

还有未跟踪内容：

- `.appdata/`
- `.dotnet-home/`
- `build/`
- `godot/.godot/`
- `plan.pdf`
- `plan_extracted.txt`
- `record.md`

后续 AI 不要无脑清理或回滚这些内容。

---

## 日志与调试规则

### Build 标记

- 当前 `src/Core/ModLogger.cs` 的 `BuildMarker` 是：
  - `2026-05-15-erosion-retry-v1`

建议后续每次产出新的 DLL 并让用户实机验证时：

- 修改 `BuildMarker`
- 重新编译 DLL
- 再让用户看日志

这样才能确认用户跑到的是哪一版。

### 日志位置

- 模组自己的日志：
  - `mods/CatastropheContract/debug.log`
- Godot / 游戏日志：
  - 用户历史上常看的还是 `godot.log`

### 已知测试纪律

历史里已经证明过一个坑：

- 不能拿“继续中的旧局”验证新选的合约
- 必须：
  - 重新进入 `开始游戏 -> 自定义模式`
  - 重新启用天灾合约
  - 新开一把 run

否则 `CurrentRun.Enabled` 可能是 false，导致误判“词条没生效”。

---

## 后续建议的执行顺序

如果新的 AI 继续开发，我建议按下面顺序推进：

1. 先统一 UI 事实来源。
   - 确认当前到底要保留 `src/UI` 还是 `godot/src/UI`。
   - 如果目标仍然是“小入口 + 居中弹窗”，那就把注入逻辑与脚本源统一掉。

2. 再改 `burning` 为 `2/4/6`。
   - 这是用户最后明确提出且尚未落实的数值变更。

3. 再做“开始前冲突校验”。
   - 重点不是自动改玩家设置，而是阻止开局并提示。

4. 再复核 `坍缩 / 悖论` 这一批已标记 implemented 的词条。
   - 稳定就保留
   - 不稳就降回未实装

5. 最后才考虑扩展新的未实装机制。
   - 不建议现在就扩到地图大改、Boss 多目标、X 费改写之类更重的玩法层。

---

## 对后续 AI 的操作规则

- 优先信源码，不要优先信 README。
- 优先看 `record.md` 最末尾，而不是中段调试过程。
- 涉及 UI 前，先处理双实现分叉。
- 涉及数值改动时，文案、效果值、风险值要一起看。
- 涉及运行态问题时，先看 `ContractStateStore` 是否真的拿到了启用状态与词条列表。
- 涉及战斗效果问题时，优先怀疑 `ContractRuntimeReflection` 的目标解析与方法签名匹配。
- 不要把“不稳定但偶尔生效”的词条继续标记成 implemented。
- 不要顺手清工作树、删缓存、回滚 DLL，除非用户明确要求。

---

## 一句话结论

这个项目已经不是空脚手架，而是一个“有真实 DLL/PCK、有一批已跑通的基础词条、有完整状态链和补丁框架，但仍高度依赖反射且 UI 源码存在分叉”的进行中模组。后续接手时，最该优先解决的是一致性问题，而不是盲目继续加新词条。
