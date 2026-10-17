using System.Reflection;

namespace HtmlToPdfCore.Tests.Licensing;

/// <summary>
/// Helper to read/write private static fields and invoke private static methods on
/// the licensing classes under test. LicenseManager keeps its validation state in
/// private static fields with no public reset hook, so tests must reset that state
/// themselves to stay independent of execution order.
/// </summary>
internal static class ReflectionHelper
{
    public static void SetStaticField(Type type, string fieldName, object? value)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {type.Name}");
        field.SetValue(null, value);
    }

    public static object? GetStaticField(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {type.Name}");
        return field.GetValue(null);
    }

    public static object? InvokeStaticMethod(Type type, string methodName, object?[] args)
    {
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on {type.Name}");
        return method.Invoke(null, args);
    }
}
