using WorkFlowPro.Application.Common.Models;
using WorkFlowPro.Domain.Common;
using Xunit;

namespace WorkFlowPro.UnitTests;

public class ArchitectureTests
{
    [Fact]
    public void DomainLayer_ShouldNotDependOnOtherLayers()
    {
        var domainAssembly = typeof(BaseEntity).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        Assert.DoesNotContain(referencedAssemblies, a => a.Name == "WorkFlowPro.Application");
        Assert.DoesNotContain(referencedAssemblies, a => a.Name == "WorkFlowPro.Infrastructure");
        Assert.DoesNotContain(referencedAssemblies, a => a.Name == "WorkFlowPro.Api");
    }

    [Fact]
    public void ApplicationLayer_ShouldNotDependOnInfrastructureOrApi()
    {
        var applicationAssembly = typeof(HealthCheckResponse).Assembly;
        var referencedAssemblies = applicationAssembly.GetReferencedAssemblies();

        Assert.DoesNotContain(referencedAssemblies, a => a.Name == "WorkFlowPro.Infrastructure");
        Assert.DoesNotContain(referencedAssemblies, a => a.Name == "WorkFlowPro.Api");
    }
}
