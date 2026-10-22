using System;
using System.Drawing;
using System.Windows.Forms;

namespace NoOccupy.Localization
{
    internal enum UiDialogKind
    {
        Information,
        Warning,
        Error,
        Question
    }

    /// <summary>
    /// 轻量的本地化消息框。系统 MessageBox 的按钮文字跟随 Windows 显示语言，
    /// 不会随软件内语言切换，因此这里用 WinForms 自绘按钮。
    /// </summary>
    internal static class UiDialog
    {
        internal static void Info(IWin32Window owner, string title, string message, string okText)
        {
            Show(owner, title, message, UiDialogKind.Information, okText);
        }

        internal static void Warning(IWin32Window owner, string title, string message, string okText)
        {
            Show(owner, title, message, UiDialogKind.Warning, okText);
        }

        internal static void Error(IWin32Window owner, string title, string message, string okText)
        {
            Show(owner, title, message, UiDialogKind.Error, okText);
        }

        internal static bool Confirm(
            IWin32Window owner,
            string title,
            string message,
            string yesText,
            string noText,
            bool defaultYes)
        {
            using (var form = new LocalizedDialogForm(
                title,
                message,
                UiDialogKind.Question,
                yesText,
                noText,
                defaultYes))
            {
                form.StartPosition = owner == null
                    ? FormStartPosition.CenterScreen
                    : FormStartPosition.CenterParent;

                DialogResult result = owner == null ? form.ShowDialog() : form.ShowDialog(owner);
                return result == DialogResult.Yes;
            }
        }

        private static void Show(
            IWin32Window owner,
            string title,
            string message,
            UiDialogKind kind,
            string okText)
        {
            using (var form = new LocalizedDialogForm(
                title,
                message,
                kind,
                okText,
                null,
                true))
            {
                form.StartPosition = owner == null
                    ? FormStartPosition.CenterScreen
                    : FormStartPosition.CenterParent;

                if (owner == null)
                {
                    form.ShowDialog();
                }
                else
                {
                    form.ShowDialog(owner);
                }
            }
        }
    }

    internal sealed class LocalizedDialogForm : Form
    {
        internal LocalizedDialogForm(
            string title,
            string message,
            UiDialogKind kind,
            string yesText,
            string noText,
            bool defaultYes)
        {
            Text = title ?? string.Empty;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            BackColor = Color.White;
            Padding = new Padding(16);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MinimumSize = new Size(360, 0);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var icon = new PictureBox
            {
                Image = GetIcon(kind).ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(32, 32),
                Margin = new Padding(0, 2, 10, 0),
                TabStop = false
            };

            var label = new Label
            {
                Text = message ?? string.Empty,
                AutoSize = true,
                MaximumSize = new Size(390, 0),
                ForeColor = Color.FromArgb(31, 41, 55),
                Margin = new Padding(0, 0, 0, 16),
                UseMnemonic = false
            };

            root.Controls.Add(icon, 0, 0);
            root.Controls.Add(label, 1, 0);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };

            if (string.IsNullOrEmpty(noText))
            {
                Button ok = CreateButton(yesText, DialogResult.OK);
                ok.Margin = new Padding(0);
                buttons.Controls.Add(ok);
                AcceptButton = ok;
                CancelButton = ok;
            }
            else
            {
                Button no = CreateButton(noText, DialogResult.No);
                no.Margin = new Padding(8, 0, 0, 0);
                Button yes = CreateButton(yesText, DialogResult.Yes);
                yes.Margin = new Padding(0);

                buttons.Controls.Add(no);
                buttons.Controls.Add(yes);

                AcceptButton = defaultYes ? yes : no;
                CancelButton = no;
            }

            root.Controls.Add(buttons, 0, 1);
            root.SetColumnSpan(buttons, 2);
            Controls.Add(root);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            try
            {
                Rectangle area = Owner == null
                    ? Screen.FromControl(this).WorkingArea
                    : Owner.Bounds;

                int x = area.Left + (area.Width - Width) / 2;
                int y = area.Top + (area.Height - Height) / 2;

                Rectangle screen = Screen.FromControl(this).WorkingArea;
                x = Math.Max(screen.Left + 8, Math.Min(x, screen.Right - Width - 8));
                y = Math.Max(screen.Top + 8, Math.Min(y, screen.Bottom - Height - 8));
                Location = new Point(x, y);
            }
            catch
            {
                // 居中失败时保留系统默认位置。
            }
        }

        private static Button CreateButton(string text, DialogResult result)
        {
            return new Button
            {
                Text = text ?? string.Empty,
                DialogResult = result,
                AutoSize = false,
                Size = new Size(88, 30),
                UseVisualStyleBackColor = true
            };
        }

        private static Icon GetIcon(UiDialogKind kind)
        {
            switch (kind)
            {
                case UiDialogKind.Warning:
                    return SystemIcons.Warning;
                case UiDialogKind.Error:
                    return SystemIcons.Error;
                case UiDialogKind.Question:
                    return SystemIcons.Question;
                default:
                    return SystemIcons.Information;
            }
        }
    }
}
