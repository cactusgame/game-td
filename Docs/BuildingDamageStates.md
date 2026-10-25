# 建筑生命与外观

在 `Assets/Scenes/Development/DefensePlayground.unity` 进入 Play，按 B 进入建设模式。点建筑或墙，在菜单里选择“试玩：造成 30% 伤害”或“试玩：修复 25% 生命”。墙菜单也提供其所含城门的独立操作。

- 生命比例低于 50%：残血外观；恰好 50% 仍为完整外观。
- 修复后达到 50%：恢复完整外观；修复上限为最大生命。
- 生命归零：损毁外观；已损毁对象不能修复或通过升级复活，重启还原。
- 城墙耐久当前按整条墙边计算，整边倒塌时其所含城门一起损毁，移除该墙边的运行阻挡；节点底座和破损门框作为遗迹保留。
- 普通建筑损毁沿用已有占地规则，本次没有引入重建、废墟清理或自动回血。

## 配置与素材

`Config/GameContent.xlsx` 的“建筑”仅保留 tower、hall、barracks.melee、barracks.ranged、wall、gate，升级表同步去掉未提供的建筑。其他工作表的业务数据保持原值。

`Config/GameContent.xlsx` 的“全局参数”表中，`building-appearance.damagedHealthRatio` 是残血阈值，范围严格在 0 与 1 之间。修改后运行现有导出入口，生成 `Assets/Content/Configuration/Resources/GameConfig/building-appearance.json`：

```powershell
python Tools/ContentConfiguration/export_config.py
python Tools/ContentConfiguration/export_config.py --check
```

运行只读取 `Assets/Content/Configuration/Resources/GameConfig/` 的导出 JSON。示例关卡及地图中的旧建筑已去除，试玩小地图放有上述所有功能类型。

素材：`Assets/Content/Maps/BuildingStates/building-states.png`、`wall-damage-atlas.png`；裁切后的 Sprite 保存在同目录的 `BuildingConditionArt.asset`，由两套 `MapVisualCatalog` 引用。建筑图集包含四种建筑的完整、残血、废墟共十二张 Sprite；墙面图集提供裂纹和碎石两种材质，墙和城门仍使用任意朝向的既有几何生成器。

素材通过内置 image_gen 生成，保留原始 PNG 和透明度，未使用 API/CLI 回退。最终采用的提示词如下。

建筑图集：

> Use case: stylized-concept. Transparent medieval pixel art GAME SPRITE SHEET, exact regular 4 columns and 3 rows. Columns: blue-roof beige-stone castle; round blue-roof arrow tower; blue-roof timber melee barracks with swords sign; blue-roof timber ranged barracks with bow sign. Row1 intact, row2 identical buildings severely cracked with missing roof tiles (low health), row3 matching collapsed rubble piles (destroyed). Same scale and x position per column, same ground baseline at bottom of every cell, transparent padding between every cell. Front three-quarter RPG game view, crisp pixel style like classic medieval city builder. No text, no people, no background, no effects.

墙面图集（参考现有 `ArtV3/wall-masonry.png`）：

> Use case: stylized-concept. Game wall damage texture atlas, 1024x1024 square. Match medieval beige gray stone pixel art masonry of reference. EXACT two equal vertical halves: left half moderately damaged masonry with large deep dark cracks and chipped stones, right half completely shattered masonry irregular broken stone pieces dark seams, still fills entire image. Seamless tiling independently within each half, flat orthographic material no perspective, no text no scenery. Crisp readable pixel art texture.

生成图集的实际单元边界不同于提示词中的等分网格，导入时已按实际画面裁切并去除透明边界，三态共用地面定位与显示宽度。

## 真实阅读路径与归属

1. `BattleConstructionView.AddHealthActions`：菜单只提交操作意图。
2. `MapPlaySession.StructureHealth.cs`：验证目标与对局阶段，选择对应状态所有者。
3. 普通建筑生命由 ECS `DefenseStructure` 保存；`EcsBattleSimulation.StructureHealth.cs` 在步间同步边界处理显式伤害和修复。战斗自然伤害仍由 `StructureCombatSystem` 结算。
4. 城门、城墙耐久由 `GatePassageSession` 独占，沿用 `GateDefense` 的按攻击者冷却规则。空间发布成功后才提交损毁；发布失败恢复原空间。门洞区域只结算城门伤害，不同时伤害墙体。
5. `MapPlayController.Update` 在模拟推进后读取带最大生命的快照，调用 `MapWorldView.SetCondition`。视图只缓存外观档位，不保存可写生命。阈值越界才重建表现，不在每次受伤时重建。
6. `MapSceneSprites`、`GatePresentation` 根据素材和档位绘制。动画和 Sprite 不裁决伤害、通行或胜败。

## 验证

验证结果：84 项试玩/建设测试、62 项地图编辑/渲染回归、15 项配置导出测试全部通过，Unity 编译无错误。新增 `StructureHealthTests` 覆盖生命上限、损毁不可复活、重启、墙门导航变更与敌人接触攻击冷却。

回归中修正了已有近战兵测试的半径断言：测试生成固定半径敌人，断言也应读取同一个固定敌人的半径，不能读取产品表中的另一种半径。

运行验证观察到帧数和模拟时间持续增长，随后冻结模拟时间以依次截图完整、残血、修复和损毁状态。截图位于 `Docs/Validation/BuildingDamage/`。已检查修复恢复完整外观；退出 Play 后测试改动不写入场景或初始血量。
