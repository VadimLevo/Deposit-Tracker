using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Учёт_Вкладов
{
    public partial class ArchiveForm : Form
    {
        private DataGridView dgvArchive;
        private List<Deposit> allDeposits;
        private Action saveAndRefreshCallback;

        // Больше не читаем файл сами, а используем общие настройки из главного окна
        private AppSettings settings;

        // Конструктор теперь требует передать ему настройки (appSettings)
        public ArchiveForm(List<Deposit> deposits, AppSettings appSettings, Action saveCallback)
        {
            allDeposits = deposits;
            settings = appSettings; // Сохраняем ссылку на общие настройки
            saveAndRefreshCallback = saveCallback;

            InitializeUI();
            RefreshArchiveGrid();

            this.FormClosing += ArchiveForm_FormClosing;
        }

        private void ArchiveForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // При закрытии архива просто обновляем цифры в памяти главного окна
            foreach (DataGridViewColumn col in dgvArchive.Columns)
            {
                if (col.Visible) settings.ArchiveColumnWidths[col.Name] = col.Width;
            }
        }

        private void InitializeUI()
        {
            this.Text = "Архив закрытых вкладов";
            this.Size = new Size(900, 450);
            this.StartPosition = FormStartPosition.CenterParent;

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            dgvArchive = new DataGridView();
            dgvArchive.Dock = DockStyle.Fill;
            dgvArchive.BackgroundColor = Color.WhiteSmoke;
            dgvArchive.BorderStyle = BorderStyle.None;
            dgvArchive.AutoGenerateColumns = false;
            dgvArchive.AllowUserToAddRows = false;
            dgvArchive.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            dgvArchive.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvArchive.AllowUserToResizeColumns = true;

            dgvArchive.RowHeadersVisible = false;
            dgvArchive.ReadOnly = true;
            dgvArchive.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            dgvArchive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvArchive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bank", HeaderText = "Банк", DataPropertyName = "Bank", Width = 150 });
            dgvArchive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Owner", HeaderText = "Владелец", DataPropertyName = "Owner", Width = 100 });
            dgvArchive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Сумма", DataPropertyName = "Amount", DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" }, Width = 120 });
            dgvArchive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Currency", HeaderText = "Валюта", DataPropertyName = "Currency", Width = 80 });
            dgvArchive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rate", HeaderText = "Ставка %", DataPropertyName = "RateString", Width = 100 });
            dgvArchive.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndDate", HeaderText = "Дата закрытия (по плану)", DataPropertyName = "EndDateString", Width = 150 });

            // Применяем ширину из переданных настроек
            foreach (var kvp in settings.ArchiveColumnWidths)
            {
                if (dgvArchive.Columns.Contains(kvp.Key)) dgvArchive.Columns[kvp.Key].Width = kvp.Value;
            }

            this.Controls.Add(dgvArchive);

            Panel pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.LightGray };
            this.Controls.Add(pnlBottom);
            dgvArchive.BringToFront();

            Button btnRestore = new Button { Text = "Вернуть в работу", Size = new Size(150, 35), Location = new Point(15, 12), BackColor = Color.LightYellow, FlatStyle = FlatStyle.Flat };
            btnRestore.Click += BtnRestore_Click;
            pnlBottom.Controls.Add(btnRestore);

            Button btnDelete = new Button { Text = "Удалить навсегда", Size = new Size(150, 35), Location = new Point(175, 12), BackColor = Color.MistyRose, FlatStyle = FlatStyle.Flat };
            btnDelete.Click += BtnDelete_Click;
            pnlBottom.Controls.Add(btnDelete);
        }

        private void RefreshArchiveGrid()
        {
            dgvArchive.DataSource = null;
            dgvArchive.DataSource = allDeposits.Where(d => d.IsClosed).ToList();
        }

        private void BtnRestore_Click(object sender, EventArgs e)
        {
            if (dgvArchive.CurrentRow == null) return;
            Guid id = (Guid)dgvArchive.CurrentRow.Cells["Id"].Value;
            var deposit = allDeposits.FirstOrDefault(d => d.Id == id);

            if (deposit != null)
            {
                deposit.IsClosed = false;
                saveAndRefreshCallback();
                RefreshArchiveGrid();
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (dgvArchive.CurrentRow == null) return;
            Guid id = (Guid)dgvArchive.CurrentRow.Cells["Id"].Value;
            var deposit = allDeposits.FirstOrDefault(d => d.Id == id);

            if (deposit != null)
            {
                if (MessageBox.Show("Удалить эту запись из базы безвозвратно?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    allDeposits.Remove(deposit);
                    saveAndRefreshCallback();
                    RefreshArchiveGrid();
                }
            }
        }
    }
}