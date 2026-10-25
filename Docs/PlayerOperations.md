# 玩家操作（PC）

运行入口：`Assets/Scenes/Development/DefensePlayground.unity` 的地图试玩。
英雄使用 WASD / 方向键移动，已有普通攻击自动触发，不需要攻击键。

短按在松键时执行；持续按住0.45秒执行一次长按，不重复触发，也不再执行短按。

| 按键 | 短按 | 长按 |
| --- | --- | --- |
| 空格 | 技能槽1 | 技能槽2 |
| R，无选择 | 选中英雄配置半径内的活士兵并持续跟随 | 边走边选，白圈内新选中的活士兵立即跟随；松开只结束范围采样，保持选择和跟随 |
| R，已有选择 | 取消选择，在各自当前位置待命 | 将所选士兵当前位置设为驻守点并取消选择 |
| 1 | 随机呼叫一名未选中的活士兵，加入跟随队伍 | 呼叫所有活士兵并持续跟随 |
| 2 | 呼叫一名未选中的近战兵，加入跟随队伍 | 呼叫全部近战兵并持续跟随 |
| 3 | 呼叫一名未选中的远程兵，加入跟随队伍 | 呼叫全部远程兵并持续跟随 |

数字键1/2/3的单人呼叫先按兵种筛选，再排除已选中和阵亡士兵；优先在未驻守候选中随机选择，仅当该候选池为空时才从驻守士兵中随机选择。连续点击会先招完未驻守士兵，再逐个召回驻守士兵。长按的全体呼叫仍包含符合兵种的全部活士兵。

“驻守”指玩家已有选择时长按R明确驻扎，状态由 ECS `SoldierState.IsStationed` 保存；初始兵营待命与短按R取消跟随后原地待命不算明确驻守。被召回后解除驻守，保持选中并跟随；再次长按R驻扎可重新标记为驻守。

士兵待机分为死守和警戒。上述明确驻守即死守：原地攻击射程内的活敌人，不追敌、不返回兵营，也不会被单位拥挤分离推走；脚下持续显示白色虚线椭圆，与选择圈独立。兵营出兵和短按R取消跟随后采用警戒：范围内主动追击最近的活怪物，受到攻击后反击攻击者（即使伤害被防御抵消），没有目标时返回待机点。主动索敌采用当前士兵与怪物的中心距离；反击可超出警戒范围，直到攻击者死亡。选中跟随命令优先于警戒追击，召回清除死守和旧反击目标。

警戒范围配置在 `Config/GameContent.xlsx` 的“全局参数”表，键为 `battle.soldierDetectionRadius`，默认5世界单位，必须为有限正数。执行现有 `Tools/ContentConfiguration/export_config.py` 后投影为 `battle.json.soldierDetectionRadius`，重新开局加载；与R选择范围和攻击射程独立。此配置属于“单位与战斗”，不修改兵营编制或补兵状态。

白圈只在边走边选期间显示；选中士兵脚下另有小白圈。失焦取消手势与选择，不补发松键操作；重开清空命令、选择及冷却。终局停止模拟和操作。

R短按、长按和白圈共用 `Config/GameContent.xlsx` “全局参数”表中的 `battle.soldierSelectionRadius`（默认4，逻辑地面单位，必须大于0）。修改该行的数值，导出Excel并重新进入Play Mode后生效；该半径与技能攻击范围相互独立。

默认战斗呼叫使用现有寻路，选中的士兵到达后仍持续跟随英雄，交战结束后继续跟随。取消选择会同时清空旧路径，以各自当前位置待命，不再走向旧呼叫点或返回兵营。试玩 HUD 的“部署瞬移”开关提供编辑/部署时的瞬移呼叫，传送后仍保持选中和跟随。该开关只改变呼叫方式，不暂停对局；独立地图编辑器当前没有运行中士兵，不保存战斗选择或士兵位置。

呼叫时以英雄为中心寻找分散净空点，将相对偏移保存在士兵状态中；导航持续以英雄当前位置加偏移更新目标。贴墙时偏移点不可站立则接近英雄本身，遵守我方墙门通行。暂无净空点时仍选中并尝试寻路接近，不进行非法传送。数字键呼叫保留此前选择并加入新成员，不创建额外士兵。

## 示例技能配置

英雄头顶的血条上方显示技能冷却进度：短按技能为青色、长按技能为金色，分别从空填充到满。只有技能正在冷却且英雄存活时显示对应进度条，到期立即独立隐藏；幽灵不显示。`EcsBattleSimulation.CopyUnits` 从现有技能截止时间和 Excel 技能冷却配置生成 `SkillCooldownSnapshot`，`UnitView / HeroSkillCooldownView` 只读显示，不自行启动计时。重开清空冷却，暂停或终局不推进模拟时进度冻结。

血条和正在显示的冷却条按边缘紧贴向上堆叠；未冷却的技能不占空行。`UnitView.ShowHealthBar` 提供未来设置界面的表现控制入口：关闭血条时，最下方冷却条落到原血条位置；恢复血条时自动上移。本次仅预留显隐与布局能力，没有新增设置菜单或改动玩法配置。

唯一数据源为 `Config/GameContent.xlsx`：

- “单位”表英雄行的 `skillIds` 用分号分隔，顺序对应空格短按/长按，最多两个槽位。
- “技能”表设置 `enabled`、`cooldown`、`attackId`。沿用既有秒单位：3秒即3000毫秒。
- 对应“攻击”的 `range` 是以英雄为中心的半径，`damage` 为瞬时扣血量，`mode` 使用 `Direct`。
- 示例技能1：半径4，扣血30，冷却3秒；技能2：半径6，扣血60，冷却8秒。只影响范围内活敌人，不扣英雄/友军，不再减防御；空放也消耗冷却。
- 当前执行器支持瞬时范围扣血。英雄主动技能引用带附加效果的配置或非 Direct 攻击时，导出报错；通用 Slow/Heal/持续效果执行尚未实现。

修改后执行 `python Tools/ContentConfiguration/export_config.py` 并重新进入 Play Mode。`battle.json` 的英雄技能由原始引用生成，不是另一份手工编辑源。

## 状态归属和阅读路径

本功能归“单位与战斗”。建设领域继续拥有兵营编制与补兵计时。

## 英雄复活

英雄阵亡后按模拟时间等待 `Config/GameContent.xlsx` “全局参数”的 `battle.heroRespawnSeconds`（默认10秒，有限正数），在幽灵当前位置满血复活，无次数限制，保留同一单位ID。复活时间由 Excel 导出到 `GameConfig/battle.json`，重新开局读取。死亡期间幽灵可移动、带动镜头，但不能攻击、使用技能或召唤士兵；技能冷却继续按模拟时间推进，不因复活重置。幽灵沿用英雄的地形通行与地图边界限制，不能移动进墙内。

`HeroRespawnSystem` 在本步伤害系统结束后记录一次尸体位置、朝向、死亡序号和复活截止时间。`GroundTransform` 在死亡期间代表幽灵位置，`MovementSystem` 仍消费移动输入；生命保持0，因此寻敌、攻击和操作入口排除幽灵。到期直接在当前坐标恢复生命；下一固定步才恢复战斗。`HeroRespawnState` 是权威复活状态，`UnitSnapshot` 复制幽灵标志、尸体位置/序号、剩余复活时间。`BattleSession` 和 `MapPlaySession` 只转发快照，不自行计时。暂停或不推进模拟时倒计时冻结；地图对局结束后不再复活。

地图试玩 HUD 的复活提示独立于P键调试面板，显示向上取整的剩余秒数与幽灵可移动提示，复活或终局后隐藏。`UnitView` 显示半透明蓝色浮动幽灵，`HeroCorpseView` 固定在每次死亡位置，播放死亡动作并永久停留末帧；尸体不随幽灵移动，复活后仍保留，本局重开或视图清场时一起移除。复活时同一身体恢复不透明和血条，幽灵立即消失。

复活后默认2秒无敌，Excel“全局参数”的 `battle.heroRespawnInvulnerabilitySeconds` 可调整（有限正数，当前 B105）。所有人物出生时带 `UnitStatusState`，其中 `InvulnerableUntil` 使用模拟时钟；复活系统授予状态，接触、士兵攻击、塔直伤和技能直伤统一检查，表现不决定免伤。保护期结束且怪物仍贴身时会重新判定接触，不会因旧接触标记永久免伤。`UnitSnapshot.InvulnerabilityRemainingSeconds` 仅是只读显示数据，`InvulnerabilityShieldView` 据此显示金色球面保护罩；状态到期罩子隐藏。后续晕眩、中毒等状态可在同一人物状态边界中明确各自的持续时间、效果系统和写入职责，本次没有预先实现这些效果。

1. `Presentation/Input/KeyboardPlayerOperations.cs`：PC键盘通过 `Battle/Application/Sessions/PressGesture.cs` 判定短/长按，输出设备无关的 `PlayerOperation`。地图试玩与旧战斗原型共用输入适配。
2. `Battle/Application/Sessions/BattleSession.cs`：每画面帧收集操作，保留到下一固定步；在移动/攻击前消费一次，补步不重发。
3. `Battle/Simulation/Bridges/EcsBattleSimulation.PlayerControls.cs`：等待旧 Job 后提交选择、驻守、传送或伤害。选择、跟随偏移与驻守点归 ECS `SoldierState`，冷却与连续选择模式归本局模拟；每个固定步后采样移动后的范围。
4. `Battle/Simulation/Systems/SoldierNavigationSystem.cs`：先禁止死守移动，再裁决跟随、反击、警戒索敌或返回待机点；`SoldierCombatSystem` 在移动后结算攻击并记录反击来源，下一固定步导航消费。`MonsterSeparationSystem` 保留死守单位占位，不写其拥挤位移。
5. `Tools/MapPlaytest/Unity/Views/SoldierSelectionView.cs`：只读 `UnitSnapshot` 的选择/死守状态和 `PlayerControlSnapshot`，画选择圈与死守虚线椭圆；`MapPlayController` 组装 HUD 与模式开关。

技能定义通过 `JsonBattleDefinitionSource → UnitDefinition.Skills` 加载。后续触控适配可提交相同的操作契约。
