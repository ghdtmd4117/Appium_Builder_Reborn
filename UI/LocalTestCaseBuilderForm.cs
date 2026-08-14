using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private readonly RichTextBox txtGuide;
        private readonly ComboBox cmbGuideProfile;
        private readonly RichTextBox txtRequirement;
        private readonly ListBox lstDocuments;
        private readonly TextBox txtTemplate;
        private Label lblLocalAiState = null!;
        private readonly DataGridView grid;
        private readonly Label lblStatus;
        private readonly BindingList<LocalTestCase> rows = new();
        private readonly List<LocalPlanningDocument> documents = new();
        private LocalTestCaseTemplate template = new();

        public LocalTestCaseBuilderForm()
        {
            Text = "Local TC Studio";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1120, 800);
            Size = new Size(1400, 940);
            BackColor = Globals.Bg;
            ForeColor = Globals.TextPrimary;
            Font = Globals.FontBody;
            AutoScaleMode = AutoScaleMode.Dpi;

            cmbGuideProfile = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                BackColor = Globals.SurfaceAlt,
                ForeColor = Globals.TextPrimary,
                Font = Globals.FontBody,
                Margin = new Padding(0, 5, 0, 5)
            };
            cmbGuideProfile.SelectedIndexChanged += GuideProfileChanged;

            txtGuide = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Globals.Surface,
                ForeColor = Globals.TextPrimary,
                Font = Globals.FontBody,
                AcceptsTab = true,
                DetectUrls = false
            };

            txtRequirement = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Globals.Surface,
                ForeColor = Globals.TextPrimary,
                Font = Globals.FontBody,
                AcceptsTab = true,
                DetectUrls = false
            };

            lstDocuments = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Surface,
                ForeColor = Globals.TextPrimary,
                Font = Globals.FontBody,
                BorderStyle = BorderStyle.FixedSingle,
                HorizontalScrollbar = true,
                IntegralHeight = false
            };

            txtTemplate = CreateReadOnlyTextBox("기본 TC 양식");

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(18),
                BackColor = Globals.Bg
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildGuideCard(), 0, 1);
            root.Controls.Add(BuildSourceCard(), 0, 2);
            root.Controls.Add(BuildActionBar(), 0, 3);

            grid = BuildGrid();
            grid.DataSource = rows;
            root.Controls.Add(grid, 0, 4);

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = "준비됨 · TC 가이드/기획서/결과는 외부 AI로 업로드되지 않습니다.",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Globals.TextMuted,
                Font = Globals.FontMuted,
                Padding = new Padding(4, 0, 0, 0)
            };
            root.Controls.Add(lblStatus, 0, 5);

            Controls.Add(root);
            LoadGuideProfiles();

            Shown += async (_, _) =>
            {
                await LocalAiRuntimeManager.TryAutoStartAsync();
                await RefreshLocalAiStatusAsync();
            };
        }

        private Control BuildHeader()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = Globals.Surface,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.Radius,
                Padding = new Padding(18, 10, 18, 10),
                Margin = new Padding(0)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label
            {
                Text = "Local TC Studio",
                Dock = DockStyle.Fill,
                Font = Globals.FontTitle,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            layout.Controls.Add(new Label
            {
                Text = "프로젝트 가이드 + PPTX/PDF/이미지 기획서를 로컬 AI가 분석해 TC를 생성합니다.",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);

            var security = new Label
            {
                Text = "LOCAL ONLY  ·  GPT / GEMINI 업로드 없음",
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

        private Control BuildGuideCard()
        {
            var card = CreateSectionCard();
            card.Margin = new Padding(0, 8, 0, 4);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(14, 10, 14, 14),
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Margin = new Padding(0, 0, 12, 0),
                BackColor = Color.Transparent
            };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.Controls.Add(new Label
            {
                Text = "TC 생성 가이드 프로필",
                Dock = DockStyle.Fill,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            left.Controls.Add(cmbGuideProfile, 0, 1);
            left.Controls.Add(new Label
            {
                Text = "회사/프로젝트 이름을 직접 입력해 저장할 수 있습니다.",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 2);

            var guideButtons = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            guideButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            guideButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            var btnSaveGuide = CreateButton("가이드 저장", Globals.AccentSoft, Globals.Accent);
            var btnDeleteGuide = CreateButton("삭제", Globals.Surface, Globals.Danger);
            btnSaveGuide.Click += (_, _) => SaveGuide();
            btnDeleteGuide.Click += (_, _) => DeleteGuide();
            guideButtons.Controls.Add(btnSaveGuide, 0, 0);
            guideButtons.Controls.Add(btnDeleteGuide, 1, 0);
            left.Controls.Add(guideButtons, 0, 3);

            left.Controls.Add(new Label
            {
                Text = "예: TC ID 규칙, 필수 컬럼, 정상/예외 분류, Step 작성법, 금지 표현, 우선순위 기준 등",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 6, 0, 0)
            }, 0, 4);

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.Controls.Add(new Label
            {
                Text = "반영해야 하는 TC 작성 규칙",
                Dock = DockStyle.Fill,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            right.Controls.Add(txtGuide, 0, 1);

            layout.Controls.Add(left, 0, 0);
            layout.Controls.Add(right, 1, 0);
            card.Controls.Add(layout);
            return card;
        }

        private Control BuildSourceCard()
        {
            var card = CreateSectionCard();
            card.Margin = new Padding(0, 4, 0, 4);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(14, 10, 14, 14),
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));

            var requirement = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 12, 0),
                BackColor = Color.Transparent
            };
            requirement.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            requirement.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            requirement.Controls.Add(new Label
            {
                Text = "추가 요구사항 / 메모  (기획서만으로 생성할 경우 비워도 됨)",
                Dock = DockStyle.Fill,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            requirement.Controls.Add(txtRequirement, 0, 1);

            var docs = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            docs.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            docs.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            docs.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            docs.Controls.Add(new Label
            {
                Text = "기획서 첨부  ·  PPTX / PDF / PNG / JPG",
                Dock = DockStyle.Fill,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            docs.Controls.Add(lstDocuments, 0, 1);

            var buttons = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
            var btnAdd = CreateButton("기획서 추가", Globals.AccentSoft, Globals.Accent);
            var btnRemove = CreateButton("선택 제거", Globals.Surface, Globals.TextSecondary);
            var btnClear = CreateButton("전체 제거", Globals.Surface, Globals.TextMuted);
            btnAdd.Click += async (_, _) => await AddDocumentsAsync(btnAdd);
            btnRemove.Click += (_, _) => RemoveSelectedDocument();
            btnClear.Click += (_, _) => ClearDocuments();
            buttons.Controls.Add(btnAdd, 0, 0);
            buttons.Controls.Add(btnRemove, 1, 0);
            buttons.Controls.Add(btnClear, 2, 0);
            docs.Controls.Add(buttons, 0, 2);

            layout.Controls.Add(requirement, 0, 0);
            layout.Controls.Add(docs, 1, 0);
            card.Controls.Add(layout);
            return card;
        }

        private Control BuildActionBar()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Margin = new Padding(0, 4, 0, 4),
                BackColor = Globals.Bg
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));

            var btnTemplate = CreateButton("CSV 양식 불러오기", Globals.Surface, Globals.TextSecondary);
            var btnDraft = CreateButton("규칙 기반 초안", Globals.AccentSoft, Globals.Accent);
            var btnLocalAi = CreateButton("AI TC 자동 생성", Globals.Accent, Color.White);
            var btnDelete = CreateButton("선택 삭제", Globals.Surface, Globals.Danger);
            var btnExport = CreateButton("CSV 내보내기", Globals.Surface, Globals.TextPrimary);

            btnTemplate.Click += (_, _) => LoadTemplate();
            btnDraft.Click += (_, _) => GenerateRuleDraft();
            btnLocalAi.Click += async (_, _) => await GenerateWithLocalModelAsync(btnLocalAi);
            btnDelete.Click += (_, _) => DeleteSelectedRows();
            btnExport.Click += (_, _) => ExportCsv();

            layout.Controls.Add(btnTemplate, 0, 0);
            layout.Controls.Add(btnDraft, 1, 0);
            layout.Controls.Add(btnLocalAi, 2, 0);
            layout.Controls.Add(btnDelete, 3, 0);

            lblLocalAiState = new Label
            {
                Text = "○ 로컬 AI 상태 확인 중...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Globals.TextMuted,
                Font = Globals.FontMuted,
                Padding = new Padding(10, 0, 4, 0)
            };
            layout.Controls.Add(lblLocalAiState, 4, 0);
            layout.Controls.Add(txtTemplate, 5, 0);
            layout.Controls.Add(btnExport, 6, 0);
            return layout;
        }

        private DataGridView BuildGrid()
        {
            var view = new DataGridView
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
                    Alignment = DataGridViewContentAlignment.MiddleLeft
                },
                EnableHeadersVisualStyles = false
            };

            view.Columns.Add(TextColumn("ID", nameof(LocalTestCase.Id), 82));
            view.Columns.Add(TextColumn("제목", nameof(LocalTestCase.Title), 220));
            view.Columns.Add(TextColumn("사전조건", nameof(LocalTestCase.Preconditions), 210));
            view.Columns.Add(TextColumn("테스트 절차", nameof(LocalTestCase.Steps), 320));
            view.Columns.Add(TextColumn("기대결과", nameof(LocalTestCase.ExpectedResult), 250));
            view.Columns.Add(TextColumn("우선순위", nameof(LocalTestCase.Priority), 82));
            view.Columns.Add(TextColumn("유형", nameof(LocalTestCase.Type), 92));
            return view;
        }

        private void LoadGuideProfiles(string? selectName = null)
        {
            IReadOnlyList<TcGenerationGuide> guides = TcGenerationGuideStore.Load();
            string? current = selectName ?? cmbGuideProfile.Text;

            cmbGuideProfile.SelectedIndexChanged -= GuideProfileChanged;
            cmbGuideProfile.Items.Clear();
            foreach (TcGenerationGuide guide in guides) cmbGuideProfile.Items.Add(guide.Name);
            cmbGuideProfile.SelectedIndexChanged += GuideProfileChanged;

            int index = -1;
            if (!string.IsNullOrWhiteSpace(current))
                index = cmbGuideProfile.Items.IndexOf(current);
            if (index < 0 && cmbGuideProfile.Items.Count > 0) index = 0;
            if (index >= 0) cmbGuideProfile.SelectedIndex = index;
            LoadSelectedGuide();
        }

        private void GuideProfileChanged(object? sender, EventArgs e) => LoadSelectedGuide();

        private void LoadSelectedGuide()
        {
            string name = cmbGuideProfile.Text.Trim();
            TcGenerationGuide? guide = TcGenerationGuideStore.Load()
                .FirstOrDefault(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (guide != null)
            {
                txtGuide.Text = guide.Rules;
                IReadOnlyList<string> columns = guide.TemplateColumns is { Count: > 0 }
                    ? guide.TemplateColumns
                    : LocalTestCaseTemplate.DefaultColumns;
                template = new LocalTestCaseTemplate
                {
                    Name = string.IsNullOrWhiteSpace(guide.TemplateName) ? "기본 TC 양식" : guide.TemplateName,
                    Columns = columns.ToArray()
                };
                txtTemplate.Text = template.Name + " · " + string.Join(" / ", template.Columns.Take(3)) + (template.Columns.Count > 3 ? " ..." : string.Empty);
            }
        }

        private void SaveGuide()
        {
            try
            {
                string name = cmbGuideProfile.Text.Trim();
                TcGenerationGuideStore.SaveOrUpdate(name, txtGuide.Text, template.Name, template.Columns);
                LoadGuideProfiles(name);
                lblStatus.Text = $"TC 생성 가이드 저장 완료 · {name} · 이 PC에만 저장됨";
                lblStatus.ForeColor = Globals.Success;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "가이드 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DeleteGuide()
        {
            string name = cmbGuideProfile.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            if (MessageBox.Show(this, $"'{name}' 가이드를 삭제할까요?", "TC 생성 가이드", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            if (TcGenerationGuideStore.Delete(name))
            {
                LoadGuideProfiles();
                lblStatus.Text = "가이드 삭제 완료";
                lblStatus.ForeColor = Globals.TextMuted;
            }
        }

        private async Task AddDocumentsAsync(Button button)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "TC 생성에 사용할 기획서 선택",
                Filter = "기획서 (*.pptx;*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.pptx;*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.gif|PPTX (*.pptx)|*.pptx|PDF (*.pdf)|*.pdf|이미지 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                CheckFileExists = true,
                Multiselect = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            string original = button.Text;
            button.Enabled = false;
            try
            {
                foreach (string path in dialog.FileNames)
                {
                    if (documents.Any(x => x.SourcePath.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
                    button.Text = "로컬 분석 중...";
                    lblStatus.Text = $"기획서 로컬 분석 중 · {Path.GetFileName(path)}";
                    lblStatus.ForeColor = Globals.Info;
                    LocalPlanningDocument document = await LocalPlanningDocumentReader.ReadAsync(path);
                    documents.Add(document);
                    lstDocuments.Items.Add(document.DisplaySummary);
                }

                int imageCount = documents.Sum(x => x.Images.Count);
                int textCount = documents.Sum(x => x.ExtractedText.Length);
                lblStatus.Text = $"기획서 {documents.Count}개 준비 · 로컬 추출 텍스트 {textCount:N0}자 · Vision 이미지 {imageCount}개";
                lblStatus.ForeColor = Globals.Success;
            }
            catch (NotSupportedException ex)
            {
                MessageBox.Show(this, ex.Message + "\n구형 .ppt 파일은 PowerPoint에서 .pptx로 저장한 뒤 추가해주세요.", "기획서 형식", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "기획서 분석 실패: " + ex.Message, "기획서 추가", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                lblStatus.Text = "기획서 분석 중 일부 파일에서 문제가 발생했습니다.";
                lblStatus.ForeColor = Globals.Warning;
            }
            finally
            {
                button.Text = original;
                button.Enabled = true;
            }
        }

        private void RemoveSelectedDocument()
        {
            int index = lstDocuments.SelectedIndex;
            if (index < 0 || index >= documents.Count) return;
            documents.RemoveAt(index);
            lstDocuments.Items.RemoveAt(index);
            lblStatus.Text = $"기획서 {documents.Count}개 첨부됨";
            lblStatus.ForeColor = Globals.TextMuted;
        }

        private void ClearDocuments()
        {
            documents.Clear();
            lstDocuments.Items.Clear();
            lblStatus.Text = "첨부 기획서를 모두 제거했습니다.";
            lblStatus.ForeColor = Globals.TextMuted;
        }

        private void LoadTemplate()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "TC CSV 양식 선택",
                Filter = "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                template = LocalTestCaseTemplate.FromCsvHeader(dialog.FileName);
                txtTemplate.Text = template.Name + " · " + string.Join(" / ", template.Columns.Take(5)) + (template.Columns.Count > 5 ? " ..." : string.Empty);
                lblStatus.Text = $"양식 로드 완료 · {template.Columns.Count}개 컬럼 · 파일 내용은 외부로 전송되지 않았습니다.";
                lblStatus.ForeColor = Globals.Success;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "양식 불러오기 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void GenerateRuleDraft()
        {
            string source = BuildRuleDraftSource();
            IReadOnlyList<LocalTestCase> generated = LocalTestCaseEngine.BuildRuleBasedDraft(source);
            if (generated.Count == 0)
            {
                MessageBox.Show(this, "요구사항을 입력하거나 기획서를 추가해주세요.", "TC 초안", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ReplaceRows(generated);
            lblStatus.Text = $"규칙 기반 로컬 초안 {generated.Count}건 생성 · 네트워크 사용 없음";
            lblStatus.ForeColor = Globals.Success;
        }

        private string BuildRuleDraftSource()
        {
            if (!string.IsNullOrWhiteSpace(txtRequirement.Text)) return txtRequirement.Text;
            LocalPlanningDocument? first = documents.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ExtractedText));
            if (first != null)
            {
                string text = first.ExtractedText.Replace("\r", " ").Replace("\n", " ").Trim();
                return text.Length > 500 ? text[..500] : text;
            }
            return documents.Count > 0 ? Path.GetFileNameWithoutExtension(documents[0].FileName) : string.Empty;
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
                    lblLocalAiState.Text = "○ Vision 모델 준비 필요 · 클릭 시 자동 준비";
                    lblLocalAiState.ForeColor = Globals.Warning;
                }
                else if (status.RuntimeAvailable)
                {
                    lblLocalAiState.Text = "○ 로컬 AI 엔진 대기 · 클릭 시 자동 시작";
                    lblLocalAiState.ForeColor = Globals.Info;
                }
                else
                {
                    lblLocalAiState.Text = "○ 최초 1회 로컬 AI 준비 필요 · 설치 작업 없음";
                    lblLocalAiState.ForeColor = Globals.TextMuted;
                }
            }
            catch
            {
                lblLocalAiState.Text = "○ 로컬 AI 상태 확인 대기";
                lblLocalAiState.ForeColor = Globals.TextMuted;
            }
        }

        private async Task GenerateWithLocalModelAsync(Button button)
        {
            if (string.IsNullOrWhiteSpace(txtRequirement.Text) && documents.Count == 0)
            {
                MessageBox.Show(this, "요구사항을 입력하거나 PPTX/PDF/이미지 기획서를 추가해주세요.", "AI TC 자동 생성", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string original = button.Text;
            button.Enabled = false;

            try
            {
                LocalAiRuntimeManager.Status status = await LocalAiRuntimeManager.GetStatusAsync();
                if (!status.Ready)
                {
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
                            "로컬 AI가 아직 준비되지 않았습니다.\n\n" +
                            "[처음 한 번만]\n" + downloadInfo + "\n\n" +
                            "프로그램이 공식 파일을 자동으로 내려받고 설정합니다.\n" +
                            "기획서/TC 데이터는 다운로드 과정에 포함되지 않으며 생성 시 127.0.0.1 안에서만 처리합니다.\n\n" +
                            "지금 준비할까요?",
                            "로컬 AI 준비",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (answer != DialogResult.Yes)
                        {
                            lblStatus.Text = "로컬 AI 준비를 건너뜀 · 규칙 기반 초안은 바로 사용할 수 있습니다.";
                            lblStatus.ForeColor = Globals.TextMuted;
                            return;
                        }
                    }

                    button.Text = "AI 준비 중...";
                    lblLocalAiState.Text = "○ 로컬 AI 준비 중...";
                    lblLocalAiState.ForeColor = Globals.Info;

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
                        IReadOnlyList<LocalTestCase> fallback = LocalTestCaseEngine.BuildRuleBasedDraft(BuildRuleDraftSource());
                        if (fallback.Count > 0) ReplaceRows(fallback);

                        lblStatus.Text = $"로컬 AI 준비 실패 → 규칙 기반 초안 {fallback.Count}건 생성";
                        lblStatus.ForeColor = Globals.Warning;
                        lblLocalAiState.Text = "○ 로컬 AI 준비 필요";
                        lblLocalAiState.ForeColor = Globals.Warning;

                        MessageBox.Show(this, ready.Message + "\n\n규칙 기반 초안으로 대신 생성했습니다.", "로컬 AI 준비 안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                button.Text = "기획서 분석/TC 생성 중...";
                int images = documents.Sum(x => x.Images.Count);
                lblStatus.Text = $"{LocalAiRuntimeManager.DefaultModel} 로컬 분석 중 · 기획서 {documents.Count}개 · Vision 이미지 {images}개 · 외부 업로드 없음";
                lblStatus.ForeColor = Globals.Info;

                using var client = new LocalOnlyLlmClient();
                IReadOnlyList<LocalTestCase> generated = await client.GenerateWithOllamaAsync(
                    LocalAiRuntimeManager.Endpoint,
                    LocalAiRuntimeManager.DefaultModel,
                    txtRequirement.Text,
                    txtGuide.Text,
                    template.Columns,
                    documents);

                if (generated.Count == 0) throw new InvalidDataException("로컬 AI가 유효한 TC를 만들지 못했습니다.");
                ReplaceRows(generated);
                lblStatus.Text = $"AI TC {generated.Count}건 생성 완료 · 가이드 + 기획서 반영 · 외부 AI/API 전송 없음";
                lblStatus.ForeColor = Globals.Success;
                lblLocalAiState.Text = $"● 로컬 AI 준비됨 · {LocalAiRuntimeManager.DefaultModel}";
                lblLocalAiState.ForeColor = Globals.Success;
            }
            catch (Exception ex)
            {
                IReadOnlyList<LocalTestCase> fallback = LocalTestCaseEngine.BuildRuleBasedDraft(BuildRuleDraftSource());
                if (fallback.Count > 0) ReplaceRows(fallback);

                lblStatus.Text = $"AI 생성 문제 → 규칙 기반 초안 {fallback.Count}건으로 자동 전환";
                lblStatus.ForeColor = Globals.Warning;
                MessageBox.Show(this, "로컬 AI 응답에 문제가 있어 규칙 기반 초안으로 자동 전환했습니다.\n\n" + ex.Message, "AI TC 자동 생성", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                button.Text = original;
                button.Enabled = true;
                await RefreshLocalAiStatusAsync();
            }
        }

        private void DeleteSelectedRows()
        {
            foreach (DataGridViewRow row in grid.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(r => r.Index))
            {
                if (!row.IsNewRow && row.DataBoundItem is LocalTestCase item) rows.Remove(item);
            }
            lblStatus.Text = $"현재 TC {rows.Count}건";
            lblStatus.ForeColor = Globals.TextMuted;
        }

        private void ExportCsv()
        {
            grid.EndEdit();
            if (rows.Count == 0)
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
                LocalTestCaseEngine.ExportCsv(dialog.FileName, template, rows);
                lblStatus.Text = $"CSV 저장 완료 · {rows.Count}건 · {dialog.FileName}";
                lblStatus.ForeColor = Globals.Success;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "CSV 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReplaceRows(IEnumerable<LocalTestCase> items)
        {
            rows.RaiseListChangedEvents = false;
            rows.Clear();
            foreach (LocalTestCase item in items) rows.Add(item);
            rows.RaiseListChangedEvents = true;
            rows.ResetBindings();
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

        private static TextBox CreateTextBox(string value)
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Text = value,
                BackColor = Globals.SurfaceAlt,
                ForeColor = Globals.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(4, 8, 4, 8)
            };
        }

        private static TextBox CreateReadOnlyTextBox(string value)
        {
            TextBox box = CreateTextBox(value);
            box.ReadOnly = true;
            box.TabStop = false;
            box.TextAlign = HorizontalAlignment.Center;
            return box;
        }

        private static Button CreateButton(string text, Color backColor, Color foreColor)
        {
            return new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 7, 4, 7),
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = Globals.FontSub,
                UseVisualStyleBackColor = false
            };
        }

        private static DataGridViewTextBoxColumn TextColumn(string header, string property, int width)
        {
            return new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                DataPropertyName = property,
                Width = width,
                MinimumWidth = Math.Min(width, 70),
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
        }
    }
}
