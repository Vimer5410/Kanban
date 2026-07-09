# Kanban

Учебная практика (кейс от VK, референс по функционалу — kaiten.ru).

## Стек

- Backend: ASP.NET Core Web API (.NET 8) + EF Core + SQLite
- Frontend: WPF (.NET 8)

## Запуск backend

```
cd Kanban.Api
dotnet restore
dotnet tool install --global dotnet-ef   # если ещё не стоит
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

## Запуск клиента

Открыть Kanban.Desktop как стартовый проект в Visual Studio, F5.

## Статус

Коммит 1: структура решения, сущности, DbContext, пустой WPF-клиент.
Auth, CRUD, drag-and-drop, комментарии — в следующих коммитах.
