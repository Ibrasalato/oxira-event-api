IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE TABLE [AlertsSent] (
        [Id] bigint NOT NULL IDENTITY,
        [SubscriberId] uniqueidentifier NOT NULL,
        [EventKey] nvarchar(220) NOT NULL,
        [Stage] int NOT NULL,
        [SentAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AlertsSent] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE TABLE [AlertSubscribers] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(160) NOT NULL,
        [Sectors] nvarchar(400) NOT NULL,
        [Lang] nvarchar(5) NOT NULL,
        [Token] nvarchar(32) NOT NULL,
        [SourceName] nvarchar(200) NULL,
        [SourceMedium] nvarchar(200) NULL,
        [SourceCampaign] nvarchar(200) NULL,
        [SourceReferrer] nvarchar(200) NULL,
        [SourceLanding] nvarchar(200) NULL,
        [SourceJson] nvarchar(4000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UnsubscribedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AlertSubscribers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE TABLE [Bookings] (
        [Id] int NOT NULL IDENTITY,
        [ContactName] nvarchar(120) NOT NULL,
        [Phone] nvarchar(40) NOT NULL,
        [Email] nvarchar(160) NOT NULL,
        [Company] nvarchar(160) NOT NULL,
        [Topic] nvarchar(max) NOT NULL,
        [Channel] nvarchar(20) NOT NULL,
        [StartsAt] datetimeoffset NOT NULL,
        [Lang] nvarchar(5) NOT NULL,
        [Page] nvarchar(300) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [SourceName] nvarchar(200) NULL,
        [SourceMedium] nvarchar(200) NULL,
        [SourceCampaign] nvarchar(200) NULL,
        [SourceReferrer] nvarchar(200) NULL,
        [SourceLanding] nvarchar(200) NULL,
        [SourceJson] nvarchar(4000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE TABLE [ChatMessages] (
        [Id] bigint NOT NULL IDENTITY,
        [SessionId] nvarchar(64) NOT NULL,
        [Role] nvarchar(12) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Lang] nvarchar(5) NOT NULL,
        [Page] nvarchar(300) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE TABLE [Leads] (
        [Id] int NOT NULL IDENTITY,
        [ContactName] nvarchar(120) NOT NULL,
        [Phone] nvarchar(40) NOT NULL,
        [Email] nvarchar(160) NOT NULL,
        [Company] nvarchar(160) NOT NULL,
        [Service] nvarchar(160) NOT NULL,
        [EventName] nvarchar(160) NOT NULL,
        [EventDate] nvarchar(80) NOT NULL,
        [City] nvarchar(120) NOT NULL,
        [StandSize] nvarchar(40) NOT NULL,
        [Needs] nvarchar(max) NOT NULL,
        [PreferredContact] nvarchar(200) NOT NULL,
        [Channel] nvarchar(20) NOT NULL,
        [Lang] nvarchar(5) NOT NULL,
        [Page] nvarchar(300) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [DesignJson] nvarchar(4000) NULL,
        [SourceName] nvarchar(200) NULL,
        [SourceMedium] nvarchar(200) NULL,
        [SourceCampaign] nvarchar(200) NULL,
        [SourceReferrer] nvarchar(200) NULL,
        [SourceLanding] nvarchar(200) NULL,
        [SourceJson] nvarchar(4000) NULL,
        [StaffNote] nvarchar(4000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Leads] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE TABLE [RenderLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [SessionId] nvarchar(64) NOT NULL,
        [Ip] nvarchar(64) NOT NULL,
        [Lang] nvarchar(5) NOT NULL,
        [ConfigJson] nvarchar(2000) NOT NULL,
        [Ok] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_RenderLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE TABLE [UsageCounters] (
        [Key] nvarchar(120) NOT NULL,
        [Day] date NOT NULL,
        [Count] int NOT NULL,
        CONSTRAINT [PK_UsageCounters] PRIMARY KEY ([Key], [Day])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE TABLE [LeadFiles] (
        [Id] int NOT NULL IDENTITY,
        [LeadId] int NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Mime] nvarchar(60) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [Size] bigint NOT NULL,
        [Content] varbinary(max) NOT NULL,
        CONSTRAINT [PK_LeadFiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LeadFiles_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE INDEX [IX_AlertsSent_EventKey] ON [AlertsSent] ([EventKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AlertsSent_SubscriberId_EventKey_Stage] ON [AlertsSent] ([SubscriberId], [EventKey], [Stage]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AlertSubscribers_Email] ON [AlertSubscribers] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AlertSubscribers_Token] ON [AlertSubscribers] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Bookings_StartsAt] ON [Bookings] ([StartsAt]) WHERE [Status] <> ''cancelled''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_SessionId_Id] ON [ChatMessages] ([SessionId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE INDEX [IX_LeadFiles_LeadId] ON [LeadFiles] ([LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE INDEX [IX_Leads_CreatedAt] ON [Leads] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE INDEX [IX_Leads_Status_CreatedAt] ON [Leads] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    CREATE INDEX [IX_RenderLogs_CreatedAt] ON [RenderLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013172456_Initial'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013172456_Initial', N'10.0.12');
END;

COMMIT;
GO

