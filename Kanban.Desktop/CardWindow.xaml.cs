using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Kanban.Desktop;

public partial class CardWindow : Window
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly int _boardId;
    private readonly int _columnId;
    private readonly int _cardId;

    public CardWindow(int boardId, int columnId, int cardId)
    {
        InitializeComponent();
        _boardId = boardId;
        _columnId = columnId;
        _cardId = cardId;
        Loaded += async (s, e) => await Load();
    }

    private async Task Load()
    {
        var response = await App.Http.GetAsync($"api/boards/{_boardId}/columns/{_columnId}/cards/{_cardId}");
        var json = await response.Content.ReadAsStringAsync();
        var card = JsonSerializer.Deserialize<CardDto>(json, JsonOpts)!;

        TitleBox.Text = card.Title;
        DescriptionBox.Text = card.Description ?? "";
        DeadlinePicker.SelectedDate = card.Deadline;
        AssigneeBox.Text = card.AssigneeUsername ?? "";

        await LoadComments();
    }

    private async Task LoadComments()
    {
        CommentsPanel.Children.Clear();

        var response = await App.Http.GetAsync($"api/boards/{_boardId}/columns/{_columnId}/cards/{_cardId}/comments");
        var json = await response.Content.ReadAsStringAsync();
        var comments = JsonSerializer.Deserialize<List<CommentDto>>(json, JsonOpts) ?? new();

        foreach (var c in comments)
        {
            CommentsPanel.Children.Add(new TextBlock
            {
                Text = $"{c.AuthorUsername}: {c.Text}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2)
            });
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        int? assigneeId = null;
        if (!string.IsNullOrWhiteSpace(AssigneeBox.Text))
        {
            var response = await App.Http.GetAsync($"api/boards/{_boardId}/members");
            var json = await response.Content.ReadAsStringAsync();
            var members = JsonSerializer.Deserialize<List<MemberDto>>(json, JsonOpts) ?? new();
            assigneeId = members.FirstOrDefault(m => m.Username == AssigneeBox.Text)?.UserId;
        }

        var body = JsonSerializer.Serialize(new
        {
            Title = TitleBox.Text,
            Description = DescriptionBox.Text,
            Deadline = DeadlinePicker.SelectedDate,
            AssigneeId = assigneeId
        });

        await App.Http.PutAsync($"api/boards/{_boardId}/columns/{_columnId}/cards/{_cardId}",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Close();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        await App.Http.DeleteAsync($"api/boards/{_boardId}/columns/{_columnId}/cards/{_cardId}");
        Close();
    }

    private async void AddComment_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewCommentBox.Text))
            return;

        var body = JsonSerializer.Serialize(new { Text = NewCommentBox.Text });
        await App.Http.PostAsync($"api/boards/{_boardId}/columns/{_columnId}/cards/{_cardId}/comments",
            new StringContent(body, Encoding.UTF8, "application/json"));

        NewCommentBox.Clear();
        await LoadComments();
    }
}
