# Автопарк — Fleet Management System

Настольная система управления автопарком на **C# / .NET 8 / Windows Forms** с базой
**Microsoft SQL Server**. Реализованы роли, путевой учёт, контроль технического
обслуживания, топливо, штрафы/ДТП, KPI-дашборд, экспорт в Excel/PDF и журнал аудита.

> Рабочая папка проекта: `C:\Users\wemsie\Documents\Проект по умолчанию`
> Точка входа: `src\FleetManager.UI\FleetManager.UI.csproj` (сборка `FleetManager.exe`).

---

## 1. Быстрый запуск

### 1.1. Что нужно установить

| Компонент | Версия / примечание |
|---|---|
| Windows | 10/11 (x64) |
| .NET SDK | **8.0** (для сборки) или .NET 8 Desktop Runtime (для готового exe) |
| SQL Server | SQL Server 2019+ **или** SQL Server Express **или** LocalDB |
| SSMS | 22.x — для выполнения SQL-скрипта (необязательно, если используете `sqlcmd`) |

Скачать .NET 8 SDK: <https://dotnet.microsoft.com/download/dotnet/8.0>

### 1.2. Создать базу данных

1. Откройте SSMS 22 и подключитесь к своему экземпляру SQL Server
   (например, `localhost\SQLEXPRESS` или `(localdb)\MSSQLLocalDB`).
2. Откройте файл `database\FleetManagerDB.sql`.
3. Нажмите **F5 (Execute)**.

Скрипт **пересоздаёт** базу `FleetManagerDB` (`DROP DATABASE` + `CREATE DATABASE`),
создаёт таблицы, триггеры, хранимые процедуры и заполняет демо-данными
(10 автомобилей, 5 водителей, 30 записей пробега, 20 заправок, 10 работ ТО,
13 активных уведомлений).

Через командную строку:

```powershell
# SQL Server Express
sqlcmd -S "localhost\SQLEXPRESS" -E -i "database\FleetManagerDB.sql"

# LocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i "database\FleetManagerDB.sql"
```

### 1.3. Указать строку подключения

Откройте `src\FleetManager.UI\appsettings.json` и проверьте `ConnectionString`.
В этом проекте по умолчанию используется **LocalDB** (уже установлена вместе
с Visual Studio / SQL Server Express LocalDB):

```json
{
  "ConnectionString": "Server=(localdb)\\MSSQLLocalDB;Database=FleetManagerDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
}
```

Если база развёрнута на SQL Server Express, замените строку на:

```json
"ConnectionString": "Server=localhost\\SQLEXPRESS;Database=FleetManagerDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

Строку можно не менять в файле, а переопределить переменной окружения
(удобно для быстрых проверок):

```powershell
$env:FLEET_CONNECTIONSTRING='Server=(localdb)\MSSQLLocalDB;Database=FleetManagerDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
```

### 1.4. Собрать и запустить

```powershell
cd "C:\Users\wemsie\Documents\Проект по умолчанию"

# сборка всего решения
dotnet build FleetManager.sln

# запуск приложения
dotnet run --project src\FleetManager.UI
```

Либо запустите уже собранный файл:

```
src\FleetManager.UI\bin\Debug\net8.0-windows\FleetManager.exe
```

В Visual Studio: откройте `FleetManager.sln` → проект **FleetManager.UI** →
**F5**.

### 1.5. Вход в систему

| Логин | Пароль | Роль |
|---|---|---|
| `admin` | `Admin@123` | Администратор |
| `dispatcher` | `Dispatch@123` | Диспетчер |
| `driver1` … `driver5` | `Driver@123` | Водитель |

При первом запуске приложение проверяет подключение к БД и показывает статус
на экране входа. Если база недоступна — проверьте строку подключения.

---

## 2. Возможности по ролям

### Администратор
- все разделы и действия;
- управление пользователями (создание, редактирование, сброс пароля,
  включение/отключение учётной записи);
- журнал аудита с фильтрами и экспортом;
- управление планами ТО и справочниками;
- экспорт таблиц в Excel/PDF, смена собственного пароля, выбор темы.

### Диспетчер
- транспорт, водители, маршруты и назначение водителей;
- учёт пробега (начало/конец одометра), топлива, штрафов и ДТП;
- ТО и ремонт: выполнение работ, планы ТО, подтверждение уведомлений;
- дашборд KPI, экспорт Excel/PDF.

### Водитель
- только «свои» разделы: дашборд, маршруты, пробег, уведомления ТО;
- внесение показаний одометра по своим поездкам;
- история пробега и уведомления о сроках обслуживания;
- без доступа к пользователям, аудиту, топливу, штрафам и справочникам.

---

## 3. Структура решения

```
FleetManager.sln
├── database/
│   └── FleetManagerDB.sql        # полный скрипт БД: схема, seed, триггеры, SP
├── docs/
│   ├── ARCHITECTURE.md           # архитектура, модель данных, ключевые сценарии
│   └── USER_GUIDE.md             # краткое руководство пользователя
└── src/
    ├── FleetManager.Core/        # модели, enum, настройки (без зависимостей)
    ├── FleetManager.Data/        # ADO.NET + Dapper, репозитории, DatabaseContext
    ├── FleetManager.Services/    # бизнес-логика, авторизация, экспорт, мониторинг ТО
    └── FleetManager.UI/          # WinForms: формы, кастомные контролы, темы
```

Слои связаны только «вниз»: `UI → Services → Data → Core`.
UI не работает с SQL напрямую.

---

## 4. Технологии

- **.NET 8**, `net8.0-windows`, Windows Forms;
- **Microsoft.Data.SqlClient 5.2.2** + **Dapper 2.1.35** (ADO.NET);
- **EPPlus 7.4.2** — экспорт Excel (`.xlsx`);
- **QuestPDF 2024.12.3** — экспорт PDF;
- кастомная отрисовка интерфейса (GDI+, Segoe MDL2 Assets), без сторонних UI-библиотек;
- настройки — `System.Text.Json` + переменные окружения `FLEET_*`;
- пароли — SHA-256 с индивидуальной солью (для production рекомендуется
  перейти на PBKDF2/Argon2id).

---

## 5. Конфигурация `appsettings.json`

| Параметр | Назначение | По умолчанию |
|---|---|---|
| `ConnectionString` | строка подключения к SQL Server | `(localdb)\MSSQLLocalDB` |
| `MonitorIntervalMinutes` | период фоновой проверки ТО, мин | `5` |
| `UpcomingThresholdKm` | порог предупреждения «скоро ТО», км | `500` |
| `OverdueThresholdKm` | порог «просрочено», км | `1000` |
| `EnableSound` | звук при новых уведомлениях ТО | `true` |
| `CompanyName` | название организации | `ООО «ТрансЛогистика»` |
| `ExportFolder` | папка экспорта по умолчанию (пусто — выбор пользователем) | `""` |
| `AppTitle` | заголовок приложения | `Автопарк` |

Любой параметр можно переопределить переменной окружения:

```powershell
$env:FLEET_CONNECTIONSTRING='...'
$env:FLEET_MONITORINTERVALMINUTES='10'
$env:FLEET_UPCOMINGTHRESHOLDKM='500'
$env:FLEET_OVERDUETHRESHOLDKM='1000'
$env:FLEET_ENABLESOUND='false'
$env:FLEET_COMPANYNAME='ООО «Ромашка»'
$env:FLEET_EXPORTFOLDER='C:\Reports'
```

---

## 6. Контроль технического обслуживания

Для каждого автомобиля ведутся индивидуальные планы ТО
(`VehicleMaintenancePlans`): вид ТО, интервал в км, последнее выполнение,
дата последнего ТО, следующая дата.

- пороги по умолчанию: **500 км** — предупреждение (жёлтый), **0 км** — наступило,
  **−1000 км** — просрочено (красный);
- проверка выполняется **при запуске** приложения и **по таймеру**
  (`MaintenanceMonitor`, интервал из настроек);
- при новых критичных уведомлениях администратор/диспетчер получают
  **toast + звук**;
- счётчик активных уведомлений отображается в сайдбаре и на кнопке
  уведомлений;
- выполненные работы фиксируются в журнале: дата, пробег, стоимость,
  исполнитель, примечание, вложение (фото/акт);
- после сохранения работы план пересчитывается, уведомления обновляются.

---

## 7. Где лежат данные приложения

| Что | Путь |
|---|---|
| Журнал ошибок | `%APPDATA%\FleetManager\logs\app-YYYYMMDD.log` |
| Выбранная тема | `%APPDATA%\FleetManager\theme.txt` (`dark` / `light`) |
| Экспорт | папка, выбранная в диалоге сохранения, либо `ExportFolder` |

---

## 8. Частые проблемы

**«Нет подключения к SQL Server»**
- проверьте, что служба SQL Server (или LocalDB) запущена;
- проверьте имя экземпляра в строке подключения (`localhost\SQLEXPRESS`,
  `(localdb)\MSSQLLocalDB`, `.\SQLEXPRESS`);
- убедитесь, что база `FleetManagerDB` создана;
- для SQL-аутентификации добавьте `User Id=...;Password=...;` и включите
  смешанный режим проверки подлинности.

**Ошибка «CREATE/ALTER TRIGGER must be the first statement…» или ошибка 334**
- скрипт уже исправлен: вставки возвращают `SCOPE_IDENTITY()` через
  `SELECT CAST(SCOPE_IDENTITY() AS INT)`, а не `OUTPUT INSERTED.Id`
  (триггеры запрещают `OUTPUT` без `INTO`);
- пересоберите решение после обновления исходников.

**NuGet не скачивает пакеты (`NU1301`)**
- нужны пакеты Dapper, Microsoft.Data.SqlClient, EPPlus, QuestPDF;
- при офлайн-сборке используйте компьютер с доступом к nuget.org или
  предварительно заполненный локальный кэш NuGet.

**Приложение не запускается после сборки**
- закройте работающий `FleetManager.exe` (файл блокируется);
- проверьте, что установлен .NET 8 Desktop Runtime.

---

## 9. Публикация

```powershell
# framework-dependent (нужен установленный .NET 8 Desktop Runtime)
dotnet publish src\FleetManager.UI -c Release -r win-x64 --self-contained false -o publish

# self-contained (runtime включён; требует доступа к NuGet для runtime pack)
dotnet publish src\FleetManager.UI -c Release -r win-x64 --self-contained true -o publish
```

Скопируйте папку `publish` на рабочую машину, рядом положите/отредактируйте
`appsettings.json` и запустите `FleetManager.exe`.

---

## 10. Roadmap

- [ ] PBKDF2/Argon2id вместо SHA-256, блокировка после N неудачных входов;
- [ ] журналирование в файл через Serilog + ротация;
- [ ] назначение водителя на автомобиль (постоянное закрепление);
- [ ] уведомления по email/Telegram, push в трее;
- [ ] интеграция с GPS/ГЛОНАСС и путевыми листами;
- [ ] мобильный кабинет водителя;
- [ ] отчёты по рентабельности ТС и прогноз бюджета;
- [ ] миграции БД (EF Core Migrations / DbUp) вместо одного SQL-скрипта;
- [ ] автотесты сервисов и репозиториев (xUnit + Testcontainers);
- [ ] установщик MSIX/Inno Setup и автообновление.
