using System;
using System.Drawing;
using System.Windows.Forms;

namespace Учёт_Вкладов
{
    public partial class AddDepositForm : Form
    {
        public Deposit CurrentDeposit { get; private set; }

        private ComboBox cmbBank;
        private ComboBox cmbOwner;
        private NumericUpDown numAmount;
        private ComboBox cmbCurrency;

        private CheckBox chkOPIF;

        private Label lblRate;
        private NumericUpDown numRate; // Одно поле для всего!
        private CheckBox chkIsKeyRateLinked;

        private ComboBox cmbPayout;

        private DateTimePicker dtpStart;
        private DateTimePicker dtpEnd;
        private CheckBox chkPerpetual;

        public AddDepositForm(string[] existingBanks, string[] existingOwners, Deposit depositToEdit = null)
        {
            CurrentDeposit = depositToEdit ?? new Deposit();
            InitializeCustomUI(existingBanks, existingOwners);
            LoadDepositData();
        }

        private void InitializeCustomUI(string[] banks, string[] owners)
        {
            this.Text = CurrentDeposit.Id != Guid.Empty ? "Редактирование записи" : "Новая запись";
            this.Size = new Size(420, 430);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            int y = 20;

            Label lblBank = new Label { Text = "Банк/Компания:", Location = new Point(20, y), AutoSize = true };
            this.Controls.Add(lblBank);
            cmbBank = new ComboBox { Location = new Point(150, y - 3), Width = 230 };
            cmbBank.Items.AddRange(banks);
            cmbBank.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbBank.AutoCompleteSource = AutoCompleteSource.ListItems;
            this.Controls.Add(cmbBank);

            y += 40;
            Label lblOwner = new Label { Text = "Владелец:", Location = new Point(20, y), AutoSize = true };
            this.Controls.Add(lblOwner);
            cmbOwner = new ComboBox { Location = new Point(150, y - 3), Width = 230 };
            cmbOwner.Items.AddRange(owners);
            cmbOwner.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbOwner.AutoCompleteSource = AutoCompleteSource.ListItems;
            this.Controls.Add(cmbOwner);

            y += 40;
            Label lblAmount = new Label { Text = "Сумма:", Location = new Point(20, y), AutoSize = true };
            this.Controls.Add(lblAmount);
            numAmount = new NumericUpDown { Location = new Point(150, y - 3), Width = 120, Maximum = 1000000000, DecimalPlaces = 2 };
            this.Controls.Add(numAmount);
            cmbCurrency = new ComboBox { Location = new Point(280, y - 3), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCurrency.Items.AddRange(new[] { "RUB", "USD", "EUR", "CNY" });
            this.Controls.Add(cmbCurrency);

            y += 40;
            chkOPIF = new CheckBox { Text = "Это ОПИФ", Location = new Point(150, y), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            chkOPIF.CheckedChanged += UpdateControlsState;
            this.Controls.Add(chkOPIF);

            y += 35;
            lblRate = new Label { Text = "Ставка (%):", Location = new Point(20, y), AutoSize = true };
            this.Controls.Add(lblRate);

            // ВАЖНО: Разрешаем отрицательные значения для надбавки (до -100)
            numRate = new NumericUpDown { Location = new Point(150, y - 3), Width = 80, DecimalPlaces = 2, Minimum = -100, Maximum = 1000 };
            this.Controls.Add(numRate);

            chkIsKeyRateLinked = new CheckBox { Text = "Привязать к КС ЦБ", Location = new Point(240, y - 1), AutoSize = true };
            chkIsKeyRateLinked.CheckedChanged += UpdateControlsState;
            this.Controls.Add(chkIsKeyRateLinked);

            y += 35;
            Label lblPayout = new Label { Text = "Выплата:", Location = new Point(20, y), AutoSize = true };
            this.Controls.Add(lblPayout);
            cmbPayout = new ComboBox { Location = new Point(150, y - 3), Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPayout.Items.AddRange(new[] { "В конце срока (на счет)", "Ежемесячно (на счет)", "Ежемесячно (капитализация)", "Ежеквартально (на счет)" });
            this.Controls.Add(cmbPayout);

            y += 40;
            Label lblDates = new Label { Text = "Срок (От - До):", Location = new Point(20, y), AutoSize = true };
            this.Controls.Add(lblDates);
            dtpStart = new DateTimePicker { Location = new Point(150, y - 3), Width = 95, Format = DateTimePickerFormat.Short };
            this.Controls.Add(dtpStart);
            dtpEnd = new DateTimePicker { Location = new Point(255, y - 3), Width = 95, Format = DateTimePickerFormat.Short };
            this.Controls.Add(dtpEnd);

            y += 30;
            chkPerpetual = new CheckBox { Text = "Бессрочный", Location = new Point(255, y), AutoSize = true };
            chkPerpetual.CheckedChanged += UpdateControlsState;
            this.Controls.Add(chkPerpetual);

            y += 40;
            Button btnSave = new Button { Text = "Сохранить", Location = new Point(150, y), Width = 100, Height = 35, BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);
        }

        private void UpdateControlsState(object sender, EventArgs e)
        {
            bool isOpif = chkOPIF.Checked;
            bool isKeyRate = chkIsKeyRateLinked.Checked;
            bool isPerpetual = chkPerpetual.Checked;

            // Меняем текст лейбла в зависимости от галочки КС
            lblRate.Text = isKeyRate ? "Надбавка к КС:" : "Ставка (%):";

            chkIsKeyRateLinked.Enabled = !isOpif;
            numRate.Enabled = !isOpif;

            dtpEnd.Enabled = !isOpif && !isPerpetual;
            chkPerpetual.Enabled = !isOpif;
            if (isOpif) chkPerpetual.Checked = false;
        }

        private void LoadDepositData()
        {
            cmbBank.Text = CurrentDeposit.Bank;
            cmbOwner.Text = CurrentDeposit.Owner;
            numAmount.Value = CurrentDeposit.Amount;
            cmbCurrency.SelectedItem = string.IsNullOrEmpty(CurrentDeposit.Currency) ? "RUB" : CurrentDeposit.Currency;
            cmbPayout.SelectedItem = string.IsNullOrEmpty(CurrentDeposit.PayoutType) ? "В конце срока (на счет)" : CurrentDeposit.PayoutType;

            chkOPIF.Checked = CurrentDeposit.IsOPIF;
            chkIsKeyRateLinked.Checked = CurrentDeposit.IsKeyRateLinked;
            numRate.Value = (decimal)CurrentDeposit.Rate;

            dtpStart.Value = CurrentDeposit.StartDate;
            dtpEnd.Value = CurrentDeposit.EndDate ?? DateTime.Today.AddYears(1);
            chkPerpetual.Checked = CurrentDeposit.IsPerpetual;

            UpdateControlsState(null, null);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(cmbBank.Text) || string.IsNullOrWhiteSpace(cmbOwner.Text))
            {
                MessageBox.Show("Заполните Банк/Компанию и Владельца!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CurrentDeposit.Bank = cmbBank.Text;
            CurrentDeposit.Owner = cmbOwner.Text;
            CurrentDeposit.Amount = numAmount.Value;
            CurrentDeposit.Currency = cmbCurrency.SelectedItem.ToString();
            CurrentDeposit.PayoutType = cmbPayout.SelectedItem.ToString();

            CurrentDeposit.IsOPIF = chkOPIF.Checked;

            CurrentDeposit.Rate = (double)numRate.Value;
            CurrentDeposit.IsKeyRateLinked = chkIsKeyRateLinked.Checked;

            CurrentDeposit.StartDate = dtpStart.Value;

            // Если ОПИФ или бессрочный, сохраняем пустую дату
            if (chkOPIF.Checked || chkPerpetual.Checked)
                CurrentDeposit.EndDate = null;
            else
                CurrentDeposit.EndDate = dtpEnd.Value;

            CurrentDeposit.IsPerpetual = chkPerpetual.Checked;

            // Обновляем дату сверки при любом редактировании ===
            CurrentDeposit.LastReconciled = DateTime.Today;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}