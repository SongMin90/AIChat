using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;

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
        this.Shown += MainForm_Shown;

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
    }

    private void InitializeComponents()
    {
        this.Size = new Size(800, 600);
        this.Text = "AI Chat";

        chatHistoryTextBox = new RichTextBox
        {
            Location = new Point(12, 12),
            Size = new Size(760, 440),
            ReadOnly = true,
            BackColor = Color.White,
            Text = "聊天记录将在这里显示..."
        };

        inputTextBox = new TextBox
        {
            Location = new Point(12, 470),
            Size = new Size(660, 60),
            Multiline = true,
            Text = "请输入消息..."
        };

        sendButton = new Button
        {
            Location = new Point(682, 470),
            Size = new Size(90, 60),
            Text = "发送"
        };

        sendButton.Click += SendButton_Click;
        inputTextBox.KeyDown += InputTextBox_KeyDown;

        this.Controls.AddRange(new Control[] { 
            chatHistoryTextBox, 
            inputTextBox, 
            sendButton 
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
        AppendMessage(userMessage);

        messages.Add(new ChatMessage("user", userMessage));
        inputTextBox.Text = string.Empty;

        // 创建一个临时列表来存储要发送的消息
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

            groqClient.GetChatCompletion(messagesToSend, (response) =>
            {
                if (response != null)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        chatHistoryTextBox.AppendText(response);
                    });
                }
            });
            chatHistoryTextBox.AppendText("\n\n");
            chatHistoryTextBox.ScrollToCaret();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"发生错误：{string.Concat(ex.Message, ex.StackTrace)}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            sendButton.Enabled = true;
            inputTextBox.Enabled = true;
            inputTextBox.Focus();
        }
    }

    private void AppendMessage(string message)
    {
        if (chatHistoryTextBox.Text == "聊天记录将在这里显示...")
        {
            chatHistoryTextBox.Clear();
        }

        chatHistoryTextBox.SelectionStart = chatHistoryTextBox.TextLength;
        chatHistoryTextBox.SelectionLength = 0;
        chatHistoryTextBox.SelectionAlignment = HorizontalAlignment.Right;
        chatHistoryTextBox.AppendText($"{message}\n\n");
        chatHistoryTextBox.ScrollToCaret();
    }

    private void MainForm_Shown(object sender, EventArgs e)
    {
        inputTextBox.Focus();
    }
} 