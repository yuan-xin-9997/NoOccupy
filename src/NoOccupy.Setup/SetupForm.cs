using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NoOccupy.Setup
{
    internal sealed class SetupForm : Form
    {
        private readonly SetupOptions _options;
        private TextBox _pathBox;
        private Button _browseButton;
        private CheckBox _desktopCheck;
        private Button _installButton;
        private Button _cancelButton;
        private Label _statusLabel;
        private ProgressBar _progress;
        private bool _busy;

        public int ExitCode { get; private set; }

        public SetupForm(SetupOptions options)
        {
            _options = options ?? new SetupOptions();

            Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel);
            AutoScaleMode = AutoScaleMode.None;
            Text = "解除占用 安装程序";
            ClientSize = new Size(760, 520);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(246, 247, 249);
            DoubleBuffered = true;

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                Icon = SystemIcons.Shield;
            }

            BuildUi();

            float scale = GetSystemDpiScale();
            if (scale > 1.001F || scale < 0.999F)
            {
                Scale(new SizeF(scale, scale));
                ScaleFonts(this, scale);
            }

            _pathBox.Text = string.IsNullOrWhiteSpace(_options.InstallDir)
                ? InstallerEngine.DefaultInstallDir
                : _options.InstallDir;
            _desktopCheck.Checked = _options.CreateDesktop;
            _pathBox.Select(_pathBox.Text.Length, 0);
        }

        private static void ScaleFonts(Control root, float scale)
        {
            if (root == null || scale <= 0F)
            {
                return;
            }

            var controls = new System.Collections.Generic.List<Control>();
            controls.Add(root);
            controls.AddRange(AllControls(root));

            var fonts = new System.Collections.Generic.List<Font>(controls.Count);
            foreach (Control control in controls)
            {
                fonts.Add(control.Font);
            }

            for (int i = 0; i < controls.Count; i++)
            {
                Font font = fonts[i];
                if (font == null || font.Size <= 0F)
                {
                    continue;
                }

                controls[i].Font = new Font(
                    font.FontFamily,
                    font.Size * scale,
                    font.Style,
                    GraphicsUnit.Pixel);
            }
        }

        private static System.Collections.Generic.IEnumerable<Control> AllControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control nested in AllControls(child))
                {
                    yield return nested;
                }
            }
        }

        private void BuildUi()
        {
            SuspendLayout();

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 140,
                BackColor = Color.White
            };
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(229, 231, 235)))
                {
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
                }
            };

            PictureBox iconBox = new PictureBox
            {
                Location = new Point(28, 34),
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                {
                    if (icon != null)
                    {
                        using (Icon large = new Icon(icon, 64, 64))
                        {
                            iconBox.Image = large.ToBitmap();
                        }
                    }
                }
            }
            catch
            {
            }

            Label title = new Label
            {
                Text = "解除占用",
                Font = new Font("Segoe UI", 30F, FontStyle.Bold, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(17, 24, 39),
                AutoSize = true,
                Location = new Point(108, 26),
                BackColor = Color.Transparent
            };

            Label subtitle = new Label
            {
                Text = "一键检测并结束正在占用文件或文件夹的进程",
                Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(107, 114, 128),
                AutoSize = true,
                Location = new Point(111, 72),
                BackColor = Color.Transparent
            };

            Label version = new Label
            {
                Text = "安装程序  v1.0.0",
                Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(156, 163, 175),
                AutoSize = true,
                Location = new Point(112, 98),
                BackColor = Color.Transparent
            };

            header.Controls.Add(iconBox);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(version);

            Panel body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(246, 247, 249),
                Padding = new Padding(28, 24, 28, 0)
            };

            Label pathLabel = new Label
            {
                Text = "安装目录",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Location = new Point(30, 26)
            };

            _pathBox = new TextBox
            {
                Location = new Point(30, 54),
                Size = new Size(626, 32),
                Font = new Font("Segoe UI", 13F, FontStyle.Regular, GraphicsUnit.Pixel),
                BorderStyle = BorderStyle.FixedSingle
            };

            _browseButton = CreateSecondaryButton("浏览...");
            _browseButton.Location = new Point(666, 52);
            _browseButton.Size = new Size(96, 34);
            _browseButton.Click += OnBrowse;

            _desktopCheck = new CheckBox
            {
                Text = "创建桌面快捷方式",
                Font = new Font("Segoe UI", 13F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Location = new Point(30, 104),
                BackColor = Color.Transparent
            };

            Label note = new Label
            {
                Text = "安装后将添加到文件、文件夹的右键菜单中。\r\nWindows 11 若未直接显示，请点击右键菜单底部的“显示更多选项”。",
                Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(107, 114, 128),
                AutoSize = true,
                MaximumSize = new Size(704, 0),
                Location = new Point(30, 144)
            };

            Panel card = new Panel
            {
                Location = new Point(28, 196),
                Size = new Size(704, 88),
                BackColor = Color.White
            };
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(229, 231, 235)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
            };

            Label cardTitle = new Label
            {
                Text = "将安装以下内容",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Location = new Point(16, 8)
            };

            Label cardBody = new Label
            {
                Text = "•  主程序与卸载程序（约 0.3 MB）\r\n•  文件 / 文件夹右键菜单“解除占用”\r\n•  开始菜单快捷方式与系统卸载信息",
                Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(75, 85, 99),
                AutoSize = false,
                Size = new Size(672, 52),
                Location = new Point(16, 30)
            };

            card.Controls.Add(cardTitle);
            card.Controls.Add(cardBody);

            body.Controls.Add(pathLabel);
            body.Controls.Add(_pathBox);
            body.Controls.Add(_browseButton);
            body.Controls.Add(_desktopCheck);
            body.Controls.Add(note);
            body.Controls.Add(card);

            Panel footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 92,
                BackColor = Color.White
            };
            footer.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(229, 231, 235)))
                {
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
                }
            };

            _progress = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 22,
                Location = new Point(0, 0),
                Size = new Size(footer.Width, 4),
                Visible = false
            };

            _statusLabel = new Label
            {
                Text = "准备安装",
                Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(107, 114, 128),
                AutoSize = true,
                Location = new Point(30, 38),
                BackColor = Color.Transparent
            };

            _cancelButton = CreateSecondaryButton("取消");
            _cancelButton.Size = new Size(88, 38);
            _cancelButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _cancelButton.Location = new Point(footer.Width - 30 - 88, 28);
            _cancelButton.Click += OnCancel;

            _installButton = new Button
            {
                Text = "安装",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Pixel),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                UseVisualStyleBackColor = false,
                Size = new Size(142, 38),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(footer.Width - 30 - 88 - 12 - 142, 28),
                Cursor = Cursors.Hand
            };
            _installButton.FlatAppearance.BorderSize = 0;
            _installButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            _installButton.Click += OnInstall;
            AcceptButton = _installButton;

            footer.Controls.Add(_progress);
            footer.Controls.Add(_statusLabel);
            footer.Controls.Add(_cancelButton);
            footer.Controls.Add(_installButton);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);

            ResumeLayout(true);
        }

        private static Button CreateSecondaryButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(31, 41, 55),
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(243, 244, 246);
            return button;
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            if (_busy)
            {
                return;
            }

            try
            {
                using (var dialog = new FolderBrowserDialog
                {
                    Description = "选择解除占用的安装目录",
                    ShowNewFolderButton = true,
                    SelectedPath = _pathBox.Text
                })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _pathBox.Text = dialog.SelectedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("无法选择目录", ex.Message);
            }
        }

        private async void OnInstall(object sender, EventArgs e)
        {
            if (_busy)
            {
                return;
            }

            string dir;
            try
            {
                dir = InstallerEngine.NormalizeDir(_pathBox.Text);
            }
            catch (Exception ex)
            {
                ShowError("安装目录无效", ex.Message);
                return;
            }

            _busy = true;
            SetBusy(true);
            _statusLabel.ForeColor = Color.FromArgb(37, 99, 235);
            _statusLabel.Text = "正在安装，请稍候...";

            try
            {
                bool desktop = _desktopCheck.Checked;
                await Task.Run(() => InstallerEngine.Install(dir, desktop));

                _progress.Visible = false;
                _statusLabel.ForeColor = Color.FromArgb(22, 163, 74);
                _statusLabel.Text = "安装完成";

                MessageBox.Show(
                    this,
                    "“解除占用”已安装成功。\r\n\r\n" +
                    "安装目录：" + dir + "\r\n\r\n" +
                    "现在可以右键点击文件或文件夹，选择“解除占用”。\r\n" +
                    "Windows 11 请在“显示更多选项”中查看。",
                    "安装成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                ExitCode = 0;
                _busy = false;
                Close();
            }
            catch (Exception ex)
            {
                InstallerEngine.Log("界面安装失败：" + ex);
                _progress.Visible = false;
                _statusLabel.ForeColor = Color.FromArgb(185, 28, 28);
                _statusLabel.Text = "安装失败";
                ExitCode = 1;
                ShowError("安装失败", ex.Message + "\r\n\r\n日志：" + InstallerEngine.LogPath);
            }
            finally
            {
                _busy = false;
                if (!IsDisposed)
                {
                    SetBusy(false);
                }
            }
        }

        private void OnCancel(object sender, EventArgs e)
        {
            if (_busy)
            {
                return;
            }

            Close();
        }

        private void SetBusy(bool busy)
        {
            _pathBox.Enabled = !busy;
            _browseButton.Enabled = !busy;
            _desktopCheck.Enabled = !busy;
            _installButton.Enabled = !busy;
            _cancelButton.Enabled = !busy;
            _progress.Visible = busy;
            _installButton.BackColor = busy
                ? Color.FromArgb(191, 219, 254)
                : Color.FromArgb(37, 99, 235);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_busy)
            {
                e.Cancel = true;
                return;
            }

            base.OnFormClosing(e);
        }

        private void ShowError(string title, string message)
        {
            if (IsDisposed)
            {
                return;
            }

            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        private static float GetSystemDpiScale()
        {
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi >= 72 && dpi <= 480)
                {
                    return dpi / 96F;
                }
            }
            catch
            {
            }

            try
            {
                using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    if (graphics.DpiX >= 72F && graphics.DpiX <= 480F)
                    {
                        return graphics.DpiX / 96F;
                    }
                }
            }
            catch
            {
            }

            return 1F;
        }
    }
}
