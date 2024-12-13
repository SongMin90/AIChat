using System.Collections.Generic;

public class ModelResponse
{
    public string @object { get; set; }
    public List<Model> data { get; set; }
}

public class Model
{
    public string id { get; set; }
    public string @object { get; set; }
    public long created { get; set; }
    public string owned_by { get; set; }
    public bool active { get; set; }
    public int context_window { get; set; }
    public object public_apps { get; set; }
} 