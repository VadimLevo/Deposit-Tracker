using System;
using System.Drawing;
using System.Windows.Forms;

namespace Учёт_Вкладов
{
    public partial class AddDepositForm : Form
    {
        public Deposit CurrentDeposit { get; private set; }
        private bool isEditMode;

        private ComboBox cmbBank;
        private ComboBox cmbOwner;
        private NumericUpDown numAmount;
        private ComboBox cmbCurrency;
        private NumericUpDown numRate;
        private ComboBox cmbPayoutType;
        private DateTimePicker dtpStartDate;
        private DateTimePicker dtpEndDate;
        private CheckBox chkIsTermless;
        private CheckBox chkIsKeyRateLinked;
        private Label lblRate;

        public AddDepositForm(string[] banks, string[] owners, Deposit depositToEdit = null)
        {
            InitializeComponent();
            InitializeCustomUI(banks, owners);

            if (depositToEdit != null)
            {
                isEditMode = true;
                CurrentDeposit = depositToEdit;
                this.Text = "Редактирование вклада";

                cmbBank.Text = depositToEdit.Bank;
                cmbOwner.Text = depositToEdit.Owner;
                numAmount.Value = depositToEdit.Amount;
                cmbCurrency.Text = depositToEdit.Currency;
                chkIsKeyRateLinked.Checked = depositToEdit.IsKeyRateLinked;
                numRate.Value = (decimal)depositToEdit.Rate;
                cmbPayoutType.Text = depositToEdit.PayoutType;
                dtpStartDate.Value = depositToEdit.StartDate;

                if (depositToEdit.EndDate.HasValue) dtpEndDate.Value = depositToEdit.EndDate.Value;
                else chkIsTermless.Checked = true;
            }
            else
            {
                CurrentDeposit = new Deposit { StartDate = DateTime.Today };
            }
        }

        private void InitializeCustomUI(string[] banks, string[] owners)
        {
            this.Text = "Новый вклад";
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
            this.Size = new Size(400, 530);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int startY = 20, stepY = 40;

            AddLabel("Банк:", 20, startY);
            cmbBank = new ComboBox { Location = new Point(150, startY), Width = 200, DropDownStyle = ComboBoxStyle.DropDown };
            // Включаем автоподстановку
            cmbBank.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbBank.AutoCompleteSource = AutoCompleteSource.ListItems;
            cmbBank.Items.AddRange(banks);
            this.Controls.Add(cmbBank);

            startY += stepY;
            AddLabel("Владелец:", 20, startY);
            cmbOwner = new ComboBox { Location = new Point(150, startY), Width = 200, DropDownStyle = ComboBoxStyle.DropDown };
            // Включаем автоподстановку
            cmbOwner.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbOwner.AutoCompleteSource = AutoCompleteSource.ListItems;
            cmbOwner.Items.AddRange(owners);
            this.Controls.Add(cmbOwner);

            startY += stepY;
            AddLabel("Сумма:", 20, startY);
            numAmount = new NumericUpDown { Location = new Point(150, startY), Width = 200, Maximum = 1000000000, DecimalPlaces = 2 };
            this.Controls.Add(numAmount);

            startY += stepY;
            AddLabel("Валюта:", 20, startY);
            cmbCurrency = new ComboBox { Location = new Point(150, startY), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCurrency.Items.AddRange(new string[] { "RUB", "USD", "CNY", "EUR" });
            cmbCurrency.SelectedIndex = 0;
            this.Controls.Add(cmbCurrency);

            startY += stepY;
            chkIsKeyRateLinked = new CheckBox { Text = "Привязка к ключевой ставке", Location = new Point(150, startY), Width = 200 };
            chkIsKeyRateLinked.CheckedChanged += (s, e) => lblRate.Text = chkIsKeyRateLinked.Checked ? "Надбавка/Вычет (%):" : "Ставка (%):";
            this.Controls.Add(chkIsKeyRateLinked);

            startY += 30;
            lblRate = AddLabel("Ставка (%):", 20, startY);
            numRate = new NumericUpDown { Location = new Point(150, startY), Width = 200, Maximum = 100, Minimum = -100, DecimalPlaces = 2 };
            this.Controls.Add(numRate);

            startY += stepY;
            AddLabel("Выплата:", 20, startY);
            cmbPayoutType = new ComboBox { Location = new Point(150, startY), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPayoutType.Items.AddRange(new string[] { "В конце срока", "Ежемесячно (капитализация)", "Ежемесячно (на счет)" });
            cmbPayoutType.SelectedIndex = 0;
            this.Controls.Add(cmbPayoutType);

            startY += stepY;
            AddLabel("Дата открытия:", 20, startY);
            dtpStartDate = new DateTimePicker { Location = new Point(150, startY), Width = 200, Format = DateTimePickerFormat.Short };
            this.Controls.Add(dtpStartDate);

            startY += stepY;
            AddLabel("Дата окончания:", 20, startY);
            dtpEndDate = new DateTimePicker { Location = new Point(150, startY), Width = 200, Format = DateTimePickerFormat.Short };
            this.Controls.Add(dtpEndDate);

            startY += 25;
            chkIsTermless = new CheckBox { Text = "Бессрочный (копилка)", Location = new Point(150, startY), Width = 200 };
            chkIsTermless.CheckedChanged += (s, e) => dtpEndDate.Enabled = !chkIsTermless.Checked;
            this.Controls.Add(chkIsTermless);

            startY += 50;
            Button btnSave = new Button { Text = "Сохранить", Location = new Point(150, startY), Width = 90, BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            Button btnCancel = new Button { Text = "Отмена", Location = new Point(260, startY), Width = 90, BackColor = Color.LightGray, FlatStyle = FlatStyle.Flat };
            btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(btnCancel);
        }

        private Label AddLabel(string text, int x, int y)
        {
            Label lbl = new Label { Text = text, Location = new Point(x, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9F) };
            this.Controls.Add(lbl);
            return lbl;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(cmbBank.Text) || string.IsNullOrWhiteSpace(cmbOwner.Text))
            {
                MessageBox.Show("Заполните поля Банк и Владелец!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CurrentDeposit.Bank = cmbBank.Text.Trim();
            CurrentDeposit.Owner = cmbOwner.Text.Trim();
            CurrentDeposit.Amount = numAmount.Value;
            CurrentDeposit.Currency = cmbCurrency.Text;
            CurrentDeposit.IsKeyRateLinked = chkIsKeyRateLinked.Checked;
            CurrentDeposit.Rate = (double)numRate.Value;
            CurrentDeposit.PayoutType = cmbPayoutType.Text;
            CurrentDeposit.StartDate = dtpStartDate.Value.Date;
            CurrentDeposit.EndDate = chkIsTermless.Checked ? (DateTime?)null : dtpEndDate.Value.Date;
            CurrentDeposit.IsClosed = false;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}