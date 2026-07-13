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

## Auth

POST `/api/auth/register` — { username, password } → { token, username }
POST `/api/auth/login` — { username, password } → { token, username }
GET `/api/auth/me` — Authorization: Bearer &lt;token&gt; → username

## Доски

GET `/api/boards` — мои доски (owner или участник)
GET `/api/boards/{id}` — доска по id
POST `/api/boards` — { title, description } → создаёт, текущий юзер = owner
PUT `/api/boards/{id}` — owner/editor
DELETE `/api/boards/{id}` — только owner
GET `/api/boards/{id}/members` — список участников с ролями
POST `/api/boards/{id}/members` — { username, role: Owner|Editor|Viewer } — только owner
DELETE `/api/boards/{id}/members/{userId}` — только owner

## Колонки

GET `/api/boards/{boardId}/columns` — список, любая роль
POST `/api/boards/{boardId}/columns` — { title } → owner/editor
PUT `/api/boards/{boardId}/columns/{id}` — { title, order } → owner/editor
DELETE `/api/boards/{boardId}/columns/{id}` — owner/editor

## Карточки

GET `/api/boards/{boardId}/columns/{columnId}/cards` — список, любая роль
POST `.../cards` — { title, description, deadline, assigneeId } → owner/editor
PUT `.../cards/{id}` — правка title/description/deadline/assignee
DELETE `.../cards/{id}` — owner/editor
PUT `.../cards/{id}/move` — { columnId, order } → drag-and-drop, owner/editor

## Статус

Коммит 1: структура решения, сущности, DbContext, пустой WPF-клиент.
Коммит 2: register/login/me, JWT, PBKDF2-хэш пароля.
Коммит 3: CRUD досок + участники/роли.
Коммит 4: CRUD колонок.
Коммит 5: CRUD карточек + move (drag-and-drop).
Комментарии — в следующем коммите.
