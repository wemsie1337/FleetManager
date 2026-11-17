# Архитектура FleetManager

## 1. Общая схема

```
┌──────────────────────────────────────────────────────────────┐
│  FleetManager.UI (WinForms, net8.0-windows)                   │
│  формы, кастомные контролы, темы, экспорт через сервисы       │
└───────────────────────────────┬──────────────────────────────┘
                                │ AppServices (composition root)
┌───────────────────────────────▼──────────────────────────────┐
│  FleetManager.Services                                       │
│  бизнес-логика, RBAC-сессия, аудит, мониторинг ТО, отчёты    │
└───────────────────────────────┬──────────────────────────────┘
                                │ репозитории
┌───────────────────────────────▼──────────────────────────────┐
│  FleetManager.Data                                           │
│  Dapper + Microsoft.Data.SqlClient, DatabaseContext          │
└───────────────────────────────┬──────────────────────────────┘
                                │ модели
┌───────────────────────────────▼──────────────────────────────┐
│  FleetManager.Core                                           │
│  POCO-модели, enum, AppSettings                              │
└──────────────────────────────────────────────────────────────┘
```

Правила зависимостей:

- `Core` не ссылается ни на кого;
- `Data` и `Services` ссылаются только на `Core`;
- `Services` дополнительно ссылается на `Data`;
- `UI` ссылается на `Core`, `Data`, `Services`;
- SQL-запросы находятся только в `Data`, UI не знает о таблицах.

## 2. Проекты и ключевые файлы

### FleetManager.Core
- `Models/Fleet.cs` — `Vehicle`, `VehicleBrand`, `VehicleStatus`, `Driver`;
- `Models/Identity.cs` — `User`, `Role`, `UserRole`;
- `Models/Operations.cs` — маршруты, пробег, топливо, штрафы;
- `Models/Maintenance.cs` — планы, работы, уведомления, `MaintenanceSeverity`;
- `Models/Dashboard.cs` — `FleetKpi`, `MonthlyCost`;
- `Configuration/AppSettings.cs` — настройки приложения.

### FleetManager.Data
- `DatabaseContext.cs` — единая точка создания `SqlConnection`, обёртки
  `QueryAsync` / `ExecuteAsync` / `QueryFirstOrDefaultAsync`;
- `Repositories/*` — по репозиторию на агрегат: `VehicleRepository`,
  `DriverRepository`, `RouteRepository`, `MileageRepository`, `FuelRepository`,
  `FineRepository`, `MaintenanceRepository`, `UserRepository`,
  `AuditRepository`, `DashboardRepository`, `LookupRepository`;
- `Repositories/SqlHelper.cs` — безопасное построение `LIKE`-фильтров и
  динамических `ORDER BY` (белый список колонок).

Все запросы параметризованы. Вставки с `IDENTITY` возвращают идентификатор
через `SELECT CAST(SCOPE_IDENTITY() AS INT)` — это совместимо с триггерами
(ошибка SQL Server 334 при `OUTPUT INSERTED.Id`).

### FleetManager.Services
- `AppServices.cs` — composition root: создаёт `DatabaseContext`, репозитории,
  сервисы и `SessionContext`;
- `AuthService.cs` — вход, выход, смена/сброс пароля, аудит;
- `SessionContext.cs` — текущий пользователь, `DriverId`, роль:
  `IsAdmin`, `IsDispatcher`, `IsDriver`, `CanManageFleet`;
- `VehicleService`, `DriverService`, `RouteService`, `MileageService`,
  `FuelService`, `FineService`, `MaintenanceService`, `UserService`,
  `AuditService`, `DashboardService` — валидация + вызовы репозиториев + аудит;
- `MaintenanceMonitor.cs` — фоновая проверка сроков ТО;
- `ReportService.cs` — Excel (EPPlus) и PDF (QuestPDF);
- `Security/PasswordHasher.cs` — SHA-256(UTF8(password + salt));
- `Infrastructure/ErrorTranslator.cs` — человекочитаемые сообщения об ошибках.

### FleetManager.UI
- `Program.cs` — настройка DPI/культуры, загрузка `appsettings.json`,
  `FLEET_*`, цикл «логин → главное окно → выход»;
- `Forms/LoginForm.cs` — экран входа, проверка БД, выбор темы;
- `Forms/MainForm.cs` — сайдбар, топбар, поиск, уведомления, профиль,
  переключение страниц, toast-хост, confirm/logout;
- `Views/*` — страницы разделов (наследуют `BaseView`);
- `Dialogs/*` — модальные формы добавления/редактирования;
- `Controls/*` — кастомные кнопки, поля, KPI-карточки, график, тосты,
  пагинатор, сайдбар;
- `Theme/*` — палитры светлой/тёмной темы и рекурсивное применение;
- `Helpers/*` — `Ui`, `GridStyler`, `Drawing`, `ModalHost`.

## 3. Модель данных

Основные таблицы (`database/FleetManagerDB.sql`):

| Таблица | Назначение |
|---|---|
| `Roles` | роли Admin / Dispatcher / Driver |
| `Users` | учётные записи: логин, хеш, соль, роль, активность |
| `VehicleBrands`, `VehicleStatuses`, `FuelTypes`, `MaintenanceTypes` | справочники |
| `Vehicles` | автомобили: марка, модель, госномер, год, статус, пробег |
| `Drivers` | водители: ФИО, пользователь, удостоверение, категория, телефон |
| `Routes` | маршруты: точки, даты, расстояние, водитель, автомобиль, статус |
| `MileageLogs` | пробег: одометр начало/конец, вычисляемый `TripMileage`, дата, автор |
| `FuelRecords` | заправки: литры, цена, сумма, одометр, водитель |
| `Fines` | штрафы и ДТП: тип, дата, сумма, описание, статус оплаты |
| `VehicleMaintenancePlans` | индивидуальные планы ТО по автомобилю и виду |
| `MaintenanceRecords` | журнал выполненных работ: дата, пробег, стоимость, исполнитель |
| `MaintenanceAlerts` | активные уведомления ТО и подтверждения |
| `AuditLog` | действия пользователей: кто, что, когда, старое/новое значение |

Связи: `Vehicles → VehicleBrands/VehicleStatuses`;
`Drivers → Users`; `Routes/MileageLogs/FuelRecords → Vehicles + Drivers`;
`VehicleMaintenancePlans/MaintenanceRecords → Vehicles + MaintenanceTypes`;
`AuditLog → Users`.

Вычисляемые поля:

- `MileageLogs.TripMileage = OdometerEnd − OdometerStart` (persisted computed);
- `FuelRecords.TotalCost = Liters × PricePerLiter` (persisted computed);
- `MaintenanceRecords` пересчитывает план через триггер/сервис.

Хранимые процедуры: `sp_GetFleetKpi`, `sp_GetMonthlyCosts`,
`sp_GetFuelConsumption` (план/факт расхода).

## 4. Поток выполнения запроса

1. View вызывает метод сервиса (`App.Mileage.CreateAsync(...)`).
2. Сервис проверяет права и входные данные, бросает `ValidationException`
   с понятным текстом.
3. Сервис вызывает репозиторий.
4. Репозиторий выполняет параметризованный SQL через `DatabaseContext`.
5. Сервис пишет запись в `AuditLog`.
6. View перезагружает данные и показывает toast об успехе.
7. Любое исключение проходит через `ErrorTranslator` и показывается
   в `MessageDialog`, подробности пишутся в `%APPDATA%\FleetManager\logs`.

## 5. RBAC

| Раздел / действие | Admin | Dispatcher | Driver |
|---|---|---|---|
| Дашборд | ✔ | ✔ | ✔ (свои поездки) |
| Транспорт | ✔ | ✔ | — |
| Водители | ✔ | ✔ | — |
| Маршруты | ✔ | ✔ | ✔ (только свои) |
| Пробег | ✔ | ✔ | ✔ (внести/история) |
| ТО и ремонт | ✔ | ✔ | уведомления |
| Топливо | ✔ | ✔ | — |
| Штрафы и ДТП | ✔ | ✔ | — |
| Пользователи | ✔ | — | — |
| Аудит | ✔ | — | — |
| Планы ТО / справочники | ✔ | ✔ | — |
| Экспорт Excel/PDF | ✔ | ✔ | ✔ (доступные разделы) |

Ограничения реализованы на трёх уровнях:

1. навигация строится по `UserRole` (`MainForm.Navigation`);
2. кнопки действий скрываются/отключаются (`CanManageFleet`);
3. сервисы повторно проверяют права (защита от обхода UI).

## 6. Ключевые формы

- `LoginForm` — вход, проверка подключения, светлая/тёмная тема;
- `MainForm` — каркас: сайдбар, топбар, глобальный поиск, уведомления,
  профиль, переключение страниц, toast-host;
- `DashboardView` — 5 KPI-карточек, график расходов, список уведомлений,
  последние поездки;
- `VehiclesView` / `VehicleEditForm` — автопарк;
- `DriversView` / `DriverEditForm` — водители и привязка к учётной записи;
- `RoutesView` / `RouteEditForm` — маршруты и назначение;
- `MileageView` / `MileageEditForm` — одометр, авторасчёт пробега;
- `MaintenanceView` / `MaintenanceRecordForm` / `MaintenancePlanForm` —
  сроки, журнал, планы, вложения;
- `FuelView` / `FuelEditForm` — заправки и план/факт;
- `FinesView` / `FineEditForm` — штрафы и ДТП;
- `UsersView` / `UserEditForm` / `PasswordResetForm` — пользователи;
- `ChangePasswordForm` — смена собственного пароля;
- `AuditView` — журнал действий;
- `MessageDialog` — единый стиль подтверждений и ошибок.

## 7. Фоновая логика ТО

```
MainForm.OnLoad
  └─ MaintenanceMonitor.Start()
       ├─ CheckNowAsync()                 // сразу после входа
       └─ Timer (MonitorIntervalMinutes)
            └─ MaintenanceService.GetAlertsAsync()
                 ├─ сравнение пробега/дат с планами
                 ├─ создание/обновление MaintenanceAlerts
                 ├─ событие AlertsChanged
                 └─ MainForm.ApplyAlerts()
                      ├─ бейджи в сайдбаре и на колокольчике
                      ├─ flyout «Уведомления ТО»
                      ├─ toast для Admin/Dispatcher
                      └─ SystemSounds.Exclamation (если EnableSound)
```

Пороги:

- `Remaining <= 0` → **Due** (наступило, оранжевый);
- `Remaining <= 0 && |Remaining| >= OverdueThresholdKm` → **Overdue**
  (просрочено, красный);
- `Remaining <= UpcomingThresholdKm` → **Upcoming** (жёлтый);
- иначе — без уведомления.

После сохранения работы `MaintenanceRecordForm` вызывает
`MaintenanceService.RegisterWorkAsync`, который обновляет план
(`LastMileage`, `LastDate`) и пересчитывает уведомления.

## 8. Toast-уведомления

- `ToastControl` — кастомный контрол с иконкой, заголовком, текстом,
  полосой времени жизни и закрытием по клику;
- `ToastHost` — прозрачный контейнер в правом верхнем углу, не более 3 тостов;
- порядок добавления контролов в WinForms определяет z-порядок: `ToastHost`
  добавляется в `MainForm` **первым**, поэтому рисуется поверх контента;
- пустой хост имеет **высоту 0** и не перекрывает страницу; высота
  рассчитывается как `count × 98 − 10`;
- при появлении тоста выполняется slide-in слева направо;
- ошибки и предупреждения живут 9 с, успех/информация — 5,5 с;
- при смене темы контейнер и полосы прокрутки перекрашиваются.

## 9. UI и темы

- собственные контролы: `GradientButton`, `RoundIconButton`, `UiTextField`,
  `UiComboBox`, `KpiCard`, `SimpleChart`, `CardPanel`, `SidebarButton`;
- иконки — шрифт Segoe MDL2 Assets;
- скругления, тени, hover-анимации, ripple, «зебра» в таблицах;
- адаптивная раскладка: минимум 1180×680, оптимизация под 1366×768 и 1920×1080;
- `ThemeManager` хранит выбор в `%APPDATA%\FleetManager\theme.txt`;
- `GridStyler` настраивает DataGridView и перекрашивает системные скроллбары
  через `SetWindowTheme("DarkMode_Explorer")`, повторно — после привязки
  данных и при смене темы;
- ширины колонок распределяются пропорционально «дизайнерским» весам
  (`BaseView.FitColumns`), а не через `DataGridView Fill`, чтобы не терять текст.

## 10. Валидация, ошибки, журналирование

- `ValidationException` — ожидаемые ошибки ввода (показываются пользователю);
- проверки: обязательные поля, диапазоны, даты, телефон (≥10 цифр),
  уникальность логина/госномера, одометр «конец ≥ начало», пароль ≥8 символов;
- `ErrorTranslator` переводит ошибки SQL/сети в понятные фразы;
- глобальные обработчики `Application.ThreadException` и
  `AppDomain.UnhandledException` пишут лог и показывают сообщение;
- журнал: `%APPDATA%\FleetManager\logs\app-YYYYMMDD.log`.

## 11. Безопасность

- пароли не хранятся в открытом виде: SHA-256 + 16-байтная соль на пользователя;
- сравнение хешей — побайтовое;
- все SQL-запросы параметризованы;
- аудит входов, неудачных попыток, изменений и сбросов паролей;
- сессия живёт в памяти; выход очищает `SessionContext`;
- для production: PBKDF2/Argon2id, HTTPS-сервис, ограничение попыток входа,
  шифрование строки подключения, роли SQL Server с минимальными правами.

## 12. Производительность

- Dapper и `async/await` без блокировки UI-потока;
- страницы кэшируются в `MainForm` и перезагружаются при переходе;
- пагинация таблиц на клиенте (`GridPaginator`), поиск с debounce;
- `DoubleBuffered` для кастомных контролов и таблиц;
- индексы в БД по внешним ключам и датам (см. SQL-скрипт);
- фоновый мониторинг ТО не блокирует интерфейс.

## 13. Развёртывание

1. Развернуть SQL Server и выполнить `database/FleetManagerDB.sql`.
2. Создать SQL-логин с правами `db_datareader`, `db_datawriter`,
   `EXECUTE` на схеме `dbo` (для продакшена).
3. Опубликовать приложение:
   `dotnet publish src\FleetManager.UI -c Release -r win-x64 --self-contained false -o publish`.
4. Положить `appsettings.json` рядом с `FleetManager.exe` или задать
   `FLEET_CONNECTIONSTRING`.
5. Запустить `FleetManager.exe`, войти под администратором и сменить
   демо-пароли.

## 14. Roadmap

- PBKDF2/Argon2id, блокировка после N неудачных входов;
- Serilog + ротация логов;
- закрепление водителя за автомобилем;
- email/Telegram-уведомления и tray-режим;
- GPS/ГЛОНАСС, путевые листы, мобильный кабинет;
- отчёты по рентабельности и прогноз бюджета;
- миграции БД (EF Core/DbUp), автотесты, установщик и автообновление.
