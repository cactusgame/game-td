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

白圈只在边走边选期间显示；选中士兵脚下另有小白圈。失焦取消手势与选择，不补发松键操作；重开清空命令、选择及冷却。终局停止模拟和操作。

R短按、长按和白圈共用 `Config/GameContent.xlsx` “全局参数”表中的 `battle.soldierSelectionRadius`（默认4，逻辑地面单位，必须大于0）。修改该行的数值，导出Excel并重新进入Play Mode后生效；该半径与技能攻击范围相互独立。

默认战斗呼叫使用现有寻路，选中的士兵到达后仍持续跟随英雄，交战结束后继续跟随。取消选择会同时清空旧路径，以各自当前位置待命，不再走向旧呼叫点或返回兵营。试玩 HUD 的“部署瞬移”开关提供编辑/部署时的瞬移呼叫，传送后仍保持选中和跟随。该开关只改变呼叫方式，不暂停对局；独立地图编辑器当前没有运行中士兵，不保存战斗选择或士兵位置。

呼叫时以英雄为中心寻找分散净空点，将相对偏移保存在士兵状态中；导航持续以英雄当前位置加偏移更新目标。贴墙时偏移点不可站立则接近英雄本身，遵守我方墙门通行。暂无净空点时仍选中并尝试寻路接近，不进行非法传送。数字键呼叫保留此前选择并加入新成员，不创建额外士兵。

## 示例技能配置

唯一数据源为 `Config/GameContent.xlsx`：

- “单位”表英雄行的 `skillIds` 用分号分隔，顺序对应空格短按/长按，最多两个槽位。
- “技能”表设置 `enabled`、`cooldown`、`attackId`。沿用既有秒单位：3秒即3000毫秒。
- 对应“攻击”的 `range` 是以英雄为中心的半径，`damage` 为瞬时扣血量，`mode` 使用 `Direct`。
- 示例技能1：半径4，扣血30，冷却3秒；技能2：半径6，扣血60，冷却8秒。只影响范围内活敌人，不扣英雄/友军，不再减防御；空放也消耗冷却。
- 当前执行器支持瞬时范围扣血。英雄主动技能引用带附加效果的配置或非 Direct 攻击时，导出报错；通用 Slow/Heal/持续效果执行尚未实现。

修改后执行 `python Tools/ContentConfiguration/export_config.py` 并重新进入 Play Mode。`battle.json` 的英雄技能由原始引用生成，不是另一份手工编辑源。

## 状态归属和阅读路径

本功能归“单位与战斗”。建设领域继续拥有兵营编制与补兵计时。

1. `Presentation/Input/KeyboardPlayerOperations.cs`：PC键盘通过 `Battle/Application/Sessions/PressGesture.cs` 判定短/长按，输出设备无关的 `PlayerOperation`。地图试玩与旧战斗原型共用输入适配。
2. `Battle/Application/Sessions/BattleSession.cs`：每画面帧收集操作，保留到下一固定步；在移动/攻击前消费一次，补步不重发。
3. `Battle/Simulation/Bridges/EcsBattleSimulation.PlayerControls.cs`：等待旧 Job 后提交选择、驻守、传送或伤害。选择、跟随偏移与驻守点归 ECS `SoldierState`，冷却与连续选择模式归本局模拟；每个固定步后采样移动后的范围。
4. `Battle/Simulation/Systems/SoldierNavigationSystem.cs`：选中时持续读取英雄位置及跟随偏移，取消选择后读取驻守点；玩家命令覆盖可选的旧英雄跟随行为。
5. `Tools/MapPlaytest/Unity/Views/SoldierSelectionView.cs`：只读 `UnitSnapshot` 和 `PlayerControlSnapshot` 画白圈；`MapPlayController` 组装 HUD 与模式开关。

技能定义通过 `JsonBattleDefinitionSource → UnitDefinition.Skills` 加载。后续触控适配可提交相同的操作契约。
