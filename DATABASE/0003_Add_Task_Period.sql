/* ============================================================
     ESTAFF — the hours a task's work runs between.

     TaskItem already carries DueDate, the day the job has to be
     finished by. These two columns record when on that day the
     work runs — 08:00 to 17:00 — which whoever raises the task
     now supplies on the create form (employee and admin alike).

     TIME, not DATETIME: the date is DueDate's job. Whole hours
     are all the forms offer, but the column keeps TIME(0) rather
     than being narrowed further, so a finer picker later needs no
     migration.

     PeriodEnd earlier than PeriodStart is allowed on purpose and
     means the work carried past midnight — a 22:00 to 06:00 night
     shift. There is deliberately NO check constraint on the
     ordering: it would reject a legitimate night shift, and the
     database cannot tell one from a typo.

     Both are NULLable. The tasks already in ESTAFF were raised
     before anyone was asked for hours, and there is no honest
     value to backfill them with — a made-up range would print in
     the statutory report as though someone had recorded it. The
     forms require them going forward; existing rows keep reading
     as "no period recorded".

     No DEFAULT for the same reason: a default would quietly give
     every future row written outside ESTAFF hours nobody chose.

     Safe to re-run: every statement is guarded.
  ============================================================ */

/* An earlier draft of this script created these as DATETIME with a
   CK_TaskItems_Period ordering constraint. If that version was
   applied, undo both here rather than leaving the table half in
   the old shape — the constraint now forbids a valid night shift,
   and DATETIME stores a date component nothing reads.

   Converting DATETIME to TIME keeps the time of day and discards
   the date, which is exactly the wanted result: rows written by
   that draft hold a date at midnight, so they become 00:00:00 and
   read as "recorded, but at midnight". There were no such rows in
   any environment this reached; if yours differs, check them
   before running. */

IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = N'CK_TaskItems_Period')
BEGIN
    ALTER TABLE [ESTAFF].[TaskItems] DROP CONSTRAINT [CK_TaskItems_Period];
    PRINT 'Dropped CK_TaskItems_Period (night shifts are legitimate)';
END
GO

IF EXISTS (SELECT 1 FROM sys.columns c
           JOIN sys.types t ON t.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[ESTAFF].[TaskItems]')
             AND c.name = N'PeriodStart'
             AND t.name <> N'time')
BEGIN
    ALTER TABLE [ESTAFF].[TaskItems] ALTER COLUMN [PeriodStart] TIME(0) NULL;
    PRINT 'Converted ESTAFF.TaskItems.PeriodStart to TIME(0)';
END
GO

IF EXISTS (SELECT 1 FROM sys.columns c
           JOIN sys.types t ON t.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[ESTAFF].[TaskItems]')
             AND c.name = N'PeriodEnd'
             AND t.name <> N'time')
BEGIN
    ALTER TABLE [ESTAFF].[TaskItems] ALTER COLUMN [PeriodEnd] TIME(0) NULL;
    PRINT 'Converted ESTAFF.TaskItems.PeriodEnd to TIME(0)';
END
GO

/* The ordinary path: neither column exists yet. */

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[ESTAFF].[TaskItems]')
                 AND name = N'PeriodStart')
BEGIN
    ALTER TABLE [ESTAFF].[TaskItems] ADD [PeriodStart] TIME(0) NULL;
    PRINT 'Added ESTAFF.TaskItems.PeriodStart';
END
ELSE PRINT 'ESTAFF.TaskItems.PeriodStart already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[ESTAFF].[TaskItems]')
                 AND name = N'PeriodEnd')
BEGIN
    ALTER TABLE [ESTAFF].[TaskItems] ADD [PeriodEnd] TIME(0) NULL;
    PRINT 'Added ESTAFF.TaskItems.PeriodEnd';
END
ELSE PRINT 'ESTAFF.TaskItems.PeriodEnd already exists';
GO

/* Existing rows, for a sanity check after applying. */
SELECT COUNT(*) AS [Tasks],
       SUM(CASE WHEN [PeriodStart] IS NOT NULL
                 AND [PeriodEnd]   IS NOT NULL THEN 1 ELSE 0 END)
           AS [WithPeriod],
       SUM(CASE WHEN [PeriodEnd] < [PeriodStart] THEN 1 ELSE 0 END)
           AS [Overnight]
  FROM [ESTAFF].[TaskItems];
GO
