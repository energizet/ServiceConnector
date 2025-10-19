namespace ServiceConnector.Common;

public class PipelineStore(PipelineStore? parentStore = null) : Store<object>(parentStore);