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
            return modelResponse.data;
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
        foreach (var m in messages)
        {
            messageList.Add(new { role = m.Role, content = m.Content });
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

        var httpRequest = (HttpWebRequest)WebRequest.Create(BaseUrl);
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