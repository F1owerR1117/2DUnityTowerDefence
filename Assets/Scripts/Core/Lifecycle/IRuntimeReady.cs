namespace DoudizhuTower.Core.Lifecycle
{
    /// <summary>
    /// 运行时就绪接口。
    /// 实现此接口的对象必须声明自己是否已准备好参与游戏逻辑。
    /// 只有 IsRuntimeReady == true 时，Update 才允许执行游戏逻辑。
    /// </summary>
    public interface IRuntimeReady
    {
        bool IsRuntimeReady { get; }
    }
}
