# DDD + ECS 代码审查与重构记录

日期：2026-10-20。

## 范围与结果

检查 `Assets/Game` 自有代码的程序集依赖、领域/契约分类、模拟状态与写入职责、输入及表现桥接、会话推进、序列化字段与测试入口。第三方包和生成代码不纳入命名重构。此记录不表示已完成手机性能或全部未来玩法的验证。

现有分层方向基本正确：纯 Domain/Application/Contracts 使用 noEngineReferences；Simulation 依赖 Entities 并实现公开端口；输入只产生意图，表现读取快照；生命和位置的权威状态由 ECS 保存。没有为了目录齐全创建当前没有业务需求的聚合、仓储或领域服务。

## 已完成的重构

| 原问题 | 本次调整 |
| --- | --- |
| BattleDefinition.cs 同时定义枚举、单位定义与战斗定义 | 拆为 Domain/Definitions 下的三个定义及 ValueObjects/UnitKind；纯规则移到 Domain/Rules |
| BattlePorts.cs 包含五个独立公共类型 | 分到 Contracts/Commands、ReadModels、Ports，每个公共入口使用同名文件 |
| BattleComponents.cs 汇集十个不同组件 | 每个组件独立文件，补充状态归属和写入者说明 |
| 系统组、启动入口、接触测试混在其他主类型文件 | 独立 BattleSystemGroup.cs、BattleEntryPoint.cs、ContactSystemTests.cs |
| 会话生命周期缺少归类，分类目录与命名空间不对应 | BattleSession 移至 Application/Sessions；Battle 内分类命名空间及所有引用同步更新 |
| 私有序列化字段命名不统一 | 改为 _camelCase，使用 FormerlySerializedAs 保留旧场景绑定；保留已有脚本 GUID |
| 关键流程多个语句同一行、资源选择多层三元 | 展开多行流程，视图资源选择改为显式 switch；统一自有代码基本格式 |
| Units 返回内部 List，虽然签名为 IReadOnlyList 仍可强转修改 | 使用可复用的只读包装，不逐帧额外复制列表；明确是随 Refresh 更新的只读活视图 |
| NaN/Infinity 帧时间会污染累加器 | 会话与模拟在修改状态前拒绝非有限时间；模拟拒绝非正步长 |
| 输入和连续地图坐标可接受 NaN/Infinity | MoveIntent、MapPoint 构造时验证；MapPoint 补充按值相等语义 |
| 未知 UnitKind 会落入 Runner 分支 | 定义构造和 GetUnit 查询明确拒绝未知分类 |
| 重构后旧文档入口失效 | 更新项目专用技能的阅读路径和目录说明 |

保留已有 ECS 类型名（Vitals、GroundTransform 等）、MapEngine 的 Grid/Navigation 组织、集中 Presentation、Tests/Editor 和既有测试命名风格。这些是既有明确组织方式，通用技能允许沿用；未机械添加 Component 后缀或把所有 MonoBehaviour 移进业务模块。

## 实际阅读入口

1. 场景：`Assets/Scenes/Gameplay/BattlePrototype.unity`。
2. 组装：`Assets/Game/Bootstrap/BattleLifetimeScope.cs`；唯一帧驱动：同目录 `BattleEntryPoint.cs`。
3. 应用：`Assets/Game/Gameplay/Battle/Application/Sessions/BattleSession.cs`。
4. 边界：`Contracts/Ports/IBattleSimulation.cs`、`Commands/MoveIntent.cs`、`ReadModels/UnitSnapshot.cs`。
5. 模拟：`Simulation/Bridges/EcsBattleSimulation.cs`，再读 `Systems/MonsterNavigationSystem.cs`、`MovementSystem.cs`、`CircleContactSystem.cs`。
6. 表现：`Assets/Game/Presentation/Views/BattleView.cs`、`UnitView.cs`。

启动：LifetimeScope.Configure -> BattleEntryPoint.Start -> BattleSession.Restart -> 配置加载验证 -> 模拟 Reset -> 只读快照。

每帧：BattleEntryPoint.Tick -> BattleSession.Advance -> 固定步 Step -> 导航、移动、接触结算 -> CopyUnits -> BattleView.LateUpdate / HUD。World 不额外接入默认 PlayerLoop。

## 规范仍需补充的细则

以下是现有规范未具体规定或只表达原则、尚缺统一执行标准的部分，未擅自改写通用技能：

| 缺项 | 建议补充的标准 | 本项目的处理 |
| --- | --- | --- |
| 有状态应用会话的分类 | Application/Sessions 管跨帧编排，UseCases 管一次性用例；明确其与 DI 作用域、World 的生命周期关系 | 本次采用 Sessions，写入项目约定 |
| Unity 序列化迁移 | 字段改名使用 FormerlySerializedAs；脚本移动保留 .meta/GUID；类型/程序集变更涉及 SerializeReference 时另行制定迁移 | 本次保留 GUID 并核对场景绑定 |
| JSON/第三方命名例外 | DTO 字段遵循外部 schema，不因 C# 命名规范改变已有 JSON 键；公开字段只允许在序列化边界按需存在 | 保留 JSON DTO 的 camelCase 和表现配置既有键 |
| 只读读模型的有效期 | 区分不可变时点快照与只读活视图，禁止强转回内部可变容器；定义保存/跨线程使用方式 | Units 使用只读活视图，单个 UnitSnapshot 是值副本 |
| 数值和错误边界 | 规定 NaN/Infinity、未知枚举、非法时间、负数和溢出的处理；区分调用错误、内容错误与预期业务失败 | 本次补齐已发现的输入、时间、坐标与分类校验 |
| 具体格式化标准 | 确定格式化工具、长参数换行、using 顺序、是否强制大括号及自动检查方式 | 本次统一多行基本格式；尚未引入格式化 CI |
| 内容文本与运行配置一致性 | 动态数量、规则和提示从实际配置/状态生成，避免创作场景里的固定文案变成错误说明 | 验证截图仍可见旧的“6 GOBLINS + 4 RUNNERS”静态说明，而当前 JSON 是 1000 怪物；这是现有场景内容待调整项，未在代码重构中重建场景 |

## 验证证据与边界

- Unity 6000.6.4f1，项目 manifest 的 Entities 为 6.6.0；没有升级游戏依赖。
- Unity recompile：0 error、0 warning。
- 最终 Game.Battle.Tests EditMode：77 项通过，0 失败、0 跳过。完整结果：`Validation/architecture-tests.json`。
- 新测试覆盖只读查询防修改、非有限帧时间后继续推进、补步上限、配置加载失败保留当前局、坐标值语义、非法分类和模拟时间边界。
- 当前场景无 Missing Script；本次改名的 21 个自有序列化引用全部有效，未保存或重建原型场景。
- Play Mode 确认真实帧推进：frameCount 从 958 变为 971；随后读到 1001 个单位及正向推进的模拟时间。
- 使用公开 Restart 入口验证 Generation 从 1 变为 2，模拟时间归零，单位数仍等于配置数量加英雄。验证完成后退出 Play Mode。
- 已检查运行截图：`Validation/architecture-refactor.png`。截图只证明该次编辑器画面，不作为设备性能结论。
- Console 历史缓冲有一条此前 TMP Essential Resources 提示；本次检查的编辑器当前错误计数为 0，未因此导入无关资源包。
- 未执行 Android/iOS 构建、真机性能、跨平台确定性或不存在的建造/存档功能验证。

所有修改留在当前工作区，未提交 Git。
