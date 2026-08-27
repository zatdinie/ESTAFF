/* ============================================================
     ESTAFF — columns ESTAFF adds to EHS_PORTAL's user table.

     *** This script alters CLIP.AspNetUsers, which belongs to
     *** EHS_PORTAL. It needs their review before production.

     ESTAFF's ApplicationUser extends Identity's user with six
     fields. Every non-nullable one carries a DEFAULT: EHS_PORTAL
     inserts users without knowing these columns exist, and a NULL
     in CreatedDate/LastModifiedDate makes ESTAFF throw when it
     reads that user.

     IsActive defaults to 0 deliberately — a new EHS_PORTAL account
     should not be able to sign into ESTAFF until an admin enables
     it. ESTAFF sets IsActive explicitly when it creates a user
     (AdminController.CreateEmployee), so this only affects rows
     ESTAFF did not create.

     Safe to re-run: every statement is guarded.
     Adds columns only — nothing existing is altered or dropped.
  ============================================================ */

  IF NOT EXISTS (SELECT 1 FROM sys.columns
                 WHERE object_id = OBJECT_ID(N'[CLIP].[AspNetUsers]')
                   AND name = N'EmpID')
  BEGIN
      ALTER TABLE [CLIP].[AspNetUsers] ADD [EmpID] NVARCHAR(50) NULL;
      PRINT 'Added CLIP.AspNetUsers.EmpID';
  END
  ELSE PRINT 'CLIP.AspNetUsers.EmpID already exists';
  GO

  IF NOT EXISTS (SELECT 1 FROM sys.columns
                 WHERE object_id = OBJECT_ID(N'[CLIP].[AspNetUsers]')
                   AND name = N'IsAdmin')
  BEGIN
      ALTER TABLE [CLIP].[AspNetUsers]
          ADD [IsAdmin] BIT NOT NULL
              CONSTRAINT [DF_AspNetUsers_IsAdmin] DEFAULT (0);
      PRINT 'Added CLIP.AspNetUsers.IsAdmin';
  END
  ELSE PRINT 'CLIP.AspNetUsers.IsAdmin already exists';
  GO

  IF NOT EXISTS (SELECT 1 FROM sys.columns
                 WHERE object_id = OBJECT_ID(N'[CLIP].[AspNetUsers]')
                   AND name = N'IsActive')
  BEGIN
      ALTER TABLE [CLIP].[AspNetUsers]
          ADD [IsActive] BIT NOT NULL
              CONSTRAINT [DF_AspNetUsers_IsActive] DEFAULT (0);
      PRINT 'Added CLIP.AspNetUsers.IsActive';
  END
  ELSE PRINT 'CLIP.AspNetUsers.IsActive already exists';
  GO

  IF NOT EXISTS (SELECT 1 FROM sys.columns
                 WHERE object_id = OBJECT_ID(N'[CLIP].[AspNetUsers]')
                   AND name = N'HireDate')
  BEGIN
      ALTER TABLE [CLIP].[AspNetUsers] ADD [HireDate] DATETIME NULL;
      PRINT 'Added CLIP.AspNetUsers.HireDate';
  END
  ELSE PRINT 'CLIP.AspNetUsers.HireDate already exists';
  GO

  IF NOT EXISTS (SELECT 1 FROM sys.columns
                 WHERE object_id = OBJECT_ID(N'[CLIP].[AspNetUsers]')
                   AND name = N'CreatedDate')
  BEGIN
      ALTER TABLE [CLIP].[AspNetUsers]
          ADD [CreatedDate] DATETIME NOT NULL
              CONSTRAINT [DF_AspNetUsers_CreatedDate] DEFAULT (GETDATE());
      PRINT 'Added CLIP.AspNetUsers.CreatedDate';
  END
  ELSE PRINT 'CLIP.AspNetUsers.CreatedDate already exists';
  GO

  IF NOT EXISTS (SELECT 1 FROM sys.columns
                 WHERE object_id = OBJECT_ID(N'[CLIP].[AspNetUsers]')
                   AND name = N'LastModifiedDate')
  BEGIN
      ALTER TABLE [CLIP].[AspNetUsers]
          ADD [LastModifiedDate] DATETIME NOT NULL
              CONSTRAINT [DF_AspNetUsers_LastModifiedDate] DEFAULT (GETDATE());
      PRINT 'Added CLIP.AspNetUsers.LastModifiedDate';
  END
  ELSE PRINT 'CLIP.AspNetUsers.LastModifiedDate already exists';
  GO

  /* Existing rows, for a sanity check after applying. */
  SELECT COUNT(*) AS [Users],
         SUM(CASE WHEN [IsAdmin]  = 1 THEN 1 ELSE 0 END) AS [Admins],
         SUM(CASE WHEN [IsActive] = 1 THEN 1 ELSE 0 END) AS [Active],
         SUM(CASE WHEN [EmpID] IS NOT NULL THEN 1 ELSE 0 END) AS [WithEmpID]
    FROM [CLIP].[AspNetUsers];
  GO