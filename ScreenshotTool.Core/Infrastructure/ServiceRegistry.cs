namespace ScreenshotTool.Core.Infrastructure;

public sealed class ServiceRegistry : IDisposable
{
    private readonly Dictionary<Type, Func<ServiceRegistry, object>> _factories = new();
    private readonly Dictionary<Type, object> _instances = new();

    public void AddSingleton<TService>(Func<ServiceRegistry, TService> factory)
        where TService : class
    {
        _factories[typeof(TService)] = registry => factory(registry);
    }

    public TService GetRequiredService<TService>()
        where TService : class
    {
        var serviceType = typeof(TService);
        if (_instances.TryGetValue(serviceType, out var existing))
        {
            return (TService)existing;
        }

        if (!_factories.TryGetValue(serviceType, out var factory))
        {
            throw new InvalidOperationException($"未注册服务：{serviceType.FullName}");
        }

        var created = factory(this);
        _instances[serviceType] = created;
        return (TService)created;
    }

    public void Dispose()
    {
        foreach (var disposable in _instances.Values.OfType<IDisposable>())
        {
            disposable.Dispose();
        }

        _instances.Clear();
        _factories.Clear();
    }
}
