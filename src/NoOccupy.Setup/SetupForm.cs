using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using NoOccupy.Localization;

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
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _versionLabel;
        private Label _pathLabel;
        private Label _noteLabel;
        private Label _cardTitle;
        private Label _cardBody;
        private Label _languageLabel;
        private ComboBox _languageCombo;
        private ProgressBar _progress;
        private bool _busy;
        private bool _suppressLanguageChange;
        private string _statusKey = "准备安装";

        public int ExitCode { get; private set; }

        public SetupForm(SetupOptions options)
        {
            _options = options ?? new SetupOptions();

            Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel);
            AutoScaleMode = AutoScaleMode.None;
            Text = S.T("解除占用 安装程序");
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

            _titleLabel = new Label
            {
                Text = S.T("解除占用"),
                Font = new Font("Segoe UI", 30F, FontStyle.Bold, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(17, 24, 39),
                AutoSize = true,
                Location = new Point(108, 26),
                BackColor = Color.Transparent
            };

            _subtitleLabel = new Label
            {
                Text = S.T("一键检测并结束正在占用文件或文件夹的进程"),
                Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(107, 114, 128),
                AutoSize = true,
                Location = new Point(111, 72),
                BackColor = Color.Transparent
            };

            _versionLabel = new Label
            {
                Text = S.T("安装程序  v{0}", InstallerEngine.ProductVersion),
                Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(156, 163, 175),
                AutoSize = true,
                Location = new Point(112, 98),
                BackColor = Color.Transparent
            };

            _languageLabel = new Label
            {
                Text = S.T("界面语言"),
                Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(107, 114, 128),
                AutoSize = true,
                Location = new Point(600, 14),
                BackColor = Color.Transparent
            };

            _languageCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel),
                Location = new Point(600, 36),
                Size = new Size(130, 30),
                TabStop = true
            };
            _languageCombo.Items.AddRange(S.NativeNames);
            _languageCombo.SelectedIndex = (int)S.Language;
            _languageCombo.SelectionChangeCommitted += OnLanguageChanged;

            header.Controls.Add(iconBox);
            header.Controls.Add(_titleLabel);
            header.Controls.Add(_subtitleLabel);
            header.Controls.Add(_versionLabel);
            header.Controls.Add(_languageLabel);
            header.Controls.Add(_languageCombo);

            Panel body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(246, 247, 249),
                Padding = new Padding(28, 24, 28, 0)
            };

            _pathLabel = new Label
            {
                Text = S.T("安装目录"),
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

            _browseButton = CreateSecondaryButton(S.T("浏览..."));
            _browseButton.Location = new Point(666, 52);
            _browseButton.Size = new Size(96, 34);
            _browseButton.Click += OnBrowse;

            _desktopCheck = new CheckBox
            {
                Text = S.T("创建桌面快捷方式"),
                Font = new Font("Segoe UI", 13F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Location = new Point(30, 104),
                BackColor = Color.Transparent
            };

            _noteLabel = new Label
            {
                Text = S.T("安装后将添加到文件、文件夹的右键菜单中。\r\nWindows 11 若未直接显示，请点击右键菜单底部的“显示更多选项”。"),
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

            _cardTitle = new Label
            {
                Text = S.T("将安装以下内容"),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(31, 41, 55),
                AutoSize = true,
                Location = new Point(16, 8)
            };

            _cardBody = new Label
            {
                Text = S.T("•  主程序与卸载程序（约 0.3 MB）\r\n•  文件 / 文件夹右键菜单“{0}”\r\n•  开始菜单快捷方式与系统卸载信息", S.MenuLabel),
                Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(75, 85, 99),
                AutoSize = false,
                Size = new Size(672, 52),
                Location = new Point(16, 30)
            };

            card.Controls.Add(_cardTitle);
            card.Controls.Add(_cardBody);

            body.Controls.Add(_pathLabel);
            body.Controls.Add(_pathBox);
            body.Controls.Add(_browseButton);
            body.Controls.Add(_desktopCheck);
            body.Controls.Add(_noteLabel);
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
                Text = S.T("准备安装"),
                Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(107, 114, 128),
                AutoSize = true,
                Location = new Point(30, 38),
                BackColor = Color.Transparent
            };

            _cancelButton = CreateSecondaryButton(S.T("取消"));
            _cancelButton.Size = new Size(88, 38);
            _cancelButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _cancelButton.Location = new Point(footer.Width - 30 - 88, 28);
            _cancelButton.Click += OnCancel;

            _installButton = new Button
            {
                Text = S.T("安装"),
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

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (_suppressLanguageChange || _languageCombo == null)
            {
                return;
            }

            if (_busy)
            {
                SyncLanguageCombo();
                return;
            }

            var selected = (UiLanguage)_languageCombo.SelectedIndex;
            if (selected == S.Language)
            {
                return;
            }

            S.SetLanguage(selected);
            ApplyLanguage();
        }

        private void SyncLanguageCombo()
        {
            if (_languageCombo == null)
            {
                return;
            }

            _suppressLanguageChange = true;
            try
            {
                _languageCombo.SelectedIndex = (int)S.Language;
            }
            finally
            {
                _suppressLanguageChange = false;
            }
        }

        private void ApplyLanguage()
        {
            Text = S.T("解除占用 安装程序");
            _titleLabel.Text = S.T("解除占用");
            _subtitleLabel.Text = S.T("一键检测并结束正在占用文件或文件夹的进程");
            _versionLabel.Text = S.T("安装程序  v{0}", InstallerEngine.ProductVersion);
            _pathLabel.Text = S.T("安装目录");
            _browseButton.Text = S.T("浏览...");
            _desktopCheck.Text = S.T("创建桌面快捷方式");
            _noteLabel.Text = S.T("安装后将添加到文件、文件夹的右键菜单中。\r\nWindows 11 若未直接显示，请点击右键菜单底部的“显示更多选项”。");
            _cardTitle.Text = S.T("将安装以下内容");
            _cardBody.Text = S.T("•  主程序与卸载程序（约 0.3 MB）\r\n•  文件 / 文件夹右键菜单“{0}”\r\n•  开始菜单快捷方式与系统卸载信息", S.MenuLabel);
            _languageLabel.Text = S.T("界面语言");
            _cancelButton.Text = S.T("取消");
            _installButton.Text = S.T("安装");
            _statusLabel.Text = S.T(_statusKey);
            SyncLanguageCombo();
        }

        private void SetStatus(string key, Color color)
        {
            _statusKey = key;
            _statusLabel.Text = S.T(key);
            _statusLabel.ForeColor = color;
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
                    Description = S.T("选择解除占用的安装目录"),
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
                ShowError(S.T("无法选择目录"), ex.Message);
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
                ShowError(S.T("安装目录无效"), ex.Message);
                return;
            }

            _busy = true;
            SetBusy(true);
            SetStatus("正在安装，请稍候...", Color.FromArgb(37, 99, 235));

            try
            {
                bool desktop = _desktopCheck.Checked;
                await Task.Run(() => InstallerEngine.Install(dir, desktop));

                _progress.Visible = false;
                SetStatus("安装完成", Color.FromArgb(22, 163, 74));

                UiDialog.Info(
                    this,
                    S.T("安装成功"),
                    S.T(
                        "“{0}”已安装成功。\r\n\r\n安装目录：{1}\r\n\r\n现在可以右键点击文件或文件夹，选择“{0}”。\r\nWindows 11 请在“显示更多选项”中查看。",
                        S.MenuLabel,
                        dir),
                    S.T("确定"));

                ExitCode = 0;
                _busy = false;
                Close();
            }
            catch (Exception ex)
            {
                InstallerEngine.Log("界面安装失败：" + ex);
                _progress.Visible = false;
                SetStatus("安装失败", Color.FromArgb(185, 28, 28));
                ExitCode = 1;
                ShowError(S.T("安装失败"), ex.Message + "\r\n\r\n" + S.T("日志：") + InstallerEngine.LogPath);
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
            _languageCombo.Enabled = !busy;
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

            UiDialog.Error(this, title, message, S.T("确定"));
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
