using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SiteManagerUI;

public partial class Form1 : Form
{
    private readonly string _projectRoot;
    private readonly string _dataPath;
    private readonly string _managerScriptPath;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private SiteData _data = new();
    private bool _loadingEditor;

    private TextBox txtBrowserTitle = null!;
    private TextBox txtBannerTitle = null!;
    private Button btnSaveOnly = null!;
    private Button btnSavePublish = null!;
    private Button btnBuildOnly = null!;

    private TextBox txtQuickUrl = null!;
    private TextBox txtQuickTitle = null!;
    private TextBox txtQuickImage = null!;
    private Button btnQuickBrowse = null!;
    private Button btnQuickAdd = null!;

    private ListView lvItems = null!;
    private Button btnDelete = null!;
    private Button btnReload = null!;

    private NumericUpDown numOrder = null!;
    private CheckBox chkEnabled = null!;
    private TextBox txtSlug = null!;
    private TextBox txtCardTitle = null!;
    private ComboBox cmbPageMode = null!;
    private TextBox txtPageFile = null!;
    private TextBox txtCardImage = null!;
    private Button btnEditorBrowse = null!;
    private TextBox txtCardAlt = null!;
    private TextBox txtPageTitle = null!;
    private TextBox txtPageSummary = null!;
    private Button btnApply = null!;

    private Label lblStatus = null!;

    public Form1()
    {
        _projectRoot = FindProjectRoot();
        _dataPath = Path.Combine(_projectRoot, "content", "site-content.json");
        _managerScriptPath = Path.Combine(_projectRoot, "scripts", "site-manager.ps1");

        InitializeComponent();
        BuildUi();
        LoadData();
    }

    private void BuildUi()
    {
        Text = "网页可视化管理器（重建版）";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1360, 880);
        MinimumSize = new Size(1240, 780);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(root);

        var topGroup = new GroupBox
        {
            Text = "步骤 1：站点设置与发布",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        root.Controls.Add(topGroup, 0, 0);

        var topLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 2,
            Padding = new Padding(12, 8, 12, 8)
        };
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8));
        topGroup.Controls.Add(topLayout);

        topLayout.Controls.Add(CreateFieldLabel("浏览器标题"), 0, 0);
        txtBrowserTitle = new TextBox { Dock = DockStyle.Fill };
        topLayout.Controls.Add(txtBrowserTitle, 1, 0);

        topLayout.Controls.Add(CreateFieldLabel("首页横幅"), 2, 0);
        txtBannerTitle = new TextBox { Dock = DockStyle.Fill };
        topLayout.Controls.Add(txtBannerTitle, 3, 0);

        btnSaveOnly = new Button
        {
            Text = "仅保存",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 249, 255)
        };
        btnSaveOnly.Click += (_, _) => SaveDataFromUi(showMessage: true);
        topLayout.Controls.Add(btnSaveOnly, 4, 0);

        btnSavePublish = new Button
        {
            Text = "保存并发布",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(42, 123, 228),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnSavePublish.Click += (_, _) => SaveAndPublish();
        topLayout.Controls.Add(btnSavePublish, 5, 0);

        btnBuildOnly = new Button
        {
            Text = "仅发布",
            Dock = DockStyle.Fill
        };
        btnBuildOnly.Click += (_, _) => RunBuildOnly();
        topLayout.Controls.Add(btnBuildOnly, 6, 0);

        var tip = new Label
        {
            Text = $"项目目录：{_projectRoot}",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleLeft
        };
        topLayout.SetColumnSpan(tip, 7);
        topLayout.Controls.Add(tip, 0, 1);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 560
        };
        root.Controls.Add(split, 0, 1);

        BuildLeftPanel(split.Panel1);
        BuildRightPanel(split.Panel2);

        lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DimGray,
            Text = "就绪"
        };
        root.Controls.Add(lblStatus, 0, 2);
    }

    private void BuildLeftPanel(Control parent)
    {
        var leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0)
        };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        parent.Controls.Add(leftLayout);

        var quickGroup = new GroupBox
        {
            Text = "步骤 2：快速新增外部网页",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        leftLayout.Controls.Add(quickGroup, 0, 0);

        var quickLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(10, 8, 10, 8)
        };
        quickLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        quickLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        quickLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        quickGroup.Controls.Add(quickLayout);

        quickLayout.Controls.Add(CreateFieldLabel("网页URL"), 0, 0);
        txtQuickUrl = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "https://example.com"
        };
        quickLayout.Controls.Add(txtQuickUrl, 1, 0);
        quickLayout.SetColumnSpan(txtQuickUrl, 2);

        quickLayout.Controls.Add(CreateFieldLabel("卡片标题"), 0, 1);
        txtQuickTitle = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "例如：我的项目"
        };
        quickLayout.Controls.Add(txtQuickTitle, 1, 1);
        quickLayout.SetColumnSpan(txtQuickTitle, 2);

        quickLayout.Controls.Add(CreateFieldLabel("封面图"), 0, 2);
        txtQuickImage = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "可填图片 URL 或本地路径（可留空）"
        };
        quickLayout.Controls.Add(txtQuickImage, 1, 2);

        btnQuickBrowse = new Button
        {
            Text = "选择图片",
            Dock = DockStyle.Fill
        };
        btnQuickBrowse.Click += (_, _) =>
        {
            var selected = SelectImageFile();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                txtQuickImage.Text = selected;
            }
        };
        quickLayout.Controls.Add(btnQuickBrowse, 2, 2);

        btnQuickAdd = new Button
        {
            Text = "新增外链卡片",
            Dock = DockStyle.Fill,
            BackColor = Color.Honeydew
        };
        btnQuickAdd.Click += (_, _) => AddExternalFromQuick();
        quickLayout.Controls.Add(btnQuickAdd, 0, 3);
        quickLayout.SetColumnSpan(btnQuickAdd, 3);

        var listGroup = new GroupBox
        {
            Text = "步骤 3：内容列表（选中后右侧编辑）",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        leftLayout.Controls.Add(listGroup, 0, 1);

        var listLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        listGroup.Controls.Add(listLayout);

        lvItems = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            GridLines = true,
            HideSelection = false
        };
        lvItems.Columns.Add("序", 48);
        lvItems.Columns.Add("显示", 58);
        lvItems.Columns.Add("卡片标题", 170);
        lvItems.Columns.Add("模式", 92);
        lvItems.Columns.Add("链接/文件", 240);
        lvItems.SelectedIndexChanged += (_, _) => LoadEditorFromSelected();
        listLayout.Controls.Add(lvItems, 0, 0);

        var rowButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        rowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        rowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        listLayout.Controls.Add(rowButtons, 0, 1);

        btnDelete = new Button
        {
            Text = "删除选中",
            Dock = DockStyle.Fill,
            BackColor = Color.MistyRose
        };
        btnDelete.Click += (_, _) => DeleteSelected();
        rowButtons.Controls.Add(btnDelete, 0, 0);

        btnReload = new Button
        {
            Text = "重新加载",
            Dock = DockStyle.Fill
        };
        btnReload.Click += (_, _) => LoadData();
        rowButtons.Controls.Add(btnReload, 1, 0);
    }

    private void BuildRightPanel(Control parent)
    {
        var group = new GroupBox
        {
            Text = "步骤 4：编辑选中卡片（改完点“应用并保存”）",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        parent.Controls.Add(group);

        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(10)
        };
        group.Controls.Add(scrollPanel);

        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        scrollPanel.Controls.Add(editor);

        var row = 0;

        AddEditorRow(editor, row++, "排序", out numOrder);
        AddEditorRow(editor, row++, "是否显示", out chkEnabled);
        AddEditorRow(editor, row++, "页面 slug", out txtSlug, readOnly: true);
        AddEditorRow(editor, row++, "卡片标题", out txtCardTitle);
        AddEditorRow(editor, row++, "页面模式", out cmbPageMode);
        cmbPageMode.Items.AddRange(new object[] { "external-link", "generated", "external" });

        AddEditorRow(editor, row++, "链接/页面文件", out txtPageFile);

        AddEditorRow(editor, row++, "卡片封面图", out txtCardImage);
        btnEditorBrowse = new Button
        {
            Text = "选图",
            Dock = DockStyle.Fill
        };
        btnEditorBrowse.Click += (_, _) =>
        {
            var selected = SelectImageFile();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                txtCardImage.Text = selected;
            }
        };
        editor.Controls.Add(btnEditorBrowse, 2, row - 1);

        AddEditorRow(editor, row++, "图片 alt", out txtCardAlt);
        AddEditorRow(editor, row++, "页面标题", out txtPageTitle);
        AddEditorRow(editor, row++, "页面简介", out txtPageSummary, multiline: true);

        btnApply = new Button
        {
            Text = "应用并保存",
            Dock = DockStyle.Fill,
            Height = 40,
            BackColor = Color.Honeydew,
            Margin = new Padding(0, 16, 0, 6)
        };
        btnApply.Click += (_, _) => ApplySelected(autoSave: true, showMessage: true);

        editor.RowCount += 1;
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        editor.Controls.Add(new Label(), 0, editor.RowCount - 1);
        editor.Controls.Add(btnApply, 1, editor.RowCount - 1);
        editor.Controls.Add(new Label(), 2, editor.RowCount - 1);
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static void AddEditorRow(TableLayoutPanel panel, int rowIndex, string label, out TextBox textBox, bool readOnly = false, bool multiline = false)
    {
        panel.RowCount += 1;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, multiline ? 100 : 38));

        panel.Controls.Add(CreateFieldLabel(label), 0, rowIndex);

        textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = readOnly,
            Multiline = multiline,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
        };
        panel.Controls.Add(textBox, 1, rowIndex);

        panel.Controls.Add(new Label(), 2, rowIndex);
    }

    private static void AddEditorRow(TableLayoutPanel panel, int rowIndex, string label, out NumericUpDown numeric)
    {
        panel.RowCount += 1;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        panel.Controls.Add(CreateFieldLabel(label), 0, rowIndex);

        numeric = new NumericUpDown
        {
            Dock = DockStyle.Left,
            Width = 140,
            Minimum = 1,
            Maximum = 99999
        };
        panel.Controls.Add(numeric, 1, rowIndex);

        panel.Controls.Add(new Label(), 2, rowIndex);
    }

    private static void AddEditorRow(TableLayoutPanel panel, int rowIndex, string label, out CheckBox checkBox)
    {
        panel.RowCount += 1;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        panel.Controls.Add(CreateFieldLabel(label), 0, rowIndex);

        checkBox = new CheckBox
        {
            Text = "启用并显示",
            Dock = DockStyle.Left,
            Width = 140
        };
        panel.Controls.Add(checkBox, 1, rowIndex);

        panel.Controls.Add(new Label(), 2, rowIndex);
    }

    private static void AddEditorRow(TableLayoutPanel panel, int rowIndex, string label, out ComboBox comboBox)
    {
        panel.RowCount += 1;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        panel.Controls.Add(CreateFieldLabel(label), 0, rowIndex);

        comboBox = new ComboBox
        {
            Dock = DockStyle.Left,
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        panel.Controls.Add(comboBox, 1, rowIndex);

        panel.Controls.Add(new Label(), 2, rowIndex);
    }

    private void LoadData()
    {
        try
        {
            if (!File.Exists(_dataPath))
            {
                _data = new SiteData
                {
                    Site = new SiteConfig
                    {
                        BrowserTitle = "椭圆轮播展示",
                        BannerTitle = "HELLO! This is Lotus! Welcome!"
                    },
                    Items = new List<SiteItem>()
                };
                SaveDataFromUi(showMessage: false);
            }
            else
            {
                var json = File.ReadAllText(_dataPath, Encoding.UTF8);
                _data = JsonSerializer.Deserialize<SiteData>(json, _jsonOptions) ?? new SiteData();
            }

            EnsureDataValid();
            FillUiFromData();
            SetStatus($"已加载：{_data.Items.Count} 个卡片");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"读取数据失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("读取失败");
        }
    }

    private void EnsureDataValid()
    {
        _data.Site ??= new SiteConfig();
        _data.Items ??= new List<SiteItem>();

        foreach (var item in _data.Items)
        {
            item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim();
            item.PageSlug = string.IsNullOrWhiteSpace(item.PageSlug) ? ToSafeSlug(item.Id) : ToSafeSlug(item.PageSlug);
            item.CardTitle = string.IsNullOrWhiteSpace(item.CardTitle)
                ? (string.IsNullOrWhiteSpace(item.PageTitle) ? item.PageSlug : item.PageTitle)
                : item.CardTitle.Trim();
            item.CardAlt = string.IsNullOrWhiteSpace(item.CardAlt) ? item.CardTitle : item.CardAlt.Trim();
            item.CardImage = NormalizePathForSite(item.CardImage);

            item.PageMode = NormalizeMode(item.PageMode);
            if (string.IsNullOrWhiteSpace(item.PageFile) && item.PageMode == "generated")
            {
                item.PageFile = $"pages/content/{item.PageSlug}.html";
            }

            item.PageTitle = string.IsNullOrWhiteSpace(item.PageTitle) ? item.CardTitle : item.PageTitle.Trim();
            item.PageSummary = string.IsNullOrWhiteSpace(item.PageSummary) ? "这是一个页面。" : item.PageSummary.Trim();
            item.PageLayout = string.IsNullOrWhiteSpace(item.PageLayout) ? "image-left" : item.PageLayout;
            item.PageImage = string.IsNullOrWhiteSpace(item.PageImage) ? item.CardImage : NormalizePathForSite(item.PageImage);
            item.PageBody ??= new List<string>();
            if (item.PageBody.Count == 0)
            {
                item.PageBody.Add("这里还没有正文内容。你可以后续再补充。");
            }

            if (item.Order <= 0)
            {
                item.Order = 1;
            }
        }

        NormalizeOrders();
    }

    private void FillUiFromData()
    {
        _loadingEditor = true;
        try
        {
            txtBrowserTitle.Text = _data.Site.BrowserTitle ?? "";
            txtBannerTitle.Text = _data.Site.BannerTitle ?? "";
            RefreshItemList();
        }
        finally
        {
            _loadingEditor = false;
        }
    }

    private void RefreshItemList(string? preferredId = null)
    {
        var selectedId = preferredId;
        if (string.IsNullOrWhiteSpace(selectedId) && lvItems.SelectedItems.Count > 0)
        {
            selectedId = lvItems.SelectedItems[0].Tag as string;
        }

        lvItems.BeginUpdate();
        try
        {
            lvItems.Items.Clear();
            foreach (var item in _data.Items.OrderBy(x => x.Order))
            {
                var mode = NormalizeMode(item.PageMode);
                var page = GetPageFileForEditor(item);
                var row = new ListViewItem(item.Order.ToString())
                {
                    Tag = item.Id
                };
                row.SubItems.Add(item.Enabled ? "是" : "否");
                row.SubItems.Add(item.CardTitle ?? "");
                row.SubItems.Add(mode);
                row.SubItems.Add(page);
                lvItems.Items.Add(row);
            }

            if (lvItems.Items.Count == 0)
            {
                ClearEditor();
                return;
            }

            var selected = false;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                foreach (ListViewItem listItem in lvItems.Items)
                {
                    if (string.Equals(listItem.Tag as string, selectedId, StringComparison.OrdinalIgnoreCase))
                    {
                        listItem.Selected = true;
                        listItem.EnsureVisible();
                        selected = true;
                        break;
                    }
                }
            }

            if (!selected)
            {
                lvItems.Items[0].Selected = true;
                lvItems.Items[0].EnsureVisible();
            }
        }
        finally
        {
            lvItems.EndUpdate();
        }
    }

    private SiteItem? GetSelectedItem()
    {
        if (lvItems.SelectedItems.Count == 0)
        {
            return null;
        }

        var id = lvItems.SelectedItems[0].Tag as string;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return _data.Items.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadEditorFromSelected()
    {
        var item = GetSelectedItem();
        if (item == null)
        {
            ClearEditor();
            return;
        }

        _loadingEditor = true;
        try
        {
            numOrder.Value = Math.Max(1, item.Order);
            chkEnabled.Checked = item.Enabled;
            txtSlug.Text = item.PageSlug ?? "";
            txtCardTitle.Text = item.CardTitle ?? "";
            txtPageFile.Text = GetPageFileForEditor(item);
            txtCardImage.Text = item.CardImage ?? "";
            txtCardAlt.Text = item.CardAlt ?? "";
            txtPageTitle.Text = item.PageTitle ?? "";
            txtPageSummary.Text = item.PageSummary ?? "";

            var mode = NormalizeMode(item.PageMode);
            cmbPageMode.SelectedItem = cmbPageMode.Items.Contains(mode) ? mode : "generated";
        }
        finally
        {
            _loadingEditor = false;
        }
    }

    private void ClearEditor()
    {
        _loadingEditor = true;
        try
        {
            numOrder.Value = 1;
            chkEnabled.Checked = true;
            txtSlug.Text = "";
            txtCardTitle.Text = "";
            cmbPageMode.SelectedItem = "external-link";
            txtPageFile.Text = "";
            txtCardImage.Text = "";
            txtCardAlt.Text = "";
            txtPageTitle.Text = "";
            txtPageSummary.Text = "";
        }
        finally
        {
            _loadingEditor = false;
        }
    }

    private void AddExternalFromQuick()
    {
        var url = txtQuickUrl.Text.Trim();
        if (!IsHttpUrl(url))
        {
            MessageBox.Show("请输入有效的外部链接（http/https）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var title = txtQuickTitle.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = GuessTitleFromUrl(url);
        }

        var slugBase = ToSafeSlug(title);
        var slug = GetUniqueSlug(slugBase);
        var cardImage = ImportImageForItem(txtQuickImage.Text, "cards", slug, "card", "./asset/image/cards/1.png");

        var item = new SiteItem
        {
            Id = slug,
            Enabled = true,
            Order = (_data.Items.Count == 0 ? 1 : _data.Items.Max(x => x.Order) + 1),
            CardTitle = title,
            CardImage = cardImage,
            CardAlt = title,
            PageSlug = slug,
            PageFile = url,
            PageMode = "external-link",
            PageTitle = title,
            PageSummary = "外部链接页面",
            PageLayout = "text-only",
            PageImage = cardImage,
            PageBody = new List<string> { "该卡片会跳转到外部网页。" }
        };

        _data.Items.Add(item);
        NormalizeOrders();

        if (!SaveDataFromUi(showMessage: false))
        {
            return;
        }

        txtQuickUrl.Text = "";
        txtQuickTitle.Text = "";
        txtQuickImage.Text = "";

        RefreshItemList(item.Id);
        SetStatus($"已新增外链卡片：{item.CardTitle}");
    }

    private bool ApplySelected(bool autoSave, bool showMessage)
    {
        if (_loadingEditor)
        {
            return false;
        }

        var item = GetSelectedItem();
        if (item == null)
        {
            if (showMessage)
            {
                MessageBox.Show("请先在左侧选中一条内容。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return false;
        }

        var cardTitle = txtCardTitle.Text.Trim();
        if (string.IsNullOrWhiteSpace(cardTitle))
        {
            MessageBox.Show("卡片标题不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var slug = string.IsNullOrWhiteSpace(txtSlug.Text)
            ? GetUniqueSlug(ToSafeSlug(cardTitle))
            : ToSafeSlug(txtSlug.Text);

        var mode = NormalizeMode(cmbPageMode.SelectedItem?.ToString());
        var pageFile = txtPageFile.Text.Trim().Trim('"');

        if (mode == "external-link" && !IsHttpUrl(pageFile))
        {
            MessageBox.Show("external-link 模式必须填写有效的 http/https 链接。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (mode == "generated" && string.IsNullOrWhiteSpace(pageFile))
        {
            pageFile = $"pages/content/{slug}.html";
        }

        if (mode == "external" && string.IsNullOrWhiteSpace(pageFile))
        {
            MessageBox.Show("external 模式必须填写本地页面文件路径。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var cardImage = ImportImageForItem(txtCardImage.Text, "cards", slug, "card", "./asset/image/cards/1.png");
        var cardAlt = txtCardAlt.Text.Trim();
        if (string.IsNullOrWhiteSpace(cardAlt))
        {
            cardAlt = cardTitle;
        }

        item.Order = (int)numOrder.Value;
        item.Enabled = chkEnabled.Checked;
        item.PageSlug = slug;
        item.CardTitle = cardTitle;
        item.CardImage = cardImage;
        item.CardAlt = cardAlt;
        item.PageMode = mode;
        item.PageFile = pageFile;
        item.PageTitle = string.IsNullOrWhiteSpace(txtPageTitle.Text) ? cardTitle : txtPageTitle.Text.Trim();
        item.PageSummary = string.IsNullOrWhiteSpace(txtPageSummary.Text) ? "这是一个页面。" : txtPageSummary.Text.Trim();
        item.PageLayout = string.IsNullOrWhiteSpace(item.PageLayout) ? "image-left" : item.PageLayout;
        item.PageImage = string.IsNullOrWhiteSpace(item.PageImage) ? cardImage : NormalizePathForSite(item.PageImage);
        item.PageBody ??= new List<string>();
        if (item.PageBody.Count == 0)
        {
            item.PageBody.Add("这里还没有正文内容。你可以后续再补充。");
        }

        NormalizeOrders();

        if (autoSave && !SaveDataFromUi(showMessage: false))
        {
            return false;
        }

        RefreshItemList(item.Id);
        SetStatus($"已更新：{item.CardTitle}");

        if (showMessage)
        {
            MessageBox.Show("修改已保存。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        return true;
    }

    private void DeleteSelected()
    {
        var item = GetSelectedItem();
        if (item == null)
        {
            return;
        }

        var confirm = MessageBox.Show($"确认删除 [{item.CardTitle}] 吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _data.Items.Remove(item);
        NormalizeOrders();

        if (!SaveDataFromUi(showMessage: false))
        {
            return;
        }

        RefreshItemList();
        SetStatus($"已删除：{item.CardTitle}");
    }

    private bool SaveDataFromUi(bool showMessage)
    {
        try
        {
            if (lvItems.SelectedItems.Count > 0 && !_loadingEditor)
            {
                if (!ApplySelected(autoSave: false, showMessage: false))
                {
                    return false;
                }
            }

            _data.Site ??= new SiteConfig();
            _data.Site.BrowserTitle = txtBrowserTitle.Text.Trim();
            _data.Site.BannerTitle = txtBannerTitle.Text.Trim();

            NormalizeOrders();
            var json = JsonSerializer.Serialize(_data, _jsonOptions);
            File.WriteAllText(_dataPath, json, new UTF8Encoding(false));

            SetStatus($"已保存：{DateTime.Now:HH:mm:ss}");
            if (showMessage)
            {
                MessageBox.Show("保存成功。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("保存失败");
            return false;
        }
    }

    private void SaveAndPublish()
    {
        if (!SaveDataFromUi(showMessage: false))
        {
            return;
        }

        var result = RunPowerShellBuild();
        if (result.ExitCode == 0)
        {
            MessageBox.Show("保存并发布成功。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetStatus("发布成功");
        }
        else
        {
            MessageBox.Show($"发布失败：\n{result.Output}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("发布失败");
        }
    }

    private void RunBuildOnly()
    {
        var result = RunPowerShellBuild();
        if (result.ExitCode == 0)
        {
            MessageBox.Show("发布完成。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetStatus("发布成功");
        }
        else
        {
            MessageBox.Show($"发布失败：\n{result.Output}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("发布失败");
        }
    }

    private (int ExitCode, string Output) RunPowerShellBuild()
    {
        if (!File.Exists(_managerScriptPath))
        {
            return (1, $"找不到脚本：{_managerScriptPath}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{_managerScriptPath}\" -Mode Build",
            WorkingDirectory = _projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return (1, "无法启动 PowerShell 进程。");
        }

        var output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.Trim());
    }

    private string? SelectImageFile()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Image Files (*.png;*.jpg;*.jpeg;*.webp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.gif|All Files (*.*)|*.*",
            Title = "选择封面图片"
        };

        return ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
    }

    private void NormalizeOrders()
    {
        var sorted = _data.Items.OrderBy(x => x.Order).ThenBy(x => x.CardTitle).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].Order = i + 1;
        }
        _data.Items = sorted;
    }

    private string GetUniqueSlug(string baseSlug)
    {
        var slug = string.IsNullOrWhiteSpace(baseSlug) ? $"item-{DateTime.Now:yyyyMMddHHmmss}" : baseSlug;
        var set = _data.Items
            .Select(x => x.PageSlug)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var index = 2;
        var candidate = slug;
        while (set.Contains(candidate))
        {
            candidate = $"{slug}-{index}";
            index++;
        }
        return candidate;
    }

    private static string ToSafeSlug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return $"item-{DateTime.Now:yyyyMMddHHmmss}";
        }

        var chars = text.Trim().ToLowerInvariant()
            .Select(c => (char.IsLetterOrDigit(c) || c == '-') ? c : '-')
            .ToArray();
        var raw = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(raw) ? $"item-{DateTime.Now:yyyyMMddHHmmss}" : raw;
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = (mode ?? "").Trim().ToLowerInvariant();
        return normalized switch
        {
            "external-link" => "external-link",
            "external" => "external",
            _ => "generated"
        };
    }

    private static bool IsHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string GuessTitleFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
            return host;
        }
        catch
        {
            return "外部网页";
        }
    }

    private string GetPageFileForEditor(SiteItem item)
    {
        var mode = NormalizeMode(item.PageMode);
        if (!string.IsNullOrWhiteSpace(item.PageFile))
        {
            return item.PageFile.Trim();
        }

        return mode == "generated"
            ? $"pages/content/{item.PageSlug}.html"
            : "";
    }

    private string ImportImageForItem(string? rawInput, string subFolder, string slug, string suffix, string fallback)
    {
        var input = (rawInput ?? "").Trim().Trim('"');
        var normalizedFallback = NormalizePathForSite(fallback);
        if (string.IsNullOrWhiteSpace(input))
        {
            return normalizedFallback;
        }

        if (IsHttpUrl(input))
        {
            return input;
        }

        if (!Path.IsPathRooted(input))
        {
            var normalizedRelative = NormalizePathForSite(input);
            var relativeFile = normalizedRelative.TrimStart('.').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var relativeFullPath = Path.Combine(_projectRoot, relativeFile);
            if (!string.IsNullOrWhiteSpace(normalizedRelative) && File.Exists(relativeFullPath))
            {
                return normalizedRelative;
            }
        }

        var sourcePath = ResolveFilePath(input);
        if (sourcePath != null)
        {
            var ext = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".png";
            }

            var safeSlug = ToSafeSlug(string.IsNullOrWhiteSpace(slug) ? "item" : slug);
            var relativePath = $"asset/image/{subFolder}/{safeSlug}-{suffix}{ext}".ToLowerInvariant();
            var relativeSystemPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var targetPath = Path.Combine(_projectRoot, relativeSystemPath);
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var sourceFullPath = Path.GetFullPath(sourcePath);
            var targetFullPath = Path.GetFullPath(targetPath);
            if (!string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourceFullPath, targetPath, true);
            }

            return $"./{relativePath}";
        }

        var normalized = NormalizePathForSite(input);
        return string.IsNullOrWhiteSpace(normalized) ? normalizedFallback : normalized;
    }

    private string? ResolveFilePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (Path.IsPathRooted(input))
        {
            return File.Exists(input) ? Path.GetFullPath(input) : null;
        }

        var candidate = Path.Combine(_projectRoot, input);
        if (File.Exists(candidate))
        {
            return Path.GetFullPath(candidate);
        }

        return null;
    }

    private static string NormalizePathForSite(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var trimmed = value.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "";
        }

        if (IsHttpUrl(trimmed))
        {
            return trimmed;
        }

        var normalized = trimmed.Replace('\\', '/');
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            return normalized;
        }

        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            return $".{normalized}";
        }

        if (normalized.Length >= 3 && char.IsLetter(normalized[0]) && normalized[1] == ':' && normalized[2] == '/')
        {
            return "";
        }

        return $"./{normalized}";
    }

    private void SetStatus(string text)
    {
        lblStatus.Text = $"[{DateTime.Now:HH:mm:ss}] {text}";
    }

    private static string FindProjectRoot()
    {
        var envRoot = Environment.GetEnvironmentVariable("SITE_MANAGER_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot))
        {
            var envCheck = Path.Combine(envRoot, "content", "site-content.json");
            if (File.Exists(envCheck))
            {
                return Path.GetFullPath(envRoot);
            }
        }

        var starts = new[]
        {
            new DirectoryInfo(Directory.GetCurrentDirectory()),
            new DirectoryInfo(AppContext.BaseDirectory)
        };

        foreach (var start in starts)
        {
            var dir = start;
            while (dir != null)
            {
                var check = Path.Combine(dir.FullName, "content", "site-content.json");
                if (File.Exists(check))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException("未找到项目根目录（缺少 content/site-content.json）。");
    }
}

public class SiteData
{
    [JsonPropertyName("site")]
    public SiteConfig Site { get; set; } = new();

    [JsonPropertyName("items")]
    public List<SiteItem> Items { get; set; } = new();
}

public class SiteConfig
{
    [JsonPropertyName("browserTitle")]
    public string BrowserTitle { get; set; } = "";

    [JsonPropertyName("bannerTitle")]
    public string BannerTitle { get; set; } = "";
}

public class SiteItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("order")]
    public int Order { get; set; } = 1;

    [JsonPropertyName("cardTitle")]
    public string CardTitle { get; set; } = "";

    [JsonPropertyName("cardImage")]
    public string CardImage { get; set; } = "";

    [JsonPropertyName("cardAlt")]
    public string CardAlt { get; set; } = "";

    [JsonPropertyName("pageSlug")]
    public string PageSlug { get; set; } = "";

    [JsonPropertyName("pageFile")]
    public string? PageFile { get; set; }

    [JsonPropertyName("pageMode")]
    public string? PageMode { get; set; }

    [JsonPropertyName("pageTitle")]
    public string PageTitle { get; set; } = "";

    [JsonPropertyName("pageSummary")]
    public string PageSummary { get; set; } = "";

    [JsonPropertyName("pageLayout")]
    public string PageLayout { get; set; } = "image-left";

    [JsonPropertyName("pageImage")]
    public string PageImage { get; set; } = "";

    [JsonPropertyName("pageBody")]
    public List<string> PageBody { get; set; } = new();
}
