using Game.Battle.Domain;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation
{
    // IComponentData 表示可挂到 Entity 上的 ECS 数据。真实状态由系统维护，视图只读取快照。
    // 业务 Id 用于显示关联；Entity 自身是所属 World 内的句柄，不能当作跨局永久 ID。
    public struct UnitIdentity : IComponentData { public ulong Id; public UnitKind Kind; }
    // 自定义二维地面坐标，不是 GameObject.Transform。Previous 供本步扫掠接触检测使用。
    public struct GroundTransform : IComponentData { public float2 Position, Previous; public float FacingX; }
    // 当前生命只保存在这里；攻击/受伤时间使用模拟时钟，表现层据此播放动作。
    public struct Vitals : IComponentData { public int Health, MaxHealth, Attack, Defense; public float LastHitTime, LastAttackTime; }
    // 速度为每秒地面单位；半径决定逻辑接触范围，与 Sprite 图片尺寸无关。
    public struct UnitMotion : IComponentData { public float Speed, Radius; }
    // 每个单位独立保存随机流与方向状态，避免并行移动时共享一个可变随机数对象。
    public struct WanderState : IComponentData { public Random Random; public float2 Direction; public float Remaining; }
    // 无字段的 Tag 通过“是否具有此组件”表达分类，供查询筛选。
    public struct HeroTag : IComponentData { }
    public struct MonsterTag : IComponentData { }
    // 每只怪物保存与英雄的上一接触状态；持续重叠不重复扣血，分离后允许再次接触。
    public struct HeroContact : IComponentData { public byte WasOverlapping; }
    // 本局只创建一份，可用 GetSingleton 读取。Singleton 是当前 World 的唯一匹配数据，不是 static 单例。
    public struct BattleSettings : IComponentData { public float2 HalfSize, MoveInput; }
    // 由接触系统维护的统计；当前没有后台 Job 读写这份 Singleton 数据。
    public struct ContactStatistics : IComponentData { public int Count; }
}
