/* =========================================================================
   RaceDay - Section C: Database Creation Script (Microsoft SQL Server)
   Matches the ERD in Section A exactly: 7 entities, PKs, FKs, and the
   constraints identified in the ERD audit (role integrity, composite
   unique bib numbers, audit columns).
   ========================================================================= */

IF DB_ID('RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END
GO

USE RaceDayDB;
GO

/* =========================================================================
   1. DROP EXISTING OBJECTS (safe re-run during development)
   ========================================================================= */
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Payments', 'U') IS NOT NULL DROP TABLE dbo.Payments;
IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Venues', 'U') IS NOT NULL DROP TABLE dbo.Venues;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

/* =========================================================================
   2. TABLE CREATION
   ========================================================================= */

-- USERS ---------------------------------------------------------------------
CREATE TABLE dbo.Users (
    UserId          INT IDENTITY(1,1)   NOT NULL,
    FullName        NVARCHAR(150)       NOT NULL,
    Email           NVARCHAR(256)       NOT NULL,
    PasswordHash    NVARCHAR(256)       NOT NULL,   -- never store plain text; see Research & Citations, Section 3
    Role            NVARCHAR(20)        NOT NULL,
    Phone           NVARCHAR(20)        NULL,
    DateOfBirth     DATE                NULL,
    IsActive        BIT                 NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedAt       DATETIME2           NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt       DATETIME2           NOT NULL CONSTRAINT DF_Users_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser', 'Participant'))
);
GO

-- VENUES ----------------------------------------------------------------------
CREATE TABLE dbo.Venues (
    VenueId         INT IDENTITY(1,1)   NOT NULL,
    Name            NVARCHAR(150)       NOT NULL,
    Address         NVARCHAR(250)       NULL,
    City            NVARCHAR(100)       NOT NULL,
    Province        NVARCHAR(100)       NOT NULL,
    Latitude        DECIMAL(9,6)        NULL,
    Longitude       DECIMAL(9,6)        NULL,
    RouteMapUrl     NVARCHAR(500)       NULL,        -- Azure Blob Storage URL, populated in Part 3
    CONSTRAINT PK_Venues PRIMARY KEY (VenueId),
    CONSTRAINT CK_Venues_Latitude CHECK (Latitude IS NULL OR Latitude BETWEEN -90 AND 90),
    CONSTRAINT CK_Venues_Longitude CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180),
    CONSTRAINT CK_Venues_CoordinatePair CHECK (
        (Latitude IS NULL AND Longitude IS NULL)
        OR (Latitude IS NOT NULL AND Longitude IS NOT NULL)
    )
);
GO

-- EVENTS ----------------------------------------------------------------------
CREATE TABLE dbo.Events (
    EventId                 INT IDENTITY(1,1)  NOT NULL,
    OrganiserId             INT                 NOT NULL,
    VenueId                 INT                 NOT NULL,
    Name                    NVARCHAR(150)       NOT NULL,
    Description             NVARCHAR(1000)      NULL,
    EventDate               DATE                NOT NULL,
    RegistrationDeadline    DATE                NOT NULL,
    Status                  NVARCHAR(20)        NOT NULL CONSTRAINT DF_Events_Status DEFAULT ('Draft'),
    IsActive                BIT                 NOT NULL CONSTRAINT DF_Events_IsActive DEFAULT (1),
    CreatedAt               DATETIME2           NOT NULL CONSTRAINT DF_Events_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt               DATETIME2           NOT NULL CONSTRAINT DF_Events_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Events PRIMARY KEY (EventId),
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Events_Venue     FOREIGN KEY (VenueId)     REFERENCES dbo.Venues (VenueId),
    CONSTRAINT CK_Events_Status CHECK (Status IN ('Draft', 'Published', 'Cancelled', 'Completed')),
    CONSTRAINT CK_Events_DeadlineBeforeEvent CHECK (RegistrationDeadline <= EventDate)
);
GO

-- CATEGORIES --------------------------------------------------------------------
CREATE TABLE dbo.Categories (
    CategoryId          INT IDENTITY(1,1)  NOT NULL,
    EventId             INT                 NOT NULL,
    Name                NVARCHAR(100)       NOT NULL,
    Distance            DECIMAL(6,2)        NOT NULL,   -- km
    MaxParticipants     INT                 NOT NULL,
    Price               DECIMAL(8,2)        NOT NULL,
    IsActive            BIT                 NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1),
    CreatedAt           DATETIME2           NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2           NOT NULL CONSTRAINT DF_Categories_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Categories PRIMARY KEY (CategoryId),
    CONSTRAINT FK_Categories_Event FOREIGN KEY (EventId) REFERENCES dbo.Events (EventId),
    CONSTRAINT UQ_Categories_Event_Name UNIQUE (EventId, Name),
    CONSTRAINT CK_Categories_Distance CHECK (Distance > 0),
    CONSTRAINT CK_Categories_MaxParticipants CHECK (MaxParticipants > 0),
    CONSTRAINT CK_Categories_Price CHECK (Price >= 0)
);
GO

-- ENROLMENTS ----------------------------------------------------------------------
CREATE TABLE dbo.Enrolments (
    EnrolmentId         INT IDENTITY(1,1)  NOT NULL,
    ParticipantId        INT                 NOT NULL,
    CategoryId           INT                 NOT NULL,
    EnrolmentDate        DATETIME2           NOT NULL CONSTRAINT DF_Enrolments_Date DEFAULT (SYSUTCDATETIME()),
    Status                NVARCHAR(20)        NOT NULL CONSTRAINT DF_Enrolments_Status DEFAULT ('Confirmed'),
    BibNumber             NVARCHAR(10)        NOT NULL,
    CONSTRAINT PK_Enrolments PRIMARY KEY (EnrolmentId),
    CONSTRAINT FK_Enrolments_Participant FOREIGN KEY (ParticipantId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Enrolments_Category    FOREIGN KEY (CategoryId)    REFERENCES dbo.Categories (CategoryId),
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantId, CategoryId),  -- can't enter the same category twice
    CONSTRAINT UQ_Enrolments_Category_Bib         UNIQUE (CategoryId, BibNumber),      -- bib numbers unique per category, not globally
    CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Pending', 'Confirmed', 'Cancelled'))
);
GO

-- PAYMENTS ----------------------------------------------------------------------
CREATE TABLE dbo.Payments (
    PaymentId       INT IDENTITY(1,1)  NOT NULL,
    EnrolmentId     INT                 NOT NULL,
    Amount          DECIMAL(8,2)        NOT NULL,
    PaymentDate     DATETIME2           NOT NULL CONSTRAINT DF_Payments_Date DEFAULT (SYSUTCDATETIME()),
    Method          NVARCHAR(30)        NOT NULL,
    Status          NVARCHAR(20)        NOT NULL CONSTRAINT DF_Payments_Status DEFAULT ('Pending'),
    CONSTRAINT PK_Payments PRIMARY KEY (PaymentId),
    CONSTRAINT FK_Payments_Enrolment FOREIGN KEY (EnrolmentId) REFERENCES dbo.Enrolments (EnrolmentId),
    CONSTRAINT UQ_Payments_Enrolment UNIQUE (EnrolmentId),   -- 1:0..1 - at most one payment per enrolment
    CONSTRAINT CK_Payments_Amount CHECK (Amount >= 0),
    CONSTRAINT CK_Payments_Method CHECK (Method IN ('Card', 'EFT', 'Cash')),
    CONSTRAINT CK_Payments_Status CHECK (Status IN ('Pending', 'Paid', 'Refunded', 'Failed'))
);
GO

-- RESULTS ----------------------------------------------------------------------
CREATE TABLE dbo.Results (
    ResultId        INT IDENTITY(1,1)  NOT NULL,
    EnrolmentId     INT                 NOT NULL,
    FinishTime      TIME(0)             NULL,
    Position        INT                 NULL,
    Status          NVARCHAR(20)        NOT NULL CONSTRAINT DF_Results_Status DEFAULT ('Finished'),
    CapturedBy      INT                 NOT NULL,
    CapturedAt      DATETIME2           NOT NULL CONSTRAINT DF_Results_CapturedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Results PRIMARY KEY (ResultId),
    CONSTRAINT FK_Results_Enrolment FOREIGN KEY (EnrolmentId) REFERENCES dbo.Enrolments (EnrolmentId),
    CONSTRAINT FK_Results_CapturedBy FOREIGN KEY (CapturedBy) REFERENCES dbo.Users (UserId),
    CONSTRAINT UQ_Results_Enrolment UNIQUE (EnrolmentId),   -- 1:0..1 - at most one result per enrolment
    CONSTRAINT CK_Results_Status CHECK (Status IN ('Finished', 'DNF', 'DSQ')),
    CONSTRAINT CK_Results_Position CHECK (Position IS NULL OR Position > 0),
    CONSTRAINT CK_Results_StatusData CHECK (
        (Status = 'Finished' AND FinishTime IS NOT NULL AND Position IS NOT NULL)
        OR (Status IN ('DNF', 'DSQ') AND FinishTime IS NULL AND Position IS NULL)
    )
);
GO

/* =========================================================================
   3. ROLE-INTEGRITY TRIGGERS
   SQL Server CHECK constraints cannot reference another table, so the
   role rules flagged in the ERD audit are enforced here with triggers:
     - Events.OrganiserId must reference a user with Role = 'Organiser'
     - Enrolments.ParticipantId must reference a user with Role = 'Participant'
     - Results.CapturedBy must reference a user with Role = 'Organiser'
   ========================================================================= */

CREATE OR ALTER TRIGGER dbo.trg_Events_OrganiserRole
ON dbo.Events
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.Users u ON u.UserId = i.OrganiserId
        WHERE u.Role <> 'Organiser'
    )
    BEGIN
        RAISERROR ('OrganiserId must reference a user with Role = ''Organiser''.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Enrolments_ParticipantRole
ON dbo.Enrolments
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.Users u ON u.UserId = i.ParticipantId
        WHERE u.Role <> 'Participant'
    )
    BEGIN
        RAISERROR ('ParticipantId must reference a user with Role = ''Participant''.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Results_CapturedByRole
ON dbo.Results
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.Users u ON u.UserId = i.CapturedBy
        WHERE u.Role <> 'Organiser'
    )
    BEGIN
        RAISERROR ('CapturedBy must reference a user with Role = ''Organiser''.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- Results may only be captured by the organiser who owns the event.
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.Enrolments en ON en.EnrolmentId = i.EnrolmentId
        JOIN dbo.Categories c ON c.CategoryId = en.CategoryId
        JOIN dbo.Events e ON e.EventId = c.EventId
        WHERE e.OrganiserId <> i.CapturedBy
    )
    BEGIN
        RAISERROR ('CapturedBy must be the organiser who owns the event.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END
GO

/* Prevent changing a user's role when existing records depend on that role. */
CREATE OR ALTER TRIGGER dbo.trg_Users_ProtectReferencedRole
ON dbo.Users
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(Role) RETURN;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        WHERE i.Role <> 'Organiser'
          AND (
              EXISTS (SELECT 1 FROM dbo.Events e WHERE e.OrganiserId = i.UserId)
              OR EXISTS (SELECT 1 FROM dbo.Results r WHERE r.CapturedBy = i.UserId)
          )
    )
    OR EXISTS (
        SELECT 1
        FROM inserted i
        WHERE i.Role <> 'Participant'
          AND EXISTS (SELECT 1 FROM dbo.Enrolments en WHERE en.ParticipantId = i.UserId)
    )
    BEGIN
        RAISERROR ('The user role cannot be changed while role-dependent records reference the user.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END
GO

/*
   Enforce one category per participant per event and category capacity.
   HOLDLOCK makes the capacity check serializable for concurrent enrolments.
   Cancelled enrolments do not consume capacity.
*/
CREATE OR ALTER TRIGGER dbo.trg_Enrolments_EventAndCapacity
ON dbo.Enrolments
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.Categories selectedCategory ON selectedCategory.CategoryId = i.CategoryId
        JOIN dbo.Enrolments otherEnrolment ON otherEnrolment.ParticipantId = i.ParticipantId
                                              AND otherEnrolment.EnrolmentId <> i.EnrolmentId
                                              AND otherEnrolment.Status <> 'Cancelled'
        JOIN dbo.Categories otherCategory ON otherCategory.CategoryId = otherEnrolment.CategoryId
        WHERE i.Status <> 'Cancelled'
          AND otherCategory.EventId = selectedCategory.EventId
    )
    BEGIN
        RAISERROR ('A participant may enrol in only one category per event.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.Categories c WITH (UPDLOCK, HOLDLOCK) ON c.CategoryId = i.CategoryId
        CROSS APPLY (
            SELECT COUNT_BIG(*) AS ActiveEnrolments
            FROM dbo.Enrolments en WITH (UPDLOCK, HOLDLOCK)
            WHERE en.CategoryId = i.CategoryId
              AND en.Status <> 'Cancelled'
        ) totals
        WHERE totals.ActiveEnrolments > c.MaxParticipants
    )
    BEGIN
        RAISERROR ('The selected category has reached its maximum participant capacity.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END
GO

/* =========================================================================
   4. INDEXES ON FOREIGN KEYS (SQL Server does not auto-index FK columns)
   ========================================================================= */
CREATE INDEX IX_Events_OrganiserId      ON dbo.Events (OrganiserId);
CREATE INDEX IX_Events_VenueId          ON dbo.Events (VenueId);
CREATE INDEX IX_Categories_EventId      ON dbo.Categories (EventId);
CREATE INDEX IX_Enrolments_ParticipantId ON dbo.Enrolments (ParticipantId);
CREATE INDEX IX_Enrolments_CategoryId   ON dbo.Enrolments (CategoryId);
CREATE INDEX IX_Results_CapturedBy      ON dbo.Results (CapturedBy);
GO

/* =========================================================================
   5. SAMPLE DATA
   Minimum per assignment: 2 Organisers, 2 Participants, 3 Events with
   categories, sample enrolments. Extended slightly for realistic coverage.
   ========================================================================= */

-- 5.1 Users: 2 Organisers, 4 Participants ------------------------------------
INSERT INTO dbo.Users (FullName, Email, PasswordHash, Role, Phone, DateOfBirth)
VALUES
    ('Thandiwe Mokoena', 'thandiwe.organiser@raceday.co.za', 'HASH_PLACEHOLDER_1', 'Organiser', '0821234567', '1985-03-14'),
    ('Johan van der Merwe', 'johan.organiser@raceday.co.za',  'HASH_PLACEHOLDER_2', 'Organiser', '0827654321', '1979-11-02'),
    ('Naledi Dlamini',      'naledi.participant@raceday.co.za', 'HASH_PLACEHOLDER_3', 'Participant', '0831112222', '1996-06-21'),
    ('Sipho Ndlovu',        'sipho.participant@raceday.co.za',  'HASH_PLACEHOLDER_4', 'Participant', '0833334444', '1990-01-09'),
    ('Emma Botha',          'emma.participant@raceday.co.za',   'HASH_PLACEHOLDER_5', 'Participant', '0835556666', '2000-09-30'),
    ('Lwazi Zulu',          'lwazi.participant@raceday.co.za',  'HASH_PLACEHOLDER_6', 'Participant', '0837778888', '1988-12-17');
GO

-- 5.2 Venues: 3 venues --------------------------------------------------------
INSERT INTO dbo.Venues (Name, Address, City, Province, Latitude, Longitude, RouteMapUrl)
VALUES
    ('Zwartkops Raceway',       '1 Ronnie Fourie Rd',  'Centurion',  'Gauteng',       -25.850000, 28.153000, NULL),
    ('Sea Point Promenade',     'Beach Rd',             'Cape Town',  'Western Cape',  -33.916000, 18.383000, NULL),
    ('Kings Park Athletics Stadium', '18 Masabalala Yengwa Ave', 'Durban', 'KwaZulu-Natal', -29.828000, 31.021000, NULL);
GO

-- 5.3 Events: 3 events, one per Organiser/venue mix ---------------------------
INSERT INTO dbo.Events (OrganiserId, VenueId, Name, Description, EventDate, RegistrationDeadline, Status)
VALUES
    (1, 1, 'Centurion Spring Classic',   'Annual road race through Centurion.',        '2026-10-18', '2026-10-10', 'Published'),
    (1, 2, 'Sea Point Sunrise Run',      'Scenic coastal run along the promenade.',    '2026-11-08', '2026-11-01', 'Published'),
    (2, 3, 'Durban Harbour Half Marathon','Half marathon starting at Kings Park.',     '2026-09-27', '2026-09-20', 'Published');
GO

-- 5.4 Categories: 2-3 per event -----------------------------------------------
INSERT INTO dbo.Categories (EventId, Name, Distance, MaxParticipants, Price)
VALUES
    (1, '5km Fun Run',   5.0,  200, 100.00),
    (1, '10km',          10.0, 150, 150.00),
    (2, '5km',           5.0,  300, 120.00),
    (2, '10km',          10.0, 200, 170.00),
    (3, '10km',          10.0, 250, 160.00),
    (3, '21.1km Half Marathon', 21.1, 400, 250.00);
GO

-- 5.5 Enrolments: sample participants entering different categories ----------
INSERT INTO dbo.Enrolments (ParticipantId, CategoryId, Status, BibNumber)
VALUES
    (3, 1, 'Confirmed', '1001'),  -- Naledi -> Centurion 5km
    (4, 2, 'Confirmed', '2001'),  -- Sipho -> Centurion 10km
    (5, 3, 'Confirmed', '3001'),  -- Emma  -> Sea Point 5km
    (6, 6, 'Confirmed', '4001'),  -- Lwazi -> Durban Half Marathon
    (3, 6, 'Confirmed', '4002');  -- Naledi also enters the Durban Half Marathon (different category, allowed)
GO

-- 5.6 Payments: entry fees paid for most enrolments ---------------------------
INSERT INTO dbo.Payments (EnrolmentId, Amount, Method, Status)
VALUES
    (1, 100.00, 'Card', 'Paid'),
    (2, 150.00, 'Card', 'Paid'),
    (3, 120.00, 'EFT',  'Paid'),
    (4, 250.00, 'Card', 'Pending');
GO

-- 5.7 Results: captured by the owning Organiser for a completed event --------
-- (Enrolment 5 left without a result to demonstrate the 1:0..1 cardinality)
INSERT INTO dbo.Results (EnrolmentId, FinishTime, Position, Status, CapturedBy)
VALUES
    (1, '00:28:45', 12, 'Finished', 1),   -- captured by Organiser Thandiwe (owns event 1)
    (2, '00:52:10', 8,  'Finished', 1);
GO

/* =========================================================================
   6. VERIFICATION QUERIES (optional - run to sanity-check the seed data)
   ========================================================================= */
SELECT * FROM dbo.Users;
SELECT * FROM dbo.Venues;
SELECT * FROM dbo.Events;
SELECT * FROM dbo.Categories;
SELECT * FROM dbo.Enrolments;
SELECT * FROM dbo.Payments;
SELECT * FROM dbo.Results;


/* =========================================================================
   7. REFERENCES

   [1] OWASP Foundation, "Password Storage Cheat Sheet," OWASP Cheat Sheet
       Series. Available:
       https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html
       Accessed: 3 September 2026.

   [2] Microsoft, "Use the inserted and deleted tables," Microsoft Learn,
       section "Example: Use the inserted table in a trigger to enforce
       business rules." Available:
       https://learn.microsoft.com/en-us/sql/relational-databases/triggers/use-the-inserted-and-deleted-tables
       Accessed: 3 September 2026.

   [3] Microsoft, "Use the inserted and deleted tables," Microsoft Learn,
       sections "Understanding the inserted and deleted tables" and guidance
       for multirow data modifications. Available:
       https://learn.microsoft.com/en-us/sql/relational-databases/triggers/use-the-inserted-and-deleted-tables
       Accessed: 3 September 2026.

   [4] Microsoft, "Table Hints (Transact-SQL)," Microsoft Learn, entries for
       HOLDLOCK, SERIALIZABLE, and UPDLOCK. Available:
       https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table
       Accessed: 3 September 2026.

   [5] Microsoft, "Primary and foreign key constraints," Microsoft Learn,
       section "Indexes on foreign key constraints." Available:
       https://learn.microsoft.com/en-us/sql/relational-databases/tables/primary-and-foreign-key-constraints
       Accessed: 3 September 2026.
   ========================================================================= */