namespace VeinWares.SubtleByte.Rewrite.Infrastructure;

public interface IModule : System.IDisposable
{
    void Initialize(ModuleContext context);
}

public interface IUpdateModule
{
    void OnUpdate(float deltaTime);
}
