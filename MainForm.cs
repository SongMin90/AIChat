using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using System.Net;
using System.Threading;
using System.Reflection;
using System.Xml;

public class MainForm : Form
{
    private TextBox inputTextBox;
    private RichTextBox chatHistoryTextBox;
    private Button sendButton;
    private GroqClient groqClient;
    private List<ChatMessage> messages;

    public MainForm()
    {
        InitializeComponents();
        groqClient = new GroqClient("你的API密钥");
        messages = new List<ChatMessage>();

        inputTextBox.Enter += (s, e) => 
        {
            if (inputTextBox.Text == "请输入消息...")
            {
                inputTextBox.Text = "";
            }
        };

        inputTextBox.Leave += (s, e) => 
        {
            if (string.IsNullOrEmpty(inputTextBox.Text))
            {
                inputTextBox.Text = "请输入消息...";
            }
        };

        LoadMessagesFromFile();
        CheckUpdateAsync();
    }

    private void InitializeComponents()
    {
        this.Size = new Size(900, 600);
        this.Text = "AI Chat";

        chatHistoryTextBox = new RichTextBox
        {
            Location = new Point(12, 12),
            Size = new Size(860, 440),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            ReadOnly = true,
            BackColor = Color.White
        };

        inputTextBox = new TextBox
        {
            Location = new Point(12, 470),
            Size = new Size(660, 60),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Multiline = true,
            Text = "请输入消息..."
        };

        sendButton = new Button
        {
            Location = new Point(682, 470),
            Size = new Size(90, 60),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Text = "发送"
        };

        Button clearButton = new Button
        {
            Location = new Point(782, 470),
            Size = new Size(90, 60),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Text = "清空历史"
        };

        clearButton.Click += ClearButton_Click;

        sendButton.Click += SendButton_Click;
        inputTextBox.KeyDown += InputTextBox_KeyDown;

        this.Controls.AddRange(new Control[] { 
            chatHistoryTextBox, 
            inputTextBox, 
            sendButton,
            clearButton 
        });
    }

    private void SendButton_Click(object sender, EventArgs e)
    {
        SendMessage();
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            SendMessage();
        }
    }

    private void SendMessage()
    {
        if (string.IsNullOrEmpty(inputTextBox.Text) || inputTextBox.Text.Trim().Length == 0)
            return;

        string userMessage = inputTextBox.Text.Trim();
        if (userMessage == "请输入消息...")
        {
            return;
        }
        AppendMessage(userMessage);

        messages.Add(new ChatMessage("user", userMessage));
        inputTextBox.Text = string.Empty;

        // 创建一个临时messagesToSend表来存储要发送的消息
        List<ChatMessage> messagesToSend = new List<ChatMessage>();
        int totalLength = 0;

        // 从最新的消息开始添加到临时列表，直到总长度不超过32k
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            int messageLength = messages[i].Content.Length;
            if (totalLength + messageLength <= 32 * 1024) // 32k
            {
                messagesToSend.Insert(0, messages[i]); // 插入到列表的开头
                totalLength += messageLength;
            }
            else
            {
                break;
            }
        }

        try
        {
            sendButton.Enabled = false;
            inputTextBox.Enabled = false;
            chatHistoryTextBox.SelectionAlignment = HorizontalAlignment.Left;
            chatHistoryTextBox.SelectionColor = Color.Black;
            
            string responseText = "";
            groqClient.GetChatCompletion(messagesToSend, (response) =>
            {
                if (response != null)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        chatHistoryTextBox.AppendText(response);
                        responseText += response;
                    });
                }
            });

            messages.Add(new ChatMessage("assistant", responseText));
            SaveMessagesToFile();
            chatHistoryTextBox.AppendText("\n\n");
            chatHistoryTextBox.ScrollToCaret();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{string.Concat(ex.Message, "\n", ex.StackTrace)}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            sendButton.Enabled = true;
            inputTextBox.Enabled = true;
            inputTextBox.Focus();
        }

        this.Text = $"AI Chat - 消耗{GetTotalMessageLength()}个token";
    }

    private void AppendMessage(string message)
    {
        if (chatHistoryTextBox.Text == "聊天记录将在这里显示...")
        {
            chatHistoryTextBox.Clear();
        }

        chatHistoryTextBox.SelectionStart = chatHistoryTextBox.TextLength;
        chatHistoryTextBox.SelectionLength = 0;
        chatHistoryTextBox.SelectionColor = Color.Blue;
        chatHistoryTextBox.SelectionAlignment = HorizontalAlignment.Right;
        chatHistoryTextBox.AppendText($"{message}\n\n");
        chatHistoryTextBox.ScrollToCaret();
    }

    private int GetTotalMessageLength()
    {
        int totalLength = 0;
        foreach (ChatMessage message in messages)
        {
            totalLength += message.Content.Length;
        }
        return totalLength;
    }

    private void SaveMessagesToFile()
    {
        string filePath = "messages.json";
        var json = JsonConvert.SerializeObject(messages, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    private void LoadMessagesFromFile()
    {
        string filePath = "messages.json";
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            messages = JsonConvert.DeserializeObject<List<ChatMessage>>(json);

            if (messages.Count > 0)
            {
                chatHistoryTextBox.Clear();
                // 将加载的消息存入聊天记录文本框
                foreach (var message in messages)
                {
                    if (message.Role == "user")
                    {
                        chatHistoryTextBox.SelectionAlignment = HorizontalAlignment.Right;
                        chatHistoryTextBox.SelectionColor = Color.Blue;
                    }
                    else
                    {
                        chatHistoryTextBox.SelectionAlignment = HorizontalAlignment.Left;
                        chatHistoryTextBox.SelectionColor = Color.Black;
                    }
                    chatHistoryTextBox.AppendText($"{message.Content}\n\n");
                }
            } 
            else
            {
                // 清空聊天记录文本框
                ChatHistoryTextBox_Clear();
            }
            
            // 滚动到聊天记录本框的末尾
            chatHistoryTextBox.ScrollToCaret();
        }
    }

    private void ClearButton_Click(object sender, EventArgs e)
    {
        messages.Clear();
        SaveMessagesToFile();
        ChatHistoryTextBox_Clear();
    }

    private void ChatHistoryTextBox_Clear()
    {
        chatHistoryTextBox.Clear();
        chatHistoryTextBox.SelectionAlignment = HorizontalAlignment.Center;
        chatHistoryTextBox.SelectionColor = Color.Black;
        chatHistoryTextBox.Text = "聊天记录将在这里显示...";
    }

    private void CheckUpdateAsync()
    {
        // 使用 Thread 类代替 Task
        Thread thread = new Thread(CheckUpdate);
        thread.Start();
    }

    private void CheckUpdate()
    {
        try
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            string updateUrl = "https://raw.githubusercontent.com/SongMin90/AIChat/refs/heads/main/AIChatApp.csproj";

            // 检查更新url是否可以访问
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(updateUrl);
            request.Method = "HEAD";
            request.Timeout = 5000; // 设置超时时间为5秒
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                MessageBox.Show($"无法访问更新地址：{updateUrl}", "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // 读取最新版本号
            using (var client = new WebClient())
            {
                string configXml = client.DownloadString(updateUrl);

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(configXml);
                XmlNode assemblyVersionNode = xmlDoc.SelectSingleNode("//AssemblyVersion");
                string version_net = assemblyVersionNode.InnerText;

                // 将版本号字符串转换为Version对象进行比较
                Assembly assembly = Assembly.GetExecutingAssembly();
                Version localVersion = assembly.GetName().Version;
                Version remoteVersion = new Version(version_net);

                // 比较版本号,提示是否更新  
                if (remoteVersion > localVersion)
                {
                    if (MessageBox.Show("发现新版本,是否更新?", "更新提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start($"https://github.com/SongMin90/AIChat/releases/tag/{version_net}");
                        Application.Exit();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{string.Concat(ex.Message, "\n", ex.StackTrace)}", "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
} 