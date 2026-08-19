using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppiumBuilder.Core;
using AppiumBuilder.Utils;

namespace AppiumBuilder.UI
{
    public sealed class LocalTestCaseBuilderForm : Form
    {
        private readonly ComboBox cmbProfile;
        private readonly RichTextBox txtManualRules;
        private readonly ListBox lstLearningSources;
        private readonly RichTextBox txtLearnedSummary;
        private readonly RichTextBox txtRequirement;
        private readonly ListBox lstGenerationDocuments;
        private readonly DataGridView grid;
        private readonly Label lblStatus;
        private Label lblLocalAiState = null!;

        private readonly List<object> learningSources = new();
        private readonly List<LocalPlanningDocument> generationDocuments = new();
        private TcLearningProfile activeProfile = TcLearningProfileStore.CreateDefault();
        private List<string> currentColumns = new();

        public LocalTestCaseBuilderForm()
        {
            Text = "Local TC Studio";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1180, 860);
            Size = new Size(1480, 1020);
            BackColor = Globals.Bg;
            ForeColor = Globals.TextPrimary;
            Font = Globals.FontBody;
            AutoScaleMode = AutoScaleMode.Dpi;

            cmbProfile = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                BackColor = Globals.SurfaceAlt,
                ForeColor = Globals.TextPrimary,
                Font = Globals.FontBody,
                Margin = new Padding(0, 5, 0, 5)
            };
            cmbProfile.SelectedIndexChanged += ProfileSelectedIndexChanged;

            txtManualRules = CreateRichTextBox(readOnly: false);
            lstLearningSources = CreateListBox();
            txtLearnedSummary = CreateRichTextBox(readOnly: true);
            txtRequirement = CreateRichTextBox(readOnly: false);
            lstGenerationDocuments = CreateListBox();
            grid = BuildGrid();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(18),
                BackColor = Globals.Bg,
                AutoScroll = true
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 176));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 194));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildProfileCard(), 0, 1);
            root.Controls.Add(BuildLearningCard(), 0, 2);
            root.Controls.Add(BuildGenerationSourceCard(), 0, 3);
            root.Controls.Add(BuildActionBar(), 0, 4);
            root.Controls.Add(grid, 0, 5);

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = "준비됨 · 프로젝트 학습 자료와 TC는 외부 AI로 전송되지 않습니다.",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Globals.TextMuted,
                Font = Globals.FontMuted,
                Padding = new Padding(4, 0, 0, 0)
            };
            root.Controls.Add(lblStatus, 0, 6);

            Controls.Add(root);
            LoadProfiles();

            Shown += async (_, _) =>
            {
                await LocalAiRuntimeManager.TryAutoStartAsync();
                await RefreshLocalAiStatusAsync();
            };
        }

        private Control BuildHeader()
        {
            var card = CreateSectionCard();
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(18, 8, 18, 8),
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label
            {
                Text = "Local TC Studio · Project Learning",
                Dock = DockStyle.Fill,
                Font = Globals.FontTitle,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            layout.Controls.Add(new Label
            {
                Text = "고정 TC 양식 없이 프로젝트의 기존 TC·가이드·기획서·이미지·직접 규칙을 학습해 같은 방식으로 작성합니다.",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);

            var security = new Label
            {
                Text = "LOCAL ONLY  ·  학습/생성 모두 127.0.0.1",
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 4, 0, 4),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Globals.SuccessSoft,
                ForeColor = Globals.Success,
                Font = Globals.FontSub
            };
            layout.Controls.Add(security, 1, 0);
            layout.SetRowSpan(security, 2);
            card.Controls.Add(layout);
            return card;
        }

        private Control BuildProfileCard()
        {
            var card = CreateSectionCard();
            card.Margin = new Padding(0, 8, 0, 4);
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(14, 10, 14, 12),
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 315));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0, 0, 14, 0),
                BackColor = Color.Transparent
            };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.Controls.Add(SectionLabel("프로젝트 학습 프로필"), 0, 0);
            left.Controls.Add(cmbProfile, 0, 1);

            var profileButtons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            profileButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            profileButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            var btnSave = CreateButton("프로필 저장", Globals.AccentSoft, Globals.Accent);
            var btnDelete = CreateButton("삭제", Globals.Surface, Globals.Danger);
            btnSave.Click += (_, _) => SaveCurrentProfile();
            btnDelete.Click += (_, _) => DeleteCurrentProfile();
            profileButtons.Controls.Add(btnSave, 0, 0);
            profileButtons.Controls.Add(btnDelete, 1, 0);
            left.Controls.Add(profileButtons, 0, 2);
            left.Controls.Add(new Label
            {
                Text = "팀/프로젝트마다 별도 프로필을 만들 수 있습니다. 학습 결과는 이 PC에만 저장됩니다.",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 4, 0, 0)
            }, 0, 3);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.Controls.Add(SectionLabel("내가 직접 설명하는 TC 작성 규칙"), 0, 0);
            right.Controls.Add(txtManualRules, 0, 1);

            layout.Controls.Add(left, 0, 0);
            layout.Controls.Add(right, 1, 0);
            card.Controls.Add(layout);
            return card;
        }

        private Control BuildLearningCard()
        {
            var card = CreateSectionCard();
            card.Margin = new Padding(0, 4, 0, 4);
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(14, 10, 14, 12),
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));

            var sources = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 0, 12, 0), BackColor = Color.Transparent };
            sources.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            sources.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sources.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            sources.Controls.Add(SectionLabel("학습 자료 · 기존 TC / TC 작성 가이드 / 관련 기획서 / 이미지"), 0, 0);
            sources.Controls.Add(lstLearningSources, 0, 1);

            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            var btnTc = CreateButton("기존 TC 추가", Globals.Surface, Globals.TextSecondary);
            var btnDocs = CreateButton("가이드/자료 추가", Globals.Surface, Globals.TextSecondary);
            var btnRemove = CreateButton("선택 제거", Globals.Surface, Globals.TextMuted);
            var btnClear = CreateButton("전체 제거", Globals.Surface, Globals.TextMuted);
            var btnLearn = CreateButton("프로필 학습", Globals.Accent, Color.White);
            btnTc.Click += async (_, _) => await AddExistingTcExamplesAsync(btnTc);
            btnDocs.Click += async (_, _) => await AddLearningDocumentsAsync(btnDocs);
            btnRemove.Click += (_, _) => RemoveLearningSource();
            btnClear.Click += (_, _) => ClearLearningSources();
            btnLearn.Click += async (_, _) => await LearnProfileAsync(btnLearn);
            buttons.Controls.Add(btnTc, 0, 0);
            buttons.Controls.Add(btnDocs, 1, 0);
            buttons.Controls.Add(btnRemove, 2, 0);
            buttons.Controls.Add(btnClear, 3, 0);
            buttons.Controls.Add(btnLearn, 4, 0);
            sources.Controls.Add(buttons, 0, 2);

            var learned = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
            learned.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            learned.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            learned.Controls.Add(SectionLabel("현재 프로필이 학습한 내용"), 0, 0);
            learned.Controls.Add(txtLearnedSummary, 0, 1);

            layout.Controls.Add(sources, 0, 0);
            layout.Controls.Add(learned, 1, 0);
            card.Controls.Add(layout);
            return card;
        }

        private Control BuildGenerationSourceCard()
        {
            var card = CreateSectionCard();
            card.Margin = new Padding(0, 4, 0, 4);
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(14, 10, 14, 12),
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));

            var memo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 12, 0), BackColor = Color.Transparent };
            memo.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            memo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            memo.Controls.Add(SectionLabel("이번 TC 추가 설명 / 규칙 / 메모"), 0, 0);
            memo.Controls.Add(txtRequirement, 0, 1);

            var docs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
            docs.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            docs.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            docs.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            docs.Controls.Add(SectionLabel("이번 TC 생성 대상 · PPTX / PDF / DOCX / TXT / 이미지"), 0, 0);
            docs.Controls.Add(lstGenerationDocuments, 0, 1);

            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            var btnAdd = CreateButton("기획서/이미지 추가", Globals.AccentSoft, Globals.Accent);
            var btnRemove = CreateButton("선택 제거", Globals.Surface, Globals.TextSecondary);
            var btnClear = CreateButton("전체 제거", Globals.Surface, Globals.TextMuted);
            btnAdd.Click += async (_, _) => await AddGenerationDocumentsAsync(btnAdd);
            btnRemove.Click += (_, _) => RemoveGenerationDocument();
            btnClear.Click += (_, _) => ClearGenerationDocuments();
            buttons.Controls.Add(btnAdd, 0, 0);
            buttons.Controls.Add(btnRemove, 1, 0);
            buttons.Controls.Add(btnClear, 2, 0);
            docs.Controls.Add(buttons, 0, 2);

            layout.Controls.Add(memo, 0, 0);
            layout.Controls.Add(docs, 1, 0);
            card.Controls.Add(layout);
            return card;
        }

        private Control BuildActionBar()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
                Margin = new Padding(0, 4, 0, 4),
                BackColor = Globals.Bg
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));

            var btnGenerate = CreateButton("학습 프로필로 TC 생성", Globals.Accent, Color.White);
            var btnLearnCurrent = CreateButton("현재 TC 학습 반영", Globals.AccentSoft, Globals.Accent);
            var btnDelete = CreateButton("선택 삭제", Globals.Surface, Globals.Danger);
            var btnClear = CreateButton("결과 지우기", Globals.Surface, Globals.TextMuted);
            var btnExport = CreateButton("CSV 내보내기", Globals.Surface, Globals.TextPrimary);
            btnGenerate.Click += async (_, _) => await GenerateWithLocalModelAsync(btnGenerate);
            btnLearnCurrent.Click += (_, _) => LearnCurrentGridAsExamples();
            btnDelete.Click += (_, _) => DeleteSelectedRows();
            btnClear.Click += (_, _) => ClearResults();
            btnExport.Click += (_, _) => ExportCsv();

            lblLocalAiState = new Label
            {
                Text = "○ 로컬 AI 상태 확인 중...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Globals.TextMuted,
                Font = Globals.FontMuted,
                Padding = new Padding(10, 0, 4, 0)
            };

            layout.Controls.Add(btnGenerate, 0, 0);
            layout.Controls.Add(btnLearnCurrent, 1, 0);
            layout.Controls.Add(btnDelete, 2, 0);
            layout.Controls.Add(btnClear, 3, 0);
            layout.Controls.Add(lblLocalAiState, 4, 0);
            layout.Controls.Add(btnExport, 5, 0);
            return layout;
        }

        private DataGridView BuildGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Globals.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Globals.Border,
                AutoGenerateColumns = false,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AllowUserToResizeRows = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Globals.Surface,
                    ForeColor = Globals.TextPrimary,
                    SelectionBackColor = Globals.AccentSoft,
                    SelectionForeColor = Globals.TextPrimary,
                    WrapMode = DataGridViewTriState.True,
                    Padding = new Padding(5)
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Globals.SurfaceAlt,
                    ForeColor = Globals.TextSecondary,
                    Font = Globals.FontSub,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    WrapMode = DataGridViewTriState.True
                },
                EnableHeadersVisualStyles = false
            };
        }

        private void LoadProfiles(string? selectName = null)
        {
            IReadOnlyList<TcLearningProfile> profiles = TcLearningProfileStore.Load();
            string desired = selectName ?? cmbProfile.Text.Trim();
            cmbProfile.SelectedIndexChanged -= ProfileSelectedIndexChanged;
            try
            {
                cmbProfile.Items.Clear();
                foreach (TcLearningProfile profile in profiles) cmbProfile.Items.Add(profile.Name);

                int index = !string.IsNullOrWhiteSpace(desired) ? cmbProfile.Items.IndexOf(desired) : -1;
                if (index < 0 && cmbProfile.Items.Count > 0) index = 0;
                if (index >= 0) cmbProfile.SelectedIndex = index;
            }
            finally
            {
                cmbProfile.SelectedIndexChanged += ProfileSelectedIndexChanged;
            }
            LoadSelectedProfile();
        }

        private void ProfileSelectedIndexChanged(object? sender, EventArgs e) => LoadSelectedProfile();

        private void EnsureProfileNameInCombo(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (cmbProfile.Items.Cast<object>().Any(x => string.Equals(Convert.ToString(x), name, StringComparison.CurrentCultureIgnoreCase))) return;
            cmbProfile.Items.Add(name);
        }

        private void LoadSelectedProfile()
        {
            string name = cmbProfile.Text.Trim();
            TcLearningProfile? profile = TcLearningProfileStore.Load()
                .FirstOrDefault(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (profile == null) return;

            activeProfile = CloneProfile(profile);
            txtManualRules.Text = activeProfile.ManualRules;
            UpdateLearnedSummary();
            grid.Rows.Clear();
            currentColumns.Clear();
            ConfigureGrid(activeProfile.LearnedColumns);
            learningSources.Clear();
            lstLearningSources.Items.Clear();
        }

        private void SaveCurrentProfile()
        {
            try
            {
                TcLearningProfile profile = BuildProfileForSave();
                TcLearningProfileStore.SaveOrUpdate(profile);
                activeProfile = CloneProfile(profile);
                EnsureProfileNameInCombo(profile.Name);
                cmbProfile.Text = profile.Name;
                UpdateLearnedSummary();
                SetStatus($"프로필 저장 완료 · {profile.Name} · 이 PC에만 저장됨", Globals.Success);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "프로필 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DeleteCurrentProfile()
        {
            string name = cmbProfile.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            if (MessageBox.Show(this, $"'{name}' 학습 프로필을 삭제할까요?", "Local TC Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            if (TcLearningProfileStore.Delete(name))
            {
                LoadProfiles();
                SetStatus("학습 프로필 삭제 완료", Globals.TextMuted);
            }
        }

        private async Task AddExistingTcExamplesAsync(Button button)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "학습할 기존 TC 선택",
                Filter = "TC 파일 (*.csv;*.xlsx)|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel Workbook (*.xlsx)|*.xlsx",
                Multiselect = true,
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            await WithBusyButton(button, "TC 분석 중...", async () =>
            {
                foreach (string path in dialog.FileNames)
                {
                    if (learningSources.OfType<TcExampleSet>().Any(x => x.SourcePath.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
                    TcExampleSet set = await Task.Run(() => LocalTestCaseEngine.ReadExampleSet(path));
                    learningSources.Add(set);
                    lstLearningSources.Items.Add("[기존 TC] " + set.DisplaySummary);
                }
                SetStatus($"기존 TC 학습 자료 추가 완료 · 총 {learningSources.Count}개", Globals.Success);
            });
        }

        private async Task AddLearningDocumentsAsync(Button button)
        {
            using var dialog = CreateDocumentOpenDialog("학습할 TC 가이드/기획서/이미지 선택");
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            await WithBusyButton(button, "자료 분석 중...", async () =>
            {
                foreach (string path in dialog.FileNames)
                {
                    if (learningSources.OfType<LocalPlanningDocument>().Any(x => x.SourcePath.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
                    LocalPlanningDocument document = await LocalPlanningDocumentReader.ReadAsync(path);
                    learningSources.Add(document);
                    lstLearningSources.Items.Add("[가이드/자료] " + document.DisplaySummary);
                }
                SetStatus($"학습 자료 준비 완료 · 총 {learningSources.Count}개", Globals.Success);
            });
        }

        private void RemoveLearningSource()
        {
            int index = lstLearningSources.SelectedIndex;
            if (index < 0 || index >= learningSources.Count) return;
            learningSources.RemoveAt(index);
            lstLearningSources.Items.RemoveAt(index);
            SetStatus($"현재 학습 자료 {learningSources.Count}개", Globals.TextMuted);
        }

        private void ClearLearningSources()
        {
            learningSources.Clear();
            lstLearningSources.Items.Clear();
            SetStatus("이번 세션의 학습 자료를 모두 제거했습니다. 이미 저장된 프로필 학습 결과는 유지됩니다.", Globals.TextMuted);
        }

        private async Task LearnProfileAsync(Button button)
        {
            string profileName = cmbProfile.Text.Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show(this, "먼저 프로젝트 학습 프로필 이름을 입력해주세요.", "프로필 학습", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtManualRules.Text) && learningSources.Count == 0)
            {
                MessageBox.Show(this, "직접 작성 규칙을 입력하거나 기존 TC/가이드/기획서/이미지를 추가해주세요.", "프로필 학습", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            button.Enabled = false;
            string original = button.Text;
            try
            {
                if (!await EnsureLocalAiReadyAsync(button)) return;
                button.Text = "프로필 분석 중...";
                SetStatus("기존 TC의 컬럼/문장 스타일/작성 규칙을 로컬 AI가 학습 중...", Globals.Info);

                TcExampleSet[] examples = learningSources.OfType<TcExampleSet>().ToArray();
                LocalPlanningDocument[] docs = learningSources.OfType<LocalPlanningDocument>().ToArray();
                using var client = new LocalOnlyLlmClient();
                TcLearningDigest digest = await client.LearnProfileAsync(
                    LocalAiRuntimeManager.Endpoint,
                    LocalAiRuntimeManager.DefaultModel,
                    txtManualRules.Text,
                    examples,
                    docs);

                var profile = new TcLearningProfile
                {
                    Name = profileName,
                    ManualRules = txtManualRules.Text,
                    LearnedColumns = digest.Columns,
                    LearnedRuleSummary = digest.RuleSummary,
                    LearnedStyleGuide = digest.StyleGuide,
                    LearnedCoverageGuide = digest.CoverageGuide,
                    LearnedWarnings = digest.Warnings,
                    LearningSourceNames = learningSources.Select(GetLearningSourceName).ToList(),
                    RepresentativeExamples = LocalTestCaseEngine.BuildRepresentativeExamples(examples, digest.Columns)
                };

                TcLearningProfileStore.SaveOrUpdate(profile);
                activeProfile = CloneProfile(profile);
                EnsureProfileNameInCombo(profileName);
                cmbProfile.Text = profileName;
                ConfigureGrid(activeProfile.LearnedColumns);
                UpdateLearnedSummary();
                SetStatus($"프로필 학습 완료 · {profileName} · 컬럼 {activeProfile.LearnedColumns.Count}개 · 대표 예시 {activeProfile.RepresentativeExamples.Count}건 저장", Globals.Success);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "프로필 학습 중 문제가 발생했습니다.\n\n" + ex.Message, "프로필 학습", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus("프로필 학습 실패 · 기존 저장 프로필은 변경되지 않았습니다.", Globals.Warning);
            }
            finally
            {
                button.Text = original;
                button.Enabled = true;
                await RefreshLocalAiStatusAsync();
            }
        }

        private async Task AddGenerationDocumentsAsync(Button button)
        {
            using var dialog = CreateDocumentOpenDialog("이번 TC 생성에 사용할 기획서/이미지 선택");
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            await WithBusyButton(button, "로컬 분석 중...", async () =>
            {
                foreach (string path in dialog.FileNames)
                {
                    if (generationDocuments.Any(x => x.SourcePath.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
                    LocalPlanningDocument document = await LocalPlanningDocumentReader.ReadAsync(path);
                    generationDocuments.Add(document);
                    lstGenerationDocuments.Items.Add(document.DisplaySummary);
                }
                int images = generationDocuments.Sum(x => x.Images.Count);
                SetStatus($"이번 생성 자료 {generationDocuments.Count}개 준비 · Vision 이미지 {images}개", Globals.Success);
            });
        }

        private void RemoveGenerationDocument()
        {
            int index = lstGenerationDocuments.SelectedIndex;
            if (index < 0 || index >= generationDocuments.Count) return;
            generationDocuments.RemoveAt(index);
            lstGenerationDocuments.Items.RemoveAt(index);
            SetStatus($"이번 생성 자료 {generationDocuments.Count}개", Globals.TextMuted);
        }

        private void ClearGenerationDocuments()
        {
            generationDocuments.Clear();
            lstGenerationDocuments.Items.Clear();
            SetStatus("이번 TC 생성 자료를 모두 제거했습니다.", Globals.TextMuted);
        }

        private async Task GenerateWithLocalModelAsync(Button button)
        {
            if (string.IsNullOrWhiteSpace(txtRequirement.Text) && generationDocuments.Count == 0)
            {
                MessageBox.Show(this, "이번 TC에 대한 설명을 입력하거나 기획서/이미지를 추가해주세요.", "TC 생성", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string original = button.Text;
            button.Enabled = false;
            try
            {
                if (!await EnsureLocalAiReadyAsync(button)) return;

                TcLearningProfile profile = BuildProfileForSave();
                TcLearningProfileStore.SaveOrUpdate(profile);
                activeProfile = CloneProfile(profile);

                if (!profile.HasLearning && string.IsNullOrWhiteSpace(profile.ManualRules))
                {
                    DialogResult answer = MessageBox.Show(
                        this,
                        "현재 프로필에 학습된 기존 TC/작성 규칙이 없습니다.\n\n고정 양식은 사용하지 않으므로 로컬 AI가 이번 자료에서 컬럼 구조를 추론하게 됩니다. 계속할까요?",
                        "학습 프로필 없음",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (answer != DialogResult.Yes) return;
                }

                button.Text = "TC 생성 중...";
                SetStatus($"{profile.Name} 학습 프로필 적용 · 기획서/이미지 로컬 분석 중 · 외부 업로드 없음", Globals.Info);

                using var client = new LocalOnlyLlmClient();
                GeneratedTcBatch generated = await client.GenerateWithOllamaAsync(
                    LocalAiRuntimeManager.Endpoint,
                    LocalAiRuntimeManager.DefaultModel,
                    txtRequirement.Text,
                    profile,
                    generationDocuments);

                ConfigureGrid(generated.Columns);
                PopulateGrid(generated.Cases);
                SetStatus($"TC {generated.Cases.Count}건 생성 완료 · 동적 컬럼 {generated.Columns.Count}개 · {profile.Name} 작성 방식 적용", Globals.Success);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "로컬 AI TC 생성 중 문제가 발생했습니다.\n기존 결과는 유지했습니다.\n\n" + ex.Message,
                    "TC 생성",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                SetStatus("TC 생성 실패 · 고정 7컬럼 규칙 기반 초안으로 자동 전환하지 않습니다.", Globals.Warning);
            }
            finally
            {
                button.Text = original;
                button.Enabled = true;
                await RefreshLocalAiStatusAsync();
            }
        }

        private async Task<bool> EnsureLocalAiReadyAsync(Button button)
        {
            LocalAiRuntimeManager.Status status = await LocalAiRuntimeManager.GetStatusAsync();
            if (status.Ready) return true;

            bool runtimeDownloadNeeded = !status.ServerRunning && status.NeedsRuntimeDownload;
            bool modelDownloadNeeded = !status.ModelAvailable;
            if (runtimeDownloadNeeded || modelDownloadNeeded)
            {
                string downloadInfo = runtimeDownloadNeeded && modelDownloadNeeded
                    ? "• Ollama standalone runtime 약 1.4GB\n• Qwen3-VL 4B 로컬 Vision 모델 약 3.3GB"
                    : runtimeDownloadNeeded
                        ? "• Ollama standalone runtime 약 1.4GB"
                        : "• Qwen3-VL 4B 로컬 Vision 모델 약 3.3GB";

                DialogResult answer = MessageBox.Show(
                    this,
                    "로컬 AI가 아직 준비되지 않았습니다.\n\n[처음 한 번만]\n" + downloadInfo + "\n\n프로그램이 공식 파일을 자동으로 내려받습니다.\n학습 자료와 TC 데이터는 127.0.0.1 밖으로 전송되지 않습니다.\n\n지금 준비할까요?",
                    "로컬 AI 준비",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return false;
            }

            button.Text = "AI 준비 중...";
            var progress = new Progress<LocalAiRuntimeManager.ProgressInfo>(info =>
            {
                string percent = info.Percent.HasValue ? $" · {info.Percent.Value}%" : string.Empty;
                lblStatus.Text = info.Detail + percent;
                lblStatus.ForeColor = Globals.Info;
                lblLocalAiState.Text = "○ " + info.Detail + percent;
                lblLocalAiState.ForeColor = Globals.Info;
            });

            var ready = await LocalAiRuntimeManager.EnsureReadyAsync(progress);
            if (!ready.Success)
            {
                MessageBox.Show(this, ready.Message, "로컬 AI 준비 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private async Task RefreshLocalAiStatusAsync()
        {
            try
            {
                LocalAiRuntimeManager.Status status = await LocalAiRuntimeManager.GetStatusAsync();
                if (status.Ready)
                {
                    lblLocalAiState.Text = $"● 로컬 AI 준비됨 · {LocalAiRuntimeManager.DefaultModel}";
                    lblLocalAiState.ForeColor = Globals.Success;
                }
                else if (status.ServerRunning)
                {
                    lblLocalAiState.Text = "○ Vision 모델 준비 필요";
                    lblLocalAiState.ForeColor = Globals.Warning;
                }
                else if (status.RuntimeAvailable)
                {
                    lblLocalAiState.Text = "○ 로컬 AI 엔진 대기";
                    lblLocalAiState.ForeColor = Globals.Info;
                }
                else
                {
                    lblLocalAiState.Text = "○ 최초 1회 로컬 AI 준비 필요";
                    lblLocalAiState.ForeColor = Globals.TextMuted;
                }
            }
            catch
            {
                lblLocalAiState.Text = "○ 로컬 AI 상태 확인 대기";
                lblLocalAiState.ForeColor = Globals.TextMuted;
            }
        }

        private void ConfigureGrid(IReadOnlyList<string> columns)
        {
            List<DynamicTestCase> existing = ReadGridRows();
            currentColumns = (columns ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Take(40)
                .ToList();

            grid.Columns.Clear();
            if (currentColumns.Count == 0) return;

            int width = currentColumns.Count <= 5 ? 220 : currentColumns.Count <= 8 ? 180 : 150;
            foreach (string column in currentColumns)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = column,
                    Name = column,
                    Width = width,
                    MinimumWidth = 100,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }

            if (existing.Count > 0) PopulateGrid(existing);
        }

        private void PopulateGrid(IEnumerable<DynamicTestCase> rows)
        {
            grid.Rows.Clear();
            foreach (DynamicTestCase item in rows)
                grid.Rows.Add(currentColumns.Select(item.GetValue).Cast<object>().ToArray());
        }

        private List<DynamicTestCase> ReadGridRows()
        {
            var result = new List<DynamicTestCase>();
            if (currentColumns.Count == 0 || grid.Columns.Count == 0) return result;
            grid.EndEdit();

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                var fields = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                bool hasValue = false;
                for (int i = 0; i < currentColumns.Count && i < row.Cells.Count; i++)
                {
                    string value = Convert.ToString(row.Cells[i].Value) ?? string.Empty;
                    fields[currentColumns[i]] = value;
                    if (!string.IsNullOrWhiteSpace(value)) hasValue = true;
                }
                if (hasValue) result.Add(new DynamicTestCase { Fields = fields });
            }
            return result;
        }

        private void LearnCurrentGridAsExamples()
        {
            List<DynamicTestCase> rows = ReadGridRows();
            if (currentColumns.Count == 0 || rows.Count == 0)
            {
                MessageBox.Show(this, "먼저 TC를 생성하거나 직접 작성한 뒤 학습에 반영해주세요.", "현재 TC 학습 반영", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                TcLearningProfile profile = BuildProfileForSave();
                profile.LearnedColumns = currentColumns.ToList();
                var merged = profile.RepresentativeExamples
                    .Select(x => new Dictionary<string, string>(x, StringComparer.CurrentCultureIgnoreCase))
                    .ToList();

                foreach (DynamicTestCase item in rows.Take(8))
                {
                    var sample = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                    foreach (string column in currentColumns) sample[column] = item.GetValue(column);
                    if (!merged.Any(existing => RowsEquivalent(existing, sample, currentColumns))) merged.Add(sample);
                    if (merged.Count >= 8) break;
                }

                profile.RepresentativeExamples = merged.Take(8).ToList();
                string source = "Local TC Studio 편집 결과 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                if (!profile.LearningSourceNames.Contains(source, StringComparer.CurrentCultureIgnoreCase))
                    profile.LearningSourceNames.Add(source);

                TcLearningProfileStore.SaveOrUpdate(profile);
                activeProfile = CloneProfile(profile);
                UpdateLearnedSummary();
                SetStatus($"현재 TC를 학습 예시로 반영 완료 · 대표 예시 {profile.RepresentativeExamples.Count}건 · 다음 생성부터 적용", Globals.Success);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "현재 TC 학습 반영 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static bool RowsEquivalent(Dictionary<string, string> left, Dictionary<string, string> right, IReadOnlyList<string> columns)
        {
            return columns.All(column =>
            {
                left.TryGetValue(column, out string? a);
                right.TryGetValue(column, out string? b);
                return string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.CurrentCultureIgnoreCase);
            });
        }

        private void DeleteSelectedRows()
        {
            foreach (DataGridViewRow row in grid.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(x => x.Index))
            {
                if (!row.IsNewRow) grid.Rows.RemoveAt(row.Index);
            }
            SetStatus($"현재 TC {ReadGridRows().Count}건", Globals.TextMuted);
        }

        private void ClearResults()
        {
            grid.Rows.Clear();
            SetStatus("TC 결과를 지웠습니다. 학습 프로필과 컬럼 구조는 유지됩니다.", Globals.TextMuted);
        }

        private void ExportCsv()
        {
            List<DynamicTestCase> rows = ReadGridRows();
            if (currentColumns.Count == 0 || rows.Count == 0)
            {
                MessageBox.Show(this, "내보낼 TC가 없습니다.", "CSV 내보내기", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string localFolder = Path.Combine(Globals.LogFolder, "TC_LOCAL");
            Directory.CreateDirectory(localFolder);
            using var dialog = new SaveFileDialog
            {
                Title = "TC CSV 저장",
                Filter = "CSV 파일 (*.csv)|*.csv",
                InitialDirectory = localFolder,
                FileName = $"TestCases_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                LocalTestCaseEngine.ExportCsv(dialog.FileName, currentColumns, rows);
                SetStatus($"CSV 저장 완료 · {rows.Count}건 · 현재 프로젝트 컬럼 {currentColumns.Count}개 유지", Globals.Success);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "CSV 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateLearnedSummary()
        {
            var lines = new List<string>();
            lines.Add(activeProfile.HasLearning ? "● 학습 완료" : "○ 아직 학습된 자료 없음");
            if (activeProfile.LearnedColumns.Count > 0)
                lines.Add("\n[학습된 컬럼 / 순서]\n" + string.Join("  →  ", activeProfile.LearnedColumns));
            else
                lines.Add("\n[학습된 컬럼]\n고정 컬럼 없음");
            if (!string.IsNullOrWhiteSpace(activeProfile.LearnedRuleSummary))
                lines.Add("\n[작성 규칙]\n" + activeProfile.LearnedRuleSummary);
            if (!string.IsNullOrWhiteSpace(activeProfile.LearnedStyleGuide))
                lines.Add("\n[문장/표현 스타일]\n" + activeProfile.LearnedStyleGuide);
            if (!string.IsNullOrWhiteSpace(activeProfile.LearnedCoverageGuide))
                lines.Add("\n[TC 분리/커버리지]\n" + activeProfile.LearnedCoverageGuide);
            if (activeProfile.RepresentativeExamples.Count > 0)
                lines.Add($"\n[대표 기존 TC]\n{activeProfile.RepresentativeExamples.Count}건을 로컬 프로필에 보관 중");
            if (activeProfile.LearningSourceNames.Count > 0)
                lines.Add("\n[학습 출처]\n" + string.Join("\n", activeProfile.LearningSourceNames.Select(x => "• " + x)));
            if (activeProfile.LearnedWarnings.Count > 0)
                lines.Add("\n[확인 필요]\n" + string.Join("\n", activeProfile.LearnedWarnings.Select(x => "• " + x)));
            txtLearnedSummary.Text = string.Join("\n", lines);
        }

        private TcLearningProfile BuildProfileForSave()
        {
            string name = cmbProfile.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("프로필 이름을 입력해주세요.");
            TcLearningProfile? stored = TcLearningProfileStore.Load()
                .FirstOrDefault(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            TcLearningProfile profile = stored != null ? CloneProfile(stored) : CloneProfile(activeProfile);
            profile.Name = name;
            profile.ManualRules = txtManualRules.Text;
            return profile;
        }

        private static TcLearningProfile CloneProfile(TcLearningProfile source)
        {
            return new TcLearningProfile
            {
                Name = source.Name,
                ManualRules = source.ManualRules,
                LearnedColumns = source.LearnedColumns.ToList(),
                LearnedRuleSummary = source.LearnedRuleSummary,
                LearnedStyleGuide = source.LearnedStyleGuide,
                LearnedCoverageGuide = source.LearnedCoverageGuide,
                LearnedWarnings = source.LearnedWarnings.ToList(),
                LearningSourceNames = source.LearningSourceNames.ToList(),
                RepresentativeExamples = source.RepresentativeExamples
                    .Select(x => new Dictionary<string, string>(x, StringComparer.CurrentCultureIgnoreCase))
                    .ToList(),
                UpdatedAtUtc = source.UpdatedAtUtc
            };
        }

        private static string GetLearningSourceName(object source)
        {
            return source switch
            {
                TcExampleSet set => "기존 TC · " + set.FileName,
                LocalPlanningDocument doc => doc.Kind + " · " + doc.FileName,
                _ => source.ToString() ?? "학습 자료"
            };
        }

        private static OpenFileDialog CreateDocumentOpenDialog(string title)
        {
            return new OpenFileDialog
            {
                Title = title,
                Filter = "학습/기획 자료 (*.pptx;*.pdf;*.docx;*.txt;*.md;*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.pptx;*.pdf;*.docx;*.txt;*.md;*.png;*.jpg;*.jpeg;*.bmp;*.gif|모든 파일 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = true
            };
        }

        private async Task WithBusyButton(Button button, string busyText, Func<Task> action)
        {
            string original = button.Text;
            button.Enabled = false;
            button.Text = busyText;
            try { await action(); }
            catch (NotSupportedException ex)
            {
                MessageBox.Show(this, ex.Message, "지원하지 않는 형식", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus("일부 파일 형식을 읽지 못했습니다.", Globals.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "로컬 자료 분석", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus("자료 분석 중 문제가 발생했습니다.", Globals.Warning);
            }
            finally
            {
                button.Text = original;
                button.Enabled = true;
            }
        }

        private void SetStatus(string text, Color color)
        {
            lblStatus.Text = text;
            lblStatus.ForeColor = color;
        }

        private static RoundedPanel CreateSectionCard()
        {
            return new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = Globals.Surface,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.Radius,
                Margin = new Padding(0)
            };
        }

        private static Label SectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static RichTextBox CreateRichTextBox(bool readOnly)
        {
            return new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = readOnly ? Globals.SurfaceAlt : Globals.Surface,
                ForeColor = Globals.TextPrimary,
                Font = Globals.FontBody,
                AcceptsTab = !readOnly,
                DetectUrls = false,
                ReadOnly = readOnly
            };
        }

        private static ListBox CreateListBox()
        {
            return new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Surface,
                ForeColor = Globals.TextPrimary,
                Font = Globals.FontBody,
                BorderStyle = BorderStyle.FixedSingle,
                HorizontalScrollbar = true,
                IntegralHeight = false
            };
        }

        private static Button CreateButton(string text, Color backColor, Color foreColor)
        {
            var button = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 6, 4, 6),
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = Globals.FontSub,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = Globals.Border;
            button.FlatAppearance.BorderSize = backColor == Globals.Accent ? 0 : 1;
            return button;
        }
    }
}
