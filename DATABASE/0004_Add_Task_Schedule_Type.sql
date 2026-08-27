/* ============================================================
     ESTAFF — daily task vs long-term task.

     0003 added the hours a task's work runs between. This adds
     the two things that make those hours usable:

       ScheduleType  which kind of task this is
       PeriodDate    the day the period falls on

     A task is now one of two kinds, chosen by whoever raises it:

       Daily (1)     work done on one named day between two hours.
                     The period is required.
       LongTerm (2)  work tracked to a due date. The period is
                     optional - a long-term task may still record
                     the hours it was worked, and most will not.

     Both kinds keep DueDate, which stays NOT NULL. Nothing that
     reads DueDate today - the overdue sweep, the calendar, task
     sorting, the report period queries - has to learn a new rule.

     ScheduleType is NOT NULL DEFAULT 2. Unlike the period columns
     in 0003, a default here is honest rather than invented: a task
     already in ESTAFF has a due date and no period, which is
     exactly what LongTerm means. Nothing is being guessed.

     Safe to re-run: every statement is guarded.
  ============================================================ */

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[ESTAFF].[TaskItems]')
                 AND name = N'ScheduleType')
BEGIN
    ALTER TABLE [ESTAFF].[TaskItems]
        ADD [ScheduleType] INT NOT NULL
            CONSTRAINT [DF_TaskItems_ScheduleType] DEFAULT (2);
    PRINT 'Added ESTAFF.TaskItems.ScheduleType (existing rows = LongTerm)';
END
ELSE PRINT 'ESTAFF.TaskItems.ScheduleType already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[ESTAFF].[TaskItems]')
                 AND name = N'PeriodDate')
BEGIN
    ALTER TABLE [ESTAFF].[TaskItems] ADD [PeriodDate] DATE NULL;
    PRINT 'Added ESTAFF.TaskItems.PeriodDate';
END
ELSE PRINT 'ESTAFF.TaskItems.PeriodDate already exists';
GO

/* The enum is stored as a plain int and this application is not
   the only thing that can write to the column. Mirrors
   CK_TaskItems_ClipItemKind. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = N'CK_TaskItems_ScheduleType')
BEGIN
    ALTER TABLE [ESTAFF].[TaskItems] WITH CHECK
        ADD CONSTRAINT [CK_TaskItems_ScheduleType]
        CHECK ([ScheduleType] IN (1, 2));
    PRINT 'Added CK_TaskItems_ScheduleType';
END
ELSE PRINT 'CK_TaskItems_ScheduleType already exists';
GO

/* Existing rows, for a sanity check after applying. */
SELECT COUNT(*) AS [Tasks],
       SUM(CASE WHEN [ScheduleType] = 1 THEN 1 ELSE 0 END) AS [Daily],
       SUM(CASE WHEN [ScheduleType] = 2 THEN 1 ELSE 0 END) AS [LongTerm],
       SUM(CASE WHEN [PeriodDate] IS NOT NULL THEN 1 ELSE 0 END) AS [WithPeriodDate]
  FROM [ESTAFF].[TaskItems];
GO
