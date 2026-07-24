using System.Reflection;
using DotNetScaffold.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DotNetScaffold.Metadata;

public interface IDbContextModelReader
{
    /// <summary>Loads <paramref name="assemblyPath"/>, locates <paramref name="dbContextTypeName"/> in
    /// it, and reads its EF Core model.</summary>
    DbContextModelMetadata ReadModel(string assemblyPath, string dbContextTypeName);
}

public sealed class EfModelReader : IDbContextModelReader
{
    private readonly IPluginAssemblyLoader _assemblyLoader;

    public EfModelReader(IPluginAssemblyLoader assemblyLoader)
    {
        _assemblyLoader = assemblyLoader;
    }

    public DbContextModelMetadata ReadModel(string assemblyPath, string dbContextTypeName)
    {
        using var plugin = _assemblyLoader.Load(assemblyPath);

        var dbContextType = plugin.Assembly.GetType(dbContextTypeName)
            ?? throw new InvalidOperationException(
                $"Type '{dbContextTypeName}' was not found in '{assemblyPath}'. " +
                "Check dbContextTypeName in .yourtool.json.");

        if (!typeof(DbContext).IsAssignableFrom(dbContextType))
        {
            throw new InvalidOperationException(
                $"'{dbContextTypeName}' does not derive from Microsoft.EntityFrameworkCore.DbContext.");
        }

        using var context = CreateDbContext(dbContextType);
        return BuildMetadata(dbContextTypeName, context.Model);
    }

    /// <summary>
    /// Constructs the target's <see cref="DbContext"/> purely to read its model -- never touches a real
    /// database. <paramref name="dbContextType"/> isn't known until runtime, so the generic
    /// <c>DbContextOptionsBuilder&lt;TContext&gt;</c>/<c>UseInMemoryDatabase&lt;TContext&gt;</c> calls the
    /// scaffolded <c>AppDbContext(DbContextOptions&lt;AppDbContext&gt;)</c> constructor expects have to be
    /// built via reflection instead of ordinary generic code.
    /// </summary>
    private static DbContext CreateDbContext(Type dbContextType)
    {
        var optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);
        var optionsBuilder = Activator.CreateInstance(optionsBuilderType)!;

        // UseInMemoryDatabase<TContext> is itself generic, so the open method must be closed over
        // dbContextType before it can be invoked; disambiguated from other overloads by parameter shape.
        var useInMemoryMethod = typeof(InMemoryDbContextOptionsExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(InMemoryDbContextOptionsExtensions.UseInMemoryDatabase)
                && m.IsGenericMethodDefinition
                && m.GetParameters() is { Length: 3 } parameters
                && parameters[1].ParameterType == typeof(string))
            .MakeGenericMethod(dbContextType);

        useInMemoryMethod.Invoke(null, [optionsBuilder, $"DotNetScaffold-metadata-{Guid.NewGuid():N}", null]);

        // DbContextOptionsBuilder<TContext>.Options hides (via `new`) the base class's non-generic
        // Options property -- both are named "Options", so a plain GetProperty("Options") throws
        // AmbiguousMatchException; DeclaredOnly resolves to the derived (correctly-typed) one.
        var optionsProperty = optionsBuilderType.GetProperty(
            "Options", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        var options = optionsProperty.GetValue(optionsBuilder)!;

        try
        {
            return (DbContext)Activator.CreateInstance(dbContextType, options)!;
        }
        catch (MissingMethodException ex)
        {
            throw new InvalidOperationException(
                $"'{dbContextType.Name}' must have a public constructor accepting " +
                $"DbContextOptions<{dbContextType.Name}> (the shape 'dnscaffold new' scaffolds by default).",
                ex);
        }
    }

    private static DbContextModelMetadata BuildMetadata(string dbContextTypeName, IModel model)
    {
        var entities = new List<EntityMetadata>();
        var skipped = new List<ManyToManySkip>();

        // HasSharedClrType filters out EF Core's implicitly-created many-to-many join entity (a
        // Dictionary<string, object>-typed "shared-type entity" with no real declared CLR class) --
        // confirmed via a probe, not assumed, since GetEntityTypes() otherwise includes it.
        foreach (var entityType in model.GetEntityTypes().Where(e => !e.HasSharedClrType))
        {
            var properties = entityType.GetProperties()
                .Select(property => new PropertyMetadata(
                    property.Name,
                    FormatClrTypeName(property.ClrType),
                    property.IsNullable,
                    property.GetMaxLength(),
                    property.IsPrimaryKey(),
                    property.IsForeignKey()))
                .ToList();

            var navigations = entityType.GetNavigations()
                .Select(navigation => new NavigationMetadata(
                    navigation.Name,
                    navigation.IsCollection,
                    navigation.TargetEntityType.ClrType.Name,
                    // A unique FK is what makes a relationship one-to-one in EF Core's model, regardless
                    // of which side's navigation this is -- confirmed via a probe against a Fluent-API
                    // configured 1:1 (Post<->PostDetail) and several conventional 1:N relationships.
                    navigation.ForeignKey.IsUnique ? RelationshipKind.OneToOne : RelationshipKind.OneToMany,
                    entityType.ClrType == navigation.TargetEntityType.ClrType,
                    navigation.ForeignKey.IsRequired,
                    navigation.IsOnDependent ? navigation.ForeignKey.Properties[0].Name : null))
                .ToList();

            entities.Add(new EntityMetadata(
                entityType.ClrType.Name,
                entityType.ClrType.FullName ?? entityType.ClrType.Name,
                entityType.ClrType.Namespace ?? string.Empty,
                properties,
                navigations));

            foreach (var skipNavigation in entityType.GetSkipNavigations())
            {
                skipped.Add(new ManyToManySkip(
                    entityType.ClrType.Name, skipNavigation.Name, skipNavigation.TargetEntityType.ClrType.Name));
            }
        }

        return new DbContextModelMetadata(dbContextTypeName, entities, skipped);
    }

    private static string FormatClrTypeName(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType switch
        {
            _ when underlyingType == typeof(string) => "string",
            _ when underlyingType == typeof(int) => "int",
            _ when underlyingType == typeof(long) => "long",
            _ when underlyingType == typeof(short) => "short",
            _ when underlyingType == typeof(byte) => "byte",
            _ when underlyingType == typeof(bool) => "bool",
            _ when underlyingType == typeof(decimal) => "decimal",
            _ when underlyingType == typeof(double) => "double",
            _ when underlyingType == typeof(float) => "float",
            _ when underlyingType == typeof(Guid) => "Guid",
            _ when underlyingType == typeof(DateTime) => "DateTime",
            _ when underlyingType == typeof(DateTimeOffset) => "DateTimeOffset",
            _ when underlyingType == typeof(DateOnly) => "DateOnly",
            _ when underlyingType == typeof(TimeOnly) => "TimeOnly",
            _ when underlyingType == typeof(TimeSpan) => "TimeSpan",
            _ => underlyingType.Name,
        };
    }
}
