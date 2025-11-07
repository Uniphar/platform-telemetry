global using Microsoft.Extensions.Logging;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Moq;
global using OpenTelemetry;
global using OpenTelemetry.Logs;
global using LogLevel = Microsoft.Extensions.Logging.LogLevel;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]