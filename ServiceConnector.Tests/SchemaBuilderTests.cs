using ServiceConnector.Jobs;

namespace ServiceConnector.Tests;

public class BaseClass
{
    public int Id { get; set; }
}

public class ExtendedClass
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class AnotherClass
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
}

public class ComplexClass
{
    public int Id { get; set; }
    public BaseClass Nested { get; set; }
}

public class ComplexClassDiff
{
    public int Id { get; set; }
    public ExtendedClass Nested { get; set; } // Nested поле имеет другой тип (расширенный)
}

public class SchemaBuilderTests
{
    // --- Тесты на Примитивы и Unknown ---

    [Fact]
    public void Merge_Unknown_And_Type_Returns_Type_With_Origin()
    {
        // Arrange
        var unknownNode = new SchemaNode { Name = "Test", Type = UniversalType.Unknown };
        var intNode = typeof(int).ConvertToSchema("Test");

        // Act
        var result1 = SchemaBuilder.MergeNodes(unknownNode, intNode);
        var result2 = SchemaBuilder.MergeNodes(intNode, unknownNode);

        // Assert
        Assert.Equal(UniversalType.Number, result1.Type);
        Assert.Equal(typeof(int), result1.ClrType);

        Assert.Equal(UniversalType.Number, result2.Type);
        Assert.Equal(typeof(int), result2.ClrType);
    }

    [Fact]
    public void Merge_Different_Primitives_Returns_String_And_Clears_Origin()
    {
        // Arrange
        var intNode = typeof(int).ConvertToSchema("Prop");
        var boolNode = typeof(bool).ConvertToSchema("Prop");

        // Act
        var result = SchemaBuilder.MergeNodes(intNode, boolNode);

        // Assert
        Assert.Equal(UniversalType.String, result.Type);
        Assert.Null(result.ClrType);
    }

    // --- Тесты на Неизменяемость (Pure Function) ---

    [Fact]
    public void Merge_Is_Pure_Does_Not_Mutate_Inputs()
    {
        // Arrange
        var nodeA = typeof(BaseClass).ConvertToSchema(); // { Id }
        var nodeB = typeof(ExtendedClass).ConvertToSchema(); // { Id, Name }

        // Clone manually to compare later
        var originalCountA = nodeA.Properties.Count;
        var originalCountB = nodeB.Properties.Count;

        // Act
        var result = SchemaBuilder.MergeNodes(nodeA, nodeB);

        // Assert
        // Проверяем, что исходные ноды не изменились
        Assert.Equal(originalCountA, nodeA.Properties.Count);
        Assert.False(nodeA.Properties.ContainsKey("Name")); // В A не должно появиться Name

        Assert.Equal(originalCountB, nodeB.Properties.Count);
        
        // Проверяем результат
        Assert.Equal(2, result.Properties.Count); // Id + Name
        Assert.NotSame(result, nodeA);
        Assert.NotSame(result, nodeB);
    }

    // --- Тесты на Объекты и OriginType (Самое важное) ---

    [Fact]
    public void Merge_Subset_Into_Superset_Returns_Superset_Origin()
    {
        // Сценарий: A = { Id }, B = { Id, Name }
        // Merge(A, B) -> Должно получиться B
        
        // Arrange
        var small = typeof(BaseClass).ConvertToSchema();
        var big = typeof(ExtendedClass).ConvertToSchema();

        // Act
        var result = SchemaBuilder.MergeNodes(small, big);

        // Assert
        Assert.True(result.Properties.ContainsKey("Id"));
        Assert.True(result.Properties.ContainsKey("Name"));
        Assert.Equal(typeof(ExtendedClass), result.ClrType); // Победил "Богатый" тип
    }

    [Fact]
    public void Merge_Superset_Into_Subset_Returns_Superset_Origin()
    {
        // Сценарий: A = { Id, Name }, B = { Id }
        // Merge(A, B) -> Должно остаться A, так как A покрывает B
        
        // Arrange
        var big = typeof(ExtendedClass).ConvertToSchema();
        var small = typeof(BaseClass).ConvertToSchema();

        // Act
        var result = SchemaBuilder.MergeNodes(big, small);

        // Assert
        Assert.Equal(2, result.Properties.Count);
        Assert.Equal(typeof(ExtendedClass), result.ClrType); // Победил "Богатый" тип (он же был первым)
    }

    [Fact]
    public void Merge_Disjoint_Objects_Returns_Hybrid_With_Null_Origin()
    {
        // Сценарий: A = { Id, Name }, B = { Id, IsActive }
        // Результат: { Id, Name, IsActive } -> Такого класса нет, Origin = null
        
        // Arrange
        var typeA = typeof(ExtendedClass).ConvertToSchema();
        var typeB = typeof(AnotherClass).ConvertToSchema();

        // Act
        var result = SchemaBuilder.MergeNodes(typeA, typeB);

        // Assert
        Assert.Equal(3, result.Properties.Count); // Id, Name, IsActive
        Assert.True(result.Properties.ContainsKey("Name"));
        Assert.True(result.Properties.ContainsKey("IsActive"));
        Assert.Null(result.ClrType); // Тип потерян
    }

    [Fact]
    public void Merge_Deep_Nested_Expansion_Changes_Origin()
    {
        // Сценарий: 
        // A = ComplexClass { Nested: BaseClass { Id } }
        // B = ComplexClassDiff { Nested: ExtendedClass { Id, Name } }
        // Результат должен стать ComplexClassDiff, так как вложенность расширилась
        
        // Arrange
        var rootA = typeof(ComplexClass).ConvertToSchema();
        var rootB = typeof(ComplexClassDiff).ConvertToSchema();

        // Act
        var result = SchemaBuilder.MergeNodes(rootA, rootB);

        // Assert
        Assert.NotNull(result.Properties["Nested"]);
        Assert.True(result.Properties["Nested"].Properties.ContainsKey("Name"));
        
        // Проверяем, что корень переключился на тип, содержащий расширенное вложение
        Assert.Equal(typeof(ComplexClassDiff), result.ClrType);
    }

    // --- Тесты на Массивы ---

    [Fact]
    public void Merge_Arrays_Of_Same_Type_Preserves_Origin()
    {
        // Arrange
        var listA = typeof(List<int>).ConvertToSchema();
        var listB = typeof(List<int>).ConvertToSchema();

        // Act
        var result = SchemaBuilder.MergeNodes(listA, listB);

        // Assert
        Assert.Equal(UniversalType.Array, result.Type);
        Assert.Equal(typeof(int), result.ArrayItemSchema.ClrType);
        Assert.Equal(typeof(List<int>), result.ClrType); // Сам список тоже сохранил тип
    }

    [Fact]
    public void Merge_Arrays_Of_Objects_Subset_Superset()
    {
        // List<Small> vs List<Big> -> Должен стать List<Big>
        
        // Arrange
        var listSmall = typeof(List<BaseClass>).ConvertToSchema();
        var listBig = typeof(List<ExtendedClass>).ConvertToSchema();

        // Act
        var result = SchemaBuilder.MergeNodes(listSmall, listBig);

        // Assert
        Assert.Equal(UniversalType.Array, result.Type);
        // Элемент массива должен стать ExtendedClass
        Assert.Equal(typeof(ExtendedClass), result.ArrayItemSchema.ClrType);
        // Сам массив должен стать List<ExtendedClass>
        Assert.Equal(typeof(List<ExtendedClass>), result.ClrType);
    }

    [Fact]
    public void Merge_Arrays_Conflict_Loses_Origin()
    {
        // List<int> vs List<bool> -> List<string> (Origin null)
        
        // Arrange
        var listInt = typeof(List<int>).ConvertToSchema();
        var listBool = typeof(List<bool>).ConvertToSchema();

        // Act
        var result = SchemaBuilder.MergeNodes(listInt, listBool);

        // Assert
        Assert.Equal(UniversalType.Array, result.Type);
        Assert.Equal(UniversalType.String, result.ArrayItemSchema.Type); // Элементы стали строками
        Assert.Null(result.ArrayItemSchema.ClrType);
        Assert.Null(result.ClrType); // Тип списка потерян
    }
    
    // --- Тест Aggregate (InferCommonSchema) ---
    
    [Fact]
    public void InferCommonSchema_Aggregates_Multiple_Types()
    {
        // Arrange: [ {Id}, {Id, Name}, {Id, Name} ] -> Result {Id, Name} (ExtendedClass)
        var list = new List<SchemaNode>
        {
            typeof(BaseClass).ConvertToSchema(),
            typeof(ExtendedClass).ConvertToSchema(),
            typeof(ExtendedClass).ConvertToSchema()
        };

        // Act
        var result = SchemaBuilder.InferCommonSchema(list);

        // Assert
        Assert.Equal(typeof(ExtendedClass), result.ClrType);
        Assert.Equal(2, result.Properties.Count);
    }
    
    [Fact]
    public void Merge_Object_And_Array_Returns_Generic_Object()
    {
        // Arrange
        // Тип A: Класс { Id }
        var objectNode = typeof(BaseClass).ConvertToSchema(); 
        // Тип B: Список [ int ]
        var arrayNode = typeof(List<int>).ConvertToSchema();

        // Act
        var result = SchemaBuilder.MergeNodes(objectNode, arrayNode);

        // Assert
        // 1. Тип становится Object (как общий знаменатель для "сложных" данных)
        Assert.Equal(UniversalType.Object, result.Type);
    
        // 2. Но у него НЕТ свойств, так как мы не можем слить поля со списком
        Assert.Null(result.Properties); 
    
        // 3. И у него НЕТ схемы массива
        Assert.Null(result.ArrayItemSchema);

        // 4. Исходный C# тип потерян, так как ни BaseClass, ни List<int> это не описывают
        Assert.Null(result.ClrType);
    }
}