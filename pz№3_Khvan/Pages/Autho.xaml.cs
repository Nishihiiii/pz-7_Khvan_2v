using pz_3_Khvan.Models;
using pz_3_Khvan.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace pz_3_Khvan.Pages
{
	public partial class Autho : Page
	{
		private int _failedAttempts = 0;
		private string _captchaText = "";
		private DispatcherTimer _lockoutTimer;
		private DateTime _lockoutEndTime;

		public Autho()
		{
			InitializeComponent();
			InitializeTimer();
			CheckPersistentLockout();
		}

		private void InitializeTimer()
		{
			_lockoutTimer = new DispatcherTimer();
			_lockoutTimer.Interval = TimeSpan.FromSeconds(1);
			_lockoutTimer.Tick += LockoutTimer_Tick;
		}

		private void CheckPersistentLockout()
		{
			DateTime savedEndTime = Properties.Settings.Default.LockoutEndTime;
			if (savedEndTime > DateTime.Now)
			{
				StartLockout(savedEndTime);
			}
		}

		public void ResetFields()
		{
			txtbLogin.Clear();
			pswbPassword.Clear();
			txtVisiblePassword.Clear();
			txtboxCaptcha.Clear();
			chkShowPassword.IsChecked = false;

			txtBlockCaptcha.Visibility = Visibility.Collapsed;
			txtCaptcha.Visibility = Visibility.Collapsed;
			txtboxCaptcha.Visibility = Visibility.Collapsed;
			btnRefreshCaptcha.Visibility = Visibility.Collapsed;
			txtAttemptsLeft.Visibility = Visibility.Collapsed;
			txtBlockTimer.Visibility = Visibility.Collapsed;

			txtCaptcha.Text = "";
			txtAttemptsLeft.Text = "";
			txtBlockTimer.Text = "";
			_captchaText = "";
			_failedAttempts = 0;
		}

		private void GenerateCaptcha()
		{
			_captchaText = CaptchaGenerator.GenerateCaptchaText(6);
			txtCaptcha.TextDecorations = TextDecorations.Strikethrough;

			txtBlockCaptcha.Visibility = Visibility.Visible;
			txtCaptcha.Visibility = Visibility.Visible;
			txtboxCaptcha.Visibility = Visibility.Visible;
			btnRefreshCaptcha.Visibility = Visibility.Visible;

			txtCaptcha.Text = _captchaText;
			txtboxCaptcha.Clear();
		}

		private void btnRefreshCaptcha_Click(object sender, RoutedEventArgs e)
		{
			GenerateCaptcha();
		}

		private void chkShowPassword_Checked(object sender, RoutedEventArgs e)
		{
			txtVisiblePassword.Text = pswbPassword.Password;
			pswbPassword.Visibility = Visibility.Collapsed;
			txtVisiblePassword.Visibility = Visibility.Visible;
		}

		private void chkShowPassword_Unchecked(object sender, RoutedEventArgs e)
		{
			pswbPassword.Password = txtVisiblePassword.Text;
			txtVisiblePassword.Visibility = Visibility.Collapsed;
			pswbPassword.Visibility = Visibility.Visible;
		}

		private void btnEnterGuests_Click(object sender, RoutedEventArgs e)
		{
			NavigationService.Navigate(new Client());
		}

		private void btnEnter_Click(object sender, RoutedEventArgs e)
		{
			string login = txtbLogin.Text.Trim();
			string password = (chkShowPassword.IsChecked == true
				? txtVisiblePassword.Text
				: pswbPassword.Password).Trim();

			if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
			{
				MessageBox.Show("Введите логин и пароль!");
				return;
			}

			if (_failedAttempts > 0)
			{
				if (txtboxCaptcha.Text.Trim() != _captchaText)
				{
					LogLoginAttempt(login, false, "Неверная CAPTCHA");
					HandleFailedAttempt("Неверно введена CAPTCHA!");
					return;
				}
			}

			try
			{
				using (TelecomServicesDBEntities db = new TelecomServicesDBEntities())
				{
					string passwordHash = EncryptionManager.GenerateSha256SecurityKey(password);

					var user = db.Users.FirstOrDefault(x =>
						x.Login == login &&
						x.PasswordHash == passwordHash);

					if (user != null && user.IsActive == true)
					{
						LogLoginAttempt(login, true, null);
						MessageBox.Show("Авторизация прошла успешно!");

						string roleName = user.Roles.RoleName;
						ResetFields();
						NavigationService.Navigate(new Client(user, roleName));
					}
					else
					{
						string reason = user == null
							? "Неверный логин или пароль"
							: "Учетная запись неактивна";

						LogLoginAttempt(login, false, reason);
						HandleFailedAttempt(reason + "!");
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Ошибка при подключении к базе данных:\n" + ex.Message);
			}
		}

		private void HandleFailedAttempt(string errorMessage)
		{
			_failedAttempts++;
			pswbPassword.Clear();
			txtVisiblePassword.Clear();

			if (_failedAttempts == 1)
			{
				GenerateCaptcha();
				txtAttemptsLeft.Text = "Осталось попыток: 2";
				txtAttemptsLeft.Visibility = Visibility.Visible;
				MessageBox.Show(errorMessage);
			}
			else if (_failedAttempts == 2)
			{
				GenerateCaptcha();
				txtAttemptsLeft.Text = "Осталось попыток: 1";
				txtAttemptsLeft.Visibility = Visibility.Visible;
				MessageBox.Show(errorMessage);
			}
			else if (_failedAttempts >= 3)
			{
				txtAttemptsLeft.Visibility = Visibility.Collapsed;
				StartLockout(DateTime.Now.AddSeconds(30));
				MessageBox.Show("Превышено количество попыток входа. Форма заблокирована на 30 секунд.");
			}
		}

		private void StartLockout(DateTime endTime)
		{
			_lockoutEndTime = endTime;
			Properties.Settings.Default.LockoutEndTime = _lockoutEndTime;
			Properties.Settings.Default.Save();

			SetControlsEnabled(false);
			txtBlockTimer.Visibility = Visibility.Visible;
			UpdateTimerText();
			_lockoutTimer.Start();
		}

		private void LockoutTimer_Tick(object sender, EventArgs e)
		{
			UpdateTimerText();
		}

		private void UpdateTimerText()
		{
			TimeSpan remaining = _lockoutEndTime - DateTime.Now;
			int secondsLeft = (int)Math.Ceiling(remaining.TotalSeconds);

			if (secondsLeft > 0)
			{
				txtBlockTimer.Text = $"Превышено число попыток. Повторный ввод доступен через: {secondsLeft} сек.";
			}
			else
			{
				_lockoutTimer.Stop();
				Properties.Settings.Default.LockoutEndTime = DateTime.MinValue;
				Properties.Settings.Default.Save();

				txtBlockTimer.Visibility = Visibility.Collapsed;
				SetControlsEnabled(true);

				_failedAttempts = 0;
				txtBlockCaptcha.Visibility = Visibility.Collapsed;
				txtCaptcha.Visibility = Visibility.Collapsed;
				txtboxCaptcha.Visibility = Visibility.Collapsed;
				btnRefreshCaptcha.Visibility = Visibility.Collapsed;
				txtAttemptsLeft.Visibility = Visibility.Collapsed;
				txtboxCaptcha.Clear();
			}
		}

		private void SetControlsEnabled(bool isEnabled)
		{
			txtbLogin.IsEnabled = isEnabled;
			pswbPassword.IsEnabled = isEnabled;
			txtVisiblePassword.IsEnabled = isEnabled;
			chkShowPassword.IsEnabled = isEnabled;
			txtboxCaptcha.IsEnabled = isEnabled;
			btnEnter.IsEnabled = isEnabled;
			btnRefreshCaptcha.IsEnabled = isEnabled;
		}

		private void LogLoginAttempt(string login, bool isSuccess, string failReason)
		{
			try
			{
				using (TelecomServicesDBEntities db = new TelecomServicesDBEntities())
				{
					db.LoginHistory.Add(new LoginHistory
					{
						UserLogin = login,
						AttemptTime = DateTime.Now,
						IsSuccess = isSuccess,
						FailReason = failReason
					});
					db.SaveChanges();
				}
			}
			catch
			{
			
			}
		}
	}
}
