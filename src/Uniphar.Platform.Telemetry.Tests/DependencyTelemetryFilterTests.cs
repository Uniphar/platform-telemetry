using System.Diagnostics;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Uniphar.Platform.Telemetry.Tests;

[TestClass]
public class DependencyTelemetryFilterTests
{
    [TestMethod]
    [DataRow("Azure blob: testcontainer/input", AzureResourceNamespaces.Storage, (int)HttpStatusCode.Conflict, AzureResourceNamespaces.Storage, true)]
    [DataRow("Azure blob: testcontainer/input", AzureResourceNamespaces.Storage, (int)HttpStatusCode.TooManyRequests, AzureResourceNamespaces.Storage, false)]
    [DataRow("PUT testcontainer/input", AzureResourceNamespaces.Storage, (int)HttpStatusCode.TooManyRequests, "System.Http", false)]
    [DataRow("test.servicebus.windows.net", AzureResourceNamespaces.ServiceBus, (int)HttpStatusCode.Conflict, AzureResourceNamespaces.ServiceBus, true)]
    [DataRow("test.computeaksacr.azurecr.io", AzureResourceNamespaces.ContainerRegistry, (int)HttpStatusCode.Unauthorized, AzureResourceNamespaces.ContainerRegistry, true)]
    [DataRow("SomeOtherOperation", null, (int)HttpStatusCode.Conflict, null, false)]
    public void OnEnd_BlobStorage_FiltersConfiguredStatusCodes(string displayName, string? azNamespace, int statusCode, string? resourceNamespace, bool shouldFilter)
    {
        // Arrange
        var configuration = new DependencyFilterConfiguration
        {
            Rules =
            [
                new DependencyFilterRule
                {
                    ResourceNamespace = AzureResourceNamespaces.Storage,
                    StatusCodes = [(int)HttpStatusCode.Conflict]
                },
                new DependencyFilterRule
                {
                    ResourceNamespace = AzureResourceNamespaces.ServiceBus,
                    StatusCodes = [(int)HttpStatusCode.Conflict]
                },
                new DependencyFilterRule
                {
                    ResourceNamespace = AzureResourceNamespaces.ContainerRegistry,
                    StatusCodes = [(int)HttpStatusCode.Unauthorized]
                }
            ]
        };

        var filter = new DependencyTelemetryFilter(configuration);
        using var activity = new Activity(displayName);
        if (azNamespace != null)
            activity.SetTag("az.namespace", azNamespace);
        activity.SetTag("http.response.status_code", statusCode);
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        // Act
        filter.OnEnd(activity);

        // Assert
        if (shouldFilter)
        {
            Assert.IsFalse(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
        }
        else
        {
            Assert.IsTrue(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
        }
    }

    [TestMethod]
    public void OnEnd_FileShare_WithCreateIfNotExistsDisplayName_FiltersConflict()
    {
        // Arrange
        var configuration = new DependencyFilterConfiguration
        {
            Rules =
            [
                new DependencyFilterRule
                {
                    ResourceNamespace = AzureResourceNamespaces.Storage,
                    StatusCodes = [409]
                }
            ]
        };

        var filter = new DependencyTelemetryFilter(configuration);
        using var activity = new Activity("ShareDirectoryClient.CreateIfNotExists");
        activity.SetTag("az.namespace", "Microsoft.Storage");
        activity.SetTag("http.response.status_code", "409");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        // Act
        filter.OnEnd(activity);

        // Assert
        Assert.IsFalse(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [TestMethod]
    public void OnEnd_FileShare_WithAzureResourceNamespacesNamespace_FiltersConflict()
    {
        // Arrange
        var configuration = new DependencyFilterConfiguration
        {
            Rules =
            [
                new DependencyFilterRule
                {
                    ResourceNamespace = AzureResourceNamespaces.Storage,
                    StatusCodes = [409]
                }
            ]
        };

        var filter = new DependencyTelemetryFilter(configuration);
        using var activity = new Activity("ShareDirectoryClient.CreateIfNotExists");
        activity.SetTag("azure.resource_provider.namespace", "Microsoft.Storage");
        activity.SetTag("http.response.status_code", "409");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        // Act
        filter.OnEnd(activity);

        // Assert
        Assert.IsFalse(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [TestMethod]
    public void OnEnd_ContainerRegistry_Filters401And403()
    {
        // Arrange
        var configuration = new DependencyFilterConfiguration
        {
            Rules =
            [
                new DependencyFilterRule
                {
                    ResourceNamespace = AzureResourceNamespaces.ContainerRegistry,
                    StatusCodes = [401, 403]
                }
            ]
        };

        var filter = new DependencyTelemetryFilter(configuration);
        
        // Test 401
        using var activity401 = new Activity("HTTP GET");
        activity401.SetTag("az.namespace", "Microsoft.ContainerRegistry");
        activity401.SetTag("http.response.status_code", "401");
        activity401.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity401.Start();

        filter.OnEnd(activity401);
        Assert.IsFalse(activity401.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));

        // Test 403
        using var activity403 = new Activity("HTTP GET");
        activity403.SetTag("az.namespace", "Microsoft.ContainerRegistry");
        activity403.SetTag("http.response.status_code", "403");
        activity403.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity403.Start();

        filter.OnEnd(activity403);
        Assert.IsFalse(activity403.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [TestMethod]
    public void OnEnd_ServiceBus_Filters401()
    {
        // Arrange
        var configuration = new DependencyFilterConfiguration
        {
            Rules =
            [
                new DependencyFilterRule
                {
                    ResourceNamespace = AzureResourceNamespaces.ServiceBus,
                    StatusCodes = [401]
                }
            ]
        };

        var filter = new DependencyTelemetryFilter(configuration);
        using var activity = new Activity("HTTP POST");
        activity.SetTag("az.namespace", "Microsoft.ServiceBus");
        activity.SetTag("http.response.status_code", "401");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        // Act
        filter.OnEnd(activity);

        // Assert
        Assert.IsFalse(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [TestMethod]
    public void OnEnd_MultipleStatusCodes_FiltersAll()
    {
        // Arrange
        var configuration = new DependencyFilterConfiguration
        {
            Rules =
            [
                new DependencyFilterRule
                {
                    ResourceNamespace = AzureResourceNamespaces.Storage,
                    StatusCodes = [401, 403, 409]
                }
            ]
        };

        var filter = new DependencyTelemetryFilter(configuration);
        var statusCodes = new[] { "401", "403", "409" };

        foreach (var statusCode in statusCodes)
        {
            using var activity = new Activity("Azure blob: PUT");
            activity.SetTag("az.namespace", "Microsoft.Storage");
            activity.SetTag("http.response.status_code", statusCode);
            activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
            activity.Start();

            // Act
            filter.OnEnd(activity);

            // Assert
            Assert.IsFalse(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded), 
                $"Status code {statusCode} should be filtered");
        }
    }

    [TestMethod]
    public void OnEnd_NoMatchingRule_DoesNotFilter()
    {
        // Arrange
        var configuration = new DependencyFilterConfiguration
        {
            Rules =
            [
                new DependencyFilterRule
                {
                    ResourceNamespace = AzureResourceNamespaces.Storage,
                    StatusCodes = [409]
                }
            ]
        };

        var filter = new DependencyTelemetryFilter(configuration);
        using var activity = new Activity("Azure blob: PUT");
        activity.SetTag("az.namespace", "Microsoft.Storage");
        activity.SetTag("http.response.status_code", "500");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        // Act
        filter.OnEnd(activity);

        // Assert
        Assert.IsTrue(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [TestMethod]
    public void OnEnd_UnknownResourceType_DoesNotFilter()
    {
        // Arrange
        var configuration = new DependencyFilterConfiguration
        {
            Rules =
            [
                new DependencyFilterRule
                {
                    ResourceNamespace = AzureResourceNamespaces.Storage,
                    StatusCodes = [409]
                }
            ]
        };

        var filter = new DependencyTelemetryFilter(configuration);
        using var activity = new Activity("HTTP GET");
        // No az.namespace tag set
        activity.SetTag("http.response.status_code", "409");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        // Act
        filter.OnEnd(activity);

        // Assert
        Assert.IsTrue(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [TestMethod]
    public void OnEnd_NoNamespaceTag_DoesNotFilter()
    {
        // Arrange
        var configuration = new DependencyFilterConfiguration
        {
            Rules =
            [
                new DependencyFilterRule
                {
                    ResourceNamespace = AzureResourceNamespaces.Storage,
                    StatusCodes = [409]
                }
            ]
        };

        var filter = new DependencyTelemetryFilter(configuration);
        using var activity = new Activity("Azure blob: PUT");
        // No namespace tag - should not filter even if display name matches
        activity.SetTag("http.response.status_code", "409");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        // Act
        filter.OnEnd(activity);

        // Assert
        Assert.IsTrue(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [TestMethod]
    public void DefaultConfiguration_Filters409ForStorageAndServiceBus()
    {
        // Arrange
        var configuration = DependencyFilterConfiguration.Default;

        // Assert
        Assert.AreEqual(2, configuration.Rules.Count);
        Assert.IsTrue(configuration.Rules.Any(r => r.ResourceNamespace == AzureResourceNamespaces.Storage && r.StatusCodes.Contains(409)));
        Assert.IsTrue(configuration.Rules.Any(r => r.ResourceNamespace == AzureResourceNamespaces.ServiceBus && r.StatusCodes.Contains(409)));
    }
}
