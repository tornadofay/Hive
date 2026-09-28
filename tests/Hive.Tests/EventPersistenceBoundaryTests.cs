using System.Reflection;
using Hive.Coordination;
using Hive.Persistence;
using Xunit;

namespace Hive.Tests;

public sealed class EventPersistenceBoundaryTests
{
    [Fact]
    public void RawEventPersistenceContracts_AreNotPublic()
    {
        var exportedTypes = typeof(HiveEventPersistence)
            .Assembly
            .GetExportedTypes();

        Assert.DoesNotContain(
            exportedTypes,
            static type => type.Name == "IEventPersistenceStore");

        Assert.DoesNotContain(
            exportedTypes,
            static type => type.Name == "SqlEventPersistenceStore");
    }

    [Fact]
    public void EventPersistenceComposition_DoesNotExposeRawPersistencePort()
    {
        var rawPortName = "IEventPersistenceStore";

        var publicMembers = typeof(HiveEventPersistenceComposition)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance);

        Assert.DoesNotContain(
            publicMembers,
            member => member switch
            {
                MethodInfo method =>
                    method.ReturnType.Name == rawPortName ||
                    method.GetParameters().Any(
                        parameter => parameter.ParameterType.Name == rawPortName),
                PropertyInfo property =>
                    property.PropertyType.Name == rawPortName,
                FieldInfo field =>
                    field.FieldType.Name == rawPortName,
                _ => false
            });
    }

    [Fact]
    public void AgentExecutionService_UsesOpaquePersistenceComposition()
    {
        var constructor = Assert.Single(
            typeof(AgentExecutionService).GetConstructors());

        Assert.Contains(
            constructor.GetParameters(),
            static parameter =>
                parameter.ParameterType == typeof(HiveEventPersistenceComposition));

        Assert.DoesNotContain(
            constructor.GetParameters(),
            static parameter =>
                parameter.ParameterType.Name == "IEventPersistenceStore");
    }
}
