# AI 调试与普通攻击重构验证

日期：2026-10-27。Unity 6000.6.4f1，Entities 6.6.0。

## 自动验证

- 脚本编译通过，`git diff --check` 通过。
- 全量 EditMode：408 项，407 通过。记录：`ai-target-cooldown-debug-tests.json`。
- 唯一失败是之前已记录的地图编辑器测试 `RightClick_CancelsUncommittedBrushAndPlacement_WithoutRemovingCompletedEdits`：预期 Select，实际 Facility，本轮未修改该路径。
- 覆盖目标保持、导航/攻击同目标、换目标不刷新冷却、英雄先手和敌人首次等待、持续接触按间隔攻击、单位/墙门/主堡共用冷却、外部提交失败不耗冷却、诊断路径只读和独立、晕眩/定身停止原因及墙门目标身份。

## 运行验证

在 `Assets/Scenes/Development/DefensePlayground.unity` 进入 Play Mode，确认帧数实际增长。

1. 默认 AI 图层关闭。
2. 向正常 Dynamic 输入更新阶段注入 P 的按下/松开事件，控制器正常 Update 打开图层，`AiDebugVisible=true`，根元素 `display=Flex`。
3. 悬停敌人后，详情显示对应 Goblin 的命令、追敌行为、主堡目标、停止原因、普通攻击剩余冷却和剩余路径点数。聚集单位使用避让的小标签，完整详情在固定面板显示。
4. 截图检查青色路径、橙色目标线/叉号、绿色警戒圈与地图地面坐标一致；非聚焦单位淡化，UI 图层 `pickingMode=Ignore`。
5. 再次注入正常 P 输入事件，`AiDebugVisible=false`，根元素 `display=None`，调试图形全部隐藏。
6. 运行期间未发现玩法或 UI 异常。CLI 本身记录了 `editor status` 不支持的兼容性报错，未将它计作游戏异常。
7. 已退出测试用 Play Mode，恢复原编辑场景；没有保存运行中的游戏状态。

截图：

- [开启并悬停敌人](ai-debug-enabled.png)
- [关闭](ai-debug-disabled.png)

调试数据仅开启时按 10Hz 采样，关闭清空诊断列表并停止采样；读取不推进模拟、不修改命令和寻路 Buffer。当前实现未做目标设备的峰值性能测试。
