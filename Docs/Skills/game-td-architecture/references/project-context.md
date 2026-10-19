# 项目语义与接手入口

## 长期语义

游戏方向为偏高斜俯视美术、Rectangle 网格的塔防建设游戏：白天建设，夜晚防守，角色参战，部队指挥，布局调整、任意方向城墙和可更新关卡。
首版固定镜头、单一逻辑地面高度、单机是范围假设；真高度、曲墙、复杂生产链和联网不视为已承诺功能。

- 玩家出战选择角色与特技。角色固定绑定武器和专属技能，武器不是独立选择项。
- 角色技能与可选特技明确区分。特技影响策略、属性或许可，不覆盖角色身份绑定。
- 变异是开发者内部关卡调校，不做玩家装备、解锁或选择入口。玩家仍按情报规则获得实际威胁信息。
- 本局有效规则由基础内容、角色、特技与关卡调校组合，运算顺序和来源可追踪；临时 Buff 不误删持久规则。
- 阶段可区分加载、白天规划、入夜准备、夜间战斗、白天结算、胜败、恢复；暂停是时钟状态。
- 夜晚结束同时检查场上存活、待生成波次、Boss 与目标条件，不能只看当前敌人数。
- 编组长期命令和成员个人临时交战目标分开；成员归属只有一处真值，反向索引可重建。
- 对局快照整体恢复，局外档案与永久奖励领取标识不倒退，奖励防重复。

这些约定来自用户领域文档与本次架构讨论；后续明确需求可以更新，不把未决定玩法包装成硬性规范。

## 当前代码的阅读方法

不要把下列历史路径当作永不变更的 API；接手时实际检索并读取文件。

1. 场景：`Assets/Scenes/Gameplay/BattlePrototype.unity`。
2. 组装与入口：`Assets/Game/Bootstrap/BattleLifetimeScope.cs` 中的 Configure 与 BattleEntryPoint。
3. 应用流程：`Assets/Game/Gameplay/Battle/Application/Sessions/BattleSession.cs` 的 Restart、Advance、Refresh。
4. 契约：同模块 `Contracts/Commands/MoveIntent.cs`、`ReadModels/UnitSnapshot.cs` 与 `Ports/` 的三个独立接口。
5. ECS：`Simulation/Components/` 的独立组件、`Bridges/EcsBattleSimulation.cs` 与 `Systems/BattleSystemGroup.cs`，再看导航、移动和接触系统。
6. 配置：`Assets/Game/Infrastructure/Configuration/JsonBattleDefinitionSource.cs` 与 `Assets/Content/Combat/Prototype/battle.json`。
7. 输入与画面：`Assets/Game/Presentation/Input`、`Views`、`UI`。
8. 工具与测试：`Assets/Game/Tools/Prototype/Editor`、Battle 的 Tests。

基本数据流：输入 -> 意图 -> 固定步模拟 -> 唯一状态 -> 只读快照 -> Sprite/动画/HUD。
基本启动流：场景 LifetimeScope -> DI 组装 -> Start/Restart -> 配置验证 -> ECS 初始化 -> 每帧推进。

## 不要固化的历史实现

10 只怪物、圆形接触伤害、随机闲逛、SpriteRenderer 显示、死亡实体保留到重启、首版静态图都属于原型阶段。
其他对话可能已增加动画、死亡表现或回收；先读当前代码以及 `Docs/BattlePrototype.md`、相关功能文档。
当前 VContainer/Entities/Unity 版本通过 ProjectVersion、manifest、lockfile 与内置包核对；不为了技能中的历史版本升级/降级。
VContainer 原型曾采用项目内置包并关闭可选 ECS/UniTask 注入，用桥接器连接 ECS；这是兼容性选择，不强制以后重复。
临时字体/Shader 或生成工具的历史解决方式不是架构要求，不自动复刻到新增界面。

## 说明代码时

用户熟悉服务端 DDD，但需要 Unity/ECS 的具体入口和执行顺序。
先给出真实场景与入口，分别解释一次性初始化和每帧/每步执行，再沿一个输入或命中用例追踪状态和画面。
解释 World、Entity、Component、System、Job 和普通服务各负责什么；明确哪些代码由 Unity/DI 回调，哪些由桥接器手动驱动。
第一次阅读让用户跳过生成工具、字体 Shader 和第三方源码。链接到实际文件与核对过的行号，不堆抽象术语。
