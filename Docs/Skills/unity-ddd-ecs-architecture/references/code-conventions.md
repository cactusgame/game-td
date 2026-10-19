# 目录与命名细则

## 统一分类规则

本规范是新代码的默认标准，不提供多套同义目录供个人选择。采用 `Entities`、`ValueObjects`、`Services`、`Repositories` 等完整名称。仅创建有代码的目录，不生成占位类。复杂模块可在分类目录内按具体业务再分组，不另建平行的 Models、Common、Helpers 存放无法归类的内容。

文件夹表示职责，不自动对应独立 asmdef。命名空间统一以项目代码根为前缀，随后是领域、层和分类，例如 `Game.Facilities.Application.UseCases`；实际项目已有 `Game.Gameplay.Facilities` 前缀时保持一致。命名空间不包含 `Assets`、磁盘路径或文件名。不要混用不同项目根前缀。

示例阅读路径：

```text
Facilities/
  Contracts/Commands/BuildTowerCommand.cs
  Contracts/Results/BuildTowerResult.cs
  Application/UseCases/BuildTowerUseCase.cs
  Domain/Aggregates/Tower/Tower.cs
  Domain/ValueObjects/TowerId.cs
  Domain/Rules/UpgradeEligibilityRule.cs
  Domain/Events/TowerBuilt.cs
  Simulation/Components/TowerCombatComponent.cs
  Simulation/Systems/TowerAttackSystem.cs
  Unity/Views/TowerView.cs
```

此示例中 Tower 聚合拥有建造、等级等业务状态，战斗状态归 ECS；目录并不授权复制生命等权威数据。

## Domain：分类和归属裁决

### Aggregates 与 Entities

- 聚合根使用业务名称，如 `ResourceAccount`、`PlayerProfile`，不默认追加 `AggregateRoot`、`Model` 或 `Data`。
- 按根类型建立 `Aggregates/<RootName>/`，放根与其专属子实体。例：`Aggregates/PlayerProfile/PlayerProfile.cs` 和 `UnlockedHero.cs`；命名空间对应该目录。
- `Entities/` 只收纳聚合之外建模的独立身份对象。如果独立实体自身就是一致性根，将其归为聚合，不凭体积大小决定放置位置。
- 聚合专属规则和类型可与聚合共置；同层多个聚合确实复用的值对象、规则进入统一分类。不要同时建立两份同名类型。
- 状态修改方法使用 `Spend`、`Upgrade`、`CompleteQuest` 等业务动词，不用 `SetState`、`UpdateData` 代替领域行为；不暴露子实体的任意写入口。

### ValueObjects 与 Definitions

- 值对象使用概念名，如 `ResourceAmount`、`WorldPosition`、`AttackRange`；身份类型统一为 `<Concept>Id`，如 `TowerId`，不混用 `ID`、`Key` 和裸字符串表达同一身份。
- 值对象对外不可变，创建时检查有效性，按组成值比较。可以使用 readonly struct、record 或不可变 class，按项目版本及实际需要选型，不以语法代替不变量验证。
- 独立单文件枚举与对应概念同名并放在最相关分类；无需另设全局 Enums 杂物目录。
- `Definitions/` 放只读领域定义，例如 `TowerDefinition`。ScriptableObject 类型归 `Unity/Assets/`，外部 JSON/存档 DTO 归 Infrastructure；领域定义不携带 GameObject、Entity 或可变对局状态。

### Services、Rules 与 Specifications

- `Services/` 用 `<业务能力>Service` 表示领域服务，如 `TargetSelectionService`。其输入输出是领域数据，不组织存档、加载、扣费与 ECS 注册等应用流程。
- `Rules/` 单项规则用 `<业务判定>Rule`，一组相关纯计算用 `<业务概念>Rules`。例如 `UpgradeEligibilityRule.CanUpgrade`、`DamageRules.CalculateDamage`。
- 可组合的真假判定才使用 `<业务条件>Specification` 和 `IsSatisfiedBy`，不为简单 if 或数据库过滤机械创建规格类。
- 已有逻辑能自然属于聚合/值对象时，优先放其行为内；没有复用或隔离收益，不拆成服务。跨实体纯计算可放规则，不需要再包一层同名 Service。

### Repositories、Factories 与 Events

- 默认仓储接口放 `Domain/Repositories/I<Aggregate>Repository.cs`；只按聚合根设计，不按表、组件或每个实体设计。
- 方法使用 `FindById`、`FindBy...`、`Save`、`Remove` 等具体名称，明确未找到语义；异步版本加 `Async`，并按项目约定支持取消。不要统一暴露 IQueryable、SQL 或存储 SDK 类型。
- 实现以技术方案前缀命名，如 `JsonPlayerProfileRepository`、`InMemoryPlayerProfileRepository`，放 `Infrastructure/Persistence/Repositories/`。不再同时建立 `Domain/Ports/` 和 `Contracts/Ports/` 的同义仓储接口。
- 工厂用 `<创建对象>Factory`，创建方法默认 `Create`；用于返回预期校验失败时可用 `TryCreate`。普通构造足够时不额外创建工厂。
- 领域事件用过去式业务事实，如 `TowerBuilt`。对外事件放 `Contracts/Events/TowerBuiltIntegrationEvent.cs`，由提交边界转换，不直接发布聚合内部可变对象。
- 预期失败使用稳定错误代码或不可变错误值；异常类型以 `Exception` 结尾，不把每次资源不足都当作异常抛出。

## Contracts 与 Application

- 命令名为 `<动作><对象>Command`，查询名为 `Get/Find/List<对象>Query`，执行结果名为 `<用例>Result`，快照名为 `<对象>Snapshot`，列表条目可用 `<对象>Summary`。
- 命令携带意图而非可变聚合；查询和结果不泄露内部集合、EntityManager 或技术存储类型。不要用统一 `Dto` 后缀掩盖用途。
- 用例类统一为 `<动作><对象>UseCase`，入口默认 `Execute` 或 `ExecuteAsync`。不在同一项目同时采用 UseCase、Handler、ApplicationService 三套名称表达同类职责；现有项目已确定其中一种时沿用。
- 真正独立的跨模块流程用 `<业务流程>Coordinator`，不把每个用例再包装成协调器。独立校验器仅校验请求形状/前置条件，不复制领域规则。
- 公开跨模块端口放 `Contracts/Ports/`，应用专用外部依赖放 `Application/Ports/`，默认命名为 `I<业务能力>Port`；仓储沿用 Repository 命名。接口只有一个权威定义，其消费方拥有依赖需求。
- 既有架构明确把仓储端口归 Application 时沿用并记录差异；不得因“接口都在 Contracts”把全部内部仓储变为公共能力。
- 映射器用 `<目标类型>Mapper`，方法 `ToSnapshot`、`ToDomain` 等；映射只转换，不隐式扣费、创建实体或发布事实。

## Simulation

| 子目录 | 类型命名 | 约束 |
| --- | --- | --- |
| `Components/` | `<概念>Component` | IComponentData 数据，如 HealthComponent；不等同 DDD Entity |
| `Components/` | `<状态>Tag` | 纯标记组件，如 DeadTag |
| `Components/` | `<条目>BufferElement` | IBufferElementData 条目，如 DamageRequestBufferElement |
| `Systems/` | `<业务动作>System` | 如 DamageResolutionSystem；说明读写集合和阶段 |
| `Systems/` | `<阶段/业务>SystemGroup` | 系统组；顺序由显式约束维护 |
| `Jobs/` | `<批量动作>Job` | 独立且值得单独阅读的 Job；小型私有 Job 可嵌套于所属系统 |
| `Aspects/` | `<业务能力>Aspect` | 组合访问，不另存权威状态 |
| `Definitions/` | `<概念>Definition`、`<概念>Blob` | 非托管只读定义；Blob 后缀用于实际 Blob 数据布局 |
| `Bridges/` | `<领域>SimulationBridge` | 实现应用端口并转换批量输入输出；不向 Burst 注入托管服务 |

普通组件不额外叠加 `Data`，避免 `HealthComponentData`；BufferElement 表示一个条目而非整个容器。已有项目使用 Health、LocalTransform 等已定名称时不强制重命名；Unity/第三方类型保留原名。

Domain 与 Simulation 各有 Definitions 时，Domain 表达业务定义，Simulation 只在布局确需变化时建立模拟表示；由明确映射生成，不手动维护两套配置真值。

## Unity、Infrastructure 与装配

- `Unity/Authoring/<对象>Authoring.cs` 提供创作输入；`Unity/Baking/<对象>Baker.cs` 转为 ECS。Unity 要求关联声明时可同文件放置紧密关联 Baker，其他独立类型按同名文件组织。
- `Unity/Views/<对象>View.cs` 只负责呈现；`Unity/Input/<用例>InputAdapter.cs` 将输入转为命令；`Unity/Adapters/Unity<能力>Adapter.cs` 实现引擎端口。
- `Unity/Assets/<概念>Asset.cs` 放 ScriptableObject 类型定义。实际 .asset、prefab、图片等内容放项目内容目录，不因存在 Unity/Assets 就把素材混入代码目录。
- `Infrastructure/Persistence/SaveData/` 放 `<对象>SaveData`，`Migrations/` 放有明确源/目标版本的迁移，如 `PlayerProfileV1ToV2Migration`；仓储实现不包含领域规则。
- Infrastructure 可按实际需要细分 Networking、Platform、Configuration；避免又建一个无语义的 Services 目录集中全部实现。
- `Bootstrap/` 放项目/会话装配入口，如 `GameBootstrap`、`BattleSessionFactory`；DI 安装器按所用框架命名。Bootstrap 可全局组织，不要求每个领域各建一份。
- 公共基础能力放项目 SharedKernel 或 Infrastructure；领域特有实现放本领域，不同时保留全局和领域内两套同义服务。

## 文件、成员与测试

- 一个主要类型一个同名 .cs 文件；私有嵌套实现或紧密相关小型类型可同文件，不把多个独立公开入口堆在一处。
- 类型、方法、属性、事件、常量用 PascalCase；参数和局部变量用 camelCase；私有实例字段用 `_camelCase`，私有静态字段用 `s_camelCase`，类型参数用 `T` 或 `TEntity`。Unity 要求的生命周期方法保留原名。
- 布尔成员用 `Is/Has/Can`，集合用复数，数量用 `Count`，持续时间标明单位如 `CooldownSeconds`；tick 与真实秒不混用。
- 领域方法用 `ApplyDamage`、`ReserveResources` 等意图名称。Try 前缀需有明确失败返回契约，Async 后缀用于实际异步契约，不把入队方法命名为已完成行为。
- 重要类型说明职责与状态所有者，重要方法说明调用者和提交时点；公开 API 按项目约定使用 XML 注释。避免逐行复述代码。
- 测试文件为 `<被测类型/用例>Tests.cs`，方法统一 `Behavior_Condition_ExpectedResult`，例如 `BuildTower_InsufficientResources_DoesNotSpendOrSpawn`。项目已有其他一致格式时沿用，不混搭。
- 测试分组按业务层/集成边界，EditMode 与 PlayMode 使用明确测试程序集；不为每个分类自动拆程序集。测试名表达行为，不断言私有布局以迎合目录结构。
