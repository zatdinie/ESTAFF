using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using EHS_PORTAL.Areas.ESTAFF.Models.Data;
using EHS_PORTAL.Areas.ESTAFF.Models.ViewModels;

namespace EHS_PORTAL.Areas.ESTAFF.Services
{
    // Produces the Environment, Safety and Health Monthly Report: the return a
    // Safety and Health Officer files under the Occupational Safety & Health
    // (Safety & Health Officer) Regulations 1997.
    //
    // The document is not ours to design. It has ten numbered parts in a fixed
    // order, each answering a named regulation, each with the columns the form
    // gives it - so this service prints all ten every time, in order, whether
    // or not ESTAFF has anything to put in them. A part with no rows says so;
    // it is never dropped, because a return that silently omits a part reads as
    // a complete return that found nothing.
    //
    // What ESTAFF supplies is the task rows. A task's classification decides
    // which part it prints under (TaskClassification.ReportSection), its title
    // is the item, its Concern/Issue is the concern, and the remarks recorded
    // against its status changes are the action taken.
    //
    // Two parts have no source in ESTAFF at all. Section 3 counts incident
    // cases and section 6 lists purchase requests, and this application records
    // neither, so both print as the blank statutory grid for completion by
    // hand. That is deliberate: leaving them out would make the printed
    // document non-compliant, and inventing zeroes for incidents nobody has
    // reported to ESTAFF would be worse than a blank.
    //
    // Layout stays conservative - portrait, ruled grids, repeating table
    // headers, a running header and numbered pages - because it is printed,
    // signed, scanned and attached to audits.
    public class ReportPdfService
    {
        // ── Palette ────────────────────────────────────────────
        private static readonly BaseColor Ink
            = new BaseColor(10, 22, 40);
        private static readonly BaseColor InkSoft
            = new BaseColor(51, 65, 85);
        private static readonly BaseColor Muted
            = new BaseColor(100, 116, 139);
        private static readonly BaseColor Accent
            = new BaseColor(16, 185, 129);
        private static readonly BaseColor Rule
            = new BaseColor(203, 213, 225);
        private static readonly BaseColor Panel
            = new BaseColor(248, 250, 252);
        private static readonly BaseColor PanelDeep
            = new BaseColor(226, 232, 240);
        private static readonly BaseColor White
            = BaseColor.WHITE;
        private static readonly BaseColor Danger
            = new BaseColor(239, 68, 68);
        private static readonly BaseColor Warning
            = new BaseColor(245, 158, 11);
        private static readonly BaseColor Info
            = new BaseColor(59, 130, 246);

        // How many blank rows a statutory grid with no ESTAFF source behind it
        // prints. Enough to write a month's worth in by hand without running
        // the section onto its own page.
        public byte[] GeneratePdf(ReportDetailViewModel vm)
        {
            var tasks    = ResolveTasks(vm);
            var settings = EshReportSettings.Load();

            using (var ms = new MemoryStream())
            {
                // Narrower side margins than a letter would take: the form's
                // widest grid is five columns of prose and needs the width.
                var doc = new Document(PageSize.A4, 30f, 30f, 42f, 46f);
                var writer = PdfWriter.GetInstance(doc, ms);

                writer.PageEvent = new PageFurniture(vm, settings);

                doc.AddTitle("Environment, Safety and Health "
                             + vm.ReportTypeLabel + " Report - "
                             + Text(vm.EmpName));
                doc.AddAuthor(Text(settings.Company) ?? "ESTAFF");
                doc.AddSubject("ESH report for " + vm.PeriodText);

                doc.Open();

                AddLetterhead(doc, vm, settings);
                AddIdentityBlock(doc, vm, settings);

                // Only the parts ESTAFF holds the data for.
                //
                // Sections 3 (incident statistics) and 6 (purchase requests)
                // have no source in this system - there is no incident or
                // purchase-order record - and used to print as blank statutory
                // grids for completion by hand. They are now left out
                // altogether.
                //
                // The numbering of the rest is untouched: what prints runs
                // 1, 2, 4, 5, 7, 8, 9, 10, so the two gaps are visible to a
                // reader and the parts that are present still carry the
                // numbers the regulation gives them. Anyone filing the return
                // has to supply 3 and 6 from the incident register and the
                // purchasing record separately.
                foreach (var section in EshSections.InFormOrder()
                             .Where(s => s.IsTaskBacked))
                    AddSection(doc, section, tasks);

                AddApprovalTrail(doc, vm);
                AddSignOff(doc, vm, settings);

                doc.Close();
                return ms.ToArray();
            }
        }

        // The controllers fill TaskDetails. A caller that only set Tasks still
        // gets a valid document rather than an empty one.
        private static List<ReportTaskDetailViewModel> ResolveTasks(
            ReportDetailViewModel vm)
        {
            if (vm.TaskDetails != null && vm.TaskDetails.Any())
                return vm.TaskDetails;

            if (vm.Tasks == null) return new List<ReportTaskDetailViewModel>();

            return vm.Tasks
                .Select(t => ReportTaskDetailViewModel.From(t, null, null))
                .ToList();
        }

        // ══════════════════════════════════════════════════════
        // LETTERHEAD AND IDENTITY
        // ══════════════════════════════════════════════════════

        private static void AddLetterhead(Document doc,
            ReportDetailViewModel vm, EshReportSettings settings)
        {
            var band = new PdfPTable(1) { WidthPercentage = 100 };
            band.SpacingAfter = 10f;

            var cell = new PdfPCell
            {
                Border      = Rectangle.BOX,
                BorderWidth = 0.8f,
                BorderColor = Ink,
                Padding     = 12f
            };

            if (settings.HasLogo)
            {
                var logo = LoadLogo(settings.LogoPath);
                if (logo != null) cell.AddElement(logo);
            }

            cell.AddElement(Para(
                Blank(settings.Company, "COMPANY NAME NOT CONFIGURED"),
                Font(13f, Ink, true), 0f, Element.ALIGN_CENTER));

            cell.AddElement(Para(
                "Environment, Safety and Health " + vm.ReportTypeLabel
                + " Report",
                Font(11.5f, Ink, true), 5f, Element.ALIGN_CENTER));

            cell.AddElement(Para(
                "(In compliance with Occupational Safety & Health "
                + "(Safety & Health Officer) Regulation 1997)",
                FontItalic(7.5f, Muted), 4f, Element.ALIGN_CENTER));

            band.AddCell(cell);
            doc.Add(band);
        }

        // A logo that has been moved or corrupted must not take the report with
        // it - the letterhead is the one part of this document nobody reads for
        // information.
        private static Image LoadLogo(string path)
        {
            try
            {
                var logo = Image.GetInstance(path);
                logo.ScaleToFit(150f, 42f);
                logo.Alignment = Element.ALIGN_CENTER;
                logo.SpacingAfter = 6f;
                return logo;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Who filed the return, from Web.config, alongside the period ESTAFF
        // built it for. The form names two preparers and one verifier.
        private static void AddIdentityBlock(Document doc,
            ReportDetailViewModel vm, EshReportSettings settings)
        {
            var table = NewTable(new[] { 1f, 1f, 1f });
            table.SpacingAfter = 16f;

            table.AddCell(PremisesCell(vm, settings));
            table.AddCell(OfficerCell("Prepared by", settings.PreparerFor(Text(vm.EmpName),Text(vm.EmpPosition),Text(vm.EmpJkkpNo))));
            table.AddCell(OfficerCell("Prepared by", settings.VerifierFor(Text(vm.DecidedByName),Text(vm.DecidedByPosition),Text(vm.DecidedByJkkpNo))));

            table.AddCell(ProvenanceCell(vm));
            table.AddCell(OfficerCell("Approved and Verified by",
                settings.Approver, 2));

            doc.Add(table);
        }

        private static PdfPCell PremisesCell(ReportDetailViewModel vm,
            EshReportSettings settings)
        {
            var cell = FormCell();

            AddLine(cell, "Month", vm.PeriodStart.ToString("MMM-yy"), true);
            AddLine(cell, "Report Date",
                DateText(vm.SubmittedDate ?? vm.CreatedDate));
            // The plant the return covers, falling back to the Esh:Plant
            // setting for a legacy personal report that has none.
            AddLine(cell, "Plant",
                Blank(!string.IsNullOrWhiteSpace(vm.PlantName)
                    ? vm.PlantName
                    : settings.Plant, null));
            AddLine(cell, "JKKP No", Blank(settings.Jkkp, null));

            return cell;
        }

        private static PdfPCell OfficerCell(string role, EshOfficer officer,
            int colspan = 1)
        {
            var cell = FormCell();
            cell.Colspan = colspan;

            cell.AddElement(Para(role.ToUpper(), Font(6.8f, Muted, true)));
            cell.AddElement(Para(
                Blank(officer != null ? Text(officer.Name) : null, null),
                Font(9.5f, Ink, true), 3f));

            AddLine(cell, "Position",
                Blank(officer != null ? Text(officer.Position) : null, null));
            AddLine(cell, "JKKP No",
                Blank(officer != null ? Text(officer.Jkkp) : null, null));

            return cell;
        }

        // ESTAFF's own record of the document: which report this is, what it
        // covers and where it sits in the approval workflow. The statutory form
        // has no field for it, but a printed copy that cannot be traced back to
        // the system it came from is not much use in an audit.
        private static PdfPCell ProvenanceCell(ReportDetailViewModel vm)
        {
            var cell = FormCell();

            AddLine(cell, "Reference", vm.Reference, true);
            AddLine(cell, "Period Covered", vm.PeriodText);
            AddLine(cell, "Prepared For", vm.PlantName);
            AddLine(cell, "Report Status", vm.Status.ToString(),
                false, ReportStatusColor(vm.Status));

            return cell;
        }

        private static PdfPCell FormCell()
        {
            return new PdfPCell
            {
                Border      = Rectangle.BOX,
                BorderWidth = 0.7f,
                BorderColor = Rule,
                Padding     = 9f
            };
        }

        // "PLANT   P21" - a small caps label with the value under it.
        private static void AddLine(PdfPCell cell, string label, string value,
            bool first = false, BaseColor color = null)
        {
            cell.AddElement(Para(label.ToUpper(), Font(6.8f, Muted, true),
                first ? 0f : 6f));
            cell.AddElement(Para(value, Font(9f, color ?? Ink, true), 2f));
        }

        // ══════════════════════════════════════════════════════
        // SECTIONS
        // ══════════════════════════════════════════════════════

        private static void AddSection(Document doc, EshSectionInfo info,
            List<ReportTaskDetailViewModel> tasks)
        {
            switch (info.Shape)
            {
                case EshSectionShape.WorkItem:
                    AddWorkItemTable(doc, info, TasksIn(tasks, info.Section));
                    break;

                case EshSectionShape.LayoutChange:
                    AddLayoutChangeTable(doc, info,
                        TasksIn(tasks, info.Section));
                    break;

                case EshSectionShape.Note:
                    AddNoteTable(doc, info, TasksIn(tasks, info.Section));
                    break;

                // Statistics and PurchaseRequest are filtered out in
                // GeneratePdf - ESTAFF holds no incident or purchase-order
                // record - so they never reach here.
            }
        }

        // Tasks print in the order the work fell due, which is the order an
        // officer filling the form in by hand would list them.
        private static List<ReportTaskDetailViewModel> TasksIn(
            List<ReportTaskDetailViewModel> tasks, EshSection section)
        {
            return tasks
                .Where(t => t.EffectiveSection == section)
                .OrderBy(t => t.EffectiveDate)
                .ThenBy(t => t.TaskId)
                .ToList();
        }

        // The numbered heading with the regulation it answers to, both fixed by
        // the form and printed verbatim, written as the opening rows of the
        // section's own grid rather than as a separate block above it.
        //
        // Two things follow from that, and both are the point. A heading can
        // never be left stranded at the foot of a page with its table overleaf,
        // because iText will not break a table between its header rows and the
        // first row under them. And a section long enough to run over a page
        // reintroduces itself at the top of the next one, so a reader who picks
        // up a continuation sheet knows which part of the return they are in.
        //
        // Returns how many header rows were written, so the caller can set
        // HeaderRows once it has added its column headings too.
        private static int AddSectionHeader(PdfPTable table,
            EshSectionInfo info, int columns, string note = null)
        {
            table.SpacingBefore = 14f;

            table.AddCell(SpanCell(columns, info.Heading,
                Font(9.5f, White, true), Ink, Ink, 8f));

            var rows = 1;

            if (!string.IsNullOrWhiteSpace(info.Regulation))
            {
                table.AddCell(SpanCell(columns, info.Regulation,
                    FontItalic(7.5f, InkSoft), Panel, Rule, 6f));
                rows++;
            }

            // Why a statutory section is blank, said inside the grid so it
            // travels with the heading it explains.
            if (!string.IsNullOrWhiteSpace(note))
            {
                table.AddCell(SpanCell(columns, note,
                    FontItalic(7.5f, Muted),
                    new BaseColor(255, 251, 235), Rule, 7f));
                rows++;
            }

            return rows;
        }

        private static PdfPCell SpanCell(int columns, string text, Font font,
            BaseColor background, BaseColor border, float padding)
        {
            var cell = new PdfPCell
            {
                Colspan         = columns,
                Border          = Rectangle.BOX,
                BorderWidth     = 0.7f,
                BorderColor     = border,
                BackgroundColor = background,
                Padding         = padding
            };

            cell.AddElement(Para(text, font));
            return cell;
        }

        // ── 1, 2, 4, 5 ─────────────────────────────────────────
        // No | Item | Issue / Concern | Action Taken | Remarks-or-Date
        private static void AddWorkItemTable(Document doc,
            EshSectionInfo info, List<ReportTaskDetailViewModel> tasks)
        {
            // Item is the widest prose column after Action Taken: as well as
            // the title and the task type it carries the attached CLIP record,
            // which is two lines of certificate number, plant and expiry.
            var table = NewTable(new[] { 0.4f, 1.95f, 1.75f, 2.0f, 1.0f });
            table.SpacingAfter = 4f;

            var header = AddSectionHeader(table, info, 5);

            AddTh(table, "No", Element.ALIGN_CENTER);
            AddTh(table, "Item");
            AddTh(table, "Issue / Concern");
            AddTh(table, "Action Taken");
            AddTh(table, info.TrailingHeader, Element.ALIGN_CENTER);

            table.HeaderRows = header + 1;

            if (!tasks.Any())
            {
                AddEmptyRow(table, 5);
                doc.Add(table);
                return;
            }

            var index = 0;
            foreach (var task in tasks)
            {
                index++;
                var bg = index % 2 == 0 ? Panel : White;

                AddTd(table, index.ToString(), bg, Muted,
                    align: Element.ALIGN_CENTER);
                table.AddCell(ItemCell(task, bg));
                AddTd(table, Blank(Text(task.Description), null), bg, InkSoft);
                table.AddCell(ActionCell(task, bg));
                table.AddCell(TrailingCell(task, info.TrailingHeader, bg));
            }

            doc.Add(table);
        }

        // ── 7 ──────────────────────────────────────────────────
        // No | Description | Issue / Concern | Recommendation
        private static void AddLayoutChangeTable(Document doc,
            EshSectionInfo info, List<ReportTaskDetailViewModel> tasks)
        {
            var table = NewTable(new[] { 0.42f, 2.3f, 2.35f, 2.0f });
            table.SpacingAfter = 4f;

            var header = AddSectionHeader(table, info, 4);

            AddTh(table, "No", Element.ALIGN_CENTER);
            AddTh(table, "Description");
            AddTh(table, "Issue / Concern");
            AddTh(table, "Recommendation");

            table.HeaderRows = header + 1;

            if (!tasks.Any())
            {
                AddEmptyRow(table, 4);
                doc.Add(table);
                return;
            }

            var index = 0;
            foreach (var task in tasks)
            {
                index++;
                var bg = index % 2 == 0 ? Panel : White;

                AddTd(table, index.ToString(), bg, Muted,
                    align: Element.ALIGN_CENTER);
                table.AddCell(ItemCell(task, bg));
                AddTd(table, Blank(Text(task.Description), null), bg, InkSoft);
                table.AddCell(ActionCell(task, bg));
            }

            doc.Add(table);
        }

        // ── 8, 9, 10 ───────────────────────────────────────────
        // No | Description | Remarks
        //
        // The narrow grid: the form gives these parts one prose column, so the
        // concern and what was done about it are printed together under the
        // item rather than lost.
        private static void AddNoteTable(Document doc, EshSectionInfo info,
            List<ReportTaskDetailViewModel> tasks)
        {
            var table = NewTable(new[] { 0.42f, 5.05f, 1.6f });
            table.SpacingAfter = 4f;

            var header = AddSectionHeader(table, info, 3);

            AddTh(table, "No", Element.ALIGN_CENTER);
            AddTh(table, "Description");
            AddTh(table, "Remarks", Element.ALIGN_CENTER);

            table.HeaderRows = header + 1;

            if (!tasks.Any())
            {
                AddEmptyRow(table, 3);
                doc.Add(table);
                return;
            }

            var index = 0;
            foreach (var task in tasks)
            {
                index++;
                var bg = index % 2 == 0 ? Panel : White;

                AddTd(table, index.ToString(), bg, Muted,
                    align: Element.ALIGN_CENTER);
                table.AddCell(NoteCell(task, bg));
                table.AddCell(TrailingCell(task, "Remarks", bg));
            }

            doc.Add(table);
        }

        // ══════════════════════════════════════════════════════
        // ROW CELLS
        // ══════════════════════════════════════════════════════

        // The item as the form asks for it: what it was, and underneath it the
        // recurring job it belongs to and the CLIP record it covers, both of
        // which name the thing being reported on more precisely than the title
        // does on its own.
        private static PdfPCell ItemCell(ReportTaskDetailViewModel task,
            BaseColor bg)
        {
            var cell = BodyCell(bg);

            cell.AddElement(Para(Text(task.Title), Font(8f, Ink, true)));

            if (!string.IsNullOrWhiteSpace(task.TaskListName))
                cell.AddElement(Para(Text(task.TaskListName),
                    Font(7f, Muted), 2f));

            if (task.ClipItem != null)
                AddClipBlock(cell, task.ClipItem);

            return cell;
        }

        // The narrow grid's one prose column: the item, the concern it was
        // raised for and what was done, stacked rather than dropped.
        private static PdfPCell NoteCell(ReportTaskDetailViewModel task,
            BaseColor bg)
        {
            var cell = BodyCell(bg);

            cell.AddElement(Para(Text(task.Title), Font(8f, Ink, true)));

            if (!string.IsNullOrWhiteSpace(task.TaskListName))
                cell.AddElement(Para(Text(task.TaskListName),
                    Font(7f, Muted), 2f));

            if (task.ClipItem != null)
                AddClipBlock(cell, task.ClipItem);

            if (!string.IsNullOrWhiteSpace(task.Description))
                cell.AddElement(Para(Text(task.Description),
                    Font(7.5f, InkSoft), 3f));

            var actions = task.ActionTakenText;
            if (!string.IsNullOrWhiteSpace(actions))
            {
                cell.AddElement(Para("ACTION TAKEN", Font(6.5f, Muted, true),
                    4f));
                cell.AddElement(Para(Text(actions), Font(7.5f, InkSoft), 2f));
            }

            return cell;
        }

        // Every remark recorded against the task's status changes, oldest
        // first. One line per remark: the form wants a description of what was
        // done, and the record of what was done is the sequence, not the last
        // entry in it.
        private static PdfPCell ActionCell(ReportTaskDetailViewModel task,
            BaseColor bg)
        {
            var cell = BodyCell(bg);
            var actions = task.ActionTakenText;

            if (string.IsNullOrWhiteSpace(actions))
            {
                cell.AddElement(Para("No action recorded.",
                    FontItalic(8f, Muted)));
                return cell;
            }

            cell.AddElement(Para(Text(actions), Font(8f, InkSoft)));
            return cell;
        }

        // The last column, which the form calls "Date" in some sections and
        // "Remarks" in others. Date wants the date the work landed; Remarks
        // wants where it stands, with the date under it.
        private static PdfPCell TrailingCell(ReportTaskDetailViewModel task,
            string header, BaseColor bg)
        {
            var cell = BodyCell(bg);

            if (string.Equals(header, "Date", StringComparison.OrdinalIgnoreCase))
            {
                cell.AddElement(Para(
                    task.EffectiveDate.ToString("dd MMM yyyy"),
                    Font(8f, task.IsOverdue ? Danger : InkSoft),
                    0f, Element.ALIGN_CENTER));
                return cell;
            }

            cell.AddElement(Para(task.StatusLabel,
                Font(8f, StatusColor(task.Status), true),
                0f, Element.ALIGN_CENTER));

            cell.AddElement(Para(
                task.EffectiveDate.ToString("dd MMM yyyy"),
                Font(7f, Muted), 2f, Element.ALIGN_CENTER));

            return cell;
        }

        private static PdfPCell BodyCell(BaseColor bg)
        {
            return new PdfPCell
            {
                BackgroundColor = bg,
                Border          = Rectangle.BOX,
                BorderWidth     = 0.5f,
                BorderColor     = Rule,
                Padding         = 6f
            };
        }

        // The attached CLIP record, printed under the item it covers, as the
        // supporting evidence behind whatever the task claims.
        //
        // A task covering a CLIP record is reporting on work tracked in another
        // system. Naming the record is not enough for a statutory return — what
        // an auditor needs is what EHS_PORTAL actually holds against it: which
        // phases have happened, when, who owns them and which document backs
        // each one. All of it is read live at render time, so this is CLIP's
        // account of the work rather than ours.
        private static void AddClipBlock(PdfPCell cell, ClipItemViewModel clip)
        {
            var accent = ClipColor(clip);

            var heading = clip.KindLabel + ": " + Text(clip.Title);
            if (!string.IsNullOrWhiteSpace(clip.Subtitle))
                heading += " (" + Text(clip.Subtitle) + ")";

            cell.AddElement(Para(heading, Font(7f, accent, true), 4f));

            // Where the record stands, on one line.
            var facts = new List<string>();

            if (!string.IsNullOrWhiteSpace(clip.PlantName))
                facts.Add("Plant " + Text(clip.PlantName));

            facts.Add("Expires " + Text(clip.ExpiryDateText)
                      + " (" + Text(clip.ExpiryStatus) + ")");

            // How long is left, spelled out: "Expiring Soon" alone does not
            // tell an auditor whether that means next week or next quarter.
            if (clip.ExpiryDate.HasValue)
                facts.Add(Text(clip.ExpiryText));

            if (!string.IsNullOrWhiteSpace(clip.ProcessStatus))
                facts.Add("CLIP status: " + Text(clip.ProcessStatus));

            // Certificates have no phases - what identifies the thing the
            // certificate covers is where it is and who runs it.
            if (!string.IsNullOrWhiteSpace(clip.Location))
                facts.Add("Location " + Text(clip.Location));

            if (!string.IsNullOrWhiteSpace(clip.Department))
                facts.Add("Dept " + Text(clip.Department));

            cell.AddElement(Para(string.Join("   |   ", facts),
                Font(6.8f, Muted), 1f));

            if (!clip.HasProgress) return;

            cell.AddElement(Para("SUPPORTING EVIDENCE (CLIP)",
                Font(6.2f, Muted, true), 4f));

            // One line per phase that has actually happened: what it was, where
            // it got to, when, who, and the document CLIP holds for it.
            foreach (var phase in clip.StartedPhases)
            {
                var line = Text(phase.Name) + " - "
                           + phase.StateLabel.ToLower() + ", "
                           + Text(phase.TimelineText);

                if (!string.IsNullOrWhiteSpace(phase.AssignedTo))
                    line += "  |  " + Text(phase.AssignedTo);

                if (phase.HasDocument)
                    line += "  |  " + Text(phase.DocumentName);

                cell.AddElement(Para(line,
                    Font(6.8f,
                        phase.State == ClipPhaseState.Complete
                            ? InkSoft
                            : Info),
                    1.5f));
            }

            // Everything still outstanding, named on one line rather than given
            // a line each. A phase nobody has started is evidence too — it is
            // what an unfinished item looks like — but it has nothing to say
            // beyond its own name.
            var outstanding = clip.OutstandingPhases;
            if (outstanding.Any())
            {
                cell.AddElement(Para(
                    "Not started: "
                    + string.Join(", ",
                        outstanding.Select(p => Text(p.Name))),
                    FontItalic(6.8f, Muted), 1.5f));
            }

            if (!string.IsNullOrWhiteSpace(clip.Remarks))
            {
                cell.AddElement(Para("CLIP remarks: " + Text(clip.Remarks),
                    Font(6.8f, Muted), 2f));
            }
        }

        private static BaseColor ClipColor(ClipItemViewModel clip)
        {
            if (clip.Urgency == ClipUrgency.Expired) return Danger;
            if (clip.Urgency == ClipUrgency.ExpiringSoon) return Warning;
            return Accent;
        }

        // ══════════════════════════════════════════════════════
        // APPROVAL AND SIGN-OFF
        // ══════════════════════════════════════════════════════

        // ESTAFF's workflow record. Not part of the statutory form, but the
        // printed copy is what gets filed, and when it was submitted and by
        // whose approval it stands is the first thing anyone asks of it.
        private static void AddApprovalTrail(Document doc,
            ReportDetailViewModel vm)
        {
            doc.Add(new Paragraph("Approval Trail", Font(9.5f, Ink, true))
            {
                SpacingBefore = 18f,
                SpacingAfter  = 6f
            });

            var table = NewTable(new[] { 1f, 1f, 1f, 1f });
            table.SpacingAfter = vm.Status == ReportStatus.Rejected ? 10f : 18f;

            AddMetaCell(table, "Created", DateTimeText(vm.CreatedDate));
            AddMetaCell(table, "Submitted", DateTimeText(vm.SubmittedDate));
            AddMetaCell(table, vm.Status == ReportStatus.Rejected
                ? "Reviewed" : "Approved", DateTimeText(vm.ApprovedDate));
            AddMetaCell(table, "Current Status", vm.Status.ToString());

            doc.Add(table);

            if (vm.Status == ReportStatus.Rejected
                && !string.IsNullOrWhiteSpace(vm.RejectionReason))
            {
                var reject = new PdfPTable(1) { WidthPercentage = 100 };
                var cell = new PdfPCell
                {
                    Border          = Rectangle.LEFT_BORDER,
                    BorderWidthLeft = 3f,
                    BorderColorLeft = Danger,
                    BackgroundColor = new BaseColor(254, 242, 242),
                    Padding         = 12f
                };
                cell.AddElement(Para("REASON FOR REJECTION",
                    Font(7f, Danger, true)));
                cell.AddElement(Para(Text(vm.RejectionReason),
                    Font(9f, Ink), 4f));
                reject.AddCell(cell);
                reject.SpacingAfter = 18f;
                doc.Add(reject);
            }
        }

        // Three signature blocks, naming the same officers as the letterhead so
        // the page that is signed matches the page that is filed.
        private static void AddSignOff(Document doc, ReportDetailViewModel vm,
            EshReportSettings settings)
        {
            var table = NewTable(new[] { 1f, 0.12f, 1f, 0.12f, 1f });
            table.SpacingBefore = 6f;
            table.KeepTogether = true;

            
            table.AddCell(OfficerCell("Prepared by", settings.PreparerFor(Text(vm.EmpName),Text(vm.EmpPosition),Text(vm.EmpJkkpNo))));
            table.AddCell(Spacer());
            table.AddCell(OfficerCell("Prepared by", settings.VerifierFor(Text(vm.DecidedByName),Text(vm.DecidedByPosition),Text(vm.DecidedByJkkpNo))));
            table.AddCell(Spacer());
            table.AddCell(SignatureCell("Approved and Verified by",
                settings.Approver));

            doc.Add(table);
        }

        private static PdfPCell Spacer()
        {
            return new PdfPCell { Border = Rectangle.NO_BORDER };
        }

        private static PdfPCell SignatureCell(string role, EshOfficer officer)
        {
            var cell = new PdfPCell
            {
                Border  = Rectangle.NO_BORDER,
                Padding = 0f
            };

            cell.AddElement(Para(role.ToUpper(), Font(6.8f, Muted, true)));

            // The rule people actually sign on.
            var line = new PdfPTable(1) { WidthPercentage = 100 };
            var lineCell = new PdfPCell(new Phrase(" ", Font(8f, Ink)))
            {
                Border            = Rectangle.BOTTOM_BORDER,
                BorderWidthBottom = 0.7f,
                BorderColorBottom = Muted,
                FixedHeight       = 34f
            };
            line.AddCell(lineCell);
            line.SpacingBefore = 4f;
            cell.AddElement(line);

            var name = officer != null ? Text(officer.Name) : null;
            cell.AddElement(Para(
                string.IsNullOrWhiteSpace(name) ? "Name" : name,
                Font(8.5f, string.IsNullOrWhiteSpace(name) ? Muted : Ink, true),
                4f));

            var caption = officer != null
                ? Join(Text(officer.Position), Text(officer.Jkkp))
                : null;

            cell.AddElement(Para(
                string.IsNullOrWhiteSpace(caption)
                    ? "Position and JKKP No"
                    : caption,
                Font(7f, Muted), 1f));

            cell.AddElement(Para("Date: ______________",
                Font(7f, Muted), 5f));

            return cell;
        }

        private static string Join(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first)) return second;
            if (string.IsNullOrWhiteSpace(second)) return first;
            return first + "  |  " + second;
        }

        // ══════════════════════════════════════════════════════
        // BUILDING BLOCKS
        // ══════════════════════════════════════════════════════

        private static PdfPTable NewTable(float[] widths)
        {
            var table = new PdfPTable(widths.Length)
            {
                WidthPercentage = 100,

                // A row that will not fit moves to the next page whole rather
                // than being cut in half by the page break. Splitting a row
                // leaves the tail of one cell stranded under a repeated header
                // with the rest of the line blank, which in a form that is
                // signed and filed reads as a missing entry rather than as a
                // continuation.
                //
                // The trade is that a single row taller than a whole page would
                // be dropped instead of split. Nothing here can reach that: a
                // row is one task's title, concern and remarks, and A4 holds
                // some ninety lines of it.
                SplitRows = false
            };
            table.SetWidths(widths);
            return table;
        }

        private static Paragraph Para(string text, Font font,
            float spacingBefore = 0f,
            int alignment = Element.ALIGN_LEFT,
            float spacingAfter = 0f)
        {
            return new Paragraph(text ?? "", font)
            {
                SpacingBefore = spacingBefore,
                SpacingAfter  = spacingAfter,
                Alignment     = alignment,
                Leading       = font.Size * 1.32f
            };
        }

        private static void AddMetaCell(PdfPTable table, string label,
            string value)
        {
            var cell = new PdfPCell
            {
                Border            = Rectangle.BOX,
                BorderWidth       = 0.7f,
                BorderColor       = Rule,
                Padding           = 8f
            };
            cell.AddElement(Para(label.ToUpper(), Font(6.8f, Muted, true)));
            cell.AddElement(Para(
                string.IsNullOrWhiteSpace(value) ? "-" : value,
                Font(9f, Ink, true), 3f));
            table.AddCell(cell);
        }

        // Composite rather than a plain Phrase cell: the widest heading on the
        // form runs to three lines, and a Phrase leaves them touching.
        private static void AddTh(PdfPTable table, string text,
            int align = Element.ALIGN_LEFT, float size = 7.5f)
        {
            var cell = new PdfPCell
            {
                BackgroundColor = Ink,
                Border          = Rectangle.BOX,
                BorderWidth     = 0.7f,
                BorderColor     = Ink,
                Padding         = 6f
            };

            cell.AddElement(Para(text.ToUpper(), Font(size, White, true),
                0f, align));

            table.AddCell(cell);
        }

        private static void AddTd(PdfPTable table, string text,
            BaseColor background, BaseColor color = null, bool bold = false,
            int align = Element.ALIGN_LEFT, float size = 8f,
            bool italic = false)
        {
            var font = italic
                ? FontItalic(size, color ?? Muted)
                : Font(size, color ?? Muted, bold);

            table.AddCell(new PdfPCell(new Phrase(text ?? "", font))
            {
                BackgroundColor     = background,
                Border              = Rectangle.BOX,
                BorderWidth         = 0.5f,
                BorderColor         = Rule,
                Padding             = 6f,
                HorizontalAlignment = align
            });
        }

        // A section of the form that ESTAFF has nothing to put in. The grid is
        // still printed, with one row saying so, rather than a heading with
        // nothing under it.
        private static void AddEmptyRow(PdfPTable table, int columns)
        {
            table.AddCell(new PdfPCell(new Phrase(
                "No items were recorded for this period.",
                FontItalic(8f, Muted)))
            {
                Colspan             = columns,
                Border              = Rectangle.BOX,
                BorderWidth         = 0.5f,
                BorderColor         = Rule,
                BackgroundColor     = Panel,
                Padding             = 12f,
                HorizontalAlignment = Element.ALIGN_CENTER
            });
        }

        private static Font Font(float size, BaseColor color,
            bool bold = false)
        {
            return FontFactory.GetFont(
                bold ? FontFactory.HELVETICA_BOLD : FontFactory.HELVETICA,
                size, color);
        }

        private static Font FontItalic(float size, BaseColor color)
        {
            return FontFactory.GetFont(
                FontFactory.HELVETICA_OBLIQUE, size, color);
        }

        // ══════════════════════════════════════════════════════
        // VALUES
        // ══════════════════════════════════════════════════════

        // The built-in PDF fonts encode Cp1252, which has no arrow. Remarks are
        // free text typed by employees, so the few characters the UI uses that
        // fall outside it are folded down to their plain equivalents rather
        // than dropped silently by the renderer.
        private static string Text(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            return value
                .Replace("→", "->")   // arrow, from TransitionText
                .Replace("←", "<-")
                .Replace("•", "-")    // bullet
                .Replace("…", "...")
                .Replace(" ", " ")    // non-breaking space
                .Replace("\r\n", "\n")
                .Trim();
        }

        // A field with nothing behind it prints as a rule to write on, not as
        // an empty cell that reads like an answer of "none".
        private static string Blank(string value, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value;
            return fallback ?? "______________";
        }

        private static string DateText(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("dd MMM yyyy")
                : "-";
        }

        private static string DateTimeText(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("dd MMM yyyy, h:mm tt")
                : "-";
        }

        private static BaseColor StatusColor(TaskStatus? status)
        {
            if (!status.HasValue) return Muted;

            switch (status.Value)
            {
                case TaskStatus.Complete:   return Accent;
                case TaskStatus.Overdue:    return Danger;
                case TaskStatus.InProgress: return Info;
                default:                    return Warning;
            }
        }

        private static BaseColor ReportStatusColor(ReportStatus status)
        {
            switch (status)
            {
                case ReportStatus.Approved:  return Accent;
                case ReportStatus.Rejected:  return Danger;
                case ReportStatus.Submitted: return Info;
                default:                     return Muted;
            }
        }

        // ══════════════════════════════════════════════════════
        // PAGE FURNITURE
        // ══════════════════════════════════════════════════════

        // Running header and numbered footer. A statutory return that is
        // printed and filed has to say what it is on every sheet, and a reader
        // has to be able to tell whether a page is missing.
        private class PageFurniture : PdfPageEventHelper
        {
            private readonly ReportDetailViewModel _vm;
            private readonly EshReportSettings _settings;
            private readonly string _generated;

            private PdfTemplate _pageCount;
            private BaseFont _font;
            private int _pages;

            public PageFurniture(ReportDetailViewModel vm,
                EshReportSettings settings)
            {
                _vm = vm;
                _settings = settings;
                _generated = DateTime.Now.ToString("dd MMM yyyy, h:mm tt");
            }

            public override void OnOpenDocument(PdfWriter writer,
                Document document)
            {
                _pageCount = writer.DirectContent.CreateTemplate(30f, 10f);
                _font = BaseFont.CreateFont(BaseFont.HELVETICA,
                    BaseFont.WINANSI, BaseFont.NOT_EMBEDDED);
            }

            public override void OnEndPage(PdfWriter writer,
                Document document)
            {
                _pages = writer.PageNumber;

                var canvas = writer.DirectContent;
                var left   = document.LeftMargin;
                var right  = document.PageSize.Width - document.RightMargin;

                // Running header, from the second page on - the first page
                // already carries the full letterhead.
                if (writer.PageNumber > 1)
                {
                    var top = document.PageSize.Height
                              - document.TopMargin + 18f;

                    var owner = string.IsNullOrWhiteSpace(_settings.Company)
                        ? "ESTAFF"
                        : Text(_settings.Company);

                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_LEFT,
                        new Phrase(owner + "  |  ESH "
                            + _vm.ReportTypeLabel + " Report",
                            Font(7.5f, Ink, true)),
                        left, top, 0);

                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_RIGHT,
                        new Phrase(_vm.Reference + "  |  " + _vm.PeriodText,
                            Font(7.5f, Muted)),
                        right, top, 0);

                    canvas.SetColorStroke(Rule);
                    canvas.SetLineWidth(0.7f);
                    canvas.MoveTo(left, top - 5f);
                    canvas.LineTo(right, top - 5f);
                    canvas.Stroke();
                }

                // Footer
                var footerY = document.BottomMargin - 16f;

                canvas.SetColorStroke(Rule);
                canvas.SetLineWidth(0.7f);
                canvas.MoveTo(left, footerY + 14f);
                canvas.LineTo(right, footerY + 14f);
                canvas.Stroke();

                ColumnText.ShowTextAligned(canvas, Element.ALIGN_LEFT,
                    new Phrase("OSH (SHO) Regulations 1997  |  "
                        + "Confidential - internal use only",
                        Font(7f, Muted)),
                    left, footerY, 0);

                ColumnText.ShowTextAligned(canvas, Element.ALIGN_RIGHT,
                    new Phrase("Generated " + _generated,
                        Font(7f, Muted)),
                    right, footerY, 0);

                // "Page 2 of " here, with the total stamped in once the
                // document knows how many pages it ran to.
                var centre = (left + right) / 2f;
                var label = new Phrase("Page " + writer.PageNumber + " of ",
                    Font(7f, Muted));
                var labelWidth = _font.GetWidthPoint(
                    "Page " + writer.PageNumber + " of ", 7f);

                ColumnText.ShowTextAligned(canvas, Element.ALIGN_LEFT, label,
                    centre - (labelWidth / 2f), footerY, 0);

                canvas.AddTemplate(_pageCount,
                    centre + (labelWidth / 2f), footerY);
            }

            public override void OnCloseDocument(PdfWriter writer,
                Document document)
            {
                // Counted from the pages actually written rather than the
                // writer's next-page number, which is one ahead here.
                _pageCount.BeginText();
                _pageCount.SetFontAndSize(_font, 7f);
                _pageCount.SetColorFill(Muted);
                _pageCount.SetTextMatrix(0, 0);
                _pageCount.ShowText(_pages.ToString());
                _pageCount.EndText();
            }
        }
    }
}
