using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NoOccupy.Core;

namespace NoOccupy
{
    internal static class UiTheme
    {
        public static readonly Color Page = Color.FromArgb(246, 247, 249);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(229, 231, 235);
        public static readonly Color ControlBorder = Color.FromArgb(209, 213, 219);
        public static readonly Color Text = Color.FromArgb(17, 24, 39);
        public static readonly Color Muted = Color.FromArgb(107, 114, 128);
        public static readonly Color Primary = Color.FromArgb(37, 99, 235);
        public static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216);
        public static readonly Color PrimaryDown = Color.FromArgb(30, 64, 175);
        public static readonly Color PrimaryDisabled = Color.FromArgb(191, 219, 254);
        public static readonly Color Success = Color.FromArgb(22, 163, 74);
        public static readonly Color Warning = Color.FromArgb(180, 83, 9);
        public static readonly Color Danger = Color.FromArgb(185, 28, 28);
        public static readonly Color SecondaryHover = Color.FromArgb(243, 244, 246);
        public static readonly Color SecondaryDown = Color.FromArgb(229, 231, 235);
        public static readonly Color DisabledBack = Color.FromArgb(249, 250, 251);
        public static readonly Color DisabledText = Color.FromArgb(156, 163, 175);
    }

    /// <summary>主窗口。</summary>
    public class MainForm : Form
    {
        private readonly string _initialPath;

        private string _currentPath;
        private string _displayPath;
        private bool _isScanning;
        private bool _isKilling;
        private bool _closing;
        private bool _suspendUiUpdates;
        private int _scanVersion;
        private CancellationTokenSource _scanCts;
        private List<LockingProcess> _processes = new List<LockingProcess>();

        private Panel _header;
        private PathDisplay _pathDisplay;
        private Button _chooseFileButton;
        private Button _chooseFolderButton;

        private Panel _statusPanel;
        private Label _statusLabel;
        private Label _statusDetail;
        private MarqueeBar _marquee;

        private Panel _listHost;
        private BufferedListView _listView;
        private Panel _emptyOverlay;
        private PictureBox _emptyIcon;
        private Label _emptyTitle;
        private Label _emptySubtitle;
        private Button _emptyChooseFileButton;
        private Button _emptyChooseFolderButton;

        private Panel _bottomPanel;
        private Button _selectAllButton;
        private Button _selectNoneButton;
        private Button _refreshButton;
        private Button _killButton;

        private ToolTip _toolTip;
        private ContextMenuStrip _contextMenu;
        private ToolStripMenuItem _killOneMenu;
        private ToolStripMenuItem _locateMenu;
        private LockingProcess _contextProcess;

        private Icon _appIcon;
        private Image _appImage;
        private ImageList _rowImages;

        public MainForm(string initialPath)
        {
            _initialPath = initialPath;
            _toolTip = new ToolTip
            {
                AutoPopDelay = 15000,
                InitialDelay = 350,
                ReshowDelay = 100,
                ShowAlways = true
            };

            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.None;

            Text = "解除占用";
            ClientSize = new Size(820, 560);
            MinimumSize = new Size(720, 480);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiTheme.Page;
            DoubleBuffered = true;

            _appIcon = LoadApplicationIcon(out _appImage);
            Icon = _appIcon;

            BuildUi();

            // 按当前系统 DPI（96 为基准）手动缩放整个窗体。
            // 不依赖 WinForms 的自动缩放时机，保证子控件与固定尺寸列一起缩放。
            float dpiScale = GetSystemDpiScale();
            if (dpiScale > 1.001F || dpiScale < 0.999F)
            {
                Scale(new SizeF(dpiScale, dpiScale));
                MinimumSize = new Size(
                    (int)Math.Round(720 * dpiScale),
                    (int)Math.Round(480 * dpiScale));
            }

            UpdateUiState();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            try
            {
                string cleaned = CleanPath(_initialPath);
                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    ShowEmptyState();
                }
                else if (PathExists(cleaned))
                {
                    BeginScan(_initialPath);
                }
                else
                {
                    ShowInvalidPath(cleaned);
                }
            }
            catch (Exception ex)
            {
                ShowScanFailure(ex);
            }

            ResizeColumns();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _closing = true;

            try
            {
                if (_scanCts != null)
                {
                    _scanCts.Cancel();
                }
            }
            catch
            {
                // 关闭窗口时忽略取消异常。
            }

            try
            {
                if (_marquee != null)
                {
                    _marquee.StopAnimation();
                }
            }
            catch
            {
                // 忽略动画停止异常。
            }

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (_scanCts != null)
                    {
                        _scanCts.Cancel();
                        _scanCts.Dispose();
                    }
                }
                catch
                {
                    // 忽略。
                }

                if (_toolTip != null)
                {
                    _toolTip.Dispose();
                }

                if (_contextMenu != null)
                {
                    _contextMenu.Dispose();
                }

                if (_rowImages != null)
                {
                    _rowImages.Dispose();
                }

                if (_appImage != null)
                {
                    _appImage.Dispose();
                }
            }

            base.Dispose(disposing);

            if (disposing && _appIcon != null)
            {
                _appIcon.Dispose();
                _appIcon = null;
            }
        }

        #region UI 构建

        private void BuildUi()
        {
            SuspendLayout();

            _listHost = BuildListHost();

            Controls.Add(_listHost);
            Controls.Add(BuildBottomPanel());
            Controls.Add(BuildStatusPanel());
            Controls.Add(BuildHeader());

            ResumeLayout(true);
        }

        private Panel BuildHeader()
        {
            _header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 132,
                BackColor = UiTheme.Card,
                Padding = new Padding(24, 16, 24, 14)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

            var iconBox = new PictureBox
            {
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = _appImage,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Margin = new Padding(0, 2, 12, 0),
                TabStop = false
            };
            layout.Controls.Add(iconBox, 0, 0);

            var titleStack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var titleLabel = new Label
            {
                Text = "解除占用",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = UiTheme.Text,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 1),
                UseMnemonic = false
            };
            var subtitleLabel = new Label
            {
                Text = "检测并结束正在占用此文件或文件夹的进程",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = UiTheme.Muted,
                AutoSize = true,
                Margin = new Padding(2, 0, 0, 0),
                UseMnemonic = false
            };
            titleStack.Controls.Add(titleLabel);
            titleStack.Controls.Add(subtitleLabel);
            layout.Controls.Add(titleStack, 1, 0);

            var pathRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108F));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124F));
            pathRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _pathDisplay = new PathDisplay
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 1, 10, 1),
                Font = Font,
                Text = "尚未选择文件或文件夹",
                ForeColor = UiTheme.DisabledText,
                BackColor = UiTheme.Card
            };
            _toolTip.SetToolTip(_pathDisplay, string.Empty);

            _chooseFileButton = CreateSecondaryButton("选择文件…");
            _chooseFileButton.Dock = DockStyle.Fill;
            _chooseFileButton.Margin = new Padding(0, 1, 8, 1);
            _chooseFileButton.Click += OnChooseFile;

            _chooseFolderButton = CreateSecondaryButton("选择文件夹…");
            _chooseFolderButton.Dock = DockStyle.Fill;
            _chooseFolderButton.Margin = new Padding(0, 1, 0, 1);
            _chooseFolderButton.Click += OnChooseFolder;

            pathRow.Controls.Add(_pathDisplay, 0, 0);
            pathRow.Controls.Add(_chooseFileButton, 1, 0);
            pathRow.Controls.Add(_chooseFolderButton, 2, 0);

            layout.Controls.Add(pathRow, 0, 2);
            layout.SetColumnSpan(pathRow, 2);

            _header.Controls.Add(layout);
            _header.Paint += OnHeaderPaint;

            return _header;
        }

        private Panel BuildStatusPanel()
        {
            _statusPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = UiTheme.Page
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(20, 10, 20, 4),
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = UiTheme.Text,
                Margin = new Padding(0),
                Text = "准备就绪"
            };

            _statusDetail = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = UiTheme.Muted,
                Margin = new Padding(8, 0, 0, 0)
            };

            _marquee = new MarqueeBar
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 0),
                Visible = false
            };

            layout.Controls.Add(_statusLabel, 0, 0);
            layout.Controls.Add(_statusDetail, 1, 0);
            layout.Controls.Add(_marquee, 0, 1);
            layout.SetColumnSpan(_marquee, 2);

            _statusPanel.Controls.Add(layout);
            return _statusPanel;
        }

        private Panel BuildListHost()
        {
            _listHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Page,
                Padding = new Padding(20, 10, 20, 0)
            };

            _rowImages = new ImageList
            {
                ImageSize = new Size(1, 24),
                ColorDepth = ColorDepth.Depth32Bit,
                TransparentColor = Color.Transparent
            };
            // ImageList 会保留图像引用，不能用 using 提前释放。
            _rowImages.Images.Add(new Bitmap(1, 24));

            _listView = new BufferedListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                HideSelection = false,
                GridLines = false,
                MultiSelect = true,
                ShowItemToolTips = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = UiTheme.Card,
                ForeColor = UiTheme.Text,
                Font = Font,
                SmallImageList = _rowImages,
                Visible = false
            };

            _listView.Columns.Add("进程名称", 200, HorizontalAlignment.Left);
            _listView.Columns.Add("PID", 70, HorizontalAlignment.Right);
            _listView.Columns.Add("程序路径", 360, HorizontalAlignment.Left);
            _listView.Columns.Add("来源", 90, HorizontalAlignment.Left);
            _listView.Columns.Add("服务", 110, HorizontalAlignment.Left);

            _listView.ItemCheck += OnListItemCheck;
            _listView.ItemChecked += OnListItemChecked;
            _listView.ItemActivate += OnListItemActivate;
            _listView.Resize += OnListResize;

            _contextMenu = new ContextMenuStrip();
            _killOneMenu = new ToolStripMenuItem("结束该进程");
            _locateMenu = new ToolStripMenuItem("打开文件位置");
            _killOneMenu.Click += OnKillOneProcess;
            _locateMenu.Click += OnLocateProcess;
            _contextMenu.Items.Add(_killOneMenu);
            _contextMenu.Items.Add(_locateMenu);
            _contextMenu.Opening += OnContextMenuOpening;
            _listView.ContextMenuStrip = _contextMenu;

            _emptyOverlay = BuildEmptyOverlay();

            _listHost.Controls.Add(_listView);
            _listHost.Controls.Add(_emptyOverlay);
            _emptyOverlay.BringToFront();

            return _listHost;
        }

        private Panel BuildEmptyOverlay()
        {
            var overlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Card,
                Visible = false
            };
            overlay.Paint += OnEmptyOverlayPaint;

            var centerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            centerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            centerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            var content = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 4,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _emptyIcon = new PictureBox
            {
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = _appImage,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, 0, 10),
                TabStop = false
            };
            content.Controls.Add(_emptyIcon, 0, 0);

            _emptyTitle = new Label
            {
                Text = "请选择一个文件或文件夹开始检测",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = UiTheme.Text,
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0)
            };
            content.Controls.Add(_emptyTitle, 0, 1);

            _emptySubtitle = new Label
            {
                Text = "支持检测文件或整个文件夹被哪些程序占用",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = UiTheme.Muted,
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 4, 0, 18)
            };
            content.Controls.Add(_emptySubtitle, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0)
            };

            _emptyChooseFileButton = CreatePrimaryButton("选择文件…");
            _emptyChooseFileButton.Size = new Size(128, 36);
            _emptyChooseFileButton.Margin = new Padding(0, 0, 10, 0);
            _emptyChooseFileButton.Click += OnChooseFile;

            _emptyChooseFolderButton = CreateSecondaryButton("选择文件夹…");
            _emptyChooseFolderButton.Size = new Size(136, 36);
            _emptyChooseFolderButton.Margin = new Padding(0);
            _emptyChooseFolderButton.Click += OnChooseFolder;

            buttons.Controls.Add(_emptyChooseFileButton);
            buttons.Controls.Add(_emptyChooseFolderButton);
            content.Controls.Add(buttons, 0, 3);

            centerLayout.Controls.Add(content, 0, 1);
            overlay.Controls.Add(centerLayout);

            return overlay;
        }

        private Panel BuildBottomPanel()
        {
            _bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = UiTheme.Card
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 5,
                RowCount = 1,
                Padding = new Padding(20, 15, 20, 15),
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _selectAllButton = CreateSecondaryButton("全选");
            _selectAllButton.Dock = DockStyle.Fill;
            _selectAllButton.Margin = new Padding(0, 0, 8, 0);
            _selectAllButton.Click += OnSelectAll;

            _selectNoneButton = CreateSecondaryButton("全不选");
            _selectNoneButton.Dock = DockStyle.Fill;
            _selectNoneButton.Margin = new Padding(0, 0, 8, 0);
            _selectNoneButton.Click += OnSelectNone;

            _refreshButton = CreateSecondaryButton("刷新");
            _refreshButton.Dock = DockStyle.Fill;
            _refreshButton.Margin = new Padding(0, 0, 8, 0);
            _refreshButton.Click += OnRefresh;

            _killButton = CreatePrimaryButton("结束选中进程");
            _killButton.Dock = DockStyle.Fill;
            _killButton.Margin = new Padding(0);
            _killButton.Click += OnKillSelected;

            layout.Controls.Add(_selectAllButton, 0, 0);
            layout.Controls.Add(_selectNoneButton, 1, 0);
            layout.Controls.Add(_refreshButton, 3, 0);
            layout.Controls.Add(_killButton, 4, 0);

            _bottomPanel.Controls.Add(layout);
            _bottomPanel.Paint += OnBottomPanelPaint;

            return _bottomPanel;
        }

        private static Button CreateSecondaryButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.Card,
                ForeColor = UiTheme.Text,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                AutoSize = false,
                Padding = new Padding(4, 0, 4, 0),
                TabStop = true
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = UiTheme.ControlBorder;
            button.FlatAppearance.MouseOverBackColor = UiTheme.SecondaryHover;
            button.FlatAppearance.MouseDownBackColor = UiTheme.SecondaryDown;
            return button;
        }

        private static Button CreatePrimaryButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.Primary,
                ForeColor = Color.White,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                AutoSize = false,
                Padding = new Padding(4, 0, 4, 0),
                TabStop = true
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = UiTheme.PrimaryHover;
            button.FlatAppearance.MouseDownBackColor = UiTheme.PrimaryDown;
            return button;
        }

        #endregion

        #region 绘制

        private void OnHeaderPaint(object sender, PaintEventArgs e)
        {
            try
            {
                using (var pen = new Pen(UiTheme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
                }
            }
            catch
            {
                // 绘制异常不影响使用。
            }
        }

        private void OnBottomPanelPaint(object sender, PaintEventArgs e)
        {
            try
            {
                using (var pen = new Pen(UiTheme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, 0, _bottomPanel.Width, 0);
                }
            }
            catch
            {
                // 绘制异常不影响使用。
            }
        }

        private void OnEmptyOverlayPaint(object sender, PaintEventArgs e)
        {
            try
            {
                using (var pen = new Pen(UiTheme.Border))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, _emptyOverlay.ClientSize.Width - 1, _emptyOverlay.ClientSize.Height - 1);
                }
            }
            catch
            {
                // 绘制异常不影响使用。
            }
        }

        #endregion

        #region 扫描

        private void BeginScan(string path)
        {
            try
            {
                string cleaned = CleanPath(path);
                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    ShowEmptyState();
                    return;
                }

                if (!PathExists(cleaned))
                {
                    ShowInvalidPath(cleaned);
                    return;
                }

                if (_scanCts != null)
                {
                    try
                    {
                        _scanCts.Cancel();
                    }
                    catch
                    {
                        // 忽略旧任务的取消异常。
                    }
                }

                _scanCts = new CancellationTokenSource();
                CancellationToken token = _scanCts.Token;
                int version = ++_scanVersion;

                _currentPath = path;
                _displayPath = cleaned;
                _pathDisplay.Text = cleaned;
                _pathDisplay.ForeColor = UiTheme.Text;
                _toolTip.SetToolTip(_pathDisplay, cleaned);

                _emptyOverlay.Visible = false;
                _listView.Visible = true;
                ClearList();

                _processes = new List<LockingProcess>();
                _isScanning = true;
                _isKilling = false;

                SetStatus("正在检测占用进程…", UiTheme.Primary, string.Empty);
                _marquee.Visible = true;
                _marquee.StartAnimation();
                UpdateUiState();

                ScanAsync(path, version, token);
            }
            catch (Exception ex)
            {
                ShowScanFailure(ex);
            }
        }

        private async void ScanAsync(string path, int version, CancellationToken token)
        {
            try
            {
                var progress = new UiProgress(this, version);
                ScanResult result = await Task.Run(
                    () => LockFinder.Scan(path, true, progress, token), token);

                if (_closing || IsDisposed || version != _scanVersion)
                {
                    return;
                }

                ApplyScanResult(result);
            }
            catch (OperationCanceledException)
            {
                if (!_closing && !IsDisposed && version == _scanVersion)
                {
                    SetStatus("已取消检测", UiTheme.Muted, string.Empty);
                }
            }
            catch (Exception ex)
            {
                if (!_closing && !IsDisposed && version == _scanVersion)
                {
                    ShowScanFailure(ex);
                }
            }
            finally
            {
                if (!_closing && !IsDisposed && version == _scanVersion)
                {
                    _isScanning = false;
                    _marquee.StopAnimation();
                    _marquee.Visible = false;
                    UpdateUiState();
                }
            }
        }

        private void ApplyScanResult(ScanResult result)
        {
            if (result == null)
            {
                throw new InvalidOperationException("检测没有返回结果。");
            }

            _processes = MergeAndSort(result.Processes);
            PopulateList(_processes);

            string detail = BuildScanDetail(result);

            if (_processes.Count == 0)
            {
                SetStatus("未发现占用进程，当前可以正常删除/移动", UiTheme.Success, detail);
            }
            else
            {
                SetStatus(string.Format("发现 {0} 个占用进程", _processes.Count), UiTheme.Warning, detail);
            }

            if (result.Warnings != null && result.Warnings.Count > 0)
            {
                string warnings = string.Join(Environment.NewLine, result.Warnings.Take(10).ToArray());
                _toolTip.SetToolTip(_statusDetail, warnings);
            }
        }

        private static string BuildScanDetail(ScanResult result)
        {
            var parts = new List<string>();

            if (result.IsDirectory)
            {
                parts.Add(string.Format("已扫描 {0:N0} 个文件", result.ScannedFiles));
            }

            if (result.ElapsedMs >= 1000)
            {
                parts.Add(string.Format("耗时 {0:0.0} 秒", result.ElapsedMs / 1000.0));
            }
            else
            {
                parts.Add(string.Format("耗时 {0} ms", result.ElapsedMs));
            }

            if (result.Warnings != null && result.Warnings.Count > 0)
            {
                parts.Add(string.Format("{0} 条警告", result.Warnings.Count));
            }

            return string.Join(" · ", parts.ToArray());
        }

        private static List<LockingProcess> MergeAndSort(IEnumerable<LockingProcess> processes)
        {
            var byPid = new Dictionary<int, LockingProcess>();

            if (processes != null)
            {
                foreach (LockingProcess process in processes)
                {
                    if (process == null)
                    {
                        continue;
                    }

                    LockingProcess existing;
                    if (!byPid.TryGetValue(process.Pid, out existing))
                    {
                        byPid[process.Pid] = new LockingProcess
                        {
                            Pid = process.Pid,
                            Name = process.Name,
                            ExecutablePath = process.ExecutablePath,
                            ServiceName = process.ServiceName,
                            UserName = process.UserName,
                            Source = process.Source,
                            IsCritical = process.IsCritical
                        };
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(existing.Name) && !string.IsNullOrEmpty(process.Name))
                        {
                            existing.Name = process.Name;
                        }

                        if (string.IsNullOrEmpty(existing.ExecutablePath) && !string.IsNullOrEmpty(process.ExecutablePath))
                        {
                            existing.ExecutablePath = process.ExecutablePath;
                        }

                        if (string.IsNullOrEmpty(existing.ServiceName) && !string.IsNullOrEmpty(process.ServiceName))
                        {
                            existing.ServiceName = process.ServiceName;
                        }

                        if (string.IsNullOrEmpty(existing.UserName) && !string.IsNullOrEmpty(process.UserName))
                        {
                            existing.UserName = process.UserName;
                        }

                        existing.IsCritical = existing.IsCritical || process.IsCritical;
                    }
                }
            }

            return byPid.Values
                .OrderBy(p => p.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(p => p.Pid)
                .ToList();
        }

        private void PopulateList(IList<LockingProcess> processes)
        {
            _suspendUiUpdates = true;

            try
            {
                _listView.BeginUpdate();
                try
                {
                    _listView.Items.Clear();

                    foreach (LockingProcess process in processes)
                    {
                        string name = string.IsNullOrWhiteSpace(process.Name)
                            ? "（未知进程）"
                            : process.Name;

                        if (process.IsCritical)
                        {
                            name += "（系统关键）";
                        }

                        var item = new ListViewItem(name);
                        item.SubItems.Add(process.Pid.ToString());
                        item.SubItems.Add(string.IsNullOrWhiteSpace(process.ExecutablePath) ? "—" : process.ExecutablePath);
                        item.SubItems.Add(process.SourceText);
                        item.SubItems.Add(string.IsNullOrWhiteSpace(process.ServiceName) ? string.Empty : process.ServiceName);
                        item.Tag = process;

                        var tip = new StringBuilder();
                        if (process.IsCritical)
                        {
                            tip.AppendLine("系统关键进程，不建议结束。");
                        }

                        tip.Append(string.IsNullOrWhiteSpace(process.ExecutablePath)
                            ? "程序路径不可用"
                            : process.ExecutablePath);

                        if (!string.IsNullOrWhiteSpace(process.ServiceName))
                        {
                            tip.AppendLine();
                            tip.Append("服务：").Append(process.ServiceName);
                        }

                        item.ToolTipText = tip.ToString();

                        if (process.IsCritical)
                        {
                            item.ForeColor = UiTheme.Danger;
                        }

                        _listView.Items.Add(item);
                    }
                }
                finally
                {
                    _listView.EndUpdate();
                }
            }
            finally
            {
                _suspendUiUpdates = false;
            }

            ResizeColumns();
            UpdateUiState();
        }

        private void ClearList()
        {
            _suspendUiUpdates = true;
            try
            {
                _listView.Items.Clear();
            }
            finally
            {
                _suspendUiUpdates = false;
            }

            UpdateUiState();
        }

        private void ShowEmptyState()
        {
            _currentPath = null;
            _displayPath = null;
            _processes = new List<LockingProcess>();
            ClearList();

            _pathDisplay.Text = "尚未选择文件或文件夹";
            _pathDisplay.ForeColor = UiTheme.DisabledText;
            _toolTip.SetToolTip(_pathDisplay, string.Empty);

            _statusLabel.Text = "准备就绪，请选择要检测的目标";
            _statusLabel.ForeColor = UiTheme.Muted;
            _statusDetail.Text = string.Empty;
            _statusDetail.ForeColor = UiTheme.Muted;
            _toolTip.SetToolTip(_statusDetail, string.Empty);

            _marquee.StopAnimation();
            _marquee.Visible = false;

            _emptyTitle.Text = "请选择一个文件或文件夹开始检测";
            _emptyTitle.ForeColor = UiTheme.Text;
            _emptySubtitle.Text = "支持检测文件或整个文件夹被哪些程序占用";
            _emptySubtitle.ForeColor = UiTheme.Muted;

            _listView.Visible = false;
            _emptyOverlay.Visible = true;
            _emptyOverlay.BringToFront();

            UpdateUiState();
        }

        private void ShowInvalidPath(string path)
        {
            _currentPath = null;
            _displayPath = path;
            _processes = new List<LockingProcess>();
            ClearList();

            _pathDisplay.Text = path;
            _pathDisplay.ForeColor = UiTheme.Muted;
            _toolTip.SetToolTip(_pathDisplay, path);

            _statusLabel.Text = "路径不存在或无法访问";
            _statusLabel.ForeColor = UiTheme.Danger;
            _statusDetail.Text = string.Empty;
            _statusDetail.ForeColor = UiTheme.Muted;
            _toolTip.SetToolTip(_statusDetail, string.Empty);

            _marquee.StopAnimation();
            _marquee.Visible = false;

            _emptyTitle.Text = "无法检测该路径";
            _emptyTitle.ForeColor = UiTheme.Text;
            _emptySubtitle.Text = "路径不存在或无法访问，请重新选择目标。";
            _emptySubtitle.ForeColor = UiTheme.Danger;

            _listView.Visible = false;
            _emptyOverlay.Visible = true;
            _emptyOverlay.BringToFront();

            UpdateUiState();
        }

        private void ShowScanFailure(Exception ex)
        {
            _isScanning = false;
            _marquee.StopAnimation();
            _marquee.Visible = false;

            string message = ex == null ? "未知错误" : ex.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                message = ex.GetType().Name;
            }

            SetStatus("检测失败：" + message, UiTheme.Danger, string.Empty);
            _emptyOverlay.Visible = false;
            _listView.Visible = true;
            ClearList();
            UpdateUiState();
        }

        private void OnScanProgress(int version, string text)
        {
            if (_closing || IsDisposed || version != _scanVersion)
            {
                return;
            }

            try
            {
                _statusDetail.Text = text ?? string.Empty;
                _statusDetail.ForeColor = UiTheme.Muted;
                _toolTip.SetToolTip(_statusDetail, text ?? string.Empty);
            }
            catch
            {
                // 进度文本更新失败不影响检测。
            }
        }

        #endregion

        #region 结束进程

        private void ConfirmAndKill(List<LockingProcess> processes)
        {
            if (processes == null || processes.Count == 0)
            {
                return;
            }

            if (_isScanning || _isKilling)
            {
                return;
            }

            processes = processes
                .Where(p => p != null && !p.IsCritical)
                .GroupBy(p => p.Pid)
                .Select(g => g.First())
                .ToList();

            if (processes.Count == 0)
            {
                ShowInformation("没有可结束的进程", "选中的行包含系统关键进程，已自动忽略。");
                return;
            }

            string message;
            if (processes.Count == 1)
            {
                LockingProcess process = processes[0];
                string name = string.IsNullOrWhiteSpace(process.Name) ? "未知进程" : process.Name;
                message = string.Format(
                    "确定要结束进程 “{0}”（PID {1}）吗？\r\n\r\n未保存的数据可能会丢失，此操作无法撤销。",
                    name,
                    process.Pid);
            }
            else
            {
                message = string.Format(
                    "确定要结束选中的 {0} 个进程吗？\r\n\r\n未保存的数据可能会丢失，此操作无法撤销。",
                    processes.Count);
            }

            DialogResult answer = MessageBox.Show(
                this,
                message,
                "结束进程",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer == DialogResult.Yes)
            {
                KillProcessesAsync(processes);
            }
        }

        private async void KillProcessesAsync(List<LockingProcess> processes)
        {
            if (_closing || _isKilling || _isScanning)
            {
                return;
            }

            int count = processes.Count;
            _isKilling = true;
            SetStatus(string.Format("正在结束 {0} 个进程…", count), UiTheme.Primary, string.Empty);
            _marquee.Visible = true;
            _marquee.StartAnimation();
            UpdateUiState();

            try
            {
                int[] pids = processes.Select(p => p.Pid).Distinct().ToArray();
                var progress = new UiProgress(this, _scanVersion);

                KillResult result = await Task.Run(() => ProcessKiller.Kill(pids, progress));

                if (_closing || IsDisposed)
                {
                    return;
                }

                _isKilling = false;
                _marquee.StopAnimation();
                _marquee.Visible = false;
                UpdateUiState();

                ShowKillResult(result);

                if (!_closing && !IsDisposed)
                {
                    if (!string.IsNullOrWhiteSpace(_currentPath))
                    {
                        BeginScan(_currentPath);
                    }
                    else
                    {
                        SetStatus("操作完成", UiTheme.Muted, string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_closing && !IsDisposed)
                {
                    _isKilling = false;
                    _marquee.StopAnimation();
                    _marquee.Visible = false;
                    SetStatus("结束进程失败：" + ex.Message, UiTheme.Danger, string.Empty);
                    UpdateUiState();
                    ShowError("结束进程失败", ex.Message);
                }
            }
            finally
            {
                if (!_closing && !IsDisposed)
                {
                    _isKilling = false;
                    if (!_isScanning)
                    {
                        _marquee.StopAnimation();
                        _marquee.Visible = false;
                    }
                    UpdateUiState();
                }
            }
        }

        private void ShowKillResult(KillResult result)
        {
            int succeeded = result == null ? 0 : result.Succeeded;
            int failed = result == null ? 0 : result.Failed;

            var message = new StringBuilder();
            message.AppendFormat("成功 {0} 个 / 失败 {1} 个。", succeeded, failed);

            if (failed > 0 && result != null && result.Outcomes != null)
            {
                message.AppendLine();
                message.AppendLine();
                message.AppendLine("失败摘要：");

                List<KillOutcome> failures = result.Outcomes
                    .Where(o => o != null && !o.Success)
                    .Take(5)
                    .ToList();

                foreach (KillOutcome failure in failures)
                {
                    string name = string.IsNullOrWhiteSpace(failure.Name) ? "未知进程" : failure.Name;
                    string error = string.IsNullOrWhiteSpace(failure.Error) ? "未知错误" : failure.Error;
                    error = error.Replace("\r", " ").Replace("\n", " ").Trim();

                    if (error.Length > 120)
                    {
                        error = error.Substring(0, 120) + "…";
                    }

                    message.AppendFormat("• {0}（PID {1}）：{2}", name, failure.Pid, error);
                    message.AppendLine();
                }

                if (result.Outcomes.Count(o => o != null && !o.Success) > failures.Count)
                {
                    message.AppendLine("…");
                }
            }

            MessageBox.Show(
                this,
                message.ToString(),
                failed == 0 ? "操作完成" : "部分进程未能结束",
                MessageBoxButtons.OK,
                failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        #endregion

        #region 事件

        private void OnChooseFile(object sender, EventArgs e)
        {
            try
            {
                using (var dialog = new OpenFileDialog
                {
                    Title = "选择要检测的文件",
                    CheckFileExists = true,
                    Multiselect = false,
                    RestoreDirectory = true,
                    Filter = "所有文件 (*.*)|*.*"
                })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        BeginScan(dialog.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("无法选择文件", ex.Message);
            }
        }

        private void OnChooseFolder(object sender, EventArgs e)
        {
            try
            {
                using (var dialog = new FolderBrowserDialog
                {
                    Description = "选择要检测的文件夹",
                    ShowNewFolderButton = false
                })
                {
                    if (!string.IsNullOrWhiteSpace(_currentPath) && Directory.Exists(CleanPath(_currentPath)))
                    {
                        dialog.SelectedPath = CleanPath(_currentPath);
                    }

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        BeginScan(dialog.SelectedPath);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("无法选择文件夹", ex.Message);
            }
        }

        private void OnRefresh(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_currentPath))
                {
                    BeginScan(_currentPath);
                }
            }
            catch (Exception ex)
            {
                ShowScanFailure(ex);
            }
        }

        private void OnSelectAll(object sender, EventArgs e)
        {
            try
            {
                _suspendUiUpdates = true;
                try
                {
                    foreach (ListViewItem item in _listView.Items)
                    {
                        var process = item.Tag as LockingProcess;
                        if (process != null && !process.IsCritical)
                        {
                            item.Checked = true;
                        }
                    }
                }
                finally
                {
                    _suspendUiUpdates = false;
                }
            }
            catch (Exception ex)
            {
                ShowError("无法全选", ex.Message);
            }
            finally
            {
                UpdateUiState();
            }
        }

        private void OnSelectNone(object sender, EventArgs e)
        {
            try
            {
                _suspendUiUpdates = true;
                try
                {
                    foreach (ListViewItem item in _listView.Items)
                    {
                        item.Checked = false;
                    }
                }
                finally
                {
                    _suspendUiUpdates = false;
                }
            }
            catch (Exception ex)
            {
                ShowError("无法取消选择", ex.Message);
            }
            finally
            {
                UpdateUiState();
            }
        }

        private void OnKillSelected(object sender, EventArgs e)
        {
            try
            {
                List<LockingProcess> selected = GetCheckedProcesses();
                if (selected.Count == 0)
                {
                    return;
                }

                ConfirmAndKill(selected);
            }
            catch (Exception ex)
            {
                ShowError("无法结束进程", ex.Message);
            }
        }

        private void OnKillOneProcess(object sender, EventArgs e)
        {
            try
            {
                LockingProcess process = _contextProcess;
                if (process == null)
                {
                    return;
                }

                if (process.IsCritical)
                {
                    ShowInformation("系统关键进程", "该进程被标记为系统关键进程，不能从本工具结束。");
                    return;
                }

                ConfirmAndKill(new List<LockingProcess> { process });
            }
            catch (Exception ex)
            {
                ShowError("无法结束进程", ex.Message);
            }
        }

        private void OnLocateProcess(object sender, EventArgs e)
        {
            try
            {
                LocateProcess(_contextProcess);
            }
            catch (Exception ex)
            {
                ShowError("无法打开文件位置", ex.Message);
            }
        }

        private void OnListItemActivate(object sender, EventArgs e)
        {
            try
            {
                ListViewItem item = _listView.FocusedItem;
                if (item != null)
                {
                    LocateProcess(item.Tag as LockingProcess);
                }
            }
            catch (Exception ex)
            {
                ShowError("无法打开文件位置", ex.Message);
            }
        }

        private void OnListItemCheck(object sender, ItemCheckEventArgs e)
        {
            try
            {
                if (_suspendUiUpdates || _isScanning || _isKilling)
                {
                    if (_isScanning || _isKilling)
                    {
                        e.NewValue = e.CurrentValue;
                    }

                    return;
                }

                if (e.Index < 0 || e.Index >= _listView.Items.Count)
                {
                    return;
                }

                var process = _listView.Items[e.Index].Tag as LockingProcess;
                if (process != null && process.IsCritical && e.NewValue == CheckState.Checked)
                {
                    e.NewValue = CheckState.Unchecked;
                }
            }
            catch
            {
                // 勾选状态异常时保持原状态。
            }
        }

        private void OnListItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_suspendUiUpdates)
            {
                return;
            }

            try
            {
                UpdateUiState();
            }
            catch
            {
                // 忽略按钮状态刷新异常。
            }
        }

        private void OnListResize(object sender, EventArgs e)
        {
            ResizeColumns();
        }

        private void OnContextMenuOpening(object sender, CancelEventArgs e)
        {
            try
            {
                Point point = _listView.PointToClient(Cursor.Position);
                ListViewItem item = _listView.GetItemAt(point.X, point.Y);

                if (item == null)
                {
                    item = _listView.FocusedItem;
                }

                if (item == null)
                {
                    e.Cancel = true;
                    return;
                }

                item.Selected = true;
                item.Focused = true;
                _contextProcess = item.Tag as LockingProcess;

                bool busy = _isScanning || _isKilling;
                _killOneMenu.Enabled = !busy && _contextProcess != null && !_contextProcess.IsCritical;
                _locateMenu.Enabled = _contextProcess != null &&
                                      !string.IsNullOrWhiteSpace(_contextProcess.ExecutablePath);
            }
            catch
            {
                e.Cancel = true;
            }
        }

        #endregion

        #region 辅助

        private void UpdateUiState()
        {
            if (_closing || IsDisposed)
            {
                return;
            }

            try
            {
                bool busy = _isScanning || _isKilling;
                int selectableCount = 0;
                int checkedCount = 0;

                foreach (ListViewItem item in _listView.Items)
                {
                    var process = item.Tag as LockingProcess;
                    if (process == null || process.IsCritical)
                    {
                        continue;
                    }

                    selectableCount++;
                    if (item.Checked)
                    {
                        checkedCount++;
                    }
                }

                _chooseFileButton.Enabled = !busy;
                _chooseFolderButton.Enabled = !busy;
                _emptyChooseFileButton.Enabled = !busy;
                _emptyChooseFolderButton.Enabled = !busy;

                _selectAllButton.Enabled = !busy && selectableCount > 0 && checkedCount < selectableCount;
                _selectNoneButton.Enabled = !busy && checkedCount > 0;
                _refreshButton.Enabled = !busy && !string.IsNullOrWhiteSpace(_currentPath);
                _killButton.Enabled = !busy && checkedCount > 0;

                StyleSecondaryButton(_selectAllButton);
                StyleSecondaryButton(_selectNoneButton);
                StyleSecondaryButton(_refreshButton);
                StylePrimaryButton(_killButton);
            }
            catch
            {
                // 刷新按钮状态失败不影响主流程。
            }
        }

        private static void StyleSecondaryButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (button.Enabled)
            {
                button.BackColor = UiTheme.Card;
                button.ForeColor = UiTheme.Text;
                button.FlatAppearance.BorderColor = UiTheme.ControlBorder;
            }
            else
            {
                button.BackColor = UiTheme.DisabledBack;
                button.ForeColor = UiTheme.DisabledText;
                button.FlatAppearance.BorderColor = UiTheme.Border;
            }
        }

        private static void StylePrimaryButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (button.Enabled)
            {
                button.BackColor = UiTheme.Primary;
                button.ForeColor = Color.White;
                button.Cursor = Cursors.Hand;
            }
            else
            {
                button.BackColor = UiTheme.PrimaryDisabled;
                button.ForeColor = Color.White;
                button.Cursor = Cursors.Default;
            }
        }

        private List<LockingProcess> GetCheckedProcesses()
        {
            var selected = new List<LockingProcess>();

            foreach (ListViewItem item in _listView.CheckedItems)
            {
                var process = item.Tag as LockingProcess;
                if (process != null && !process.IsCritical && !selected.Any(p => p.Pid == process.Pid))
                {
                    selected.Add(process);
                }
            }

            return selected;
        }

        private void ResizeColumns()
        {
            try
            {
                if (_listView == null || _listView.Columns.Count < 5)
                {
                    return;
                }

                float scale = DeviceDpi / 96F;
                int nameWidth = (int)Math.Round(200 * scale);
                int pidWidth = (int)Math.Round(70 * scale);
                int sourceWidth = (int)Math.Round(90 * scale);
                int serviceWidth = (int)Math.Round(110 * scale);
                int available = _listView.ClientSize.Width;
                int pathWidth = Math.Max((int)Math.Round(180 * scale), available - (nameWidth + pidWidth + sourceWidth + serviceWidth) - 2);

                _listView.Columns[0].Width = nameWidth;
                _listView.Columns[1].Width = pidWidth;
                _listView.Columns[2].Width = pathWidth;
                _listView.Columns[3].Width = sourceWidth;
                _listView.Columns[4].Width = serviceWidth;
            }
            catch
            {
                // 调整列宽失败不影响使用。
            }
        }

        private void LocateProcess(LockingProcess process)
        {
            if (process == null)
            {
                return;
            }

            string path = process.ExecutablePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                ShowInformation("无法定位", "无法获取该进程的程序路径。");
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/select,\"" + path + "\"",
                        UseShellExecute = true
                    });
                    return;
                }

                string directory = null;
                try
                {
                    directory = Path.GetDirectoryName(path);
                }
                catch
                {
                    // 路径格式异常时按不可定位处理。
                }

                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "\"" + directory + "\"",
                        UseShellExecute = true
                    });
                    return;
                }

                ShowInformation("无法定位", "程序文件已不存在，无法打开所在位置。\r\n\r\n" + path);
            }
            catch (Exception ex)
            {
                ShowError("无法打开文件位置", ex.Message);
            }
        }

        private void SetStatus(string text, Color color, string detail)
        {
            _statusLabel.Text = text ?? string.Empty;
            _statusLabel.ForeColor = color;
            _statusDetail.Text = detail ?? string.Empty;
            _statusDetail.ForeColor = UiTheme.Muted;
            _toolTip.SetToolTip(_statusDetail, detail ?? string.Empty);
        }

        private void ShowInformation(string title, string message)
        {
            if (_closing || IsDisposed)
            {
                return;
            }

            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowError(string title, string message)
        {
            if (_closing || IsDisposed)
            {
                return;
            }

            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static string CleanPath(string path)
        {
            if (path == null)
            {
                return string.Empty;
            }

            string value = path.Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                value = value.Substring(1, value.Length - 2);
            }

            return value;
        }

        private static bool PathExists(string path)
        {
            try
            {
                return File.Exists(path) || Directory.Exists(path);
            }
            catch
            {
                return false;
            }
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
                // Windows 10 1607 之前没有该 API，使用 Graphics 回退。
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

        private static Icon LoadApplicationIcon(out Image preview)
        {
            Icon icon = null;

            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath))
                {
                    icon = new Icon(icoPath);
                }
            }
            catch
            {
                icon = null;
            }

            if (icon == null)
            {
                try
                {
                    icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch
                {
                    icon = null;
                }
            }

            if (icon == null)
            {
                try
                {
                    icon = (Icon)SystemIcons.Shield.Clone();
                }
                catch
                {
                    icon = SystemIcons.Application;
                }
            }

            try
            {
                using (var large = new Icon(icon, 64, 64))
                {
                    preview = large.ToBitmap();
                }
            }
            catch
            {
                try
                {
                    preview = SystemIcons.Shield.ToBitmap();
                }
                catch
                {
                    preview = null;
                }
            }

            return icon;
        }

        private void SafeBeginInvoke(Action action)
        {
            if (action == null || _closing || IsDisposed || Disposing)
            {
                return;
            }

            try
            {
                if (!IsHandleCreated)
                {
                    return;
                }

                BeginInvoke(new Action(() =>
                {
                    if (_closing || IsDisposed || Disposing)
                    {
                        return;
                    }

                    try
                    {
                        action();
                    }
                    catch
                    {
                        // 异步 UI 更新异常不向外传播。
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                // 窗口句柄正在销毁或窗口已销毁。
            }
        }

        private sealed class UiProgress : IProgress<string>
        {
            private readonly MainForm _owner;
            private readonly int _version;
            private long _lastTicks;

            public UiProgress(MainForm owner, int version)
            {
                _owner = owner;
                _version = version;
            }

            public void Report(string value)
            {
                if (_owner == null || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                long now = DateTime.UtcNow.Ticks;
                if (now - _lastTicks < TimeSpan.TicksPerMillisecond * 80)
                {
                    return;
                }

                _lastTicks = now;
                string text = value;
                _owner.SafeBeginInvoke(() => _owner.OnScanProgress(_version, text));
            }
        }

        #endregion
    }

    /// <summary>路径显示控件：中间省略，悬停由 ToolTip 显示完整路径。</summary>
    internal sealed class PathDisplay : Control
    {
        public PathDisplay()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            Height = 34;
            BackColor = UiTheme.Card;
            ForeColor = UiTheme.Text;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                Graphics graphics = e.Graphics;
                graphics.SmoothingMode = SmoothingMode.None;

                using (var background = new SolidBrush(BackColor))
                {
                    graphics.FillRectangle(background, ClientRectangle);
                }

                using (var border = new Pen(UiTheme.ControlBorder))
                {
                    graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
                }

                var textBounds = new Rectangle(10, 0, Math.Max(0, Width - 20), Height);
                TextRenderer.DrawText(
                    graphics,
                    Text ?? string.Empty,
                    Font,
                    textBounds,
                    ForeColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.PathEllipsis |
                    TextFormatFlags.NoPrefix);
            }
            catch
            {
                // 绘制异常时保持空白。
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Invalidate();
        }
    }

    /// <summary>细长的 Marquee 进度条。</summary>
    internal sealed class MarqueeBar : Control
    {
        private readonly System.Windows.Forms.Timer _timer;
        private float _phase;

        public MarqueeBar()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            Height = 4;
            BackColor = UiTheme.Page;

            _timer = new System.Windows.Forms.Timer { Interval = 25 };
            _timer.Tick += OnTimerTick;
        }

        public void StartAnimation()
        {
            Visible = true;
            if (!_timer.Enabled)
            {
                _timer.Start();
            }
        }

        public void StopAnimation()
        {
            _timer.Stop();
            _phase = 0F;
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            try
            {
                _phase += 0.022F;
                if (_phase >= 1F)
                {
                    _phase -= 1F;
                }

                Invalidate();
            }
            catch
            {
                // 动画异常时停止，不影响检测。
                _timer.Stop();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                Graphics graphics = e.Graphics;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                var trackRect = new RectangleF(0, 0, Width, Height);
                using (GraphicsPath trackPath = CreateRoundedRect(trackRect, Height / 2F))
                using (var trackBrush = new SolidBrush(UiTheme.Border))
                {
                    graphics.FillPath(trackBrush, trackPath);
                }

                float segmentWidth = Math.Max(40F, Width / 4F);
                float x = _phase * (Width + segmentWidth) - segmentWidth;
                var segmentRect = new RectangleF(x, 0, segmentWidth, Height);

                using (GraphicsPath segmentPath = CreateRoundedRect(segmentRect, Height / 2F))
                using (var segmentBrush = new SolidBrush(UiTheme.Primary))
                {
                    graphics.FillPath(segmentBrush, segmentPath);
                }
            }
            catch
            {
                // 绘制异常时保持空白。
            }
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float diameter = Math.Max(0F, radius * 2F);

            if (diameter <= 0F || rect.Width <= diameter || rect.Height <= diameter)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Tick -= OnTimerTick;
                _timer.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>开启双缓冲的 ListView，减少刷新闪烁。</summary>
    internal sealed class BufferedListView : ListView
    {
        public BufferedListView()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}
