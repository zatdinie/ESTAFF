USE ESH
GO

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ReportApprovals' AND SCHEMA_NAME(schema_id) = N'ESTAFF')
    AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ESTAFF].[ReportApprovals]') AND name = N'SubmittedDate')

BEGIN
    DROP TABLE [ESTAFF].[ReportApprovals]
    PRINT 'Dropped table [ESTAFF].[ReportApprovals]'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ReportApprovals' AND SCHEMA_NAME(schema_id) = N'ESTAFF')

BEGIN 
    CREATE TABLE [ESTAFF].[ReportApprovals](
        [ApprovalId] [int] IDENTITY(1,1) NOT NULL,
        [ReportId] [int] NOT NULL,
        [ReporterId] [nvarchar](128) NOT NULL,
        [SubmittedDate] [datetime] NOT NULL,
        [ApprovalStatus] [int] NOT NULL,
        [ApproverId] [nvarchar](128) NULL,
        [DateApproved] [datetime] NULL,
        [Comments] [nvarchar](max) NULL,

        CONSTRAINT [PK_ESTAFF.ReportApprovals] PRIMARY KEY CLUSTERED ([ApprovalId] ASC)
        ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];

    PRINT 'Created table [ESTAFF].[ReportApprovals]'
END
ELSE PRINT '[ESTAFF].[ReportApprovals] already exists.'
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReportApprovals_ReportId' AND object_id = OBJECT_ID(N'[ESTAFF].[ReportApprovals]'))
BEGIN
    CREATE INDEX [IX_ReportApprovals_ReportId] ON [ESTAFF].[ReportApprovals] ([ReportId] ASC);
    PRINT 'Created index [IX_ReportApprovals_ReportId] on table [ESTAFF].[ReportApprovals]'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ESTAFF.ReportApprovals_ESTAFF.Reports_ReportId')
BEGIN
    ALTER TABLE [ESTAFF].[ReportApprovals] WITH CHECK
        ADD CONSTRAINT [FK_ESTAFF.ReportApprovals_ESTAFF.Reports_ReportId]
        FOREIGN KEY([ReportId]) REFERENCES [ESTAFF].[Reports] ([ReportId]);
    PRINT 'Created FK ReportApprovals -> Reports';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ESTAFF.ReportApprovals_CLIP.AspNetUsers_ReporterId')
BEGIN
    ALTER TABLE [ESTAFF].[ReportApprovals] WITH CHECK
        ADD CONSTRAINT [FK_ESTAFF.ReportApprovals_CLIP.AspNetUsers_ReporterId]
        FOREIGN KEY([ReporterId]) REFERENCES [CLIP].[AspNetUsers] ([Id]);
    PRINT 'Created FK ReportApprovals -> AspNetUsers (Reporter)';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ESTAFF.ReportApprovals_CLIP.AspNetUsers_ApproverId')
BEGIN
    ALTER TABLE [ESTAFF].[ReportApprovals] WITH CHECK
        ADD CONSTRAINT [FK_ESTAFF.ReportApprovals_CLIP.AspNetUsers_ApproverId]
        FOREIGN KEY([ApproverId]) REFERENCES [CLIP].[AspNetUsers] ([Id]);
    PRINT 'Created FK ReportApprovals -> AspNetUsers (Approver)';
END
GO