using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Kanban.Desktop;

public partial class LoginWindow : Window
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public LoginWindow()
    {
        InitializeComponent();
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        await DoAuth("api/auth/login");
    }

    private async void Register_Click(object sender, RoutedEventArgs e)
    {
        await DoAuth("api/auth/register");
    }

    private async Task DoAuth(string url)
    {
        var body = JsonSerializer.Serialize(new { Username = UsernameBox.Text, Password = PasswordBox.Password });
        var response = await App.Http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            ErrorText.Text = await response.Content.ReadAsStringAsync();
            return;
        }

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResult>(json, JsonOpts)!;

        App.Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Token);

        new MainWindow().Show();
        Close();
    }
}
