-- Run this ONCE in Azure Portal > SQL database ST10379781 > Query editor,
-- signed in with "Microsoft Entra authentication" (ST10379781@rcconnect.edu.za).
-- First switch on Identity > System assigned for both apps, or these lines fail.
-- It lets the website and the function log in to the database without passwords.

-- 1. Website (App Service): read and write data
CREATE USER [giftofthegivers20260916211758] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [giftofthegivers20260916211758];
ALTER ROLE db_datawriter ADD MEMBER [giftofthegivers20260916211758];

-- 2. Azure Function: read and write data, and create/alter tables (runs the schema updates)
CREATE USER [giversfunction20260925190342] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [giversfunction20260925190342];
ALTER ROLE db_datawriter ADD MEMBER [giversfunction20260925190342];
ALTER ROLE db_ddladmin  ADD MEMBER [giversfunction20260925190342];
