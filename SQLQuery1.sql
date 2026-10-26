Create database gift_of_thegivers;
use gift_of_thegivers;


Drop table Users;
CREATE TABLE users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(150) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    Role NVARCHAR(20) NOT NULL DEFAULT 'Donor',
    CreatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);