using HtmlToPdfCore.Extensions;
using HtmlToPdfCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HtmlToPdfCore.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHtmlToPdfCore_Action_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddHtmlToPdfCore());
    }

    [Fact]
    public void AddHtmlToPdfCore_Action_NoConfigure_RegistersCoreServicesAndMemoryOptimizer()
    {
        var services = new ServiceCollection();

        services.AddHtmlToPdfCore();

        Assert.Contains(services, d => d.ServiceType == typeof(IHtmlToPdfRenderer));
        Assert.Contains(services, d => d.ServiceType == typeof(IPdfManipulator));
        Assert.Contains(services, d => d.ServiceType.IsGenericTypeDefinition && d.ServiceType == typeof(ILogger<>));
        Assert.Contains(services, d => d.ServiceType == typeof(IMemoryOptimizer));
    }

    [Fact]
    public void AddHtmlToPdfCore_Action_EnableAIOptimizationFalse_DoesNotRegisterMemoryOptimizer()
    {
        var services = new ServiceCollection();

        services.AddHtmlToPdfCore(opts => opts.EnableAIOptimization = false);

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IMemoryOptimizer));
    }

    [Fact]
    public void AddHtmlToPdfCore_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection();

        services.AddHtmlToPdfCore();
        services.AddHtmlToPdfCore();

        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IHtmlToPdfRenderer)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IPdfManipulator)));
        Assert.Equal(1, services.Count(d => d.ServiceType.IsGenericTypeDefinition && d.ServiceType == typeof(ILogger<>)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IMemoryOptimizer)));
    }

    [Fact]
    public void AddHtmlToPdfCore_Options_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var options = new HtmlToPdfCoreOptions();

        Assert.Throws<ArgumentNullException>(() => services.AddHtmlToPdfCore(options));
    }

    [Fact]
    public void AddHtmlToPdfCore_Options_NullOptions_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        HtmlToPdfCoreOptions options = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddHtmlToPdfCore(options));
    }

    [Fact]
    public void AddHtmlToPdfCore_Options_ForwardsEnableAIOptimization_True_RegistersMemoryOptimizer()
    {
        var services = new ServiceCollection();
        var options = new HtmlToPdfCoreOptions
        {
            EnableAIOptimization = true,
            MaxConcurrentRenders = 4,
            DefaultRenderTimeout = 5000
        };

        services.AddHtmlToPdfCore(options);

        Assert.Contains(services, d => d.ServiceType == typeof(IMemoryOptimizer));
    }

    [Fact]
    public void AddHtmlToPdfCore_Options_ForwardsEnableAIOptimization_False_DoesNotRegisterMemoryOptimizer()
    {
        var services = new ServiceCollection();
        var options = new HtmlToPdfCoreOptions
        {
            EnableAIOptimization = false,
            MaxConcurrentRenders = 2,
            DefaultRenderTimeout = 1000
        };

        services.AddHtmlToPdfCore(options);

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IMemoryOptimizer));
    }

    [Fact]
    public void HtmlToPdfCoreOptions_Defaults_AreExpected()
    {
        var options = new HtmlToPdfCoreOptions();

        Assert.True(options.EnableAIOptimization);
        Assert.Equal(Environment.ProcessorCount, options.MaxConcurrentRenders);
        Assert.Equal(30000, options.DefaultRenderTimeout);
        Assert.False(options.EnableDetailedLogging);
        Assert.Equal(10, options.ObjectPoolSize);
    }
}
