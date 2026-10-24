using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.ReadModels;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>应用层在固定步前提交，ECS 桥接器独占选择、驻守位置、生命和冷却的写入。</summary>
    public interface IPlayerOperations
    {
        PlayerControlSnapshot PlayerControls { get; }
        void Execute(PlayerOperation operation, bool instantDeployment = false);
    }
}
