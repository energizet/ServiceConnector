namespace ServiceConnector.Jobs;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class PipelineJobAttribute : Attribute
{
	public override string ToString()
	{
		return "{TypeName}";
	}
}