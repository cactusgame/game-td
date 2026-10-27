# 单位 AI：命令、姿态、当前行为与人物状态

四类概念均属于“单位与战斗”领域。选择是输入操作状态，不是 AI 命令。

| 概念 | 回答的问题 | 当前实现与权威存储 |
| --- | --- | --- |
| 玩家命令 | 玩家希望士兵持续做什么？ | `SoldierState.Command`：None、FollowHero、MoveToPosition |
| 作战姿态 | 自主交战允许怎样行动？ | `SoldierState.Stance`：Alert、HoldGround |
| 当前行为 | 本步正在执行什么？ | 士兵的 `SoldierState.Behavior`；怪物的 `MonsterNavigationState.Behavior` |
| 人物状态 | 什么效果正在影响单位？ | `UnitStatusState`：独立计时的无敌、晕眩、定身、沉默与中毒，所有阵营共用 |
| 选择 | 哪些士兵接收本次批量操作？ | `SoldierState.IsSelected`，导航和战斗不读取它 |

命令与姿态使用互斥枚举，当前行为也只有一个值。人物状态可叠加，例如晕眩与中毒同时存在，不能合并为互斥 AI 状态。指定目标攻击、其他新增姿态、减速、亢奋仍未实现。

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

## 共享人物状态

英雄、近战兵、弓兵及敌人从同一个出生入口获得 `UnitStatusState`，不按阵营复制效果实现。四种效果与无敌可以并存，期限使用 `ElapsedTime`；暂停或仅查询快照不消耗期限，到达截止时刻控制解除。

| 效果 | 移动 | 普通攻击 | 施法 |
| --- | --- | --- | --- |
| 晕眩 Stun | 禁止 | 禁止 | 禁止 |
| 定身 Root | 禁止 | 允许 | 允许 |
| 沉默 Silence | 允许 | 允许 | 禁止 |
| 中毒 Poison | 不影响 | 不影响 | 不影响 |

多个效果同时存在时取全部限制的交集。限制发生在实际行动入口，而不是把命令改为停止：`MovementSystem` 覆盖英雄输入、跟随路径和游走，`MonsterSeparationSystem` 不挤动受控单位；`SoldierCombatSystem`、`CircleContactSystem`、`StructureCombatSystem` 与墙门协调器分别检查攻击权限，英雄技能检查施法权限。受控期间调用瞬移部署也不能绕过位移限制。英雄仍能发出士兵指令；这不是施法。未来士兵/敌人的技能实现也应调用 `CanCastAt`，目前没有虚构这些技能。

桥接生成快照时投影剩余时长和权限，墙门协调器消费 `UnitSnapshot.CanAttack`，不会在结算后才用动画记录接口拒绝攻击。士兵归队阶段及命令保持原值；快照将被控制暂停的行为显示为 Idle，解除后继续原命令。禁止施法时不扣技能冷却、不记录施法动作。接触战斗双方分别检查晕眩，解晕后只补受控一方的持续接触攻击，不让另一方重复扣血。

### 施加入口

调用公开端口 `IUnitStatusEffects.TryApplyStatus(ApplyUnitStatusCommand)`，使用当前会话内稳定的单位 ID，不向调用者暴露 ECS 句柄。当前没有自动触发这些效果的武器或 UI；此入口供后续技能、攻击效果及测试调用。

```csharp
effects.TryApplyStatus(new ApplyUnitStatusCommand(unitId, UnitStatusKind.Stun, 2));
effects.TryApplyStatus(new ApplyUnitStatusCommand(unitId, UnitStatusKind.Poison, 5,
    poisonDamagePerTick: 3, poisonIntervalSeconds: 1));
```

不存在或已死亡的目标返回 false，`default` 命令也被拒绝；构造命令时拒绝非法种类、非正数/非有限持续时间和非法毒伤参数。晕眩、定身、沉默重复施加取较晚截止时刻，不叠加数值、不缩短已有期限。

### 中毒与生命周期顺序

`UnitStatusSystem` 在目标选择、导航和移动之前结算中毒，毒死的单位本步不能攻击。毒伤绕过防御，受无敌保护；首次伤害在施加后一个周期，到期时刻上的最后一跳仍结算。例如持续 3 秒、周期 1 秒，在第 1、2、3 秒各扣一次。持续时间短于周期则没有伤害跳。

大步长或补步跨越多个周期时按数量批量结算，不逐跳循环；无敌按每跳发生的模拟时刻判断，不能用当前时刻漏掉无敌期间的保护。已结算周期推进计时，不会因重复查询或同一时刻重复更新而再次扣血。重复中毒先结算已有到期周期，再替换伤害、持续时间和周期，以重新施加时刻开始计时；目前同种毒不叠层，也没有来源分别叠加的规则。

死亡清除效果；英雄复活重新初始化状态，只保留复活授予的无敌，毒伤不会穿越死亡继续影响新生命。重启通过销毁旧单位清理全部状态。效果参数通过命令传入，目前不需要新增配置文件或修改 Excel。

新增阅读路径：`Contracts/Commands/ApplyUnitStatusCommand.cs` → `Simulation/Bridges/EcsBattleSimulation.StatusEffects.cs` → `Simulation/Components/UnitStatusState.cs` → `Simulation/Systems/UnitStatusSystem.cs` → 各行动系统 → `Contracts/ReadModels/UnitStatusSnapshot.cs`。测试见 `UnitStatusEffectsTests.cs`、`UnitStatusTests.cs` 及墙门回归测试。

## 代码阅读入口

- 术语类型：`Assets/Game/Gameplay/Battle/Domain/ValueObjects/SoldierCommand.cs`、`CombatStance.cs`、`UnitBehavior.cs`。
- 士兵权威状态：`Assets/Game/Gameplay/Battle/Simulation/Components/SoldierState.cs`。
- 输入提交：`Assets/Game/Gameplay/Battle/Simulation/Bridges/EcsBattleSimulation.PlayerControls.cs`。
- 导航与战斗：`Assets/Game/Gameplay/Battle/Simulation/Systems/SoldierNavigationSystem.cs` → `SoldierCombatSystem.cs`。
- 敌人决策：同目录的 `MonsterObjectiveSystem.cs` → `MonsterNavigationSystem.cs`，后续接触及攻城阶段写攻击结果。
- 回归：`Assets/Game/Gameplay/Battle/Tests/Editor/SoldierIdleTests.cs`、`PlayerOperationTests.cs`、`MonsterNavigationTests.cs`。
