# 原型运行验证

日期：2026-10-16。环境：Unity 6000.6.4f1 / Windows Editor。

- 三种透明 Sprite 已导入并设置脚部枢轴；实际运行 Sprite 的 `packed=True`，纹理名为 `sactx-0-1024x1024-DXT5|BC3-BattleUnits-...`。
- Sprite Packer 读回值为 `SpriteAtlasV2`，内置交付。
- 初始快照为 1 英雄、6 普通怪、4 快速怪；英雄生命 120。
- 注入 RightArrow 按下与释放后，英雄从原点向右移动，并被限制在 `x=12.58`（13 减碰撞半径 0.42）。
- 接触后观察到英雄掉血及怪物减少；自动化测试另外验证准确伤害、接触去重和双方同时致死。
- 使用 Input System 指针注入点击摇杆，观测到非零方向；释放后为 `(0,0)`。
- 调用已绑定 Restart 按钮的点击回调，观测到英雄恢复 120、怪物恢复 10、Generation 递增。
- 性能 HUD 实际显示 FPS、帧时、模拟耗时、Unity 内存、托管内存和单位数量。
- 本机截图采样曾显示 120 FPS；这只是小规模 Editor 运行记录，不代表大规模、独立 Player 或手机性能。
- 当前 Editor 未提供有效 Draw Calls 采样时显示 `n/a`，不把缺失采样当作 0。
- 验证期间的历史导入/编译错误已修复；最后运行检查应结合当前 Console 状态与测试结果文件。

截图：`battle-prototype.png`。自动化结果：`editmode-tests.json`。
尚未进行 Android/iOS 真机验证，也未验证大量单位的 SpriteRenderer 渲染性能。
