namespace Game.Battle.Domain.Rules
{
    /// <summary>纯领域计算：不查询实体、不修改组件、不播放特效，可由测试和 Burst 系统共同调用。</summary>
    public static class ContactRules
    {
        // 防御高于攻击时伤害为零，不会变成治疗；生命最低为零。
        public static int Damage(int attack, int defense) => attack <= defense ? 0 : attack - defense;
        public static int ApplyDamage(int health, int damage) => damage >= health ? 0 : health - damage;
        // 相对扫掠圆检测：把两单位的相对运动视为一条线段，判断其是否经过合并半径范围。
        // 输入是怪物相对英雄的前后位置；即使本步起点、终点都不重叠，也能检测中途穿过。
        public static bool SweptCircles(float previousX, float previousY, float currentX, float currentY, float combinedRadius)
        {
            float dx = currentX - previousX, dy = currentY - previousY;
            float lengthSq = dx * dx + dy * dy;
            // 求线段上离原点最近的点；相对位置不变时避免除零，再把参数限制在线段内。
            float t = lengthSq > 0.000001f ? -(previousX * dx + previousY * dy) / lengthSq : 0;
            if (t < 0)
                t = 0;
            if (t > 1)
                t = 1;
            float x = previousX + dx * t, y = previousY + dy * t;
            return x * x + y * y <= combinedRadius * combinedRadius;
        }
    }
}
