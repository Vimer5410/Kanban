using System.Net.Http;
using System.Windows;

namespace Kanban.Desktop;

public partial class App : Application
{
    public static readonly HttpClient Http = new HttpClient { BaseAddress = new Uri("http://localhost:5170/") };
}
