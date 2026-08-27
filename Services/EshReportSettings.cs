using System;
using System.Configuration;
using System.IO;
using System.Web;

namespace ESTAFF.Services
{
    // The letterhead of the statutory ESH monthly report: who the premises
    // are, which officers prepared the return and who verified it.
    //
    // None of this is ESTAFF data. ApplicationUser has no name, position or
    // JKKP registration, and adding them would put a competency certificate
    // number on every employee record to serve two lines of a printed page.
    // It is deployment configuration, so it is read from Web.config and can be
    // corrected - a new SHO, a renewed certificate - without a rebuild.
    //
    // Nothing here throws or is required. A key that is missing prints as a
    // blank ruled line for someone to complete by hand, which is how the form
    // is filled in anyway.
    public class EshReportSettings
    {
        // ── Premises ───────────────────────────────────────────
        public string Company { get; private set; }
        public string Plant { get; private set; }
        public string Jkkp { get; private set; }

        // ── The two officers who prepare the return ────────────
        public EshOfficer Sho { get; private set; }
        public EshOfficer Officer { get; private set; }

        // ── Who verifies it ────────────────────────────────────
        public EshOfficer Approver { get; private set; }

        // Absolute path to a letterhead image, resolved from an app-relative
        // setting. Null unless the setting is present and the file is there -
        // a logo that has been moved must not take the whole report with it.
        public string LogoPath { get; private set; }

        public bool HasLogo
        {
            get { return !string.IsNullOrEmpty(LogoPath); }
        }

        public static EshReportSettings Load()
        {
            return new EshReportSettings
            {
                Company  = Read("Esh:Company"),
                Plant    = Read("Esh:Plant"),
                Jkkp     = Read("Esh:Jkkp"),
                Sho      = EshOfficer.Read("Esh:Sho"),
                Officer  = EshOfficer.Read("Esh:Officer"),
                Approver = EshOfficer.Read("Esh:Approver"),
                LogoPath = ResolveLogo(Read("Esh:LogoPath"))
            };
        }

        // The form names two preparers. The second is the officer who actually
        // did the work, so when the setting is left blank the report falls back
        // to the employee it was generated for rather than printing a stranger.
        public EshOfficer PreparerFor(string employeeName)
        {
            if (Officer != null && !string.IsNullOrWhiteSpace(Officer.Name))
                return Officer;

            return new EshOfficer
            {
                Name     = employeeName,
                Position = Officer != null ? Officer.Position : null,
                Jkkp     = Officer != null ? Officer.Jkkp : null
            };
        }

        private static string Read(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string ResolveLogo(string setting)
        {
            if (string.IsNullOrWhiteSpace(setting)) return null;

            try
            {
                var path = setting.StartsWith("~")
                           || setting.StartsWith("/")
                    ? HttpContext.Current != null
                        ? HttpContext.Current.Server.MapPath(setting)
                        : null
                    : setting;

                return !string.IsNullOrEmpty(path) && File.Exists(path)
                    ? path
                    : null;
            }
            catch (Exception)
            {
                // A misconfigured path is a blank letterhead, never a failed
                // download.
                return null;
            }
        }
    }

    // One signatory block: name, position and JKKP registration number.
    public class EshOfficer
    {
        public string Name { get; set; }
        public string Position { get; set; }
        public string Jkkp { get; set; }

        internal static EshOfficer Read(string prefix)
        {
            return new EshOfficer
            {
                Name     = Setting(prefix + ".Name"),
                Position = Setting(prefix + ".Position"),
                Jkkp     = Setting(prefix + ".Jkkp")
            };
        }

        private static string Setting(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
