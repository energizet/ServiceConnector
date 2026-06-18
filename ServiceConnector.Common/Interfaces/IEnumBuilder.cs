namespace ServiceConnector.Common.Interfaces;

public interface IEnumBuilder : ITypeBuilder
{
    string BaseType { get; }

    IEnumBuilder CreateElement(string name, int value);
}
