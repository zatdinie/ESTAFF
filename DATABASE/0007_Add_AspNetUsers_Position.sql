/* ============================================================
     ESTAFF — position and JKKP number on the user record.

     *** This script alters CLIP.AspNetUsers, which belongs to
     *** EHS_PORTAL. It needs their review before production,
     *** same as 0002.

     The statutory ESH monthly return names three officers with
     their position and JKKP registration. Those have until now
     been Web.config settings (Esh:Sho.*, Esh:Officer.*,
     Esh:Approver.*), because the report named officers the
     application did not otherwise know about.

     Now that ESTAFF records who submitted and who approved each
     report (ESTAFF.ReportApprovals, script 0006), the letterhead
     can name the actual people — which means their position and
     JKKP number have to live on the user rather than in config.

     Position is stored as the underlying int of the C# enum
     (Models/Data/ApplicationUser.cs):
         1 = EshManager   2 = EshEngineer
         3 = ShoOfficer   4 = EshOfficer

     It carries a DEFAULT because EHS_PORTAL inserts users without
     knowing this column exists, and a NOT NULL column with no
     default would make their INSERT fail. The default matches the
     C# initialiser, so a row created either side means the same
     thing.

     JkkpNo is NULL: most staff do not hold a JKKP registration,
     and a blank prints as a ruled line on the report.

     Safe to re-run: every statement is guarded.
     Adds columns only — nothing existing is altered or dropped.
  ============================================================ */

USE ESH
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[CLIP].[AspNetUsers]')
                 AND name = N'Position')
BEGIN
    ALTER TABLE [CLIP].[AspNetUsers]
        ADD [Position] INT NOT NULL
            CONSTRAINT [DF_AspNetUsers_Position] DEFAULT (4);
    PRINT 'Added CLIP.AspNetUsers.Position';
END
ELSE PRINT 'CLIP.AspNetUsers.Position already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[CLIP].[AspNetUsers]')
                 AND name = N'JkkpNo')
BEGIN
    ALTER TABLE [CLIP].[AspNetUsers] ADD [JkkpNo] NVARCHAR(50) NULL;
    PRINT 'Added CLIP.AspNetUsers.JkkpNo';
END
ELSE PRINT 'CLIP.AspNetUsers.JkkpNo already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = N'CK_AspNetUsers_Position')
BEGIN
    ALTER TABLE [CLIP].[AspNetUsers] WITH CHECK
        ADD CONSTRAINT [CK_AspNetUsers_Position]
        CHECK ([Position] IN (1, 2, 3, 4));
    PRINT 'Created CK_AspNetUsers_Position';
END
ELSE PRINT 'CK_AspNetUsers_Position already exists';
GO