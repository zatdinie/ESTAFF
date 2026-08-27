using System.Net;
using System.Web.Mvc;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;
using ESTAFF.Services;
using Microsoft.AspNet.Identity;

namespace ESTAFF.Controllers
{
    [Authorize]
    public class ClipController : Controller
    {
        private ApplicationDbContext _db = new ApplicationDbContext();
        
        // GET /CLIP/Progress?key=PM:3
        [HttpGet]
        public ActionResult Progress(string key)
        {
            ClipItemKind kind;
            int id;
            if (!ClipService.TryParseKey(key, out kind, out id))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            using (var clip = new ClipService(_db))
            {
                var item = kind == ClipItemKind.COF
                    ? clip.GetCofItem(id)
                    : clip.GetMonitoringItem(id);

                if (item == null) return HttpNotFound();

                // This used to refuse any record outside the user's
                // CLIP.UserPlants rows, matching the same restriction the task
                // form applied on write. Both are gone: ESTAFF now offers every
                // CLIP record for attaching and prints the attached record's
                // plant, expiry, phases and documents on the task and in the
                // report, so refusing to *link* to the same record was
                // inconsistent rather than protective.
                //
                // The redirect leaves ESTAFF entirely. EHS_PORTAL authenticates
                // and authorises its own pages, which is where that decision
                // belongs — this action only resolves the key to a URL.
                var url = ClipService.BuildProgressUrl(kind, id);
                if (url == null) return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable);

                return Redirect(url);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _db == null)
            {
                _db.Dispose();
                _db = null;
            }
            base.Dispose(disposing);
        }
    }
}