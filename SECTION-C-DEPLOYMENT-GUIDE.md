# Section C – Continuous Deployment Guide (Gift of the Givers)

`azure-pipelines.yaml` has two stages:

1. **Build and unit test** (Section B). Runs on every commit, on any branch.
2. **Deploy to Azure** (Section C). Runs on `master` only, after the tests pass. Its steps run in this order:
   1. **C.2:** zip-deploys `givers_function` to the Function App.
   2. **C.3:** calls the `ApplySchema` function, which runs `Database/schema.sql` on Azure SQL. Each schema version is applied once and logged in `SchemaVersions`.
   3. **C.1:** zip-deploys the website to the App Service and checks that the live site returns HTTP 200.

## Why it's set up this way

The college Azure account blocks two things:
- **SQL passwords** (Entra-only authentication).
- **Creating Azure DevOps service connections.**

So the project works around both:
- **Deployment** uses each app's publishing credentials (zip deploy), stored as secret pipeline variables.
- **Database login** uses each app's **managed identity**, so no database password is stored anywhere. The connection string uses `Authentication=Active Directory Managed Identity`.
- **Schema updates (C.3)** run through the Function App's identity. The pipeline triggers them and shows the result in its log.

## Azure resources

| Purpose | Name |
|---|---|
| Website | App Service `giftofthegivers20260916211758` (Windows) |
| Function | Function App `giversfunction20260925190342` (Windows) |
| Database | Azure SQL `ST10379781` on server `st10379781` (Basic tier) |

## One-time setup

1. **Managed identity:** on both apps, go to **Settings > Identity > System assigned**, set it to **On**, and **Save**.
2. **Database access:**
   - Open **SQL database ST10379781 > Query editor** and sign in with Microsoft Entra.
   - Run `Database/grant-access.sql`.
3. **App settings:** go to **Settings > Environment variables**.
   - On the **Function App**, add `GOTG_SQL`.
   - On the **Website**, add both `GOTG_SQL` and `GOTG_FUNCTION_URL`. Its value is `https://<function default domain>/api/Function1?code=<default host key>`.
   - `GOTG_SQL` is this, for both apps:

   ```
   Server=tcp:st10379781.database.windows.net,1433;Initial Catalog=ST10379781;Authentication=Active Directory Managed Identity;Encrypt=True;
   ```

4. **Pipeline secret variables** (Azure DevOps): `webDeployUser`, `webDeployPassword`, `funcDeployUser`, `funcDeployPassword` and `functionKey`.
   - Take the usernames and passwords from each publish profile (`userName` and `userPWD`).
   - Take the key from **Function App > App keys > default**.
5. **YAML:** set `functionHost` to the Function App's **Default domain**.

## Screenshots

### C.1 – App Service

- The YAML `Deploy` stage.
- The **C.1** step log ending in "Live site is up: https://…".
- The live site in a browser with the address bar visible: the Home page, then a donation and the certificate page.

### C.2 – Functions

- The **C.2** step log.
- **Function App > Functions**, showing `Function1` and `ApplySchema`.
- **Function1 > Code + Test > Test/Run** returning 200 with a certificate. Use these query parameters:
  - `amount` = 100
  - `currency` = ZAR
  - `type` = OneTime
  - `donorName` = Azure Test
- The **Monitor > Invocations** tab.

### C.3 – SQL

- The **C.3** step log, showing the PRINT lines and "Applied schema versions: v1 … v4".
- **Query editor** results of:
  - `SELECT * FROM SchemaVersions;`
  - `SELECT TOP 5 * FROM Donations ORDER BY CreatedOn DESC;`
- **Optional:** run the pipeline again. The log now says "Version N already applied" for each version, which proves every update is applied exactly once.
