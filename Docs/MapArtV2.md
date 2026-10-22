# 地图美术 V2

本轮使用内置 ImageGen 生成素材，使用 Unity Sprite Editor 数据提供器裁切与导入；未修改人物素材。

## 内容与使用

- Unity → Game → 地图编辑器 → **美术示例地图**：打开 72×48 的 green-valley 示例。
- 画布上方固定的 **显示 Tile 网格 / 隐藏 Tile 网格** 按钮独立控制网格线，默认隐藏，不随素材列表滚动。实际通行叠加仍可单独打开。
- **Q** 选择平移工具，左键拖动整张地图；**V** 返回选择。中键拖动或按住空格加左键也可临时平移。滚轮以鼠标位置为中心缩放。
- 所有快捷键在画布获得焦点时生效。平移、缩放和网格开关不修改地图文档，也不加入撤销记录。
- 五种建筑：游侠箭楼、曙光主堡、星辉法师塔、铜火炮坊、风车农庄。新增外观与配置不自动增加攻击、研究或经济能力。
- 岩石、橡树和针叶树与地面使用一致的光照和配色。障碍有固定随机变体、少量视觉偏移，逻辑格子保持原位。

## 自然拼接的实现

草地是连续底层；道路与泥地共用软土覆盖层，水面使用独立覆盖层和岸边。相邻格子密度插值形成横路、纵路、转弯与分叉，不需要产品人员逐一选择方向素材。表面在世界坐标中重复采样，边界共享坐标、透明度与 UV；轻微轮廓扰动减少机械直角感。

地形仍以格子保存并判定通行。过渡轮廓是表现，不改变建设与碰撞规则；可通过“显示实际通行”检查真实边界。首版仍可看到部分格状轮廓，尚无自由曲线道路编辑、悬崖、桥梁或多级地形。

城墙所有方向共用实体几何：地面矩形厚度固定为最小格边长的 0.52 倍；高度投影严格使用 `ground + Vector2.up * height`，不包含横向偏移。墙身、墙顶、女墙与垛口由同一组地面轮廓竖直抬升，只有地面轮廓随方向旋转。使用 limestone 材质贴面，不再按角度切换或拉伸横墙、纵墙的整幅图片。转角与自由端保留独立柱，墙身接到柱边。编辑器与游戏共用几何、UV、颜色与稳定排序，不改变墙节点、稳定 ID 或通行规则。

装饰与建筑按脚底 Y 从远到近排序；同高度再按墙身/物体层、脚底 X、稳定身份排序。墙身片段还有各自的空间坐标，障碍使用格子坐标身份。游戏中同 sortingOrder 的对象另用共享顺序产生微小 Z 深度，避免依赖重建后的 InstanceID。新增对象不会交换原有对象的相对顺序。

## 文件与阅读路径

所有最终 PNG 保存在 `Assets/Content/Maps/ArtV2/`：

| 文件 | 用途 |
| --- | --- |
| objects.png | 五种建筑、橡树、针叶树与岩石，透明图集 |
| materials.png | 草地、泥地、水面与石材，四象限材质 |
| walls.png | 当前使用角柱与端柱；旧方向墙身切片保留为制作源 |
| wall-vertical.png | 旧纵墙素材保留为制作源，当前墙身已改用统一几何 |
| green-valley.map.json | 72×48 示例，12 座预放建筑、河湖、林地和南北开口城墙 |

映射与锚点在 `Assets/Content/Maps/Shared/MapVisualCatalog.asset`，占地配置在同目录 `facilities.json`。以后替换图片时应通过 Unity Sprite Editor 更新切片，保持 Sprite 名称与引用；勿手写 .meta。

绘制调用链：
`MapCanvas / MapWorldView → MapSurfaceGeometry + MapSceneSprites → 编辑器网格 / MapSurfaceView 分块 Mesh`。

- `MapSurfaceGeometry.cs`：共享地形边缘与纹理坐标。
- `MapSurfaceView.cs`：按 16×16 格合并运行时地面网格。
- `MapSceneSprites.cs`：建筑脚底、障碍变体、墙向与连接柱。
- `MapArtShowcase.cs`：只生成美术示例地图。
- `MapArtCapture.cs`：临时附加场景中渲染实际 MapWorldView，不污染当前场景。

## 生成记录

模式：**内置 ImageGen**，保留实际 Alpha，未使用 CLI 回退。
生成源目录：`C:/Users/a/.codex/generated_images/01a15a77-8040-7b63-a1e9-417c0b1eed4f/`。
选用源文件分别为 exec-33f5f2c9-2720-40f4-9b51-8f0961bffc16.png、exec-97ed963c-f901-4aae-8985-28d4c56e11f9.png、exec-73a44ea1-2442-4fa2-950c-420268e87de5.png、exec-26783a9d-6adf-4afc-83c6-dda50471f6c5.png。

以下记录最终提示词。objects 为制作规格整理，其余为实际调用文本。

### 建筑与障碍（objects）

Use case: stylized-concept. Production transparent 4 columns by 2 rows atlas. Polished original hand-painted fantasy tower-defense art, chunky readable silhouettes, elevated orthographic front view on flat XY ground, consistent upper-left lighting. Sage olive foliage, ivory limestone, brown timber, terracotta and blue slate roofs. Top row: archer tower, violet-roof magical observatory with cyan crystal, two-turret keep, cannon workshop. Bottom row: farm cottage with windmill and grain, oak, fir cluster, moss boulders. Fully isolated, generous transparent gutters, no labels, characters, backgrounds or platforms; tight shadows.

### mapMaterialPrompt

Use case: stylized-concept. Asset type: 2D fantasy tower-defense material texture atlas, ONE square image with EXACT 2 columns x 2 rows equal square tiles, no gutters, no grid lines. Four opaque fully painted material swatches, flat top-down orthographic, no objects, no perspective, no tile border, no shadow around perimeter. Top-left: muted sage and olive meadow grass, soft painterly broad leaf strokes, extremely sparse small tufts, low contrast, no flowers. Top-right: warm light ochre sandy footpath soil, subtle compacted ground, a few tiny embedded smooth pebbles, low contrast. Bottom-left: deep teal river water with calm gentle broad flowing ripples, no shore, no objects, moderately low contrast. Bottom-right: warm gray limestone masonry wall face, several regular horizontal courses of large chunky rectangular blocks, warm beige mortar, hand painted gentle variation, horizontally repeatable. Style: polished original hand-painted fantasy strategy game, approachable chunky illustrated look reminiscent of Kingdom Rush's landscape, confident but subtle outlines on stone, warm upper-left daylight, NOT photorealistic, no gritty high-frequency noise, no embossed 3D tile platforms. Each entire quadrant is a seamless full-bleed material texture, its same material continues uniformly to all four edges. Do not depict separate floating squares, frames, gaps, rounded corners, trees, houses, labels, text, watermarks or drop shadows. This image will be precisely sliced at the center into four texture sources and used with soft transition masks; all quadrants must be filled completely, no black margins.

### mapFinalWallPrompt

Use case: stylized-concept. Generate a transparent-background 2D game sprite atlas. This is NOT a presentation: absolutely no backdrop, no colored haze, no ground plane, no surrounding shadows. Alpha transparency everywhere outside the objects. Exact 3 columns x 2 rows with six fully separate objects inside their own equally sized cells, generous empty transparent gutters. Hand-painted fantasy defense game, chunky warm ivory limestone blocks with gentle moss, soft upper-left lighting, matching whimsical medieval buildings with blue flags and red roofs. Elevated orthographic front view. Six modular fortification components: row 1 col1 horizontal low crenellated stone wall without attached towers, row1 col2 north-south upright stone wall in correct receding perspective without towers, row1 col3 diagonal wall bottom-left to top-right without towers. Row2 col1 diagonal wall top-left to bottom-right without towers. Row2 col2 standalone small square crenellated stone corner pillar, same height as walls. Row2 col3 standalone small round crenellated stone terminal pillar, same height. Each object fully inside own cell. Stones and merlons stay upright for diagonal and vertical walls; architectural perspective, not flat image rotation. Clear professional readable silhouettes and confident illustrated outlines. Tight moss only directly attached to stone. No landscape, no gradients behind objects, no smoke, no checkerboard, no text or labels, no UI frames, no characters.

### mapVerticalWallPrompt

Use case: stylized-concept. One single game sprite: a NORTH-SOUTH VERTICAL medieval stone curtain wall segment for a 2D elevated orthographic FRONT-VIEW fantasy tower defense game. True alpha transparency, no background, no gradients or cast shadows behind it. The long wall ground axis MUST be perfectly vertical in the image: both ground attachment endpoints have EXACTLY THE SAME X coordinate. NO left-to-right drift, NO diagonal lean, NOT isometric. A narrow straight wall walkway seen from above, extending directly up/down the center of the image, about 4 times as long as its visible width, with regular chunky ivory limestone battlements along BOTH long vertical sides. The front end shows a short warm-gray stone face, three courses of blocks, and all stone courses remain consistent. Moss tiny on stone only. Centered, whole object isolated, generous transparent margin all around. Hand-painted warm storybook fantasy art similar in rendering craft to Kingdom Rush, bright warm limestone, soft upper-left daylight, bold tidy outline, not realistic gritty stone. This is intended to tile end-to-end vertically, so the main shaft must be perfectly straight with parallel sides and no attached towers, buttresses, end columns, decorative large rocks, ground patches, text or UI.

## 验证

2026-10-21，Unity 6000.6.4f1：编译无错误和警告，完整 EditMode 测试 99/99 通过。覆盖相邻地表拼接、横纵转弯道路、示例校验、画布平移与文档不变性。实际 MapWorldView 渲染截图在 `Docs/Validation/map-art-v2-overview.png` 与 `map-art-v2-detail.png`。独立 Play Mode 正确加载 green-valley、12 座初始建筑与分块地面，模拟持续推进。

尚未进行独立 Player 构建与移动端性能验收。

### 斜墙、排序与网格按钮修正

本轮编译无错误和警告，完整 EditMode 测试 104/104 通过。新增覆盖正负斜率、陡斜墙的四角映射，以及新增建筑、反转源对象列表后的已有对象相对顺序。按钮通过实际 PointerDown/PointerUp 测试，切换网格不改变地图文档。缓斜墙、陡斜墙与多个转角的实际运行时视图见 `Docs/Validation/map-wall-diagonal-fixed.png`。本轮没有执行 Player 构建。

### 纵墙的立体墙身修正

历史方案，已被下方的统一竖直投影替代；该阶段的横向高度偏移不符合当前俯视视角，不能继续沿用。

此前的纵墙与陡斜墙只有墙顶贴图，缺少石砌侧面，容易被看成地沟。新增 `MapRaisedWallPresentation.cs`：按真实墙线建立有宽度的基座，投影抬升墙顶，朝向观察者的一侧连接石砌立面；墙顶走道缩窄，留出可见承重墙身。材质复用 `materials.png` 的 limestone 切片，映射为目录资产的 `Wall Masonry`，没有生成或修改 PNG。编辑器与游戏共用面片与颜色，地面节点、碰撞、墙边身份保持不变。

编译无错误和警告，完整 EditMode 测试 107/107 通过；新增验证正纵向与正负陡斜方向的立面、墙顶接合和源文档不变性。实际当前编辑文档的表现截图：`Docs/Validation/map-wall-volume-current.png`。本轮没有执行 Player 构建。

### 统一竖直投影（当前实现）

取消缓斜/陡斜分支和斜向高度偏移，所有墙身通过 `MapRaisedWallPresentation.Build` 生成。地面宽度始终为最小格边长的 0.52 倍；墙身、女墙和垛口都通过 `Project(ground, height)` 抬升，高度只改变 Y。贴面不会再把立面旋转成倾斜石片。一个遮挡片段的多个面合并为一个 Mesh，编辑器与游戏直接使用相同顶点、颜色和 UV；墙柱使用原有独立 Sprite，墙身在柱边结束。

阅读顺序：`MapSceneSprites → MapRaisedWallPresentation → MapCanvas.DrawSurface / MapWallMeshView`。

本轮编译 0 错误、0 警告，完整 EditMode 测试 **116/116** 通过。回归验证 16 个角度（包含 85°、89°、90°、91°、95°）的每条立面竖边 X 不变、墙宽不变、反向墙网绘制一致与源文档不变。另实际渲染 16 个方向并检查用户当前编辑中的墙网。当前截图：`Docs/Validation/map-wall-upright-current.png`；方向对照：`Docs/Validation/map-wall-upright-angles.png`。当前画布已刷新为统一几何，未改写工作文档。未执行 Player 构建。

