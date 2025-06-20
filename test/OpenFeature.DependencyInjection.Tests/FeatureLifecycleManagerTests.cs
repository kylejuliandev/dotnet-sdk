using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFeature.Constant;
using OpenFeature.DependencyInjection.Internal;
using OpenFeature.Model;

namespace OpenFeature.DependencyInjection.Tests;

public class FeatureLifecycleManagerTests
{
    private readonly IServiceCollection _serviceCollection;

    public FeatureLifecycleManagerTests()
    {
        Api.Instance.SetContext(null);
        Api.Instance.ClearHooks();

        _serviceCollection = new ServiceCollection()
            .Configure<OpenFeatureOptions>(options =>
            {
                options.AddDefaultProviderName();
            });
    }

    [Fact]
    public async Task EnsureInitializedAsync_ShouldLogAndSetProvider_WhenProviderExists()
    {
        // Arrange
        var featureProvider = new NoOpFeatureProvider();
        _serviceCollection.AddSingleton<FeatureProvider>(featureProvider);

        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var sut = new FeatureLifecycleManager(Api.Instance, serviceProvider, NullLogger<FeatureLifecycleManager>.Instance);

        // Act
        await sut.EnsureInitializedAsync().ConfigureAwait(true);

        // Assert
        Assert.Equal(featureProvider, Api.Instance.GetProvider());
    }

    [Fact]
    public async Task EnsureInitializedAsync_ShouldThrowException_WhenProviderDoesNotExist()
    {
        // Arrange
        _serviceCollection.RemoveAll<FeatureProvider>();

        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var sut = new FeatureLifecycleManager(Api.Instance, serviceProvider, NullLogger<FeatureLifecycleManager>.Instance);

        // Act
        var act = () => sut.EnsureInitializedAsync().AsTask();

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act).ConfigureAwait(true);
        Assert.NotNull(exception);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public async Task EnsureInitializedAsync_ShouldSetHook_WhenHooksAreRegistered()
    {
        // Arrange
        var featureProvider = new NoOpFeatureProvider();
        var hook = new NoOpHook();

        _serviceCollection.AddSingleton<FeatureProvider>(featureProvider)
            .AddKeyedSingleton<Hook>("NoOpHook", (_, key) => hook)
            .Configure<OpenFeatureOptions>(options =>
            {
                options.AddHookName("NoOpHook");
            });

        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var sut = new FeatureLifecycleManager(Api.Instance, serviceProvider, NullLogger<FeatureLifecycleManager>.Instance);

        // Act
        await sut.EnsureInitializedAsync().ConfigureAwait(true);

        // Assert
        var actual = Api.Instance.GetHooks().FirstOrDefault();
        Assert.Equal(hook, actual);
    }

    [Fact]
    public async Task EnsureInitializedAsync_ShouldSetHandler_WhenHandlersAreRegistered()
    {
        // Arrange
        EventHandlerDelegate eventHandlerDelegate = (_) => { };
        var featureProvider = new NoOpFeatureProvider();
        var handler = new EventHandlerDelegateWrapper(ProviderEventTypes.ProviderReady, eventHandlerDelegate);

        _serviceCollection.AddSingleton<FeatureProvider>(featureProvider)
            .AddSingleton(_ => handler);

        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var sut = new FeatureLifecycleManager(Api.Instance, serviceProvider, NullLogger<FeatureLifecycleManager>.Instance);

        // Act
        await sut.EnsureInitializedAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task EnsureInitializedAsync_ShouldSetHandler_WhenMultipleHandlersAreRegistered()
    {
        // Arrange
        EventHandlerDelegate eventHandlerDelegate1 = (_) => { };
        EventHandlerDelegate eventHandlerDelegate2 = (_) => { };
        var featureProvider = new NoOpFeatureProvider();
        var handler1 = new EventHandlerDelegateWrapper(ProviderEventTypes.ProviderReady, eventHandlerDelegate1);
        var handler2 = new EventHandlerDelegateWrapper(ProviderEventTypes.ProviderReady, eventHandlerDelegate2);

        _serviceCollection.AddSingleton<FeatureProvider>(featureProvider)
            .AddSingleton(_ => handler1)
            .AddSingleton(_ => handler2);

        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var sut = new FeatureLifecycleManager(Api.Instance, serviceProvider, NullLogger<FeatureLifecycleManager>.Instance);

        // Act
        await sut.EnsureInitializedAsync().ConfigureAwait(true);
    }
}
