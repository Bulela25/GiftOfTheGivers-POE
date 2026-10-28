-- =====================================================================
-- Gift of the Givers - Azure SQL schema (Section C.3)
-- Deployed automatically by azure-pipelines.yaml on every run.
--
-- Safe to run again and again: each change is a numbered version and is
-- only applied once. Applied versions are logged in dbo.SchemaVersions,
-- which is the evidence that updates were applied.
--
-- To add a new update later: copy a block, give it the next number,
-- commit, and the pipeline applies it.
-- =====================================================================

-- Version log table
IF OBJECT_ID('dbo.SchemaVersions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersions (
        Version     INT            NOT NULL PRIMARY KEY,
        Description NVARCHAR(200)  NOT NULL,
        AppliedOn   DATETIME2      NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT 'Created SchemaVersions table';
END
GO

-- ---------- Version 1: Users table ----------
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Version = 1)
BEGIN
    IF OBJECT_ID('dbo.Users', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Users (
            Id           INT IDENTITY(1,1) PRIMARY KEY,
            FullName     NVARCHAR(100) NOT NULL,
            Email        NVARCHAR(150) NOT NULL UNIQUE,
            PasswordHash NVARCHAR(255) NOT NULL,
            Role         NVARCHAR(20)  NOT NULL DEFAULT 'Donor',
            CreatedOn    DATETIME2     NOT NULL DEFAULT GETUTCDATE()
        );
    END
    INSERT INTO dbo.SchemaVersions (Version, Description) VALUES (1, 'Create Users table');
    PRINT 'Applied version 1: Create Users table';
END
ELSE
    PRINT 'Version 1 already applied';
GO

-- ---------- Version 2: Donations table ----------
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Version = 2)
BEGIN
    IF OBJECT_ID('dbo.Donations', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Donations (
            Id           INT IDENTITY(1,1) PRIMARY KEY,
            Reference    NVARCHAR(30)  NOT NULL UNIQUE,
            DonorName    NVARCHAR(100) NOT NULL,
            Amount       DECIMAL(18,2) NOT NULL,
            Currency     NVARCHAR(3)   NOT NULL,
            DonationType NVARCHAR(20)  NOT NULL,
            CreatedOn    DATETIME2     NOT NULL DEFAULT GETUTCDATE()
        );
    END
    INSERT INTO dbo.SchemaVersions (Version, Description) VALUES (2, 'Create Donations table');
    PRINT 'Applied version 2: Create Donations table';
END
ELSE
    PRINT 'Version 2 already applied';
GO

-- ---------- Version 3: Index for donation reports by date ----------
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Version = 3)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Donations_CreatedOn')
        CREATE INDEX IX_Donations_CreatedOn ON dbo.Donations (CreatedOn);
    INSERT INTO dbo.SchemaVersions (Version, Description) VALUES (3, 'Add IX_Donations_CreatedOn index');
    PRINT 'Applied version 3: Add IX_Donations_CreatedOn index';
END
ELSE
    PRINT 'Version 3 already applied';
GO

-- ---------- Version 4: Donor email on donations (optional receipts) ----------
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Version = 4)
BEGIN
    IF COL_LENGTH('dbo.Donations', 'DonorEmail') IS NULL
        ALTER TABLE dbo.Donations ADD DonorEmail NVARCHAR(150) NULL;
    INSERT INTO dbo.SchemaVersions (Version, Description) VALUES (4, 'Add Donations.DonorEmail column');
    PRINT 'Applied version 4: Add Donations.DonorEmail column';
END
ELSE
    PRINT 'Version 4 already applied';
GO

-- Show what has been applied (appears in the pipeline log)
SELECT Version, Description, AppliedOn FROM dbo.SchemaVersions ORDER BY Version;
GO
