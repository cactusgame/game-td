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

`MonsterNavigationSystem` 每步读取共享目标并写入基础行为：无感知目标时游走为 Moving，追单位或主堡为 Chasing，主堡没有可达攻击目标时为 Idle，死亡为 Dead。射程内或攻城结算阶段可标记 Attacking；墙门攻击从 `TryAttackExternalTarget` 受控提交。导航组件中的 IsChasing 仍是移动实现的“消费路线/随机游走”开关，不能把它当成业务当前行为。士兵虽然复用该导航组件的路径进度字段，但其 Behavior 只以 SoldierState 为准。

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

桥接生成快照时投影剩余时长和权限，墙门协调器消费 `UnitSnapshot.CanAttack`，实际提交时模拟再校验权限与普通攻击冷却。士兵归队阶段及命令保持原值；快照将被控制暂停的行为显示为 Idle，解除后继续原命令。禁止施法时不扣技能冷却、不记录施法动作。接触战斗双方分别检查晕眩，解除后仍遵循普通攻击间隔与敌人首次对英雄接战的延迟规则，不补发历史攻击。

## 战斗目标与普通攻击冷却

`CombatTargetState.Target` 是单位追击与实际攻击共享的当前目标。阶段顺序为：状态伤害 → 主堡长期目标 → 单位目标选择 → 导航 → 移动/碰撞 → 英雄优先接战 → 单位攻击复核 → 攻城 → 复活 → 一致快照。移动后允许对死亡、出圈等失效目标重选；仍合法的目标不因另一个目标略近而切换。

- 士兵保持仍存活、符合姿态/跟随范围/警戒规则的目标；最近受击来源只在需要重选时参与优先级，不持续覆盖合法目标。新玩家命令清除旧目标，归队仍优先于追击。
- 敌人保持有效交战目标并使用导航配置的感知/脱离半径；攻城时只有已进入攻击范围的单位触发临时交战。当前主堡仍存活且可达时继续以它为长期目标；失效后重选。
- 英雄由玩家移动，普通攻击选择并保持射程内的一个目标，每次冷却只攻击一次。持续贴身、换目标、离开再进入、无敌到期都不能刷新攻击冷却。
- 墙门由 `GatePassageSession` 保持当前接触的可攻击墙门，失去接触或破坏后重选；可在前往长期目标途中攻击阻挡物。诊断区分单位/主堡目标与当前墙门目标。

`Vitals.NextNormalAttackAt` 是每个单位普通攻击的唯一冷却时刻。攻击英雄、士兵、敌人、主堡、墙或门都消费它，间隔统一取该单位 `CombatProfile.AttackInterval`。`LastAttackTime` 只记录实际动作，技能使用独立技能冷却。旧的墙门 `GateAttackInterval` 配置暂留兼容内容格式，普通攻击不再读取；`GateDefense` 只拥有耐久，不再按每个墙门维护攻击者冷却。

英雄与敌人均满足攻击条件时先提交英雄攻击；被英雄击杀的怪物不会同一步反击。怪物首次进入对英雄的有效攻击条件时，只把共享冷却至少推迟一个自己的攻击间隔，不造成伤害、不记录攻击动作；此后持续贴身按间隔攻击。离开攻击范围或换为其他目标结束这次接战，再次对英雄接战仍等待一个间隔，已有冷却不会缩短。士兵与敌人交战没有该首次延迟，双方按各自共享冷却正常攻击。

墙门通过 `IDefenseBattleSimulation.TryAttackExternalTarget` 提交：验证模拟时刻、单位存活/状态/冷却 → 同步执行墙门耐久及必要空间提交 → 成功后消费冷却并记录动作。回调返回 false 或抛出异常不消费冷却；回调不得推进模拟或发起其他攻击。这样破坏空间提交失败不会丢掉本次攻击资格。

## P 键 AI 调试显示

地图对局内按 **P** 同时切换原调试工具栏和 AI 图层，默认关闭；输入框编辑文字时不触发。单位旁显示紧凑 ID/行为标签，鼠标悬停单位切换详情，初始聚焦英雄。详情显示命令、姿态、行为、当前目标、停止原因、普通攻击剩余冷却与路径点数。青线为剩余路径，橙线及叉号为目标，绿圈为警戒范围；悬停单位高亮，其他单位淡化，跟随士兵的圈以英雄为中心。英雄显示“玩家输入”，敌人显示“自主战斗”，不虚构玩家命令。

`MonsterNavigationState.StopReason` 由导航、移动和地形阶段记录，区分死亡、晕眩、定身、死守、英雄死亡、进入攻击范围、到达目标、缺少目标、无路可走、等待寻路、地形阻挡和无移动输入。建设或结束阶段额外显示应用会话拥有的模拟暂停原因。

UI 只在开启时按 10Hz 调用 `IAiDebugSimulation.CopyAiDebug`。诊断完成 Job 后复制本步位置、共享目标、剩余路径和原因，不写命令、不推进时钟；路径集合是独立只读副本。图层与文字均忽略鼠标拾取，重启后按会话代次重建显示，关闭后停止采样。入口为 `MapPlayController.UpdateDebugHud` → `AiDebugView` → `MapPlaySession.CopyAiDebug` → `EcsBattleSimulation.AiDebug.cs`。

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
# 人物单位生命恢复

配置入口为 `Config/GameContent.xlsx` →「单位」→ `healthRegenerationPerSecond`（生命恢复/点每秒），按绝对生命值恢复，0 表示关闭。当前初值：英雄 5 点/秒；近战、远程士兵 0.2 点/秒；两类敌人 0.1 点/秒。使用 `Tools/ContentConfiguration/export_config.py` 导出后，由 `JsonBattleDefinitionSource` 加载到 `UnitDefinition`。

阅读路径：`EcsBattleSimulation.Spawn` 为所有阵营创建 `HealthRegenerationState` → `HealthRegenerationSystem` 按模拟步长累计小数恢复量 → 写入 `Vitals.Health`。生命仍用整数显示，例如 0.1 点/秒累计 10 秒恢复 1 点。不超过生命上限，满血不储存恢复量，死亡不恢复，也不触发受击或攻击反馈。

恢复必须脱战：追击存活单位、被存活单位追击，或进入建筑攻击范围都视为交战；停止交战并连续 5 秒没有攻击、施放攻击技能或受击后才可恢复。中毒、晕眩、定身、沉默均禁止恢复，无敌本身不禁止。进入战斗或异常状态时丢弃不足一点的恢复余量；跨越阻断截止时刻只累计本步真正允许恢复的时间，暂停模拟时不推进。

系统顺序为中毒结算 → 导航、移动与战斗 → 生命恢复 → 英雄复活。`UnitStatusSystem` 在清除到期中毒前记录状态截止时间；生命恢复阶段读取本步最终攻击、受击和共享目标。墙门攻击及攻击技能可在 ECS 固定步之外提交，通过 `InterruptHealthRegeneration` 撤销同一模拟时刻刚恢复的生命并进入脱战等待，防止攻击与恢复同时生效。中毒致死后不能靠被动恢复复活；英雄复活仍由 `HeroRespawnSystem` 裁决并清空上一条生命的恢复余量与阻断记录。
