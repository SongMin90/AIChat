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
    private ComboBox modelComboBox;
    private List<Model> models;

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
        InitializeModelComboBox();
    }

    private void InitializeComponents()
    {
        this.Size = new Size(900, 600);
        this.Text = "AI Chat";

        Label modelLabel = new Label
        {
            Location = new Point(8, 22),
            Size = new Size(50, 20),
            Text = "模型：",
            TextAlign = ContentAlignment.MiddleRight
        };

        modelComboBox = new ComboBox
        {
            Location = new Point(65, 22),
            Size = new Size(807, 25),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        chatHistoryTextBox = new RichTextBox
        {
            Location = new Point(12, 50),
            Size = new Size(860, 412),
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
            modelLabel,
            modelComboBox,
            chatHistoryTextBox, 
            inputTextBox, 
            sendButton,
            clearButton 
        });
    }

    private void InitializeModelComboBox()
    {
        try
        {
            models = groqClient.GetModels();

            // models按id排序
            models.Sort(delegate(Model x, Model y)
            {
                return x.id.CompareTo(y.id);
            });
            
            // 清空已有项
            modelComboBox.Items.Clear();
            
            // 只添加active为true的模型
            foreach (Model model in models)
            {
                // 还需要排除id包含whisper的
                if (model.active && !model.id.Contains("whisper"))
                {
                    // 使用owned_by和id组合作为显示文本
                    string displayText = $"[{model.owned_by}]{model.id}";
                    modelComboBox.Items.Add(new ComboBoxItem(displayText, model.id));
                }
            }
            
            // 默认选择llama-3.3-70b-versatile或第一个可用模型
            bool foundDefault = false;
            for (int i = 0; i < modelComboBox.Items.Count; i++)
            {
                ComboBoxItem item = (ComboBoxItem)modelComboBox.Items[i];
                if (item.Value == "llama-3.3-70b-versatile")
                {
                    modelComboBox.SelectedIndex = i;
                    foundDefault = true;
                    break;
                }
            }
            
            if (!foundDefault && modelComboBox.Items.Count > 0)
            {
                modelComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载模型列表失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

        ComboBoxItem selectedItem = modelComboBox.SelectedItem as ComboBoxItem;
        string selectedModel = selectedItem.Value;
        
        // 获取选中模型的context window
        int modelContextWindow = 32768; // 默认值
        foreach (Model model in models)
        {
            if (model.id == selectedModel)
            {
                modelContextWindow = model.context_window;
                break;
            }
        }

        // 创建一个临时messagesToSend表来存储要发送的消息
        List<ChatMessage> messagesToSend = new List<ChatMessage>();
        int totalLength = 0;

        // 从最新的消息开始添加到临时列表，直到总长度不超过context window
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            int messageLength = messages[i].Content.Length;
            if (totalLength + messageLength <= modelContextWindow) // 使用模型的context window
            {
                messagesToSend.Insert(0, messages[i]);
                totalLength += messageLength;
            }
            else
            {
                break;
            }
        }

        Thread thread = new Thread(() =>
        {
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    sendButton.Enabled = false;
                    inputTextBox.Enabled = false;
                });
                
                chatHistoryTextBox.SelectionAlignment = HorizontalAlignment.Left;
                chatHistoryTextBox.SelectionColor = Color.Black;
                
                string responseText = "";
                groqClient.GetChatCompletion(messagesToSend, selectedModel, modelContextWindow, (response) =>
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
                
                this.Invoke((MethodInvoker)delegate
                {
                    chatHistoryTextBox.AppendText("\n\n");
                    chatHistoryTextBox.ScrollToCaret();
                    sendButton.Enabled = true;
                    inputTextBox.Enabled = true;
                    inputTextBox.Focus();
                    this.Text = $"AI Chat - 消耗{GetTotalMessageLength()}个token";
                });
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show($"{string.Concat(ex.Message, "\n", ex.StackTrace)}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    // 恢复发送失败的消息
                    inputTextBox.Text = userMessage;
                    // messages删除最后一个
                    messages.RemoveAt(messages.Count - 1);
                    // 恢复发送按钮和输入框的可用状态
                    sendButton.Enabled = true;
                    inputTextBox.Enabled = true;
                    inputTextBox.Focus();
                });
            }
        });
        thread.Start();
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
        else
        {
            ChatHistoryTextBox_Clear();
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
            request.Timeout = 5000; // 设置���时时间为5秒
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

    // 添加一个用于ComboBox的辅助类
    private class ComboBoxItem
    {
        public string Text { get; set; }
        public string Value { get; set; }

        public ComboBoxItem(string text, string value)
        {
            Text = text;
            Value = value;
        }

        public override string ToString()
        {
            return Text;
        }
    }
} 