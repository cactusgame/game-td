# 任意方向、多城门与整数 Tile

打开 `Game → 关卡编辑器`，点击工具栏“城门示例地图”。示例 `Assets/Content/Maps/Example/gates.map.json` 包含同墙两门、缓斜、陡斜和纵墙城门，既有关闭状态，也有开启状态。

## 编辑方式

1. 绘制任意方向、足够长的墙段。
2. 选择“城门”，在右侧填写门框宽度和门洞净宽（整数 Tile），点击墙段空位新增一座门。
3. 点击已有门只选择它；切换“选择移动”可沿墙拖动。门框起点按沿墙整 Tile 步长吸附。
4. 修改右侧尺寸或“初始开门”后，点击“应用到所选城门”。“移除城门”仅删除所选门，恢复对应墙身；“删除选择”也支持单门删除。
5. 放置重叠、越过端部连接底座、缩短墙段导致门放不下，以及非法尺寸都会被拒绝。拖动失败整笔不提交。支持撤销、重做、保存和重新打开，校验问题定位到具体门 ID。
6. 独立试玩使用 `Open / close gate` 点击门框或门洞切换该座门。关门覆盖单位时拒绝；空间同步失败保留原墙网与版本。

## 整 Tile 的含义

门框沿墙宽度、门洞净宽和门框起点均使用整数计数。默认门框 4 Tile、门洞 2 Tile，两边门柱各 1 Tile。最小门框 3 Tile、门洞 1 Tile；两侧门柱对称，要求 `(门框宽度 - 门洞宽度)` 为正偶数，不产生半格宽门柱。

任意角度的门使用随墙旋转的局部 Tile 度量，而不是把斜门的包围盒强行铺成台阶。正方形格的沿墙 Tile 长度等于格边长；长方形格按归一化网格坐标中的单位方向长度换算。门的尺寸是整数 Tile，但斜门投影覆盖的地图格子仍可能只被部分覆盖。门中心也可能位于半格坐标，例如宽 3 Tile 的门，这不代表门宽为半格规格。

门的地面物理厚度继承所属墙体，和沿墙 Tile 宽度分别表达。当前未改变整张地图的保守导航投影精度。

## 数据与领域归属

一条 `WallEdge` 保存 `gates` 初始描述列表，每座门有独立稳定 ID：

```json
"gates": [
  { "id": "west-gate", "startTile": 3, "widthTiles": 4, "openingTiles": 2, "isOpen": false },
  { "id": "east-gate", "startTile": 10, "widthTiles": 4, "openingTiles": 2, "isOpen": true }
]
```

`startTile` 从墙段坐标较小的一端计量，横向优先比较 X，纵向比较 Y；反向绘制同一墙线不会翻转门的位置。改变墙端点时保留整数起点索引，不按比例缩小门；放不下就拒绝移动。

工事领域负责尺寸、门框区间、端部净空、门间冲突和独立门状态。Maps 转换初始描述、定位诊断；建设应用层协调候选墙网、单位净空和模拟空间发布。渲染与导航各自从同一份工事快照派生，不从图片计算洞口。

运行建设也可调用 `MapPlaySession.TryPlaceGate`；它复用 `WallNetwork.TryPlaceGate`，只有提交成功才返回新门 ID。当前试玩界面仍提供开关门工具，没有新增运行时放门面板。建设费用和阶段许可沿用原型现状，本次未引入经济体系。

## 素材和表现

门柱、直门楣、门洞内壁与墙身使用同一套石材、世界长度纹理坐标、墙厚和高度。墙身沿多个门框区间切分，门柱和门顶共用普通墙的生成流程；网格分片不会改变门或墙的身份。

铁栅是有厚度的程序化构件，安装在可见外口。关闭时覆盖门洞，开启时显示抬起后的状态，尚未添加开关过渡动画。默认使用纯色金属；可在 `MapVisualCatalog.gateMetal` 替换金属纹理。旧的正面门图片仍用于工具缩略图，不再决定正式门框或门洞几何。

近乎沿视线方向的墙，门正面会自然变窄；选择命中使用投影体凸包，包含空门洞，而非固定正面图片矩形。

## 旧地图

读取旧 `hasGate` / `gate` 时在基础设施导入层转换，保留开关状态并生成 `墙段ID/gate` 身份。旧世界长度按当前 Tile 尺寸调整为整数，并保证两侧整格门柱。例如原先总宽 4.5、净宽 2 的门，在单位格地图中会转为总宽 6、净宽 2。沿墙位置吸附到最近整格，可能发生位置和总宽调整；狭短墙段应按诊断重新放置。

读取不会自动保存或修改源文件。下次主动保存时输出新的 `gates` 列表。JSON 中写入 `widthTiles: 4.5` 等非整数值会明确拒绝，不截断成整数。

## 阅读入口与验证

- `Gameplay/Fortifications/Domain/Rules/WallGateRules.cs`：整数 Tile、任意方向区间与冲突规则。
- `Gameplay/Fortifications/Domain/Aggregates/WallNetwork/WallNetwork.cs`：多门放置与独立开关。
- `Gameplay/Fortifications/Domain/Rules/WallObstacleRules.cs`：扣除各座已开门洞后输出通用导航障碍。
- `Gameplay/Fortifications/Unity/Views/GatePresentation.cs`：程序化结构、铁栅与投影命中。
- `Tools/MapAuthoring/Editor/MapEditorWindow.cs`：编辑器意图和历史提交。
- `Infrastructure/Maps/Serialization/MapGateMigration.cs`：旧格式兼容与非整数导入拒绝。

测试结果见 `Docs/Validation/multi-gate-tests.json`。静态 Unity 渲染见 `Docs/Validation/multi-gate-render.png` 和 `multi-gate-detail.png`；静态截图不代表移动端或角色遮挡已完整验收。


## 阵营通行与破门

运行城门的己方门洞永远通行，附近己方单位触发铁栅自动开启，通行不等待表现。敌方碰撞图始终封住完整城门，不能跟随己方穿门；敌人可沿含门洞的攻城路线接近，在门前按攻击间隔扣除耐久。耐久归零后先同步空间，再提交摧毁状态和表现版本，门框保留。重启创建新的城门防御状态，不改写源地图。

阅读路径：MapPlaySession.Advance → GatePassageSession.Update → GateDefense.ReceiveAttack → ISpatialBattleSimulation.UpdateNavigationMap → TerrainCollisionSystem。单位位置由 ECS 拥有，门耐久由 GateDefense 拥有，铁栅开关只是投影。当前原型己方单位为 Hero，敌方为 Goblin/Runner。
