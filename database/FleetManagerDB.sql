/* ============================================================================
   FleetManagerDB — база данных системы «Автопарк»
   СУБД: Microsoft SQL Server 2019+ / SSMS 22
   ----------------------------------------------------------------------------
   ВНИМАНИЕ: скрипт ПЕРЕСОЗДАЁТ базу FleetManagerDB (DROP DATABASE).
   Перед запуском на рабочем сервере сделайте резервную копию.
   ----------------------------------------------------------------------------
   Пароли демо-пользователей:
     admin       / Admin@123
     dispatcher  / Dispatch@123
     driver1..5  / Driver@123
   Хеш: SHA-256(UTF8(password + salt)), hex в нижнем регистре.
   ============================================================================ */

USE [master];
GO

IF DB_ID(N'FleetManagerDB') IS NOT NULL
BEGIN
    ALTER DATABASE [FleetManagerDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [FleetManagerDB];
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FleetManagerDB')
    PRINT 'Используется проверка подлинности Windows. При необходимости создайте SQL-логин вручную.';
GO

CREATE DATABASE [FleetManagerDB];
GO

ALTER DATABASE [FleetManagerDB] SET RECOVERY SIMPLE;
GO

USE [FleetManagerDB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ============================================================================
   1. СПРАВОЧНИКИ
   ============================================================================ */

CREATE TABLE dbo.Roles
(
    Id          INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    RoleName    NVARCHAR(50)  NOT NULL,
    Description NVARCHAR(255) NULL,
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
);
GO

CREATE TABLE dbo.VehicleStatuses
(
    Id         INT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleStatuses PRIMARY KEY,
    StatusName NVARCHAR(50) NOT NULL,
    ColorHex   VARCHAR(7)   NOT NULL CONSTRAINT DF_VehicleStatuses_Color DEFAULT ('#64748B'),
    CONSTRAINT UQ_VehicleStatuses_StatusName UNIQUE (StatusName)
);
GO

CREATE TABLE dbo.VehicleBrands
(
    Id        INT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleBrands PRIMARY KEY,
    BrandName NVARCHAR(80) NOT NULL,
    CONSTRAINT UQ_VehicleBrands_BrandName UNIQUE (BrandName)
);
GO

CREATE TABLE dbo.FuelTypes
(
    Id            INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuelTypes PRIMARY KEY,
    FuelName      NVARCHAR(50)   NOT NULL,
    PricePerLiter DECIMAL(10, 2) NOT NULL CONSTRAINT DF_FuelTypes_Price DEFAULT (0),
    CONSTRAINT UQ_FuelTypes_FuelName UNIQUE (FuelName),
    CONSTRAINT CK_FuelTypes_Price CHECK (PricePerLiter >= 0)
);
GO

CREATE TABLE dbo.MaintenanceTypes
(
    Id          INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenanceTypes PRIMARY KEY,
    TypeName    NVARCHAR(100) NOT NULL,
    IntervalKm  INT           NOT NULL,
    Description NVARCHAR(500) NULL,
    CONSTRAINT UQ_MaintenanceTypes_TypeName UNIQUE (TypeName),
    CONSTRAINT CK_MaintenanceTypes_Interval CHECK (IntervalKm > 0)
);
GO

/* ============================================================================
   2. ПОЛЬЗОВАТЕЛИ, РОЛИ, ВОДИТЕЛИ
   ============================================================================ */

CREATE TABLE dbo.Users
(
    Id           INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Login        NVARCHAR(64)  NOT NULL,
    PasswordHash CHAR(64)      NOT NULL,
    Salt         VARCHAR(64)   NOT NULL,
    FullName     NVARCHAR(150) NOT NULL,
    RoleId       INT           NOT NULL,
    IsActive     BIT           NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedAt    DATETIME2(0)  NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    LastLoginAt  DATETIME2(0)  NULL,
    CONSTRAINT UQ_Users_Login UNIQUE (Login),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id)
);
GO

CREATE TABLE dbo.Drivers
(
    Id            INT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_Drivers PRIMARY KEY,
    UserId        INT          NULL,
    LicenseNumber NVARCHAR(30) NOT NULL,
    Category      NVARCHAR(10) NOT NULL,
    HireDate      DATE         NOT NULL,
    Phone         NVARCHAR(30) NOT NULL,
    IsActive      BIT          NOT NULL CONSTRAINT DF_Drivers_IsActive DEFAULT (1),
    CONSTRAINT UQ_Drivers_LicenseNumber UNIQUE (LicenseNumber),
    CONSTRAINT FK_Drivers_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id)
);
GO

/* ============================================================================
   3. ТРАНСПОРТ
   ============================================================================ */

CREATE TABLE dbo.Vehicles
(
    Id                  INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vehicles PRIMARY KEY,
    BrandId             INT            NOT NULL,
    Model               NVARCHAR(80)   NOT NULL,
    PlateNumber         NVARCHAR(20)   NOT NULL,
    Vin                 NVARCHAR(17)   NULL,
    Year                INT            NOT NULL,
    CurrentMileage      INT            NOT NULL CONSTRAINT DF_Vehicles_CurrentMileage DEFAULT (0),
    StatusId            INT            NOT NULL,
    FuelTypeId          INT            NOT NULL,
    PlannedConsumption  DECIMAL(6, 2)  NULL,
    PurchaseDate        DATE           NULL,
    Notes               NVARCHAR(1000) NULL,
    CreatedAt           DATETIME2(0)   NOT NULL CONSTRAINT DF_Vehicles_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2(0)   NOT NULL CONSTRAINT DF_Vehicles_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Vehicles_PlateNumber UNIQUE (PlateNumber),
    CONSTRAINT FK_Vehicles_Brands FOREIGN KEY (BrandId) REFERENCES dbo.VehicleBrands (Id),
    CONSTRAINT FK_Vehicles_Statuses FOREIGN KEY (StatusId) REFERENCES dbo.VehicleStatuses (Id),
    CONSTRAINT FK_Vehicles_FuelTypes FOREIGN KEY (FuelTypeId) REFERENCES dbo.FuelTypes (Id),
    CONSTRAINT CK_Vehicles_Year CHECK (Year BETWEEN 1950 AND 2100),
    CONSTRAINT CK_Vehicles_Mileage CHECK (CurrentMileage >= 0),
    CONSTRAINT CK_Vehicles_Consumption CHECK (PlannedConsumption IS NULL OR PlannedConsumption > 0)
);
GO

CREATE TABLE dbo.Routes
(
    Id             INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_Routes PRIMARY KEY,
    DriverId       INT            NOT NULL,
    VehicleId      INT            NOT NULL,
    StartPoint     NVARCHAR(200)  NOT NULL,
    EndPoint       NVARCHAR(200)  NOT NULL,
    PlannedMileage INT            NOT NULL,
    StartDate      DATETIME2(0)   NOT NULL,
    EndDate        DATETIME2(0)   NULL,
    Status         TINYINT        NOT NULL CONSTRAINT DF_Routes_Status DEFAULT (1),
    Notes          NVARCHAR(500)  NULL,
    CreatedAt      DATETIME2(0)   NOT NULL CONSTRAINT DF_Routes_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Routes_Drivers FOREIGN KEY (DriverId) REFERENCES dbo.Drivers (Id),
    CONSTRAINT FK_Routes_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles (Id),
    CONSTRAINT CK_Routes_PlannedMileage CHECK (PlannedMileage >= 0),
    CONSTRAINT CK_Routes_Status CHECK (Status BETWEEN 1 AND 4),
    CONSTRAINT CK_Routes_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate)
);
GO

CREATE TABLE dbo.MileageLogs
(
    Id              INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_MileageLogs PRIMARY KEY,
    VehicleId       INT           NOT NULL,
    DriverId        INT           NOT NULL,
    OdometerStart   INT           NOT NULL,
    OdometerEnd     INT           NOT NULL,
    TripMileage     AS (OdometerEnd - OdometerStart) PERSISTED,
    [Date]          DATE          NOT NULL,
    Note            NVARCHAR(500) NULL,
    CreatedByUserId INT           NULL,
    CreatedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_MileageLogs_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_MileageLogs_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles (Id),
    CONSTRAINT FK_MileageLogs_Drivers FOREIGN KEY (DriverId) REFERENCES dbo.Drivers (Id),
    CONSTRAINT FK_MileageLogs_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users (Id),
    CONSTRAINT CK_MileageLogs_Odometers CHECK (OdometerStart >= 0 AND OdometerEnd >= OdometerStart)
);
GO

/* ============================================================================
   4. ТЕХНИЧЕСКОЕ ОБСЛУЖИВАНИЕ
   ============================================================================ */

CREATE TABLE dbo.VehicleMaintenancePlans
(
    Id                 INT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleMaintenancePlans PRIMARY KEY,
    VehicleId          INT          NOT NULL,
    MaintenanceTypeId  INT          NOT NULL,
    IntervalKm         INT          NOT NULL,
    LastServiceMileage INT          NOT NULL CONSTRAINT DF_VehicleMaintenancePlans_LastMileage DEFAULT (0),
    LastServiceDate    DATE         NULL,
    IsActive           BIT          NOT NULL CONSTRAINT DF_VehicleMaintenancePlans_IsActive DEFAULT (1),
    CONSTRAINT FK_VehicleMaintenancePlans_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles (Id) ON DELETE CASCADE,
    CONSTRAINT FK_VehicleMaintenancePlans_Types FOREIGN KEY (MaintenanceTypeId) REFERENCES dbo.MaintenanceTypes (Id),
    CONSTRAINT UQ_VehicleMaintenancePlans UNIQUE (VehicleId, MaintenanceTypeId),
    CONSTRAINT CK_VehicleMaintenancePlans_Interval CHECK (IntervalKm > 0),
    CONSTRAINT CK_VehicleMaintenancePlans_LastMileage CHECK (LastServiceMileage >= 0)
);
GO

CREATE TABLE dbo.MaintenanceRecords
(
    Id                INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenanceRecords PRIMARY KEY,
    VehicleId         INT            NOT NULL,
    MaintenanceTypeId INT            NOT NULL,
    [Date]            DATE           NOT NULL,
    MileageAtService  INT            NOT NULL,
    Cost              DECIMAL(12, 2) NOT NULL CONSTRAINT DF_MaintenanceRecords_Cost DEFAULT (0),
    Performer         NVARCHAR(150)  NOT NULL,
    Notes             NVARCHAR(1000) NULL,
    AttachmentPath    NVARCHAR(500)  NULL,
    CreatedByUserId   INT            NULL,
    CreatedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_MaintenanceRecords_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_MaintenanceRecords_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles (Id),
    CONSTRAINT FK_MaintenanceRecords_Types FOREIGN KEY (MaintenanceTypeId) REFERENCES dbo.MaintenanceTypes (Id),
    CONSTRAINT FK_MaintenanceRecords_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users (Id),
    CONSTRAINT CK_MaintenanceRecords_Mileage CHECK (MileageAtService >= 0),
    CONSTRAINT CK_MaintenanceRecords_Cost CHECK (Cost >= 0)
);
GO

CREATE TABLE dbo.MaintenanceAlerts
(
    Id                INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenanceAlerts PRIMARY KEY,
    VehicleId         INT           NOT NULL,
    MaintenanceTypeId INT           NOT NULL,
    DueMileage        INT           NOT NULL,
    Severity          NVARCHAR(20)  NOT NULL CONSTRAINT DF_MaintenanceAlerts_Severity DEFAULT (N'Due'),
    IsAcknowledged    BIT           NOT NULL CONSTRAINT DF_MaintenanceAlerts_Ack DEFAULT (0),
    CreatedAt         DATETIME2(0)  NOT NULL CONSTRAINT DF_MaintenanceAlerts_CreatedAt DEFAULT (SYSUTCDATETIME()),
    AcknowledgedAt    DATETIME2(0)  NULL,
    CONSTRAINT FK_MaintenanceAlerts_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles (Id) ON DELETE CASCADE,
    CONSTRAINT FK_MaintenanceAlerts_Types FOREIGN KEY (MaintenanceTypeId) REFERENCES dbo.MaintenanceTypes (Id),
    CONSTRAINT UQ_MaintenanceAlerts UNIQUE (VehicleId, MaintenanceTypeId, DueMileage),
    CONSTRAINT CK_MaintenanceAlerts_Severity CHECK (Severity IN (N'Upcoming', N'Due', N'Overdue'))
);
GO

/* ============================================================================
   5. ТОПЛИВО, ШТРАФЫ, АУДИТ
   ============================================================================ */

CREATE TABLE dbo.FuelRecords
(
    Id           INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuelRecords PRIMARY KEY,
    VehicleId    INT            NOT NULL,
    [Date]       DATE           NOT NULL,
    Liters       DECIMAL(10, 2) NOT NULL,
    CostPerLiter DECIMAL(10, 2) NOT NULL,
    TotalCost    AS (Liters * CostPerLiter) PERSISTED,
    Odometer     INT            NOT NULL,
    DriverId     INT            NULL,
    CreatedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_FuelRecords_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_FuelRecords_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles (Id),
    CONSTRAINT FK_FuelRecords_Drivers FOREIGN KEY (DriverId) REFERENCES dbo.Drivers (Id),
    CONSTRAINT CK_FuelRecords_Liters CHECK (Liters > 0),
    CONSTRAINT CK_FuelRecords_Price CHECK (CostPerLiter >= 0),
    CONSTRAINT CK_FuelRecords_Odometer CHECK (Odometer >= 0)
);
GO

CREATE TABLE dbo.Fines
(
    Id           INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_Fines PRIMARY KEY,
    VehicleId    INT            NOT NULL,
    DriverId     INT            NULL,
    [Date]       DATE           NOT NULL,
    Amount       DECIMAL(12, 2) NOT NULL,
    Description  NVARCHAR(1000) NOT NULL,
    IsPaid       BIT            NOT NULL CONSTRAINT DF_Fines_IsPaid DEFAULT (0),
    PaidDate     DATE           NULL,
    IncidentType VARCHAR(20)    NOT NULL CONSTRAINT DF_Fines_IncidentType DEFAULT ('Fine'),
    CreatedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_Fines_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Fines_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles (Id),
    CONSTRAINT FK_Fines_Drivers FOREIGN KEY (DriverId) REFERENCES dbo.Drivers (Id),
    CONSTRAINT CK_Fines_Amount CHECK (Amount > 0),
    CONSTRAINT CK_Fines_IncidentType CHECK (IncidentType IN ('Fine', 'Accident'))
);
GO

CREATE TABLE dbo.AuditLog
(
    Id        BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    UserId    INT           NULL,
    [Action]  NVARCHAR(100) NOT NULL,
    TableName NVARCHAR(100) NOT NULL,
    RecordId  NVARCHAR(50)  NULL,
    OldValue  NVARCHAR(MAX) NULL,
    NewValue  NVARCHAR(MAX) NULL,
    [Timestamp] DATETIME2(0) NOT NULL CONSTRAINT DF_AuditLog_Timestamp DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_AuditLog_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id)
);
GO

/* ============================================================================
   6. ИНДЕКСЫ
   ============================================================================ */

CREATE INDEX IX_Vehicles_StatusId      ON dbo.Vehicles (StatusId) INCLUDE (PlateNumber, CurrentMileage);
CREATE INDEX IX_Vehicles_BrandId       ON dbo.Vehicles (BrandId);
CREATE UNIQUE INDEX UX_Vehicles_Vin    ON dbo.Vehicles (Vin) WHERE Vin IS NOT NULL;
CREATE UNIQUE INDEX UX_Drivers_UserId  ON dbo.Drivers (UserId) WHERE UserId IS NOT NULL;
CREATE INDEX IX_Users_RoleId           ON dbo.Users (RoleId);
CREATE INDEX IX_Drivers_UserId         ON dbo.Drivers (UserId);
CREATE INDEX IX_Routes_DriverId        ON dbo.Routes (DriverId) INCLUDE (Status, StartDate);
CREATE INDEX IX_Routes_VehicleId       ON dbo.Routes (VehicleId);
CREATE INDEX IX_Routes_Status          ON dbo.Routes (Status, StartDate);
CREATE INDEX IX_MileageLogs_Vehicle    ON dbo.MileageLogs (VehicleId, [Date] DESC) INCLUDE (TripMileage, OdometerEnd);
CREATE INDEX IX_MileageLogs_Driver     ON dbo.MileageLogs (DriverId, [Date] DESC);
CREATE INDEX IX_MaintenanceRecords_Veh ON dbo.MaintenanceRecords (VehicleId, [Date] DESC) INCLUDE (Cost, MileageAtService);
CREATE INDEX IX_MaintenanceRecords_Type ON dbo.MaintenanceRecords (MaintenanceTypeId);
CREATE INDEX IX_Plans_Vehicle          ON dbo.VehicleMaintenancePlans (VehicleId);
CREATE INDEX IX_Alerts_Active          ON dbo.MaintenanceAlerts (IsAcknowledged, Severity) INCLUDE (VehicleId, DueMileage);
CREATE INDEX IX_FuelRecords_Vehicle    ON dbo.FuelRecords (VehicleId, [Date] DESC) INCLUDE (Liters, TotalCost);
CREATE INDEX IX_Fines_Vehicle          ON dbo.Fines (VehicleId, [Date] DESC);
CREATE INDEX IX_Fines_IsPaid           ON dbo.Fines (IsPaid) INCLUDE (Amount);
CREATE INDEX IX_AuditLog_Timestamp     ON dbo.AuditLog ([Timestamp] DESC);
CREATE INDEX IX_AuditLog_UserId        ON dbo.AuditLog (UserId, [Timestamp] DESC);
GO

/* ============================================================================
   7. ПРЕДСТАВЛЕНИЯ
   ============================================================================ */

CREATE VIEW dbo.vw_Vehicles
AS
SELECT v.Id,
       v.BrandId,
       b.BrandName,
       v.Model,
       v.PlateNumber,
       v.Vin,
       v.Year,
       v.CurrentMileage,
       v.StatusId,
       s.StatusName,
       v.FuelTypeId,
       f.FuelName,
       v.PlannedConsumption,
       v.PurchaseDate,
       v.Notes
FROM dbo.Vehicles v
JOIN dbo.VehicleBrands  b ON b.Id = v.BrandId
JOIN dbo.VehicleStatuses s ON s.Id = v.StatusId
JOIN dbo.FuelTypes       f ON f.Id = v.FuelTypeId;
GO

CREATE VIEW dbo.vw_MaintenanceDue
AS
SELECT v.Id                         AS VehicleId,
       v.PlateNumber,
       CONCAT(b.BrandName, ' ', v.Model) AS VehicleName,
       mt.Id                        AS MaintenanceTypeId,
       mt.TypeName,
       p.IntervalKm,
       p.LastServiceMileage,
       p.LastServiceDate,
       p.LastServiceMileage + p.IntervalKm AS NextDueMileage,
       v.CurrentMileage,
       (p.LastServiceMileage + p.IntervalKm) - v.CurrentMileage AS RemainingKm
FROM dbo.VehicleMaintenancePlans p
JOIN dbo.Vehicles v          ON v.Id = p.VehicleId
JOIN dbo.VehicleBrands b     ON b.Id = v.BrandId
JOIN dbo.MaintenanceTypes mt ON mt.Id = p.MaintenanceTypeId
WHERE p.IsActive = 1
  AND v.StatusId <> 4;  -- списанные ТС не требуют ТО
GO

/* ============================================================================
   8. ТРИГГЕРЫ
   ============================================================================ */

-- 8.1. Автоматическое обновление пробега ТС при добавлении/изменении записи пробега
CREATE TRIGGER trg_MileageLogs_UpdateMileage
ON dbo.MileageLogs
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE v
       SET v.CurrentMileage = CASE WHEN v.CurrentMileage < x.MaxEnd THEN x.MaxEnd ELSE v.CurrentMileage END,
           v.UpdatedAt = SYSUTCDATETIME()
    FROM dbo.Vehicles v
    JOIN (SELECT VehicleId, MAX(OdometerEnd) AS MaxEnd
          FROM inserted
          GROUP BY VehicleId) x ON x.VehicleId = v.Id;
END;
GO

-- 8.2. После выполнения ТО обновляем план и закрываем уведомления
CREATE TRIGGER trg_MaintenanceRecords_AfterInsert
ON dbo.MaintenanceRecords
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- Создаём план, если для ТС/вида ТО его ещё нет
    INSERT INTO dbo.VehicleMaintenancePlans (VehicleId, MaintenanceTypeId, IntervalKm, LastServiceMileage, LastServiceDate)
    SELECT x.VehicleId, x.MaintenanceTypeId, mt.IntervalKm, x.MaxMileage, x.LastDate
    FROM (SELECT i.VehicleId,
                 i.MaintenanceTypeId,
                 MAX(i.MileageAtService) AS MaxMileage,
                 MAX(i.[Date])           AS LastDate
          FROM inserted i
          GROUP BY i.VehicleId, i.MaintenanceTypeId) x
    JOIN dbo.MaintenanceTypes mt ON mt.Id = x.MaintenanceTypeId
    WHERE NOT EXISTS (SELECT 1
                      FROM dbo.VehicleMaintenancePlans p
                      WHERE p.VehicleId = x.VehicleId
                        AND p.MaintenanceTypeId = x.MaintenanceTypeId);

    -- Обновляем «последнее ТО» только если новый пробег больше прежнего
    ;WITH latest AS
    (
        SELECT i.VehicleId, i.MaintenanceTypeId, i.MileageAtService, i.[Date],
               ROW_NUMBER() OVER (PARTITION BY i.VehicleId, i.MaintenanceTypeId
                                  ORDER BY i.MileageAtService DESC, i.[Date] DESC) AS rn
        FROM inserted i
    )
    UPDATE p
       SET p.LastServiceMileage = l.MileageAtService,
           p.LastServiceDate    = l.[Date]
    FROM dbo.VehicleMaintenancePlans p
    JOIN latest l ON l.VehicleId = p.VehicleId
                 AND l.MaintenanceTypeId = p.MaintenanceTypeId
                 AND l.rn = 1
    WHERE l.MileageAtService >= p.LastServiceMileage;

    -- Закрываем уведомления, срок которых уже пройден выполненным ТО
    UPDATE a
       SET a.IsAcknowledged = 1,
           a.AcknowledgedAt = SYSUTCDATETIME()
    FROM dbo.MaintenanceAlerts a
    JOIN inserted i ON i.VehicleId = a.VehicleId
                   AND i.MaintenanceTypeId = a.MaintenanceTypeId
    WHERE a.IsAcknowledged = 0
      AND a.DueMileage <= i.MileageAtService;
END;
GO

/* ============================================================================
   9. ХРАНИМЫЕ ПРОЦЕДУРЫ
   ============================================================================ */

-- 9.1. KPI для дашборда
CREATE PROCEDURE dbo.sp_GetFleetKpi
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MonthStart DATE = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
    DECLARE @NextMonth  DATE = DATEADD(MONTH, 1, @MonthStart);

    SELECT
        (SELECT COUNT(*) FROM dbo.Vehicles)                                                   AS TotalVehicles,
        (SELECT COUNT(*) FROM dbo.Vehicles WHERE StatusId = 1)                                AS OnLine,
        (SELECT COUNT(*) FROM dbo.Vehicles WHERE StatusId = 2)                                AS InRepair,
        (SELECT COUNT(*) FROM dbo.Vehicles WHERE StatusId = 3)                                AS InMaintenance,
        (SELECT COUNT(*) FROM dbo.Vehicles WHERE StatusId = 4)                                AS Retired,
        (SELECT COUNT(*) FROM dbo.Drivers WHERE IsActive = 1)                                 AS ActiveDrivers,
        (SELECT COUNT(*) FROM dbo.Routes WHERE Status IN (1, 2))                              AS ActiveRoutes,
        (SELECT COUNT(*) FROM dbo.vw_MaintenanceDue WHERE (LastServiceMileage + IntervalKm) - CurrentMileage BETWEEN 1 AND 500) AS UpcomingCount,
        (SELECT COUNT(*) FROM dbo.vw_MaintenanceDue WHERE (LastServiceMileage + IntervalKm) - CurrentMileage <= 0
                                                       AND (LastServiceMileage + IntervalKm) - CurrentMileage >= -1000)         AS DueCount,
        (SELECT COUNT(*) FROM dbo.vw_MaintenanceDue WHERE (LastServiceMileage + IntervalKm) - CurrentMileage < -1000)           AS OverdueCount,
        ISNULL((SELECT SUM(TripMileage) FROM dbo.MileageLogs
                WHERE [Date] >= @MonthStart AND [Date] < @NextMonth), 0)                      AS MonthMileage,
        ISNULL((SELECT SUM(TotalCost) FROM dbo.FuelRecords
                WHERE [Date] >= @MonthStart AND [Date] < @NextMonth), 0)                      AS MonthFuelCost,
        ISNULL((SELECT SUM(Cost) FROM dbo.MaintenanceRecords
                WHERE [Date] >= @MonthStart AND [Date] < @NextMonth), 0)                      AS MonthMaintenanceCost,
        (SELECT COUNT(*) FROM dbo.Fines WHERE IsPaid = 0)                                     AS UnpaidFines,
        ISNULL((SELECT SUM(Amount) FROM dbo.Fines WHERE IsPaid = 0), 0)                       AS UnpaidFinesAmount;
END;
GO

-- 9.2. Список ТС, которым требуется ТО
CREATE PROCEDURE dbo.sp_GetMaintenanceDue
    @UpcomingKm INT = 500,
    @OverdueKm  INT = 1000
AS
BEGIN
    SET NOCOUNT ON;

    IF @UpcomingKm IS NULL OR @UpcomingKm < 0 SET @UpcomingKm = 500;
    IF @OverdueKm  IS NULL OR @OverdueKm  < 0 SET @OverdueKm  = 1000;

    ;WITH data AS
    (
        SELECT VehicleId, PlateNumber, VehicleName, MaintenanceTypeId, TypeName, IntervalKm,
               LastServiceMileage, LastServiceDate, NextDueMileage, CurrentMileage, RemainingKm
        FROM dbo.vw_MaintenanceDue
    ),
    scored AS
    (
        SELECT *,
               CASE WHEN RemainingKm < -@OverdueKm THEN N'Overdue'
                    WHEN RemainingKm <= 0          THEN N'Due'
                    WHEN RemainingKm <= @UpcomingKm THEN N'Upcoming'
                    ELSE N'OK' END AS Severity
        FROM data
    )
    SELECT VehicleId, PlateNumber, VehicleName, MaintenanceTypeId, TypeName, IntervalKm,
           LastServiceMileage, LastServiceDate, NextDueMileage, CurrentMileage, RemainingKm,
           Severity
    FROM scored
    WHERE Severity <> N'OK'
    ORDER BY CASE Severity WHEN N'Overdue' THEN 1 WHEN N'Due' THEN 2 ELSE 3 END,
             RemainingKm;
END;
GO

-- 9.3. Синхронизация журнала уведомлений с фактическим состоянием ТО
CREATE PROCEDURE dbo.sp_RefreshMaintenanceAlerts
    @UpcomingKm INT = 500,
    @OverdueKm  INT = 1000
AS
BEGIN
    SET NOCOUNT ON;

    IF @UpcomingKm IS NULL OR @UpcomingKm < 0 SET @UpcomingKm = 500;
    IF @OverdueKm  IS NULL OR @OverdueKm  < 0 SET @OverdueKm  = 1000;

    ;WITH currentDue AS
    (
        SELECT VehicleId, MaintenanceTypeId, NextDueMileage AS DueMileage,
               CASE WHEN RemainingKm < -@OverdueKm THEN N'Overdue'
                    WHEN RemainingKm <= 0          THEN N'Due'
                    WHEN RemainingKm <= @UpcomingKm THEN N'Upcoming'
                    ELSE N'OK' END AS Severity
        FROM dbo.vw_MaintenanceDue
    )
    MERGE dbo.MaintenanceAlerts AS t
    USING (SELECT VehicleId, MaintenanceTypeId, DueMileage, Severity
           FROM currentDue
           WHERE Severity <> N'OK') AS s
       ON t.VehicleId = s.VehicleId
      AND t.MaintenanceTypeId = s.MaintenanceTypeId
      AND t.DueMileage = s.DueMileage
    WHEN MATCHED AND t.IsAcknowledged = 0 AND t.Severity <> s.Severity
        THEN UPDATE SET t.Severity = s.Severity
    WHEN NOT MATCHED BY TARGET
        THEN INSERT (VehicleId, MaintenanceTypeId, DueMileage, Severity)
             VALUES (s.VehicleId, s.MaintenanceTypeId, s.DueMileage, s.Severity);

    -- Удаляем устаревшие неподтверждённые уведомления (план изменился / ТО выполнено)
    DELETE a
    FROM dbo.MaintenanceAlerts a
    WHERE a.IsAcknowledged = 0
      AND NOT EXISTS (SELECT 1
                      FROM dbo.vw_MaintenanceDue d
                      WHERE d.VehicleId = a.VehicleId
                        AND d.MaintenanceTypeId = a.MaintenanceTypeId
                        AND d.NextDueMileage = a.DueMileage);
END;
GO

-- 9.4. Расходы по месяцам (топливо + ТО + пробег)
CREATE PROCEDURE dbo.sp_GetMonthlyCosts
    @Year INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Year IS NULL SET @Year = YEAR(GETDATE());

    ;WITH months AS
    (
        SELECT 1 AS MonthNo
        UNION ALL
        SELECT MonthNo + 1 FROM months WHERE MonthNo < 12
    )
    SELECT m.MonthNo AS [Month],
           ISNULL(f.FuelCost, 0)          AS FuelCost,
           ISNULL(mr.MaintenanceCost, 0)  AS MaintenanceCost,
           ISNULL(ml.Mileage, 0)          AS Mileage
    FROM months m
    OUTER APPLY (SELECT SUM(TotalCost) AS FuelCost
                 FROM dbo.FuelRecords
                 WHERE YEAR([Date]) = @Year AND MONTH([Date]) = m.MonthNo) f
    OUTER APPLY (SELECT SUM(Cost) AS MaintenanceCost
                 FROM dbo.MaintenanceRecords
                 WHERE YEAR([Date]) = @Year AND MONTH([Date]) = m.MonthNo) mr
    OUTER APPLY (SELECT SUM(TripMileage) AS Mileage
                 FROM dbo.MileageLogs
                 WHERE YEAR([Date]) = @Year AND MONTH([Date]) = m.MonthNo) ml
    ORDER BY m.MonthNo
    OPTION (MAXRECURSION 12);
END;
GO

-- 9.5. Фактический расход топлива на 100 км
CREATE PROCEDURE dbo.sp_GetFuelConsumption
AS
BEGIN
    SET NOCOUNT ON;

    SELECT v.Id AS VehicleId,
           v.PlateNumber,
           CONCAT(b.BrandName, ' ', v.Model) AS VehicleName,
           ISNULL(m.Mileage, 0) AS TotalMileage,
           ISNULL(f.Liters, 0)  AS TotalLiters,
           ISNULL(f.Cost, 0)    AS TotalCost,
           CASE WHEN ISNULL(m.Mileage, 0) > 0
                THEN CAST(ISNULL(f.Liters, 0) * 100.0 / m.Mileage AS DECIMAL(8, 2)) END AS FactConsumption,
           v.PlannedConsumption AS PlanConsumption
    FROM dbo.Vehicles v
    JOIN dbo.VehicleBrands b ON b.Id = v.BrandId
    -- Пробег считаем по диапазону одометра (макс. конец - мин. начало), а не суммой
    -- суточных записей: архивные дни могут отсутствовать, и расход был бы завышен.
    OUTER APPLY (SELECT MAX(OdometerEnd) - MIN(OdometerStart) AS Mileage
                 FROM dbo.MileageLogs WHERE VehicleId = v.Id) m
    OUTER APPLY (SELECT SUM(Liters) AS Liters, SUM(TotalCost) AS Cost FROM dbo.FuelRecords WHERE VehicleId = v.Id) f
    ORDER BY v.PlateNumber;
END;
GO

-- 9.6. Пробег ТС по дням за месяц
CREATE PROCEDURE dbo.sp_GetVehicleMileageSummary
    @VehicleId INT,
    @Year      INT,
    @Month     INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CAST([Date] AS DATE) AS [Date],
           SUM(TripMileage)     AS TripMileage,
           MIN(OdometerStart)   AS OdometerStart,
           MAX(OdometerEnd)     AS OdometerEnd,
           COUNT(*)             AS Trips
    FROM dbo.MileageLogs
    WHERE VehicleId = @VehicleId
      AND YEAR([Date]) = @Year
      AND MONTH([Date]) = @Month
    GROUP BY CAST([Date] AS DATE)
    ORDER BY [Date];
END;
GO

-- 9.7. Журнал аудита
CREATE PROCEDURE dbo.sp_GetAuditLog
    @Top INT = 300
AS
BEGIN
    SET NOCOUNT ON;
    IF @Top IS NULL OR @Top <= 0 SET @Top = 300;
    SELECT TOP (@Top) a.Id, a.UserId, ISNULL(u.FullName, N'Система') AS UserName,
           a.[Action], a.TableName, a.RecordId, a.OldValue, a.NewValue, a.[Timestamp]
    FROM dbo.AuditLog a
    LEFT JOIN dbo.Users u ON u.Id = a.UserId
    ORDER BY a.[Timestamp] DESC;
END;
GO

/* ============================================================================
   10. ТЕСТОВЫЕ ДАННЫЕ
   ============================================================================ */

SET NOCOUNT ON;

INSERT INTO dbo.Roles (RoleName, Description) VALUES
    (N'Admin',      N'Полный доступ ко всем разделам и настройкам'),
    (N'Dispatcher', N'Маршруты, пробег, ТО, ремонты, ДТП и штрафы'),
    (N'Driver',     N'Свои маршруты, ввод пробега, история поездок');

INSERT INTO dbo.VehicleStatuses (StatusName, ColorHex) VALUES
    (N'На линии',  '#22C55E'),
    (N'В ремонте', '#F97316'),
    (N'На ТО',     '#3B82F6'),
    (N'Списан',    '#64748B');

INSERT INTO dbo.VehicleBrands (BrandName) VALUES
    (N'KAMAZ'), (N'GAZ'), (N'Volvo'), (N'Scania'), (N'MAN'),
    (N'Ford'), (N'Hyundai'), (N'Mercedes-Benz');

INSERT INTO dbo.FuelTypes (FuelName, PricePerLiter) VALUES
    (N'ДТ',     68.50),
    (N'АИ-92',  56.20),
    (N'АИ-95',  61.80),
    (N'Газ',    32.40);

INSERT INTO dbo.MaintenanceTypes (TypeName, IntervalKm, Description) VALUES
    (N'ТО-1',             10000, N'Регламентное ТО каждые 10 000 км'),
    (N'ТО-2',             30000, N'Расширенное ТО каждые 30 000 км'),
    (N'Замена масла',      7500, N'Замена моторного масла и фильтров'),
    (N'Замена шин',       40000, N'Сезонная замена/шиномонтаж'),
    (N'Диагностика ГБО',  15000, N'Проверка газобаллонного оборудования');

-- Пароли: admin/Admin@123, dispatcher/Dispatch@123, driverN/Driver@123
INSERT INTO dbo.Users (Login, PasswordHash, Salt, FullName, RoleId, IsActive) VALUES
    (N'admin',      'fd72ccc64a40e0837f525d381c86c51d3cb67e748eb2b574c2de95cfd362bd69', 'a1b2c3d4e5f60718293a4b5c6d7e8f90', N'Смирнов Алексей Викторович', 1, 1),
    (N'dispatcher', 'b99b710fd208d78988082013d83b25ff87830b1e03aeb318d3262f4eb6a1c8e3', 'b2c3d4e5f60718293a4b5c6d7e8f90a1', N'Ковалёва Марина Сергеевна',   2, 1),
    (N'driver1',    '12fa1bf4252e90fb556a3532bb1ebae8272bc8ce620eccccb90834a6de7985c9', 'c3d4e5f60718293a4b5c6d7e8f90a1b2', N'Иванов Пётр Николаевич',      3, 1),
    (N'driver2',    'cb59ef8b73f92236e0086aa8a54e2e97e121415ba36b2b457c9520f7c662825a', 'd4e5f60718293a4b5c6d7e8f90a1b2c3', N'Сидоров Андрей Игоревич',     3, 1),
    (N'driver3',    '2e3a8342a20137fb44e4e64e0a6ca438b978263d2371c2f5c3e8958b6b4e8078', 'e5f60718293a4b5c6d7e8f90a1b2c3d4', N'Кузнецов Дмитрий Олегович',   3, 1),
    (N'driver4',    '2a36f50957df9ba388fe1e4fa9422561a4b21b68c00a8288b9bdd98e1ed02a87', 'f60718293a4b5c6d7e8f90a1b2c3d4e5', N'Попов Сергей Анатольевич',    3, 1),
    (N'driver5',    '58ba398d62c43a7f60bf891ebb502e93d01a4baedb106cc791d31dc04f1064a4', '0718293a4b5c6d7e8f90a1b2c3d4e5f6', N'Фёдоров Илья Владимирович',   3, 1);

INSERT INTO dbo.Drivers (UserId, LicenseNumber, Category, HireDate, Phone, IsActive) VALUES
    (3, N'77 12 345678', N'CE', DATEADD(YEAR, -6, CAST(GETDATE() AS DATE)), N'+7 (916) 111-22-33', 1),
    (4, N'50 98 765432', N'CE', DATEADD(YEAR, -4, CAST(GETDATE() AS DATE)), N'+7 (925) 222-33-44', 1),
    (5, N'99 45 112233', N'C',  DATEADD(YEAR, -7, CAST(GETDATE() AS DATE)), N'+7 (903) 333-44-55', 1),
    (6, N'77 33 445566', N'CE', DATEADD(YEAR, -2, CAST(GETDATE() AS DATE)), N'+7 (999) 444-55-66', 1),
    (7, N'50 77 889900', N'C',  DATEADD(YEAR, -5, CAST(GETDATE() AS DATE)), N'+7 (985) 555-66-77', 1);

INSERT INTO dbo.Vehicles
    (BrandId, Model, PlateNumber, Vin, Year, CurrentMileage, StatusId, FuelTypeId, PlannedConsumption, PurchaseDate, Notes)
VALUES
    (1, N'5490',            N'А123ВС77', N'XTC54900011223344', 2020, 80750,  1, 1, 28.50, DATEADD(YEAR, -4, CAST(GETDATE() AS DATE)), N'Магистральный тягач'),
    (2, N'Газель Next',     N'В456ЕК50', N'XGA12345678901234', 2021, 45230,  1, 2, 14.00, DATEADD(YEAR, -3, CAST(GETDATE() AS DATE)), N'Городская доставка'),
    (3, N'FH',              N'С789МН99', N'YV2RT40A123456789', 2019, 154300, 2, 1, 30.00, DATEADD(YEAR, -5, CAST(GETDATE() AS DATE)), N'Ремонт КПП'),
    (4, N'R450',            N'Е321ОР77', N'YS2R4X20011223344', 2018, 212500, 1, 1, 31.00, DATEADD(YEAR, -6, CAST(GETDATE() AS DATE)), N'Дальние рейсы'),
    (5, N'TGX',             N'К654РТ50', N'WMA06XZZ112233445', 2022, 38600,  3, 1, 29.50, DATEADD(YEAR, -2, CAST(GETDATE() AS DATE)), N'Плановое ТО-2'),
    (6, N'Transit',         N'М987УХ77', N'WF0XXXTTG12345678', 2021, 62300,  1, 1, 11.50, DATEADD(YEAR, -3, CAST(GETDATE() AS DATE)), N'Сервисная бригада'),
    (7, N'HD78',            N'Н147СВ99', N'KMJ12345678901234', 2020, 88900,  1, 1, 17.50, DATEADD(YEAR, -4, CAST(GETDATE() AS DATE)), N'Самосвал'),
    (8, N'Sprinter',        N'О258ТМ77', N'WDB90665712345678', 2022, 27400,  1, 1, 12.00, DATEADD(YEAR, -2, CAST(GETDATE() AS DATE)), N'Перевозка персонала'),
    (1, N'65115',           N'Р369АВ50', N'XTC65115009876543', 2017, 198700, 4, 1, 33.00, DATEADD(YEAR, -8, CAST(GETDATE() AS DATE)), N'Списан по акту'),
    (2, N'Газель NN',       N'Т741НЕ77', N'XGA98765432109876', 2023, 15800,  1, 2, 13.50, DATEADD(YEAR, -1, CAST(GETDATE() AS DATE)), N'Новый автомобиль');

INSERT INTO dbo.Routes (DriverId, VehicleId, StartPoint, EndPoint, PlannedMileage, StartDate, EndDate, Status, Notes) VALUES
    (1, 1, N'Москва, склад «Южный»',   N'Казань, ТЦ «Восток»',       820, DATEADD(DAY, -1, GETDATE()),  DATEADD(DAY, 1, GETDATE()),  2, N'Доставка сборного груза'),
    (1, 3, N'Москва',                  N'Санкт-Петербург',           710, DATEADD(DAY, -8, GETDATE()),  DATEADD(DAY, -7, GETDATE()), 3, N'Выполнен'),
    (2, 2, N'Химки',                   N'Мытищи',                    45,  DATEADD(DAY, -2, GETDATE()),  DATEADD(DAY, -2, GETDATE()), 3, N'Городской рейс'),
    (2, 10,N'Москва',                  N'Тверь',                     180, DATEADD(DAY, 2, GETDATE()),   NULL,                        1, N'Запланирован'),
    (3, 4, N'Москва',                  N'Нижний Новгород',           420, DATEADD(DAY, -3, GETDATE()),  DATEADD(DAY, -2, GETDATE()), 3, N'Дальний рейс'),
    (4, 6, N'Москва',                  N'Обнинск',                   120, DATEADD(DAY, 0, GETDATE()),   NULL,                        2, N'Сервисное обслуживание клиентов'),
    (4, 8, N'Москва',                  N'Аэропорт Шереметьево',      60,  DATEADD(DAY, -5, GETDATE()),  DATEADD(DAY, -5, GETDATE()), 3, N'Трансфер'),
    (5, 7, N'Москва',                  N'Подольск',                  70,  DATEADD(DAY, 1, GETDATE()),   NULL,                        1, N'Песок, стройплощадка'),
    (5, 2, N'Москва',                  N'Коломна',                   110, DATEADD(DAY, -12, GETDATE()), DATEADD(DAY, -12, GETDATE()), 3, N'Выполнен'),
    (3, 4, N'Москва',                  N'Воронеж',                   520, DATEADD(DAY, 4, GETDATE()),   NULL,                        1, N'Планируется');

-- 30 записей пробега (20 свежих + 10 исторических для графиков)
INSERT INTO dbo.MileageLogs (VehicleId, DriverId, OdometerStart, OdometerEnd, [Date], Note, CreatedByUserId) VALUES
    (1, 1, 79900, 80320, CAST(DATEADD(DAY, -5, GETDATE()) AS DATE), N'Смена 1', 2),
    (1, 1, 80320, 80750, CAST(DATEADD(DAY, -1, GETDATE()) AS DATE), N'Смена 2', 2),
    (2, 2, 44200, 44620, CAST(DATEADD(DAY, -5, GETDATE()) AS DATE), N'Город', 2),
    (2, 2, 44620, 45230, CAST(DATEADD(DAY, -1, GETDATE()) AS DATE), N'Город + область', 2),
    (3, 1, 152800, 153300, CAST(DATEADD(DAY, -4, GETDATE()) AS DATE), N'Рейс', 2),
    (3, 1, 153300, 154300, CAST(DATEADD(DAY, -2, GETDATE()) AS DATE), N'Рейс', 2),
    (4, 3, 211000, 211700, CAST(DATEADD(DAY, -4, GETDATE()) AS DATE), N'Межгород', 2),
    (4, 3, 211700, 212500, CAST(DATEADD(DAY, -1, GETDATE()) AS DATE), N'Межгород', 2),
    (5, 5, 38100, 38400, CAST(DATEADD(DAY, -3, GETDATE()) AS DATE), N'До ТО', 2),
    (5, 5, 38400, 38600, CAST(DATEADD(DAY, -1, GETDATE()) AS DATE), N'До ТО', 2),
    (6, 4, 61700, 62000, CAST(DATEADD(DAY, -6, GETDATE()) AS DATE), N'Сервис', 2),
    (6, 4, 62000, 62300, CAST(DATEADD(DAY, -1, GETDATE()) AS DATE), N'Сервис', 2),
    (7, 5, 88100, 88500, CAST(DATEADD(DAY, -7, GETDATE()) AS DATE), N'Самосвал', 2),
    (7, 5, 88500, 88900, CAST(DATEADD(DAY, -2, GETDATE()) AS DATE), N'Самосвал', 2),
    (8, 4, 26800, 27100, CAST(DATEADD(DAY, -4, GETDATE()) AS DATE), N'Трансфер', 2),
    (8, 4, 27100, 27400, CAST(DATEADD(DAY, -1, GETDATE()) AS DATE), N'Трансфер', 2),
    (9, 3, 196500, 197500, CAST(DATEADD(DAY, -20, GETDATE()) AS DATE), N'Последний рейс', 2),
    (9, 3, 197500, 198700, CAST(DATEADD(DAY, -15, GETDATE()) AS DATE), N'Последний рейс', 2),
    (10, 2, 15200, 15500, CAST(DATEADD(DAY, -5, GETDATE()) AS DATE), N'Обкатка', 2),
    (10, 2, 15500, 15800, CAST(DATEADD(DAY, -1, GETDATE()) AS DATE), N'Обкатка', 2),
    (1, 1, 74800, 75600, CAST(DATEADD(DAY, -95, GETDATE()) AS DATE), N'Архив', 1),
    (1, 1, 75600, 76400, CAST(DATEADD(DAY, -65, GETDATE()) AS DATE), N'Архив', 1),
    (2, 2, 40600, 41400, CAST(DATEADD(DAY, -85, GETDATE()) AS DATE), N'Архив', 1),
    (2, 2, 41400, 42400, CAST(DATEADD(DAY, -55, GETDATE()) AS DATE), N'Архив', 1),
    (3, 1, 148600, 149600, CAST(DATEADD(DAY, -120, GETDATE()) AS DATE), N'Архив', 1),
    (4, 3, 205500, 206500, CAST(DATEADD(DAY, -100, GETDATE()) AS DATE), N'Архив', 1),
    (6, 4, 59600, 60400, CAST(DATEADD(DAY, -75, GETDATE()) AS DATE), N'Архив', 1),
    (7, 5, 85600, 86400, CAST(DATEADD(DAY, -70, GETDATE()) AS DATE), N'Архив', 1),
    (8, 4, 24400, 25200, CAST(DATEADD(DAY, -50, GETDATE()) AS DATE), N'Архив', 1),
    (10, 2, 13400, 14000, CAST(DATEADD(DAY, -45, GETDATE()) AS DATE), N'Архив', 1);

-- Обновляем текущий одометр вручную, чтобы триггер не «поднял» пробег выше факта
UPDATE v SET CurrentMileage = x.MaxEnd
FROM dbo.Vehicles v
JOIN (SELECT VehicleId, MAX(OdometerEnd) AS MaxEnd FROM dbo.MileageLogs GROUP BY VehicleId) x
     ON x.VehicleId = v.Id
WHERE v.CurrentMileage < x.MaxEnd;

-- Планы ТО: для каждого ТС — ТО-1, ТО-2 и замена масла
INSERT INTO dbo.VehicleMaintenancePlans (VehicleId, MaintenanceTypeId, IntervalKm, LastServiceMileage, LastServiceDate, IsActive) VALUES
    (1, 1, 10000, 71000, CAST(DATEADD(DAY, -80, GETDATE()) AS DATE), 1),
    (1, 2, 30000, 60000, CAST(DATEADD(DAY, -160, GETDATE()) AS DATE), 1),
    (1, 3,  7500, 74000, CAST(DATEADD(DAY, -35, GETDATE()) AS DATE), 1),
    (2, 1, 10000, 37000, CAST(DATEADD(DAY, -110, GETDATE()) AS DATE), 1),
    (2, 2, 30000, 20000, CAST(DATEADD(DAY, -250, GETDATE()) AS DATE), 1),
    (2, 3,  7500, 38000, CAST(DATEADD(DAY, -45, GETDATE()) AS DATE), 1),
    (3, 1, 10000, 148000, CAST(DATEADD(DAY, -90, GETDATE()) AS DATE), 1),
    (3, 2, 30000, 125000, CAST(DATEADD(DAY, -210, GETDATE()) AS DATE), 1),
    (3, 3,  7500, 147000, CAST(DATEADD(DAY, -60, GETDATE()) AS DATE), 1),
    (4, 1, 10000, 210000, CAST(DATEADD(DAY, -30, GETDATE()) AS DATE), 1),
    (4, 2, 30000, 190000, CAST(DATEADD(DAY, -140, GETDATE()) AS DATE), 1),
    (4, 3,  7500, 206000, CAST(DATEADD(DAY, -25, GETDATE()) AS DATE), 1),
    (5, 1, 10000, 29000, CAST(DATEADD(DAY, -70, GETDATE()) AS DATE), 1),
    (5, 2, 30000, 10000, CAST(DATEADD(DAY, -300, GETDATE()) AS DATE), 1),
    (5, 3,  7500, 31200, CAST(DATEADD(DAY, -40, GETDATE()) AS DATE), 1),
    (6, 1, 10000, 52000, CAST(DATEADD(DAY, -120, GETDATE()) AS DATE), 1),
    (6, 2, 30000, 32000, CAST(DATEADD(DAY, -280, GETDATE()) AS DATE), 1),
    (6, 3,  7500, 56200, CAST(DATEADD(DAY, -50, GETDATE()) AS DATE), 1),
    (7, 1, 10000, 78000, CAST(DATEADD(DAY, -100, GETDATE()) AS DATE), 1),
    (7, 2, 30000, 58000, CAST(DATEADD(DAY, -240, GETDATE()) AS DATE), 1),
    (7, 3,  7500, 80000, CAST(DATEADD(DAY, -75, GETDATE()) AS DATE), 1),
    (8, 1, 10000, 18000, CAST(DATEADD(DAY, -60, GETDATE()) AS DATE), 1),
    (8, 2, 30000, 0,     NULL, 1),
    (8, 3,  7500, 20000, CAST(DATEADD(DAY, -30, GETDATE()) AS DATE), 1),
    (9, 1, 10000, 190000, CAST(DATEADD(DAY, -150, GETDATE()) AS DATE), 1),
    (9, 2, 30000, 180000, CAST(DATEADD(DAY, -300, GETDATE()) AS DATE), 1),
    (9, 3,  7500, 193000, CAST(DATEADD(DAY, -120, GETDATE()) AS DATE), 1),
    (10, 1, 10000, 10000, CAST(DATEADD(DAY, -90, GETDATE()) AS DATE), 1),
    (10, 2, 30000, 0,     NULL, 1),
    (10, 3,  7500, 8000,  CAST(DATEADD(DAY, -40, GETDATE()) AS DATE), 1),
    (1, 4, 40000, 50000, CAST(DATEADD(DAY, -360, GETDATE()) AS DATE), 1),
    (5, 5, 15000, 20000, CAST(DATEADD(DAY, -180, GETDATE()) AS DATE), 1);

-- История ТО
INSERT INTO dbo.MaintenanceRecords (VehicleId, MaintenanceTypeId, [Date], MileageAtService, Cost, Performer, Notes, CreatedByUserId) VALUES
    (1, 1, CAST(DATEADD(DAY, -80, GETDATE()) AS DATE), 71000, 18500.00, N'ООО «СервисТрак»', N'Замена фильтров, диагностика', 2),
    (1, 3, CAST(DATEADD(DAY, -35, GETDATE()) AS DATE), 74000, 9200.00,  N'ООО «СервисТрак»', N'Масло Shell 15W-40, фильтр', 2),
    (2, 1, CAST(DATEADD(DAY, -110, GETDATE()) AS DATE), 37000, 7800.00,  N'ИП Гаврилов', N'ТО-1', 2),
    (3, 2, CAST(DATEADD(DAY, -210, GETDATE()) AS DATE), 125000, 34200.00, N'ООО «Вольво-Центр»', N'ТО-2, ремень ГРМ', 2),
    (4, 1, CAST(DATEADD(DAY, -30, GETDATE()) AS DATE), 210000, 21000.00, N'ООО «Скан-Сервис»', N'Плановое ТО', 2),
    (5, 3, CAST(DATEADD(DAY, -40, GETDATE()) AS DATE), 31200, 8900.00,  N'ООО «МАН-Сервис»', N'Замена масла', 2),
    (6, 1, CAST(DATEADD(DAY, -120, GETDATE()) AS DATE), 52000, 12400.00, N'ООО «Форд-Сервис»', N'ТО-1', 2),
    (7, 3, CAST(DATEADD(DAY, -75, GETDATE()) AS DATE), 80000, 7500.00,  N'ИП Гаврилов', N'Замена масла', 2),
    (8, 1, CAST(DATEADD(DAY, -60, GETDATE()) AS DATE), 18000, 9800.00,  N'ООО «Мерседес-Сервис»', N'ТО-1', 2),
    (10, 3, CAST(DATEADD(DAY, -40, GETDATE()) AS DATE), 8000, 6200.00, N'ООО «ГАЗ-Сервис»', N'Замена масла', 2);

-- Заправки за последние 6 месяцев
INSERT INTO dbo.FuelRecords (VehicleId, [Date], Liters, CostPerLiter, Odometer, DriverId) VALUES
    (1, CAST(DATEADD(DAY, -160, GETDATE()) AS DATE), 420.00, 64.10, 74200, 1),
    (1, CAST(DATEADD(DAY, -120, GETDATE()) AS DATE), 380.00, 65.40, 76100, 1),
    (1, CAST(DATEADD(DAY, -75,  GETDATE()) AS DATE), 450.00, 66.20, 78400, 1),
    (1, CAST(DATEADD(DAY, -25,  GETDATE()) AS DATE), 410.00, 68.10, 80300, 1),
    (2, CAST(DATEADD(DAY, -150, GETDATE()) AS DATE), 120.00, 55.80, 40200, 2),
    (2, CAST(DATEADD(DAY, -95,  GETDATE()) AS DATE), 140.00, 56.10, 42000, 2),
    (2, CAST(DATEADD(DAY, -40,  GETDATE()) AS DATE), 110.00, 56.20, 44100, 2),
    (2, CAST(DATEADD(DAY, -8,   GETDATE()) AS DATE), 95.00,  56.30, 44900, 2),
    (3, CAST(DATEADD(DAY, -140, GETDATE()) AS DATE), 520.00, 64.80, 150200, 1),
    (3, CAST(DATEADD(DAY, -70,  GETDATE()) AS DATE), 480.00, 67.20, 152100, 1),
    (4, CAST(DATEADD(DAY, -130, GETDATE()) AS DATE), 610.00, 65.10, 207900, 3),
    (4, CAST(DATEADD(DAY, -60,  GETDATE()) AS DATE), 590.00, 67.80, 210400, 3),
    (4, CAST(DATEADD(DAY, -10,  GETDATE()) AS DATE), 560.00, 68.40, 212000, 3),
    (6, CAST(DATEADD(DAY, -100, GETDATE()) AS DATE), 90.00, 63.50, 60100, 4),
    (6, CAST(DATEADD(DAY, -20,  GETDATE()) AS DATE), 85.00, 68.20, 61900, 4),
    (7, CAST(DATEADD(DAY, -90,  GETDATE()) AS DATE), 190.00, 64.00, 86100, 5),
    (7, CAST(DATEADD(DAY, -35,  GETDATE()) AS DATE), 210.00, 67.90, 87900, 5),
    (8, CAST(DATEADD(DAY, -80,  GETDATE()) AS DATE), 70.00, 64.50, 24900, 4),
    (10, CAST(DATEADD(DAY, -50, GETDATE()) AS DATE), 75.00, 55.90, 13900, 2),
    (10, CAST(DATEADD(DAY, -12, GETDATE()) AS DATE), 65.00, 56.40, 15300, 2);

-- Штрафы и ДТП
INSERT INTO dbo.Fines (VehicleId, DriverId, [Date], Amount, Description, IsPaid, PaidDate, IncidentType) VALUES
    (1, 1, CAST(DATEADD(DAY, -45, GETDATE()) AS DATE), 5000.00,  N'Превышение скорости на 22 км/ч', 1, CAST(DATEADD(DAY, -30, GETDATE()) AS DATE), 'Fine'),
    (4, 3, CAST(DATEADD(DAY, -20, GETDATE()) AS DATE), 15000.00, N'Нарушение режима труда и отдыха', 0, NULL, 'Fine'),
    (6, 4, CAST(DATEADD(DAY, -12, GETDATE()) AS DATE), 25000.00, N'Повреждение заднего бампера на парковке', 0, NULL, 'Accident'),
    (2, 2, CAST(DATEADD(DAY, -70, GETDATE()) AS DATE), 3000.00,  N'Стоянка в неположенном месте', 1, CAST(DATEADD(DAY, -65, GETDATE()) AS DATE), 'Fine'),
    (7, 5, CAST(DATEADD(DAY, -8,  GETDATE()) AS DATE), 8000.00,  N'Перегруз по массе', 0, NULL, 'Fine'),
    (8, 4, CAST(DATEADD(DAY, -130, GETDATE()) AS DATE), 12000.00, N'Незначительное ДТП, повреждено крыло', 1, CAST(DATEADD(DAY, -100, GETDATE()) AS DATE), 'Accident');

INSERT INTO dbo.AuditLog (UserId, [Action], TableName, RecordId, OldValue, NewValue) VALUES
    (1, N'Login', N'Users', N'1', NULL, N'Вход в систему'),
    (2, N'Update', N'Vehicles', N'3', N'Статус: На линии', N'Статус: В ремонте'),
    (2, N'Insert', N'MileageLogs', N'20', NULL, N'Пробег 300 км, ТС Т741НЕ77'),
    (1, N'Update', N'Users', N'5', N'IsActive: 0', N'IsActive: 1');

-- Формируем уведомления по ТО
EXEC dbo.sp_RefreshMaintenanceAlerts @UpcomingKm = 500, @OverdueKm = 1000;

/* ============================================================================
   11. КОНТРОЛЬНЫЙ ВЫВОД
   ============================================================================ */

PRINT '=== FleetManagerDB создана ===';
SELECT (SELECT COUNT(*) FROM dbo.Users)               AS Users,
       (SELECT COUNT(*) FROM dbo.Drivers)             AS Drivers,
       (SELECT COUNT(*) FROM dbo.Vehicles)            AS Vehicles,
       (SELECT COUNT(*) FROM dbo.MileageLogs)         AS MileageLogs,
       (SELECT COUNT(*) FROM dbo.FuelRecords)         AS FuelRecords,
       (SELECT COUNT(*) FROM dbo.MaintenanceRecords)  AS MaintenanceRecords,
       (SELECT COUNT(*) FROM dbo.MaintenanceAlerts)   AS ActiveAlerts;

SELECT VehicleId, PlateNumber, TypeName, CurrentMileage, NextDueMileage, RemainingKm, Severity
FROM (
    SELECT d.*,
           CASE WHEN d.RemainingKm < -1000 THEN N'Overdue'
                WHEN d.RemainingKm <= 0     THEN N'Due'
                WHEN d.RemainingKm <= 500   THEN N'Upcoming'
                ELSE N'OK' END AS Severity
    FROM dbo.vw_MaintenanceDue d
) x
WHERE Severity <> N'OK'
ORDER BY CASE Severity WHEN N'Overdue' THEN 1 WHEN N'Due' THEN 2 ELSE 3 END, RemainingKm;
GO
