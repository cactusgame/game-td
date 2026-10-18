# 领域边界、目录与模块调用

## 模块地图

这些是按需演进的业务模块，不是必须立即创建的空目录或独立限界上下文。

| 模块 | 权威职责 |
| --- | --- |
| Session | 对局身份、天数、阶段、许可、胜负、本局规则快照 |
| Facilities | 建筑等级、投资、升级分支、能力与搬迁规则 |
| Fortifications | 墙网拓扑、稳定墙段、门机制、连接、维修和破坏规则 |
| Economy | 资源账户、消费、收入声明、退款和去重 |
| Units | 单位定义、出生、移动意图、控制权；高频实例状态可在 ECS |
| Command | 编组成员归属、长期命令、部署与返回策略 |
| Combat / Battle | 目标、攻击、伤害、投射物、效果、战斗模拟 |
| Encounter | 波次、关卡目标、事件与 Boss 调度；Boss 生命仍归对应战斗状态 |
| Loadouts | 玩家选角色与特技、资格、确认方案 |
| Rules | 基础内容、特技、开发者调校的组合与来源 |
| Recovery | 规划撤销、稳定边界快照、整体恢复 |
| Progression | 永久解锁、玩家档案、永久奖励去重 |
| MapEngine | 地形/空间/几何/导航共享能力，不拥有金币或关卡阶段 |

现有战斗模块名为 `Gameplay/Battle`。不要为了本表把它批量重命名为 Combat，也不要机械拆成 Heroes/Enemies 上下文。

## 项目目录

```text
Assets/
  Game/
    SharedKernel/             最小共享 ID、时间、地面语义；不做杂物箱
    MapEngine/                共享地图能力
    Gameplay/<Module>/        业务模块
    Presentation/             跨模块输入、镜头、HUD、视图
    Infrastructure/           存储、JSON、加载等技术实现
    Tools/                    地图/内容/调试工具
    Bootstrap/                DI 与会话组装
  Content/                    配置和素材，按内容归属组织
  Scenes/                     Bootstrap、Frontend、Gameplay、Development
  Settings/                   输入、渲染、音频项目配置
  ThirdParty/                 只能放 Assets 的第三方资产
  Generated/                  可重新生成的资产
Packages/                     UPM 与有来源/许可记录的内置包
SourceArt/                    不需要 Unity 导入的制作源文件
Docs/                         规范、模型、决策与验证记录
```

模块内部按需使用 `Contracts/Domain/Application/Simulation/Unity/Editor/Tests`；不要为简单模块建立无用层次。
Contracts 包含 Commands、Results、ReadModels、Ports；Domain 包含 Models、ValueObjects、Rules、Events。
Unity 包含 Authoring、Views、Adapters；Simulation 包含 Components、Systems、Bridges。
现有 Presentation 集中组织可继续使用；Editor-only 代码用 Editor 程序集约束，不能由 Runtime 程序集引用。

## 依赖方向

```text
Contracts -> 最小共享模型（必要时依赖稳定的领域定义）
Domain -> SharedKernel / 必要的地图纯规则
Application -> 本模块 Domain、Contracts、所需模块公开契约
Simulation -> Burst 兼容规则、Contracts、Entities/Burst/Collections
Unity / Presentation -> Application、Contracts、必要的表现适配
Infrastructure -> 内层定义的端口
Integration -> 两侧公开契约和适配实现
Bootstrap -> 具体实现的组装入口
```

用 asmdef 固定已稳定的边界；当前纯 C# 程序集使用 `noEngineReferences`。不要引入循环引用、反向 UI 依赖或全局 Service Locator。

## Facilities 与 Battle 的协作示例

1. 设施拥有等级和投资，ECS 若拥有当前生命，设施对象不另存一份可变生命。
2. 建塔用例协调空间、资源、设施创建与战斗注册。全部成功后才发布 TowerBuilt。
3. 普通命中只查询公共战斗组件，不调用 `GetBuilding().TakeDamage()`，也不从 Burst 系统查托管服务。
4. 伤害步骤确认死亡后，设施战斗协调器通过稳定 FacilityId 调用设施公开入口，例如 `IFacilityLifecycle.CommitDestruction`。
5. 该入口提交设施后果，不再次扣血；停止运作和必要通行变更在稳定边界完成，随后通知表现。

协调器可放 `Gameplay/Integrations/FacilityBattle`。由设施主导且无循环的用例，也可在 Facilities.Application 定义消费者端口 `IFacilityCombatUpdater`，由集成适配实现。不要把每一对模块都机械建立集成目录。

## 通信与一致性

| 类型 | 示例 | 约定 |
| --- | --- | --- |
| 命令 | 建造、升级、调兵 | 返回完成结果，或 CommandId 对应的异步执行结果 |
| 查询 | 金币、预览、单位快照 | 只读，不泄露内部可变状态 |
| 事件 | 建筑已建成、单位已死亡 | 提交后事实，不承包核心事务 |

预览与提交复用规则，提交检查世界版本并重新验证；预览不扣费、不占用正式身份。
使用预校验加变更集、完整提交或受控回滚实现单机一致性，无需引入分布式事务。
拆除返还依据真实投资与策略，不能用现价冒充历史支付；日结与奖励有去重依据。
只读模型、空间索引和反向索引可以重建；不能从这些缓存反向任意改写权威状态。
