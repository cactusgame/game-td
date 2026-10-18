# 角色动作序列

三种角色各 16 帧：4 行 × 4 列，从上到下为 Idle、Move、Attack、Hurt，行内从左到右播放。

资源（相对项目根目录）：

- `Assets/Content/Units/Heroes/Knight/Sprites/knight-actions.png`
- `Assets/Content/Units/Enemies/Goblin/Sprites/goblin-actions.png`
- `Assets/Content/Units/Enemies/Runner/Sprites/runner-actions.png`

同目录的 `.asset` 是场景引用的 `UnitAnimationSet`，保存 Sprite 帧和播放速率：待机 5 FPS、移动 9 FPS、攻击 10 FPS、受伤 14 FPS。角色左右转向复用水平翻转；当前没有单独的上下方向图。

## 架构与行为

Domain 的接触伤害规则保持原有语义。ECS `CircleContactSystem` 在新接触时写入双方 `LastAttackTime`，实际受到伤害才写入 `LastHitTime`。Contracts 快照携带时间戳，Application 仍负责固定步长推进。Presentation 的 `UnitView` 根据快照选择序列帧，不通过动画事件改变伤害、碰撞、速度或生命。

待机/移动循环播放。攻击/受伤单次播放后回到待机或移动。同次交换既攻击又受伤时，先完成攻击再播放受伤；伤害仍在接触发生的模拟步立即结算。死亡怪物优先播放独立死亡序列，再保留最后一帧，最后销毁场景视图；英雄仍保持死亡后隐藏。动画采用模拟时间，所以暂停会冻结动作，重启会清除旧视图和事件时间。

`BattleAnimationSetup.Apply()` 在当前战斗场景安全切帧并保存动画引用；不会重建场景。`BattlePrototypeSetup` 重建原型时也会引用这三套资源。切帧通过 Unity Sprite DataProvider 执行，重复执行保留已有 Sprite ID。源图不覆盖原有静态头像。动画帧使用统一高度与按 alpha 计算的脚底 pivot，避免按各帧可见轮廓缩放导致角色忽大忽小。疾跑怪伸爪与哥布林挥击弧的两处横向边界经过调整，把相邻角色之间的透明留白划给攻击帧；pivot 补偿保证地面位置不变。

## 图像生成

使用内置 image_gen，透明背景，以原有三张角色图片为参考，保留手绘卡通身份与装备。

初稿提示规格：每个角色生成 4×4 共 16 帧，朝右三分之二侧面，全身、统一比例、透明背景、无文字和格线；第 1 行呼吸待机，第 2 行交替迈步，第 3 行蓄力/挥击/命中/恢复，第 4 行受击/后仰/恢复平衡/准备。骑士挥剑、哥布林挥木棒、疾跑怪用爪攻击。

最终留白修订提示（每张初稿分别作为编辑输入）：

> Fix this animation sprite sheet for mechanical equal-grid slicing. Keep SAME character, SAME 16 distinct poses, SAME 4-column 4-row layout, SAME hand painted style, and TRUE TRANSPARENCY. Critical edit: make EVERY figure including weapons, scarves, ears and swing arcs SIGNIFICANTLY SMALLER, 65% of original size, centered in its own square cell, with feet at 85% down the cell. At least 12% empty transparent padding on all four sides of EACH cell. Each figure must be completely isolated, weapons and effects must never cross any cell boundary or touch neighbor. Identical body scale across all sixteen cells, identical foot baseline. Preserve four rows idle, walk/run, attack, hurt, four frames each. No grid lines, text or background. This is a sprite sheet for a real game, preserving clean isolated padded cells is more important than filling space.

哥布林最终采用以下修订提示：

> Fix sprite sheet for equal grid slicing. Keep the SAME goblin, SAME 16 animation poses in 4 columns and 4 rows, SAME transparent background. Shrink all 16 figures including clubs and swing effects to 60% current size, individually centered in each of sixteen cells with ample blank transparent gutters. Every figure including every weapon and effect MUST stay fully inside its own cell, never spill into adjacent cells. Preserve green skin and brown tunic, idle row1, walk row2, attack row3, hurt row4. Keep a consistent character scale throughout all cells. No text or grid or shadows.

疾跑怪最终采用以下修订提示：

> Fix sprite sheet for equal grid slicing. Keep the SAME orange imp runner, SAME 16 animation poses in 4 columns and 4 rows, SAME transparent background. Shrink all 16 figures including ears, scarves, extended claws to 60% current size, individually centered in each of sixteen cells with ample blank transparent gutters. Every figure including every hand and scarf MUST stay fully inside its own cell, never spill into adjacent cells. Preserve orange red skin, huge ears, beige scarf, idle row1, run row2, attack row3, hurt row4. Keep a consistent character scale throughout all cells. No text or grid or shadows.

## 验证

`ContactSystemTests` 覆盖接触只结算一次、分离后可再触发、同时死亡、防御抵消后攻击事件仍存在、死亡英雄不攻击。

`UnitAnimationTests` 对三套真实导入资源分别验证 16 帧完整、移动切换与转向、同次攻击后受伤、回到循环动作和英雄死亡隐藏；另验证两种怪物死亡序列、停留计时与统一配置。

最终验证：Unity 脚本编译通过；`Game.Battle.Tests` 编辑器测试共 20 项全部通过。已进入当前 `BattlePrototype` 场景验证角色生成、待机、移动、转向及实际接触结算；运行截图保存为 `Docs/Validation/battle-animations.png`。验证后退出 Play Mode，场景保持可编辑状态。
