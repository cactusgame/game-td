# 单位 AI：命令、姿态、当前行为与人物状态

四类概念均属于“单位与战斗”领域。选择是输入操作状态，不是 AI 命令。

| 概念 | 回答的问题 | 当前实现与权威存储 |
| --- | --- | --- |
| 玩家命令 | 玩家希望士兵持续做什么？ | `SoldierState.Command`：None、FollowHero、MoveToPosition |
| 作战姿态 | 自主交战允许怎样行动？ | `SoldierState.Stance`：Alert、HoldGround |
| 当前行为 | 本步正在执行什么？ | 士兵的 `SoldierState.Behavior`；怪物的 `MonsterNavigationState.Behavior` |
| 人物状态 | 什么效果正在影响单位？ | `UnitStatusState`：当前只有带模拟时间期限的无敌 |
| 选择 | 哪些士兵接收本次批量操作？ | `SoldierState.IsSelected`，导航和战斗不读取它 |

命令与姿态使用互斥枚举，当前行为也只有一个值。人物状态未来可以叠加，例如中毒与减速同时存在，不能合并为互斥 AI 状态。指定目标攻击、其他新增姿态、中毒、减速、亢奋均未在本轮实现，也没有添加可选择却不起作用的枚举项。

## 士兵规则与写入顺序

1. `EcsBattleSimulation.TrySpawnSoldier` 初始化出生集结点、警戒姿态与命令。会话的默认跟随选项在此转成 FollowHero；默认跟随不代表被选中。
2. `EcsBattleSimulation.Execute` 在旧 Job 完成后提交输入。呼叫同时选择士兵、提交 FollowHero、恢复 Alert 并进入 Regrouping；短按 R 放下队伍提交 MoveToPosition，以本次分配的位置为固定落点。取消在当前位置警戒；长按 R 在当前位置切为 HoldGround。既有按键仍可一次改变多个概念，这是操作语义，不是状态互相推导。
3. `SoldierNavigationSystem` 读取命令决定英雄编队点或 Rally，读取姿态约束移动，再裁决当前行为。死守禁止主动移动但允许射程内攻击；英雄死亡时暂停跟随移动，仍可原地防御。归队或前往刚分配的固定落点期间使用 Regrouping，到位后解除，途中不会追敌或主动攻击。离英雄过远也重新进入 Regrouping。
4. 到位后 Alert 可追敌（Chasing）、射程内攻击或等待冷却（Attacking），无敌人时移动跟随（Moving）或返回固定点（Returning），到位待机（Idle）。MoveToPosition 是持续意图，到位后保留固定警戒点，追敌结束仍返回该点，不重置为出生集结点。
5. 移动与接触之后，`SoldierCombatSystem` 按移动后的距离结算攻击，可以将当前行为提升为 Attacking；归队暂停主动攻击但不免疫伤害。怪物攻击士兵时只登记反击来源，不改变其命令或姿态。
6. `CopyUnits` 复制 Command、Stance、Behavior 和纯选择状态。`IsStationed` 是快照上由 HoldGround 派生的显示属性；死亡单位快照统一显示 Dead。动画仍使用既有攻击时点、位置等表现数据。

`Rally` 与 `FollowOffset` 是命令所需的空间参数；`FollowCombatRadius` 是每步导航产生、后续战斗阶段读取的活动范围；`RetaliationTarget` 是已受攻击的来源。它们不再与布尔字段共同编码命令。IsRegrouping、IsStationed、HasPlayerOrder 已从士兵权威组件删除。

## 敌人的边界

敌人没有玩家命令，不人为赋予 FollowHero 或士兵姿态。配置中的主堡偏好属于目标选择策略，`MonsterObjectiveSystem` 仍负责目标和可达攻击位置。

`MonsterNavigationSystem` 每步写入基础行为：无感知目标时游走为 Moving，追英雄或主堡为 Chasing，主堡没有可达攻击目标时为 Idle，死亡为 Dead。随后士兵近身协调、英雄接触和攻城阶段可标记 Attacking；墙门攻击从 `RecordExternalAttack` 登记。导航组件中的 IsChasing 仍是移动实现的“消费路线/随机游走”开关，不能把它当成业务当前行为：等待、攻城和追击可能都使用路线模式。士兵虽然复用该导航组件的路径进度字段，但其 Behavior 只以 SoldierState 为准。

英雄由玩家直接移动，本轮没有引入英雄 AI，其存活时 Behavior 为 None。当前行为表达本步模拟决策，包含尝试寻路和等待攻击冷却，不等同于动画片段或保证单位本步发生位移。

## 代码阅读入口

- 术语类型：`Assets/Game/Gameplay/Battle/Domain/ValueObjects/SoldierCommand.cs`、`CombatStance.cs`、`UnitBehavior.cs`。
- 士兵权威状态：`Assets/Game/Gameplay/Battle/Simulation/Components/SoldierState.cs`。
- 输入提交：`Assets/Game/Gameplay/Battle/Simulation/Bridges/EcsBattleSimulation.PlayerControls.cs`。
- 导航与战斗：`Assets/Game/Gameplay/Battle/Simulation/Systems/SoldierNavigationSystem.cs` → `SoldierCombatSystem.cs`。
- 敌人决策：同目录的 `MonsterObjectiveSystem.cs` → `MonsterNavigationSystem.cs`，后续接触及攻城阶段写攻击结果。
- 回归：`Assets/Game/Gameplay/Battle/Tests/Editor/SoldierIdleTests.cs`、`PlayerOperationTests.cs`、`MonsterNavigationTests.cs`。
