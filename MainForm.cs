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
using System.Text;

public class MainForm : Form
{
    private TextBox inputTextBox;
    private RichTextBox chatHistoryTextBox;
    private Button sendButton;
    private GroqClient groqClient;
    private List<ChatMessage> messages;
    private ComboBox modelComboBox;
    private List<Model> models;
    private readonly Color PRIMARY_COLOR = Color.FromArgb(51, 122, 183);
    private readonly Color SECONDARY_COLOR = Color.FromArgb(108, 117, 125);
    private readonly Color BACKGROUND_COLOR = Color.FromArgb(248, 249, 250);
    private readonly Color TEXT_COLOR = Color.FromArgb(33, 37, 41);
    private static readonly Color CODE_BLOCK_COLOR = Color.FromArgb(40, 44, 52);
    private static readonly Color CODE_BLOCK_BACK_COLOR = Color.FromArgb(248, 249, 250);
    private Button selectImageButton;
    private string imageBase64;

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
        // 异步设置图标
        SetFormIconAsync();
        CheckUpdateAsync();
        InitializeModelComboBox();
    }

    private void InitializeComponents()
    {
        this.Size = new Size(900, 600);
        this.Text = "AI Chat";
        this.BackColor = BACKGROUND_COLOR;

        Label modelLabel = new Label
        {
            Location = new Point(12, 20),
            Size = new Size(50, 25),
            Text = "模型：",
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            ForeColor = TEXT_COLOR
        };

        modelComboBox = new ComboBox
        {
            Location = new Point(70, 20),
            Size = new Size(807, 25),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        chatHistoryTextBox = new RichTextBox
        {
            Location = new Point(5, 5),
            Size = new Size(855, 392),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            ReadOnly = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            Padding = new Padding(10)
        };

        // 为chatHistoryTextBox添加圆角边框Panel
        Panel chatHistoryPanel = new Panel
        {
            Location = new Point(12, 55),
            Size = new Size(865, 402),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Color.White,
            Padding = new Padding(1),
            BorderStyle = BorderStyle.FixedSingle
        };
        chatHistoryPanel.Controls.Add(chatHistoryTextBox);

        inputTextBox = new TextBox
        {
            Location = new Point(5, 5),
            Size = new Size(650, 50),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Multiline = true,
            Text = "请输入消息...",
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            BorderStyle = BorderStyle.None,
            BackColor = Color.White
        };

        // 为inputTextBox添加圆角边框Panel
        Panel inputPanel = new Panel
        {
            Location = new Point(12, 470),
            Size = new Size(560, 60),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.White,
            Padding = new Padding(1),
            BorderStyle = BorderStyle.FixedSingle
        };
        inputPanel.Controls.Add(inputTextBox);

        sendButton = new Button
        {
            Location = new Point(682, 470),
            Size = new Size(90, 60),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Text = "发送",
            FlatStyle = FlatStyle.Flat,
            BackColor = PRIMARY_COLOR,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            Cursor = Cursors.Hand
        };

        Button clearButton = new Button
        {
            Location = new Point(782, 470),
            Size = new Size(90, 60),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Text = "清空历史",
            FlatStyle = FlatStyle.Flat,
            BackColor = SECONDARY_COLOR,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            Cursor = Cursors.Hand
        };

        selectImageButton = new Button
        {
            Location = new Point(582, 470),
            Size = new Size(90, 60),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Text = "选择图片",
            FlatStyle = FlatStyle.Flat,
            BackColor = PRIMARY_COLOR,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            Cursor = Cursors.Hand,
            Enabled = false
        };

        selectImageButton.Click += SelectImageButton_Click;

        // 移除按钮边框
        sendButton.FlatAppearance.BorderSize = 0;
        clearButton.FlatAppearance.BorderSize = 0;

        clearButton.Click += ClearButton_Click;
        sendButton.Click += SendButton_Click;
        inputTextBox.KeyDown += InputTextBox_KeyDown;

        this.Controls.AddRange(new Control[] { 
            modelLabel,
            modelComboBox,
            chatHistoryPanel, 
            inputPanel, 
            sendButton,
            clearButton,
            selectImageButton
        });

        // 添加鼠标悬停效果
        sendButton.MouseEnter += (s, e) => {
            sendButton.BackColor = Color.FromArgb(40, 98, 146);
        };
        sendButton.MouseLeave += (s, e) => {
            sendButton.BackColor = PRIMARY_COLOR;
        };

        clearButton.MouseEnter += (s, e) => {
            clearButton.BackColor = Color.FromArgb(87, 94, 100);
        };
        clearButton.MouseLeave += (s, e) => {
            clearButton.BackColor = SECONDARY_COLOR;
        };

        // 恢复输入框的提示文本功能
        inputTextBox.ForeColor = Color.Gray;

        inputTextBox.Enter += (s, e) => 
        {
            if (inputTextBox.Text == "请输入消息...")
            {
                inputTextBox.Text = "";
                inputTextBox.ForeColor = Color.Black;
            }
        };

        inputTextBox.Leave += (s, e) => 
        {
            if (string.IsNullOrEmpty(inputTextBox.Text))
            {
                inputTextBox.Text = "请输入消息...";
                inputTextBox.ForeColor = Color.Gray;
            }
        };
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

            modelComboBox.SelectedIndexChanged += (s, e) =>
            {
                // 处理模型选择变化的逻辑
                ComboBoxItem selectedItem = modelComboBox.SelectedItem as ComboBoxItem;
                if (selectedItem != null)
                {
                    // 可以在这里添加对选中模型的处理代码
                    if (selectedItem.Value == "llama-3.2-90b-vision-preview" || selectedItem.Value == "llama-3.2-11b-vision-preview")
                    {
                        selectImageButton.Enabled = true; // 使按钮可点击
                    }
                    else
                    {
                        selectImageButton.Enabled = false; // 使按钮不可点击
                    }
                }
            };
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

        messages.Add(new ChatMessage("user", userMessage, imageBase64));
        inputTextBox.Text = string.Empty;

        ComboBoxItem selectedItem = modelComboBox.SelectedItem as ComboBoxItem;
        string selectedModel = selectedItem.Value;
        
        // 获取选模型的context window
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
                
                using (var tempRichTextBox = new RichTextBox())
                {
                    tempRichTextBox.Rtf = chatHistoryTextBox.Rtf;
                    string responseText = "";
                    groqClient.GetChatCompletion(messagesToSend, selectedModel, modelContextWindow, (response) =>
                    {
                        if (response != null)
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                chatHistoryTextBox.AppendText(response);
                                // 如果response是个换行符，则滚动到末尾
                                if (response.Contains("\n"))
                                {
                                    chatHistoryTextBox.ScrollToCaret();
                                }
                            });
                            responseText += response;
                        }
                    });
                    chatHistoryTextBox.Rtf = tempRichTextBox.Rtf;
                    AppendFormattedText(responseText);
                    messages.Add(new ChatMessage("assistant", responseText, null));
                    SaveMessagesToFile();
                }
                
                this.Invoke((MethodInvoker)delegate
                {
                    chatHistoryTextBox.AppendText("\n\n");
                    chatHistoryTextBox.ScrollToCaret();
                    sendButton.Enabled = true;
                    inputTextBox.Enabled = true;
                    inputTextBox.Focus();
                    imageBase64 = null;
                    selectImageButton.Text = "选择图片";
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
        
        // 确保消息不会超出显示区域
        int maxWidth = chatHistoryTextBox.ClientSize.Width - 20; // 留出一些边距
        using (Graphics g = chatHistoryTextBox.CreateGraphics())
        {
            string wrappedMessage = message;
            SizeF size = g.MeasureString(message, chatHistoryTextBox.Font, maxWidth);
            if (size.Width > maxWidth)
            {
                wrappedMessage = WrapText(message, maxWidth, g, chatHistoryTextBox.Font);
            }
            chatHistoryTextBox.AppendText($"{wrappedMessage}\n\n");
        }
        
        chatHistoryTextBox.ScrollToCaret();
    }

    private string WrapText(string text, float maxWidth, Graphics g, Font font)
    {
        string[] words = text.Split(' ');
        StringBuilder wrappedText = new StringBuilder();
        string line = "";

        foreach (string word in words)
        {
            string testLine = line.Length == 0 ? word : line + " " + word;
            SizeF size = g.MeasureString(testLine, font);

            if (size.Width > maxWidth)
            {
                if (line.Length > 0)
                {
                    wrappedText.AppendLine(line);
                    line = word;
                }
                else
                {
                    wrappedText.AppendLine(word);
                    line = "";
                }
            }
            else
            {
                line = testLine;
            }
        }

        if (line.Length > 0)
        {
            wrappedText.Append(line);
        }

        return wrappedText.ToString();
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
                    //chatHistoryTextBox.AppendText($"{message.Content}\n\n");
                    AppendFormattedText($"{message.Content}\n\n");
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
            string updateUrl = "http://47.236.64.47:8081/SongMin90/AIChat/refs/heads/main/AIChatApp.csproj";

            // 检查更新url是否可以访问
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(updateUrl);
            request.Method = "HEAD";
            request.Timeout = 5000; // 设置时时间为5秒
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                MessageBox.Show($"无法访问更新地址：{updateUrl}", "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // 读取最新版号
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

                // 比较版本号,提��是否更新  
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

    private void AppendFormattedText(string text)
    {
        bool inCodeBlock = false;
        StringBuilder codeBlock = new StringBuilder();
        
        using (StringReader reader = new StringReader(text))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                // 处理代码块
                if (line.StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        // 结束代码块
                        chatHistoryTextBox.SelectionBackColor = CODE_BLOCK_BACK_COLOR;
                        chatHistoryTextBox.SelectionColor = CODE_BLOCK_COLOR;
                        chatHistoryTextBox.AppendText(codeBlock.ToString());
                        chatHistoryTextBox.SelectionBackColor = Color.White;
                        chatHistoryTextBox.SelectionColor = Color.Black;
                        chatHistoryTextBox.AppendText("\n");
                        codeBlock.Length = 0;
                        inCodeBlock = false;
                    }
                    else
                    {
                        // 开始代码块
                        inCodeBlock = true;
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlock.AppendLine(line);
                    continue;
                }

                // 处理标题
                if (line.StartsWith("#"))
                {
                    int level = CountConsecutiveChars(line, '#');
                    string title = line.Substring(level).Trim();
                    float fontSize = 12 - (level - 1);
                    
                    chatHistoryTextBox.SelectionFont = new Font(chatHistoryTextBox.Font.FontFamily, 
                        fontSize, 
                        FontStyle.Bold);
                    chatHistoryTextBox.AppendText(title + "\n");
                    chatHistoryTextBox.SelectionFont = chatHistoryTextBox.Font;
                    continue;
                }

                // 处理粗体
                if (line.Contains("**"))
                {
                    int startIndex = 0;
                    while (true)
                    {
                        int boldStart = line.IndexOf("**", startIndex);
                        if (boldStart == -1) break;
                        
                        int boldEnd = line.IndexOf("**", boldStart + 2);
                        if (boldEnd == -1) break;

                        // 添加粗体前的普通文本
                        chatHistoryTextBox.SelectionFont = chatHistoryTextBox.Font;
                        chatHistoryTextBox.AppendText(line.Substring(startIndex, boldStart - startIndex));

                        // 添加粗体文本
                        chatHistoryTextBox.SelectionFont = new Font(chatHistoryTextBox.Font, FontStyle.Bold);
                        chatHistoryTextBox.AppendText(line.Substring(boldStart + 2, boldEnd - boldStart - 2));

                        startIndex = boldEnd + 2;
                    }

                    // 添加剩余的文本
                    if (startIndex < line.Length)
                    {
                        chatHistoryTextBox.SelectionFont = chatHistoryTextBox.Font;
                        chatHistoryTextBox.AppendText(line.Substring(startIndex));
                    }
                    chatHistoryTextBox.AppendText("\n");
                    continue;
                }

                // 处理列表项
                if (line.TrimStart().StartsWith("- "))
                {
                    string indent = new string(' ', CountConsecutiveChars(line, ' '));
                    chatHistoryTextBox.AppendText(indent + "• " + line.TrimStart().Substring(2) + "\n");
                    continue;
                }

                // 普通文本
                chatHistoryTextBox.AppendText(line + "\n");
            }
        }
    }

    private static int CountConsecutiveChars(string str, char target)
    {
        int count = 0;
        for (int i = 0; i < str.Length; i++)
        {
            if (str[i] != target) break;
            count++;
        }
        return count;
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

    // 异步设置窗体图标
    private void SetFormIconAsync()
    {
        Thread thread = new Thread(() =>
        {
            try
            {
                // 使用 WebRequest 下载图标
                WebRequest request = WebRequest.Create("http://47.236.64.47:8081/SongMin90/AIChat/refs/heads/main/app.ico");
                using (WebResponse response = request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }

                    // 检查流是否有效
                    ms.Seek(0, SeekOrigin.Begin);
                    using (var img = Image.FromStream(ms))
                    {
                        this.Icon = Icon.FromHandle(((Bitmap)img).GetHicon());
                    }
                }
            }
            catch (Exception ex)
            {
                // 处理异常
                MessageBox.Show($"{string.Concat(ex.Message, "\n", ex.StackTrace)}", "图标", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        });
        thread.Start();
    }

    // 添加选择图片的事件处理程序
    private void SelectImageButton_Click(object sender, EventArgs e)
    {
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // 读取图片并转换为Base64
                byte[] imageBytes = File.ReadAllBytes(openFileDialog.FileName);
                string imageType;

                // 使用if-else替代switch表达式
                string extension = Path.GetExtension(openFileDialog.FileName).ToLower();
                if (extension == ".jpg" || extension == ".jpeg")
                {
                    imageType = "image/jpeg";
                }
                else if (extension == ".png")
                {
                    imageType = "image/png";
                }
                else if (extension == ".bmp")
                {
                    imageType = "image/bmp";
                }
                else
                {
                    imageType = "image/png"; // 默认类型
                }

                imageBase64 = $"data:{imageType};base64,{Convert.ToBase64String(imageBytes)}";

                selectImageButton.Text = Path.GetFileName(openFileDialog.FileName); // 只显示文件名
            }
        }
    }
} 