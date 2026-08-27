using System;
using System.Collections.Generic;
using System.Linq;

namespace ESTAFF.Models.Data
{
    // The ten numbered parts of the Environment, Safety and Health Monthly
    // Report a Safety and Health Officer files under the Occupational Safety &
    // Health (Safety & Health Officer) Regulations 1997.
    //
    // The numbering, the titles and the regulation each part answers to are
    // fixed by the form, not by us - which is why they are an enum with a
    // lookup rather than another data-maintained taxonomy. What ESTAFF decides
    // is only which of its own classifications feed which part; see
    // TaskClassification.ReportSection.
    //
    // Two parts have no task behind them. Statistics counts incidents and
    // PurchaseRequests lists purchase orders, and ESTAFF records neither, so
    // both print as the blank statutory grid to be completed by hand.
    public enum EshSection
    {
        ComplianceActivity    = 1,
        SafeWorkplaceMethods  = 2,
        Statistics            = 3,
        InjuryHazards         = 4,
        RiskMinimisation      = 5,
        PurchaseRequests      = 6,
        LayoutChanges         = 7,
        TrainingAndInspection = 8,
        MattersArising        = 9,
        Feedback              = 10
    }

    // How a part of the form is laid out. Four of the ten share one grid, three
    // share a narrower one, and the remaining three are each their own thing.
    public enum EshSectionShape
    {
        // No | Item | Issue / Concern | Action Taken | Remarks-or-Date
        WorkItem,

        // No | Description | Issue / Concern | Recommendation
        LayoutChange,

        // No | Description | Remarks
        Note,

        // The incident case matrix. Not task-backed.
        Statistics,

        // No | Date | PR No | Description | Status. Not task-backed.
        PurchaseRequest
    }

    // One part of the form, as it has to be printed.
    public class EshSectionInfo
    {
        public EshSection Section { get; private set; }
        public int Number { get; private set; }
        public string Title { get; private set; }

        // The regulation the part answers to, or null where the form cites
        // none - section 6 carries no citation on the original.
        public string Regulation { get; private set; }

        public EshSectionShape Shape { get; private set; }

        // What the last column of a WorkItem grid is called. The form calls it
        // "Remarks" in sections 1 and 5 and "Date" in sections 2 and 4.
        public string TrailingHeader { get; private set; }

        public bool IsTaskBacked
        {
            get
            {
                return Shape == EshSectionShape.WorkItem
                    || Shape == EshSectionShape.LayoutChange
                    || Shape == EshSectionShape.Note;
            }
        }

        // "2. Methods of Establishing and Maintaining a Safe and Healthy
        // Workplace" - the heading exactly as the form numbers it.
        public string Heading
        {
            get { return Number + ". " + Title; }
        }

        internal EshSectionInfo(EshSection section, string title,
            string regulation, EshSectionShape shape,
            string trailingHeader = null)
        {
            Section        = section;
            Number         = (int)section;
            Title          = title;
            Regulation     = regulation;
            Shape          = shape;
            TrailingHeader = trailingHeader;
        }
    }

    public static class EshSections
    {
        // The regulation citations are printed verbatim under each heading, so
        // they are written out in full rather than composed from a pattern.
        private const string Reg = ", OSH (SHO) Regulation 1997";

        private static readonly List<EshSectionInfo> All =
            new List<EshSectionInfo>
            {
                new EshSectionInfo(EshSection.ComplianceActivity,
                    "Compliance Activity with Safety and Health Related "
                    + "Regulation",
                    "Regulation 19(2) (a)" + Reg,
                    EshSectionShape.WorkItem, "Remarks"),

                new EshSectionInfo(EshSection.SafeWorkplaceMethods,
                    "Methods of Establishing and Maintaining a Safe and "
                    + "Healthy Workplace",
                    "Regulation 19(2) (b)" + Reg,
                    EshSectionShape.WorkItem, "Date"),

                new EshSectionInfo(EshSection.Statistics,
                    "Safety and Health Statistic",
                    "Regulation 19(2) (c)" + Reg,
                    EshSectionShape.Statistics),

                new EshSectionInfo(EshSection.InjuryHazards,
                    "(Machinery / Plant / Equipment / Appliance / Substance / "
                    + "Process / Manual Labour) That Can Led to Injuries",
                    "Regulation 19(2) (d)" + Reg,
                    EshSectionShape.WorkItem, "Date"),

                new EshSectionInfo(EshSection.RiskMinimisation,
                    "(Machinery / Plant / Equipment / Appliance / PPE "
                    + "Required) Given for Minimizing Risk",
                    "Regulation 19(2) (e)" + Reg,
                    EshSectionShape.WorkItem, "Remarks"),

                new EshSectionInfo(EshSection.PurchaseRequests,
                    "Purchase Request (PR) of the Month",
                    null,
                    EshSectionShape.PurchaseRequest),

                new EshSectionInfo(EshSection.LayoutChanges,
                    "Layout Changes in The Premises",
                    "Regulation 19(2) (f)" + Reg,
                    EshSectionShape.LayoutChange),

                new EshSectionInfo(EshSection.TrainingAndInspection,
                    "Safety and Health Training, Promotions, Activities, and "
                    + "Inspection",
                    "Regulation 19(2) (g)" + Reg,
                    EshSectionShape.Note),

                new EshSectionInfo(EshSection.MattersArising,
                    "Matters Arising, Unclosed Items from Previous Report",
                    "Regulation 19(2) (h)" + Reg,
                    EshSectionShape.Note),

                new EshSectionInfo(EshSection.Feedback,
                    "Feedback, Communication Received Related Safety and "
                    + "Health",
                    "Regulation 19(2) (i)" + Reg,
                    EshSectionShape.Note)
            };

        // Where a task lands when nothing has said otherwise. Section 2 is the
        // catch-all on the form itself - it is the part that asks what was done
        // to keep the workplace safe, which is what an unclassified ESTAFF task
        // records - so an unmapped classification is filed there rather than
        // dropped from a statutory return.
        public const EshSection Default = EshSection.SafeWorkplaceMethods;

        // Every section, in the order the form prints them.
        public static IEnumerable<EshSectionInfo> InFormOrder()
        {
            return All.OrderBy(s => s.Number);
        }

        // The sections an admin can map a classification to: only the ones that
        // read their rows from tasks. Mapping a classification to the incident
        // statistics or the purchase request list would have nowhere to print.
        public static IEnumerable<EshSectionInfo> Mappable()
        {
            return InFormOrder().Where(s => s.IsTaskBacked);
        }

        public static EshSectionInfo Describe(EshSection section)
        {
            return All.FirstOrDefault(s => s.Section == section);
        }

        public static EshSectionInfo Describe(EshSection? section)
        {
            return Describe(section ?? Default);
        }

        // "§2 Methods of Establishing..." for the admin screens. A
        // classification that has never been mapped says so rather than
        // claiming the section it will silently fall into.
        public static string ShortLabel(EshSection? section)
        {
            if (!section.HasValue) return "Not mapped";

            var info = Describe(section.Value);
            return info == null
                ? "Not mapped"
                : info.Number + ". " + info.Title;
        }

        // Guards a value read from the database: the column is a plain int and
        // this application is not the only thing that can write to it.
        public static EshSection? Sanitise(EshSection? section)
        {
            if (!section.HasValue) return null;
            return Describe(section.Value) != null ? section : null;
        }
    }
}
