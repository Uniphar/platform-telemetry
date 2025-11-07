// Global using directives

global using Azure.Monitor.OpenTelemetry.AspNetCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using OpenTelemetry;
global using OpenTelemetry.Logs;
global using OpenTelemetry.Metrics;
global using OpenTelemetry.Resources;
global using OpenTelemetry.Trace;
global using System.Collections.Immutable;
global using System.Diagnostics;
using System.Runtime.CompilerServices;


[assembly: InternalsVisibleTo("Uniphar.Platform.Telemetry.Tests")]