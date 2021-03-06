using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ascsite.Core;
using ascsite.Core.MSLInterface;
using AscSite.Core;
using AscSite.Core.Interface.Database;
using AscSite.Core.Interface.DbInterface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Processor;

namespace ascsite.Pages
{
    public static class MSLProgramPool
    {
        static Dictionary<string, MSLInterface> pool = new Dictionary<string, MSLInterface>();

        public static string CreateProgram()
        {
            string id = Guid.NewGuid().ToString();
            pool.Add(id, new MSLInterface());
            return id;
        }

        public static MSLInterface GetById(string id)
        {
            if (!pool.ContainsKey(id))
                throw new KeyNotFoundException();
            else
                return pool[id];
        }

        internal static void KillById(string id)
        {
            var process = GetById(id);
            process.Kill();
            pool.Remove(id);
        }
    }
    public class MSLModel : PageModel
    {
        public List<string> SampleList { get; } = new List<string>()
        {
            "vectors.msl",
            "arrays.msl",
            "methods.msl",
            "gc.msl",
            "exceptions.msl",
        };
        public MSLModel()
        {
            CodeText = MSLInterface.GetSample("sample.msl");
        }
        [BindProperty] public string CodeText { get; set; }
        public string OutputMSL { get; set; }
        [BindProperty(SupportsGet = true)] public string Link { get; set; }
        [BindProperty(SupportsGet = true)] public string ReturnedId { get; set; } // currently unsupported

        public void OnGet()
        {
            if (Link != null)
            {
                try
                {
                    int linkId = LinkGenerator.FromString(Link).Integer;
                    var codeEntry = DbInterface.GetCodeLinkById(linkId);
                    if (codeEntry != null)
                        CodeText = codeEntry.Code;
                }
                catch { }
            }
        }
        public void OnPostCompileCode()
        {
            string id = MSLProgramPool.CreateProgram();
            MSLProgramPool.GetById(id).Execute(CodeText);
            ReturnedId = id;
            string output = string.Empty;
            var task = Task.Run(() => output = MSLProgramPool.GetById(id).PullOutput());
            bool timeout = !task.Wait(millisecondsTimeout: Const.LIMIT_MSL_EXECUTE_MS);
                
            MSLProgramPool.KillById(id);
            if (timeout)
                OutputMSL = Const.ERMSG_EXECUTE_TIMEOUT + ": " + Const.LIMIT_MSL_EXECUTE_MS.ToString() + "ms passed";
            else
                OutputMSL = output;
        }

        public string LoadSample(string file) => MSLInterface.GetSample(file);

        public void OnPostGenerateLink()
        {
            if (string.IsNullOrEmpty(CodeText)) return;
            int linkId = DbInterface.CreateCodeLink(CodeText);
            Link = LinkGenerator.FromInteger(linkId).Text;
            Response.Redirect("/msl?Link=" + Link); 
        }
    }
}
