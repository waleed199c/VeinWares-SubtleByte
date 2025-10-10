namespace VeinWares.SubtleByte.Template.Infrastructure;

public interface IModule : System.IDisposable
{
    void Initialize(ModuleContext context);
}

public interface IUpdateModule
{
    void OnUpdate(float deltaTime);
}
