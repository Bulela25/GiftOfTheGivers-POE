# Section A – Testing Guide (Gift of the Givers)

Everything for Section A is in the new **GiftOfTheGivers.Tests** project, already added to `gift_of_thegivers.sln`.

| Brief | File | What it tests |
|---|---|---|
| A.1 Unit tests | `Unit/DonationLogicTests.cs` | Donation validation, total calculation, certificate formatting (12 tests, 21 cases) |
| A.2 Integration tests | `Integration/DonationFlowTests.cs`<br>`Integration/FunctionDatabaseTests.cs` | Donation form > Azure Function > database, plus function > database and registration > database |
| A.3 Load test | `Load/LoadTests.cs` + `loadtest.settings.json` | 5 concurrent users for 30 s |
| A.4 Selenium UI | `UI/SeleniumUiTests.cs` | Employee login, anonymous donation, wrong-password login |

## What changed in the app (so it could be tested)

- **`givers_function/DonationLogic.cs` (new):** the donation rules (validation, totals, certificate text, reference number) moved out of the function so they can be unit tested.
- **`givers_function/Function1.cs`:** donations are now validated and **saved to a `Donations` table**. Each one gets a unique reference such as `GOTG-20261026-1A2B3C4D`. The "role" default bug is fixed.
- **`Pages/Donate/Index.cshtml.cs`:** the amount is sent to the function in a culture-safe format.
- **`SQLQuery2_Donations.sql` (new):** creates the `Donations` table.

## One-time setup

1. Open `gift_of_thegivers/gift_of_thegivers.sln` in Visual Studio. NuGet packages restore automatically.
2. In **SQL Server Object Explorer**, open a query on `(localdb)\ngo` and run `SQLQuery2_Donations.sql`. Run `SQLQuery1.sql` first if the database doesn't exist yet.
3. Make sure **Google Chrome** is installed (needed for Selenium).
4. **Build > Build Solution.**

## Starting the app (needed for A.2 form flow, A.3 and A.4)

Right-click the solution, choose **Configure Startup Projects**, then:

1. Select **Multiple startup projects**.
2. Set **gift_of_thegivers** to **Start** and choose its **http** profile (port 5191).
3. Set **givers_function** to **Start** (port 7234).
4. Press **Ctrl+F5** to start without debugging.

Leave both running while you run the tests from **Test > Test Explorer**.

> **Tip:** in Test Explorer, choose **Group By > Traits** to see the tests split into Unit, Integration, Load and UI.

## A.1 – Unit tests (10 marks)

- **Needs:** nothing running.
- **Run:** in Test Explorer, right-click **Category [Unit]** and choose **Run**.
- **Screenshot:** the green ticks in Test Explorer, plus `DonationLogicTests.cs`.

## A.2 – Integration tests (10 marks)

- **Needs:**
  - `FunctionDatabaseTests` needs only the database.
  - `DonationFlowTests` also needs **givers_function** running.
- **Run:** right-click **Category [Integration]** and choose **Run**.
- **What it proves:** `DonationFlowTests` hosts the website in memory and submits the real Donate form. The site calls the running Azure Function, and the test then reads the `Donations` table to confirm the row was saved. Test data is deleted afterwards.
- **Screenshots and snippets:**
  - Test Explorer passing.
  - The code of `AnonymousDonation_FromForm_ThroughFunction_IsSavedToDatabase`.
  - Optionally, `SELECT * FROM Donations` while debugging, before cleanup.

## A.3 – Load and stress testing (10 marks)

- **Needs:** website **and** function running.
- **Configuration:** `loadtest.settings.json` sets 5 virtual users, 30 seconds, 500 ms think time, and pass limits (average under 1000 ms, 95th percentile under 2000 ms, error rate under 1 %). Each user loops: Home > About > Donate form > Tax certificate (Function).
- **Run:** right-click **Category [Load]** and choose **Run**. It takes about 30 seconds.
- **Results:** click the test, then **Open additional output for this result**. You'll see a table of requests, errors, average, P95 and max per page, plus throughput. It's also saved to `GiftOfTheGivers.Tests/bin/Debug/net8.0/LoadTestResults.txt`.
- **Screenshots:** `loadtest.settings.json` (the configuration) and the results table.

## A.4 – Selenium UI tests (15 marks)

- **Needs:** website **and** function running, plus Chrome. Selenium downloads ChromeDriver itself the first time, so give it a moment.
- **Run:** right-click **Category [UI]** and choose **Run**. A Chrome window opens and clicks through each scenario.
- **Scenarios:**
  1. **Employee login:** registers a new Employee, logs in, and checks it lands on the Staff Dashboard with "Hello, Selenium Employee".
  2. **Anonymous donation:** fills in R500, ticks "Donate as anonymous guest", and checks the certificate shows "Anonymous Guest" and "500.00 ZAR".
  3. **Wrong password:** stays on the Login page.
- **Screenshots:** Selenium saves its own screenshots to `GiftOfTheGivers.Tests/bin/Debug/net8.0/Screenshots/`. Add the Test Explorer results and the test code.
- Set the environment variable `GOTG_HEADLESS=true` if you want it to run without a visible window.

## If your ports or database are different

The tests read these environment variables (defaults in brackets):

- `GOTG_WEB_URL` (`http://localhost:5191`)
- `GOTG_FUNCTION_URL` (`http://localhost:7234/api/Function1`)
- `GOTG_SQL` (the `(localdb)\ngo` connection string)

## Troubleshooting

- **"Could not open the gift_of_thegivers database":** run the SQL scripts on `(localdb)\ngo`.
- **"Could not generate the tax certificate" or the Selenium donation fails:** `givers_function` isn't running.
- **Selenium can't reach the site:** start the website with the **http** profile, or set `GOTG_WEB_URL` to the address shown in its console window.
