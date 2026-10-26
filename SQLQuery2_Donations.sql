-- Run this once in the gift_of_thegivers database (after SQLQuery1.sql).
-- It adds the Donations table that the Azure Function now writes to.
USE gift_of_thegivers;
GO

IF OBJECT_ID('dbo.Donations', 'U') IS NULL
BEGIN
    CREATE TABLE Donations (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Reference NVARCHAR(30) NOT NULL UNIQUE,
        DonorName NVARCHAR(100) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        Currency NVARCHAR(3) NOT NULL,
        DonationType NVARCHAR(20) NOT NULL,
        CreatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO
