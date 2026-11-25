using System;
using System.Windows;
using System.Windows.Controls;
using StatsTool.Data;

namespace StatsTool.Views
{
    public partial class LoginView : UserControl
    {
        private bool _passwordVisible;

        public LoginView()
        {
            InitializeComponent();
        }

        // =====================================================
        // GET THE EXISTING MAIN WINDOW
        // =====================================================

        private MainWindow? GetMainWindow()
        {
            return Window.GetWindow(this) as MainWindow;
        }

        // =====================================================
        // BACK TO START PAGE
        // =====================================================

        private void BackButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            GetMainWindow()?.ShowStart();
        }

        // =====================================================
        // PASSWORD VISIBILITY — FRONTEND TEAM
        // =====================================================
        // PasswordInput remains the source used by login logic.
        // When a user reveals the password, a normal TextBox displays
        // it. When hidden again, the visible text is cleared.
        // BACKEND TEAM: Never log this text or persist it locally.
        // DATABASE TEAM: No database action belongs to this toggle.

        private void PasswordVisibilityButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_passwordVisible)
            {
                VisiblePasswordTextBox.Text = PasswordInput.Password;
                PasswordInput.Visibility = Visibility.Collapsed;
                VisiblePasswordTextBox.Visibility = Visibility.Visible;
                PasswordVisibilityButton.Content = "Hide";
                _passwordVisible = true;
                VisiblePasswordTextBox.Focus();
                VisiblePasswordTextBox.CaretIndex =
                    VisiblePasswordTextBox.Text.Length;
            }
            else
            {
                PasswordInput.Password = VisiblePasswordTextBox.Text;
                VisiblePasswordTextBox.Clear();
                VisiblePasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordInput.Visibility = Visibility.Visible;
                PasswordVisibilityButton.Content = "Show";
                _passwordVisible = false;
                PasswordInput.Focus();
            }
        }

        // =====================================================
        // LOGIN
        // =====================================================

        private void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            FeedbackText.Visibility = Visibility.Collapsed;
            string login = UsernameTextBox.Text.Trim();

            if (_passwordVisible)
                PasswordInput.Password = VisiblePasswordTextBox.Text;

            // Keep the existing authentication source synchronized
            // if the user last typed while the password was visible.
            if (string.IsNullOrWhiteSpace(login) ||
                string.IsNullOrWhiteSpace(PasswordInput.Password))
            {
            // FRONTEND VALIDATION:
            // Only verify that required fields are filled.
            // Actual credential verification belongs to backend.
                ShowError("Enter your email or name and password.");
                return;
            }

            // =================================================
            // DATABASE AUTHENTICATION
            // =================================================
            //
            // 1. Connect to the SQL Server database.
            // 2. Find the account using its registered email.
            // 3. Verify the password against the stored password.
            // 4. Open the Dashboard only if the credentials match.
            //
            // Users can log in using their email only.
            // Do not hardcode usernames or passwords.
            //
            // =================================================
            try
            {
                DatabaseService.Account? account =
                    DatabaseService.Authenticate(login, PasswordInput.Password);

                if (account == null)
                {
                    ShowError("Incorrect email/name or password.");
                    return;
                }

                                // LOGIN SUCCESSFUL:
                                // Retrieve the authenticated user's details.
                                // Navigate to Dashboard after successful login.
                                // LOGIN FAILED:
                                // Email or password does not match.
                MainWindow.SetCurrentUser(account);
                GetMainWindow()?.ShowDashboard();
            }
            catch (Exception ex)
            {
                // DATABASE ERROR:
                // Display an error if the database connection fails.
                ShowError("Database connection failed: " + ex.Message);
            }
        }

        // =====================================================
        // SIGN UP NAVIGATION
        // =====================================================

        private void SignUpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            GetMainWindow()?.ShowSignUp();
        }

        // =====================================================
        // VALIDATION MESSAGE
        // =====================================================

        private void ShowError(string message)
        {
            FeedbackText.Text = message;
            FeedbackText.Visibility = Visibility.Visible;
        }
    }
}
