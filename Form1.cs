using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography; // НОВОЕ: Для шифрования AES
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Учёт_Вкладов
{
    // === НОВЫЙ КЛАСС: КРИПТОГРАФИЧЕСКИЙ ПОМОЩНИК ===
    public static class EncryptionHelper
    {
        // "Соль" для усиления ключа (фиксированная для простоты, но работает надежно)
        private static readonly byte[] Salt = new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };

        public static string Encrypt(string clearText, string password)
        {
            if (string.IsNullOrEmpty(password)) return clearText;
            byte[] clearBytes = Encoding.UTF8.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(password, Salt, 10000);
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Decrypt(string cipherText, string password)
        {
            if (string.IsNullOrEmpty(password)) return cipherText;
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                using (Aes encryptor = Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(password, Salt, 10000);
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                        }
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
            catch { return ""; } // Если пароль не подошел или файл сломан
        }
    }
    // ===============================================

    public class AppSettings
    {
        public Dictionary<string, int> ColumnWidths { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ArchiveColumnWidths { get; set; } = new Dictionary<string, int>();
        public double GlobalKeyRate { get; set; } = 16.0;

        // Хранилище хеша пароля
        public string PasswordHash { get; set; } = "";

        public int WindowLeft { get; set; } = -1;
        public int WindowTop { get; set; } = -1;
        public int WindowWidth { get; set; } = 1200;
        public int WindowHeight { get; set; } = 600;
        public FormWindowState WindowState { get; set; } = FormWindowState.Normal;
    }

    public partial class Form1 : Form
    {
        private Panel pnlTop;
        private ComboBox cmbFilterBank;
        private ComboBox cmbFilterOwner;
        private ComboBox cmbFilterCurrency;
        private bool isUpdatingFilters = false;

        private DataGridView dgvDeposits;
        private Panel pnlBottom;
        private TextBox txtTotal;
        private NumericUpDown numGlobalKeyRate;

        private string filePath = "deposits.json";
        private string settingsPath = "settings.json";

        private List<Deposit> allDeposits = new List<Deposit>();
        private AppSettings settings = new AppSettings();

        // НОВОЕ: Переменная, хранящая расшифрованный пароль только в оперативной памяти!
        private string _sessionPassword = "";

        private int _printRowIndex = 0;
        private bool _printingTotals = false;

        public Form1()
        {
            InitializeComponent();

            PreLoadSettings();

            // ПРОВЕРКА ПАРОЛЯ ПЕРЕД ЗАПУСКОМ
            if (!string.IsNullOrEmpty(settings.PasswordHash))
            {
                using (var loginForm = new PasswordForm(settings.PasswordHash, false))
                {
                    if (loginForm.ShowDialog() != DialogResult.OK)
                    {
                        Environment.Exit(0);
                    }
                    // ПОЛУЧАЕМ ПАРОЛЬ ОТ ОКНА ВХОДА ДЛЯ РАСШИФРОВКИ БАЗЫ
                    _sessionPassword = loginForm.EnteredPassword;
                }
            }

            InitializeCustomUI();

            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }

        private void PreLoadSettings()
        {
            if (File.Exists(settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(settingsPath);
                    var loadedSettings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (loadedSettings != null) settings = loadedSettings;
                }
                catch { }
            }
        }

        private void InitializeCustomUI()
        {
            this.Controls.Clear();

            this.Text = "Учёт Вкладов";
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            this.MinimumSize = new Size(1000, 450);

            bool isVisibleOnAnyScreen = false;
            if (settings.WindowLeft != -1 && settings.WindowTop != -1)
            {
                foreach (Screen screen in Screen.AllScreens)
                {
                    if (screen.WorkingArea.IntersectsWith(new Rectangle(settings.WindowLeft, settings.WindowTop, 10, 10)))
                    {
                        isVisibleOnAnyScreen = true; break;
                    }
                }
            }

            if (isVisibleOnAnyScreen)
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(settings.WindowLeft, settings.WindowTop);
                this.Size = new Size(settings.WindowWidth, settings.WindowHeight);
            }
            else
            {
                this.StartPosition = FormStartPosition.CenterScreen;
                this.Size = new Size(1200, 600);
            }

            pnlTop = new Panel();
            dgvDeposits = new DataGridView();
            pnlBottom = new Panel();
            txtTotal = new TextBox();
            numGlobalKeyRate = new NumericUpDown();

            // ВЕРХНЯЯ ПАНЕЛЬ
            pnlTop.Dock = DockStyle.Top;
            pnlTop.Height = 45;
            pnlTop.BackColor = Color.WhiteSmoke;
            this.Controls.Add(pnlTop);

            Label lblFilter = new Label { Text = "Фильтры:", Location = new Point(15, 14), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            pnlTop.Controls.Add(lblFilter);

            cmbFilterBank = new ComboBox { Location = new Point(90, 11), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFilterBank.SelectedIndexChanged += Filter_Changed;
            pnlTop.Controls.Add(cmbFilterBank);

            cmbFilterOwner = new ComboBox { Location = new Point(250, 11), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFilterOwner.SelectedIndexChanged += Filter_Changed;
            pnlTop.Controls.Add(cmbFilterOwner);

            cmbFilterCurrency = new ComboBox { Location = new Point(410, 11), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFilterCurrency.SelectedIndexChanged += Filter_Changed;
            pnlTop.Controls.Add(cmbFilterCurrency);

            Button btnClearFilter = new Button { Text = "Сбросить", Location = new Point(520, 10), Width = 80, Height = 25, BackColor = Color.LightGray, FlatStyle = FlatStyle.Flat };
            btnClearFilter.Click += (s, e) => {
                isUpdatingFilters = true;
                if (cmbFilterBank.Items.Count > 0) cmbFilterBank.SelectedIndex = 0;
                if (cmbFilterOwner.Items.Count > 0) cmbFilterOwner.SelectedIndex = 0;
                if (cmbFilterCurrency.Items.Count > 0) cmbFilterCurrency.SelectedIndex = 0;
                isUpdatingFilters = false;
                RefreshGrid();
            };
            pnlTop.Controls.Add(btnClearFilter);

            // КНОПКА ЗАДАНИЯ ПАРОЛЯ В ПРАВОМ ВЕРХНЕМ УГЛУ
            Button btnPassword = new Button { Text = "🔒 Пароль", Location = new Point(pnlTop.Width - 110, 10), Width = 90, Height = 25, Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.LightYellow, FlatStyle = FlatStyle.Flat };
            btnPassword.Click += BtnPassword_Click;
            pnlTop.Controls.Add(btnPassword);

            // НИЖНЯЯ ПАНЕЛЬ
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 150;
            pnlBottom.BackColor = Color.WhiteSmoke;
            this.Controls.Add(pnlBottom);

            // ТАБЛИЦА
            dgvDeposits.Dock = DockStyle.Fill;
            dgvDeposits.BackgroundColor = Color.White;
            dgvDeposits.BorderStyle = BorderStyle.None;
            dgvDeposits.CellDoubleClick += DgvDeposits_CellDoubleClick;
            this.Controls.Add(dgvDeposits);
            dgvDeposits.BringToFront();

            // ИТОГИ
            txtTotal.Multiline = true;
            txtTotal.ReadOnly = true;
            txtTotal.ScrollBars = ScrollBars.Vertical;
            txtTotal.BorderStyle = BorderStyle.None;
            txtTotal.BackColor = Color.WhiteSmoke;
            txtTotal.Location = new Point(15, 15);
            txtTotal.Size = new Size(pnlBottom.Width - 750, 120);
            txtTotal.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtTotal.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            txtTotal.Text = "Загрузка данных...";
            pnlBottom.Controls.Add(txtTotal);

            Label lblKeyRate = new Label { Text = "Ключевая ставка ЦБ (%):", Location = new Point(pnlBottom.Width - 700, 31), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            lblKeyRate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnlBottom.Controls.Add(lblKeyRate);

            numGlobalKeyRate.Location = new Point(pnlBottom.Width - 540, 27);
            numGlobalKeyRate.Width = 60;
            numGlobalKeyRate.DecimalPlaces = 2;
            numGlobalKeyRate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            numGlobalKeyRate.ValueChanged += (s, e) =>
            {
                Deposit.CurrentKeyRate = (double)numGlobalKeyRate.Value;
                SaveSettings();
                RefreshGrid();
            };
            pnlBottom.Controls.Add(numGlobalKeyRate);

            // КНОПКИ
            Button btnAdd = new Button { Text = "Добавить вклад", Size = new Size(140, 35), Location = new Point(pnlBottom.Width - 460, 15), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat };
            btnAdd.Click += BtnAdd_Click;
            pnlBottom.Controls.Add(btnAdd);

            Button btnClose = new Button { Text = "Закрыть (в архив)", Size = new Size(140, 35), Location = new Point(pnlBottom.Width - 460, 55), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.LightGray, FlatStyle = FlatStyle.Flat };
            btnClose.Click += BtnClose_Click;
            pnlBottom.Controls.Add(btnClose);

            Button btnDelete = new Button { Text = "Удалить ошибку", Size = new Size(140, 35), Location = new Point(pnlBottom.Width - 310, 15), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.MistyRose, FlatStyle = FlatStyle.Flat };
            btnDelete.Click += BtnDelete_Click;
            pnlBottom.Controls.Add(btnDelete);

            Button btnArchive = new Button { Text = "Открыть Архив", Size = new Size(140, 35), Location = new Point(pnlBottom.Width - 310, 55), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.LightSteelBlue, FlatStyle = FlatStyle.Flat };
            btnArchive.Click += BtnArchive_Click;
            pnlBottom.Controls.Add(btnArchive);

            Button btnExport = new Button { Text = "Экспорт в CSV", Size = new Size(140, 35), Location = new Point(pnlBottom.Width - 160, 15), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.LightGoldenrodYellow, FlatStyle = FlatStyle.Flat };
            btnExport.Click += BtnExport_Click;
            pnlBottom.Controls.Add(btnExport);

            Button btnPrint = new Button { Text = "Печать", Size = new Size(140, 35), Location = new Point(pnlBottom.Width - 160, 55), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.LightSkyBlue, FlatStyle = FlatStyle.Flat };
            btnPrint.Click += BtnPrint_Click;
            pnlBottom.Controls.Add(btnPrint);
        }

        private void BtnPassword_Click(object sender, EventArgs e)
        {
            // Открываем окно в режиме "Настройка" (true)
            using (var pf = new PasswordForm(settings.PasswordHash, true))
            {
                if (pf.ShowDialog() == DialogResult.OK)
                {
                    settings.PasswordHash = pf.NewHash;
                    _sessionPassword = pf.EnteredPassword; // Обновляем сессионный ключ

                    SaveSettings();
                    SaveData(); // СРАЗУ ПЕРЕЗАПИСЫВАЕМ БАЗУ С НОВЫМ ШИФРОВАНИЕМ!

                    string msg = string.IsNullOrEmpty(pf.NewHash) ? "Пароль успешно отключен! База расшифрована." : "Новый пароль установлен! База данных зашифрована.";
                    MessageBox.Show(msg, "Безопасность", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = settings.WindowState == FormWindowState.Minimized ? FormWindowState.Normal : settings.WindowState;

            SetupGridColumns();
            ApplySettingsToGrid();
            LoadData();
            dgvDeposits.CellValueChanged += DgvDeposits_CellValueChanged;

            // НОВАЯ СТРОКА: подписываемся на мгновенное изменение ячейки
            dgvDeposits.CurrentCellDirtyStateChanged += DgvDeposits_CurrentCellDirtyStateChanged;
        }

        // НОВЫЙ МЕТОД (можно вставить сразу под Form1_Load)
        private void DgvDeposits_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvDeposits.IsCurrentCellDirty && dgvDeposits.CurrentCell is DataGridViewCheckBoxCell)
            {
                // Заставляем таблицу мгновенно применить изменение галочки
                dgvDeposits.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void SetupGridColumns()
        {
            dgvDeposits.AutoGenerateColumns = false;
            dgvDeposits.AllowUserToAddRows = false;
            dgvDeposits.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvDeposits.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvDeposits.AllowUserToResizeColumns = true;
            dgvDeposits.RowHeadersVisible = false;
            dgvDeposits.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            dgvDeposits.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvDeposits.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bank", HeaderText = "Банк/Компания", DataPropertyName = "Bank", ReadOnly = true, Width = 150 });
            dgvDeposits.Columns.Add(new DataGridViewTextBoxColumn { Name = "Owner", HeaderText = "Владелец", DataPropertyName = "Owner", ReadOnly = true, Width = 100 });
            dgvDeposits.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Сумма", DataPropertyName = "Amount", DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" }, Width = 120 });
            dgvDeposits.Columns.Add(new DataGridViewTextBoxColumn { Name = "Currency", HeaderText = "Валюта", DataPropertyName = "Currency", ReadOnly = true, Width = 70 });
            dgvDeposits.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rate", HeaderText = "Номинал %", DataPropertyName = "RateString", ReadOnly = true, Width = 100 });
            dgvDeposits.Columns.Add(new DataGridViewTextBoxColumn { Name = "PayoutType", HeaderText = "Выплата", DataPropertyName = "PayoutType", ReadOnly = true, Width = 180 });
            dgvDeposits.Columns.Add(new DataGridViewTextBoxColumn { Name = "EffectiveRateString", HeaderText = "Эфф. ставка %", DataPropertyName = "EffectiveRateString", ReadOnly = true, Width = 100 });
            dgvDeposits.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndDateString", HeaderText = "Окончание", DataPropertyName = "EndDateString", ReadOnly = true, Width = 100 });
            dgvDeposits.Columns.Add(new DataGridViewTextBoxColumn { Name = "DaysLeft", HeaderText = "Дней осталось", DataPropertyName = "DaysLeft", ReadOnly = true, Width = 100 });
            // Столбец даты и столбец галочек ===
            dgvDeposits.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastReconciled", HeaderText = "Сверено", DataPropertyName = "LastReconciledString", ReadOnly = true, Width = 80 });
            dgvDeposits.Columns.Add(new DataGridViewCheckBoxColumn { Name = "ReconcileCheck", HeaderText = "✓", DataPropertyName = "IsReconciledSession", Width = 30 });
        }

        private void ApplySettingsToGrid()
        {
            foreach (var kvp in settings.ColumnWidths)
            {
                if (dgvDeposits.Columns.Contains(kvp.Key)) dgvDeposits.Columns[kvp.Key].Width = kvp.Value;
            }
            Deposit.CurrentKeyRate = settings.GlobalKeyRate;
            numGlobalKeyRate.Value = (decimal)Deposit.CurrentKeyRate;
        }

        // === ОБНОВЛЕНО: Чтение с расшифровкой ===
        private void LoadData()
        {
            if (File.Exists(filePath))
            {
                string rawText = File.ReadAllText(filePath);
                string jsonToParse = rawText;

                // Жесткая очистка от невидимых символов (BOM), которые Windows любит добавлять в начало файлов
                string cleanText = rawText.Trim('\uFEFF', '\u200B').TrimStart();

                // Если текст начинается с квадратной скобки, это 100% открытый старый файл
                bool isUnencryptedJson = cleanText.StartsWith("[");

                // Если это не открытый JSON и у нас в памяти есть пароль - расшифровываем
                if (!isUnencryptedJson && !string.IsNullOrEmpty(_sessionPassword))
                {
                    jsonToParse = EncryptionHelper.Decrypt(rawText, _sessionPassword);
                }

                try
                {
                    allDeposits = JsonConvert.DeserializeObject<List<Deposit>>(jsonToParse) ?? new List<Deposit>();
                }
                catch (Exception ex)
                {
                    // Теперь программа точно скажет, какое именно слово или символ ей не понравились!
                    MessageBox.Show($"Не удалось прочитать базу.\n\nТехническая деталь:\n{ex.Message}", "Ошибка чтения", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    allDeposits = new List<Deposit>();
                }
            }
            RefreshGrid();
        }

        // === ОБНОВЛЕНО: Запись с шифрованием ===
        private void SaveData()
        {
            var json = JsonConvert.SerializeObject(allDeposits, Formatting.Indented);

            // Если пароль установлен, шифруем JSON перед записью
            if (!string.IsNullOrEmpty(_sessionPassword))
            {
                json = EncryptionHelper.Encrypt(json, _sessionPassword);
            }

            File.WriteAllText(filePath, json);
        }

        private void SaveSettings()
        {
            foreach (DataGridViewColumn col in dgvDeposits.Columns)
            {
                if (col.Visible) settings.ColumnWidths[col.Name] = col.Width;
            }
            settings.GlobalKeyRate = Deposit.CurrentKeyRate;

            if (this.WindowState == FormWindowState.Normal)
            {
                settings.WindowLeft = this.Left;
                settings.WindowTop = this.Top;
                settings.WindowWidth = this.Width;
                settings.WindowHeight = this.Height;
            }
            else
            {
                settings.WindowLeft = this.RestoreBounds.Left;
                settings.WindowTop = this.RestoreBounds.Top;
                settings.WindowWidth = this.RestoreBounds.Width;
                settings.WindowHeight = this.RestoreBounds.Height;
            }

            settings.WindowState = this.WindowState == FormWindowState.Minimized ? FormWindowState.Normal : this.WindowState;

            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(settingsPath, json);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
        }

        private void UpdateFilterDropdowns()
        {
            isUpdatingFilters = true;

            string selectedBank = cmbFilterBank.SelectedItem?.ToString() ?? "Все банки";
            string selectedOwner = cmbFilterOwner.SelectedItem?.ToString() ?? "Все владельцы";
            string selectedCurrency = cmbFilterCurrency.SelectedItem?.ToString() ?? "Все валюты";

            var active = allDeposits.Where(d => !d.IsClosed).ToList();

            cmbFilterBank.Items.Clear();
            cmbFilterBank.Items.Add("Все банки");
            cmbFilterBank.Items.AddRange(active.Select(d => d.Bank).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().OrderBy(b => b).ToArray());

            cmbFilterOwner.Items.Clear();
            cmbFilterOwner.Items.Add("Все владельцы");
            cmbFilterOwner.Items.AddRange(active.Select(d => d.Owner).Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().OrderBy(o => o).ToArray());

            cmbFilterCurrency.Items.Clear();
            cmbFilterCurrency.Items.Add("Все валюты");
            cmbFilterCurrency.Items.AddRange(active.Select(d => d.Currency).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToArray());

            cmbFilterBank.SelectedItem = cmbFilterBank.Items.Contains(selectedBank) ? selectedBank : "Все банки";
            cmbFilterOwner.SelectedItem = cmbFilterOwner.Items.Contains(selectedOwner) ? selectedOwner : "Все владельцы";
            cmbFilterCurrency.SelectedItem = cmbFilterCurrency.Items.Contains(selectedCurrency) ? selectedCurrency : "Все валюты";

            isUpdatingFilters = false;
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (!isUpdatingFilters) RefreshGrid();
        }

        private void RefreshGrid()
        {
            UpdateFilterDropdowns();

            var activeDeposits = allDeposits.Where(d => !d.IsClosed).ToList();

            if (cmbFilterBank.SelectedIndex > 0)
                activeDeposits = activeDeposits.Where(d => d.Bank == cmbFilterBank.SelectedItem.ToString()).ToList();

            if (cmbFilterOwner.SelectedIndex > 0)
                activeDeposits = activeDeposits.Where(d => d.Owner == cmbFilterOwner.SelectedItem.ToString()).ToList();

            if (cmbFilterCurrency.SelectedIndex > 0)
                activeDeposits = activeDeposits.Where(d => d.Currency == cmbFilterCurrency.SelectedItem.ToString()).ToList();

            activeDeposits = activeDeposits.OrderBy(d => d.IsOPIF || d.IsPerpetual || !d.EndDate.HasValue ? int.MaxValue : (d.EndDate.Value - DateTime.Today).Days).ToList();

            dgvDeposits.CellValueChanged -= DgvDeposits_CellValueChanged;
            dgvDeposits.DataSource = null;
            dgvDeposits.DataSource = activeDeposits;
            dgvDeposits.CellValueChanged += DgvDeposits_CellValueChanged;

            var totalSummary = activeDeposits.GroupBy(d => d.Currency).Select(g => $"ИТОГО: {g.Sum(s => s.Amount):N2} {g.Key}");
            var ownerBankSummary = activeDeposits.GroupBy(d => new { d.Owner, d.Bank, d.Currency }).Select(g => $"• {g.Key.Owner} ({g.Key.Bank}): {g.Sum(s => s.Amount):N2} {g.Key.Currency}");

            string totalsText = string.Join("  |  ", totalSummary);
            if (ownerBankSummary.Any()) totalsText += Environment.NewLine + Environment.NewLine + "Детализация:" + Environment.NewLine + string.Join(Environment.NewLine, ownerBankSummary);

            txtTotal.Text = string.IsNullOrWhiteSpace(totalsText) ? "Вкладов нет (или не найдено по фильтру)" : totalsText;
            ColorUrgentRows();
        }

        private void ColorUrgentRows()
        {
            foreach (DataGridViewRow row in dgvDeposits.Rows)
            {
                if (row.Cells["DaysLeft"].Value != null && int.TryParse(row.Cells["DaysLeft"].Value.ToString(), out int days))
                {
                    if (days <= 7) row.DefaultCellStyle.BackColor = Color.MistyRose;
                }
            }
        }

        private void DgvDeposits_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                string colName = dgvDeposits.Columns[e.ColumnIndex].Name;

                // Если изменили сумму вручную
                if (colName == "Amount")
                {
                    SaveData();
                    RefreshGrid();
                }
                // Если кликнули по галочке сверки
                else if (colName == "ReconcileCheck")
                {
                    bool isChecked = (bool)dgvDeposits.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                    if (isChecked)
                    {
                        Guid id = (Guid)dgvDeposits.Rows[e.RowIndex].Cells["Id"].Value;
                        var deposit = allDeposits.FirstOrDefault(d => d.Id == id);
                        if (deposit != null)
                        {
                            deposit.LastReconciled = DateTime.Today; // Обновляем дату на сегодня
                            SaveData();      // Сохраняем базу
                            RefreshGrid();   // Обновляем визуальную таблицу
                        }
                    }
                }
            }
        }

        private string[] GetUniqueBanks() => allDeposits.Select(d => d.Bank).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().OrderBy(b => b).ToArray();
        private string[] GetUniqueOwners() => allDeposits.Select(d => d.Owner).Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().OrderBy(o => o).ToArray();

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var addForm = new AddDepositForm(GetUniqueBanks(), GetUniqueOwners()))
            {
                if (addForm.ShowDialog() == DialogResult.OK)
                {
                    allDeposits.Add(addForm.CurrentDeposit);
                    SaveData();
                    RefreshGrid();
                }
            }
        }

        private void DgvDeposits_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            Guid id = (Guid)dgvDeposits.Rows[e.RowIndex].Cells["Id"].Value;
            var deposit = allDeposits.FirstOrDefault(d => d.Id == id);

            if (deposit != null)
            {
                using (var editForm = new AddDepositForm(GetUniqueBanks(), GetUniqueOwners(), deposit))
                {
                    if (editForm.ShowDialog() == DialogResult.OK)
                    {
                        SaveData();
                        RefreshGrid();
                    }
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (dgvDeposits.CurrentRow == null) return;
            Guid id = (Guid)dgvDeposits.CurrentRow.Cells["Id"].Value;
            var deposit = allDeposits.FirstOrDefault(d => d.Id == id);

            if (deposit != null && MessageBox.Show($"Полностью удалить запись из базы (без сохранения в архиве)?", "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                allDeposits.Remove(deposit);
                SaveData();
                RefreshGrid();
            }
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            if (dgvDeposits.CurrentRow == null) return;
            Guid id = (Guid)dgvDeposits.CurrentRow.Cells["Id"].Value;
            var deposit = allDeposits.FirstOrDefault(d => d.Id == id);

            if (deposit != null && MessageBox.Show($"Убрать вклад в архив?", "Архивация", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                deposit.IsClosed = true;
                SaveData();
                RefreshGrid();
            }
        }

        private void BtnArchive_Click(object sender, EventArgs e)
        {
            using (var archiveForm = new ArchiveForm(allDeposits, settings, () => { SaveData(); RefreshGrid(); }))
            {
                archiveForm.ShowDialog();
            }

            SaveSettings();
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "CSV Файл (*.csv)|*.csv", FileName = "Мои_вклады.csv" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var lines = new List<string>();
                    var headers = dgvDeposits.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Select(c => "\"" + c.HeaderText + "\"");
                    lines.Add(string.Join(";", headers));

                    foreach (DataGridViewRow row in dgvDeposits.Rows)
                    {
                        var cells = row.Cells.Cast<DataGridViewCell>().Where(c => dgvDeposits.Columns[c.ColumnIndex].Visible)
                                       .Select(c => "\"" + (c.Value?.ToString() ?? "").Replace("\"", "\"\"") + "\"");
                        lines.Add(string.Join(";", cells));
                    }

                    File.WriteAllLines(sfd.FileName, lines, new UTF8Encoding(true));
                    MessageBox.Show("Данные успешно экспортированы!", "Экспорт завершен", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnPrint_Click(object sender, EventArgs e)
        {
            PrintDocument pd = new PrintDocument();
            pd.DefaultPageSettings.Landscape = true;

            pd.BeginPrint += (s, ev) =>
            {
                _printRowIndex = 0;
                _printingTotals = false;
            };

            pd.PrintPage += PrintDocument_PrintPage;

            using (PrintDialog pdi = new PrintDialog { Document = pd, UseEXDialog = true })
            {
                if (pdi.ShowDialog(this) == DialogResult.OK)
                {
                    using (PrintPreviewDialog ppd = new PrintPreviewDialog { Document = pd, Width = 900, Height = 600 })
                    {
                        ((Form)ppd).TopMost = true;
                        ppd.ShowDialog(this);
                    }
                }
            }
        }

        private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics;
            Font headerFont = new Font("Segoe UI", 10, FontStyle.Bold);
            Font rowFont = new Font("Segoe UI", 9);
            Font italicFont = new Font("Segoe UI", 9, FontStyle.Italic);

            int y = 50;
            int x = 40;
            int bottomMargin = e.PageBounds.Height - 60;

            string dateText = $"Дата формирования: {DateTime.Now.ToShortDateString()}";
            SizeF dateSize = g.MeasureString(dateText, italicFont);
            g.DrawString(dateText, italicFont, Brushes.DimGray, e.PageBounds.Width - dateSize.Width - 40, 20);

            if (_printRowIndex == 0 && !_printingTotals)
            {
                g.DrawString("Отчет по активным вкладам", new Font("Segoe UI", 14, FontStyle.Bold), Brushes.Black, x, y);
                y += 40;
            }

            foreach (DataGridViewColumn col in dgvDeposits.Columns)
            {
                if (col.Visible)
                {
                    Rectangle rect = new Rectangle(x, y, col.Width, 30);
                    g.FillRectangle(Brushes.LightGray, rect);
                    g.DrawRectangle(Pens.Black, rect);

                    StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(col.HeaderText, headerFont, Brushes.Black, rect, format);
                    x += col.Width;
                }
            }
            y += 30;

            if (!_printingTotals)
            {
                while (_printRowIndex < dgvDeposits.Rows.Count)
                {
                    if (y + 25 > bottomMargin)
                    {
                        e.HasMorePages = true;
                        return;
                    }

                    DataGridViewRow row = dgvDeposits.Rows[_printRowIndex];
                    x = 40;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (dgvDeposits.Columns[cell.ColumnIndex].Visible)
                        {
                            int width = dgvDeposits.Columns[cell.ColumnIndex].Width;
                            Rectangle rect = new Rectangle(x, y, width, 25);

                            g.DrawRectangle(Pens.Black, rect);
                            string cellText = cell.FormattedValue?.ToString() ?? "";
                            g.DrawString(cellText, rowFont, Brushes.Black, new RectangleF(x + 5, y + 4, width, 25));
                            x += width;
                        }
                    }
                    y += 25;
                    _printRowIndex++;
                }

                _printingTotals = true;
            }

            if (_printingTotals)
            {
                y += 20;

                if (y + 150 > bottomMargin)
                {
                    e.HasMorePages = true;
                    return;
                }

                RectangleF totalsRect = new RectangleF(40, y, e.PageBounds.Width - 80, e.PageBounds.Height - y);
                g.DrawString(txtTotal.Text, rowFont, Brushes.Black, totalsRect);

                e.HasMorePages = false;
            }
        }
    }
}