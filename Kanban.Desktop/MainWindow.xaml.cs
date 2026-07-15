using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kanban.Desktop;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private List<BoardDto> _boards = new();
    private int? _currentBoardId;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (s, e) => await LoadBoards();
    }

    private async Task LoadBoards()
    {
        var response = await App.Http.GetAsync("api/boards");
        var json = await response.Content.ReadAsStringAsync();
        _boards = JsonSerializer.Deserialize<List<BoardDto>>(json, JsonOpts) ?? new();

        BoardsCombo.Items.Clear();
        foreach (var b in _boards)
            BoardsCombo.Items.Add(b.Title);

        if (_boards.Count > 0)
            BoardsCombo.SelectedIndex = 0;
    }

    private async void BoardsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BoardsCombo.SelectedIndex < 0)
            return;

        _currentBoardId = _boards[BoardsCombo.SelectedIndex].Id;
        await LoadColumns();
    }

    private async void NewBoard_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewBoardTitle.Text))
            return;

        var body = JsonSerializer.Serialize(new { Title = NewBoardTitle.Text, Description = (string?)null });
        await App.Http.PostAsync("api/boards", new StringContent(body, Encoding.UTF8, "application/json"));
        NewBoardTitle.Clear();
        await LoadBoards();
    }

    private async Task LoadColumns()
    {
        ColumnsPanel.Children.Clear();
        if (_currentBoardId == null)
            return;

        var response = await App.Http.GetAsync($"api/boards/{_currentBoardId}/columns");
        var json = await response.Content.ReadAsStringAsync();
        var columns = JsonSerializer.Deserialize<List<ColumnDto>>(json, JsonOpts) ?? new();

        foreach (var col in columns)
        {
            var colBox = await BuildColumnBox(col);
            ColumnsPanel.Children.Add(colBox);
        }

        var addColBox = new StackPanel { Width = 220, Margin = new Thickness(5) };
        var addColTitle = new TextBox { Margin = new Thickness(0, 0, 0, 5) };
        var addColBtn = new Button { Content = "+ колонка" };
        addColBtn.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(addColTitle.Text))
                return;

            var body = JsonSerializer.Serialize(new { Title = addColTitle.Text });
            await App.Http.PostAsync($"api/boards/{_currentBoardId}/columns", new StringContent(body, Encoding.UTF8, "application/json"));
            await LoadColumns();
        };
        addColBox.Children.Add(addColTitle);
        addColBox.Children.Add(addColBtn);
        ColumnsPanel.Children.Add(addColBox);
    }

    private async Task<StackPanel> BuildColumnBox(ColumnDto col)
    {
        var box = new StackPanel { Width = 220, Margin = new Thickness(5), Background = Brushes.WhiteSmoke };
        box.Children.Add(new TextBlock { Text = col.Title, FontWeight = FontWeights.Bold, Margin = new Thickness(5) });

        var cardsPanel = new StackPanel { AllowDrop = true, MinHeight = 100 };
        cardsPanel.Drop += async (s, e) =>
        {
            if (!e.Data.GetDataPresent(typeof(CardRef)))
                return;

            var cardRef = (CardRef)e.Data.GetData(typeof(CardRef))!;
            var body = JsonSerializer.Serialize(new { ColumnId = col.Id, Order = 999999 });
            await App.Http.PutAsync(
                $"api/boards/{_currentBoardId}/columns/{cardRef.ColumnId}/cards/{cardRef.CardId}/move",
                new StringContent(body, Encoding.UTF8, "application/json"));
            await LoadColumns();
        };

        var response = await App.Http.GetAsync($"api/boards/{_currentBoardId}/columns/{col.Id}/cards");
        var json = await response.Content.ReadAsStringAsync();
        var cards = JsonSerializer.Deserialize<List<CardDto>>(json, JsonOpts) ?? new();

        foreach (var card in cards)
            cardsPanel.Children.Add(BuildCardBorder(card, col.Id));

        box.Children.Add(cardsPanel);

        var addCardTitle = new TextBox { Margin = new Thickness(5) };
        var addCardBtn = new Button { Content = "+ карточка", Margin = new Thickness(5) };
        addCardBtn.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(addCardTitle.Text))
                return;

            var body = JsonSerializer.Serialize(new { Title = addCardTitle.Text, Description = (string?)null, Deadline = (DateTime?)null, AssigneeId = (int?)null });
            await App.Http.PostAsync($"api/boards/{_currentBoardId}/columns/{col.Id}/cards", new StringContent(body, Encoding.UTF8, "application/json"));
            await LoadColumns();
        };
        box.Children.Add(addCardTitle);
        box.Children.Add(addCardBtn);

        return box;
    }

    private Border BuildCardBorder(CardDto card, int columnId)
    {
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(5),
            Padding = new Thickness(5)
        };

        var text = card.Title;
        if (card.Deadline != null)
            text += $"\n{card.Deadline:dd.MM}";
        if (card.AssigneeUsername != null)
            text += $"\n{card.AssigneeUsername}";

        border.Child = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };

        Point? dragStart = null;

        border.MouseLeftButtonDown += (s, e) => dragStart = e.GetPosition(null);

        border.MouseMove += (s, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || dragStart == null)
                return;

            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - dragStart.Value.X) < 5 && Math.Abs(pos.Y - dragStart.Value.Y) < 5)
                return;

            dragStart = null;
            var data = new DataObject(typeof(CardRef), new CardRef(card.Id, columnId));
            DragDrop.DoDragDrop(border, data, DragDropEffects.Move);
        };

        border.MouseLeftButtonUp += (s, e) =>
        {
            if (_currentBoardId == null)
                return;

            var win = new CardWindow(_currentBoardId.Value, columnId, card.Id);
            win.ShowDialog();
            _ = LoadColumns();
        };

        return border;
    }
}
