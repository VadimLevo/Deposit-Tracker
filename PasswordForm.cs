using System;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace Учёт_Вкладов
{
    public class PasswordForm : Form
    {
        private TextBox txtPassword;
        private string currentHash;
        private bool isSetupMode;

        public string NewHash { get; private set; } = "";

        // НОВОЕ: Переменная, которая передаст пароль в главную форму для шифрования
        public string EnteredPassword { get; private set; } = "";

        public PasswordForm(string hash, bool setupMode = false)
        {
            currentHash = hash;
            isSetupMode = setupMode;

            this.Text = isSetupMode ? "Настройка пароля" : "Вход в программу";
            this.Size = new Size(350, 180);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label lblInfo = new Label { Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 9F) };
            lblInfo.Text = isSetupMode
                ? "Введите новый пароль (оставьте пустым для отключения):"
                : "Введите пароль для доступа к базе данных:";
            this.Controls.Add(lblInfo);

            txtPassword = new TextBox { Location = new Point(20, 50), Width = 290, PasswordChar = '●', Font = new Font("Segoe UI", 10F) };
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnOk_Click(s, e); };
            this.Controls.Add(txtPassword);

            Button btnOk = new Button { Text = "ОК", Location = new Point(130, 90), Width = 80, Height = 30, BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat };
            btnOk.Click += BtnOk_Click;
            this.Controls.Add(btnOk);

            this.ActiveControl = txtPassword;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            string plainPassword = txtPassword.Text; // Запоминаем то, что ввели
            string inputHash = string.IsNullOrWhiteSpace(plainPassword) ? "" : GetHash(plainPassword);

            if (isSetupMode)
            {
                NewHash = inputHash;
                EnteredPassword = plainPassword; // Передаем чистый пароль для перешифровки базы
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                if (inputHash == currentHash)
                {
                    EnteredPassword = plainPassword; // Передаем чистый пароль для расшифровки сессии
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Неверный пароль!", "Ошибка доступа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtPassword.Clear();
                }
            }
        }

        public static string GetHash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}