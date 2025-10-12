namespace ServiceConnector.Web.Configs;

public class ServiceConnectorConfig
{
	public string PipelinesPath { get; set; } = "./Pipelines";
	public static string Filter => "*.json";
}