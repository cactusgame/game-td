---
name: unity-ddd-ecs-architecture
description: 设计、实现或评审 Unity 游戏的 DDD 与 ECS 混合架构，明确领域边界、状态归属、系统写入职责、跨模块一致性和表现桥接。适用于玩法模块拆分、Entities 模拟、存档恢复与架构演进；不替代具体项目约定或 Unity 专项工具指南。
---

# Unity 游戏开发 DDD + ECS 架构规范

DDD 用于表达领域语义、职责与一致性边界；ECS 用于组织适合批量处理的运行状态和系统；Unity 负责内容创作、引擎适配与表现。按业务复杂度、数据规模和实际性能选择实现，不要求所有游戏或所有模块使用 ECS。

## 使用顺序

1. 阅读目标仓库的 AGENTS.md、项目架构规范和相关实际入口。现有架构与用户明确要求优先，不因加载本技能自动重构或安装框架。
2. 先明确本次玩家行为、涉及的业务模块、权威状态、必须同时成立的不变量、提交时点与可观察结果。
3. 领域建模或模块拆分时读 [领域与依赖](references/domain-and-boundaries.md)；修改 ECS、Job、模拟生命周期时读 [模拟与桥接](references/simulation-and-bridges.md)；涉及建造、升级、死亡后果、恢复或验证时读 [一致性、用例与验证](references/consistency-and-validation.md)。只加载任务相关参考。
4. 编码前核对 Unity 版本、Packages/manifest.json、packages-lock.json 与实际包源码。按当前版本查官方文档；技能中的概念顺序不代表可直接复制的 API。
5. 实现完整行为路径，执行与风险相称的验证，交付真实入口、状态归属和结果证据。

## 核心原则

- 模型与实现共享领域语言。以 `BuildTower`、`ApplyDamage`、`GrantReward` 表达意图，避免无语义的 `Process`。`System`、`Component`、`Adapter` 等技术后缀在确有职责时保留。
- 按业务领域组织上层模块，在模块内部按需分 Domain、Application、Contracts、Simulation、Unity、Infrastructure；目录不是限界上下文的证明。
- 普通对象领域使用有行为的实体、值对象与聚合维护不变量，应用服务组织用例；不要让对象只剩公共可变属性。
- ECS 模拟用数据组件、纯规则、明确写入职责和系统顺序共同维护不变量。组件只有数据是合理设计，不为满足“充血模型”给每个 Entity 创建托管聚合副本。
- 每份可变业务状态只有一个权威拥有者。只读快照、缓存和存档副本必须说明来源、版本、有效时点和重建方式，不能各自裁决玩法。
- DDD Entity 是领域身份；Unity Entity 是某个 World 内的技术句柄；GameObject 是引擎对象。三者不等同。跨会话身份使用稳定领域 ID。
- 高频模拟与低频业务可以存在于同一业务上下文内。不要机械规定“局内只能 ECS、局外只能 DDD”，也不要按英雄、怪物、塔复制整套架构。
- 命令表达意图并产生明确结果；查询只读；事件表达已提交事实。异步与最终一致性是有条件的选择，不能依赖事件订阅顺序维持扣费、建造、奖励等核心一致性。
- 不向领域层引入 UnityEngine、Entities、场景、容器、存储实现。确有收益的数值库依赖需记录理由并验证目标运行环境；Burst 规则只使用其实际支持的数据与操作。
- 表现读取状态，输入转换为意图。伤害、死亡、奖励与胜负不能由动画结束或 UI 回调独自裁决。
- 通过公开契约协作，不跨模块任意修改聚合内部或 ECS 组件；共享能力只提取已证实稳定的部分。

## 代码规范

新增领域模块及文件统一采用下列分类。仅在存在对应代码时创建目录；目录名使用完整英文复数，不混用 `Entity/Entities`、`VO/ValueObjects`、`Service/Services`、`Repo/Repositories`。已有项目的明确约定优先，差异需记录，不自动批量迁移。

| 目录 | 职责与内部分类 | 命名示例 |
| --- | --- | --- |
| `Domain/` | 领域实体、聚合、值对象、规则、领域服务、仓储接口与领域事实；不依赖 Unity/ECS | 见下表 |
| `Contracts/` | `Commands/` 意图、`Queries/` 查询请求、`Results/` 执行结果、`ReadModels/` 只读快照、`Events/` 集成事件、`Ports/` 公开能力接口 | `BuildTowerCommand`、`GetTowerQuery`、`BuildTowerResult`、`TowerSnapshot`、`TowerBuiltIntegrationEvent`、`IFacilityCombatPort` |
| `Application/` | `UseCases/` 用例、`Coordinators/` 跨对象/模块协调、`Ports/` 应用内部所需端口、`Mappings/` 契约转换 | `BuildTowerUseCase`、`TowerConstructionCoordinator`、`IConstructionReservationPort`、`TowerSnapshotMapper` |
| `Simulation/` | `Components/` ECS 数据、`Systems/` 系统、`Jobs/` 独立 Job、`Aspects/` 组合访问、`Bridges/` 托管边界、`Definitions/` 模拟只读定义 | `HealthComponent`、`DamageResolutionSystem`、`CollectHitsJob`、`CombatantAspect`、`BattleSimulationBridge`、`CombatStatsDefinition` |
| `Unity/` | `Authoring/` 创作组件、`Baking/` 转换、`Views/` 表现、`Input/` 输入适配、`Adapters/` 引擎能力适配、`Assets/` 领域专用资产类型 | `TowerAuthoring`、`TowerBaker`、`TowerView`、`BuildTowerInputAdapter`、`UnityPhysicsQueryAdapter`、`TowerDefinitionAsset` |
| `Infrastructure/` | `Persistence/` 存档与仓储实现、`Networking/` 网络、`Platform/` 平台、`Configuration/` 外部定义加载 | `JsonPlayerProfileRepository`、`PlayerProfileSaveData`、`BattleNetworkGateway`、`JsonTowerDefinitionLoader` |
| `Editor/` | 编辑器窗口、Inspector、导入及创作工具；使用 Editor-only 程序集 | `TowerDefinitionEditor`、`TowerPreviewWindow` |
| `Tests/` | 按 `Domain/`、`Application/`、`Simulation/`、`Integration/` 分类；EditMode/PlayMode 由测试程序集标识 | `DamageRulesTests`、`BuildTowerUseCaseTests` |

Domain 内部统一细分如下，分类依据是职责而不是类的大小：

| 子目录 | 放置规则 | 类型与文件命名 |
| --- | --- | --- |
| `Aggregates/` | 聚合根及其聚合专属子实体；按聚合根名称再建子目录，整个聚合模型放在一起 | `Aggregates/ResourceAccount/ResourceAccount.cs`；根类型不强加 `Aggregate` 后缀 |
| `Entities/` | 不属于已有聚合内部、确实具有独立身份的领域实体；不得用它绕过一致性边界 | `SquadMember.cs`；类型不加 `Entity` 后缀 |
| `ValueObjects/` | 不可变、按值相等的领域概念与强类型 ID；校验合法值 | `ResourceAmount.cs`、`TowerId.cs`；不加 `VO` 后缀 |
| `Services/` | 无法自然归属实体/值对象的领域行为；不协调存储、UI 或跨模块事务 | `TargetSelectionService.cs`，必要接口 `ITargetSelectionService.cs` |
| `Rules/` | 无副作用的计算规则、策略与判定；Burst 可复用部分保持兼容 | `DamageRules.cs`、`UpgradeEligibilityRule.cs` |
| `Repositories/` | 围绕聚合根的仓储接口；实现放 Infrastructure | `IPlayerProfileRepository.cs`，方法 `FindById`、`Save` |
| `Factories/` | 复杂领域对象的有效创建；不实例化 GameObject 或 ECS Entity | `EncounterFactory.cs`，方法 `Create` |
| `Events/` | 本上下文已提交的领域事实；与对外集成事件区分 | `TowerBuilt.cs`、`UnitDied.cs`，使用过去式 |
| `Specifications/` | 确有复用需求、可组合的领域判定；不封装 SQL/EntityQuery | `UpgradeableTowerSpecification.cs`，方法 `IsSatisfiedBy` |
| `Definitions/` | 引擎无关的只读领域配置；不放 ScriptableObject 或对局可变状态 | `TowerDefinition.cs` |
| `Errors/` | 稳定领域失败原因；预期失败用结果表达，异常用于异常路径 | `ConstructionErrorCode.cs`、`InsufficientResourcesError.cs` |

一个类型只保留一份定义。聚合专属子实体放 `Aggregates/<Root>/`，不再复制到 `Entities/`；独立复用的值对象进入 `ValueObjects/`。具体分类、接口归属、ECS 数据命名及成员规范见 [目录与命名细则](references/code-conventions.md)，新增或评审代码时读取。

文件与主要类型同名，命名空间对应领域与目录，如 `Game.Facilities.Domain.ValueObjects`。类型/方法/属性用 PascalCase，参数/局部变量用 camelCase，私有实例字段用 `_camelCase`；接口用 `I` 前缀。领域行为用业务动词，布尔成员用 `Is/Has/Can`，异步方法用 `Async` 后缀。禁止用 `Manager`、`Helper`、`Utils` 掩盖未划分的职责。

## 建模时必须回答的问题

| 问题 | 应形成的结论 |
| --- | --- |
| 玩家或策划如何描述行为？ | 一致的领域术语与用例名称 |
| 哪些规则必须同时成立？ | 聚合/模拟提交边界与失败语义 |
| 谁保存真实状态？ | 状态所有者、唯一写入者或受控归并者 |
| 为何使用对象或 ECS？ | 规则复杂度、访问模式、规模与测量依据 |
| 模块如何协作？ | 命令、查询、结果、事件、端口和依赖方向 |
| 谁创建并结束生命周期？ | 会话、World、Native 数据和表现的所有者 |
| 如何恢复并拒绝过期工作？ | 快照边界、稳定 ID、会话代次与去重依据 |

任务简单时用短说明即可，不要求先生成完整架构文档或空目录。

## 代码与交付要求

- 关键流程使用常规多行格式和明确命名。重要类/方法解释职责、调用者、状态归属；Job 依赖、归并和提交顺序解释原因。
- 配置定义、运行状态、表现资源分离。ScriptableObject 或 JSON 是内容入口，不充当共享可变对局状态。
- 接口用于真实替换点、边界或外部依赖，不为每个类机械创建接口；不默认要求 CQRS 框架、事件溯源、Actor、仓储或 DI 容器。
- 测试验证行为、不变量、失败路径、时序与生命周期，不仅验证类结构或调用次数。性能结论基于目标设备和预计峰值。
- 交付说明：完成的业务路径；权威状态和调用顺序；少量真实阅读入口；已执行验证及边界；确实存在的风险或待决策项。

## 快速诊断

| 检查 | 失败时的修正 |
| --- | --- |
| 名称可对应玩法语言，边界有业务依据？ | 明确术语与责任，避免技术目录冒充领域边界 |
| 对象模型有行为，ECS 有规则与受控写入？ | 把规则放回状态所有者的行为/系统，避免双份状态 |
| 不变量有明确执行和提交时点？ | 写出校验、提交、失败恢复和系统顺序 |
| 跨模块依赖单向，外部模型在边界转换？ | 通过端口、契约或必要的反腐败转换隔离 |
| 命令结果与已提交事件一致？ | 区分已接收、处理中、成功、失败和取消 |
| 并行写入、销毁与快照有同步边界？ | 明确 Job 依赖、归并、结构变化和快照阶段 |
| 重试、读档和旧异步结果不会重复生效？ | 使用命令/事实 ID、会话代次及必要版本检查 |
| ECS 有实际规模或访问模式收益？ | 测量后选择；避免为了形式转换低频业务 |

## 来源与维护

仙人掌，《扫描仙人》创始人、独立软件开发者。

本技能定义通用决策规则，不把示例中的玩法、目录、框架和数量当成永久限制。
