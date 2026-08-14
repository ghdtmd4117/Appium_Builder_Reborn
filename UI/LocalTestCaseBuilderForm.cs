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
        private readonly RichTextBox txtRequirement;
        private readonly TextBox txtTemplate;
        private readonly TextBox txtLocalEndpoint;
        private readonly TextBox txtLocalModel;
        private readonly DataGridView grid;
        private readonly Label lblStatus;
        private readonly BindingList<LocalTestCase> rows = new();
        private LocalTestCaseTemplate template = new();

        public LocalTestCaseBuilderForm()
        {
            Text = "Local TC Builder";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1050, 720);
            Size = new Size(1320, 860);
            BackColor = Globals.Bg;
            ForeColor = Globals.TextPrimary;
            Font = Globals.FontBody;
            AutoScaleMode = AutoScaleMode.Dpi;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(18),
                BackColor = Globals.Bg
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 214));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

            root.Controls.Add(BuildHeader(), 0, 0);

            var inputGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 8, 0, 8),
                BackColor = Globals.Bg
            };
            inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));

            txtRequirement = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Globals.Surface,
                ForeColor = Globals.TextPrimary,
                Font = Globals.FontBody,
                DetectUrls = false,
                AcceptsTab = true,
                Text = string.Empty
            };
            inputGrid.Controls.Add(BuildRequirementCard(), 0, 0);

            txtTemplate = CreateReadOnlyTextBox("기본 TC 양식");
            txtLocalEndpoint = CreateTextBox("http://127.0.0.1:11434");
            txtLocalModel = CreateTextBox(string.Empty);
            inputGrid.Controls.Add(BuildLocalSettingsCard(), 1, 0);
            root.Controls.Add(inputGrid, 0, 1);

            root.Controls.Add(BuildActionBar(), 0, 2);

            grid = BuildGrid();
            grid.DataSource = rows;
            root.Controls.Add(grid, 0, 3);

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                Text = "준비됨 · 입력 내용은 자동 업로드/전송되지 않습니다.",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Globals.TextMuted,
                Font = Globals.FontMuted,
                Padding = new Padding(4, 0, 0, 0)
            };
            root.Controls.Add(lblStatus, 0, 4);

            Controls.Add(root);
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
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 285));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label
            {
                Text = "Local TC Builder",
                Dock = DockStyle.Fill,
                Font = Globals.FontTitle,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            layout.Controls.Add(new Label
            {
                Text = "업무 요구사항을 로컬에서 TC로 정리하고 양식에 맞춰 내보냅니다.",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);

            var security = new Label
            {
                Text = "LOCAL ONLY  ·  외부 AI 전송 차단",
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

        private Control BuildRequirementCard()
        {
            var card = CreateSectionCard();
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(14, 10, 14, 14),
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label
            {
                Text = "요구사항 / 기능 설명",
                Dock = DockStyle.Fill,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            layout.Controls.Add(txtRequirement, 0, 1);
            card.Controls.Add(layout);
            return card;
        }

        private Control BuildLocalSettingsCard()
        {
            var card = CreateSectionCard();
            card.Margin = new Padding(10, 0, 0, 0);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(14, 10, 14, 14),
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label
            {
                Text = "로컬 생성 설정",
                Dock = DockStyle.Fill,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(title, 0, 0);
            layout.SetColumnSpan(title, 2);

            layout.Controls.Add(Caption("양식"), 0, 1);
            layout.Controls.Add(txtTemplate, 1, 1);
            layout.Controls.Add(Caption("로컬 주소"), 0, 2);
            layout.Controls.Add(txtLocalEndpoint, 1, 2);

            var modelRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
            modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            modelRow.Controls.Add(Caption("모델명"), 0, 0);
            txtLocalModel.PlaceholderText = "예: qwen2.5:7b (설치된 모델)";
            modelRow.Controls.Add(txtLocalModel, 1, 0);
            layout.Controls.Add(modelRow, 0, 3);
            layout.SetColumnSpan(modelRow, 2);

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
                Margin = new Padding(0, 6, 0, 6),
                BackColor = Globals.Bg
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));

            var btnTemplate = CreateButton("CSV 양식 불러오기", Globals.Surface, Globals.TextSecondary);
            var btnDraft = CreateButton("규칙 기반 초안", Globals.AccentSoft, Globals.Accent);
            var btnLocalAi = CreateButton("로컬 LLM 생성", Globals.Accent, Color.White);
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
            layout.Controls.Add(new Label
            {
                Text = "로컬 LLM은 localhost만 허용하며 Redirect/Proxy를 사용하지 않습니다.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Globals.TextMuted,
                Font = Globals.FontMuted,
                Padding = new Padding(10, 0, 4, 0)
            }, 4, 0);
            layout.Controls.Add(btnExport, 5, 0);
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
            IReadOnlyList<LocalTestCase> generated = LocalTestCaseEngine.BuildRuleBasedDraft(txtRequirement.Text);
            if (generated.Count == 0)
            {
                MessageBox.Show(this, "요구사항을 먼저 입력해주세요.", "TC 초안", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ReplaceRows(generated);
            lblStatus.Text = $"규칙 기반 로컬 초안 {generated.Count}건 생성 · 네트워크 사용 없음";
            lblStatus.ForeColor = Globals.Success;
        }

        private async Task GenerateWithLocalModelAsync(Button button)
        {
            if (string.IsNullOrWhiteSpace(txtRequirement.Text))
            {
                MessageBox.Show(this, "요구사항을 먼저 입력해주세요.", "로컬 LLM", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!LocalOnlyLlmClient.IsLoopbackEndpoint(txtLocalEndpoint.Text.Trim()))
            {
                MessageBox.Show(this,
                    "보안상 localhost / 127.0.0.1 / ::1 주소만 허용됩니다.\n외부 IP나 도메인은 이 기능에서 사용할 수 없습니다.",
                    "외부 연결 차단",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtLocalModel.Text))
            {
                MessageBox.Show(this, "PC에 설치된 로컬 모델명을 입력해주세요.", "로컬 LLM", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string original = button.Text;
            button.Enabled = false;
            button.Text = "로컬 생성 중...";
            lblStatus.Text = "localhost 로컬 모델에만 요청 중 · 외부 Redirect/Proxy 차단";
            lblStatus.ForeColor = Globals.Info;

            try
            {
                using var client = new LocalOnlyLlmClient();
                IReadOnlyList<LocalTestCase> generated = await client.GenerateWithOllamaAsync(
                    txtLocalEndpoint.Text.Trim(),
                    txtLocalModel.Text.Trim(),
                    txtRequirement.Text,
                    template.Columns);

                if (generated.Count == 0) throw new InvalidDataException("로컬 모델이 유효한 TC를 만들지 못했습니다.");
                ReplaceRows(generated);
                lblStatus.Text = $"로컬 LLM TC {generated.Count}건 생성 완료 · 외부 API 사용 없음";
                lblStatus.ForeColor = Globals.Success;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "로컬 LLM 생성 실패 · 데이터는 외부로 전송되지 않았습니다.";
                lblStatus.ForeColor = Globals.Danger;
                MessageBox.Show(this, ex.Message, "로컬 LLM 생성 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                button.Text = original;
                button.Enabled = true;
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

        private static Label Caption(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Globals.TextMuted,
            Font = Globals.FontMuted
        };

        private static TextBox CreateTextBox(string value)
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Text = value,
                BackColor = Globals.SurfaceAlt,
                ForeColor = Globals.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 6, 0, 6)
            };
        }

        private static TextBox CreateReadOnlyTextBox(string value)
        {
            TextBox box = CreateTextBox(value);
            box.ReadOnly = true;
            box.TabStop = false;
            return box;
        }

        private static Button CreateButton(string text, Color backColor, Color foreColor)
        {
            return new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 8, 4, 8),
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
