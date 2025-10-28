using System.Reflection;

namespace Uniphar.Platform.Telemetry;

public static class ReflectionExtensions
{
    public static MethodInfo GetMethod(Delegate del) => del.Method;
}