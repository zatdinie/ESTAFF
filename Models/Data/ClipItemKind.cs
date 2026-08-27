namespace EHS_PORTAL.Areas.ESTAFF.Models.Data
{
    // Which CLIP table a task's attached record lives in.
    //
    // Stored on the task (TaskItem.ClipItemKind) rather than inferred, because
    // the two CLIP tables number their rows independently — id 14 is a valid
    // certificate and a valid monitoring record at the same time, so the id
    // alone cannot say what it points at.
    //
    // This used to be derived from the task's TaskList name ("Certificate Of
    // FItness" meant a COF, "Plant Monitoring" meant monitoring), which tied
    // attaching a CLIP record to picking a particular task type under a
    // particular classification. Attaching a record is now independent of how
    // the task is classified, so the kind is recorded outright.
    public enum ClipItemKind
    {
        COF = 1,
        PlantMonitoring = 2
    }
}
