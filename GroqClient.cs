using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using System.Windows.Forms;
using System.Text;

public class GroqClient
{
    private readonly string _apiKey;
    private const string _host = "http://47.236.64.47:8080";
    private const string BaseUrl = _host + "/api/openai/v1/chat/completions";
    private const string ModelsUrl = _host + "/api/openai/v1/models";

    public GroqClient(string apiKey)
    {
        _apiKey = apiKey;
    }

    public class ChatCompletionResult
    {
        public List<Choice> choices { get; set; }
    }

    public class Choice
    {
        public Message delta { get; set; }
    }

    public class Message
    {
        public string content { get; set; }
    }

    public List<Model> GetModels()
    {
        var httpRequest = (HttpWebRequest)WebRequest.Create(ModelsUrl);
        httpRequest.Method = "GET";

        using (var response = (HttpWebResponse)httpRequest.GetResponse())
        using (var streamReader = new StreamReader(response.GetResponseStream()))
        {
            var result = streamReader.ReadToEnd();
            var modelResponse = JsonConvert.DeserializeObject<ModelResponse>(result);
            var models = modelResponse.data;
            models.Add(new Model
            {
                id = "DeepSeek-V2.5",
                @object = "model",
                owned_by = "DeepSeek",
                active = true,
                context_window = 8192
            });
            return models;
        }
    }

    public void GetChatCompletion(List<ChatMessage> messages, string model, int modelContextWindow, Action<string> onMessageReceived)
    {
        // ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        // ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Ssl3;
        ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

        ServicePointManager.DefaultConnectionLimit = 10;
        ServicePointManager.Expect100Continue = false;

        var messageList = new List<object>();
        // messageList.Add(new { role = "system", content = "用中文回复我" });
        foreach (var m in messages)
        {
            if (!string.IsNullOrEmpty(m.ImageBase64) && (model == "llama-3.2-90b-vision-preview" || model == "llama-3.2-11b-vision-preview")) 
            {
                messageList.Add(new { role = m.Role, content = new object[] { new { type = "text", text = m.Content }, new { type = "image_url", image_url = new { url = m.ImageBase64 } } } });
            }
            else
            {
                messageList.Add(new { role = m.Role, content = m.Content });
            }
        }
        var request = new
        {
            model = model,
            messages = messageList,
            temperature = 1,
            max_tokens = modelContextWindow,
            top_p = 1,
            stream = true
        };

        //File.WriteAllText("messages1.json", JsonConvert.SerializeObject(request, Newtonsoft.Json.Formatting.Indented));
        string reqUrl = BaseUrl;
        if (model == "DeepSeek-V2.5")
        {
            reqUrl = "http://47.236.64.47:8007/chat/completions";
        }
        var httpRequest = (HttpWebRequest)WebRequest.Create(reqUrl);
        httpRequest.Method = "POST";
        httpRequest.ContentType = "application/json";
        // httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");

        using (var streamWriter = new StreamWriter(httpRequest.GetRequestStream()))
        {
            var json = JsonConvert.SerializeObject(request);
            streamWriter.Write(json);
        }

        using (var response = (HttpWebResponse)httpRequest.GetResponse())
        using (var streamReader = new StreamReader(response.GetResponseStream()))
        {
            string line;
            while ((line = streamReader.ReadLine()) != null)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    if (line.StartsWith("data:"))
                    {
                        line = line.Substring(5);
                    }
                    if (line.Trim() == "[DONE]")
                    {
                        break;
                    }
                    var result = JsonConvert.DeserializeObject<ChatCompletionResult>(line);
                    if (result.choices.Count > 0)
                    {
                        var content = result.choices[0].delta.content;
                        onMessageReceived(content);
                    }
                }
            }
        }
    }
} 