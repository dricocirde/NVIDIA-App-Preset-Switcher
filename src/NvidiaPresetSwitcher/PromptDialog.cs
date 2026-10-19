using System.Drawing;
using System.Windows.Forms;

namespace NvidiaPresetSwitcher
{
    internal sealed class PromptDialog : Form
    {
        private readonly TextBox _text;

        public PromptDialog(string title, string label, string initial)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(390, 132);
            Font = new Font("Microsoft YaHei UI", 9F);

            Label caption = new Label { Text = label, AutoSize = true, Location = new Point(18, 16) };
            _text = new TextBox { Text = initial ?? "", Location = new Point(20, 42), Width = 350 };
            Button ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(214, 87), Width = 75 };
            Button cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(295, 87), Width = 75 };
            Controls.Add(caption);
            Controls.Add(_text);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            Shown += delegate { _text.Focus(); _text.SelectAll(); };
        }

        public string Value { get { return _text.Text.Trim(); } }
    }
}
