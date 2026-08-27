/* ============================================================
     ESTAFF — a report covers a plant, not a person.

     A report used to list the tasks of the employee who
     generated it. It now lists every task belonging to a plant
     for the period, which is what the statutory ESH monthly
     return actually is: one return per plant per month, not one
     per officer.

     Which tasks belong to a plant is derived from
     CLIP.UserPlants — a task counts for a plant when the person
     it is assigned to is mapped to that plant in EHS_PORTAL.

     *** Be aware CLIP.UserPlants is EHS_PORTAL's record and is
     *** incomplete. An employee with no rows there belongs to no
     *** plant, so their tasks appear in NO report. Four of
     *** seventeen users were in that position when this was
     *** written, the ESTAFF admin among them. Filling those rows
     *** in is an EHS_PORTAL job, not something ESTAFF can fix.

     PlantId is NULLable. The reports already submitted were
     personal ones covering one employee's tasks, and there is no
     honest plant to backfill them with — the employee may have
     been mapped to several plants, or to none. A report with no
     PlantId is a legacy personal report and is read as such.

     Safe to re-run: every statement is guarded.
     Adds a column only — nothing existing is altered or dropped.
  ============================================================ */

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[ESTAFF].[Reports]')
                 AND name = N'PlantId')
BEGIN
    ALTER TABLE [ESTAFF].[Reports] ADD [PlantId] INT NULL;
    PRINT 'Added ESTAFF.Reports.PlantId';
END
ELSE PRINT 'ESTAFF.Reports.PlantId already exists';
GO

/* Deliberately NO foreign key to CLIP.Plants.

   ESTAFF.Reports already FKs to CLIP.AspNetUsers, so the
   precedent exists — but a submitted report is a historical
   record, and a plant EHS_PORTAL later removes or renumbers
   should not make an old return unreadable or unsaveable. The id
   is stored plainly and the name resolved when the report is
   rendered; a plant that no longer exists simply prints as
   unknown rather than blocking the row. */

/* One report per plant per period is enforced in
   EmployeeController.SubmitReport rather than by a unique index:
   a rejected report may legitimately be followed by another for
   the same plant and period, which a plain unique constraint
   would forbid. */

/* Existing rows, for a sanity check after applying. */
SELECT COUNT(*) AS [Reports],
       SUM(CASE WHEN [PlantId] IS NULL THEN 1 ELSE 0 END) AS [LegacyPersonal],
       SUM(CASE WHEN [PlantId] IS NOT NULL THEN 1 ELSE 0 END) AS [PlantScoped]
  FROM [ESTAFF].[Reports];
GO
