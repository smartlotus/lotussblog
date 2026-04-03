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

    private ListView lvSections = null!;
    private Button btnSectionAdd = null!;
    private Button btnSectionEdit = null!;
    private Button btnSectionMoveUp = null!;
    private Button btnSectionMoveDown = null!;
    private Button btnSectionDelete = null!;

    private ComboBox cmbItemSectionFilter = null!;
    private ListView lvItems = null!;
    private Button btnMoveUp = null!;
    private Button btnMoveDown = null!;
    private Button btnDelete = null!;
    private Button btnReload = null!;

    private NumericUpDown numOrder = null!;
    private CheckBox chkEnabled = null!;
    private TextBox txtSlug = null!;
    private TextBox txtCardTitle = null!;
    private ComboBox cmbSection = null!;
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
            RowCount = 3,
            Padding = new Padding(0)
        };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
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

        var sectionGroup = new GroupBox
        {
            Text = "步骤 3：分类板块管理（前台仅显示分类名称）",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        leftLayout.Controls.Add(sectionGroup, 0, 1);

        var sectionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        sectionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        sectionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        sectionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        sectionGroup.Controls.Add(sectionLayout);

        lvSections = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            GridLines = true,
            HideSelection = false
        };
        lvSections.Columns.Add("序", 44);
        lvSections.Columns.Add("显示", 52);
        lvSections.Columns.Add("分类名称", 140);
        lvSections.Columns.Add("ID", 110);
        lvSections.Columns.Add("卡片数", 64);
        lvSections.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingEditor) return;
            var selectedSection = GetSelectedSection();
            if (selectedSection == null) return;
            RefreshItemSectionFilter(selectedSection.Id);
            RefreshItemList();
        };
        sectionLayout.Controls.Add(lvSections, 0, 0);

        var sectionButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1
        };
        sectionButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
        sectionButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
        sectionButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
        sectionButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
        sectionButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        sectionLayout.Controls.Add(sectionButtons, 0, 1);

        btnSectionAdd = new Button
        {
            Text = "新增分类",
            Dock = DockStyle.Fill,
            BackColor = Color.Honeydew
        };
        btnSectionAdd.Click += (_, _) => AddSection();
        sectionButtons.Controls.Add(btnSectionAdd, 0, 0);

        btnSectionEdit = new Button
        {
            Text = "编辑分类",
            Dock = DockStyle.Fill
        };
        btnSectionEdit.Click += (_, _) => EditSelectedSection();
        sectionButtons.Controls.Add(btnSectionEdit, 1, 0);

        btnSectionMoveUp = new Button
        {
            Text = "上移",
            Dock = DockStyle.Fill
        };
        btnSectionMoveUp.Click += (_, _) => MoveSelectedSection(-1);
        sectionButtons.Controls.Add(btnSectionMoveUp, 2, 0);

        btnSectionMoveDown = new Button
        {
            Text = "下移",
            Dock = DockStyle.Fill
        };
        btnSectionMoveDown.Click += (_, _) => MoveSelectedSection(1);
        sectionButtons.Controls.Add(btnSectionMoveDown, 3, 0);

        btnSectionDelete = new Button
        {
            Text = "删除分类",
            Dock = DockStyle.Fill,
            BackColor = Color.MistyRose
        };
        btnSectionDelete.Click += (_, _) => DeleteSelectedSection();
        sectionButtons.Controls.Add(btnSectionDelete, 4, 0);

        var sectionHint = new Label
        {
            Text = "进入分类后将通过页面返回箭头回到上级，不再使用“返回分类卡片”。",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleLeft
        };
        sectionLayout.Controls.Add(sectionHint, 0, 2);

        var listGroup = new GroupBox
        {
            Text = "步骤 4：内容列表（选中后右侧编辑）",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        leftLayout.Controls.Add(listGroup, 0, 2);

        var listLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        listGroup.Controls.Add(listLayout);

        var filterBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        listLayout.Controls.Add(filterBar, 0, 0);

        var lblFilter = new Label
        {
            Text = "分类筛选",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        filterBar.Controls.Add(lblFilter, 0, 0);

        cmbItemSectionFilter = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbItemSectionFilter.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingEditor) return;
            RefreshItemList();
        };
        filterBar.Controls.Add(cmbItemSectionFilter, 1, 0);

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
        lvItems.Columns.Add("分类", 120);
        lvItems.Columns.Add("模式", 92);
        lvItems.Columns.Add("链接/文件", 190);
        lvItems.SelectedIndexChanged += (_, _) => LoadEditorFromSelected();
        listLayout.Controls.Add(lvItems, 0, 1);

        var rowButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        rowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        rowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        rowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        rowButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        listLayout.Controls.Add(rowButtons, 0, 2);

        btnMoveUp = new Button
        {
            Text = "上移",
            Dock = DockStyle.Fill
        };
        btnMoveUp.Click += (_, _) => MoveSelectedItem(-1);
        rowButtons.Controls.Add(btnMoveUp, 0, 0);

        btnMoveDown = new Button
        {
            Text = "下移",
            Dock = DockStyle.Fill
        };
        btnMoveDown.Click += (_, _) => MoveSelectedItem(1);
        rowButtons.Controls.Add(btnMoveDown, 1, 0);

        btnDelete = new Button
        {
            Text = "删除选中",
            Dock = DockStyle.Fill,
            BackColor = Color.MistyRose
        };
        btnDelete.Click += (_, _) => DeleteSelected();
        rowButtons.Controls.Add(btnDelete, 2, 0);

        btnReload = new Button
        {
            Text = "重新加载",
            Dock = DockStyle.Fill
        };
        btnReload.Click += (_, _) => LoadData();
        rowButtons.Controls.Add(btnReload, 3, 0);
    }

    private void BuildRightPanel(Control parent)
    {
        var group = new GroupBox
        {
            Text = "步骤 5：编辑选中卡片（改完点“应用并保存”）",
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
        AddEditorRow(editor, row++, "所属分类", out cmbSection);
        AddEditorRow(editor, row++, "页面模式", out cmbPageMode);
        cmbSection.DropDownStyle = ComboBoxStyle.DropDownList;
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
            SetStatus($"已加载：{_data.Sections.Count} 个分类，{_data.Items.Count} 个卡片");
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
        _data.Sections ??= new List<SiteSection>();
        _data.Items ??= new List<SiteItem>();

        var normalizedSections = new List<SiteSection>();
        var usedSectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in _data.Sections)
        {
            var title = string.IsNullOrWhiteSpace(section.Title)
                ? "未命名分类"
                : section.Title.Trim();
            var baseId = string.IsNullOrWhiteSpace(section.Id)
                ? ToSafeSectionId(title)
                : ToSafeSectionId(section.Id);
            var id = baseId;
            var suffix = 2;
            while (usedSectionIds.Contains(id))
            {
                id = $"{baseId}-{suffix}";
                suffix += 1;
            }

            usedSectionIds.Add(id);

            normalizedSections.Add(new SiteSection
            {
                Id = id,
                Enabled = section.Enabled,
                Order = section.Order <= 0 ? normalizedSections.Count + 1 : section.Order,
                Title = title,
                Description = (section.Description ?? "").Trim(),
                Cover = ImportImageForItem(section.Cover, "cards", id, "section", "./asset/image/cards/1.png")
            });
        }

        if (normalizedSections.Count == 0)
        {
            normalizedSections.Add(new SiteSection
            {
                Id = "projects",
                Enabled = true,
                Order = 1,
                Title = "项目板块",
                Description = "进入项目合集并跳转到不同站点",
                Cover = "./asset/image/cards/pickup-card.jpg"
            });
            normalizedSections.Add(new SiteSection
            {
                Id = "author",
                Enabled = true,
                Order = 2,
                Title = "关于作者板块",
                Description = "进入作者相关页面与内容索引",
                Cover = "./asset/image/cards/author-card.png"
            });
        }

        _data.Sections = normalizedSections;
        NormalizeSectionOrders();
        var validSectionIds = _data.Sections
            .Select(x => x.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var defaultSectionId = GetDefaultSectionId();

        foreach (var item in _data.Items)
        {
            item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim();
            item.PageSlug = string.IsNullOrWhiteSpace(item.PageSlug) ? ToSafeSlug(item.Id) : ToSafeSlug(item.PageSlug);
            item.CardTitle = string.IsNullOrWhiteSpace(item.CardTitle)
                ? (string.IsNullOrWhiteSpace(item.PageTitle) ? item.PageSlug : item.PageTitle)
                : item.CardTitle.Trim();
            item.CardAlt = string.IsNullOrWhiteSpace(item.CardAlt) ? item.CardTitle : item.CardAlt.Trim();
            item.CardImage = ImportImageForItem(item.CardImage, "cards", item.PageSlug, "card", "./asset/image/cards/1.png");

            item.PageMode = NormalizeMode(item.PageMode);
            if (string.IsNullOrWhiteSpace(item.PageFile) && item.PageMode == "generated")
            {
                item.PageFile = $"pages/content/{item.PageSlug}.html";
            }

            item.PageTitle = string.IsNullOrWhiteSpace(item.PageTitle) ? item.CardTitle : item.PageTitle.Trim();
            item.PageSummary = string.IsNullOrWhiteSpace(item.PageSummary) ? "这是一个页面。" : item.PageSummary.Trim();
            item.PageLayout = string.IsNullOrWhiteSpace(item.PageLayout) ? "image-left" : item.PageLayout;
            item.PageImage = string.IsNullOrWhiteSpace(item.PageImage)
                ? item.CardImage
                : ImportImageForItem(item.PageImage, "pages", item.PageSlug, "page", item.CardImage);
            item.PageBody ??= new List<string>();
            if (item.PageBody.Count == 0)
            {
                item.PageBody.Add("这里还没有正文内容。你可以后续再补充。");
            }

            if (item.Order <= 0)
            {
                item.Order = 1;
            }

            item.SectionId = string.IsNullOrWhiteSpace(item.SectionId)
                ? defaultSectionId
                : ToSafeSectionId(item.SectionId);
            if (!validSectionIds.Contains(item.SectionId))
            {
                item.SectionId = defaultSectionId;
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
            RefreshSectionList();
            RefreshSectionCombo();
            RefreshItemSectionFilter();
            RefreshItemList();
        }
        finally
        {
            _loadingEditor = false;
        }
    }

    private void RefreshSectionList(string? preferredId = null)
    {
        if (lvSections == null)
        {
            return;
        }

        var selectedId = preferredId;
        if (string.IsNullOrWhiteSpace(selectedId) && lvSections.SelectedItems.Count > 0)
        {
            selectedId = lvSections.SelectedItems[0].Tag as string;
        }

        lvSections.BeginUpdate();
        try
        {
            lvSections.Items.Clear();
            foreach (var section in _data.Sections.OrderBy(x => x.Order))
            {
                var cardCount = _data.Items.Count(item =>
                    string.Equals(item.SectionId, section.Id, StringComparison.OrdinalIgnoreCase));

                var row = new ListViewItem(section.Order.ToString())
                {
                    Tag = section.Id
                };
                row.SubItems.Add(section.Enabled ? "是" : "否");
                row.SubItems.Add(section.Title ?? "");
                row.SubItems.Add(section.Id ?? "");
                row.SubItems.Add(cardCount.ToString());
                lvSections.Items.Add(row);
            }

            if (lvSections.Items.Count == 0)
            {
                return;
            }

            var selected = false;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                foreach (ListViewItem listItem in lvSections.Items)
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
                lvSections.Items[0].Selected = true;
                lvSections.Items[0].EnsureVisible();
            }
        }
        finally
        {
            lvSections.EndUpdate();
        }
    }

    private void RefreshSectionCombo()
    {
        if (cmbSection == null)
        {
            return;
        }

        var selectedId = (cmbSection.SelectedItem as SectionComboItem)?.Id;
        cmbSection.BeginUpdate();
        try
        {
            cmbSection.Items.Clear();
            foreach (var section in _data.Sections.OrderBy(x => x.Order))
            {
                var title = section.Enabled ? section.Title : $"{section.Title}（已隐藏）";
                cmbSection.Items.Add(new SectionComboItem(section.Id, title));
            }

            if (cmbSection.Items.Count == 0)
            {
                return;
            }

            SelectSectionInCombo(selectedId);
        }
        finally
        {
            cmbSection.EndUpdate();
        }
    }

    private void RefreshItemSectionFilter(string? preferredId = null)
    {
        if (cmbItemSectionFilter == null)
        {
            return;
        }

        var selectedId = preferredId;
        if (string.IsNullOrWhiteSpace(selectedId))
        {
            selectedId = (cmbItemSectionFilter.SelectedItem as SectionComboItem)?.Id;
        }

        cmbItemSectionFilter.BeginUpdate();
        try
        {
            cmbItemSectionFilter.Items.Clear();
            cmbItemSectionFilter.Items.Add(new SectionComboItem("", "全部分类"));
            foreach (var section in _data.Sections.OrderBy(x => x.Order))
            {
                cmbItemSectionFilter.Items.Add(new SectionComboItem(section.Id, section.Title));
            }

            if (cmbItemSectionFilter.Items.Count == 0)
            {
                return;
            }

            var matched = false;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                for (var i = 0; i < cmbItemSectionFilter.Items.Count; i++)
                {
                    if (cmbItemSectionFilter.Items[i] is SectionComboItem section &&
                        string.Equals(section.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                    {
                        cmbItemSectionFilter.SelectedIndex = i;
                        matched = true;
                        break;
                    }
                }
            }

            if (!matched)
            {
                cmbItemSectionFilter.SelectedIndex = 0;
            }
        }
        finally
        {
            cmbItemSectionFilter.EndUpdate();
        }
    }

    private string? GetCurrentItemFilterSectionId()
    {
        var value = (cmbItemSectionFilter.SelectedItem as SectionComboItem)?.Id;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private List<SiteItem> GetVisibleItemsForList()
    {
        var filterSectionId = GetCurrentItemFilterSectionId();
        if (!string.IsNullOrWhiteSpace(filterSectionId))
        {
            return _data.Items
                .Where(x => string.Equals(x.SectionId, filterSectionId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Order)
                .ThenBy(x => x.CardTitle)
                .ToList();
        }

        var orderedSections = _data.Sections.OrderBy(x => x.Order).ToList();
        var result = new List<SiteItem>();
        foreach (var section in orderedSections)
        {
            var sectionItems = _data.Items
                .Where(x => string.Equals(x.SectionId, section.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Order)
                .ThenBy(x => x.CardTitle)
                .ToList();
            result.AddRange(sectionItems);
        }

        var sectionIds = orderedSections
            .Select(x => x.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ungrouped = _data.Items
            .Where(x => string.IsNullOrWhiteSpace(x.SectionId) || !sectionIds.Contains(x.SectionId))
            .OrderBy(x => x.Order)
            .ThenBy(x => x.CardTitle)
            .ToList();
        result.AddRange(ungrouped);

        return result;
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
            var visibleItems = GetVisibleItemsForList();
            for (var i = 0; i < visibleItems.Count; i++)
            {
                var item = visibleItems[i];
                var mode = NormalizeMode(item.PageMode);
                var page = GetPageFileForEditor(item);
                var row = new ListViewItem((i + 1).ToString())
                {
                    Tag = item.Id
                };
                row.SubItems.Add(item.Enabled ? "是" : "否");
                row.SubItems.Add(item.CardTitle ?? "");
                row.SubItems.Add(GetSectionTitle(item.SectionId));
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

    private SiteSection? GetSelectedSection()
    {
        if (lvSections == null || lvSections.SelectedItems.Count == 0)
        {
            return null;
        }

        var id = lvSections.SelectedItems[0].Tag as string;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return _data.Sections.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
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
            SelectSectionInCombo(item.SectionId);
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
            SelectSectionInCombo(GetDefaultSectionId());
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
        var sectionId = GetDefaultSectionId();

        var item = new SiteItem
        {
            Id = slug,
            SectionId = sectionId,
            Enabled = true,
            Order = GetNextItemOrderForSection(sectionId),
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

        RefreshSectionList();
        RefreshSectionCombo();
        RefreshItemSectionFilter(item.SectionId);
        RefreshItemList(item.Id);
        SetStatus($"已新增外链卡片：{item.CardTitle}");
    }

    private void AddSection()
    {
        var nextOrder = (_data.Sections.Count == 0 ? 1 : _data.Sections.Max(x => x.Order) + 1);
        var draft = new SiteSection
        {
            Id = GetUniqueSectionId("section"),
            Enabled = true,
            Order = nextOrder,
            Title = "新分类",
            Description = "",
            Cover = "./asset/image/cards/1.png"
        };

        var created = ShowSectionEditorDialog(draft, isNew: true);
        if (created == null)
        {
            return;
        }

        _data.Sections.Add(created);
        NormalizeSectionOrders();
        EnsureItemsHaveValidSections();

        if (!SaveDataWithoutApply(showMessage: false))
        {
            return;
        }

        RefreshSectionList(created.Id);
        RefreshSectionCombo();
        RefreshItemSectionFilter();
        RefreshItemList();
        SetStatus($"已新增分类：{created.Title}");
    }

    private void EditSelectedSection()
    {
        var section = GetSelectedSection();
        if (section == null)
        {
            MessageBox.Show("请先选中一个分类。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var draft = new SiteSection
        {
            Id = section.Id,
            Enabled = section.Enabled,
            Order = section.Order,
            Title = section.Title,
            Description = section.Description,
            Cover = section.Cover
        };

        var updated = ShowSectionEditorDialog(draft, isNew: false);
        if (updated == null)
        {
            return;
        }

        section.Enabled = updated.Enabled;
        section.Order = updated.Order;
        section.Title = updated.Title;
        section.Description = updated.Description;
        section.Cover = updated.Cover;
        NormalizeSectionOrders();
        EnsureItemsHaveValidSections();

        if (!SaveDataWithoutApply(showMessage: false))
        {
            return;
        }

        RefreshSectionList(section.Id);
        RefreshSectionCombo();
        RefreshItemSectionFilter();
        RefreshItemList();
        SetStatus($"已更新分类：{section.Title}");
    }

    private void DeleteSelectedSection()
    {
        var section = GetSelectedSection();
        if (section == null)
        {
            return;
        }

        if (_data.Sections.Count <= 1)
        {
            MessageBox.Show("至少需要保留一个分类，无法删除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var fallback = _data.Sections
            .Where(x => !string.Equals(x.Id, section.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Order)
            .FirstOrDefault();

        if (fallback == null)
        {
            MessageBox.Show("未找到可迁移的分类，无法删除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var affectedItems = _data.Items
            .Where(x => string.Equals(x.SectionId, section.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var confirm = MessageBox.Show(
            $"确认删除分类 [{section.Title}] 吗？\n该分类下 {affectedItems.Count} 个卡片将自动迁移到 [{fallback.Title}]。",
            "确认删除分类",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        foreach (var item in affectedItems)
        {
            item.SectionId = fallback.Id;
        }

        _data.Sections.Remove(section);
        NormalizeSectionOrders();
        EnsureItemsHaveValidSections();

        if (!SaveDataWithoutApply(showMessage: false))
        {
            return;
        }

        RefreshSectionList(fallback.Id);
        RefreshSectionCombo();
        RefreshItemSectionFilter(fallback.Id);
        RefreshItemList();
        SetStatus($"已删除分类：{section.Title}");
    }

    private void MoveSelectedSection(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        var section = GetSelectedSection();
        if (section == null)
        {
            MessageBox.Show("请先选中一个分类。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var ordered = _data.Sections.OrderBy(x => x.Order).ToList();
        var index = ordered.FindIndex(x => string.Equals(x.Id, section.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var targetIndex = index + delta;
        if (targetIndex < 0 || targetIndex >= ordered.Count)
        {
            return;
        }

        var current = ordered[index];
        var target = ordered[targetIndex];
        (current.Order, target.Order) = (target.Order, current.Order);
        NormalizeSectionOrders();
        NormalizeOrders();

        if (!SaveDataWithoutApply(showMessage: false))
        {
            return;
        }

        RefreshSectionList(current.Id);
        RefreshSectionCombo();
        RefreshItemSectionFilter(current.Id);
        RefreshItemList();
        SetStatus($"已调整分类顺序：{current.Title}");
    }

    private void MoveSelectedItem(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        var item = GetSelectedItem();
        if (item == null)
        {
            MessageBox.Show("请先选中一个卡片。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var sectionId = item.SectionId;
        var sectionItems = _data.Items
            .Where(x => string.Equals(x.SectionId, sectionId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Order)
            .ThenBy(x => x.CardTitle)
            .ToList();

        var index = sectionItems.FindIndex(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var targetIndex = index + delta;
        if (targetIndex < 0 || targetIndex >= sectionItems.Count)
        {
            return;
        }

        var target = sectionItems[targetIndex];
        (item.Order, target.Order) = (target.Order, item.Order);
        NormalizeOrders();

        if (!SaveDataWithoutApply(showMessage: false))
        {
            return;
        }

        RefreshSectionList();
        RefreshItemList(item.Id);
        SetStatus($"已调整卡片顺序：{item.CardTitle}");
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

        var sectionId = GetSelectedSectionIdFromCombo();
        if (string.IsNullOrWhiteSpace(sectionId))
        {
            MessageBox.Show("请先选择所属分类。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        item.SectionId = sectionId;
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

        RefreshSectionList(sectionId);
        RefreshSectionCombo();
        RefreshItemSectionFilter(sectionId);
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

        RefreshSectionList();
        RefreshItemSectionFilter(GetCurrentItemFilterSectionId());
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
            return SaveDataWithoutApply(showMessage);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("保存失败");
            return false;
        }
    }

    private bool SaveDataWithoutApply(bool showMessage)
    {
        try
        {
            _data.Site ??= new SiteConfig();
            _data.Site.BrowserTitle = txtBrowserTitle.Text.Trim();
            _data.Site.BannerTitle = txtBannerTitle.Text.Trim();

            NormalizeSectionOrders();
            EnsureItemsHaveValidSections();
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
        var normalized = new List<SiteItem>();
        var orderedSections = _data.Sections.OrderBy(x => x.Order).ToList();

        foreach (var section in orderedSections)
        {
            var sectionItems = _data.Items
                .Where(x => string.Equals(x.SectionId, section.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Order)
                .ThenBy(x => x.CardTitle)
                .ToList();
            for (var i = 0; i < sectionItems.Count; i++)
            {
                sectionItems[i].Order = i + 1;
            }

            normalized.AddRange(sectionItems);
        }

        var sectionIds = orderedSections
            .Select(x => x.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ungroupedItems = _data.Items
            .Where(x => string.IsNullOrWhiteSpace(x.SectionId) || !sectionIds.Contains(x.SectionId))
            .OrderBy(x => x.Order)
            .ThenBy(x => x.CardTitle)
            .ToList();
        for (var i = 0; i < ungroupedItems.Count; i++)
        {
            ungroupedItems[i].Order = i + 1;
        }

        normalized.AddRange(ungroupedItems);
        _data.Items = normalized;
    }

    private void NormalizeSectionOrders()
    {
        var sorted = _data.Sections.OrderBy(x => x.Order).ThenBy(x => x.Title).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].Order = i + 1;
        }
        _data.Sections = sorted;
    }

    private void EnsureItemsHaveValidSections()
    {
        if (_data.Sections.Count == 0)
        {
            _data.Sections.Add(new SiteSection
            {
                Id = "projects",
                Enabled = true,
                Order = 1,
                Title = "项目板块",
                Description = "进入项目合集并跳转到不同站点",
                Cover = "./asset/image/cards/pickup-card.jpg"
            });
            NormalizeSectionOrders();
        }

        var defaultSectionId = GetDefaultSectionId();
        var validSectionIds = _data.Sections
            .Select(x => x.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in _data.Items)
        {
            item.SectionId = string.IsNullOrWhiteSpace(item.SectionId)
                ? defaultSectionId
                : ToSafeSectionId(item.SectionId);
            if (!validSectionIds.Contains(item.SectionId))
            {
                item.SectionId = defaultSectionId;
            }
        }
    }

    private string GetDefaultSectionId()
    {
        var section = _data.Sections
            .OrderBy(x => x.Order)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Id))
            ?? _data.Sections.FirstOrDefault();
        return string.IsNullOrWhiteSpace(section?.Id) ? "projects" : section.Id;
    }

    private int GetNextItemOrderForSection(string? sectionId)
    {
        var normalizedSectionId = string.IsNullOrWhiteSpace(sectionId) ? GetDefaultSectionId() : sectionId;
        var maxOrder = _data.Items
            .Where(x => string.Equals(x.SectionId, normalizedSectionId, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Order)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(0, maxOrder) + 1;
    }

    private string GetSectionTitle(string? sectionId)
    {
        var section = _data.Sections.FirstOrDefault(x =>
            string.Equals(x.Id, sectionId, StringComparison.OrdinalIgnoreCase));
        return section?.Title ?? "未分组";
    }

    private void SelectSectionInCombo(string? sectionId)
    {
        if (cmbSection == null || cmbSection.Items.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(sectionId))
        {
            for (var i = 0; i < cmbSection.Items.Count; i++)
            {
                if (cmbSection.Items[i] is SectionComboItem item &&
                    string.Equals(item.Id, sectionId, StringComparison.OrdinalIgnoreCase))
                {
                    cmbSection.SelectedIndex = i;
                    return;
                }
            }
        }

        cmbSection.SelectedIndex = 0;
    }

    private string? GetSelectedSectionIdFromCombo()
    {
        return (cmbSection.SelectedItem as SectionComboItem)?.Id;
    }

    private string GetUniqueSectionId(string? baseId)
    {
        var seed = ToSafeSectionId(baseId);
        if (string.IsNullOrWhiteSpace(seed))
        {
            seed = $"section-{DateTime.Now:yyyyMMddHHmmss}";
        }

        var set = _data.Sections
            .Select(x => x.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidate = seed;
        var index = 2;
        while (set.Contains(candidate))
        {
            candidate = $"{seed}-{index}";
            index += 1;
        }

        return candidate;
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

    private static string ToSafeSectionId(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "section";
        }

        var chars = text.Trim().ToLowerInvariant()
            .Select(c => (char.IsLetterOrDigit(c) || c == '-') ? c : '-')
            .ToArray();
        var raw = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(raw) ? "section" : raw;
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

    private SiteSection? ShowSectionEditorDialog(SiteSection draft, bool isNew)
    {
        var edited = draft;

        using var form = new Form
        {
            Text = isNew ? "新增分类" : "编辑分类",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(560, 360)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 7,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        for (var i = 0; i < 6; i++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 4 ? 86 : 38));
        }
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        form.Controls.Add(root);

        var txtId = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Text = draft.Id
        };
        var txtTitle = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = draft.Title
        };
        var txtCover = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = draft.Cover
        };
        var txtDesc = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Text = draft.Description
        };
        var numOrderLocal = new NumericUpDown
        {
            Dock = DockStyle.Left,
            Width = 140,
            Minimum = 1,
            Maximum = 99999,
            Value = Math.Max(1, draft.Order)
        };
        var chkEnabledLocal = new CheckBox
        {
            Dock = DockStyle.Left,
            Width = 140,
            Text = "启用分类",
            Checked = draft.Enabled
        };

        root.Controls.Add(CreateFieldLabel("分类 ID"), 0, 0);
        root.Controls.Add(txtId, 1, 0);
        root.Controls.Add(new Label(), 2, 0);

        root.Controls.Add(CreateFieldLabel("分类名称"), 0, 1);
        root.Controls.Add(txtTitle, 1, 1);
        root.Controls.Add(new Label(), 2, 1);

        root.Controls.Add(CreateFieldLabel("封面图"), 0, 2);
        root.Controls.Add(txtCover, 1, 2);
        var btnBrowseLocal = new Button
        {
            Text = "选图",
            Dock = DockStyle.Fill
        };
        btnBrowseLocal.Click += (_, _) =>
        {
            var selected = SelectImageFile();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                txtCover.Text = selected;
            }
        };
        root.Controls.Add(btnBrowseLocal, 2, 2);

        root.Controls.Add(CreateFieldLabel("排序"), 0, 3);
        root.Controls.Add(numOrderLocal, 1, 3);
        root.Controls.Add(new Label(), 2, 3);

        root.Controls.Add(CreateFieldLabel("分类描述"), 0, 4);
        root.Controls.Add(txtDesc, 1, 4);
        root.Controls.Add(new Label(), 2, 4);

        root.Controls.Add(CreateFieldLabel("状态"), 0, 5);
        root.Controls.Add(chkEnabledLocal, 1, 5);
        root.Controls.Add(new Label(), 2, 5);

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        root.Controls.Add(buttons, 1, 6);

        var btnCancel = new Button
        {
            Text = "取消",
            Dock = DockStyle.Fill
        };
        btnCancel.Click += (_, _) => form.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(btnCancel, 0, 0);

        var btnOk = new Button
        {
            Text = "确定",
            Dock = DockStyle.Fill,
            BackColor = Color.Honeydew
        };
        btnOk.Click += (_, _) =>
        {
            var title = txtTitle.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show(form, "分类名称不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var id = isNew
                ? GetUniqueSectionId(title)
                : draft.Id;

            edited = new SiteSection
            {
                Id = id,
                Enabled = chkEnabledLocal.Checked,
                Order = (int)numOrderLocal.Value,
                Title = title,
                Description = txtDesc.Text.Trim(),
                Cover = ImportImageForItem(txtCover.Text, "cards", id, "section", "./asset/image/cards/1.png")
            };

            form.DialogResult = DialogResult.OK;
        };
        buttons.Controls.Add(btnOk, 1, 0);

        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        return form.ShowDialog(this) == DialogResult.OK ? edited : null;
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

    [JsonPropertyName("sections")]
    public List<SiteSection> Sections { get; set; } = new();

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

    [JsonPropertyName("sectionId")]
    public string SectionId { get; set; } = "";

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

public class SiteSection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("order")]
    public int Order { get; set; } = 1;

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("cover")]
    public string Cover { get; set; } = "";
}

internal sealed class SectionComboItem
{
    public SectionComboItem(string id, string title)
    {
        Id = id;
        Title = title;
    }

    public string Id { get; }
    public string Title { get; }

    public override string ToString()
    {
        return Title;
    }
}
