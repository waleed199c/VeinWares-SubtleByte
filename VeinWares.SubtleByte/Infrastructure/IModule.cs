namespace VeinWares.SubtleByte.Infrastructure;

internal interface IModule : System.IDisposable
{
    void Initialize(ModuleContext context);
}

internal interface IUpdateModule
{
    void OnUpdate(float deltaTime);
}
